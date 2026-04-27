# AI 行为树模块说明文档

## 一、模块概述

AI 行为树模块是技能系统（Skills-Tool）的企业级 AI 解决方案，基于 **NodeCanvas / CanvasCore** 的 `BehaviourTree` 框架扩展构建。该模块提供了一整套可视化的行为树节点（Actions、Conditions、Composites、Decorators），结合黑板系统（Blackboard）和传感器（Sensor），实现复杂 AI 行为的快速搭建与迭代。

### 核心能力

| 能力 | 说明 |
|------|------|
| 可视化行为编辑 | 基于 NodeCanvas 图形编辑器，拖拽节点连线即可构建 AI 逻辑 |
| 优先级选择器 | 支持动态重评估的优先级行为选择，自动打断低优先级任务 |
| 并行行为 | 同时执行多个行为（如边移动边攻击） |
| 传感器系统 | 可扩展的感知框架，支持视野、距离、视线遮挡检测 |
| 黑板通信 | 统一的黑板变量系统，所有节点通过 `AIBBKey` 常量读写共享状态 |
| 冷却控制 | 内置冷却装饰器，控制技能/行为执行频率 |
| 生命周期管理 | 完整的 Init → Execute → Reset/Pause 生命周期 |
| **CSV 驱动生成** | 策划通过 AITree.csv 配置表批量定义 AI，自动生成 AIGraph 资产 |

---

## 二、目录结构

```
Assets/Scripts/AI/
├── Data/                       # 数据配置类
│   └── AITreeConfig.cs         # CSV 行配置数据类
├── Core/                       # 核心基础类
│   ├── AIController.cs         # AI 控制器（MonoBehaviour，驱动行为树运行）
│   ├── AIActionNode.cs         # Action 节点基类
│   ├── AIConditionNode.cs      # Condition 节点基类
│   └── AIBlackboard.cs         # 黑板键常量 + AI 状态枚举
├── Graph/                      # 行为树图定义
│   └── AIGraph.cs              # AI 行为树 ScriptableObject + AI 类型枚举
├── Actions/                    # 行为节点（叶子节点）
│   ├── MoveTo.cs               # 移动到目标
│   ├── AttackTarget.cs         # 攻击目标
│   ├── CastSkill.cs            # 释放技能
│   ├── Flee.cs                 # 逃跑
│   ├── Idle.cs                 # 待机
│   ├── PatrolWaypoints.cs      # 路径巡逻
│   ├── PlayAnimation.cs        # 播放动画
│   └── SensorScan.cs           # 传感器扫描
├── Conditions/                 # 条件节点（装饰器模式）
│   └── AIConditions.cs         # 7 种条件判断
├── Composites/                 # 组合节点（控制流）
│   ├── PrioritizedSelector.cs  # 优先级选择器
│   └── ParallelAll.cs          # 并行全部
├── Decorators/                 # 装饰器节点
│   ├── Cooldown.cs             # 冷却装饰器
│   └── TargetObserver.cs       # 目标观察装饰器
└── Editor/                     # 编辑器工具
    ├── AIGraphMenu.cs          # 菜单入口 + 示例行为树生成器
    ├── AITreeGenerator.cs      # CSV → AIGraph 资产生成器
    └── AITreeSyncPostprocessor.cs  # CSV 变更自动监听

CSV 配置文件:
├── Resources/Config/
│   └── AITree.csv              # AI 行为树配置（单一数据源）
└── 生成输出:
    └── Resources/AITrees/      # 自动生成的 AIGraph 资产
```

---

## 三、架构设计

### 3.1 类继承关系

```
NodeCanvas.Framework.Graph
    └── NodeCanvas.BehaviourTrees.BehaviourTree
            └── AIGraph                          # 自定义行为树资产

NodeCanvas.BehaviourTrees.BTNode
    ├── AIActionNode                             # Action 节点基类
    │       └── MoveTo, AttackTarget, CastSkill, Flee, Idle, PatrolWaypoints, PlayAnimation, SensorScan
    ├── AIConditionNode                          # Condition 节点基类
    │       └── HasTarget, IsTargetInRange, IsHealthBelow, HasEnemyDetected, ...
    ├── BTComposite
    │       └── PrioritizedSelector, ParallelAll
    └── BTDecorator
            └── Cooldown, TargetObserver

NodeCanvas.Framework.GraphOwner<BehaviourTree>
    └── NodeCanvas.BehaviourTrees.BehaviourTreeOwner
            └── AIController                     # 运行时控制器

MonoBehaviour
    └── AISensor (abstract)                      # 传感器基类
```

### 3.2 运行时数据流

