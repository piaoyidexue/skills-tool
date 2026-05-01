using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// ============================================================
//  SaveManager —— 统一存档管理器
//  接口搜集 + 全局字典序列化 + AES 加密 + 安全备份。
//
//  设计准则：
//  - 低成本接入：新模块仅需实现 ISaveable，自动被搜集
//  - 安全落盘：写入临时文件 → 验证 → 替换原文件，防止断电死档
//  - AES 加密：防止玩家轻易修改本地数据
//  - JSON 序列化：可读性好，调试方便
//  - 模块解耦：SaveManager 不关心具体业务数据的结构
// ============================================================

/// <summary>
///     存档数据根结构 —— JSON 序列化的顶层对象。
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>存档版本号（用于迁移兼容）</summary>
    public int Version = 1;

    /// <summary>存档时间戳</summary>
    public string Timestamp;

    /// <summary>
    ///     全局键值对字典。
    ///     Key: ISaveable.SaveKey，Value: 该模块的快照数据。
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> Modules = new();
}

/// <summary>
///     统一存档管理器 —— 负责搜集、序列化、加密、落盘的完整流程。
///     保存：遍历场景/系统树中所有 ISaveable → CaptureSnapshot → 序列化 → 加密 → 写入文件。
///     加载：读取文件 → 解密 → 反序列化 → 按 Key 分发给各 ISaveable → RestoreSnapshot。
/// </summary>
public class SaveManager : MonoBehaviour
{
    // ──────────── 单例 ────────────

    public static SaveManager Instance { get; private set; }

    // ──────────── 配置 ────────────

    [Header("=== 存档配置 ===")]
    [Tooltip("存档文件名（不含扩展名）")]
    [SerializeField] private string _saveFileName = "save_slot_0";

    [Tooltip("是否启用 AES 加密")]
    [SerializeField] private bool _enableEncryption = true;

    [Tooltip("是否启用安全备份（写入临时文件后替换）")]
    [SerializeField] private bool _enableSafeWrite = true;

    [Header("=== 调试 ===")]
    [SerializeField] private bool _showDebugInfo;

    // ──────────── AES 密钥（16字节 = AES-128） ────────────

    // 注意：生产环境应从更安全的地方获取密钥，此处仅为演示
    private static readonly byte[] AesKey = Encoding.UTF8.GetBytes("SkillToolAesKey1"); // 16 bytes
    private static readonly byte[] AesIv = Encoding.UTF8.GetBytes("SkillToolAesIV__1"); // 16 bytes

    // ──────────── 存档路径 ────────────

    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    private string SaveFilePath => Path.Combine(SaveDirectory, $"{_saveFileName}.sav");

    private string TempFilePath => Path.Combine(SaveDirectory, $"{_saveFileName}.tmp");

    private string BackupFilePath => Path.Combine(SaveDirectory, $"{_saveFileName}.bak");

    // ──────────── 事件 ────────────

    /// <summary>保存完成事件</summary>
    public event Action<bool> OnSaveCompleted;

    /// <summary>加载完成事件</summary>
    public event Action<bool> OnLoadCompleted;

    // ──────────── 生命周期 ────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureSaveDirectory();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────── 公开 API ────────────

    /// <summary>
    ///     保存游戏。
    ///     流程：搜集 ISaveable → 生成快照 → 序列化 → 加密 → 安全写入。
    /// </summary>
    /// <returns>是否保存成功</returns>
    public bool SaveGame()
    {
        try
        {
            var saveData = CaptureAllSnapshots();

            var json = JsonUtility.ToJson(saveData, true);

            if (_enableEncryption)
            {
                json = Encrypt(json);
            }

            if (_enableSafeWrite)
            {
                return SafeWrite(json);
            }

            File.WriteAllText(SaveFilePath, json);
            OnSaveCompleted?.Invoke(true);
            Debug.Log($"[SaveManager] Game saved to {SaveFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Save failed: {ex}");
            OnSaveCompleted?.Invoke(false);
            return false;
        }
    }

