# 双拓扑网格系统（Square & Hexagon）

## 架构设计

采用**拓扑策略模式（Topology Strategy Pattern）**，将网格形状相关的数学运算与业务数据完全分离。

### 核心原则
- **统一寻址**：使用 `GridCoord(int U, int V)` 结构体统一表示坐标
- **数据层0 GC**：底层数据存储与拓扑无关，只关心一维索引
- **业务层无感**：所有技能、建造、共鸣逻辑只通过 `IGridTopology` 接口调用

## 拓扑实现

### SquareTopology（正方形）
- 坐标系：U = x, V = y
- 距离计算：曼哈顿距离
- 邻居：8方向
- 适用场景：传统塔防、RTS、ARPG

### HexagonTopology（六边形）
- 坐标系：尖顶朝上轴向坐标 (q, r)
- 距离计算：三轴曼哈顿距离的一半
- 邻居：6方向
- 适用场景：战棋游戏、策略游戏、六边形地形

## 使用方法

### 1. 在Inspector中配置
- 将 `LogicalGridManager` 的 `Topology` 字段设置为 `SquareTopology` 或 `HexagonTopology`
- 或在运行时通过 `TopologyManager` 切换

### 2. 业务系统调用
```csharp
// 无论什么拓扑，业务代码都一样
var gridCoord = GridManager.Topology.WorldToGrid(worldPosition);
var neighbors = GridManager.Topology.GetNeighbors(gridCoord);
var radiusCells = GridManager.Topology.GetCellsInRadius(gridCoord, 2);

// 建造建筑
GridManager.BuildTowerAt(gridCoord, entityId);
```

## 架构优势

1. **策划自由**：第一关正方形塔防，第二关六边形战棋，无需修改任何业务代码
2. **0 GC性能**：`GridCoord` 是值类型，配合 `Span<T>` 可实现零堆分配
3. **扩展性强**：新增拓扑只需实现 `IGridTopology` 接口
4. **表现层适配**：Gizmos、Mesh绘制自动适配不同拓扑

## API参考
- `WorldToGrid()` / `GridToWorld()`：坐标转换
- `GetDistance()`：距离计算
- `GetNeighbors()`：获取邻居
- `GetCellsInRadius()`：范围查询
- `GetLine()`：直线射线
- `GetOccupiedCells()`：建筑占位模板