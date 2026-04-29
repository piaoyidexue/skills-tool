# AI 行为树 & 感知模块说明文档

## 一、模块概述

AI 模块是技能系统（Skills-Tool）的三层 AI 解决方案，覆盖从轻量级小兵到复杂 Boss 的完整 AI 谱系。基于 **NodeCanvas / CanvasCore** 的 `BehaviourTree` 框架扩展构建，并内置高性能感知层（**SpatialHashGrid** + **Burst Job** 并行检测）。

### 三层 AI 策略

| Tier | 实现 | 适用对象 | 特点 |
|------|------|---------|------|
| **Minion（小兵）** | `MinionBrain` 轻量 FSM | 塔防小兵、炮灰 | 纯 C# FSM，0 GC，0 序列化开销，无 NodeCanvas 依赖 |
| **Elite / Boss（精英/Boss）** | `AIController` + NodeCanvas BT | 精英怪、Boss | 完整行为树 + CSV 驱动生成，支持多阶段战斗 |
| **Tower（防御塔）** | `AIController` + NodeCanvas BT | 防御塔、炮台 | 专用 BT（站位、索敌、技能循环） |

### 核心能力

| 能力 | 说明 |
|------|------|
| 可视化行为编辑 | 基于 NodeCanvas 图形编辑器，拖拽节点连线即可构建 AI 逻辑 |
| 优先级选择器 | 支持动态重评估的优先级行为选择，自动打断低优先级任务 |
| 并行行为 | 同时执行多个行为（如边移动边攻击） |
| O(1) 空间查询 | SpatialHashGrid 替代 Physics.OverlapSphere，万级单位无压力 |
| Burst 并行感知 | AISensorJobSystem + FOVDistanceJob，多线程 FOV/距离检测 |
| 传感器系统 | 可扩展的感知框架，支持视野、距离、视线遮挡检测 |
| 黑板通信 | 统一的黑板变量系统，所有节点通过 `AIBBKey` 常量读写共享状态 |
| 冷却控制 | 内置冷却装饰器，控制技能/行为执行频率 |
| 生命周期管理 | 完整的 Init → Execute → Reset/Pause 生命周期 |
| **CSV 驱动生成** | 策划通过 AITree.csv 配置表批量定义 AI，自动生成 AIGraph 资产 |
| **DamagePipeline 集成** | MinionBrain 和 AI Action 统一通过 DamagePipeline 结算伤害 |

---

## 二、目录结构

```
Assets/Scripts/AI/
├── Data/                       # 数据配置类
│   └── AITreeConfig.cs         # CSV 行配置数据类
├── Core/                       # 核心基础类
│   ├── AIController.cs         # AI 控制器（Elite/Boss/Tower Tier）
│   ├── AIActionNode.cs         # Action 节点基类
│   ├── AIConditionNode.cs      # Condition 节点基类
│   ├── AIBlackboard.cs         # 黑板键常量 + AI 状态枚举
│   ├── AISensorJobSystem.cs    # Burst 并行 FOV 检测 Job 系统
│   └── MinionBrain.cs          # 轻量 FSM（Minion Tier，无 NodeCanvas 依赖）
├── Graph/                      # 行为树图定义
│   └── AIGraph.cs              # AI 行为树 ScriptableObject + AI 类型枚举
├── Actions/                    # 行为节点（叶子节点）
│   ├── MoveTo.cs / AttackTarget.cs / CastSkill.cs
│   ├── Flee.cs / Idle.cs / PatrolWaypoints.cs
│   ├── PlayAnimation.cs / SensorScan.cs
├── Conditions/                 # 条件节点
│   └── AIConditions.cs         # 7 种条件判断
├── Composites/                 # 组合节点
│   ├── PrioritizedSelector.cs  # 优先级选择器
│   └── ParallelAll.cs          # 并行全部
├── Decorators/                 # 装饰器节点
│   ├── Cooldown.cs / TargetObserver.cs
└── Editor/                     # 编辑器工具
    ├── AIGraphMenu.cs / AITreeGenerator.cs / AITreeSyncPostprocessor.cs

感知系统（Runtime/ 目录下，AI 模块依赖）：
└── Runtime/
    └── SpatialHashGrid.cs      # O(1) 空间哈希网格 + ISpatialEntity 接口

CSV 配置：Resources/Config/AITree.csv → 生成输出：Resources/AITrees/
```

---

## 三、架构设计

### 3.1 类继承关系

