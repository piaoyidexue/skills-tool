using System.Collections;
using UnityEngine;

public class DelayNode : SkillNode
{
    public FloatBinding delaySeconds = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.DelaySeconds,
        DefaultValue = 0f
    };

    [System.NonSerialized] private float _elapsed;

    public override void OnEnter(SkillContext ctx)
    {
        _elapsed = 0f;
    }

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var delay = Mathf.Max(0f, delaySeconds.Resolve(ctx));
        if (delay <= 0f) return NodeTickResult.Success;

        _elapsed += deltaTime;
        if (_elapsed >= delay)
        {
            _elapsed = 0f;
            return NodeTickResult.Success;
        }

        return NodeTickResult.Running;
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        var delay = Mathf.Max(0f, delaySeconds.Resolve(ctx));
        if (delay <= 0f) yield break;

        yield return new WaitForSeconds(delay);
    }
}
