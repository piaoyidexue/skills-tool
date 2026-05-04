using System;
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
    ///     生成小队。
    /// </summary>
    private void SpawnSquad(SquadConfig squadConfig)
    {
        if (squadConfig == null || squadConfig.MonsterIDs.Count == 0)
            return;

        if (_patrolCenter == null)
        {
            _patrolCenter = transform;
        }

        var spawnPosition = _patrolCenter.position + new Vector3(
            UnityEngine.Random.Range(-_patrolRadius, _patrolRadius),
            0f,
            UnityEngine.Random.Range(-_patrolRadius, _patrolRadius)
        );

        var context = new MonsterSpawnContext
        {
            TargetMode = AITargetMode.FreeRoam,
            PatrolCenter = _patrolCenter.position,
            PatrolRadius = _patrolRadius,
            SquadID = squadConfig.SquadID,
            EnableAggroPropagation = true
        };

        MonsterFactory.CreateSquad(
            squadConfig.SquadID,
            squadConfig.Level > 0 ? squadConfig.Level : 1,
            spawnPosition,
            Quaternion.identity,
            context);
    }
}