using System;
using UnityEngine;


/// <summary>
/// 拓扑管理器 - 用于在运行时动态切换网格拓扑
/// </summary>
public class TopologyManager : MonoBehaviour
{
    private static TopologyManager _instance;
    public static TopologyManager Instance => _instance;
    
    [Header("Topology Configuration")]
    [Tooltip("正方形拓扑配置")]
    [SerializeField] private SquareTopology _squareTopology;
    
    [Tooltip("六边形拓扑配置")]
    [SerializeField] private HexagonTopology _hexagonTopology;
    
    private LogicalGridManager _gridManager;
    
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
            Debug.LogError("[TopologyManager] LogicalGridManager not found!");
        }
    }
    
    /// <summary>
    /// 切换到正方形拓扑
    /// </summary>
    public void SwitchToSquareTopology()
    {
        if (_gridManager == null || _squareTopology == null) return;
        
        _gridManager.Topology = _squareTopology;
        Debug.Log("[TopologyManager] Switched to Square Topology");
    }
    
    /// <summary>
    /// 切换到六边形拓扑
    /// </summary>
    public void SwitchToHexagonTopology()
    {
        if (_gridManager == null || _hexagonTopology == null) return;
        
        _gridManager.Topology = _hexagonTopology;
        Debug.Log("[TopologyManager] Switched to Hexagon Topology");
    }
    
    /// <summary>
    /// 获取当前拓扑类型
    /// </summary>
    public string GetCurrentTopologyName()
    {
        if (_gridManager == null || _gridManager.Topology == null)
        {
            return "Unknown";
        }
        
        return _gridManager.Topology.GetType().Name;
    }
}