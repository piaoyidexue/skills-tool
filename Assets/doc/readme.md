# 商业级技能系统落地执行方案（CSV数据驱动 + NodeCanvas逻辑 + 高性能表现）

---

## 0. 项目目标与验收标准

**目标（4–6周）：**
- 在编辑器中通过拖拽连线创建技能（NodeCanvas CanvasCore）
- 支持顺序、并行、条件、子图（模块复用）
- 所有数值参数来自 CSV 表格，逻辑完全可视化
- VFX 使用 Shader + 对象池，中端机连续触发 10 次无明显卡顿
- 提供调试工具：实时执行节点高亮、Blackboard 变量查看

**验收标准：**
- 新增技能只需配置 CSV 一行数据 + 拼装 NodeCanvas 子图，**零代码改动**
- 同一子图被 ≥3 个技能复用
- 连锁闪电 10 次同时触发，帧率稳定且无明显 GC Alloc

---

## 1. 总体架构（严格分层）

```text
┌─────────────────┐
│   CSV 数值层     │  ← 策划维护，技能所有参数（伤害、范围、冷却等）
├─────────────────┤
│ NodeCanvas 逻辑层│  ← 策划/程序合作，定义执行流程与条件
├─────────────────┤
│  SkillRuntime   │  ← 程序维护，协程驱动，支持暂停/断点
├─────────────────┤
│  表现层 (VFX)    │  ← 特效/程序，对象池 + Shader 实现
└─────────────────┘
```

**核心原则：**
- **CSV 管数值，NodeCanvas 管逻辑，Runtime 管执行，表现层只做展示**
- 禁止在 CSV 中存放逻辑（如公式、条件），禁止在节点中硬编码数值
- 所有运行时数据通过 Blackboard 在节点间传递

---

## 2. 项目目录结构（最终版）

```text
Assets/
├── Scripts/
│   ├── Data/                   # 数据模型与加载
│   │   ├── SkillConfig.cs
│   │   └── ConfigLoader.cs
│   │
│   ├── Graph/                  # NodeCanvas 图定义
│   │   ├── SkillGraph.cs       (继承 NodeCanvas.Framework.Graph)
│   │   └── SkillConnection.cs  (命名端口连接)
│   │
│   ├── Nodes/                  # 所有节点脚本
│   │   ├── SkillNode.cs        (基类，继承 NodeCanvas.Framework.Node)
│   │   ├── StartNode.cs
│   │   ├── EndNode.cs
│   │   ├── DelayNode.cs
│   │   ├── LogNode.cs
│   │   ├── SetValueNode.cs
│   │   ├── ConditionNode.cs
│   │   ├── ParallelNode.cs
│   │   ├── SubGraphNode.cs
│   │   └── DamageNode.cs
│   │
│   ├── Runtime/                # 运行时核心
│   │   ├── SkillRunner.cs
│   │   ├── SkillContext.cs
│   │   ├── SkillCaster.cs
│   │   ├── Blackboard.cs
│   │   ├── BBKey.cs
│   │   └── DebugRecorder.cs
│   │
│   ├── Editor/                 # 编辑器扩展
│   │   ├── ElementLineGraphGenerator.cs
│   │   ├── SkillNodeEditor.cs
│   │   ├── SkillDebugWindow.cs
│   │   ├── SkillGraphContextMenu.cs
│   │   ├── SkillGraphValidatorWindow.cs
│   │   └── BreakpointDatabase.cs
│   │
│   ├── Entity/                 # 实体组件
│   │   ├── SkillOwner.cs
│   │   └── TargetDummy.cs
│   │
│   └── VFX/                    # 特效模块
│       ├── VFXManager.cs
│       └── VFXObjectPool.cs
│
├── Plugins/                    # 第三方（NodeCanvas/ParadoxNotion）
└── Resources/
    ├── Config/                 # CSV 配置
    └── SkillGraphs/             # 技能图资产
```

---

## 3. 核心设计决策（避坑指南）

| 原则 | 说明 |
|------|------|
| **单一数据源** | CSV 是唯一数值来源，节点通过 `ConfigLoader.GetSkill(id)` 获取参数 |
| **不直接操作 GameObject** | 节点只通过 `SkillContext.caster/target` 传递 Transform，伤害通过 `IDamageable` 接口解耦 |
| **协程执行，不依赖 async/await** | 统一用 `IEnumerator`，主线程安全，可暂停/单步调试 |
| **对象池是强制项** | 所有 VFX 通过 `VFXObjectPool` 获取，`Release` 回收，禁止 Instantiate/Destroy |
| **Blackboard 隔离上下文** | 节点间不直接传参，通过 Blackboard 读写，方便调试和子图复用 |