```
NodeCanvas.Framework.Graph
    └── BehaviourTree → AIGraph                     # 自定义行为树资产

BTNode
    ├── AIActionNode → MoveTo, AttackTarget...       # Action 节点基类
    ├── AIConditionNode → HasTarget, IsHealthBelow... # Condition 节点基类
    ├── BTComposite → PrioritizedSelector, ParallelAll
    └── BTDecorator → Cooldown, TargetObserver

GraphOwner<BehaviourTree> → BehaviourTreeOwner → AIController  # Elite/Boss/Tower

MonoBehaviour
    ├── AISensor (abstract)                         # 传感器基类
    ├── MinionBrain                                 # 轻量 FSM（Minion Tier，ISpatialEntity）
    ├── SpatialHashGrid                             # O(1) 空间哈希（Singleton）
    └── AISensorJobSystem                          # Burst 并行 Job 调度（Singleton）
```

### 3.2 运行时数据流

**Elite/Boss/Tower Tier（NodeCanvas BT）**：

```
┌─────────────────────────────────────────────────────────┐
│                      AIController                        │
│  AIGraph → BehaviourTree → tickInterval                 │
│    │                                                     │
│    ├── IBlackboard ←── AIBBKey 常量                      │
│    │   (Target, HealthPercent, AIState, ...)             │
│    │                                                     │
│    └── AISensor.Scan() → DetectTarget()                  │
│        写入 Target/HasTarget 到黑板                      │
└─────────────────────────────────────────────────────────┘
```

**Minion Tier（轻量 FSM）**：

```
┌─────────────────────────────────────────────────────────┐
│                      MinionBrain                         │
│                                                          │
│  FSM: MoveToWaypoint ←→ Attack → Dead                    │
│    │                  │        │                         │
│    │    QueryNearest() │        │                        │
│    └──────────────────┼────────┘                         │
│                       ▼                                  │
│              SpatialHashGrid (Singleton)                 │
│              O(1) 范围查询 / Register / Unregister       │
│              GetNativeEntityArray                        │
│                       │                                  │
│                       ▼                                  │
│            AISensorJobSystem (Singleton)                 │
│            FOVDistanceJob (Burst 多线程)                  │
│            FOVDistanceSortedJob                          │
│                                                          │
│  伤害: DamagePipeline.CalculateAndApply()                │
└─────────────────────────────────────────────────────────┘
```

---

## 四、核心组件详解

### 4.1 AIController — AI 控制器

**文件**: `Core/AIController.cs`

AI 控制器是挂载在 AI 角色（Elite/Boss/Tower Tier）上的 MonoBehaviour 组件，继承自 `BehaviourTreeOwner`。

**Inspector 可配置参数**:

| 参数 | 类型 | 说明 |
|------|------|------|
| `aiType` | `AIType` | AI 类型标签（Combat/Patrol/Boss/Guard 等） |
| `sensor` | `AISensor` | 传感器组件引用 |
| `tickInterval` | `float` (0~1) | 行为树更新间隔（秒），0=每帧更新 |

**运行时属性**:

| 属性/事件 | 说明 |
|-----------|------|
| `CurrentAIState` | 设置自动写入黑板 `AIBBKey.AIState`，触发 `OnStateChanged` |
| `AlertLevel` | 设置自动写入黑板 `AIBBKey.AlertLevel`，触发 `OnAlertChanged` |
| `SetBBValue<T>(key, value)` | 向黑板写入变量 |
| `GetBBValue<T>(key)` | 从黑板读取变量 |

---

### 4.2 AIActionNode — Action 节点基类

**文件**: `Core/AIActionNode.cs`

所有 AI 行为节点（叶子节点）的基类。继承 `BTNode`，不使用 Task 系统。

**生命周期**:
```
OnExecute(agent, blackboard)   ← NodeCanvas 框架调用
    ├── 缓存 this.agent / this.blackboard
    ├── if status == Resting: OnActionInit(agent, blackboard)
    └── OnExecuteOnce(agent, blackboard) → Status
OnReset()   →   OnActionReset()
OnGraphPaused()   →   OnActionPause()
```

**子类必须实现**: `OnExecuteOnce(agent, blackboard)` → `Status`
**可选覆写**: `OnActionInit()`, `OnActionReset()`, `OnActionPause()`

---

### 4.3 AIConditionNode — Condition 节点基类

**文件**: `Core/AIConditionNode.cs`

条件判断节点，采用装饰器模式（仅一个子节点输出）。条件满足时执行子节点，否则返回 Failure。

**子类必须实现**: `CheckCondition(agent, blackboard)` → `bool`
**可选覆写**: `OnConditionEnable()` / `OnConditionDisable()`

