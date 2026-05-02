using System.Collections.Generic;

/// <summary>
///     投射物配置数据 —— 支持跨技能复用的独立配置。
///     在 ConfigLoader.Initialize() 时预加载到内存，
///     启动时覆盖 SkillConfig 中的内联字段，运行时零GC访问。
/// </summary>
public class ProjectileConfig
{
    public string ProjectileID;
    public string Name;
    public string PrefabKey;
    public int Trajectory;
    public float HitRadius;
    public float Lifetime;
    public float Gravity;
    public List<string> Tags = new();
}
