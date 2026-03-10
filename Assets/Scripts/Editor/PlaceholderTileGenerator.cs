using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Generates simple solid-colour placeholder tile sprites and Tile assets
/// so you can paint tilemaps before real art is ready.
///
/// Run via: ZoneForge → Generate Placeholder Tiles
/// Output:  Assets/Art/Tiles/Placeholder/
/// </summary>
public static class PlaceholderTileGenerator
{
    private const string OutputPath = "Assets/Art/Tiles/Placeholder";
    private const int TileSize = 32;

    [MenuItem("ZoneForge/Generate Placeholder Tiles")]
    public static void GeneratePlaceholderTiles()
    {
        EnsureFolders();

        CreateTile("Grass",  new Color32(106, 168, 79,  255));
        CreateTile("Dirt",   new Color32(180, 122, 66,  255));
        CreateTile("Stone",  new Color32(153, 153, 153, 255));
        CreateTile("Water",  new Color32( 70, 130, 180, 255));
        CreateTile("Wall",   new Color32( 80,  80,  80, 255));

        AssetDatabase.Refresh();

        Debug.Log($"[ZoneForge] Placeholder tiles generated at {OutputPath}. " +
                  "Open Window → 2D → Tile Palette to create a palette from them.");

        // Open the output folder in the Project window
        Object folder = AssetDatabase.LoadAssetAtPath<Object>(OutputPath);
        if (folder != null)
            Selection.activeObject = folder;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Tiles"))
            AssetDatabase.CreateFolder("Assets/Art", "Tiles");
        if (!AssetDatabase.IsValidFolder(OutputPath))
            AssetDatabase.CreateFolder("Assets/Art/Tiles", "Placeholder");
    }

    private static void CreateTile(string tileName, Color32 colour)
    {
        // ── 1. Generate PNG ──────────────────────────────────────────
        string pngPath = $"{OutputPath}/{tileName}.png";

        // Only regenerate if the file doesn't exist yet
        if (!File.Exists(Path.Combine(Application.dataPath, pngPath.Substring("Assets/".Length))))
        {
            var tex = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[TileSize * TileSize];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = colour;

            // Add a 1-pixel dark border so tiles are visually distinct
            Color32 border = new Color32(
                (byte)(colour.r / 2),
                (byte)(colour.g / 2),
                (byte)(colour.b / 2),
                255);

            for (int x = 0; x < TileSize; x++)
            {
                pixels[x] = border;                           // bottom row
                pixels[(TileSize - 1) * TileSize + x] = border; // top row
                pixels[x * TileSize] = border;                // left col
                pixels[x * TileSize + (TileSize - 1)] = border; // right col
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, pngPath.Substring("Assets/".Length)),
                               tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // Force import as a sprite before creating the Tile asset
        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
        SetSpriteImportSettings(pngPath);

        // ── 2. Create Tile asset ─────────────────────────────────────
        string tilePath = $"{OutputPath}/{tileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<TileBase>(tilePath) != null)
            return; // already exists

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        if (sprite == null)
        {
            Debug.LogWarning($"[ZoneForge] Could not load sprite at {pngPath} — skipping tile asset.");
            return;
        }

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.colliderType = Tile.ColliderType.None;

        AssetDatabase.CreateAsset(tile, tilePath);
    }

    private static void SetSpriteImportSettings(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spritePixelsPerUnit = TileSize;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }
}
