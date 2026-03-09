using UnityEditor;
using UnityEngine;

public class MapEditorWindow : EditorWindow
{
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
        GUILayout.Label("Tilemap tools will appear here.", EditorStyles.helpBox);
    }
}