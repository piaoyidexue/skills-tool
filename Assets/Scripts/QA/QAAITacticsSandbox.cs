using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 模块五：AI 战术沙盘
/// - OnDrawGizmosSelected 绘制 SpatialHashGrid 网格可视化
/// - 刷怪/刷墙笔刷工具
/// - 集成 EQS Debugger 和性能监控
/// </summary>
[AddComponentMenu("Skills QA/AI Tactics Sandbox")]
public class QAAITacticsSandbox : MonoBehaviour
{
    public static QAAITacticsSandbox Instance { get; private set; }

    [Header("场景配置")]
    [SerializeField] private int _gridCellSize = 10;
    [SerializeField] private float _gridHeight = 0.05f;

    [Header("刷怪配置")]
    [SerializeField] private GameObject _spawnableMinionPrefab;
    [SerializeField] private GameObject _spawnableObstaclePrefab;
    [SerializeField] private int _spawnableTeamId = 1;

    [Header("可视化配置")]
    [SerializeField] private bool _showGrid = true;
    [SerializeField] private bool _showSpawnPoints = true;
    [SerializeField] private Color _gridColor = new Color(0.2f, 0.8f, 0.3f, 0.4f);
    [SerializeField] private Color _spawnPointColor = new Color(1f, 0.8f, 0f, 0.8f);
    [SerializeField] private Color _obstacleColor = new Color(0.5f, 0.3f, 1f, 0.6f);

    [Header("调试")]
    [SerializeField] private bool _showEntityIds = false;
    [SerializeField] private bool _showTeamColors = true;

    // 场景管理
    private readonly List<GameObject> _spawnedEntities = new();
    private readonly List<Vector3> _spawnPoints = new();
    private readonly HashSet<Vector3Int> _obstacleCells = new();

    // EQS Debugger 引用
    private QAEQSDebugger _eqsDebugger;
    private QAEQSDebugger EQS => _eqsDebugger ??= GetComponent<QAEQSDebugger>();