    /// <summary>
    ///     加载游戏。
    ///     流程：读取文件 → 解密 → 反序列化 → 分发给各 ISaveable。
    /// </summary>
    /// <returns>是否加载成功</returns>
    public bool LoadGame()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.LogWarning($"[SaveManager] Save file not found: {SaveFilePath}");
                OnLoadCompleted?.Invoke(false);
                return false;
            }

            var json = File.ReadAllText(SaveFilePath);

            if (_enableEncryption)
            {
                json = Decrypt(json);
            }

            var saveData = JsonUtility.FromJson<SaveData>(json);
            if (saveData == null)
            {
                Debug.LogError("[SaveManager] Failed to deserialize save data");
                OnLoadCompleted?.Invoke(false);
                return false;
            }

            RestoreAllSnapshots(saveData);
            OnLoadCompleted?.Invoke(true);
            Debug.Log($"[SaveManager] Game loaded from {SaveFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Load failed: {ex}");
            // 尝试从备份恢复
            if (TryRestoreFromBackup())
            {
                OnLoadCompleted?.Invoke(true);
                return true;
            }

            OnLoadCompleted?.Invoke(false);
            return false;
        }
    }

    /// <summary>
    ///     检查存档文件是否存在。
    /// </summary>
    public bool HasSaveFile() => File.Exists(SaveFilePath);

    /// <summary>
    ///     删除存档文件。
    /// </summary>
    public void DeleteSave()
    {
        if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
        if (File.Exists(BackupFilePath)) File.Delete(BackupFilePath);
        if (File.Exists(TempFilePath)) File.Delete(TempFilePath);
    }

    /// <summary>
    ///     获取存档信息（不完整加载）。
    /// </summary>
    public string GetSaveInfo()
    {
        if (!File.Exists(SaveFilePath)) return "No save file";

        try
        {
            var json = File.ReadAllText(SaveFilePath);
            if (_enableEncryption) json = Decrypt(json);
            var data = JsonUtility.FromJson<SaveData>(json);
            return data != null ? $"V{data.Version} | {data.Timestamp} | {data.Modules.Count} modules" : "Invalid save";
        }
        catch
        {
            return "Corrupted save file";
        }
    }

    // ──────────── 搜集与还原 ────────────

    /// <summary>
    ///     搜集所有 ISaveable 的快照。
    /// </summary>
    private SaveData CaptureAllSnapshots()
    {
        var saveData = new SaveData
        {
            Version = 1,
            Timestamp = DateTime.Now.ToString("o")
        };

        // 搜集场景中的 ISaveable
        var saveables = FindAllSaveables();

        foreach (var saveable in saveables)
        {
            try
            {
                var snapshot = saveable.CaptureSnapshot();
                if (snapshot != null && snapshot.Count > 0)
                {
                    saveData.Modules[saveable.SaveKey] = snapshot;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] CaptureSnapshot failed for {saveable.SaveKey}: {ex}");
            }
        }

        // 搜集静态系统数据（如 QuestRunner 的已完成任务列表）
        CaptureStaticSystemData(saveData);

        return saveData;
    }

    /// <summary>
    ///     将快照数据还原到各 ISaveable。
    /// </summary>
    private void RestoreAllSnapshots(SaveData saveData)
    {
        var saveables = FindAllSaveables();

        // 建立 Key → ISaveable 映射
        var lookup = new Dictionary<string, ISaveable>(saveables.Count);
        foreach (var saveable in saveables)
        {
            lookup[saveable.SaveKey] = saveable;
        }

        // 按 Key 分发快照
        foreach (var kvp in saveData.Modules)
        {
            if (lookup.TryGetValue(kvp.Key, out var saveable))
            {
                try
                {
                    saveable.RestoreSnapshot(kvp.Value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveManager] RestoreSnapshot failed for {kvp.Key}: {ex}");
                }
            }
            else
            {
                Debug.LogWarning($"[SaveManager] No ISaveable found for key: {kvp.Key}");
            }
        }

        // 还原静态系统数据
        RestoreStaticSystemData(saveData);
    }

    /// <summary>
    ///     搜集场景中所有 ISaveable 实例。
    /// </summary>
    private List<ISaveable> FindAllSaveables()
    {
        // 方式1：通过 MonoBehaviour 查找
        var results = new List<ISaveable>();
        var monoBehaviours = FindObjectsOfType<MonoBehaviour>();

        foreach (var mb in monoBehaviours)
        {
            if (mb is ISaveable saveable)
                results.Add(saveable);
        }

        return results;
    }

    // ──────────── 静态系统数据 ────────────

    private void CaptureStaticSystemData(SaveData saveData)
    {
        // 任务系统静态数据
        var questData = new Dictionary<string, object>();
        var completedIds = QuestRunner.CompletedQuests;
        if (completedIds != null && completedIds.Count > 0)
        {
            questData["CompletedQuestIds"] = string.Join(",", completedIds);
        }

        if (questData.Count > 0)
        {
            saveData.Modules["QuestSystem.Static"] = questData;
        }
    }

    private void RestoreStaticSystemData(SaveData saveData)
    {
        // 任务系统静态数据由 QuestRunner 的 ISaveable 实现处理
        // 此处为扩展点，用于非 MonoBehaviour 的静态系统
    }

    // ──────────── AES 加密/解密 ────────────

    private static string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.IV = AesIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return Convert.ToBase64String(cipherBytes);
    }

    private static string Decrypt(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.IV = AesIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    // ──────────── 安全写入 ────────────

    /// <summary>
    ///     安全写入流程：
    ///     1. 将当前存档文件备份为 .bak
    ///     2. 写入临时文件 .tmp
    ///     3. 验证临时文件可读
    ///     4. 替换原文件
    /// </summary>
    private bool SafeWrite(string content)
    {
        EnsureSaveDirectory();

        // 步骤1：备份当前存档
        if (File.Exists(SaveFilePath))
        {
            File.Copy(SaveFilePath, BackupFilePath, true);
        }

        // 步骤2：写入临时文件
        File.WriteAllText(TempFilePath, content);

        // 步骤3：验证临时文件
        try
        {
            var verify = File.ReadAllText(TempFilePath);
            if (string.IsNullOrEmpty(verify))
            {
                Debug.LogError("[SaveManager] SafeWrite verification failed: temp file is empty");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] SafeWrite verification failed: {ex}");
            return false;
        }

        // 步骤4：替换原文件
        try
        {
            if (File.Exists(SaveFilePath))
                File.Delete(SaveFilePath);

            File.Move(TempFilePath, SaveFilePath);

            // 写入成功后删除临时文件
            if (File.Exists(TempFilePath))
                File.Delete(TempFilePath);

            OnSaveCompleted?.Invoke(true);
            Debug.Log($"[SaveManager] Game saved safely to {SaveFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] SafeWrite replace failed: {ex}");

            // 尝试从备份恢复
            if (File.Exists(BackupFilePath))
            {
                try
                {
                    File.Copy(BackupFilePath, SaveFilePath, true);
                    Debug.Log("[SaveManager] Restored from backup after failed write");
                }
                catch
                {
                    // 静默失败
                }
            }

            OnSaveCompleted?.Invoke(false);
            return false;
        }
    }

    /// <summary>
    ///     尝试从备份文件恢复。
    /// </summary>
    private bool TryRestoreFromBackup()
    {
        if (!File.Exists(BackupFilePath)) return false;

        try
        {
            File.Copy(BackupFilePath, SaveFilePath, true);

            var json = File.ReadAllText(SaveFilePath);
            if (_enableEncryption) json = Decrypt(json);
            var saveData = JsonUtility.FromJson<SaveData>(json);

            if (saveData != null)
            {
                RestoreAllSnapshots(saveData);
                Debug.Log("[SaveManager] Restored from backup successfully");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Backup restore failed: {ex}");
        }

        return false;
    }

    // ──────────── 辅助 ────────────

    private void EnsureSaveDirectory()
    {
        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);
    }

    // ──────────── 调试面板 ────────────

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!_showDebugInfo) return;

        GUILayout.BeginArea(new Rect(730, 720, 350, 150));
        GUILayout.Label("<b>Save Manager</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"  File: {SaveFilePath}");
        GUILayout.Label($"  Exists: {HasSaveFile()}");
        if (HasSaveFile()) GUILayout.Label($"  Info: {GetSaveInfo()}");
        GUILayout.EndArea();
    }
#endif
}
