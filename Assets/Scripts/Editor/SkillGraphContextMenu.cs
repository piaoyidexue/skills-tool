using UnityEditor;
using UnityEngine;
using NodeCanvas.Editor;
using NodeCanvas.Framework;

/// <summary>
///     技能图上下文菜单 —— 基于 CanvasCore 框架。
///     CanvasCore 的 GraphEditor 已内置节点菜单系统，
///     通过 SkillNode.OnContextMenu() 和 [ContextMenu] 属性扩展。
/// </summary>
public class SkillGraphContextMenu
{
    /// <summary>为选中节点切换断点状态（全局菜单入口）</summary>
    [MenuItem("Tools/Skills/Toggle Breakpoint", true)]
    private static bool ValidateToggleBreakpoint()
    {
        return GraphEditorUtility.activeNode is SkillNode;
    }

    [MenuItem("Tools/Skills/Toggle Breakpoint")]
    private static void ToggleBreakpointForSelection()
    {
        var node = GraphEditorUtility.activeNode as SkillNode;
        if (node == null) return;

        node.HasBreakpoint = !node.HasBreakpoint;

        if (GraphEditor.currentGraph != null)
            EditorUtility.SetDirty(GraphEditor.currentGraph);

        AssetDatabase.SaveAssets();
    }
}
