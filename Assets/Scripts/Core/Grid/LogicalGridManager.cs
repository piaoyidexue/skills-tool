using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 逻辑网格管理器 - 管理离散逻辑网格的数据结构和建造系统
/// </summary>
public class LogicalGridManager : MonoBehaviour
{
    private static LogicalGridManager _instance;
    public static LogicalGridManager Instance => _instance;

    [Header("Grid Configuration")] [Tooltip("网格配置")] [SerializeField]
    private GridConfig _gridConfig;

    [Header("Grid Data")] [Tooltip("网格数据数组（一维扁平化）")] [SerializeField]
    private GridCellData[] _gridData;

    [Header("Topology")] [Tooltip("网格拓扑实现（正方形或六边形）")] [SerializeField]
    private IGridTopology _topology;

    [Header("Placement Settings")] [Tooltip("是否启用建造校验")] [SerializeField]
    private bool _enablePlacementValidation = true;

    [Tooltip("建造时是否检查路径阻塞")] [SerializeField]
    private bool _checkPathBlocking = true;

    // 事件系统
    public event Action<GridCoord, GridCellData> OnCellChanged;
    public event Action<GridCoord, bool> OnCellOccupiedChanged;
    public event Action<GridCoord, TerrainEffect> OnTerrainEffectChanged;

    private int _width;
    private int _height;

    // 元素衰减更新时间
    private const float DECAY_INTERVAL = 0.1f;
    private float _lastDecayTime;

    public GridConfig Config => _gridConfig;
    public int Width => _width;
    public int Height => _height;
    public IGridTopology Topology { get => _topology; set => _topology = value; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 初始化网格配置
        if (_gridConfig.Size == Vector2Int.zero)
        {
            _gridConfig.Initialize();
        }
        
        _width = _gridConfig.Size.x;
        _height = _gridConfig.Size.y;
        
        // 初始化网格数据
        InitializeGridData();
        
        // 初始化拓扑
        if (_topology == null)
        {
            Debug.LogWarning("[LogicalGridManager] No topology assigned, using default SquareTopology");
            _topology = new SquareTopology(_gridConfig.Origin, _gridConfig.CellSize);
        }
        
        // 设置网格数学参数（兼容旧系统）
        GridMath.Origin = _gridConfig.Origin;
        GridMath.CellSize = _gridConfig.CellSize;
    }

    private void Update()
    {
        UpdateElementDecay();
    }
    
    /// <summary>
    /// 更新元素衰减
    /// </summary>
    private void UpdateElementDecay()
    {
        if (Time.time - _lastDecayTime < DECAY_INTERVAL) return;
        
        _lastDecayTime = Time.time;
        
        for (int i = 0; i < _gridData.Length; i++)
        {
            if (_gridData[i].TerrainType != TerrainEffect.None && _gridData[i].TerrainDuration > 0f)
            {
                _gridData[i].TerrainDuration -= DECAY_INTERVAL;
                
                if (_gridData[i].TerrainDuration <= 0f)
                {
                    GridCoord coord = GetCoordFromIndex(i);
                    TerrainEffect oldEffect = _gridData[i].TerrainType;
                    
                    _gridData[i].TerrainType = TerrainEffect.None;
                    _gridData[i].TerrainDuration = 0f;
                    
                    OnCellChanged?.Invoke(coord, _gridData[i]);
                    OnTerrainEffectChanged?.Invoke(coord, TerrainEffect.None);
                }
            }
        }
    }
    
    /// <summary>
    /// 根据索引获取网格坐标
    /// </summary>
    private GridCoord GetCoordFromIndex(int index)
    {
        int v = index / _width;
        int u = index % _width;
        return new GridCoord(u, v);
    }
    
    /// <summary>
    /// 设置地形效果
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <param name="effect">地形效果类型</param>
    /// <param name="duration">持续时间（秒）</param>
    /// <returns>是否成功设置</returns>
    public bool SetTerrainEffect(GridCoord gridCoord, TerrainEffect effect, float duration)
    {
        if (!IsValidGridPosition(gridCoord))
        {
            Debug.LogError($"[LogicalGridManager] Cannot set terrain effect at {gridCoord}: Out of bounds");
            return false;
        }
        
        int index = GetIndexFromGridPosition(gridCoord);
        if (index < 0 || index >= _gridData.Length)
        {
            Debug.LogError($"[LogicalGridManager] Invalid index {index} for grid coord {gridCoord}");
            return false;
        }
        
        GridCellData updatedCellData = _gridData[index];
        updatedCellData.TerrainType = effect;
        updatedCellData.TerrainDuration = duration;
        
        _gridData[index] = updatedCellData;
        
        OnCellChanged?.Invoke(gridCoord, updatedCellData);
        OnTerrainEffectChanged?.Invoke(gridCoord, effect);
        
        return true;
    }
    
    /// <summary>
    /// 设置区域地形效果
    /// </summary>
    /// <param name="center">中心坐标</param>
    /// <param name="radius">半径</param>
    /// <param name="effect">地形效果类型</param>
    /// <param name="duration">持续时间</param>
    public void SetTerrainEffectInRadius(GridCoord center, int radius, TerrainEffect effect, float duration)
    {
        var cells = _topology.GetCellsInRadius(center, radius);
        foreach (var cell in cells)
        {
            SetTerrainEffect(cell, effect, duration);
        }
    }
    
