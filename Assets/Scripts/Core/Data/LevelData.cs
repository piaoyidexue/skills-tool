using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 拓扑类型枚举
/// </summary>
public enum TopologyType
{
    Square,
    Hexagon
}



/// <summary>
/// 关卡数据 - 地图编辑器导出的纯数据结构
/// </summary>
[Serializable]
public class LevelData
{
    /// <summary>
    /// 关卡 ID
    /// </summary>
    public string LevelId;

    /// <summary>
    /// 拓扑类型
    /// </summary>
    public TopologyType TopologyType;

    /// <summary>
    /// 网格尺寸
    /// </summary>
    public Vector2Int GridSize;

    /// <summary>
    /// 单元格尺寸（米）
    /// </summary>
    public float CellSize;

    /// <summary>
    /// 网格原点
    /// </summary>
    public Vector3 Origin;

    /// <summary>
    /// 所有网格单元格数据
    /// </summary>
    public List<LevelGridCellData> GridCells;

    /// <summary>
    /// 所有路径数据
    /// </summary>
    public List<LevelPathData> Paths;

    /// <summary>
    /// 所有刷怪器数据
    /// </summary>
    public List<LevelSpawnerData> Spawners;

    /// <summary>
    /// 所有触发器数据
    /// </summary>
    public List<LevelTriggerData> Triggers;

    /// <summary>
    /// 美术场景路径
    /// </summary>
    public string ArtScenePath;

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version;

    /// <summary>
    /// 初始化空关卡数据
    /// </summary>
    public LevelData()
    {
        LevelId = "";
        TopologyType = TopologyType.Square;
        GridSize = new Vector2Int(50, 50);
        CellSize = 1f;
        Origin = Vector3.zero;
        GridCells = new List<LevelGridCellData>();
        Paths = new List<LevelPathData>();
        Spawners = new List<LevelSpawnerData>();
        Triggers = new List<LevelTriggerData>();
        ArtScenePath = "";
        Version = "1.0.0";
    }
}

/// <summary>
/// 关卡网格单元格数据（序列化用）
/// </summary>
[Serializable]
public struct LevelGridCellData
{
    public int X;
    public int Y;
    public GridCellType Type;
    public TerrainEffect Terrain;
}

/// <summary>
/// 关卡路径数据
/// </summary>
[Serializable]
public struct LevelPathData
{
    public string PathId;
    public List<Vector2Int> Nodes;
}

/// <summary>
/// 关卡刷怪器数据
/// </summary>
[Serializable]
public struct LevelSpawnerData
{
    public string SpawnerId;
    public Vector2Int Position;
    public string LinkedPathId;
    public int WaveConfigId;
    public int SquadId;
}

/// <summary>
/// 关卡触发器数据
/// </summary>
[Serializable]
public struct LevelTriggerData
{
    public string TriggerId;
    public TriggerType Type;
    public Vector2Int Position;
    public float Radius;
    public int EffectId;
    public string LinkedSquadId;
}

/// <summary>
/// 触发器类型
/// </summary>
public enum TriggerType
{
    Trap,
    CampSpawn,
    Hazard,
    Dialogue
}

/// <summary>
/// 共享路径数据（供 MinionBrain 使用）
/// </summary>
[System.Serializable]
public class SharedPathData
{
    public List<UnityEngine.Vector3> Waypoints;

    public SharedPathData()
    {
        Waypoints = new List<Vector3>();
    }
}
