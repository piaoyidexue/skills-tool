using System.Collections;
using UnityEngine;

public class SubGraphNode : SkillNode
{
    public SkillGraph subGraph;

    public override IEnumerator Execute(SkillContext ctx)
    {
        if (subGraph == null)
        {
            Debug.LogError("[SubGraphNode] Missing subGraph reference.");
            yield break;
        }

        yield return SkillRunner.Instance.RunSkill(subGraph, ctx);
    }
}