    /// <summary>
    /// 获取地形效果
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>地形效果类型</returns>
    public TerrainEffect GetTerrainEffect(GridCoord gridCoord)
    {
        var cellData = GetCellData(gridCoord);
        return cellData.HasValue ? cellData.Value.TerrainType : TerrainEffect.None;
    }
    
    /// <summary>
    /// 初始化网格数据数组
    /// </summary>
    private void InitializeGridData()
    {
        int totalCells = _width * _height;
        
        if (_gridData == null || _gridData.Length != totalCells)
        {
            _gridData = new GridCellData[totalCells];
        }
        
        // 初始化所有单元格
        for (int i = 0; i < _gridData.Length; i++)
        {
            _gridData[i].Initialize();
        }
    }
    
    /// <summary>
    /// 获取网格坐标对应的单元格数据
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>单元格数据，如果坐标越界则返回null</returns>
    public GridCellData? GetCellData(GridCoord gridCoord)
    {
        if (!IsValidGridPosition(gridCoord))
        {
            return null;
        }
        
        int index = GetIndexFromGridPosition(gridCoord);
        if (index < 0 || index >= _gridData.Length)
        {
            Debug.LogError($"[LogicalGridManager] Invalid index {index} for grid coord {gridCoord}");
            return null;
        }
        
        return _gridData[index];
    }
    
    /// <summary>
    /// 设置网格坐标对应的单元格数据
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <param name="cellData">单元格数据</param>
    /// <returns>是否成功设置</returns>
    public bool SetCellData(GridCoord gridCoord, GridCellData cellData)
    {
        if (!IsValidGridPosition(gridCoord))
        {
            Debug.LogError($"[LogicalGridManager] Cannot set cell data at {gridCoord}: Out of bounds");
            return false;
        }
        
        int index = GetIndexFromGridPosition(gridCoord);
        if (index < 0 || index >= _gridData.Length)
        {
            Debug.LogError($"[LogicalGridManager] Invalid index {index} for grid coord {gridCoord}");
            return false;
        }
        
        _gridData[index] = cellData;
        
        OnCellChanged?.Invoke(gridCoord, cellData);
        
        return true;
    }
    