    // ===== MonoBehaviour =====

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearAll();
    }

    // ===== 公共 API =====

    /// <summary>添加刷怪点</summary>
    public void AddSpawnPoint(Vector3 position)
    {
        _spawnPoints.Add(position);
        Log($"刷怪点 +1 (共 {_spawnPoints.Count} 个)");
    }

    /// <summary>清除所有刷怪点</summary>
    public void ClearSpawnPoints()
    {
        _spawnPoints.Clear();
        Log("已清除所有刷怪点");
    }

    /// <summary>生成所有刷怪点的怪物</summary>
    public void SpawnAllMinions()
    {
        if (_spawnableMinionPrefab == null)
        {
            LogError("未设置怪物预制体!");
            return;
        }

        ClearEntities();

        foreach (var pos in _spawnPoints)
        {
            var go = Instantiate(_spawnableMinionPrefab, pos, Quaternion.identity);
            _spawnedEntities.Add(go);
        }

        Log($"生成了 {_spawnedEntities.Count} 个怪物");
    }

    /// <summary>生成障碍物</summary>
    public void SpawnObstacle(Vector3 position, float size = 5f)
    {
        if (_spawnableObstaclePrefab == null)
        {
            LogError("未设置障碍物预制体!");
            return;
        }

        var go = Instantiate(_spawnableObstaclePrefab, position, Quaternion.identity);
        go.transform.localScale = Vector3.one * size;
        _spawnedEntities.Add(go);

        var cellX = Mathf.FloorToInt(position.x / _gridCellSize);
        var cellZ = Mathf.FloorToInt(position.z / _gridCellSize);
        _obstacleCells.Add(new Vector3Int(cellX, 0, cellZ));

        Log($"障碍物已放置于 {position}, 格子 ({cellX}, {cellZ})");
    }

    /// <summary>清空所有已生成实体</summary>
    public void ClearEntities()
    {
        foreach (var go in _spawnedEntities)
            if (go != null) Destroy(go);
        _spawnedEntities.Clear();
        _obstacleCells.Clear();
        Log("已清空所有实体");
    }

    /// <summary>清空所有（实体 + 刷怪点）</summary>
    public void ClearAll()
    {
        ClearEntities();
        ClearSpawnPoints();
        Log("场景已重置");
    }

    /// <summary>运行 EQS 查询测试</summary>
    public void RunEQSQuery(Vector3 origin, Vector3 forward, float fov, float range)
    {
        if (EQS == null)
        {
            LogError("未找到 EQS Debugger 组件!");
            return;
        }

        EQS.ExecuteQuery(origin, forward, fov, range, _spawnableTeamId == 1 ? 2 : 1);
    }

    /// <summary>获取当前已注册实体数</summary>
    public int GetEntityCount()
    {
        var grid = SpatialHashGrid.Instance;
        return grid?.EntityCount ?? 0;
    }

    /// <summary>获取网格统计信息</summary>
    public (int entities, int cells, int dirty) GetGridStats()
    {
        var grid = SpatialHashGrid.Instance;
        return grid?.GetStats() ?? (0, 0, 0);
    }

    // ===== Editor Gizmos 绘制 =====

    private void OnDrawGizmosSelected()
    {
        if (!_showGrid) return;

        DrawGridVisualization();
        DrawSpawnPoints();
        DrawObstacles();
    }

    private void DrawGridVisualization()
    {
        var grid = SpatialHashGrid.Instance;
        if (grid == null) return;

        var stats = grid.GetStats();
        Gizmos.color = _gridColor;
        Gizmos.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, 1));

        // 使用较小的高度绘制网格线
        var gridHeight = _gridHeight;

        // 绘制已激活的格子
        var cells = typeof(SpatialHashGrid)
            .GetField("_cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(grid) as Dictionary<int, List<ISpatialEntity>>;

        if (cells != null)
        {
            foreach (var kvp in cells)
            {
                var key = kvp.Key;
                var x = (key / 73856093) * _gridCellSize + _gridCellSize * 0.5f;
                var z = (key % 73856093) / 19349663.0f;

                var cellPos = new Vector3(x, gridHeight, z);
                var cellSize = new Vector3(_gridCellSize, gridHeight, _gridCellSize);
                Gizmos.DrawWireCube(cellPos, cellSize);
            }
        }

        Gizmos.color = new Color(_gridColor.r, _gridColor.g, _gridColor.b, _gridColor.a * 0.5f);
        Gizmos.DrawCube(transform.position + Vector3.up * gridHeight, new Vector3(1, gridHeight, 1));
    }

    private void DrawSpawnPoints()
    {
        if (!_showSpawnPoints) return;

        Gizmos.color = _spawnPointColor;

        // 绘制刷怪点
        foreach (var pos in _spawnPoints)
        {
            // 主点
            Gizmos.DrawWireSphere(pos, 1f);
            // 顶部标记
            Gizmos.DrawLine(pos, pos + Vector3.up * 3f);
            Gizmos.DrawWireSphere(pos + Vector3.up * 3f, 0.5f);
        }

        // 绘制最近的刷怪点连线（如果有选中目标）
        #if UNITY_EDITOR
        if (UnityEditor.Selection.activeGameObject != null && _spawnPoints.Count > 0)
        {
            var targetPos = UnityEditor.Selection.activeGameObject.transform.position;
            var nearest = Vector3.zero;
            var minDist = float.MaxValue;

            foreach (var sp in _spawnPoints)
            {
                var d = Vector3.Distance(sp, targetPos);
                if (d < minDist) { minDist = d; nearest = sp; }
            }

            Gizmos.color = new Color(_spawnPointColor.r, _spawnPointColor.g, _spawnPointColor.b, 0.5f);
            Gizmos.DrawLine(nearest, targetPos);
        }
        #endif
    }

    private void DrawObstacles()
    {
        if (_spawnedEntities.Count == 0) return;

        Gizmos.color = _obstacleColor;

        foreach (var go in _spawnedEntities)
        {
            if (go == null) continue;

            // 检测是否为障碍物（通过缩放或名称）
            if (go.name.Contains("Obstacle") || go.transform.localScale.x > 2f)
            {
                var pos = go.transform.position;
                var size = go.transform.localScale.x;
                Gizmos.DrawWireCube(pos, Vector3.one * size);
            }
        }
    }

    // ===== 工具方法 =====

    private void Log(string msg)
    {
        UnityEngine.Debug.Log($"<color=yellow><b>[QAAITacticsSandbox]</b></color> {msg}");
    }

    private void LogError(string msg)
    {
        UnityEngine.Debug.LogError($"<color=red><b>[QAAITacticsSandbox]</b></color> {msg}");
    }
}

#if UNITY_EDITOR
/// <summary>
/// Editor 工具类：在 Scene 视图中提供笔刷工具
/// </summary>
public static class QAAITacticsBrush
{
    private static bool _brushActive;
    private static bool _isObstacle;

    [UnityEditor.MenuItem("Tools/Skills/QA/AI Brush Mode")]
    public static void ToggleBrushMode()
    {
        _brushActive = !_brushActive;
        UnityEditor.EditorUtility.DisplayDialog(
            "AI 刷怪笔刷",
            _brushActive ? "笔刷模式已启用\n左键点击场景放置刷怪点\n按住 Shift 点击放置障碍物" : "笔刷模式已禁用",
            "OK");
    }

    [UnityEditor.MenuItem("Tools/Skills/QA/Clear AI Sandbox")]
    public static void ClearSandbox()
    {
        var sandbox = UnityEngine.Object.FindObjectOfType<QAAITacticsSandbox>();
        if (sandbox != null)
            sandbox.ClearAll();
        else
            Debug.LogWarning("未找到 QAAITacticsSandbox 实例");
    }
}
#endif