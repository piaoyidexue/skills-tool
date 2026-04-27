using UnityEngine;

/// <summary>
///     技能拥有者 —— 挂载在角色/实体上，管理当前技能目标和图形引用。
///     实际释放流程由 SkillCaster 接管（状态机 + 打断 + 前腰/后摇/吟唱）。
///     保留旧 API 兼容：CastSkill() 内部委托给 SkillCaster.TryCast()。
/// </summary>
[RequireComponent(typeof(SkillRunner))]
[RequireComponent(typeof(SkillCaster))]
public class SkillOwner : MonoBehaviour
{
    [Header("Skill")]
    public int skillID = 1001;

    [Tooltip("当前目标（由锁定系统/玩家输入驱动）")]
    public Transform currentTarget;

    [Header("Graph")]
    [Tooltip("如果 SkillConfig.GraphPath 无效，退回此图")]
    public SkillGraph fallbackGraph;

    [Tooltip("优先使用配置中的图表路径")]
    public bool useConfigGraph = true;

    // -- internal --
    private SkillCaster _caster;

    /// <summary>施法者组件引用</summary>
    public SkillCaster Caster => _caster;

    private void Awake()
    {
        _caster = GetComponent<SkillCaster>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            CastSkill();
    }

    /// <summary>
    ///     释放技能（兼容旧 API）。
    ///     内部委托给 SkillCaster.TryCast()。
    /// </summary>
    /// <returns>是否成功进入施法流程</returns>
    public bool CastSkill()
    {
        return _caster.TryCast(skillID, currentTarget);
    }

    /// <summary>
    ///     用指定 ID 释放技能。
    /// </summary>
    public bool CastSkill(int overrideSkillId, Transform target = null)
    {
        var t = target ?? currentTarget;
        return _caster.TryCast(overrideSkillId, t);
    }

    /// <summary>
    ///     打断当前技能。
    /// </summary>
    public void InterruptSkill(InterruptReason reason = InterruptReason.Manual)
    {
        _caster.Interrupt(reason);
    }
}
