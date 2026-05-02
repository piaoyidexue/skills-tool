using UnityEngine;

[CreateAssetMenu(fileName = "PaintTerrainNode", menuName = "Skill System/Nodes/VFX/PaintTerrain")]
public class PaintTerrainNode : SkillNodeBase
{
    public StringBinding terrainTags = new() { LiteralValue = "scorch" };
    public float defaultRadius = 1.5f;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var tags = terrainTags.Resolve(ctx);
        if (string.IsNullOrWhiteSpace(tags)) return NodeTickResult.Success;

        // GAS架构：地形标签不再写入黑板，由 TerrainEffectSystem 直接处理
        var terrainSystem = TerrainEffectSystem.EnsureInstance();
        var position = ctx.Target != null ? ctx.Target.position : (ctx.Caster != null ? ctx.Caster.position : Vector3.zero);
        foreach (var rawTag in tags.Split('|'))
        {
            var tag = rawTag.Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var duration = ResolveTerrainDuration(tag);
            terrainSystem.Paint(tag, position, defaultRadius, duration);
        }

        return NodeTickResult.Success;
    }

    private static float ResolveTerrainDuration(string tag)
    {
        var terrainConfig = tag.ToLowerInvariant() switch
        {
            "scorch" => ConfigLoader.GetTerrainConfig("焦土地板"),
            "ice" => ConfigLoader.GetTerrainConfig("冰面地板"),
            "steam" => ConfigLoader.GetTerrainConfig("蒸汽地板"),
            "metal" => ConfigLoader.GetTerrainConfig("锻钢地板"),
            _ => null
        };

        return terrainConfig != null ? terrainConfig.DurationSeconds : 4f;
    }
}
