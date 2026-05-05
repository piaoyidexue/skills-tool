#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 地图编辑器主窗口
/// </summary>
public class MapEditorWindow : EditorWindow
{
    private const string WindowTitle = "Map Editor";
    private const string MenuPath = "Window/Map Editor";
    private const string LevelDataDirectory = "Assets/Resources/LevelData";

    // 当前编辑的关卡数据
    private LevelData _currentLevel;

    // 编辑器状态
    private bool _showGridSettings = true;
    private bool _showBrushSettings = true;
    private bool _showPathSettings = true;
    private bool _showSpawnerSettings = true;
    private bool _showTriggerSettings = true;

    // 笔刷设置
    private BrushMode _brushMode = BrushMode.CellType;
    private GridCellType _selectedCellType = GridCellType.Buildable;
    private TerrainEffect _selectedTerrain = TerrainEffect.None;
    private int _brushSize = 1;

    // Shader设置
    private Material _sharedCellMaterial;
    private Shader _defaultShader;

    // 当前选中的对象
    private string _selectedPathId = "";
    private string _selectedSpawnerId = "";
    private string _selectedTriggerId = "";

    // 场景编辑状态
    private bool _isEditing = false;
    private GameObject _editorGridVisual;
    private Dictionary<GridCoord, GameObject> _cellVisuals;

    // 路径绘制状态
    private bool _isDrawingPath = false;
    private string _editingPathId = "";
    private List<Vector2Int> _editingPathNodes = new List<Vector2Int>();

    [MenuItem(MenuPath)]
    public static void ShowWindow()
    {
        var window = GetWindow<MapEditorWindow>(WindowTitle);
        window.Show();
    }

    private void OnEnable()
    {
        _cellVisuals = new Dictionary<GridCoord, GameObject>();
        _currentLevel = new LevelData();
        _currentLevel.LevelId = "LV_Editor_New";

        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearEditorVisuals();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.Space(10);

        DrawLevelInfoPanel();

        EditorGUILayout.Space(10);

        DrawGridSettingsPanel();

        EditorGUILayout.Space(10);

        DrawBrushSettingsPanel();

        EditorGUILayout.Space(10);

        DrawPathSettingsPanel();

        EditorGUILayout.Space(10);

        DrawSpawnerSettingsPanel();

        EditorGUILayout.Space(10);

        DrawTriggerSettingsPanel();

        EditorGUILayout.Space(20);

        DrawActionsPanel();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New", EditorStyles.toolbarButton))
        {
            CreateNewLevel();
        }

        if (GUILayout.Button("Open...", EditorStyles.toolbarButton))
        {
            OpenLevelDialog();
        }

        if (GUILayout.Button("Save", EditorStyles.toolbarButton))
        {
            SaveLevel();
        }

        if (GUILayout.Button("Export", EditorStyles.toolbarButton))
        {
            ExportLevel();
        }

        GUILayout.FlexibleSpace();

        if (_isEditing)
        {
            if (GUILayout.Button("Stop Editing", EditorStyles.toolbarButton))
            {
                StopEditing();
            }
        }
        else
        {
            if (GUILayout.Button("Start Editing", EditorStyles.toolbarButton))
            {
                StartEditing();
            }
        }

        GUILayout.EndHorizontal();
    }

