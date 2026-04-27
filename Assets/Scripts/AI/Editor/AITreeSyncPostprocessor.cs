using UnityEditor;

namespace SkillAI
{
    /// <summary>
    ///     AI 行为树 CSV 变更监听 —— 当 AITree.csv 被修改后，自动重新生成 AIGraph 资产。
    ///     遵循"CSV 是唯一数据源"原则，策划改表即自动生效。
    /// </summary>
    public class AITreeSyncPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var shouldGenerate = false;
            foreach (var asset in importedAssets)
            {
                if (asset.EndsWith("AITree.csv"))
                {
                    shouldGenerate = true;
                    break;
                }
            }

            if (!shouldGenerate) return;

            // 延迟执行，避免导入过程中操作 AssetDatabase
            EditorApplication.delayCall += AITreeGenerator.GenerateAllSilently;
        }
    }
}
