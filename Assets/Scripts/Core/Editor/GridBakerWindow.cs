#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 离线网格烘焙工具 - 从场景提取地形数据
/// </summary>
public class GridBakerWindow : EditorWindow
{
    private const string MenuPath = "Window/Grid/Grid Baker";

    [MenuItem(MenuPath)]
    public static void ShowWindow()
    {
        GetWindow<GridBakerWindow>("Grid Baker");
    }

    private Vector3 _origin = Vector3.zero;
    private Vector2Int _gridSize = new Vector2Int(50, 50);
    private float _cellSize = 1f;
    private LayerMask _terrainLayer;
    private LayerMask _blockedLayer;
    private LayerMask _buildableLayer;
    private LayerMask _highGroundLayer;
    private string _outputPath = "Assets/Resources/GridData/";

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        _origin = EditorGUILayout.Vector3Field("Origin", _origin);
        _gridSize = EditorGUILayout.Vector2IntField("Grid Size", _gridSize);
        _cellSize = EditorGUILayout.FloatField("Cell Size", _cellSize);

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Layer Settings", EditorStyles.boldLabel);
        _terrainLayer = EditorGUILayout.LayerField("Terrain Layer", _terrainLayer);
        _blockedLayer = EditorGUILayout.LayerField("Blocked Layer", _blockedLayer);
        _buildableLayer = EditorGUILayout.LayerField("Buildable Layer", _buildableLayer);
        _highGroundLayer = EditorGUILayout.LayerField("High Ground Layer", _highGroundLayer);

        EditorGUILayout.Space(10);

        _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Bake Grid Data"))
        {
            BakeGridData();
        }

        if (GUILayout.Button("Clear Grid Data"))
        {
            ClearGridData();
        }
    }

    private void BakeGridData()
    {
        if (!Directory.Exists(_outputPath))
        {
            Directory.CreateDirectory(_outputPath);
        }

        GridCellData[] gridData = new GridCellData[_gridSize.x * _gridSize.y];

        for (int v = 0; v < _gridSize.y; v++)
        {
            for (int u = 0; u < _gridSize.x; u++)
            {
                GridCoord coord = new GridCoord(u, v);
                Vector3 worldPos = GridToWorld(coord);
                worldPos.y = 100f;

                Ray ray = new Ray(worldPos, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                {
                    int index = GetIndex(coord);
                    gridData[index] = CreateCellData(hit.collider.gameObject.layer, hit.point.y, hit.normal);
                }
                else
                {
                    int index = GetIndex(coord);
                    gridData[index] = CreateCellData(0, 0f, Vector3.up);
                }
            }
        }

        GridDataAsset asset = ScriptableObject.CreateInstance<GridDataAsset>();
        asset.GridData = gridData;
        asset.GridSize = _gridSize;
        asset.CellSize = _cellSize;
        asset.Origin = _origin;

        string assetPath = $"{_outputPath}GridData.asset";
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Bake Complete", $"Grid data baked successfully!\n\nOutput: {assetPath}\nTotal cells: {_gridSize.x * _gridSize.y}", "OK");
    }

    private GridCellData CreateCellData(int layer, float height, Vector3 normal)
    {
        GridCellData cell = new GridCellData();
        cell.Initialize();

        if (layer == _blockedLayer || layer == 0)
        {
            cell.Type = GridCellType.Blocked;
        }
        else if (layer == _highGroundLayer)
        {
            cell.Type = GridCellType.HighGround;
        }
        else if (layer == _buildableLayer)
        {
            cell.Type = GridCellType.Buildable;
        }
        else
        {
            cell.Type = GridCellType.Walkable;
        }

        cell.CustomData = LayerMask.NameToLayer(LayerMask.LayerToName(layer));

        return cell;
    }

    private void ClearGridData()
    {
        string assetPath = $"{_outputPath}GridData.asset";
        if (File.Exists(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Clear Complete", "Grid data cleared!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "No grid data found to clear!", "OK");
        }
    }

    private Vector3 GridToWorld(GridCoord coord)
    {
        return new Vector3(
            coord.U * _cellSize + _origin.x + _cellSize * 0.5f,
            0f,
            coord.V * _cellSize + _origin.z + _cellSize * 0.5f
        );
    }

    private int GetIndex(GridCoord coord)
    {
        return coord.V * _gridSize.x + coord.U;
    }
}

/// <summary>
/// 网格数据资产 - 存储烘焙后的网格数据
/// </summary>
[CreateAssetMenu(fileName = "GridData", menuName = "Grid/Grid Data Asset")]
public class GridDataAsset : ScriptableObject
{
    [Tooltip("网格尺寸")]
    public Vector2Int GridSize;

    [Tooltip("单元格尺寸")]
    public float CellSize;

    [Tooltip("网格原点")]
    public Vector3 Origin;

    [Tooltip("网格数据")]
    public GridCellData[] GridData;
}
#endif