```
┌──────────────────────────────────────────────────────────────────────┐
│                          AIController                                │
│  ┌─────────────┐    ┌─────────────────┐    ┌───────────────────┐   │
│  │   AIGraph   │───▶│  BehaviourTree  │───▶│  tickInterval     │   │
│  │  (资产引用)  │    │   Owner 运行    │    │  (更新间隔控制)   │   │
│  └─────────────┘    └─────────────────┘    └───────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│              ┌───────────────────────────────┐                      │
│              │         IBlackboard            │                      │
│              │  ┌─────────────────────────┐  │                      │
│              │  │ Target, TargetPosition, │  │                      │
│              │  │ HealthPercent, AIState, │  │                      │
│              │  │ IsAttacking, MoveSpeed  │  │   ◀── AIBBKey 常量   │
│              │  │ ...                     │  │                      │
│              │  └─────────────────────────┘  │                      │
│              └───────────────────────────────┘                      │
│                              ▲                                       │
│              ┌───────────────┴───────────────┐                      │
│              │          AISensor              │                      │
│              │  Scan() → DetectTarget()       │                      │
│              │  写入 Target/HasTarget 等      │                      │
│              └───────────────────────────────┘                      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 四、核心组件详解

### 4.1 AIController — AI 控制器

**文件**: `Core/AIController.cs`

AI 控制器是挂载在 AI 角色上的 MonoBehaviour 组件，继承自 `BehaviourTreeOwner`。它负责：

- 指定并运行 `AIGraph` 行为树资产
- 管理 AI 状态（`AIStateType`）和警戒等级（`AIAlertLevel`）
- 提供 `SetBBValue<T>` / `GetBBValue<T>` 便捷方法读写黑板
- 自动查找并关联 `AISensor`

**Inspector 可配置参数**:

| 参数 | 类型 | 说明 |
|------|------|------|
| `aiType` | `AIType` | AI 类型标签（战斗/巡逻/守卫等） |
| `sensor` | `AISensor` | 传感器组件引用 |
| `tickInterval` | `float` (0~1) | 行为树更新间隔（秒），0=每帧更新 |

**运行时属性**:

| 属性/事件 | 说明 |
|-----------|------|
| `CurrentAIState` | 设置自动写入黑板 `AIBBKey.AIState`，触发 `OnStateChanged` 事件 |
| `AlertLevel` | 设置自动写入黑板 `AIBBKey.AlertLevel`，触发 `OnAlertChanged` 事件 |
| `AITree` | 获取当前 `AIGraph` 引用 |
| `SetBBValue<T>(key, value)` | 向黑板写入变量 |
| `GetBBValue<T>(key)` | 从黑板读取变量 |

---

### 4.2 AIActionNode — Action 节点基类

**文件**: `Core/AIActionNode.cs`

所有 AI 行为节点（叶子节点）的基类。继承 `BTNode`，不依赖 Task 系统，直接实现行为逻辑。

**生命周期**:

```
OnExecute(agent, blackboard)   ← NodeCanvas 框架调用
    │
    ├── 缓存 this.agent / this.blackboard
    │
    ├── if status == Resting:
    │       OnActionInit(agent, blackboard)   ← 首次进入时调用一次
    │
    └── OnExecuteOnce(agent, blackboard)      ← 每帧调用，返回 Status

OnReset()   →   OnActionReset()               ← 节点重置（无参数，可通过 this.agent/blackboard 访问）
OnGraphPaused()   →   OnActionPause()         ← 图暂停
```

**子类必须实现**:

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `OnExecuteOnce(agent, blackboard)` | `Status` | 核心行为逻辑，每帧调用 |

**子类可选覆写**:

| 方法 | 说明 |
|------|------|
| `OnActionInit(agent, blackboard)` | 首次进入执行时初始化（缓存组件引用等） |
| `OnActionReset()` | 节点重置时清理状态 |
| `OnActionPause()` | 图暂停时停止行为 |

**关键设计**: `agent` 和 `blackboard` 属性在 `OnExecute` 入口自动缓存，Reset/Pause 时无需传参即可访问。

---

### 4.3 AIConditionNode — Condition 节点基类

**文件**: `Core/AIConditionNode.cs`

条件判断节点基类，采用**装饰器模式**（仅一个子节点输出）。条件满足时执行子节点，否则返回 Failure。

**生命周期**:

```
OnExecute(agent, blackboard)
    │
    ├── if status == Resting:
    │       OnConditionEnable(agent, blackboard)
    │
    ├── CheckCondition(agent, blackboard)?
    │       ├── false → return Failure
    │       └── true  → return outConnections[0].Execute(agent, blackboard)