---

## 4. 关键代码骨架（只列最核心部分）

### 4.1 数据层
```csharp
// SkillConfig.cs
public class SkillConfig {
    public int skill_id;
    public float damage;
    public float range;
    public float cooldown;
}

// ConfigLoader.cs - 游戏启动时加载，全局缓存
public static class ConfigLoader {
    static Dictionary<int, SkillConfig> dict;
    [RuntimeInitializeOnLoadMethod]
    static void Load() { /* 从 Resources/Config/Skill.csv 加载 */ }
    public static SkillConfig GetSkill(int id) => dict[id];
}
```

### 4.2 节点基类（基于 NodeCanvas CanvasCore）
```csharp
public abstract class SkillNode : NodeCanvas.Framework.Node {
    // 命名端口系统（通过 SkillConnection 实现）
    // 输出端口类型由 outConnectionType 指定
    public abstract IEnumerator Execute(SkillContext ctx);
    public virtual SkillNode ResolveNextNode(SkillContext ctx) {
        return GetConnectedNode("output");
    }
    // 使用 CanvasCore 内置的 isBreakpoint
    public bool HasBreakpoint {
        get => isBreakpoint;
        set => isBreakpoint = value;
    }
    [NonSerialized] public bool IsExecuting;
}

// SkillConnection.cs — 命名端口连接
public class SkillConnection : NodeCanvas.Framework.Connection {
    [SerializeField] private string _portName = "output";
    public string portName { get; set; }
    public static SkillConnection Create(SkillNode source, SkillNode target, string portName);
}
```

### 4.3 关键节点
- `StartNode`：入口，直接调用 `ContinueAt("start");`
- `DamageNode`：从 `ConfigLoader` 取伤害，调用 `ctx.target.GetComponent<IDamageable>().TakeDamage(damage, ctx.caster)`
- `ConditionNode`：根据 Random/Distance/Blackboard 变量选择 `trueNode` 或 `falseNode` 端口推进
- `ParallelNode`：遍历所有 `branches` 端口，分别启动协程执行
- `SubGraphNode`：查找子图 `StartNode` 并调用 `SkillRunner.Run(start, ctx)`，复用整个子图
- `DelayNode`：`yield return new WaitForSeconds(delay)` 后推进

### 4.4 运行时引擎
```csharp
public class SkillRunner : MonoBehaviour {
    public static SkillRunner Instance;
    public SkillContext currentContext; // 调试窗口用
    public IEnumerator Run(SkillNode currentNode, SkillContext ctx) {
        while (currentNode != null) {
            currentNode.isExecuting = true;
            if (isDebug && currentNode.hasBreakpoint) PauseHere();
            yield return currentNode.Execute(ctx);
            currentNode.isExecuting = false;
            currentNode = GetNextNode(currentNode, ctx); // 处理条件分支
        }
    }
    // 断点控制方法 Pause(), Step(), Continue()
}
```

### 4.5 角色接入
```csharp
public class SkillOwner : MonoBehaviour {
    public void PlaySkill(SkillGraph graph, Transform target = null) {
        // 冷却检查、构建 SkillContext、调用 SkillRunner.Run
    }
}
```

---

## 5. 里程碑与任务拆分（按周执行）

### W1：骨架与数据驱动基础
- **任务**：
    - 引入 NodeCanvas，运行一键生成脚本拿到全部目录与文件
    - 跑通 `Start → Delay → End`
    - 实现 CSV 加载器，`DamageNode` 能从 CSV 读取伤害并施加
    - 完成 `SkillOwner` 基础调用
- **验收**：按空格释放一个最简单的受伤技能，伤害值来自 CSV

### W2：Blackboard 与流程控制
- **任务**：
    - Blackboard 读写节点（SetValue、内部 Get）
    - ConditionNode（随机、距离、BB 布尔）
    - ParallelNode 并行执行
    - SubGraphNode 子图复用
- **验收**：制作"连锁闪电+暴击分支"技能，子图至少被 2 个技能复用

