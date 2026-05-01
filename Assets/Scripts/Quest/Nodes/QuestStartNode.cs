using UnityEngine;

// ============================================================
//  任务起始节点 (QuestStartNode)
//  任务图的入口点，等同于技能图的 StartNode。
//  QuestRunner 从此节点开始 Tick。
// ============================================================

public class QuestStartNode : QuestNodeBase
{
    public override QuestNodeResult Tick(float deltaTime)
    {
        // 起始节点直接返回成功，推进到下一个节点
        return QuestNodeResult.Success;
    }
}
