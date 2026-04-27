using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     AI 行为树图 —— 基于 NodeCanvas BehaviourTree 扩展。
    ///     作为 AI 行为逻辑的可视化容器，包含所有节点、连接和黑板变量。
    /// </summary>
    [GraphInfo(
        packageName = "Skills-Tool",
        docsURL = "",
        resourcesURL = "",
        forumsURL = "")]
    [CreateAssetMenu(fileName = "NewAITree", menuName = "Skill System/AI Behaviour Tree")]
    public class AIGraph : BehaviourTree
    {
        [SerializeField] private string aiTreeId = System.Guid.NewGuid().ToString("N");

        /// <summary>行为树唯一标识符</summary>
        public string AITreeId => string.IsNullOrWhiteSpace(aiTreeId) ? name : aiTreeId;

        /// <summary>行为树名称（用于调试）</summary>
        [SerializeField] private string treeName;
        public string TreeName
        {
            get => string.IsNullOrWhiteSpace(treeName) ? name : treeName;
            set => treeName = value;
        }

        /// <summary>行为树描述</summary>
        [TextArea(2, 4)]
        [SerializeField] private string treeDescription;
        public string TreeDescription
        {
            get => treeDescription;
            set => treeDescription = value;
        }

        /// <summary>AI 类型标签（用于分类筛选）</summary>
        [SerializeField] private AIType aiType = AIType.Combat;
        public AIType AIType => aiType;

        /// <summary>优先级（0-100，越大越优先）</summary>
        [SerializeField] [Range(0, 100)] private int priority = 50;
        public int Priority => priority;

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Skills/AI/Create AI Behaviour Tree", false, 100)]
        private static void Editor_CreateAIGraph()
        {
            var newGraph = EditorUtils.CreateAsset<AIGraph>();
            UnityEditor.Selection.activeObject = newGraph;
        }
#endif
    }

    /// <summary>AI 类型枚举</summary>
    public enum AIType
    {
        [Description("战斗AI")] Combat = 0,
        [Description("巡逻AI")] Patrol = 1,
        [Description("守卫AI")] Guard = 2,
        [Description("逃跑AI")] Flee = 3,
        [Description("空闲AI")] Idle = 4,
        [Description("Boss AI")] Boss = 5,
        [Description("辅助AI")] Support = 6,
    }
}