### W3：VFX 高性能表现
- **任务**：
    - 实现 BeamVFX、ImpactVFX 模块，全部基于对象池
    - 编写对应的简单 Shader（UV 动画闪光）
    - 在 `DamageNode` 或新增 `VFXNode` 中调用 VFX 播放
- **验收**：同时释放 10 个技能，无明显卡顿，Profiler 无 GC Alloc 尖峰

### W4：编辑器与调试工具
- **任务**：
    - 节点执行高亮（绿色标题栏）
    - 断点设置/取消（右键菜单），运行时断点暂停
    - Blackboard 监视面板（Window 实时显示变量）
    - 执行记录与回放（`DebugRecorder` + 编辑器滑块）
    - 一步创建节点的右键菜单
- **验收**：能在编辑器中单步执行技能，查看变量变化，回放历史执行路径

### W5（可选）：模板库与规范制定
- **任务**：
    - 制作 `ChainLightning`、`AOEExplosion` 等子图模板（右键生成）
    - 统一命名规范、子图制作指南
    - 集成技能冷却、资源消耗
- **验收**：新策划可直接从模板拼出新技能，不写一行代码

---

## 6. 开发规范（强制执行）

- **所有参数**使用 CSV 配置，节点内通过 `ConfigLoader` 获取，严禁硬编码
- **节点命名**：`XxxNode`；子图命名：`Skill_Xxx`；BBKey：PascalCase 常量
- **子图**必须包含 `StartNode`，参数通过 Blackboard 传递，禁止循环引用
- **VFX** 一律通过 `VFXManager.Play(key, ...)` 调用，内部使用对象池
- **代码提交**前需通过验收测试，且不能有 `Instantiate/Destroy` 在运行时频繁调用

---

## 7. 团队分工与协作

| 角色 | 负责内容 | 交付物 |
|------|----------|--------|
| **技能程序** | 节点基类、Runner、条件/并行/子图、调试系统 | 所有 `Runtime/`、`Nodes/` 脚本 |
| **数据程序** | CSV 结构与加载器、SkillConfig、ConfigLoader | `Data/` 脚本与 CSV 示例 |
| **特效程序** | VFX 模块、Shader、对象池 | `VFX/` 脚本与材质 |
| **策划/设计** | 配置 CSV 数值、制作技能图、设计子图 | 可玩的 `.asset` 技能图、CSV 数据 |
| **QA** | 按验收标准测试，提供性能报告 | 测试用例与 Profiler 截图 |

---

## 8. 风险控制与常见陷阱

| 陷阱 | 应对方案 |
|------|----------|
| 逻辑误入 CSV | 评估：CSV 只放纯数字，任何条件判断必须用节点 |
| 频繁 Instantiate | 代码审查：强制使用 `VFXObjectPool.Get/Release` |
| 子图循环引用 | 工具限制：编辑器脚本检测子图嵌套深度 ≤ 3 |
| 性能回退 | 每周五固定 Profiler 跑 10 次连锁闪电，对比帧时间 |
| 多人合并冲突 | 技能图使用 NodeCanvas 资产或专用文件夹分人负责 |

---

## 9. 最终交付物清单

- [ ] 可视化技能编辑器（NodeCanvas CanvasCore）
- [ ] 十几种基础功能节点（Start、Delay、Damage、Condition、Parallel、SubGraph 等）
- [ ] CSV 配置读取与热重载（策划改表即生效）
- [ ] 基于 Blackboard 的上下文变量系统
- [ ] 至少 3 个可复用子图（如连锁闪电、范围爆炸、Buff 应用）
- [ ] 高性能 VFX 模块（Beam、Impact）与配套 Shader
- [ ] 调试工具箱（断点、单步、变量监视、执行回放）
- [ ] 角色技巧释放组件（SkillOwner）含冷却功能
- [ ] 示例场景与一键测试脚本

---

## 🚀 启动命令

1. 导入 NodeCanvas (ParadoxNotion)，将一键生成脚本放入 `Editor`，执行 `Tools/Skills/Bootstrap All`
2. 检查 CSV 路径，运行示例场景验证 `Start→Damage→End`
3. 按 W1 任务逐项推进，每周五对照验收标准演示

---

> **核心宗旨**：这是一条**技能生产流水线**——策划填表，设计师连线，程序守护管道。三者各司其职，系统才能规模化和可维护。
