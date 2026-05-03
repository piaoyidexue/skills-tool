using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     营地生成器 —— ARPG模式专用。
///     在指定区域生成怪物小队，支持自由巡逻和索敌行为。
/// </summary>
public class CampSpawner : MonoBehaviour
{
    /// <summary>小队配置数组</summary>
    [SerializeField] private SquadConfig[] _squadConfigs;

    /// <summary>巡逻半径</summary>
    [SerializeField] private float _patrolRadius = 5f;

    /// <summary>巡逻区域中心</summary>
    [SerializeField] private Transform _patrolCenter;

    /// <summary>是否启用巡逻</summary>
    [SerializeField] private bool _enablePatrol = true;

    /// <summary>巡逻点数组</summary>
    private List<Vector3> _patrolPoints = new List<Vector3>();

    /// <summary>
    ///     开始生成小队。
    /// </summary>
    public void StartSpawning()
    {
        if (_squadConfigs == null || _squadConfigs.Length == 0)
        {
            Debug.LogWarning("[CampSpawner] No squad configs assigned.");
            return;
        }

        // 生成巡逻点
        GeneratePatrolPoints();

        // 为每个小队配置生成小队
        foreach (var squadConfig in _squadConfigs)
        {
            SpawnSquad(squadConfig);
        }
    }

    /// <summary>
    ///     停止生成小队。
    /// </summary>
    public void StopSpawning()
    {
        // 清理所有生成的怪物
        var monsters = GetComponentsInChildren<Transform>();
        foreach (var monster in monsters)
        {
            if (monster != transform && monster.GetComponent<MonsterAttributeSet>() != null)
            {
                Destroy(monster.gameObject);
            }
        }
    }

    /// <summary>
    ///     生成巡逻点。
    /// </summary>
    private void GeneratePatrolPoints()
    {
        _patrolPoints.Clear();

        if (_patrolCenter == null)
        {
            _patrolCenter = transform;
        }

        // 生成随机巡逻点
        for (int i = 0; i < 4; i++)
        {
            var angle = i * Mathf.PI * 0.5f;
            var point = _patrolCenter.position + new Vector3(
                Mathf.Cos(angle) * _patrolRadius,
                0f,
                Mathf.Sin(angle) * _patrolRadius
            );
            _patrolPoints.Add(point);
        }
    }

    /// <summary>
    ///     生成小队。
    /// </summary>
    private void SpawnSquad(SquadConfig squadConfig)
    {
        if (squadConfig == null || squadConfig.MonsterIDs.Count == 0)
            return;

        // 随机选择一个巡逻点作为生成位置
        var spawnIndex = UnityEngine.Random.Range(0, _patrolPoints.Count);
        var spawnPosition = _patrolPoints[spawnIndex];
        var spawnRotation = Quaternion.identity;

        // 创建小队并获取创建结果
        var spawnResults = MonsterFactory.CreateSquadWithContext(
            squadConfig.SquadID,
            squadConfig.Level > 0 ? squadConfig.Level : 1,
            spawnPosition,
            spawnRotation);

        // 获取怪物配置以获取 AiTier
        var squadConfigData = ConfigLoader.GetSquadConfig(squadConfig.SquadID);

        // 为每个怪物注入ARPG上下文
        foreach (var result in spawnResults)
        {
            if (result.Transform == null) continue;

            // 获取对应怪物的配置信息
            var monsterConfig = ConfigLoader.GetMonsterConfig(result.MonsterID);
            var aiTier = monsterConfig?.AiTier ?? "minion";

            // 创建ARPG上下文
            var context = MonsterSpawnContext.CreateForARPG(
                result.MonsterID,
                result.Level,
                aiTier,
                _patrolCenter.position,
                _patrolRadius,
                result.SquadID);

            // 通过 IMonsterInitializer 注入上下文
            var initializer = result.Transform.GetComponent<IMonsterInitializer>();
            initializer?.Initialize(context);
        }
    }
}