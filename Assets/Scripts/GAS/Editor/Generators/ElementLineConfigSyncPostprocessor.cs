using UnityEditor;

public class ElementLineConfigSyncPostprocessor : AssetPostprocessor
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
            if (asset.EndsWith("SkillRecipe.csv") || asset.EndsWith("NodePreset.csv"))
            {
                shouldGenerate = true;
                break;
            }
        }

        if (!shouldGenerate)
        {
            return;
        }

        EditorApplication.delayCall += ElementLineSkillConfigGenerator.GenerateRuntimeSkillConfigSilently;
    }
}