OnReset()   →   OnConditionDisable()
```

**子类必须实现**: `CheckCondition(agent, blackboard)` → `bool`

**子类可选覆写**: `OnConditionEnable()` / `OnConditionDisable()`

---

### 4.4 AISensor — 传感器基类

**文件**: `Core/AIController.cs`（与 AIController 同文件）

抽象传感器基类，负责环境感知。子类实现 `DetectTarget()` 自定义检测逻辑。

**内置工具方法**:

| 方法 | 说明 |
|------|------|
| `DetectTarget()` | **抽象方法**，子类实现具体检测逻辑 |
| `Scan()` | 执行扫描，检测到目标变化时自动更新黑板和触发事件 |
| `HasLineOfSight(target)` | 检测是否有视线遮挡（Raycast） |
| `IsInFieldOfView(target)` | 检测目标是否在视野角度内 |
| `IsInDetectionRange(target)` | 检测目标是否在检测范围内 |
| `DrawGizmos()` | Scene 视图可视化（检测范围球、攻击范围球、目标连线） |

**事件**:

| 事件 | 说明 |
|------|------|
| `OnTargetChanged(old, new)` | 目标变更时触发 |
| `OnTargetDetected(target)` | 检测到新目标 |
| `OnTargetLost(target)` | 丢失目标 |

**Inspector 配置**:

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `detectionRange` | 15f | 检测范围（视野半径） |
| `attackRange` | 3f | 攻击范围 |
| `fieldOfView` | 120° | 视野角度 |
| `detectionMask` | -1 (Everything) | 检测层级掩码 |
| `obstacleMask` | 0 | 障碍物层级掩码 |

---

### 4.5 AIBlackboard / AIBBKey — 黑板键常量

**文件**: `Core/AIBlackboard.cs`

统一管理所有黑板变量名称，避免字符串硬编码。分为五大类：

| 类别 | 键名 | 类型 | 说明 |
|------|------|------|------|
| **目标** | `Target` | `Transform` | 当前目标引用 |
| | `TargetPosition` | `Vector3` | 目标位置 |
| | `LastKnownPosition` | `Vector3` | 目标最后已知位置 |
| | `IsTargetVisible` | `bool` | 目标是否可见 |
| | `DistanceToTarget` | `float` | 到目标的距离 |
| | `HasTarget` | `bool` | 是否有有效目标 |
| **战斗** | `IsInCombat` | `bool` | 是否在战斗中 |
| | `IsAttacking` | `bool` | 是否正在攻击 |
| | `LastDamage` | `float` | 最后一次伤害量 |
| | `HealthPercent` | `float` | 自身生命值百分比 (0~1) |
| | `ManaPercent` | `float` | 自身法力值百分比 (0~1) |
| | `TargetHealthPercent` | `float` | 目标生命值百分比 (0~1) |
| **移动** | `MoveSpeed` | `float` | 移动速度 |
| | `PatrolIndex` | `int` | 当前巡逻点索引 |
| | `IsMoving` | `bool` | 是否正在移动 |
| | `HasReachedDestination` | `bool` | 是否到达目的地 |
| | `PatrolWaitTime` | `float` | 巡逻等待剩余时间 |
| **技能** | `CurrentSkillId` | `string` | 当前技能 ID |
| | `SkillCooldownReady` | `bool` | 技能冷却是否就绪 |
| | `LastUsedSkillTime` | `float` | 上次使用技能时间 |
| **状态** | `AIState` | `int` | AI 状态（对应 `AIStateType` 枚举） |
| | `AlertLevel` | `int` | 警戒等级（对应 `AIAlertLevel` 枚举） |
| | `HasBuff` | `bool` | 是否有 Buff |
| | `BuffType` | `string` | Buff 类型 |
| **传感器** | `DetectionRange` | `float` | 检测范围 |
| | `AttackRange` | `float` | 攻击范围 |
| | `FleeHealthThreshold` | `float` | 逃跑血量阈值 |
| | `EnemyCount` | `int` | 敌人数量 |
| | `AllyCount` | `int` | 友军数量 |

**AI 状态枚举** (`AIStateType`): `Idle → Patrol → Chase → Combat → Flee → Dead`

**警戒等级枚举** (`AIAlertLevel`): `None(0) → Suspicious(1) → Alert(2) → Combat(3)`

---

### 4.6 AIGraph — 行为树资产

**文件**: `Graph/AIGraph.cs`

自定义 `BehaviourTree` ScriptableObject，扩展了 AI 元数据字段。

**字段**:

| 字段 | 类型 | 说明 |
|------|------|------|
| `aiTreeId` | `string` | 唯一标识符（GUID） |
| `treeName` | `string` | 行为树名称（调试用） |
| `treeDescription` | `string` | 行为树描述 |
| `aiType` | `AIType` | AI 类型标签 |
| `priority` | `int` (0~100) | 优先级，越大越优先 |

**编辑器菜单**: `Tools/Skills/AI/Create AI Behaviour Tree`

---

## 五、节点参考手册

### 5.1 Action 节点（行为节点）

#### MoveTo — 移动到目标

| 参数 | 类型 | 说明 |
|------|------|------|
| `mode` | `TargetMode` | `BlackboardTarget` / `BlackboardPosition` / `FixedPosition` |
| `targetKey` | `string` | 目标 Transform 黑板键（默认 `Target`） |
| `positionKey` | `string` | 目标位置黑板键（默认 `TargetPosition`） |
| `fixedPosition` | `Vector3` | 固定目标位置（mode=FixedPosition 时） |
| `arriveDistance` | `float` | 到达距离阈值（默认 1.5m） |
| `speedMultiplier` | `float` | 速度系数（0~1，默认 1） |
| `timeout` | `float` | 超时时间（0=永不超时） |
| `faceTarget` | `bool` | 移动时是否面向目标 |
| `rotationSpeed` | `float` | 面向旋转速度 |

**行为**: 优先使用 NavMeshAgent，无 NavMesh 时回退到 Transform 平移。到达后返回 `Success`，超时返回 `Failure`，移动中返回 `Running`。

**写入黑板**: `DistanceToTarget`, `HasReachedDestination`, `IsMoving`

---

#### AttackTarget — 攻击目标

| 参数 | 类型 | 说明 |
|------|------|------|
| `mode` | `AttackMode` | `Melee`（近战）/ `Ranged`（远程） |
| `targetKey` | `string` | 目标黑板键 |
| `attackRange` | `float` | 攻击范围（近战有效，默认 2.5m） |
| `damage` | `float` | 每次攻击伤害（默认 10） |
| `attackAnimationTrigger` | `string` | 攻击动画 Trigger 名（默认 "Attack"） |
| `attackSpeed` | `float` | 攻击速度（秒，默认 1） |
| `skillId` | `string` | 关联技能 ID（可选） |

**行为**: 近战模式下超出范围返回 Failure（由 MoveTo 处理靠近），冷却时间由 `attackSpeed` 控制。触发 Animator Trigger 并写入伤害到黑板。

**写入黑板**: `IsAttacking`, `LastDamage`, `CurrentSkillId`, `DistanceToTarget`, `TargetPosition`

---

#### CastSkill — 释放技能

| 参数 | 类型 | 说明 |
|------|------|------|
| `skillId` | `string` | 技能 ID |
| `targetKey` | `string` | 目标黑板键 |
| `castRange` | `float` | 技能释放距离（默认 10m） |

**行为**: 超出释放范围返回 Failure，面向目标后写入 `CurrentSkillId` 和 `LastUsedSkillTime`，与技能系统通过黑板通信。

---

#### Flee — 逃跑

| 参数 | 类型 | 说明 |
|------|------|------|
| `targetKey` | `string` | 威胁来源黑板键 |
| `safeDistance` | `float` | 安全距离（默认 15m） |
| `fleeSpeedMultiplier` | `float` | 逃跑速度系数（默认 1.5x） |

**行为**: 计算远离目标的方向，移动到安全距离外后返回 Success。无目标时直接返回 Success。

---

#### Idle — 待机

| 参数 | 类型 | 说明 |
|------|------|------|
| `duration` | `float` | 待机时间（秒） |
| `playRandomIdleAnimation` | `bool` | 是否播放随机待机动画 |

**行为**: 等待指定时间后返回 Success，等待中返回 Running。

---

#### PatrolWaypoints — 路径巡逻

| 参数 | 类型 | 说明 |
|------|------|------|
| `mode` | `PatrolMode` | `Loop`（循环）/ `Once`（单次）/ `PingPong`（往返） |
| `waypoints` | `List<Transform>` | 路径点列表 |
| `waitTime` | `float` | 每点等待时间（秒） |
| `maxCycles` | `int` | 最大巡逻次数（0=无限） |

**行为**: 沿路径点依次移动，到达后等待 `waitTime` 秒再前进。根据 `maxCycles` 控制循环次数。

**写入黑板**: `TargetPosition`, `DistanceToTarget`, `PatrolIndex`, `PatrolWaitTime`, `IsMoving`

---

#### PlayAnimation — 播放动画

| 参数 | 类型 | 说明 |
|------|------|------|
| `paramType` | `ParamType` | `Trigger` / `Bool` / `Float` |
| `paramName` | `string` | Animator 参数名 |
| `boolValue` | `bool` | Bool 值（paramType=Bool 时） |
| `floatValue` | `float` | Float 值（paramType=Float 时） |

**行为**: 立即设置 Animator 参数并返回 Success。未找到 Animator 组件返回 Failure。

---

#### SensorScan — 传感器扫描

| 参数 | 类型 | 说明 |
|------|------|------|
| `scanInterval` | `float` | 扫描间隔（秒，默认 0.5） |

**行为**: 驱动 `AISensor.Scan()` 执行环境检测。无传感器时回退到 `OverlapSphere` 检测。

---

### 5.2 Condition 节点（条件节点）

所有条件节点都继承 `AIConditionNode`，只有一个子节点输出。

| 节点 | 说明 | 关键参数 |
|------|------|----------|
| **HasTarget** | 检查是否有有效目标 | `targetKey`（目标键）, `requireAlive`（要求存活） |
| **IsTargetInRange** | 检查目标是否在范围内 | `targetKey`, `range`（范围距离）, `rangeKey`（动态范围键） |
| **IsHealthBelow** | 检查生命值是否低于阈值 | `threshold`（0~1）, `healthKey`（血量键） |
| **HasEnemyDetected** | 检查传感器是否检测到敌人 | `targetKey` |
| **BlackboardBool** | 检查黑板布尔值 | `key`（键名）, `expectedValue`, `resetChildOnFail` |
| **CooldownReady** | 检查技能冷却是否就绪 | `lastUsedKey`, `cooldown`（冷却秒数） |
| **CompareFloat** | 比较两个浮点数 | `mode`（Greater/Less/Equal/...）, `keyA`, `keyB`, `fixedValue` |

---

### 5.3 Composite 节点（组合节点）

#### PrioritizedSelector — 优先级选择器

按从左到右（从上到下）的优先级依次执行子节点。支持**动态重评估**（`dynamic` 模式）。

| 参数 | 类型 | 说明 |
|------|------|------|
| `dynamic` | `bool` | 每帧从第一个子节点重新检查，高优先级行为可打断低优先级 |
| `randomOrder` | `bool` | 启动时随机打乱子节点顺序 |

**返回值逻辑**: 遍历子节点 → 遇到 `Running` 返回 `Running` → 遇到 `Success` 返回 `Success` → 全部 `Failure` 返回 `Failure`

**典型场景**: `逃跑 > 治疗 > 攻击 > 巡逻 > 待机`

**GU 标记**: dynamic 模式显示 "⚡ DYNAMIC"，random 模式显示 "🎲 RANDOM"

---

#### ParallelAll — 并行全部

同时执行所有子节点。

| 参数 | 类型 | 说明 |
|------|------|------|
| `requireAllSuccess` | `bool` | 是否所有子节点成功才算成功 |
| `failOnAny` | `bool` | 任一失败时立即终止其他子节点 |

**典型场景**: 边移动边射击、同时巡逻和检测

---

### 5.4 Decorator 节点（装饰器节点）

#### Cooldown — 冷却装饰器

| 参数 | 类型 | 说明 |
|------|------|------|
| `cooldownTime` | `float` | 冷却时间（秒，默认 2） |
| `resetChildOnCooldownEnd` | `bool` | 冷却期满后自动重置子节点 |

**行为**: 子节点执行完成（Success/Failure）后进入冷却，冷却期内跳过子节点直接返回 Failure。

**GU 显示**: 冷却中显示 "⏳ CD: x.xs"，就绪时显示 "⏱ 就绪 (x.xs)"

---

#### TargetObserver — 目标观察装饰器

| 参数 | 类型 | 说明 |
|------|------|------|
| `maxObserveDistance` | `float` | 最大观察距离（0=不限） |
| `checkLineOfSight` | `bool` | 是否检查视线遮挡 |
| `targetMustBeAlive` | `bool` | 目标死亡是否算丢失 |
| `targetKey` | `string` | 目标黑板键 |

**行为**: 持续验证目标有效性（存在、存活、距离、视线）。目标丢失时自动清除黑板目标并返回 Failure。

---

## 六、使用指南

### 6.1 快速开始

1. **创建行为树**: 菜单 `Tools/Skills/AI/Create AI Behaviour Tree`，或 `Assets/Create/Skill System/AI Behaviour Tree`
2. **挂载 AI 控制器**: 在 AI 角色 GameObject 上添加 `AIController` 组件
3. **关联行为树**: 将创建的 `AIGraph` 资产拖拽到 `AIController` 的 `graph` 字段
4. **设置黑板**: 编辑行为树时，在 NodeCanvas Graph Editor 中添加所需黑板变量（参考 [AIBBKey](#45-aiblackboard--aibbkey--黑板键常量) 键名列表）
5. **搭建行为逻辑**: 在 Graph Editor 中拖拽节点，连线构建行为树

### 6.2 生成示例行为树

菜单 `Tools/Skills/AI/Generate Sample AI Trees` 会自动生成三套示例：

| 示例 | 路径 | 行为逻辑 |
|------|------|----------|
| `AI_Combat` | `Assets/Examples/AI/AI_Combat.asset` | 检测敌人 → 追击 → 攻击，低血量逃跑，无目标时待机扫描 |
| `AI_Patrol` | `Assets/Examples/AI/AI_Patrol.asset` | 巡逻为主，检测到敌人切换追击/攻击 |
| `AI_Boss` | `Assets/Examples/AI/AI_Boss.asset` | 三阶段战斗：常规攻击 → 召唤技能（HP<60%）→ 狂暴大招（HP<30%） |

### 6.3 推荐的行为树结构

```
[PrioritizedSelector - dynamic=true]
│
├── [逃跑分支 - 最高优先级]
│   └── Sequencer
│       ├── IsHealthBelow(threshold=0.25)
│       └── Flee
│
├── [战斗分支 - 中优先级]
│   └── Sequencer
│       ├── HasTarget
│       ├── TargetObserver           ← 目标持续有效
│       └── Selector                 ← 接近或攻击
│           ├── Sequencer
│           │   ├── IsTargetInRange(range=attackRange)
│           │   ├── Cooldown(cooldownTime=1/attackSpeed)
│           │   └── AttackTarget
│           └── MoveTo
│
└── [默认分支 - 最低优先级]
    └── Sequencer
        ├── SensorScan
        └── Idle(duration=0.5)
