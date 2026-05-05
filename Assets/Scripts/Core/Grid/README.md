# 网格地图系统（六层架构）

## 概述

本模块实现了一个高性能的离散逻辑网格系统，采用**六层架构**设计，实现了 0 GC、万级实体寻路、满屏元素地表不掉帧的极致性能目标。

## 六层架构设计

### 第一层：数学拓扑层（Mathematical Topology Layer）

**核心功能：**
- 坐标系抽象与统一：屏蔽正方形和六边形的坐标系差异
- 空间数学运算：世界坐标与网格坐标互转、距离计算、邻居获取、范围圈定

**关键实现：**
- `IGridTopology` 接口协议
- 扁平化一维映射：`Index = U + V * MapWidth`
- 0 GC 设计：使用 `ref` 参数和预先分配的缓存数组

### 第二层：静态数据层（Static Data Layer）

**核心功能：**
- 高度与地形感知：记录网格的绝对高度和地表法线
- 通行与建造规则：定义哪些格子可通行、可建造

**关键实现：**
- `GridCellData` 结构体存储类型、占用状态、地形效果
- 离线烘焙工具（GridBaker）生成数据
- ScriptableObject 持久化资产

### 第三层：动态状态层（Dynamic State Layer）

**核心功能：**
- 空间占用登记：记录格子被哪个实体占据
- 元素地表管理：记录元素附着状态及残留时间

**关键实现：**
- `GridCellData.OccupiedBy` 和 `TerrainType` 字段
- 时间轴清理机制：定时扣减元素持续时间

### 第四层：空间算法与查询层（Spatial Algorithm Layer）

**核心功能：**
- 动态寻路：结合静态和动态数据的路径计算
- 视线遮挡检测：判断射击轨迹是否被阻挡

**关键实现：**
- 流场寻路（Flow Field）支持海量怪物
- Bresenham 画线算法进行视线检测

### 第五层：业务逻辑集成层（Gameplay Integration Layer）

**核心功能：**
- 建造校验管线：极速造塔合法性验证
- 网格触发元素反应：站在火上受伤害、冰上减速
- 行列共鸣查询：同列/同行技能强化判定

### 第六层：视觉与表现层（Visual Presentation Layer）

**核心功能：**
- 平滑移动插值：角色移动时的高度插值
- 地形完美贴花：元素特效贴合起伏地形
- 交互全息投影：建造时的绿色/红色投影

## 核心组件

| 组件 | 职责 |
|------|------|
| `GridCellData.cs` | 单元格数据结构（0 GC） |
| `LogicalGridManager.cs` | 网格管理器主入口 |
| `IGridTopology.cs` | 拓扑接口定义 |
| `SquareTopology.cs` | 正方形拓扑实现 |
| `HexagonTopology.cs` | 六边形拓扑实现 |
| `TopologyManager.cs` | 拓扑切换管理 |
| `PlacementValidator.cs` | 建造校验 |
| `GridVisualManager.cs` | 视觉表现管理 |
| `GridBakerWindow.cs` | 离线烘焙工具 |

## 性能特性

1. **0 GC 设计**：所有方法使用 `ref` 参数和预先分配的缓存数组
2. **O(1) 数组寻址**：`Index = U + V * Width` 的一维数组访问
3. **SOA 布局**：数据按列存储，提高缓存命中率
4. **降频更新**：元素衰减等非关键逻辑每 0.1 秒批量更新
5. **流场寻路**：怪物只需查询方向，计算量极小

## 使用示例

```csharp
// 坐标转换
GridCoord coord = LogicalGridManager.Instance.Topology.WorldToGrid(worldPos);
Vector3 worldPos = LogicalGridManager.Instance.Topology.GridToWorld(coord);

// 建造校验
bool canBuild = LogicalGridManager.Instance.CanPlaceAt(coord, towerSize);

// 设置元素地表
LogicalGridManager.Instance.SetTerrainEffect(coord, TerrainEffect.Fire, 5f);

// 获取邻居
var neighbors = LogicalGridManager.Instance.Topology.GetNeighbors(coord);

// 范围查询
var cells = LogicalGridManager.Instance.Topology.GetCellsInRadius(coord, 3);
```

## 数据流

```
用户输入 → 业务逻辑层 → 空间算法层 → 动态状态层 → 静态数据层
                                              ↓
                                        视觉表现层
```

## 架构优势

1. **极致性能**：放弃物理引擎，全部转为 O(1) 数组读写和位运算
2. **数据安全**：视觉表现延迟不影响逻辑判断
3. **极强扩展**：替换拓扑或表现层，中间层无需修改