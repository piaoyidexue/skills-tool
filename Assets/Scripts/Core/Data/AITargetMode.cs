/// <summary>
///     AI目标模式枚举 —— 怪物行为树的高层语义指令。
///     所有生成器（WaveSpawner/CampSpawner）和AI控制器必须统一使用此枚举，
///     禁止硬编码字符串（如 "Waypoint"）以保障类型安全与重构友好。
/// </summary>
public enum AITargetMode
{
    /// <summary>自由索敌模式（ARPG）</summary>
    FreeRoam,

    /// <summary>路径点导航模式（塔防）</summary>
    Waypoint,

    /// <summary>巡逻模式（ARPG）</summary>
    Patrol,

    /// <summary>守卫模式（ARPG）</summary>
    Guard,

    /// <summary>无目标模式（待机）</summary>
    None
}