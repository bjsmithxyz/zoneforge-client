using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates simple placeholder sprites for characters and props.
/// Run via: ZoneForge → Generate Placeholder Sprites
/// Output:  Assets/Art/Sprites/Placeholder/
/// </summary>
public static class PlaceholderSpriteGenerator
{
    private const string OutputPath = "Assets/Art/Sprites/Placeholder";
    private const int SpriteSize = 32;

    [MenuItem("ZoneForge/Generate Placeholder Sprites")]
    public static void GeneratePlaceholderSprites()
    {
        EnsureFolders();

        // Characters
        CreateCircleSprite("Character_Player",  new Color32( 70, 130, 210, 255));
        CreateCircleSprite("Character_Enemy",   new Color32(210,  70,  70, 255));
        CreateCircleSprite("Character_NPC",     new Color32( 70, 190, 120, 255));

        // Props
        CreateDiamondSprite("Prop_Chest",       new Color32(210, 170,  30, 255));
        CreateDiamondSprite("Prop_Barrel",      new Color32(160, 100,  60, 255));
        CreateDiamondSprite("Prop_Crate",       new Color32(180, 150,  90, 255));

        // Obstacles
        CreateRectSprite("Obstacle_Tree",       new Color32( 34, 139,  34, 255), SpriteSize, SpriteSize);
        CreateRectSprite("Obstacle_Rock",       new Color32(120, 120, 120, 255), SpriteSize, SpriteSize);
        CreateRectSprite("Obstacle_Wall",       new Color32( 90,  80,  70, 255), SpriteSize, SpriteSize);

        AssetDatabase.Refresh();

        Debug.Log($"[ZoneForge] Placeholder sprites generated at {OutputPath}.");

        Object folder = AssetDatabase.LoadAssetAtPath<Object>(OutputPath);
        if (folder != null)
            Selection.activeObject = folder;
    }

    // ── Shape generators ──────────────────────────────────────────────────

    private static void CreateCircleSprite(string name, Color32 fill)
    {
        var tex = NewTexture();
        float cx = SpriteSize * 0.5f;
        float cy = SpriteSize * 0.5f;
        float r = SpriteSize * 0.45f;

        for (int y = 0; y < SpriteSize; y++)
        for (int x = 0; x < SpriteSize; x++)
        {
            float dx = x - cx + 0.5f;
            float dy = y - cy + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist <= r)
                tex.SetPixel(x, y, dist >= r - 1.5f ? DarkenBorder(fill) : (Color)fill);
        }

        SaveSprite(name, tex);
    }

    private static void CreateDiamondSprite(string name, Color32 fill)
    {
        var tex = NewTexture();
        float cx = SpriteSize * 0.5f;
        float cy = SpriteSize * 0.5f;
        float halfW = SpriteSize * 0.45f;

        for (int y = 0; y < SpriteSize; y++)
        for (int x = 0; x < SpriteSize; x++)
        {
            float dx = Mathf.Abs(x + 0.5f - cx);
            float dy = Mathf.Abs(y + 0.5f - cy);
            bool inside = (dx + dy) <= halfW;
            bool onBorder = inside && (dx + dy) >= halfW - 1.5f;
            if (inside)
                tex.SetPixel(x, y, onBorder ? DarkenBorder(fill) : (Color)fill);
        }

        SaveSprite(name, tex);
    }

    private static void CreateRectSprite(string name, Color32 fill, int w, int h)
    {
        var tex = NewTexture();
        int padX = (SpriteSize - w) / 2;
        int padY = (SpriteSize - h) / 2;

        for (int y = padY; y < padY + h; y++)
        for (int x = padX; x < padX + w; x++)
        {
            bool onBorder = x == padX || x == padX + w - 1 || y == padY || y == padY + h - 1;
            tex.SetPixel(x, y, onBorder ? DarkenBorder(fill) : (Color)fill);
        }

        SaveSprite(name, tex);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Texture2D NewTexture()
    {
        var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
        // Fill transparent
        var pixels = new Color32[SpriteSize * SpriteSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);
        tex.SetPixels32(pixels);
        return tex;
    }

    private static Color32 DarkenBorder(Color32 c) =>
        new Color32((byte)(c.r / 2), (byte)(c.g / 2), (byte)(c.b / 2), 255);

    private static void SaveSprite(string spriteName, Texture2D tex)
    {
        tex.Apply();
        string pngPath = $"{OutputPath}/{spriteName}.png";
        string fullPath = Path.Combine(Application.dataPath, pngPath.Substring("Assets/".Length));

        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
        SetSpriteImportSettings(pngPath);
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
        settings.spritePixelsPerUnit = SpriteSize;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Sprites"))
            AssetDatabase.CreateFolder("Assets/Art", "Sprites");
        if (!AssetDatabase.IsValidFolder(OutputPath))
            AssetDatabase.CreateFolder("Assets/Art/Sprites", "Placeholder");
    }
}