```

### 6.4 使用 AIController API

```csharp
// 获取 AI 控制器
var ai = GetComponent<AIController>();

// 读取/写入黑板
var hp = ai.GetBBValue<float>(AIBBKey.HealthPercent);
ai.SetBBValue(AIBBKey.HasTarget, true);

// 切换 AI 状态（自动同步黑板 + 触发事件）
ai.CurrentAIState = AIStateType.Combat;
ai.AlertLevel = AIAlertLevel.Combat;

// 监听状态变化
ai.OnStateChanged += (oldState, newState) => {
    Debug.Log($"AI 状态: {oldState} → {newState}");
};
```

### 6.5 自定义传感器

```csharp
public class MyCustomSensor : AISensor
{
    protected override Transform DetectTarget()
    {
        // 自定义检测逻辑
        var hits = Physics.OverlapSphere(transform.position, detectionRange, detectionMask);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy") && HasLineOfSight(hit.transform))
                return hit.transform;
        }
        return null;
    }
}
```

---

## 七、CSV 配置驱动的行为树自动创建

### 7.1 设计理念

遵循技能系统的"**CSV 是唯一数据源**"原则，AI 模块支持通过 `AITree.csv` 配置表批量定义和自动生成行为树资产。

**分工模型**:

| 角色 | 职责 | 工具 |
|------|------|------|
| **策划** | 在 CSV 中填写 AI 的行为链组成和数值参数 | Excel / 文本编辑器 |
| **程序** | 实现节点类型，维护 CSV→Graph 生成器 | AITreeGenerator.cs |
| **设计师** | 在 NodeCanvas 编辑器中微调生成的图结构 | Graph Editor |

**数据流**:

```
AITree.csv（策划维护）
    │
    │  保存文件 / AssetPostprocessor 自动触发
    ▼
