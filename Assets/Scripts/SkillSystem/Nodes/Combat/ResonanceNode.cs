using UnityEngine;

[CreateAssetMenu(fileName = "ResonanceNode", menuName = "Skill System/Nodes/Combat/Resonance")]
public class ResonanceNode : SkillNodeBase
{
    public StringBinding resonanceTags = new() { LiteralValue = "row_focus" };
    public bool defaultActive = true;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        // GAS架构：共鸣标签不再写入黑板，由 ResonanceNode 直接持有。
        // 如需向 GEHost 注入标签，可在此处调用 ctx.Target.GetComponent<GEHost>()?.ApplyEffect(...)
        var tags = resonanceTags.Resolve(ctx);
        if (defaultActive && !string.IsNullOrWhiteSpace(tags) && ctx.Target != null)
        {
            var geHost = ctx.Target.GetComponent<GEHost>();
            if (geHost != null)
            {
                var geConfig = new GEConfig
                {
                    GEId = $"Resonance_{tags}".GetHashCode(),
                    Name = $"Resonance_{tags}",
                    DurationPolicy = GEDurationPolicy.Instant
                };
                foreach (var tag in tags.Split('|'))
                    geConfig.GrantedTags.Add(tag.Trim());
                geHost.ApplyEffect(geConfig, ctx.Caster);
            }
        }

        return NodeTickResult.Success;
    }
}