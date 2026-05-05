using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 流场寻路系统 - 基于Dijkstra算法的矢量场寻路
/// 专为塔防游戏优化，支持海量怪物同时寻路
/// </summary>
public class FlowFieldSystem : MonoBehaviour
{
    private static FlowFieldSystem _instance;
    public static FlowFieldSystem Instance => _instance;

    [Header("Flow Field Settings")]
    [Tooltip("流场更新频率（秒）")]
    [SerializeField] private float _updateInterval = 0.5f;

    [Tooltip("是否启用局部刷新")]
    [SerializeField] private bool _enablePartialRefresh = true;

    private LogicalGridManager _gridManager;
    
    // 流场数据
    private FlowFieldNode[] _flowField;
    private Vector2Int _gridSize;
    
    // 更新计时器
    private float _lastUpdateTime;

    // 待刷新区域
    private HashSet<GridCoord> _dirtyCells = new HashSet<GridCoord>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        _gridManager = LogicalGridManager.Instance;
        if (_gridManager == null)
        {
            Debug.LogError("[FlowFieldSystem] LogicalGridManager not found!");
        }
    }

    private void Start()
    {
        InitializeFlowField();
        
        // 监听建造事件
        _gridManager.OnCellOccupiedChanged += OnCellOccupiedChanged;
    }

    private void Update()
    {
        if (Time.time - _lastUpdateTime >= _updateInterval)
        {
            _lastUpdateTime = Time.time;
            
            if (_enablePartialRefresh && _dirtyCells.Count > 0)
            {
                RefreshPartialFlowField();
            }
            else
            {
                CalculateFlowField();
            }
        }
    }

    /// <summary>
    /// 初始化流场
    /// </summary>
    private void InitializeFlowField()
    {
        if (_gridManager == null) return;
        
        _gridSize = new Vector2Int(_gridManager.Width, _gridManager.Height);
        int totalCells = _gridSize.x * _gridSize.y;
        _flowField = new FlowFieldNode[totalCells];
        
        for (int i = 0; i < totalCells; i++)
        {
            _flowField[i].Cost = float.MaxValue;
            _flowField[i].Direction = Vector2.zero;
        }
    }

    /// <summary>
    /// 计算完整流场
    /// </summary>
    public void CalculateFlowField()
    {
        CalculateFlowField(_gridManager.Width / 2, _gridManager.Height - 1);
    }

    /// <summary>
    /// 计算流场（指定终点）
    /// </summary>
    public void CalculateFlowField(int endU, int endV)
    {
        if (_gridManager == null) return;
        
        // 重置流场
        for (int i = 0; i < _flowField.Length; i++)
        {
            _flowField[i].Cost = float.MaxValue;
            _flowField[i].Direction = Vector2.zero;
        }
        
        // Dijkstra算法
        PriorityQueue<GridCoord, float> openList = new PriorityQueue<GridCoord, float>();
        GridCoord endCoord = new GridCoord(endU, endV);
        
        if (_gridManager.IsValidGridPosition(endCoord))
        {
            int endIndex = GetIndex(endCoord);
            _flowField[endIndex].Cost = 0f;
            openList.Enqueue(endCoord, 0f);
        }
        
        GridCoord[] neighbors = new GridCoord[8];
        
        while (openList.Count > 0)
        {
            GridCoord current = openList.Dequeue();
            int currentIndex = GetIndex(current);
            
            // 获取邻居
            _gridManager.Topology.GetNeighbors(current);
            List<GridCoord> neighborList = _gridManager.Topology.GetNeighbors(current);
            
            foreach (GridCoord neighbor in neighborList)
            {
                if (!_gridManager.IsValidGridPosition(neighbor)) continue;
                
                int neighborIndex = GetIndex(neighbor);
                
                // 计算代价
                float cost = GetCellCost(neighbor);
                float newCost = _flowField[currentIndex].Cost + cost;
                
                if (newCost < _flowField[neighborIndex].Cost)
                {
                    _flowField[neighborIndex].Cost = newCost;
                    openList.Enqueue(neighbor, newCost);
                }
            }
        }
        
        // 计算方向
        for (int v = 0; v < _gridSize.y; v++)
        {
            for (int u = 0; u < _gridSize.x; u++)
            {
                GridCoord coord = new GridCoord(u, v);
                int index = GetIndex(coord);
                
                if (_flowField[index].Cost == float.MaxValue) continue;
                
                List<GridCoord> neighborList = _gridManager.Topology.GetNeighbors(coord);
                
                float minCost = float.MaxValue;
                GridCoord bestNeighbor = coord;
                
                foreach (GridCoord neighbor in neighborList)
                {
                    if (!_gridManager.IsValidGridPosition(neighbor)) continue;
                    
                    int nIndex = GetIndex(neighbor);
                    if (_flowField[nIndex].Cost < minCost)
                    {
                        minCost = _flowField[nIndex].Cost;
                        bestNeighbor = neighbor;
                    }
                }
                
                Vector2 direction = new Vector2(bestNeighbor.U - coord.U, bestNeighbor.V - coord.V);
                if (direction.sqrMagnitude > 0)
                {
                    direction.Normalize();
                }
                _flowField[index].Direction = direction;
            }
        }
        
        _dirtyCells.Clear();
    }

    /// <summary>
    /// 局部刷新流场
    /// </summary>
    private void RefreshPartialFlowField()
    {
        if (_dirtyCells.Count == 0) return;
        
        // 简单实现：重新计算整个流场
        // 优化版本可以只更新受影响的区域
        CalculateFlowField();
    }

    /// <summary>
    /// 获取单元格代价
    /// </summary>
    private float GetCellCost(GridCoord coord)
    {
        var cellData = _gridManager.GetCellData(coord);
        if (!cellData.HasValue) return float.MaxValue;
        
        switch (cellData.Value.Type)
        {
            case GridCellType.Blocked:
                return float.MaxValue;
            case GridCellType.HighGround:
                return 2f; // 高地代价较高
            case GridCellType.Buildable:
            case GridCellType.Walkable:
            default:
                return 1f;
        }
    }

    /// <summary>
    /// 获取移动方向
    /// </summary>
    public Vector2 GetDirection(GridCoord coord)
    {
        if (!_gridManager.IsValidGridPosition(coord)) return Vector2.zero;
        
        int index = GetIndex(coord);
        return _flowField[index].Direction;
    }

    /// <summary>
    /// 获取世界坐标方向
    /// </summary>
    public Vector3 GetWorldDirection(Vector3 worldPos)
    {
        GridCoord coord = _gridManager.Topology.WorldToGrid(worldPos);
        Vector2 direction = GetDirection(coord);
        
        return new Vector3(direction.x, 0f, direction.y);
    }

    /// <summary>
    /// 获取代价
    /// </summary>
    public float GetCost(GridCoord coord)
    {
        if (!_gridManager.IsValidGridPosition(coord)) return float.MaxValue;
        
        int index = GetIndex(coord);
        return _flowField[index].Cost;
    }

    /// <summary>
    /// 单元格占用变化回调
    /// </summary>
    private void OnCellOccupiedChanged(GridCoord coord, bool isOccupied)
    {
        if (_enablePartialRefresh)
        {
            _dirtyCells.Add(coord);
            
            // 添加相邻单元格
            List<GridCoord> neighbors = _gridManager.Topology.GetNeighbors(coord);
            foreach (var neighbor in neighbors)
            {
                _dirtyCells.Add(neighbor);
            }
        }
    }

    /// <summary>
    /// 获取一维索引
    /// </summary>
    private int GetIndex(GridCoord coord)
    {
        return coord.V * _gridSize.x + coord.U;
    }

    private void OnDestroy()
    {
        if (_gridManager != null)
        {
            _gridManager.OnCellOccupiedChanged -= OnCellOccupiedChanged;
        }
    }
}

/// <summary>
/// 流场节点数据
/// </summary>
public struct FlowFieldNode
{
    public float Cost;
    public Vector2 Direction;
}