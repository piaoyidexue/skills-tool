using UnityEngine;


/// <summary>
/// 拓扑切换测试脚本 - 展示如何在运行时切换网格拓扑
/// </summary>
public class TopologySwitchTest : MonoBehaviour
{
    private void Update()
    {
        // 按1键切换到正方形拓扑
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TopologyManager.Instance?.SwitchToSquareTopology();
        }
        
        // 按2键切换到六边形拓扑
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TopologyManager.Instance?.SwitchToHexagonTopology();
        }
        
        // 按T键打印当前拓扑类型
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log($"[TopologySwitchTest] Current topology: {TopologyManager.Instance?.GetCurrentTopologyName()}");
        }
    }
}