    /// <summary>
    /// 检查指定位置是否可以建造防御塔
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>是否可以建造</returns>
    public bool CanBuildTowerAt(GridCoord gridCoord)
    {
        if (!_enablePlacementValidation) return true;
        
        // 1. 检查是否超出地图边界
        if (!IsValidGridPosition(gridCoord))
        {
            Debug.Log($"[LogicalGridManager] Cannot build at {gridCoord}: Out of bounds");
            return false;
        }
        
        // 2. 检查单元格类型是否支持建造
        var cellData = GetCellData(gridCoord);
        if (!cellData.HasValue || !cellData.Value.IsBuildable())
        {
            Debug.Log($"[LogicalGridManager] Cannot build at {gridCoord}: Invalid cell type or occupied");
            return false;
        }
        
        // 3. 检查是否已被其他塔占用
        if (cellData.Value.OccupiedBy != -1)
        {
            Debug.Log($"[LogicalGridManager] Cannot build at {gridCoord}: Already occupied");
            return false;
        }
        
        // 4. 检查是否会导致路径完全阻塞（如果启用）
        if (_checkPathBlocking && WouldBlockPath(gridCoord))
        {
            Debug.Log($"[LogicalGridManager] Cannot build at {gridCoord}: Would block path");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 检查在指定位置建造是否会完全阻塞敌人路径
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>是否会阻塞路径</returns>
    private bool WouldBlockPath(GridCoord gridCoord)
    {
        // 使用拓扑接口获取邻居
        var adjacentCells = _topology.GetNeighbors(gridCoord);
        
        int passableCount = 0;
        foreach (var adj in adjacentCells)
        {
            if (IsValidGridPosition(adj))
            {
                var cellData = GetCellData(adj);
                if (cellData.HasValue && IsPassableCell(cellData.Value))
                {
                    passableCount++;
                }
            }
        }
        
        // 如果周围可通行单元格少于2个，则可能造成阻塞
        return passableCount < 2;
    }
    
    /// <summary>
    /// 检查单元格是否可通行
    /// </summary>
    /// <param name="cellData">单元格数据</param>
    /// <returns>是否可通行</returns>
    private bool IsPassableCell(GridCellData cellData)
    {
        return cellData.Type != GridCellType.Blocked && 
               cellData.OccupiedBy == -1;
    }
    
    /// <summary>
    /// 在指定位置建造防御塔
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <param name="entityId">实体ID</param>
    /// <returns>是否成功建造</returns>
    public bool BuildTowerAt(GridCoord gridCoord, int entityId)
    {
        if (!CanBuildTowerAt(gridCoord))
        {
            return false;
        }
        
        var cellData = GetCellData(gridCoord);
        if (!cellData.HasValue)
        {
            Debug.LogError($"[LogicalGridManager] Cannot build at {gridCoord}: Cell data is null");
            return false;
        }
        
        // 更新占用状态 - 需要先获取值，修改后再赋值
        GridCellData updatedCellData = cellData.Value;
        updatedCellData.OccupiedBy = entityId;
        
        // 更新网格数据
        SetCellData(gridCoord, updatedCellData);
        
        // 触发占用变更事件
        OnCellOccupiedChanged?.Invoke(gridCoord, true);
        
        Debug.Log($"[LogicalGridManager] Built tower at {gridCoord} with entity ID {entityId}");
        
        return true;
    }
    
    /// <summary>
    /// 移除指定位置的防御塔
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveTowerAt(GridCoord gridCoord)
    {
        var cellData = GetCellData(gridCoord);
        if (!cellData.HasValue || cellData.Value.OccupiedBy == -1)
        {
            return false;
        }
        
        // 清空占用状态 - 需要先获取值，修改后再赋值
        GridCellData updatedCellData = cellData.Value;
        updatedCellData.OccupiedBy = -1;
        
        // 更新网格数据
        SetCellData(gridCoord, updatedCellData);
        
        // 触发占用变更事件
        OnCellOccupiedChanged?.Invoke(gridCoord, false);
        
        Debug.Log($"[LogicalGridManager] Removed tower at {gridCoord}");
        
        return true;
    }
    
    /// <summary>
    /// 获取指定网格坐标的所有已建造塔的实体ID列表
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>实体ID列表</returns>
    public List<int> GetTowerEntitiesInRow(int row)
    {
        var entities = new List<int>();
        
        for (int x = 0; x < _width; x++)
        {
            var gridPos = new GridCoord(x, row);
            var cellData = GetCellData(gridPos);
            if (cellData.HasValue && cellData.Value.OccupiedBy > 0)
            {
                entities.Add(cellData.Value.OccupiedBy);
            }
        }
        
        return entities;
    }
    
    /// <summary>
    /// 获取指定网格坐标的所有已建造塔的实体ID列表
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>实体ID列表</returns>
    public List<int> GetTowerEntitiesInColumn(int column)
    {
        var entities = new List<int>();
        
        for (int y = 0; y < _height; y++)
        {
            var gridPos = new GridCoord(column, y);
            var cellData = GetCellData(gridPos);
            if (cellData.HasValue && cellData.Value.OccupiedBy > 0)
            {
                entities.Add(cellData.Value.OccupiedBy);
            }
        }
        
        return entities;
    }
    
    /// <summary>
    /// 检查网格坐标是否有效（在边界内）
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>是否有效</returns>
    public bool IsValidGridPosition(GridCoord gridCoord)
    {
        if (!_gridConfig.EnableBoundsCheck) return true;
        
        // 使用拓扑接口进行边界检查
        return gridCoord.U >= 0 && gridCoord.U < _width &&
               gridCoord.V >= 0 && gridCoord.V < _height;
    }
    
    /// <summary>
    /// 根据网格坐标获取一维数组索引
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>数组索引</returns>
    private int GetIndexFromGridPosition(GridCoord gridCoord)
    {
        // 确保坐标在有效范围内
        int u = Mathf.Clamp(gridCoord.U, 0, _width - 1);
        int v = Mathf.Clamp(gridCoord.V, 0, _height - 1);
        return v * _width + u;
    }
    
    /// <summary>
    /// 获取网格中所有已建造塔的实体ID列表
    /// </summary>
    /// <returns>实体ID列表</returns>
    public List<int> GetAllTowerEntities()
    {
        var entities = new List<int>();
        
        for (int i = 0; i < _gridData.Length; i++)
        {
            if (_gridData[i].OccupiedBy > 0)
            {
                entities.Add(_gridData[i].OccupiedBy);
            }
        }
        
        return entities;
    }
    
    /// <summary>
    /// 获取指定区域内的所有单元格坐标
    /// </summary>
    /// <param name="center">中心网格坐标</param>
    /// <param name="radius">半径（曼哈顿距离）</param>
    /// <returns>单元格坐标列表</returns>
    public List<GridCoord> GetCellsInRadius(GridCoord center, int radius)
    {
        return _topology.GetCellsInRadius(center, radius);
    }
    
    /// <summary>
    /// 获取指定方向上的直线坐标
    /// </summary>
    /// <param name="start">起始坐标</param>
    /// <param name="direction">方向向量</param>
    /// <param name="length">长度</param>
    /// <returns>直线上的坐标列表</returns>
    public List<GridCoord> GetLine(GridCoord start, Vector2 direction, int length)
    {
        return _topology.GetLine(start, direction, length);
    }
    
    /// <summary>
    /// 获取建筑占用的坐标列表
    /// </summary>
    /// <param name="center">中心坐标</param>
    /// <param name="size">建筑大小</param>
    /// <returns>占用的坐标列表</returns>
    public List<GridCoord> GetOccupiedCells(GridCoord center, int size)
    {
        return _topology.GetOccupiedCells(center, size);
    }
}