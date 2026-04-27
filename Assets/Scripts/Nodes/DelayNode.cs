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

    public override IEnumerator Execute(SkillContext ctx)
    {
        var delay = Mathf.Max(0f, delaySeconds.Resolve(ctx));
        if (delay <= 0f) yield break;

        yield return new WaitForSeconds(delay);
    }
}