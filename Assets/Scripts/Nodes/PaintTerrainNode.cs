using System.Collections;
using UnityEngine;

public class PaintTerrainNode : SkillNode
{
    public string blackboardKey = BBKey.TerrainTags;
    public StringBinding terrainTags = new() { LiteralValue = "scorch" };
    public bool append = true;
    public float defaultRadius = 1.5f;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var tags = terrainTags.Resolve(ctx);
        if (string.IsNullOrWhiteSpace(tags)) return NodeTickResult.Success;

        if (append)
        {
            var existing = ctx.Blackboard.GetString(blackboardKey, string.Empty);
            if (string.IsNullOrWhiteSpace(existing))
                ctx.Blackboard.SetValue(blackboardKey, tags);
            else if (!existing.Contains(tags)) ctx.Blackboard.SetValue(blackboardKey, existing + "|" + tags);
        }
        else
        {
            ctx.Blackboard.SetValue(blackboardKey, tags);
        }

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

    public override IEnumerator Execute(SkillContext ctx)
    {
        var tags = terrainTags.Resolve(ctx);
        if (string.IsNullOrWhiteSpace(tags)) yield break;

        if (append)
        {
            var existing = ctx.Blackboard.GetString(blackboardKey, string.Empty);
            if (string.IsNullOrWhiteSpace(existing))
                ctx.Blackboard.SetValue(blackboardKey, tags);
            else if (!existing.Contains(tags)) ctx.Blackboard.SetValue(blackboardKey, existing + "|" + tags);
        }
        else
        {
            ctx.Blackboard.SetValue(blackboardKey, tags);
        }

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
