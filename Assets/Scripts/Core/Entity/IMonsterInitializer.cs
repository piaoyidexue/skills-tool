/// <summary>
///     怪物初始化外观接口 —— 生成器与怪物之间的契约入口。
///     遵循架构红线：生成器绝不直接读写黑板，只传递上下文载荷。
/// </summary>
public interface IMonsterInitializer
{
    /// <summary>
    ///     初始化怪物状态（由生成器在出生时调用）。
    /// </summary>
    void Initialize(MonsterSpawnContext context);
}