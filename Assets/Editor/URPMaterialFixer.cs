using UnityEditor;
using UnityEngine;

public class URPMaterialFixer
{
    [MenuItem("Tools/一键修复URP材质(带贴图迁移)")]
    static void Fix()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null) continue;

            string shaderName = mat.shader.name;

            // 只处理旧管线材质
            if (shaderName.Contains("Standard") || shaderName.Contains("Legacy"))
            {
                // ===== 先缓存旧数据 =====
                Texture mainTex = null;
                Color color = Color.white;
                Texture normalMap = null;
                Texture emissionMap = null;
                Color emissionColor = Color.black;

                if (mat.HasProperty("_MainTex"))
                    mainTex = mat.GetTexture("_MainTex");

                if (mat.HasProperty("_Color"))
                    color = mat.GetColor("_Color");

                if (mat.HasProperty("_BumpMap"))
                    normalMap = mat.GetTexture("_BumpMap");

                if (mat.HasProperty("_EmissionMap"))
                    emissionMap = mat.GetTexture("_EmissionMap");

                if (mat.HasProperty("_EmissionColor"))
                    emissionColor = mat.GetColor("_EmissionColor");

                // ===== 替换 Shader =====
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");

                // ===== 重新赋值（关键） =====
                if (mainTex != null && mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", mainTex);

                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);

                if (normalMap != null && mat.HasProperty("_BumpMap"))
                    mat.SetTexture("_BumpMap", normalMap);

                if (emissionMap != null && mat.HasProperty("_EmissionMap"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", emissionMap);
                    mat.SetColor("_EmissionColor", emissionColor);
                }
                else
                {
                    // 没有发光就关掉，避免你现在那种“绿色发光”
                    mat.DisableKeyword("_EMISSION");
                }

                EditorUtility.SetDirty(mat);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("URP材质修复完成（含贴图迁移）");
    }
}