using UnityEngine;

[CreateAssetMenu(fileName = "PaintTerrainNode", menuName = "Skill System/Nodes/VFX/PaintTerrain")]
public class PaintTerrainNode : SkillNodeBase
{
    public StringBinding terrainTags = new() { LiteralValue = "scorch" };
    public float defaultRadius = 1.5f;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var tags = terrainTags.Resolve(ctx);
        if (string.IsNullOrWhiteSpace(tags)) return NodeTickResult.Success;

        var gridManager = LogicalGridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogWarning("[PaintTerrainNode] LogicalGridManager not found");
            return NodeTickResult.Failure;
        }

        var position = ctx.Target != null ? ctx.Target.position : (ctx.Caster != null ? ctx.Caster.position : Vector3.zero);
        var centerGridCoord = gridManager.Topology.WorldToGrid(position);
        
        // Get cells in radius around the center
        var affectedCells = gridManager.GetCellsInRadius(centerGridCoord, (int)defaultRadius);
        
        foreach (var rawTag in tags.Split('|'))
        {
            var tag = rawTag.Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var duration = ResolveTerrainDuration(tag);
            var terrainEffect = ResolveTerrainEffect(tag);
            
            // Update each affected cell
            foreach (var gridCoord in affectedCells)
            {
                var cellData = gridManager.GetCellData(gridCoord);
                if (cellData.HasValue)
                {
                    var updatedCellData = cellData.Value;
                    updatedCellData.TerrainType = terrainEffect;
                    updatedCellData.TerrainDuration = duration;
                    
                    // Notify VFXManager to create visual effect at grid center
                    var worldPos = gridManager.Topology.GridToWorld(gridCoord);
                    var vfxRequest = new VFXRequest
                    {
                        VFXKey = "TerrainEffect",
                        Position = worldPos,
                        ScaleMultiplier = 1f
                    };
                    VFXManager.Instance?.Play(vfxRequest);
                    
                    // Update the cell data
                    gridManager.SetCellData(gridCoord, updatedCellData);
                }
            }
        }

        return NodeTickResult.Success;
    }

    private static float ResolveTerrainDuration(string tag)
    {
        var terrainConfig = tag.ToLowerInvariant() switch
        {
            "scorch" => ConfigLoader.GetTerrainConfig("焦土地板"),
            "ice" => ConfigLoader.GetTerrainConfig("冰面地板"),
            "steam" => ConfigLoader.GetTerrainConfig("蒸汽地板"),
            "metal" => ConfigLoader.GetTerrainConfig("锻钢地板"),
            _ => null
        };

        return terrainConfig != null ? terrainConfig.DurationSeconds : 4f;
    }

    private static TerrainEffect ResolveTerrainEffect(string tag)
    {
        return tag.ToLowerInvariant() switch
        {
            "scorch" => TerrainEffect.Burnt,
            "ice" => TerrainEffect.Ice,
            "steam" => TerrainEffect.Swamp,
            "metal" => TerrainEffect.Rock,
            _ => TerrainEffect.None
        };
    }
}
