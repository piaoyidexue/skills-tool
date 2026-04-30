using UnityEditor;
using UnityEngine;

/// <summary>
///     技能图上下文菜单 —— 自建框架版。
///     基于 Unity Selection API，不依赖 NodeCanvas。
/// </summary>
public class SkillGraphContextMenu
{
    /// <summary>为选中节点切换断点状态（全局菜单入口）</summary>
    [MenuItem("Tools/Skills/Toggle Breakpoint", true)]
    private static bool ValidateToggleBreakpoint()
    {
        return Selection.activeObject is SkillNodeBase;
    }

    [MenuItem("Tools/Skills/Toggle Breakpoint")]
    private static void ToggleBreakpointForSelection()
    {
        var node = Selection.activeObject as SkillNodeBase;
        if (node == null) return;

        node.IsBreakpoint = !node.IsBreakpoint;
        EditorUtility.SetDirty(node);
        AssetDatabase.SaveAssets();
    }
}