---

### 4.4 AISensor — 传感器基类

**文件**: `Core/AIController.cs`（与 AIController 同文件）

抽象传感器基类，负责环境感知。推荐基于 `SpatialHashGrid.QueryRange()` 实现检测逻辑。

**内置工具方法**: `Scan()`, `HasLineOfSight()`, `IsInFieldOfView()`, `IsInDetectionRange()`

**事件**: `OnTargetChanged`, `OnTargetDetected`, `OnTargetLost`

**Inspector 配置**: `detectionRange`(15m), `attackRange`(3m), `fieldOfView`(120°), `detectionMask`, `obstacleMask`

---

### 4.5 AIBlackboard / AIBBKey — 黑板键常量

**文件**: `Core/AIBlackboard.cs`

统一管理所有黑板变量名称，分为五大类：

| 类别 | 键名 | 类型 | 说明 |
|------|------|------|------|
| **目标** | `Target`, `TargetPosition`, `LastKnownPosition`, `IsTargetVisible`, `DistanceToTarget`, `HasTarget` | Transform/Vector3/bool/float | 目标感知 |
| **战斗** | `IsInCombat`, `IsAttacking`, `LastDamage`, `HealthPercent`, `ManaPercent`, `TargetHealthPercent` | bool/float | 战斗状态 |
| **移动** | `MoveSpeed`, `PatrolIndex`, `IsMoving`, `HasReachedDestination`, `PatrolWaitTime` | float/int/bool | 移动控制 |
| **技能** | `CurrentSkillId`, `SkillCooldownReady`, `LastUsedSkillTime` | string/bool/float | 技能状态 |
| **状态** | `AIState`, `AlertLevel`, `HasBuff`, `BuffType`, `EnemyCount`, `AllyCount` | int/bool/string | 整体状态 |

**AI 状态枚举** (`AIStateType`): `Idle → Patrol → Chase → Combat → Flee → Dead`
**警戒等级** (`AIAlertLevel`): `None(0) → Suspicious(1) → Alert(2) → Combat(3)`

---

### 4.6 AIGraph — 行为树资产

**文件**: `Graph/AIGraph.cs`

自定义 `BehaviourTree` ScriptableObject，扩展了 AI 元数据：`aiTreeId`(GUID), `treeName`, `treeDescription`, `aiType`(AIType), `priority`(0~100)。

**编辑器菜单**: `Tools/Skills/AI/Create AI Behaviour Tree`

---

### 4.7 SpatialHashGrid — O(1) 空间哈希网格

**文件**: `Runtime/SpatialHashGrid.cs`

全局 Singleton 空间查询系统，通过均匀网格哈希实现 O(1) 范围查询，替代 `Physics.OverlapSphere`。支持万级实体注册。

**核心接口 — ISpatialEntity**:

```csharp
public interface ISpatialEntity
{
    int EntityId { get; }       // 注册时分配的唯一 ID
    Vector3 Position { get; }   // 当前位置
    int TeamId { get; }         // 队伍 ID（0=中立, 1=玩家方, 2=敌对方）
    bool IsActive { get; }      // 是否活跃
    int EntityType { get; }     // 实体类型（对应 AITier 枚举）
}
```

**关键 API**:

| 方法 | 说明 |
|------|------|
| `Register(entity)` / `RegisterWithMeta(entity, meta)` | 注册实体，返回 entityId |
| `Unregister(entityId)` | 移除实体 |
| `UpdatePosition(entityId, pos)` | 更新位置（超 0.5m 阈值自动换格） |
| `GetEntity(entityId)` | O(1) 查找 ISpatialEntity |
| `QueryRange(center, radius, results, teamFilter, excludeId)` | 范围查询 |
| `QueryNearest(center, radius, teamFilter, excludeId)` | 最近目标查询 |
| `GetNativeEntityArray(allocator)` | 导出 NativeArray 供 Burst Job 使用 |
| `GetStats()` | 调试统计（实体数/格子数/脏实体数） |

**配置**: `_cellSize`(10m) 网格单元大小, `_dirtyThreshold`(0.5m) 位置更新阈值。

**设计要点**: Singleton + DontDestroyOnLoad，延迟脏标记刷新（`LateUpdate` 统一 Flush），NativeArray 按需导出。

---

### 4.8 AISensorJobSystem — Burst 并行感知

**文件**: `Core/AISensorJobSystem.cs`

全局 Singleton，收集每帧所有 AI 实体的感知查询请求，分发到 Burst 编译的多线程 Job 中执行，消除主线程串行 O(N²) 检测开销。

