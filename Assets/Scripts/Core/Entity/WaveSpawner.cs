using System;
using System.Collections.Generic;
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

    /// <summary>波次配置结构体</summary>
    [System.Serializable]
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
        public List<int> AffixIDs ;
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
    private System.Collections.IEnumerator SpawnWaveCoroutine()
    {
        while (_isRunning && _currentWaveIndex < _waveConfigs.Length)
        {
            var waveConfig = _waveConfigs[_currentWaveIndex];
            waveConfig.AffixIDs ??= new List<int>();

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

            // 生成小队
            var squadTransforms = MonsterFactory.CreateSquad(waveConfig.SquadID, spawnPoint.position, spawnPoint.rotation);

            // 为每个怪物注入路径点信息（用于塔防AI）
            foreach (var transform in squadTransforms)
            {
                if (transform != null)
                {
                    var context = CreatePathContext(spawnPoint, waveConfig.SquadID);
                    var initializer = transform.GetComponent<IMonsterInitializer>();
                    if (initializer != null)
                    {
                        initializer.Initialize(context);
                    }
                    
                    // 应用词缀
                    if (waveConfig.AffixIDs?.Count > 0)
                    {
                        AffixApplier.ApplyAffixes(transform, waveConfig.AffixIDs.ToArray());
                    }
                }
            }

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
        config.AffixIDs ??= new List<int>();
        config.AffixIDs.Clear();
        if (string.IsNullOrEmpty(config.AffixPool)) return;

        var affixIds = config.AffixPool.Split('|');
        foreach (var idStr in affixIds)
        {
            if (int.TryParse(idStr.Trim(), out int id))
            {
                config.AffixIDs.Add(id);
            }
        }
    }

    /// <summary>
    ///     查找生成点。
    /// </summary>
    private Transform FindSpawnPoint(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return transform;

        // 尝试查找带有指定标签的子对象
        var children = GetComponentsInChildren<Transform>();
        foreach (var child in children)
        {
            if (child.CompareTag(tag))
                return child;
        }

        // 尝试查找同名子对象
        var namedChild = transform.Find(tag);
        if (namedChild != null)
            return namedChild;

        return transform;
    }



    /// <summary>
    ///     创建路径上下文载荷。
    /// </summary>
    private MonsterSpawnContext CreatePathContext(Transform spawnPoint, int squadId)
    {
        return new MonsterSpawnContext
        {
            TargetMode = AITargetMode.Waypoint,
            PathNodes = GetPathNodes(spawnPoint),
            SquadID = squadId,
            EnableAggroPropagation = true
        };
    }

    /// <summary>
    ///     获取路径点数组。
    /// </summary>
    private Transform[] GetPathNodes(Transform spawnPoint)
    {
        // 在实际项目中，这里会从场景中获取路径点
        // 为了演示，返回一个空数组
        return new Transform[0];
    }
}