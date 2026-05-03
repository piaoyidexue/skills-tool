using System;
using System.Collections;
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
        public List<int> AffixIDs;

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

            // 计算怪物等级
            var effectiveLevel = _baseLevel + _currentWaveIndex * _levelIncrementPerWave;
            var finalLevel = waveConfig.GetEffectiveLevel(effectiveLevel);

            // 获取路径点
            var pathNodes = GetPathNodes(spawnPoint);

            // 创建小队并获取创建结果
            var spawnResults = MonsterFactory.CreateSquadWithContext(
                waveConfig.SquadID,
                finalLevel,
                spawnPoint.position,
                spawnPoint.rotation);

            // 初始化每个怪物
            foreach (var result in spawnResults)
            {
                if (result.Transform == null) continue;

                // 创建塔防上下文
                var context = MonsterSpawnContext.CreateForTowerDefense(
                    result.MonsterID,
                    result.Level,
                    result.AiTier,
                    pathNodes,
                    result.SquadID);

                // 通过 IMonsterInitializer 注入上下文
                var initializer = result.Transform.GetComponent<IMonsterInitializer>();
                initializer?.Initialize(context);

                // 应用词缀
                if (waveConfig.AffixIDs?.Count > 0)
                {
                    AffixApplier.ApplyAffixes(result.Transform, waveConfig.AffixIDs.ToArray());
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
    ///     获取路径点数组。
    /// </summary>
    private Transform[] GetPathNodes(Transform spawnPoint)
    {
        // 在实际项目中，这里会从场景中获取路径点
        // 为了演示，返回一个空数组
        return new Transform[0];
    }
}