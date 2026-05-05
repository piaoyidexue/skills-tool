using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第六层：视觉与表现层 - 网格视觉管理器
/// </summary>
public class GridVisualManager : MonoBehaviour
{
    private static GridVisualManager _instance;
    public static GridVisualManager Instance => _instance;

    [Header("Visual Settings")]
    [SerializeField] private Material _buildableMaterial;
    [SerializeField] private Material _blockedMaterial;
    [SerializeField] private Material _walkableMaterial;
    [SerializeField] private Material _highGroundMaterial;

    [Header("Element Visuals")]
    [SerializeField] private GameObject _fireDecalPrefab;
    [SerializeField] private GameObject _iceDecalPrefab;
    [SerializeField] private GameObject _poisonDecalPrefab;
    [SerializeField] private GameObject _lightningDecalPrefab;

    [Header("Build Indicator")]
    [SerializeField] private GameObject _buildIndicatorPrefab;

    // 元素贴花缓存
    private Dictionary<GridCoord, GameObject> _elementDecals = new Dictionary<GridCoord, GameObject>();

    // 建造指示器
    private GameObject _buildIndicator;

    // 网格渲染器缓存
    private MeshRenderer[] _gridRenderers;

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
    }

    private void Start()
    {
        _gridManager = LogicalGridManager.Instance;
        if (_gridManager != null)
        {
            _gridManager.OnCellChanged += OnCellChanged;
        }

        InitializeGridVisuals();
    }

    private void OnCellChanged(GridCoord coord, GridCellData cellData)
    {
        UpdateCellVisual(coord);
        UpdateElementVisual(coord, cellData.TerrainType);
    }

    private void InitializeGridVisuals()
    {
        if (_gridManager == null) return;

        int width = _gridManager.Width;
        int height = _gridManager.Height;
        float cellSize = _gridManager.Config.CellSize;
        Vector3 origin = _gridManager.Config.Origin;

        // 创建网格平面
        GameObject gridParent = new GameObject("GridVisual");
        gridParent.transform.SetParent(transform);

        _gridRenderers = new MeshRenderer[width * height];

        for (int v = 0; v < height; v++)
        {
            for (int u = 0; u < width; u++)
            {
                GridCoord coord = new GridCoord(u, v);
                Vector3 worldPos = _gridManager.Topology.GridToWorld(coord);
                worldPos.y = 0.01f;

                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Plane);
                cell.name = $"Cell_{u}_{v}";
                cell.transform.SetParent(gridParent.transform);
                cell.transform.position = worldPos;
                cell.transform.localScale = Vector3.one * cellSize * 0.1f;

                MeshRenderer renderer = cell.GetComponent<MeshRenderer>();
                renderer.material = GetMaterialForCell(coord);

                Destroy(cell.GetComponent<Collider>());

                _gridRenderers[GetIndex(coord, width)] = renderer;
            }
        }
    }

    private Material GetMaterialForCell(GridCoord coord)
    {
        if (_gridManager == null) return _walkableMaterial;

        var cellData = _gridManager.GetCellData(coord);
        if (!cellData.HasValue)
        {
            return _blockedMaterial;
        }

        switch (cellData.Value.Type)
        {
            case GridCellType.Blocked:
                return _blockedMaterial;
            case GridCellType.HighGround:
                return _highGroundMaterial;
            case GridCellType.Buildable:
                return _buildableMaterial;
            case GridCellType.Walkable:
            default:
                return _walkableMaterial;
        }
    }

    /// <summary>
    /// 更新单元格颜色
    /// </summary>
    public void UpdateCellVisual(GridCoord coord)
    {
        int index = GetIndex(coord, _gridManager.Width);
        if (index < 0 || index >= _gridRenderers.Length) return;

        _gridRenderers[index].material = GetMaterialForCell(coord);
    }

    /// <summary>
    /// 更新元素视觉效果
    /// </summary>
    private void UpdateElementVisual(GridCoord coord, TerrainEffect terrainEffect)
    {
        // 移除旧贴花
        if (_elementDecals.TryGetValue(coord, out GameObject oldDecal))
        {
            Destroy(oldDecal);
            _elementDecals.Remove(coord);
        }

        // 创建新贴花
        if (terrainEffect != TerrainEffect.None)
        {
            CreateElementDecal(coord, terrainEffect);
        }
    }

    /// <summary>
    /// 创建元素贴花
    /// </summary>
    private void CreateElementDecal(GridCoord coord, TerrainEffect terrainEffect)
    {
        GameObject prefab = GetElementPrefab(terrainEffect);
        if (prefab == null) return;

        Vector3 worldPos = _gridManager.Topology.GridToWorld(coord);
        worldPos.y = 0.02f;

        GameObject decal = Instantiate(prefab, worldPos, Quaternion.identity);
        decal.name = $"Element_{terrainEffect}_{coord.U}_{coord.V}";
        decal.transform.SetParent(transform);
        decal.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.up);

        _elementDecals[coord] = decal;
    }

    private GameObject GetElementPrefab(TerrainEffect terrainEffect)
    {
        switch (terrainEffect)
        {
            case TerrainEffect.Burnt:
                return _fireDecalPrefab;
            case TerrainEffect.Ice:
                return _iceDecalPrefab;
            case TerrainEffect.Poison:
            case TerrainEffect.Swamp:
                return _poisonDecalPrefab;
            case TerrainEffect.Rock:
            case TerrainEffect.Grass:
            default:
                return null;
        }
    }

    /// <summary>
    /// 更新建造指示器
    /// </summary>
    public void UpdateBuildIndicator(Vector3 worldPos, bool isValid)
    {
        if (_buildIndicator == null && _buildIndicatorPrefab != null)
        {
            _buildIndicator = Instantiate(_buildIndicatorPrefab);
            _buildIndicator.name = "BuildIndicator";
            _buildIndicator.transform.SetParent(transform);
        }

        if (_buildIndicator == null) return;

        GridCoord coord = _gridManager.Topology.WorldToGrid(worldPos);
        Vector3 gridWorldPos = _gridManager.Topology.GridToWorld(coord);

        _buildIndicator.transform.position = gridWorldPos + Vector3.up * 0.1f;

        MeshRenderer renderer = _buildIndicator.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = isValid ? Color.green : Color.red;
        }

        _buildIndicator.SetActive(true);
    }

    /// <summary>
    /// 隐藏建造指示器
    /// </summary>
    public void HideBuildIndicator()
    {
        if (_buildIndicator != null)
        {
            _buildIndicator.SetActive(false);
        }
    }

    private int GetIndex(GridCoord coord, int width)
    {
        return coord.V * width + coord.U;
    }

    private void OnDestroy()
    {
        if (_gridManager != null)
        {
            _gridManager.OnCellChanged -= OnCellChanged;
        }
    }
}