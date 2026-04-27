using System;
using System.Collections.Generic;

namespace SkillAI
{
    /// <summary>
    ///     AI 行为树 CSV 行配置 —— 从 AITree.csv 解析的单行数据。
    ///     每行描述行为树中的一个节点（属于某个行为链）。
    /// </summary>
    [Serializable]
    public class AITreeRowConfig
    {
        /// <summary>行为树唯一标识（同 tree_id 的行属于同一棵树）</summary>
        public string TreeId;

        /// <summary>行为树名称（编辑器显示用）</summary>
        public string TreeName;

        /// <summary>AI 类型标签</summary>
        public AIType AIType;

        /// <summary>优先级（0-100）</summary>
        public int Priority;

        /// <summary>更新间隔（秒）</summary>
        public float UpdateInterval;

        /// <summary>行为链序号（决定优先级，越小越高）</summary>
        public int ChainOrder;

        /// <summary>节点在链内的顺序</summary>
        public int NodeOrder;

        /// <summary>节点类名（如 MoveTo、AttackTarget、Sequencer）</summary>
        public string NodeClass;

        /// <summary>节点参数（key1=value1;key2=value2 格式）</summary>
        public string Params;
    }

    /// <summary>
    ///     AI 行为树定义 —— 由多行 AITreeRowConfig 聚合而成。
    /// </summary>
    [Serializable]
    public class AITreeDefinition
    {
        public string TreeId;
        public string TreeName;
        public AIType AIType;
        public int Priority;
        public float UpdateInterval;

        /// <summary>按 chain_order 分组的节点链</summary>
        public List<AITreeChain> Chains = new();
    }

    /// <summary>
    ///     行为链 —— 一组按 node_order 排序的顺序执行节点。
    ///     生成时包裹在一个 Sequencer 中。
    /// </summary>
    [Serializable]
    public class AITreeChain
    {
        public int ChainOrder;
        public List<AITreeRowConfig> Nodes = new();
    }
}
