using UnityEngine;

/// <summary>
/// 逻辑网格测试脚本 - 用于验证网格系统功能
/// </summary>
public class LogicalGridTest : MonoBehaviour
{
    private LogicalGridManager _gridManager;
    
    private void Awake()
    {
        _gridManager = LogicalGridManager.Instance;
        
        if (_gridManager != null)
        {
            _gridManager.OnCellChanged += OnCellChanged;
            _gridManager.OnCellOccupiedChanged += OnCellOccupiedChanged;
        }
    }
    
    private void OnDestroy()
    {
        if (_gridManager != null)
        {
            _gridManager.OnCellChanged -= OnCellChanged;
            _gridManager.OnCellOccupiedChanged -= OnCellOccupiedChanged;
        }
    }
    
    private void OnCellChanged(GridCoord gridCoord, GridCellData cellData)
    {
        Debug.Log($"[LogicalGridTest] Cell {gridCoord} changed to {cellData}");
    }
    
    private void OnCellOccupiedChanged(GridCoord gridCoord, bool isOccupied)
    {
        Debug.Log($"[LogicalGridTest] Cell {gridCoord} occupancy changed to {isOccupied}");
    }
    
    private void Update()
    {
        // 测试世界坐标到网格坐标的转换
        if (Input.GetKeyDown(KeyCode.T))
        {
            Vector3 worldPos = transform.position;
            GridCoord gridCoord = _gridManager.Topology.WorldToGrid(worldPos);
            Vector3 worldPosBack = _gridManager.Topology.GridToWorld(gridCoord);
            
            Debug.Log($"[LogicalGridTest] World {worldPos} -> Grid {gridCoord} -> World {worldPosBack}");
        }
        
        // 测试建造功能
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (_gridManager != null)
            {
                GridCoord gridCoord = _gridManager.Topology.WorldToGrid(transform.position);
                bool success = _gridManager.BuildTowerAt(gridCoord, 1001);
                Debug.Log($"[LogicalGridTest] Build at {gridCoord}: {success}");
            }
        }
        
        // 测试行列共鸣查询
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (_gridManager != null)
            {
                int row = 5;
                var towersInRow = _gridManager.GetTowerEntitiesInRow(row);
                Debug.Log($"[LogicalGridTest] Towers in row {row}: {towersInRow.Count}");
                
                int col = 3;
                var towersInCol = _gridManager.GetTowerEntitiesInColumn(col);
                Debug.Log($"[LogicalGridTest] Towers in column {col}: {towersInCol.Count}");
            }
        }
    }
}