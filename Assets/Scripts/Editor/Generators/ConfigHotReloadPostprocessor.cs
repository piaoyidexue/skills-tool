using UnityEditor;

public class ConfigHotReloadPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        var shouldReload = false;

        foreach (var path in importedAssets)
            if (path.EndsWith(".csv") && path.Contains("/Resources/"))
            {
                shouldReload = true;
                break;
            }

        if (!shouldReload) return;

        EditorApplication.delayCall += ConfigLoader.ReloadAll;
    }
}