using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 关卡管理器 - 负责加载和管理 LevelData
/// </summary>
public class LevelManager : MonoBehaviour
{
    private static LevelManager _instance;
    public static LevelManager Instance => _instance;

    /// <summary>
    /// 当前加载的关卡数据
    /// </summary>
    public LevelData CurrentLevel { get; private set; }

    /// <summary>
    /// 路径注册表
    /// </summary>
    private Dictionary<string, LevelPathData> _pathRegistry;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        _pathRegistry = new Dictionary<string, LevelPathData>();
    }

    /// <summary>
    /// 从 Resources 加载关卡数据
    /// </summary>
    public bool LoadLevelFromResources(string levelId)
    {
        string path = $"LevelData/{levelId}";
        TextAsset jsonAsset = Resources.Load<TextAsset>(path);

        if (jsonAsset == null)
        {
            Debug.LogError($"[LevelManager] Level data not found: {path}");
            return false;
        }

        return LoadLevelFromJson(jsonAsset.text);
    }

    /// <summary>
    /// 从 JSON 字符串加载关卡数据
    /// </summary>
    public bool LoadLevelFromJson(string json)
    {
        try
        {
            CurrentLevel = JsonUtility.FromJson<LevelData>(json);

            if (CurrentLevel == null)
            {
                Debug.LogError("[LevelManager] Failed to parse LevelData");
                return false;
            }

            // 初始化路径注册表
            _pathRegistry.Clear();
            foreach (var path in CurrentLevel.Paths)
            {
                _pathRegistry[path.PathId] = path;
            }

            Debug.Log($"[LevelManager] Loaded level: {CurrentLevel.LevelId} (v{CurrentLevel.Version})");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] Error loading level: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 将关卡数据应用到 LogicalGridManager
    /// </summary>
    public void ApplyToGrid(LogicalGridManager gridManager)
    {
        if (CurrentLevel == null || gridManager == null)
        {
            Debug.LogError("[LevelManager] Cannot apply level: Missing data or grid manager");
            return;
        }

        for (int i = 0; i < CurrentLevel.GridCells.Count; i++)
        {
            var cell = CurrentLevel.GridCells[i];
            GridCoord coord = new GridCoord(cell.X, cell.Y);

            GridCellData cellData = new GridCellData();
            cellData.Initialize();

            // LevelData 的 GridCellType 和 GridCellData 的类型现在一致，直接赋值
            cellData.Type = cell.Type;

            cellData.TerrainType = cell.Terrain;

            gridManager.SetCellData(coord, cellData);
        }

        Debug.Log($"[LevelManager] Applied {CurrentLevel.GridCells.Count} cells to grid");
    }

    /// <summary>
    /// 获取路径数据
    /// </summary>
    public LevelPathData? GetPath(string pathId)
    {
        if (_pathRegistry.TryGetValue(pathId, out var path))
        {
            return path;
        }
        return null;
    }

    /// <summary>
    /// 将路径数据转换为 Transform 数组（供 MinionBrain 使用）
    /// </summary>
    public Transform[] ConvertPathToTransforms(string pathId, LogicalGridManager gridManager)
    {
        var pathData = GetPath(pathId);
        if (!pathData.HasValue || gridManager == null)
        {
            return new Transform[0];
        }

        List<Transform> transforms = new List<Transform>();

        // 创建临时空对象作为路径点
        GameObject pathParent = new GameObject($"Path_{pathId}");

        foreach (var node in pathData.Value.Nodes)
        {
            GameObject waypoint = new GameObject($"Waypoint_{node.x}_{node.y}");
            waypoint.transform.SetParent(pathParent.transform);

            // 转换网格坐标到世界坐标
            GridCoord coord = new GridCoord(node.x, node.y);
            waypoint.transform.position = gridManager.Topology.GridToWorld(coord);

            transforms.Add(waypoint.transform);
        }

        return transforms.ToArray();
    }

    /// <summary>
    /// 转换 Path 数据为 SharedPathData
    /// </summary>
    public SharedPathData ConvertToSharedPathData(string pathId, LogicalGridManager gridManager)
    {
        var pathData = GetPath(pathId);
        if (!pathData.HasValue || gridManager == null)
        {
            return new SharedPathData();
        }

        SharedPathData sharedPath = new SharedPathData();

        foreach (var node in pathData.Value.Nodes)
        {
            GridCoord coord = new GridCoord(node.x, node.y);
            sharedPath.Waypoints.Add(gridManager.Topology.GridToWorld(coord));
        }

        return sharedPath;
    }
}