AITreeGenerator（解析 CSV → 创建 AIGraph 资产）
    │
    ▼
Resources/AITrees/AI_xxx.asset（自动生成的 .asset 文件）
    │
    │  拖拽引用
    ▼
AIController.graph（运行时执行）
```

---

### 7.2 CSV 格式

**文件路径**: `Assets/Resources/Config/AITree.csv`

**编码**: UTF-8

**列定义**:

| 列名 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `tree_id` | `string` | ✅ | 行为树唯一标识，同 ID 的行属于同一棵树 |
| `tree_name` | `string` | | 行为树显示名称 |
| `ai_type` | `AIType` | | AI 类型标签（Combat/Patrol/Guard/Flee/Idle/Boss/Support） |
| `priority` | `int` (0-100) | | 行为树优先级，默认 50 |
| `update_interval` | `float` | | 更新间隔（秒），0=每帧 |
| `chain_order` | `int` | ✅ | 行为链序号（决定优先级，越小越优先，从 1 开始） |
| `node_order` | `int` | ✅ | 节点在链内的执行顺序（从 1 开始） |
| `node_class` | `string` | ✅ | 节点类名（大小写不敏感） |
| `params` | `string` | | 节点参数，格式 `key1=value1;key2=value2` |

**注释**: 以 `#` 开头的行会被忽略，可用于分组注释。