**工作流程**:
```
1. AI 实体调用 RegisterQuery(request) 提交查询请求
2. LateUpdate: CollectQueries → 从 SpatialHashGrid 获取 NativeArray
3. Schedule FOVDistanceJob / FOVDistanceSortedJob（Burst 并行）
4. 下一帧 LateUpdate: CompleteJobs → 回读结果 → 回调 Callback
```

**关键数据结构**:

| 结构体 | 用途 |
|--------|------|
| `SensorQueryRequest` | 查询请求（位置、朝向、范围、队伍、回调） |
| `SensorQueryInput` | Job 输入（Burst 友好的 float3/值类型） |
| `SensorQueryResult` | 查询结果（最近敌人 ID/距离、敌我数量、攻击范围标记） |
| `AIEntityNativeData` | 实体 Job 数据（从 SpatialHashGrid 导出） |

**Burst Job 变体**: `FOVDistanceJob`（基础检测）、`FOVDistanceSortedJob`（距离排序，使用 `Span<T> + stackalloc`）。

**配置**: `_useJobs`(true), `_batchSize`(64)。

---

### 4.9 MinionBrain — 轻量 FSM（Minion Tier）

**文件**: `Core/MinionBrain.cs`

Minion Tier 的 AI 实现，纯 C# 状态机，无需 NodeCanvas，专为塔防/ARPG 大量小兵设计。实现 `ISpatialEntity`。

**状态机**: `MoveToWaypoint → Attack → Dead`（目标丢失时回退巡逻）

**Inspector 参数**:

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `_aiTier` | `Minion` | AI 层级 |
| `_teamId` | 2 | 队伍 ID |
| `_attackRange` / `_detectionRange` | 2m / 8m | 攻击/检测范围 |
| `_fieldOfView` | 360° | 视野角度 |
| `_attackCooldown` / `_damage` | 1.5s / 10 | 攻击冷却/伤害 |
| `_useJobSystem` | `true` | 是否使用 Burst Job 感知 |
| `_waypoints` / `_moveSpeed` | — / 3 | 巡逻路径/移动速度 |

**核心生命周期**:
```csharp
Start():     grid.RegisterWithMeta(this, meta);  // 注册到 SpatialHashGrid
Update():    grid.UpdatePosition(...)            // 同步位置
             jobSys.RegisterQuery(...)           // 每 4 帧提交 Job 查询
             FSM Tick                            // 状态机更新
TakeDamage:  grid.Unregister(id); Destroy(...);  // 死亡清理
OnDestroy(): grid.Unregister(id);               // 安全兜底
```

**伤害结算**: `DamagePipeline.CalculateAndApply(_damage, tf, transform)` 统一走 GE 管道。

**目标检测优先级**: ① Job 系统结果（最快） → ② SpatialHashGrid.QueryNearest()（回退） → ③ 继续巡逻。

---

## 五、节点参考手册

### 5.1 Action 节点（行为节点）

#### MoveTo — 移动到目标

| 参数 | 类型 | 说明 |
|------|------|------|
| `mode` | `TargetMode` | `BlackboardTarget` / `BlackboardPosition` / `FixedPosition` |
| `arriveDistance` | `float` | 到达距离阈值（默认 1.5m） |
| `speedMultiplier` | `float` | 速度系数（0~1，默认 1） |
| `timeout` | `float` | 超时时间（0=永不超时） |
| `faceTarget` / `rotationSpeed` | `bool` / `float` | 面向目标控制 |

**行为**: 优先使用 NavMeshAgent，无 NavMesh 时回退到 Transform 平移。到达 → `Success`，超时 → `Failure`。

**写入黑板**: `DistanceToTarget`, `HasReachedDestination`, `IsMoving`

---

#### AttackTarget — 攻击目标

| 参数 | 类型 | 说明 |
|------|------|------|
| `mode` | `AttackMode` | `Melee` / `Ranged` |
| `attackRange` / `damage` | `float` | 攻击范围(2.5m) / 每次伤害(10) |
| `attackAnimationTrigger` | `string` | Animator Trigger 名 |
| `attackSpeed` | `float` | 攻击冷却（秒，默认 1） |
| `skillId` | `string` | 关联技能 ID（可选） |

**写入黑板**: `IsAttacking`, `LastDamage`, `CurrentSkillId`, `DistanceToTarget`

---

#### CastSkill / Flee / Idle / PatrolWaypoints / PlayAnimation

