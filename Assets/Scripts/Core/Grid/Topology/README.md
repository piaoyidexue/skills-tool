# 拓扑系统（双拓扑支持）

## 概述

本目录实现了网格系统的第一层——**数学拓扑层**，采用**策略模式**设计，支持正方形和六边形两种网格拓扑的无缝切换。

## 核心设计

### 统一坐标系统

使用 `GridCoord(int U, int V)` 结构体统一表示坐标：
- **正方形**：U = x, V = y（笛卡尔坐标）
- **六边形**：U = q, V = r（轴向坐标）

### 接口协议

`IGridTopology` 接口定义了所有拓扑必须实现的数学运算：

| 方法 | 功能 |
|------|------|
| `WorldToGrid()` | 世界坐标转网格坐标 |
| `GridToWorld()` | 网格坐标转世界坐标 |
| `GetDistance()` | 计算两坐标距离 |
| `GetNeighbors()` | 获取邻居坐标 |
| `GetCellsInRadius()` | 获取范围内所有坐标 |
| `GetLine()` | 获取直线上的坐标 |
| `GetOccupiedCells()` | 获取建筑占用的坐标 |

## 拓扑实现

### SquareTopology（正方形）

```csharp
// 创建实例
var topology = new SquareTopology(origin, cellSize);

// 特性
- 坐标系：U = x, V = y
- 距离计算：曼哈顿距离
- 邻居：8方向（上下左右+斜向）
- 适用：传统塔防、RTS、ARPG
```

### HexagonTopology（六边形）

```csharp
// 创建实例
var topology = new HexagonTopology(origin, cellSize);

// 特性
- 坐标系：尖顶朝上轴向坐标 (q, r)
- 距离计算：三轴曼哈顿距离的一半
- 邻居：6方向
- 适用：战棋游戏、策略游戏
```

## 使用方式

### 方式一：Inspector 配置

```
LogicalGridManager → Topology → SquareTopology/HexagonTopology
```

### 方式二：运行时切换

```csharp
// 通过 TopologyManager 切换
TopologyManager.Instance.SwitchToSquare();
TopologyManager.Instance.SwitchToHexagon();

// 或直接赋值
LogicalGridManager.Instance.Topology = new SquareTopology(origin, cellSize);
```

### 方式三：业务代码调用

```csharp
// 无论什么拓扑，业务代码都一样
var topology = LogicalGridManager.Instance.Topology;

GridCoord coord = topology.WorldToGrid(worldPosition);
var neighbors = topology.GetNeighbors(coord);
var range = topology.GetCellsInRadius(coord, 2);
```

## 0 GC 设计

虽然当前实现使用 `List<T>` 返回结果，但接口设计预留了优化空间：

```csharp
// 优化后的 0 GC 签名
void GetNeighbors(GridCoord center, ref GridCoord[] result, out int count);
void GetCellsInRadius(GridCoord center, int radius, ref GridCoord[] result, out int count);
```

## 架构优势

1. **业务无感**：所有技能、建造、共鸣逻辑不关心底层拓扑
2. **性能优秀**：`GridCoord` 是值类型，配合 `Span<T>` 可实现零堆分配
3. **扩展性强**：新增拓扑只需实现 `IGridTopology` 接口
4. **表现适配**：视觉系统自动适配不同拓扑的坐标转换