---

### 7.3 生成规则

CSV 中的每一行对应行为树中的一个**节点**。生成器按以下规则构建图结构：

```
PrioritizedSelector (dynamic=true, 根节点)
│
├── chain_order=1 → Sequencer (链1，最高优先级)
│       ├── node_order=1 → [节点类名] (params)
│       ├── node_order=2 → [节点类名] (params)
│       └── ...
│
├── chain_order=2 → Sequencer (链2)
│       └── ...
│
└── chain_order=3 → Sequencer (链3，最低优先级)
        └── ...
```

- **根节点**自动创建为 `PrioritizedSelector`（`dynamic=true`）
- **每个 `chain_order`** 自动包裹为一个 `Sequencer`，挂载到根节点
- **链内节点**按 `node_order` 升序串联
- **优先级**: `chain_order` 越小越高，越先被 PrioritizedSelector 评估

---

### 7.4 参数格式 `params`

参数列支持 `key=value;key=value` 格式，自动映射到 C# 字段/属性。

**类型自动转换**:

| C# 类型 | CSV 值示例 | 说明 |
|---------|-----------|------|
| `string` | `fireball` | 直接赋值 |
| `int` | `42` | 整数解析 |
| `float` | `2.5` | 浮点解析 |
| `bool` | `true`/`false`/`1`/`0` | 布尔解析 |
| 枚举 | `Melee`/`Loop` | 按名称匹配 |
| `BBParameter<float>` | `3.0` | 自动包装 |
| `BBParameter<int>` | `5` | 自动包装 |
| `Vector3` | `1,2,3` | 逗号分隔 |