| 节点 | 关键参数 | 行为 |
|------|---------|------|
| **CastSkill** | `skillId`, `castRange` | 超出范围返回 Failure，面向目标后写入 `CurrentSkillId` |
| **Flee** | `safeDistance`(15m), `fleeSpeedMultiplier`(1.5x) | 计算远离方向，移动到安全距离 |
| **Idle** | `duration`, `playRandomIdleAnimation` | 等待指定时间后返回 Success |
| **PatrolWaypoints** | `mode`(Loop/Once/PingPong), `waitTime`, `maxCycles` | 沿路径点依次移动并等待 |
| **PlayAnimation** | `paramType`(Trigger/Bool/Float), `paramName` | 立即设置 Animator 参数 |

---

#### SensorScan — 传感器扫描

| 参数 | 类型 | 说明 |
|------|------|------|
| `scanInterval` | `float` | 扫描间隔（秒，默认 0.5） |

**行为**: 驱动 `AISensor.Scan()` 执行环境检测。推荐传感器基于 `SpatialHashGrid.QueryRange()` 实现。

> **Minion Tier**：不使用此节点，通过内部 FSM 直接调用 `SpatialHashGrid.QueryNearest()` + `AISensorJobSystem.RegisterQuery()`。

---

### 5.2 Condition 节点（条件节点）

| 节点 | 说明 | 关键参数 |
|------|------|----------|
| **HasTarget** | 检查是否有有效目标 | `targetKey`, `requireAlive` |
| **IsTargetInRange** | 检查目标是否在范围内 | `targetKey`, `range`, `rangeKey` |
| **IsHealthBelow** | 检查生命值是否低于阈值 | `threshold`(0~1), `healthKey` |
| **HasEnemyDetected** | 检查传感器是否检测到敌人 | `targetKey` |
| **BlackboardBool** | 检查黑板布尔值 | `key`, `expectedValue`, `resetChildOnFail` |
| **CooldownReady** | 检查技能冷却是否就绪 | `lastUsedKey`, `cooldown` |
| **CompareFloat** | 比较两个浮点数 | `mode`(Greater/Less/Equal/...), `keyA`, `keyB`, `fixedValue` |

---

### 5.3 Composite 节点（组合节点）

#### PrioritizedSelector — 优先级选择器

按从左到右的优先级依次执行子节点。支持**动态重评估**（`dynamic` 模式）。

| 参数 | 说明 |
|------|------|
| `dynamic`(bool) | 每帧从第一个子节点重新检查，高优先级可打断低优先级 |
| `randomOrder`(bool) | 启动时随机打乱子节点顺序 |

**典型场景**: `逃跑 > 治疗 > 攻击 > 巡逻 > 待机`

---

#### ParallelAll — 并行全部

同时执行所有子节点。`requireAllSuccess`(bool), `failOnAny`(bool)。

**典型场景**: 边移动边射击、同时巡逻和检测。

---

### 5.4 Decorator 节点（装饰器节点）

| 节点 | 关键参数 | 行为 |
|------|---------|------|
| **Cooldown** | `cooldownTime`(2s), `resetChildOnCooldownEnd` | 子节点执行完成后进入冷却，冷却期内跳过 |
| **TargetObserver** | `maxObserveDistance`, `checkLineOfSight` | 持续验证目标有效性，丢失时清除黑板目标 |

---

## 六、使用指南

### 6.1 快速开始（Elite/Boss/Tower）