    private void DrawLevelInfoPanel()
    {
        EditorGUILayout.LabelField("Level Info", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        _currentLevel.LevelId = EditorGUILayout.TextField("Level ID", _currentLevel.LevelId);
        _currentLevel.Version = EditorGUILayout.TextField("Version", _currentLevel.Version);
        _currentLevel.ArtScenePath = EditorGUILayout.TextField("Art Scene Path", _currentLevel.ArtScenePath);

        EditorGUI.indentLevel--;
    }

    private void DrawGridSettingsPanel()
    {
        _showGridSettings = EditorGUILayout.Foldout(_showGridSettings, "Grid Settings");

        if (_showGridSettings)
        {
            EditorGUI.indentLevel++;

            _currentLevel.TopologyType = (TopologyType)EditorGUILayout.EnumPopup("Topology", _currentLevel.TopologyType);
            _currentLevel.GridSize = EditorGUILayout.Vector2IntField("Grid Size", _currentLevel.GridSize);
            _currentLevel.CellSize = EditorGUILayout.FloatField("Cell Size", _currentLevel.CellSize);
            _currentLevel.Origin = EditorGUILayout.Vector3Field("Origin", _currentLevel.Origin);

            if (GUILayout.Button("Rebuild Grid"))
            {
                RebuildGrid();
            }

            EditorGUI.indentLevel--;
        }
    }

    private void DrawBrushSettingsPanel()
    {
        _showBrushSettings = EditorGUILayout.Foldout(_showBrushSettings, "Brush Settings");

        if (_showBrushSettings)
        {
            EditorGUI.indentLevel++;

            _brushMode = (BrushMode)EditorGUILayout.EnumPopup("Brush Mode", _brushMode);
            _brushSize = EditorGUILayout.IntSlider("Brush Size", _brushSize, 1, 5);

            if (_brushMode == BrushMode.CellType)
            {
                _selectedCellType = (GridCellType)EditorGUILayout.EnumPopup("Cell Type", _selectedCellType);
            }
            else if (_brushMode == BrushMode.Terrain)
            {
                _selectedTerrain = (TerrainEffect)EditorGUILayout.EnumPopup("Terrain", _selectedTerrain);
            }

            if (GUILayout.Button("Bake From Scene Collision"))
            {
                BakeFromCollision();
            }

            EditorGUI.indentLevel--;
        }
    }

    private void DrawPathSettingsPanel()
    {
        _showPathSettings = EditorGUILayout.Foldout(_showPathSettings, "Path Settings");

        if (_showPathSettings)
        {
            EditorGUI.indentLevel++;

            if (GUILayout.Button("Add New Path"))
            {
                AddNewPath();
            }

            EditorGUILayout.LabelField("Existing Paths:");
            EditorGUI.indentLevel++;

            for (int i = 0; i < _currentLevel.Paths.Count; i++)
            {
                var path = _currentLevel.Paths[i];

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(path.PathId, EditorStyles.miniButton))
                {
                    _selectedPathId = path.PathId;
                }

                if (GUILayout.Button("Edit", EditorStyles.miniButton))
                {
                    StartEditingPath(path.PathId);
                }

                if (GUILayout.Button("Delete", EditorStyles.miniButton))
                {
                    DeletePath(path.PathId);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;

            if (_isDrawingPath)
            {
                if (GUILayout.Button("Finish Path"))
                {
                    FinishEditingPath();
                }

                if (GUILayout.Button("Cancel Path"))
                {
                    CancelEditingPath();
                }
            }

            EditorGUI.indentLevel--;
        }
    }

    private void DrawSpawnerSettingsPanel()
    {
        _showSpawnerSettings = EditorGUILayout.Foldout(_showSpawnerSettings, "Spawner Settings");

        if (_showSpawnerSettings)
        {
            EditorGUI.indentLevel++;

            if (GUILayout.Button("Add New Spawner"))
            {
                AddNewSpawner();
            }

            EditorGUILayout.LabelField("Existing Spawners:");
            EditorGUI.indentLevel++;

            for (int i = 0; i < _currentLevel.Spawners.Count; i++)
            {
                var spawner = _currentLevel.Spawners[i];

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(spawner.SpawnerId, EditorStyles.miniButton))
                {
                    _selectedSpawnerId = spawner.SpawnerId;
                }

                if (GUILayout.Button("Edit", EditorStyles.miniButton))
                {
                    EditSpawner(spawner.SpawnerId);
                }

                if (GUILayout.Button("Delete", EditorStyles.miniButton))
                {
                    DeleteSpawner(spawner.SpawnerId);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;
        }
    }

    private void DrawTriggerSettingsPanel()
    {
        _showTriggerSettings = EditorGUILayout.Foldout(_showTriggerSettings, "Trigger Settings");

        if (_showTriggerSettings)
        {
            EditorGUI.indentLevel++;

            if (GUILayout.Button("Add New Trigger"))
            {
                AddNewTrigger();
            }

            EditorGUILayout.LabelField("Existing Triggers:");
            EditorGUI.indentLevel++;

            for (int i = 0; i < _currentLevel.Triggers.Count; i++)
            {
                var trigger = _currentLevel.Triggers[i];

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(trigger.TriggerId, EditorStyles.miniButton))
                {
                    _selectedTriggerId = trigger.TriggerId;
                }

                if (GUILayout.Button("Edit", EditorStyles.miniButton))
                {
                    EditTrigger(trigger.TriggerId);
                }

                if (GUILayout.Button("Delete", EditorStyles.miniButton))
                {
                    DeleteTrigger(trigger.TriggerId);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;
        }
    }

    private void DrawActionsPanel()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Run Sandbox"))
        {
            RunSandbox();
        }

        if (GUILayout.Button("Validate Level"))
        {
            ValidateLevel();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_isEditing || _currentLevel == null)
            return;

        HandleGridPainting(sceneView);
        HandlePathDrawing(sceneView);
        DrawGridVisual();
    }

    private void HandleGridPainting(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
        {
            if (e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 1000f))
                {
                    GridCoord coord = WorldToGridCoord(hit.point);

                    if (IsValidCoord(coord))
                    {
                        ApplyBrushAt(coord);
                        e.Use();
                    }
                }
            }
        }
    }

    private void HandlePathDrawing(SceneView sceneView)
    {
        if (!_isDrawingPath)
            return;

        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000f))
            {
                GridCoord coord = WorldToGridCoord(hit.point);

                if (IsValidCoord(coord))
                {
                    _editingPathNodes.Add(new Vector2Int(coord.U, coord.V));
                    e.Use();
                }
            }
        }

        // 绘制当前路径
        if (_editingPathNodes.Count > 1)
        {
            Handles.color = Color.yellow;
            for (int i = 0; i < _editingPathNodes.Count - 1; i++)
            {
                Vector3 p1 = GridCoordToWorld(new GridCoord(_editingPathNodes[i].x, _editingPathNodes[i].y));
                Vector3 p2 = GridCoordToWorld(new GridCoord(_editingPathNodes[i + 1].x, _editingPathNodes[i + 1].y));
                Handles.DrawLine(p1, p2);
            }
        }
    }

    private void DrawGridVisual()
    {
        if (_editorGridVisual == null)
            return;

        // 绘制网格线
        Handles.color = Color.gray * 0.3f;
        for (int x = 0; x <= _currentLevel.GridSize.x; x++)
        {
            Vector3 start = GridCoordToWorld(new GridCoord(x, 0)) + Vector3.up * 0.01f;
            Vector3 end = GridCoordToWorld(new GridCoord(x, _currentLevel.GridSize.y)) + Vector3.up * 0.01f;
            Handles.DrawLine(start, end);
        }
        for (int y = 0; y <= _currentLevel.GridSize.y; y++)
        {
            Vector3 start = GridCoordToWorld(new GridCoord(0, y)) + Vector3.up * 0.01f;
            Vector3 end = GridCoordToWorld(new GridCoord(_currentLevel.GridSize.x, y)) + Vector3.up * 0.01f;
            Handles.DrawLine(start, end);
        }

        // 绘制单元格高亮
        foreach (var cell in _currentLevel.GridCells)
        {
            Vector3 pos = GridCoordToWorld(new GridCoord(cell.X, cell.Y)) + Vector3.up * 0.02f;

            Color cellColor = GetCellColor(cell.Type, cell.Terrain);
            Handles.color = cellColor;
            Handles.DrawSolidDisc(pos, Vector3.up, _currentLevel.CellSize * 0.4f);
        }
    }

    private void CreateNewLevel()
    {
        _currentLevel = new LevelData();
        _currentLevel.LevelId = "LV_Editor_New";
        RebuildGrid();
    }

    private void OpenLevelDialog()
    {
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { LevelDataDirectory });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("No Levels", "No saved levels found.", "OK");
            return;
        }

        GenericMenu menu = new GenericMenu();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string filename = Path.GetFileNameWithoutExtension(path);
            menu.AddItem(new GUIContent(filename), false, () => LoadLevelFromAsset(path));
        }

        menu.ShowAsContext();
    }

    private void LoadLevelFromAsset(string path)
    {
        TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);

        if (jsonAsset != null)
        {
            _currentLevel = JsonUtility.FromJson<LevelData>(jsonAsset.text);
            Debug.Log($"[MapEditor] Loaded level: {_currentLevel.LevelId}");
            RebuildGrid();
        }
    }

    private void SaveLevel()
    {
        if (string.IsNullOrEmpty(_currentLevel.LevelId))
        {
            EditorUtility.DisplayDialog("Error", "Please set a valid Level ID", "OK");
            return;
        }

        if (!Directory.Exists(LevelDataDirectory))
        {
            Directory.CreateDirectory(LevelDataDirectory);
        }

        string filePath = $"{LevelDataDirectory}/{_currentLevel.LevelId}.json";
        string json = JsonUtility.ToJson(_currentLevel, prettyPrint: true);
        File.WriteAllText(filePath, json);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Saved", $"Level saved to {filePath}", "OK");
    }

    private void ExportLevel()
    {
        if (string.IsNullOrEmpty(_currentLevel.LevelId))
        {
            EditorUtility.DisplayDialog("Error", "Please set a valid Level ID", "OK");
            return;
        }

        string savePath = EditorUtility.SaveFilePanel("Export Level", "", $"{_currentLevel.LevelId}.bytes", "bytes");

        if (!string.IsNullOrEmpty(savePath))
        {
            // TODO: 实际项目中这里可能会做二进制压缩
            string json = JsonUtility.ToJson(_currentLevel, prettyPrint: false);
            File.WriteAllText(savePath, json);

            EditorUtility.DisplayDialog("Exported", $"Level exported to {savePath}", "OK");
        }
    }

    private void StartEditing()
    {
        _isEditing = true;
        CreateEditorVisuals();
        RebuildGrid();
        SceneView.RepaintAll();
    }

    private void StopEditing()
    {
        _isEditing = false;
        ClearEditorVisuals();
    }

    private void RebuildGrid()
    {
        _currentLevel.GridCells.Clear();

        for (int y = 0; y < _currentLevel.GridSize.y; y++)
        {
            for (int x = 0; x < _currentLevel.GridSize.x; x++)
            {
                LevelGridCellData cell = new LevelGridCellData();
                cell.X = x;
                cell.Y = y;
                cell.Type = GridCellType.Walkable;
                cell.Terrain = TerrainEffect.None;
                _currentLevel.GridCells.Add(cell);
            }
        }

        if (_isEditing)
        {
            UpdateAllCellVisuals();
        }
    }

    private void CreateEditorVisuals()
    {
        ClearEditorVisuals();

        _editorGridVisual = new GameObject("EditorGridVisual");
        _editorGridVisual.hideFlags = HideFlags.DontSave;

        UpdateAllCellVisuals();
    }

    private void UpdateAllCellVisuals()
    {
        if (_cellVisuals == null)
            _cellVisuals = new Dictionary<GridCoord, GameObject>();

        // 清除旧的
        foreach (var kvp in _cellVisuals)
        {
            DestroyImmediate(kvp.Value);
        }
        _cellVisuals.Clear();

        // 创建新的
        foreach (var cell in _currentLevel.GridCells)
        {
            CreateCellVisual(new GridCoord(cell.X, cell.Y), cell);
        }
    }

    private void CreateCellVisual(GridCoord coord, LevelGridCellData cellData)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
        visual.name = $"Cell_{coord.U}_{coord.V}";
        visual.transform.SetParent(_editorGridVisual.transform);
        visual.transform.position = GridCoordToWorld(coord);
        visual.transform.localScale = Vector3.one * _currentLevel.CellSize * 0.1f;
        visual.hideFlags = HideFlags.DontSave;

        Renderer renderer = visual.GetComponent<Renderer>();

        // 1. 初始化共享材质（整个编辑器生命周期只创建一次，防止泄漏）
        if (_sharedCellMaterial == null)
        {
            _sharedCellMaterial = GetCompatibleMaterial();
            _sharedCellMaterial.hideFlags = HideFlags.DontSave;
        }

        // 2. 赋值 sharedMaterial（严禁在 Edit Mode 使用 .material）
        renderer.sharedMaterial = _sharedCellMaterial;

        // 3. 使用 MaterialPropertyBlock 单独修改这个格子的颜色
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(mpb); // 获取当前的属性块

        Color cellColor = GetCellColor(cellData.Type, cellData.Terrain);
        if (_sharedCellMaterial.HasProperty("_BaseColor"))
        {
            mpb.SetColor("_BaseColor", cellColor); // URP 材质
        }
        else if (_sharedCellMaterial.HasProperty("_Color"))
        {
            mpb.SetColor("_Color", cellColor);      // Built-in 材质
        }

        renderer.SetPropertyBlock(mpb); // 应用修改，不会产生新的材质实例

        DestroyImmediate(visual.GetComponent<Collider>());

        _cellVisuals[coord] = visual;
    }

    /// <summary>
    /// 获取兼容当前渲染管线的默认材质 (支持 URP 和 Built-in)
    /// </summary>
    private Material GetCompatibleMaterial()
    {
        Material mat = null;

        // 方案 1：尝试直接从当前激活的渲染管线资产中获取默认材质 (最稳妥的 URP/HDRP 做法)
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
        {
            mat = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial);
            return mat;
        }

        // 方案 2：如果方案 1 失败，尝试硬编码查找 URP 无光照 Shader (编辑器网格用无光照更好看)
        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpUnlit != null) return new Material(urpUnlit);

        // 方案 3：查找 URP 标准光照 Shader
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit != null) return new Material(urpLit);

        // 方案 4：回退到 Unity 传统的内置管线 (Built-in)
        Shader standardShader = Shader.Find("Standard");
        if (standardShader != null) return new Material(standardShader);

        // 方案 5：终极兜底 (Unity 底层一定存在的 Shader)
        return new Material(Shader.Find("Hidden/Internal-Colored"));
    }
    private void ClearEditorVisuals()
    {
        if (_editorGridVisual != null)
        {
            DestroyImmediate(_editorGridVisual);
            _editorGridVisual = null;
        }

        if (_cellVisuals != null)
        {
            _cellVisuals.Clear();
        }
        // 清理共享材质，防止关闭编辑器窗口后发生内存泄漏
        if (_sharedCellMaterial != null)
        {
            DestroyImmediate(_sharedCellMaterial);
            _sharedCellMaterial = null;
        }
    }

    private void ApplyBrushAt(GridCoord centerCoord)
    {
        for (int dy = -_brushSize + 1; dy < _brushSize; dy++)
        {
            for (int dx = -_brushSize + 1; dx < _brushSize; dx++)
            {
                GridCoord coord = new GridCoord(centerCoord.U + dx, centerCoord.V + dy);

                if (IsValidCoord(coord))
                {
                    int index = GetCellIndex(coord);

                    if (index >= 0 && index < _currentLevel.GridCells.Count)
                    {
                        LevelGridCellData cell = _currentLevel.GridCells[index];

                        if (_brushMode == BrushMode.CellType)
                        {
                            cell.Type = _selectedCellType;
                        }
                        else if (_brushMode == BrushMode.Terrain)
                        {
                            cell.Terrain = _selectedTerrain;
                        }

                        _currentLevel.GridCells[index] = cell;

                        // 更新可视化
                        if (_cellVisuals.TryGetValue(coord, out var visual))
                        {
                            Renderer renderer = visual.GetComponent<Renderer>();
                            renderer.material.color = GetCellColor(cell.Type, cell.Terrain);
                        }
                    }
                }
            }
        }
    }

    private void AddNewPath()
    {
        string pathId = $"Path_{_currentLevel.Paths.Count + 1}";

        LevelPathData path = new LevelPathData();
        path.PathId = pathId;
        path.Nodes = new List<Vector2Int>();

        _currentLevel.Paths.Add(path);
        _selectedPathId = pathId;
    }

    private void StartEditingPath(string pathId)
    {
        int index = _currentLevel.Paths.FindIndex(p => p.PathId == pathId);

        if (index >= 0)
        {
            _isDrawingPath = true;
            _editingPathId = pathId;
            _editingPathNodes = new List<Vector2Int>(_currentLevel.Paths[index].Nodes);
        }
    }

    private void FinishEditingPath()
    {
        int index = _currentLevel.Paths.FindIndex(p => p.PathId == _editingPathId);

        if (index >= 0)
        {
            LevelPathData path = _currentLevel.Paths[index];
            path.Nodes = new List<Vector2Int>(_editingPathNodes);
            _currentLevel.Paths[index] = path;
        }

        _isDrawingPath = false;
        _editingPathId = "";
        _editingPathNodes.Clear();
    }

    private void CancelEditingPath()
    {
        _isDrawingPath = false;
        _editingPathId = "";
        _editingPathNodes.Clear();
    }

    private void DeletePath(string pathId)
    {
        int index = _currentLevel.Paths.FindIndex(p => p.PathId == pathId);

        if (index >= 0)
        {
            _currentLevel.Paths.RemoveAt(index);

            if (_selectedPathId == pathId)
            {
                _selectedPathId = "";
            }
        }
    }

    private void AddNewSpawner()
    {
        string spawnerId = $"Spawner_{_currentLevel.Spawners.Count + 1}";

        LevelSpawnerData spawner = new LevelSpawnerData();
        spawner.SpawnerId = spawnerId;
        spawner.Position = new Vector2Int(_currentLevel.GridSize.x / 2, _currentLevel.GridSize.y / 2);
        spawner.LinkedPathId = "";
        spawner.WaveConfigId = 0;
        spawner.SquadId = 0;

        _currentLevel.Spawners.Add(spawner);
        _selectedSpawnerId = spawnerId;
    }

    private void EditSpawner(string spawnerId)
    {
        _selectedSpawnerId = spawnerId;
    }

    private void DeleteSpawner(string spawnerId)
    {
        int index = _currentLevel.Spawners.FindIndex(s => s.SpawnerId == spawnerId);

        if (index >= 0)
        {
            _currentLevel.Spawners.RemoveAt(index);

            if (_selectedSpawnerId == spawnerId)
            {
                _selectedSpawnerId = "";
            }
        }
    }

    private void AddNewTrigger()
    {
        string triggerId = $"Trigger_{_currentLevel.Triggers.Count + 1}";

        LevelTriggerData trigger = new LevelTriggerData();
        trigger.TriggerId = triggerId;
        trigger.Type = TriggerType.Trap;
        trigger.Position = new Vector2Int(_currentLevel.GridSize.x / 2, _currentLevel.GridSize.y / 2);
        trigger.Radius = 2f;
        trigger.EffectId = 0;
        trigger.LinkedSquadId = "";

        _currentLevel.Triggers.Add(trigger);
        _selectedTriggerId = triggerId;
    }

    private void EditTrigger(string triggerId)
    {
        _selectedTriggerId = triggerId;
    }

    private void DeleteTrigger(string triggerId)
    {
        int index = _currentLevel.Triggers.FindIndex(t => t.TriggerId == triggerId);

        if (index >= 0)
        {
            _currentLevel.Triggers.RemoveAt(index);

            if (_selectedTriggerId == triggerId)
            {
                _selectedTriggerId = "";
            }
        }
    }