**示例**:

```csv
combat,战斗AI,Combat,50,0.1,2,5,AttackTarget,damage=10;attackRange=2.5;attackSpeed=1;mode=Melee
combat,战斗AI,Combat,50,0.1,2,6,MoveTo,arriveDistance=1.5;speedMultiplier=0.8;faceTarget=true
patrol,巡逻AI,Patrol,50,0.15,2,1,PatrolWaypoints,waitTime=1.5;mode=Loop;maxCycles=0
combat,战斗AI,Combat,50,0.1,1,1,IsHealthBelow,threshold=0.25
boss,Boss AI,Boss,80,0,1,2,CastSkill,skillId=boss_ultimate;castRange=15
```

---

### 7.5 支持的节点类名一览

| 分类 | 节点类名 | 常用参数 |
|------|---------|----------|
| **组合** | `PrioritizedSelector` | （根节点自动创建） |
| | `Sequencer` | （链自动包裹） |
| | `Selector` | |
| | `ParallelAll` | `requireAllSuccess`, `failOnAny` |
| **装饰器** | `Cooldown` | `cooldownTime`, `resetChildOnCooldownEnd` |
| | `TargetObserver` | `maxObserveDistance`, `checkLineOfSight` |
| **行为** | `MoveTo` | `arriveDistance`, `speedMultiplier`, `timeout`, `faceTarget` |
| | `AttackTarget` | `damage`, `attackRange`, `attackSpeed`, `mode`(Melee/Ranged) |
| | `CastSkill` | `skillId`, `castRange` |
| | `Flee` | `safeDistance`, `fleeSpeedMultiplier` |
| | `Idle` | `duration`, `playRandomIdleAnimation` |
| | `PatrolWaypoints` | `waitTime`, `mode`(Loop/Once/PingPong), `maxCycles` |
| | `PlayAnimation` | `paramName`, `paramType`(Trigger/Bool/Float) |
| | `SensorScan` | `scanInterval` |
| **条件** | `HasTarget` | `targetKey`, `requireAlive` |
| | `IsTargetInRange` | `range`, `targetKey`, `rangeKey` |
| | `IsHealthBelow` | `threshold`, `healthKey` |
| | `HasEnemyDetected` | `targetKey` |
| | `BlackboardBool` | `key`, `expectedValue` |
| | `CooldownReady` | `lastUsedKey`, `cooldown` |
| | `CompareFloat` | `mode`(Greater/Less/Equal/...), `keyA`, `keyB`, `fixedValue` |

---

### 7.6 操作方式

**手动生成**: 菜单 `Tools/Skills/AI/Generate AI Trees from CSV`

**自动生成**: 保存 `AITree.csv` 后 `AITreeSyncPostprocessor` 自动触发

**Bootstrap 初始化**: 菜单 `Tools/Skills/Bootstrap All` 创建默认 CSV 及 4 套预置模板

---

### 7.7 预置模板

| tree_id | 名称 | 行为链 |
|---------|------|--------|
| `combat` | 战斗AI | ① 低血量逃跑 → ② 目标攻击 → ③ 待机扫描 |
| `patrol` | 巡逻AI | ① 遇敌追击攻击 → ② 路径巡逻 |
| `boss` | Boss AI | ① HP<30% 大招狂暴 → ② HP<60% 召唤 → ③ 常规攻击 |
| `guard` | 守卫AI | ① 遇敌追击攻击 → ② 警戒待机 |

---

### 7.8 扩展新节点到 CSV 生成器

在 `AITreeGenerator.NodeTypeMap` 中注册类名映射：

```csharp
{ "MyNewAction", typeof(MyNewAction) },
{ "MyNewCondition", typeof(MyNewCondition) },
```

---

### 7.9 完整生成示例

**CSV 输入**:

```csv
tree_id,tree_name,ai_type,priority,update_interval,chain_order,node_order,node_class,params
# === 链1：低血量逃跑 ===
combat,战斗AI,Combat,50,0.1,1,1,IsHealthBelow,threshold=0.25
combat,战斗AI,Combat,50,0.1,1,2,Flee,safeDistance=15;fleeSpeedMultiplier=1.5

# === 链2：目标攻击 ===
combat,战斗AI,Combat,50,0.1,2,1,HasTarget,
combat,战斗AI,Combat,50,0.1,2,2,TargetObserver,maxObserveDistance=30
combat,战斗AI,Combat,50,0.1,2,3,IsTargetInRange,range=2.5
combat,战斗AI,Combat,50,0.1,2,4,Cooldown,cooldownTime=1
combat,战斗AI,Combat,50,0.1,2,5,AttackTarget,damage=10;attackRange=2.5;attackSpeed=1
combat,战斗AI,Combat,50,0.1,2,6,MoveTo,arriveDistance=1.5

# === 链3：待机扫描 ===
combat,战斗AI,Combat,50,0.1,3,1,SensorScan,scanInterval=0.5
combat,战斗AI,Combat,50,0.1,3,2,Idle,duration=0.5
```

**生成的图结构**:

```
PrioritizedSelector (dynamic=true)
├── Sequencer (链1: 低血量逃跑)
│   ├── IsHealthBelow(threshold=0.25)
│   └── Flee(safeDistance=15, fleeSpeedMultiplier=1.5)
├── Sequencer (链2: 目标攻击)
│   ├── HasTarget
│   ├── TargetObserver(maxObserveDistance=30)
│   ├── IsTargetInRange(range=2.5)
│   ├── Cooldown(cooldownTime=1)
│   ├── AttackTarget(damage=10, attackRange=2.5, attackSpeed=1)
│   └── MoveTo(arriveDistance=1.5)
└── Sequencer (链3: 待机扫描)
    ├── SensorScan(scanInterval=0.5)
    └── Idle(duration=0.5)
```

---

## 八、扩展指南

### 8.1 添加新 Action 节点

```csharp
[Name("★ My Action")]
[Category("Composites/AI/Actions")]
[Description("我的自定义行为描述。")]
[Color("FF5722")]
public class MyAction : AIActionNode
{
    public BBParameter<float> myParam = 1f;

    protected override void OnActionInit(Component agent, IBlackboard blackboard)
    {
        // 初始化（缓存组件引用等）
    }

    protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
    {
        // 核心逻辑
        return Status.Success; // 或 Running / Failure
    }

    protected override void OnActionReset()
    {
        // 清理状态（可通过 this.agent / this.blackboard 访问）
        blackboard?.SetVariableValue("MyKey", false);
    }
}
```

### 8.2 添加新 Condition 节点

```csharp
[Name("★ My Condition")]
[Category("Composites/AI/Conditions")]
[Description("我的自定义条件。")]
[Color("26A69A")]
public class MyCondition : AIConditionNode
{
    public string myKey = "SomeBlackboardKey";

    protected override bool CheckCondition(Component agent, IBlackboard blackboard)
    {
        return blackboard.GetVariableValue<bool>(myKey);
    }
}
```

**注意**: `Category("Composites/AI/...")` 确保节点出现在 NodeCanvas 节点浏览器的 AI 分组中。

---

## 九、注意事项

1. **NodeCanvas 依赖**: 本模块依赖 `ParadoxNotion.NodeCanvas` 和 `CanvasCore` 框架，确保插件已正确导入。

2. **黑板变量初始化**: 使用节点前，需在行为树 Graph Editor 的黑板面板中预先定义对应变量（类型和键名需与 `AIBBKey` 常量一致）。

3. **`AIActionNode` 的 agent/blackboard 缓存**: `OnActionReset()` 和 `OnActionPause()` 无参数，通过 `this.agent` / `this.blackboard` 属性访问（在 `OnExecute` 入口自动缓存）。

4. **节点 Category**: 所有自定义节点使用 `Category("Composites/AI/...")` 确保在 NodeCanvas 编辑器菜单中有序分组。`★` 前缀使节点在列表中醒目。

5. **`AIController.updateInterval`**: 通过 `BehaviourTreeOwner.updateInterval` 属性设置更新间隔，不要直接修改 `graph.updateInterval`（`graph` 属性返回 `Graph` 基类，无此字段）。

6. **NavMeshAgent 依赖**: `MoveTo` 和 `Flee` 节点优先使用 NavMeshAgent，无 NavMesh 时回退到简易 Transform 移动。建议设置 AI 角色时添加 NavMeshAgent 组件。

7. **示例行为树**: 执行 `Generate Sample AI Trees` 会覆盖已有同路径文件（`Assets/Examples/AI/AI_Combat.asset` 等），请先备份自定义内容。

8. **CSV 驱动生成**: 修改 `AITree.csv` 后会自动重新生成 `Resources/AITrees/` 下的所有资产。如需手动触发，使用菜单 `Tools/Skills/AI/Generate AI Trees from CSV`。
