using UnityEngine;

/// <summary>
///     黑板初始化器 —— IMonsterInitializer 的标准实现。
///     负责将 MonsterSpawnContext 中的数据写入怪物的黑板系统。
///     遵循单一职责原则：只负责数据写入，不参与属性计算。
/// </summary>
[RequireComponent(typeof(BlackboardComponent))]
public class BlackboardInitializer : MonoBehaviour, IMonsterInitializer
{
    private BlackboardComponent _blackboardComponent;

    private void Awake()
    {
        _blackboardComponent = GetComponent<BlackboardComponent>();
    }

    /// <summary>
    ///     初始化怪物黑板数据。
    ///     由生成器在怪物出生时调用。
    /// </summary>
    /// <param name="context">生成上下文</param>
    public void Initialize(MonsterSpawnContext context)
    {
        if (_blackboardComponent == null)
        {
            _blackboardComponent = GetComponent<BlackboardComponent>();
        }

        var blackboard = _blackboardComponent.Blackboard;

        // 写入核心数据
        blackboard.SetValue("MonsterID", context.MonsterID);
        blackboard.SetValue("Level", context.Level);
        blackboard.SetValue("AiTier", context.AiTier);
        blackboard.SetValue("TargetMode", context.TargetMode);
        blackboard.SetValue("SquadID", context.SquadID);

        // 根据目标模式写入特定数据
        switch (context.TargetMode)
        {
            case AITargetMode.Waypoint:
                blackboard.SetValue("PathNodes", context.PathNodes);
                blackboard.SetValue("CurrentWaypointIndex", 0);
                blackboard.SetValue("WaypointProgress", 0f);
                break;

            case AITargetMode.FreeRoam:
                blackboard.SetValue("PatrolCenter", context.PatrolCenter);
                blackboard.SetValue("PatrolRadius", context.PatrolRadius);
                blackboard.SetValue("IsPatrolling", true);
                break;
        }

        // 仇恨联动设置
        blackboard.SetValue("EnableAggroPropagation", context.EnableAggroPropagation);
    }
}