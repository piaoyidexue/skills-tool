using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class ElementLineVFXFactory
{
    private const string ShaderFolder = "Assets/Shaders";
    private const string MaterialFolder = "Assets/Resources/VFX/Materials";
    private const string PrefabFolder = "Assets/Resources/VFX/Prefabs";

    [MenuItem("Tools/Skills/VFX/生成元素阵线特效资源")]
    public static void GenerateVfxResources()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/VFX");
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        // === 基础通用资源 ===
        var hitSparkMat = CreateMaterial("HitSpark.mat", "ElementLine/Pulse", new Color(1f, 0.55f, 0.16f, 1f));
        var frostBurstMat = CreateMaterial("FrostBurst.mat", "ElementLine/Pulse", new Color(0.7f, 0.95f, 1f, 1f));
        var explosionMat =
            CreateMaterial("ExplosionWave.mat", "ElementLine/GroundRing", new Color(1f, 0.72f, 0.32f, 1f));
        var beamMat = CreateMaterial("LightningBeam.mat", "ElementLine/Beam", new Color(0.72f, 0.95f, 1f, 1f));

        // === A. 施法脉冲 4元素独立材质 ===
        var castFireMat = CreateMaterial("CastPulse_Fire.mat", "ElementLine/Pulse", new Color(1f, 0.42f, 0.08f, 1f));
        var castIceMat = CreateMaterial("CastPulse_Ice.mat", "ElementLine/Pulse", new Color(0.55f, 0.88f, 1f, 1f));
        var castThunderMat =
            CreateMaterial("CastPulse_Thunder.mat", "ElementLine/Pulse", new Color(0.7f, 0.65f, 1f, 1f));
        var castMetalMat =
            CreateMaterial("CastPulse_Metal.mat", "ElementLine/Pulse", new Color(1f, 0.85f, 0.45f, 1f));

        // === A. 反应特效独立材质 ===
        var collapseMat =
            CreateMaterial("Reaction_Collapse.mat", "ElementLine/GroundRing", new Color(0.75f, 0.35f, 1f, 1f));
        var executeMat =
            CreateMaterial("Reaction_Execute.mat", "ElementLine/GroundRing", new Color(1f, 0.95f, 0.85f, 1f));

        // === A. 地表特效独立材质 ===
        var scorchFieldMat =
            CreateMaterial("Terrain_ScorchField.mat", "ElementLine/GroundRing", new Color(1f, 0.35f, 0.08f, 1f));
        var iceFieldMat =
            CreateMaterial("Terrain_IceField.mat", "ElementLine/GroundRing", new Color(0.45f, 0.78f, 1f, 1f));

        // === B. 3种形态光束独立材质 ===
        var arcBeamMat = CreateMaterial("ArcBeam.mat", "ElementLine/ArcBeam", new Color(0.55f, 0.82f, 1f, 1f));
        var prismBeamMat =
            CreateMaterial("PrismBeam.mat", "ElementLine/PrismBeam", new Color(0.75f, 0.88f, 1f, 1f));
        var bulwarkBeamMat =
            CreateMaterial("BulwarkBeam.mat", "ElementLine/BulwarkBeam", new Color(0.9f, 0.78f, 0.35f, 1f));

        // === C. 吸能特效独立材质 ===
        var energyAbsorbMat =
            CreateMaterial("EnergyAbsorb.mat", "ElementLine/EnergyAbsorb", new Color(0.55f, 0.3f, 1f, 1f));

        // === 创建 Prefab ===
        // 基础
        CreateImpactPrefab("HitSpark", hitSparkMat, typeof(ImpactPulseVFX));
        CreateImpactPrefab("FrostBurst", frostBurstMat, typeof(FrostBurstVFX));
        CreateImpactPrefab("ExplosionWave", explosionMat, typeof(ShockwaveRingVFX));
        CreateBeamPrefab("LightningBeam", beamMat, typeof(BeamVFX));

        // A. CastPulse
        CreateImpactPrefab("CastPulse_Fire", castFireMat, typeof(ImpactPulseVFX));
        CreateImpactPrefab("CastPulse_Ice", castIceMat, typeof(FrostBurstVFX));
        CreateImpactPrefab("CastPulse_Thunder", castThunderMat, typeof(ImpactPulseVFX));
        CreateImpactPrefab("CastPulse_Metal", castMetalMat, typeof(ImpactPulseVFX));

        // A. Reaction
        CreateImpactPrefab("Reaction_Collapse", collapseMat, typeof(ShockwaveRingVFX));
        CreateImpactPrefab("Reaction_Execute", executeMat, typeof(ShockwaveRingVFX));

        // A. Terrain
        CreateImpactPrefab("Terrain_ScorchField", scorchFieldMat, typeof(ShockwaveRingVFX));
        CreateImpactPrefab("Terrain_IceField", iceFieldMat, typeof(ShockwaveRingVFX));

        // B. Beam 形态
        CreateBeamPrefab("ArcBeam", arcBeamMat, typeof(ArcBeamVFX));
        CreateBeamPrefab("PrismBeam", prismBeamMat, typeof(PrismBeamVFX));
        CreateBeamPrefab("BulwarkBeam", bulwarkBeamMat, typeof(BulwarkBeamVFX));

        // C. EnergyAbsorb
        CreateImpactPrefab("EnergyAbsorb", energyAbsorbMat, typeof(EnergyAbsorbVFX));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ElementLineVFXFactory] 已生成全部粒子/Shader 特效资源（含独立语义 Prefab / Beam形态 / 吸能特效）。");
    }

    private static void CreateImpactPrefab(string prefabName, Material material, Type behaviourType)
    {
        var root = new GameObject(prefabName);
        var filter = root.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateQuadMesh(prefabName + "_Mesh");
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        root.AddComponent(behaviourType);
        SavePrefab(root, prefabName);
    }

    private static void CreateBeamPrefab(string prefabName, Material material, Type behaviourType)
    {
        var root = new GameObject(prefabName);
        var line = root.AddComponent<LineRenderer>();
        line.material = material;
        line.textureMode = LineTextureMode.Tile;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.positionCount = 2;
        line.widthMultiplier = 0.28f;
        root.AddComponent(behaviourType);
        SavePrefab(root, prefabName);
    }

    private static Material CreateMaterial(string fileName, string shaderName, Color tint)
    {
        var path = $"{MaterialFolder}/{fileName}";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"[ElementLineVFXFactory] 未找到 Shader: {shaderName}");
            return null;
        }

        var material = new Material(shader);
        material.SetColor("_Tint", tint);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SavePrefab(GameObject root, string prefabName)
    {
        var path = $"{PrefabFolder}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static Mesh CreateQuadMesh(string meshName)
    {
        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        });
        mesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        });
        mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}