1. **创建行为树**: 菜单 `Tools/Skills/AI/Create AI Behaviour Tree`
2. **挂载 AI 控制器**: 在 AI 角色上添加 `AIController` 组件
3. **关联行为树**: 将 `AIGraph` 资产拖拽到 `graph` 字段
4. **设置黑板**: 在 Graph Editor 中添加所需黑板变量（参考 [AIBBKey](#45-aiblackboard--aibbkey--黑板键常量)）
5. **搭建行为逻辑**: 拖拽节点连线构建行为树

### 6.2 生成示例行为树

菜单 `Tools/Skills/AI/Generate Sample AI Trees` 自动生成三套示例：

| 示例 | 路径 | 行为逻辑 |
|------|------|----------|
| `AI_Combat` | `Examples/AI/AI_Combat.asset` | 检测敌人 → 追击 → 攻击，低血量逃跑 |
| `AI_Patrol` | `Examples/AI/AI_Patrol.asset` | 巡逻为主，检测到敌人切换追击/攻击 |
| `AI_Boss` | `Examples/AI/AI_Boss.asset` | 三阶段：常规攻击 → HP<60% 召唤 → HP<30% 狂暴 |

### 6.3 推荐的行为树结构

```
[PrioritizedSelector - dynamic=true]
├── [逃跑分支 - 最高优先级]
│   └── Sequencer → IsHealthBelow(0.25) + Flee
├── [战斗分支 - 中优先级]
│   └── Sequencer → HasTarget + TargetObserver
│       └── Selector → (IsTargetInRange + Cooldown + AttackTarget) / MoveTo
└── [默认分支 - 最低优先级]
    └── Sequencer → SensorScan + Idle(0.5)
```

### 6.4 使用 AIController API

```csharp
var ai = GetComponent<AIController>();
ai.SetBBValue(AIBBKey.HasTarget, true);
ai.CurrentAIState = AIStateType.Combat;
ai.AlertLevel = AIAlertLevel.Combat;
ai.OnStateChanged += (old, newState) => Debug.Log($"状态: {old} → {newState}");
```

### 6.5 自定义传感器

推荐基于 `SpatialHashGrid` 实现，避免 `Physics.OverlapSphere`：

```csharp
public class MySensor : AISensor
{
    private readonly List<ISpatialEntity> _results = new(64);

    protected override Transform DetectTarget()
    {
        var grid = SpatialHashGrid.Instance;
        if (grid == null) return null;
        _results.Clear();
        grid.QueryRange(transform.position, detectionRange, _results, teamFilter: 2);
        foreach (var e in _results)
        {
            var tf = (e as MonoBehaviour)?.transform;
            if (tf != null && HasLineOfSight(tf)) return tf;
        }
        return null;
    }
}
```

> 如需 Job 级并行检测，优先使用 `AISensorJobSystem.RegisterQuery()`。

### 6.6 配置 MinionBrain（塔防小兵）

1. 场景中创建 `SpatialHashGrid` Singleton（根节点 `AppManager`，`DefaultExecutionOrder = -200`）
2. 场景中创建 `AISensorJobSystem` Singleton（`DefaultExecutionOrder = 100`）
3. 为 Minion 预制体添加 `MinionBrain` 组件，配置 `_teamId=2`, `_waypoints`, 勾选 `_useJobSystem`

**运行时行为**:
```
MoveToWaypoint → 检测到敌人（FOV+距离） → Attack（冷却循环） → 目标死亡 → 回到巡逻
                                                                    → 受到致命伤害 → Dead（注销+销毁）
```

### 6.7 场景初始化顺序

```
1. SpatialHashGrid.Awake()        [ExecutionOrder = -200]
2. 业务 Manager Awake
3. AISensorJobSystem.Awake()      [ExecutionOrder = 100]
4. AI 实体 Start() → RegisterWithMeta()
5. Update → 提交查询请求
6. AISensorJobSystem.LateUpdate() → 收集查询 → 调度 Job
7. 下一帧: Job 完成 → CompleteJobs → 回调分发
```

---

## 七、CSV 配置驱动的行为树自动创建

### 7.1 设计理念

遵循"**CSV 是唯一数据源**"原则，策划通过 `AITree.csv` 配置表批量定义 AI，自动生成 AIGraph 资产。

**分工**: 策划填 CSV → 程序维护生成器 → 设计师编辑器微调

**数据流**: `AITree.csv` → `AITreeGenerator` → `Resources/AITrees/AI_*.asset` → `AIController.graph`

### 7.2 CSV 格式

**文件**: `Resources/Config/AITree.csv`，编码 UTF-8，`#` 开头为注释。

| 列名 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `tree_id` | `string` | ✅ | 行为树唯一标识，同 ID 的行属于同一棵树 |
| `tree_name` | `string` | | 显示名称 |
| `ai_type` | `AIType` | | Combat/Patrol/Guard/Flee/Idle/Boss/Support |
| `priority` | `int` (0-100) | | 默认 50 |
| `update_interval` | `float` | | 更新间隔（0=每帧） |
| `chain_order` | `int` | ✅ | 行为链序号（越小越优先，从 1 开始） |
| `node_order` | `int` | ✅ | 节点在链内的顺序（从 1 开始） |
| `node_class` | `string` | ✅ | 节点类名（大小写不敏感） |
| `params` | `string` | | 参数 `key=value;key=value` |

### 7.3 生成规则

```
PrioritizedSelector (dynamic=true, 根节点)
├── chain_order=1 → Sequencer (链1，最高优先级)
│       └── node_order=1,2,... → 节点串联
├── chain_order=2 → Sequencer (链2)
└── chain_order=3 → Sequencer (链3，最低优先级)
```

- 根节点自动创建为 `PrioritizedSelector`（`dynamic=true`）
- 每个 `chain_order` 自动包裹为 `Sequencer`
- 链内节点按 `node_order` 升序串联

### 7.4 参数格式

`key=value;key=value` 格式，自动类型转换：

| C# 类型 | 示例 | 说明 |
|---------|------|------|
| `string` / `int` / `float` / `bool` | `fireball` / `42` / `2.5` / `true` | 直接解析 |
| 枚举 / Vector3 | `Melee` / `1,2,3` | 按名称匹配 / 逗号分隔 |
| `BBParameter<T>` | `3.0` | 自动包装 |

### 7.5 支持的节点类名

| 分类 | 节点类名 | 常用参数 |
|------|---------|----------|
| 组合 | `PrioritizedSelector`, `Sequencer`, `Selector`, `ParallelAll` | `requireAllSuccess`, `failOnAny` |
| 装饰器 | `Cooldown`, `TargetObserver` | `cooldownTime`, `maxObserveDistance`, `checkLineOfSight` |
| 行为 | `MoveTo`, `AttackTarget`, `CastSkill`, `Flee`, `Idle`, `PatrolWaypoints`, `PlayAnimation`, `SensorScan` | `arriveDistance`, `damage`, `attackRange`, `skillId`, `safeDistance`, `duration`, `waitTime`, `mode` |
| 条件 | `HasTarget`, `IsTargetInRange`, `IsHealthBelow`, `HasEnemyDetected`, `BlackboardBool`, `CooldownReady`, `CompareFloat` | `threshold`, `range`, `cooldown`, `expectedValue` |

### 7.6 操作方式

- **手动生成**: 菜单 `Tools/Skills/AI/Generate AI Trees from CSV`
- **自动生成**: 保存 `AITree.csv` 后 `AITreeSyncPostprocessor` 自动触发
- **Bootstrap**: 菜单 `Tools/Skills/Bootstrap All` 创建默认 CSV + 4 套模板

### 7.7 预置模板

| tree_id | 名称 | 行为链 |
|---------|------|--------|
| `combat` | 战斗AI | ① 低血量逃跑 → ② 目标攻击 → ③ 待机扫描 |
| `patrol` | 巡逻AI | ① 遇敌追击攻击 → ② 路径巡逻 |
| `boss` | Boss AI | ① HP<30% 大招 → ② HP<60% 召唤 → ③ 常规攻击 |
| `guard` | 守卫AI | ① 遇敌追击攻击 → ② 警戒待机 |

### 7.8 完整生成示例

**CSV 输入**:
```csv
tree_id,tree_name,ai_type,priority,update_interval,chain_order,node_order,node_class,params
combat,战斗AI,Combat,50,0.1,1,1,IsHealthBelow,threshold=0.25
combat,战斗AI,Combat,50,0.1,1,2,Flee,safeDistance=15;fleeSpeedMultiplier=1.5
combat,战斗AI,Combat,50,0.1,2,1,HasTarget,
combat,战斗AI,Combat,50,0.1,2,2,TargetObserver,maxObserveDistance=30
combat,战斗AI,Combat,50,0.1,2,3,IsTargetInRange,range=2.5
combat,战斗AI,Combat,50,0.1,2,4,Cooldown,cooldownTime=1
combat,战斗AI,Combat,50,0.1,2,5,AttackTarget,damage=10;attackRange=2.5;attackSpeed=1
combat,战斗AI,Combat,50,0.1,2,6,MoveTo,arriveDistance=1.5
combat,战斗AI,Combat,50,0.1,3,1,SensorScan,scanInterval=0.5
combat,战斗AI,Combat,50,0.1,3,2,Idle,duration=0.5
```

**生成结构**: `PrioritizedSelector(dynamic=true)` → 链1(Flee分支) / 链2(战斗分支) / 链3(待机扫描)

### 7.9 扩展新节点到 CSV 生成器

在 `AITreeGenerator.NodeTypeMap` 中注册：`{ "MyNewAction", typeof(MyNewAction) }`

---

## 八、扩展指南

### 8.1 添加新 Action 节点

```csharp
[Name("★ My Action")]
[Category("Composites/AI/Actions")]
public class MyAction : AIActionNode
{
    public BBParameter<float> myParam = 1f;

    protected override void OnActionInit(Component agent, IBlackboard blackboard) { }
    protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard) => Status.Success;
    protected override void OnActionReset() => blackboard?.SetVariableValue("MyKey", false);
}
```

### 8.2 添加新 Condition 节点

```csharp
[Name("★ My Condition")]
[Category("Composites/AI/Conditions")]
public class MyCondition : AIConditionNode
{
    public string myKey = "SomeKey";
    protected override bool CheckCondition(Component agent, IBlackboard blackboard)
        => blackboard.GetVariableValue<bool>(myKey);
}
```

**注意**: `Category("Composites/AI/...")` 确保节点出现在 NodeCanvas 节点浏览器的 AI 分组中。

### 8.3 实现自定义 ISpatialEntity

任何需要被 AI 感知系统检测到的实体都应实现 `ISpatialEntity`：

```csharp
public class MyTower : MonoBehaviour, ISpatialEntity
{
    private int _entityId = -1;

    int ISpatialEntity.EntityId => _entityId;
    Vector3 ISpatialEntity.Position => transform.position;
    int ISpatialEntity.TeamId => 1;         // 玩家方
    bool ISpatialEntity.IsActive => isActiveAndEnabled;
    int ISpatialEntity.EntityType => (int)AITier.Tower;

    void Start()    => _entityId = SpatialHashGrid.Instance.Register(this);
    void Update()   => SpatialHashGrid.Instance?.UpdatePosition(_entityId, transform.position);
    void OnDestroy() => SpatialHashGrid.Instance?.Unregister(_entityId);
}
```

### 8.4 自定义 Burst Job 感知

```csharp
[BurstCompile]
public struct MyDetectJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<AIEntityNativeData> Entities;
    [ReadOnly] public NativeArray<SensorQueryInput> Queries;
    [WriteOnly] public NativeArray<SensorQueryResult> Results;
    public void Execute(int index) { /* 自定义检测逻辑 */ }
}
```

> **Burst 限制**: Job 内不能使用 class、delegate、try-catch、Debug.Log。使用 `Span<T> + stackalloc` 替代堆分配数组。

---

## 九、注意事项

1. **NodeCanvas 依赖**: Elite/Boss/Tower Tier 依赖 `ParadoxNotion.NodeCanvas`。**MinionBrain 不依赖 NodeCanvas**。

2. **黑板变量初始化**: 使用节点前，需在 Graph Editor 中预先定义对应变量（类型和键名与 `AIBBKey` 一致）。

3. **`AIActionNode` 缓存**: `OnActionReset()` 和 `OnActionPause()` 无参数，通过 `this.agent` / `this.blackboard` 属性访问（`OnExecute` 入口自动缓存）。

4. **节点 Category**: 所有自定义节点使用 `Category("Composites/AI/...")`，`★` 前缀使节点醒目。

5. **`AIController.updateInterval`**: 通过 `BehaviourTreeOwner.updateInterval` 属性设置，不要直接修改 `graph.updateInterval`。

6. **NavMeshAgent 依赖**: `MoveTo` 和 `Flee` 节点优先使用 NavMeshAgent，无 NavMesh 时回退到 Transform 移动。

7. **示例行为树覆盖**: `Generate Sample AI Trees` 会覆盖已存在的同路径文件，请先备份。

8. **CSV 驱动生成**: 修改 `AITree.csv` 后自动重新生成 `Resources/AITrees/` 下所有资产。手动触发: `Tools/Skills/AI/Generate AI Trees from CSV`。

9. **SpatialHashGrid 初始化顺序**: 必须早于任何 AI 实体注册（`DefaultExecutionOrder = -200`），场景启动时最先创建。

10. **AISensorJobSystem 初始化顺序**: 在 SpatialHashGrid 之后、AI 实体之前（`DefaultExecutionOrder = 100`）。

11. **三层 AI 选择指南**:
    - **大量小兵（>50）** → `MinionBrain` FSM，避免 NodeCanvas 序列化和 GC 开销
    - **精英怪 / Boss** → `AIController` + NodeCanvas BT，利用 CSV 驱动和可视化编辑
    - **防御塔** → `AIController` + NodeCanvas BT（专用策略），搭配 SkillGraph 技能系统

12. **ISpatialEntity 接口**: 任何需要被 AI 感知系统检测到的实体都应实现该接口并注册到 `SpatialHashGrid`。

13. **伤害结算统一**: 所有 AI 伤害必须通过 `DamagePipeline.CalculateAndApply()` 结算，确保 GE 事件拦截正常工作。

14. **Job 回调查询延迟**: `AISensorJobSystem` 查询是异步的——当前帧提交，下一帧通过 `Callback` 返回结果。

15. **禁用 Physics.OverlapSphere**: 项目中 AI 感知统一走 `SpatialHashGrid.QueryRange()` 或 `AISensorJobSystem`。
