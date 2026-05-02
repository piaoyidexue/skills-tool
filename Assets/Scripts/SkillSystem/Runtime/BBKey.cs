public static class BBKey
{
    // ============================================================
    //  GAS 架构红线：黑板仅允许存储图节点寻址所需的基础引用或控制计数器。
    //  禁止存储数值流转变量（如 DamageOverride）。
    //  禁止存储业务状态判定（如 IsCrit、ReactionSummary）。
    //  所有害数计算由 EffectSystem + Modifier Pipeline 接管。
    //
    //  架构原则（5 维复用方案）:
    //  - 图定义逻辑形状，表定义数值大小，黑板提供运行时上下文
    //  - 节点间禁止直接传参，通过黑板解耦输入输出
    //  - 节点通过 BBKeyRef 声明式引用黑板键，不硬编码字符串
    // ============================================================

    // ---- 允许：图控制计数器 ----
    public const string BranchCount = "BranchCount";
    public const string ExecutionCount = "ExecutionCount";

    // ---- 允许：目标引用（图寻址） ----
    public const string CurrentTarget = "CurrentTarget";
    public const string CurrentGraph = "CurrentGraph";
    public const string TargetCount = "TargetCount";
    public const string TargetList = "TargetList";
    public const string TargetDistance = "TargetDistance";

    // ---- 允许：施法管线状态（时间轴控制） ----
    public const string IsPreCasting = "IsPreCasting";
    public const string IsPostCasting = "IsPostCasting";
    public const string IsChanneling = "IsChanneling";
    public const string IsInterrupted = "IsInterrupted";
    public const string InterruptReason = "InterruptReason";
    public const string PreCastTime = "PreCastTime";
    public const string PostCastTime = "PostCastTime";
    public const string PreCastProgress = "PreCastProgress";
    public const string PostCastProgress = "PostCastProgress";

    // ---- 允许：通道控制 ----
    public const string ChannelProgress = "ChannelProgress";
    public const string ChannelDuration = "ChannelDuration";
    public const string ChannelCurrentTick = "ChannelCurrentTick";
    public const string ChannelTotalTicks = "ChannelTotalTicks";

    // ---- 允许：投射物寻址 ----
    public const string ProjectileActive = "ProjectileActive";
    public const string ProjectileHitPosition = "ProjectileHitPosition";
    public const string ProjectileHitTarget = "ProjectileHitTarget";

    // ---- 允许：技能配置注入（只读引用） ----
    public const string SkillID = "SkillID";

    // ---- 允许：动画同步标记 ----
    public const string AnimEvent = "AnimEvent";
    public const string AnimLastEventTime = "AnimLastEventTime";
    public const string AnimNormalizedTime = "AnimNormalizedTime";
    public const string AnimIsPlaying = "AnimIsPlaying";
    public const string AnimOnHit = "AnimOnHit";
    public const string AnimOnCastEnd = "AnimOnCastEnd";

    // ---- 允许：节点间数据流（维度4 解耦键） ----
    // 这些键用于节点间的上下文传递，替代直接参数传递
    // 节点通过 BBKeyRef 声明式引用这些键，不硬编码字符串
    public const string CustomBool1 = "CustomBool1";
    public const string CustomBool2 = "CustomBool2";
    public const string CustomFloat1 = "CustomFloat1";
    public const string CustomFloat2 = "CustomFloat2";
    public const string CustomString1 = "CustomString1";
    public const string CustomString2 = "CustomString2";

    /// <summary>生成 ChannelTick_{n} 键名</summary>
    public static string ChannelTick(int tickIndex) => $"ChannelTick_{tickIndex}";
}
