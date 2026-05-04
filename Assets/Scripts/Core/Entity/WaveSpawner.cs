using System;
using System.Collections;
using UnityEngine;

/// <summary>
///     波次生成器 —— 塔防模式专用。
///     按时间间隔生成怪物波次，支持路径点导航。
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    /// <summary>波次配置数组</summary>
    [SerializeField] private WaveConfig[] _waveConfigs;

    /// <summary>当前波次索引</summary>
    private int _currentWaveIndex = 0;

    /// <summary>是否正在运行</summary>
    private bool _isRunning = false;

    /// <summary>全局等级调整（每波递增）</summary>
    [SerializeField] private int _baseLevel = 1;
    [SerializeField] private int _levelIncrementPerWave = 0;

    /// <summary>波次配置结构体</summary>
    [Serializable]
    public struct WaveConfig
    {
        /// <summary>波次索引</summary>
        public int WaveIndex;

        /// <summary>小队ID</summary>
        public int SquadID;

        /// <summary>生成点标签</summary>
        public string SpawnPointTag;

        /// <summary>生成延迟</summary>
        public float SpawnDelay;

        /// <summary>词缀池（管道符分隔）</summary>
        public string AffixPool;

        /// <summary>解析后的词缀ID数组</summary>
        public int[] AffixIDs;

        /// <summary>
        ///     怪物等级（如果不设置，则使用波次器的全局等级计算值）
        /// </summary>
        public int Level;

        /// <summary>
        ///     获取实际等级（优先使用配置的等级，否则使用计算值）
        /// </summary>
        public int GetEffectiveLevel(int calculatedLevel)
        {
            return Level > 0 ? Level : calculatedLevel;
        }
    }

    /// <summary>
    ///     开始波次生成。
    /// </summary>
    public void StartSpawning()
    {
        if (_isRunning) return;

        _isRunning = true;
        _currentWaveIndex = 0;
        StartCoroutine(SpawnWaveCoroutine());
    }

    /// <summary>
    ///     停止波次生成。
    /// </summary>
    public void StopSpawning()
    {
        _isRunning = false;
        StopAllCoroutines();
    }

    /// <summary>
    ///     波次生成协程。
    /// </summary>
    private IEnumerator SpawnWaveCoroutine()
    {
        while (_isRunning && _currentWaveIndex < _waveConfigs.Length)
        {
            var waveConfig = _waveConfigs[_currentWaveIndex];

            // 解析词缀池
            ParseAffixPool(waveConfig);

            // 查找生成点
            var spawnPoint = FindSpawnPoint(waveConfig.SpawnPointTag);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[WaveSpawner] Spawn point not found: {waveConfig.SpawnPointTag}");
                _currentWaveIndex++;
                yield return null;
                continue;
            }

            // 计算怪物等级
            var effectiveLevel = _baseLevel + _currentWaveIndex * _levelIncrementPerWave;
            var finalLevel = waveConfig.GetEffectiveLevel(effectiveLevel);

            // 获取路径点
            var pathNodes = GetPathNodes(spawnPoint);

            // 创建塔防上下文
            var context = new MonsterSpawnContext
            {
                TargetMode = AITargetMode.Waypoint,
                PathNodes = pathNodes,
                SquadID = waveConfig.SquadID,
                EnableAggroPropagation = true
            };

            // 批量创建小队（包含初始化和应用词缀）
            MonsterFactory.CreateSquad(
                waveConfig.SquadID,
                finalLevel,
                spawnPoint.position,
                spawnPoint.rotation,
                context,
                waveConfig.AffixIDs);

            // 等待下一次波次
            _currentWaveIndex++;
            yield return new WaitForSeconds(waveConfig.SpawnDelay);
        }
    }

    /// <summary>
    ///     解析词缀池字符串。
    /// </summary>
    private void ParseAffixPool(WaveConfig config)
    {
        config.AffixIDs = Array.Empty<int>();
        if (string.IsNullOrEmpty(config.AffixPool)) return;

        var affixIds = config.AffixPool.Split('|');
        var ids = new System.Collections.Generic.List<int>();
        foreach (var idStr in affixIds)
        {
            if (int.TryParse(idStr.Trim(), out int id))
            {
                ids.Add(id);
            }
        }
        config.AffixIDs = ids.ToArray();
    }

    /// <summary>
    ///     查找生成点。
    /// </summary>
    private Transform FindSpawnPoint(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return transform;

        var children = GetComponentsInChildren<Transform>();
        foreach (var child in children)
        {
            if (child.CompareTag(tag))
                return child;
        }

        var namedChild = transform.Find(tag);
        if (namedChild != null)
            return namedChild;

        return transform;
    }

    /// <summary>
    ///     获取路径点数组。
    /// </summary>
    private Transform[] GetPathNodes(Transform spawnPoint)
    {
        return new Transform[0];
    }
}