private void BakeFromCollision()
    {
        if (_currentLevel == null || _currentLevel.GridCells == null || _currentLevel.GridCells.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Grid is not initialized. Please rebuild grid first.", "OK");
            return;
        }

        // 弹窗二次确认，防止误触覆盖数据
        bool confirm = EditorUtility.DisplayDialog("Bake Grid from Collision", 
            "This will overwrite the current grid cell types based on scene physical colliders. Are you sure?", 
            "Bake", "Cancel");

        if (!confirm) return;

        int bakedHitCount = 0;
        float rayStartHeight = 100f; // 射线起始高度（假设地图不会超过 Y=100）
        float maxDistance = 200f;    // 射线最大检测距离

        try
        {
            for (int i = 0; i < _currentLevel.GridCells.Count; i++)
            {
                LevelGridCellData cell = _currentLevel.GridCells[i];
                GridCoord coord = new GridCoord(cell.X, cell.Y);
                Vector3 worldPos = GridCoordToWorld(coord);
                
                // 从该格子的高空正中心向下发射射线
                Vector3 rayOrigin = new Vector3(worldPos.x, _currentLevel.Origin.y + rayStartHeight, worldPos.z);

                // 显示烘焙进度条（防止网格过大导致 Unity 无响应假死）
                if (i % 50 == 0)
                {
                    EditorUtility.DisplayProgressBar("Baking Grid...", 
                        $"Scanning cell {i}/{_currentLevel.GridCells.Count}", 
                        (float)i / _currentLevel.GridCells.Count);
                }

                // 默认初始化：没扫到东西的格子一律视为不可通行（Blocked）
                cell.Type = GridCellType.Blocked;
                cell.Terrain = TerrainEffect.None;
                // 如果你的 LevelGridCellData 有 Height 字段，可以在此重置
                // cell.Height = 0f; 

                // 向下发射射线
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxDistance))
                {
                    bakedHitCount++;

                    // 1. 高度与法线提取 (预留给后续的高度图系统)
                    // cell.Height = hit.point.y;
                    // cell.Normal = hit.normal;

                    // 2. 根据 Layer 提取格子类型 (CellType)
                    int layer = hit.collider.gameObject.layer;
                    string layerName = LayerMask.LayerToName(layer);

                    switch (layerName)
                    {
                        case "Buildable":    // 可建造的高台/平地
                            cell.Type = GridCellType.Buildable;
                            break;
                        case "Walkable":     // 仅供怪物行走的道路
                            cell.Type = GridCellType.Walkable;
                            break;
                        case "HighGround":   // 狙击位/高地
                            cell.Type = GridCellType.HighGround;
                            break;
                        case "Obstacle":     // 障碍物/墙壁
                            cell.Type = GridCellType.Blocked;
                            break;
                        default:
                            // 降级策略：如果层级未明确划分，可以通过 Tag 判断
                            if (hit.collider.CompareTag("Buildable"))
                                cell.Type = GridCellType.Buildable;
                            else if (hit.collider.CompareTag("Obstacle"))
                                cell.Type = GridCellType.Blocked;
                            else 
                                cell.Type = GridCellType.Walkable; // 默认击中底板视为可通行
                            break;
                    }

                    // 3. 根据 Tag 提取特殊地形效果 (TerrainEffect)
                    if (hit.collider.CompareTag("Water") || hit.collider.CompareTag("Swamp"))
                    {
                        cell.Terrain = TerrainEffect.Swamp;
                    }
                    else if (hit.collider.CompareTag("Ice"))
                    {
                        cell.Terrain = TerrainEffect.Ice;
                    }
                    else if (hit.collider.CompareTag("Lava") || hit.collider.CompareTag("Burnt"))
                    {
                        cell.Terrain = TerrainEffect.Burnt;
                    }
                    else
                    {
                        cell.Terrain = TerrainEffect.None;
                    }
                }

                // 将修改后的数据写回列表
                _currentLevel.GridCells[i] = cell;
            }

            // 如果处于编辑预览模式，立即刷新视图以展现烘焙结果
            if (_isEditing)
            {
                UpdateAllCellVisuals();
            }

            Debug.Log($"[MapEditor] Grid successfully baked! {bakedHitCount}/{_currentLevel.GridCells.Count} cells hit physical colliders.");
        }
        finally
        {
            // 无论是否发生异常，确保清除进度条，防止编辑器卡在进度条界面
            EditorUtility.ClearProgressBar();
        }
    }

    private void RunSandbox()
    {
        // 保存当前关卡临时文件
        SaveLevel();

        // 查找 QAAITacticsSandbox 并初始化
        var sandbox = FindObjectOfType<QAAITacticsSandbox>();

        if (sandbox != null)
        {
            EditorUtility.DisplayDialog("Sandbox", "Sandbox initialized - please run play mode!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Sandbox", "QAAITacticsSandbox not found in scene!", "OK");
        }
    }

    private void ValidateLevel()
    {
        List<string> errors = new List<string>();

        // 验证所有路径是否可达
        foreach (var path in _currentLevel.Paths)
        {
            foreach (var node in path.Nodes)
            {
                int cellIndex = GetCellIndex(new GridCoord(node.x, node.y));

                if (cellIndex >= 0)
                {
                    var cell = _currentLevel.GridCells[cellIndex];

                    if (cell.Type == GridCellType.Blocked)
                    {
                        errors.Add($"Path {path.PathId} passes through blocked cell ({node.x}, {node.y})");
                    }
                }
            }
        }

        // 验证 Spawner 引用的路径是否存在
        foreach (var spawner in _currentLevel.Spawners)
        {
            if (!string.IsNullOrEmpty(spawner.LinkedPathId))
            {
                bool pathExists = _currentLevel.Paths.Exists(p => p.PathId == spawner.LinkedPathId);

                if (!pathExists)
                {
                    errors.Add($"Spawner {spawner.SpawnerId} references missing path: {spawner.LinkedPathId}");
                }
            }
        }

        if (errors.Count > 0)
        {
            string errorMsg = $"Found {errors.Count} validation errors:\n\n" + string.Join("\n", errors);
            EditorUtility.DisplayDialog("Validation Failed", errorMsg, "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Validation Passed", "Level validated successfully!", "OK");
        }
    }

    // 辅助方法
    private GridCoord WorldToGridCoord(Vector3 worldPos)
    {
        float x = (worldPos.x - _currentLevel.Origin.x) / _currentLevel.CellSize;
        float y = (worldPos.z - _currentLevel.Origin.z) / _currentLevel.CellSize;

        return new GridCoord(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
    }

    private Vector3 GridCoordToWorld(GridCoord coord)
    {
        return new Vector3(
            coord.U * _currentLevel.CellSize + _currentLevel.Origin.x,
            _currentLevel.Origin.y,
            coord.V * _currentLevel.CellSize + _currentLevel.Origin.z
        );
    }

    private bool IsValidCoord(GridCoord coord)
    {
        return coord.U >= 0 && coord.U < _currentLevel.GridSize.x &&
               coord.V >= 0 && coord.V < _currentLevel.GridSize.y;
    }

    private int GetCellIndex(GridCoord coord)
    {
        return coord.V * _currentLevel.GridSize.x + coord.U;
    }

    private Color GetCellColor(GridCellType type, TerrainEffect terrain)
    {
        Color cellColor = Color.white;

        switch (type)
        {
            case GridCellType.Blocked:
                cellColor = new Color(0.8f, 0.2f, 0.2f);
                break;
            case GridCellType.Walkable:
                cellColor = new Color(0.5f, 0.5f, 0.5f);
                break;
            case GridCellType.Buildable:
                cellColor = new Color(0.2f, 0.8f, 0.2f);
                break;
            case GridCellType.HighGround:
                cellColor = new Color(0.2f, 0.2f, 0.8f);
                break;
        }

        switch (terrain)
        {
            case TerrainEffect.Ice:
                cellColor = Color.Lerp(cellColor, new Color(0.8f, 0.95f, 1f), 0.5f);
                break;
            case TerrainEffect.Burnt:
                cellColor = Color.Lerp(cellColor, new Color(0.3f, 0.15f, 0f), 0.5f);
                break;
            case TerrainEffect.Swamp:
                cellColor = Color.Lerp(cellColor, new Color(0.2f, 0.4f, 0.1f), 0.5f);
                break;
        }

        return cellColor;
    }
}

/// <summary>
/// 笔刷模式
/// </summary>
public enum BrushMode
{
    CellType,
    Terrain
}
#endif
