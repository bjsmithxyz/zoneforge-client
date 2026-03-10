using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// ZoneForge Map Editor — creates zone scenes with the correct Grid + Tilemap layer setup.
/// Open via ZoneForge → Map Editor.
/// </summary>
public class MapEditorWindow : EditorWindow
{
    private string _zoneName = "NewZone";
    private int _gridWidth = 20;
    private int _gridHeight = 20;

    [MenuItem("ZoneForge/Map Editor")]
    public static void OpenWindow()
    {
        MapEditorWindow window = GetWindow<MapEditorWindow>();
        window.titleContent = new GUIContent("Map Editor");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("ZoneForge Map Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // ── Create Zone ──────────────────────────────────────────────
        GUILayout.Label("Create Zone", EditorStyles.boldLabel);

        _zoneName = EditorGUILayout.TextField("Zone Name", _zoneName);
        _gridWidth = EditorGUILayout.IntField("Grid Width", _gridWidth);
        _gridHeight = EditorGUILayout.IntField("Grid Height", _gridHeight);

        EditorGUILayout.Space();

        bool nameValid = !string.IsNullOrWhiteSpace(_zoneName);
        bool dimsValid = _gridWidth > 0 && _gridHeight > 0;

        EditorGUI.BeginDisabledGroup(!nameValid || !dimsValid);
        if (GUILayout.Button("Create Zone Scene"))
            CreateZoneScene(_zoneName.Trim(), (uint)_gridWidth, (uint)_gridHeight);
        EditorGUI.EndDisabledGroup();

        if (!nameValid)
            EditorGUILayout.HelpBox("Zone name cannot be empty.", MessageType.Warning);
        if (!dimsValid)
            EditorGUILayout.HelpBox("Width and Height must be greater than zero.", MessageType.Warning);

        EditorGUILayout.Space();

        // ── Tile Palette reminder ─────────────────────────────────────
        GUILayout.Label("Tile Palette Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Generate placeholder tiles: ZoneForge → Generate Placeholder Tiles\n" +
            "2. Open the Tile Palette window: Window → 2D → Tile Palette\n" +
            "3. Create a new palette and drag tiles from Assets/Art/Tiles/ into it\n" +
            "4. Select a tilemap layer in the Hierarchy, then paint in Scene view",
            MessageType.Info);
    }

    private static void CreateZoneScene(string zoneName, uint width, uint height)
    {
        // Prompt to save the current scene first
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Create and open a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Grid root ────────────────────────────────────────────────
        var gridGo = new GameObject($"Zone_{zoneName}");
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = Vector3.one;

        // Attach ZoneController and pre-fill metadata
        var controller = gridGo.AddComponent<ZoneController>();
        controller.ZoneName = zoneName;
        controller.GridWidth = width;
        controller.GridHeight = height;

        // ── Tilemap layers ───────────────────────────────────────────
        controller.GroundLayer = CreateTilemapLayer(gridGo, "Ground", 0);
        controller.DecorationLayer = CreateTilemapLayer(gridGo, "Decoration", 1);
        controller.CollisionLayer = CreateTilemapLayer(gridGo, "Collision", 2);

        // Mark the collision layer with a tag so physics / the editor can identify it
        controller.CollisionLayer.gameObject.AddComponent<TilemapCollider2D>();

        // Select the grid in the Hierarchy for convenience
        Selection.activeGameObject = gridGo;

        // Save the scene to Assets/Scenes/Zones/<ZoneName>.unity
        string scenesPath = "Assets/Scenes/Zones";
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(scenesPath))
            AssetDatabase.CreateFolder("Assets/Scenes", "Zones");

        string scenePath = $"{scenesPath}/{zoneName}.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();

        Debug.Log($"[ZoneForge] Zone '{zoneName}' scene created at {scenePath}");
    }

    private static Tilemap CreateTilemapLayer(GameObject parent, string layerName, int sortOrder)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(parent.transform, false);

        var tilemap = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortOrder;

        return tilemap;
    }
}
