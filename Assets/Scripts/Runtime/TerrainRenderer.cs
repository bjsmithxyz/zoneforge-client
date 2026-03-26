using System;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Builds and maintains the terrain mesh from TerrainChunk rows.
/// Attach to a GameObject in the scene; assign the TerrainSplatmap material.
/// TerrainRenderer rebuilds when the active zone changes and patches on chunk updates.
/// Fires OnMeshBuilt after every full rebuild so NavMeshManager can re-bake.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainRenderer : MonoBehaviour
{
    /// <summary>Fired after every full mesh rebuild. Args: the new Mesh and this Transform.</summary>
    public static event Action<Mesh, Transform> OnMeshBuilt;

    [SerializeField] private Material _terrainMaterial;

    public static TerrainRenderer Instance { get; private set; }

    private Mesh _mesh;
    private MeshFilter _filter;
    private MeshRenderer _renderer;

    // zone dimensions in sample points
    private int _terrainWidth;
    private int _terrainHeight;

    // chunk cache: (cx, cz) → last received TerrainChunk
    private readonly Dictionary<(int, int), TerrainChunk> _chunks = new();

    // single full-terrain splatmap texture (rebuilt when zone changes)
    private Texture2D _splatTexture;

    // -----------------------------------------------------------------------

    void Awake()
    {
        Instance  = this;
        _filter   = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();
        _mesh = new Mesh { name = "TerrainMesh" };
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _filter.mesh = _mesh;
    }

    /// <summary>
    /// Returns the terrain surface Y at world position (wx, wz).
    /// Returns 0 if terrain is not yet loaded at this position.
    /// </summary>
    public static float GetSurfaceHeight(float wx, float wz)
    {
        if (Instance == null) return 0f;
        // Convert world XZ to terrain-local grid coordinates (accounts for GO position offset).
        Vector3 offset = Instance.transform.position;
        float gx = wx - offset.x;
        float gz = wz - offset.z;

        // Bilinear interpolation across the quad so the returned height matches the
        // actual mesh surface between vertices, not just the nearest corner.
        int x0 = Mathf.FloorToInt(gx);
        int z0 = Mathf.FloorToInt(gz);
        float tx = gx - x0;
        float tz = gz - z0;

        float h00 = Instance.SampleHeight(x0,   z0);
        float h10 = Instance.SampleHeight(x0+1, z0);
        float h01 = Instance.SampleHeight(x0,   z0+1);
        float h11 = Instance.SampleHeight(x0+1, z0+1);

        float localY = Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        return offset.y + localY;
    }

    void OnEnable()
    {
        SpacetimeDBManager.OnConnected            += OnConnected;
        SpacetimeDBManager.OnTerrainChunkUpdated  += OnChunkUpdated;
        SpacetimeDBManager.OnZoneChanged          += OnActiveZoneChanged;

        if (SpacetimeDBManager.IsSubscribed)
            RebuildFromActiveZone();
    }

    void OnDisable()
    {
        SpacetimeDBManager.OnConnected           -= OnConnected;
        SpacetimeDBManager.OnTerrainChunkUpdated -= OnChunkUpdated;
        SpacetimeDBManager.OnZoneChanged         -= OnActiveZoneChanged;
    }

    // -----------------------------------------------------------------------

    void OnConnected() => RebuildFromActiveZone();

    void OnActiveZoneChanged(ulong _)
    {
        _chunks.Clear();
        ClearSplatTexture();
        RebuildFromActiveZone();
    }

    void OnChunkUpdated(TerrainChunk chunk)
    {
        if (chunk.ZoneId != SpacetimeDBManager.CurrentZoneId) return;
        _chunks[((int)chunk.ChunkX, (int)chunk.ChunkZ)] = chunk;
        PatchChunk(chunk);
    }

    // -----------------------------------------------------------------------

    void RebuildFromActiveZone()
    {
        if (SpacetimeDBManager.Conn == null || SpacetimeDBManager.CurrentZoneId == 0) return;

        _terrainWidth  = 0;
        _terrainHeight = 0;

        foreach (var zone in SpacetimeDBManager.Conn.Db.Zone.Iter())
        {
            if (zone.Id != SpacetimeDBManager.CurrentZoneId) continue;
            _terrainWidth  = (int)zone.TerrainWidth;
            _terrainHeight = (int)zone.TerrainHeight;
            break;
        }

        if (_terrainWidth == 0) return;

        // Cache all chunks for this zone.
        _chunks.Clear();
        foreach (var chunk in SpacetimeDBManager.Conn.Db.TerrainChunk.Iter())
        {
            if (chunk.ZoneId != SpacetimeDBManager.CurrentZoneId) continue;
            _chunks[((int)chunk.ChunkX, (int)chunk.ChunkZ)] = chunk;
        }

        BuildFullMesh();
    }

    // -----------------------------------------------------------------------
    // Mesh construction

    void BuildFullMesh()
    {
        int w = _terrainWidth;
        int h = _terrainHeight;

        // Vertices: one per sample point
        var vertices = new Vector3[w * h];
        var uv0      = new Vector2[w * h]; // world XZ (passed to shader as worldXZ)
        var uv1      = new Vector2[w * h]; // normalised terrain UV for splatmap

        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            int vi = z * w + x;
            float height = SampleHeight(x, z);
            vertices[vi] = new Vector3(x, height, z);
            uv0[vi]      = new Vector2(x, z);
            uv1[vi]      = new Vector2((float)x / w, (float)z / h);
        }

        // Triangles: 2 per quad
        int quadCount = (w - 1) * (h - 1);
        var triangles = new int[quadCount * 6];
        int t = 0;
        for (int z = 0; z < h - 1; z++)
        for (int x = 0; x < w - 1; x++)
        {
            int vi = z * w + x;
            triangles[t++] = vi;
            triangles[t++] = vi + w;
            triangles[t++] = vi + 1;
            triangles[t++] = vi + 1;
            triangles[t++] = vi + w;
            triangles[t++] = vi + w + 1;
        }

        _mesh.Clear();
        _mesh.vertices  = vertices;
        _mesh.uv        = uv0;
        _mesh.uv2       = uv1;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        // Build combined splatmap texture (full terrain, one RGBA texture).
        BuildSplatTexture();

        _renderer.sharedMaterial = _terrainMaterial;

        OnMeshBuilt?.Invoke(_mesh, transform);
    }

    float SampleHeight(int gx, int gz)
    {
        TerrainChunkData.WorldToChunk(gx, gz, TerrainChunkData.ChunkSize,
            out int cx, out int cz);
        int idx = TerrainChunkData.WorldToLocalIndex(gx, gz, TerrainChunkData.ChunkSize);

        if (_chunks.TryGetValue((cx, cz), out var chunk))
            return TerrainChunkData.GetHeight(chunk.HeightData.ToArray(), idx);
        return 0f;
    }

    void BuildSplatTexture()
    {
        // Reuse or create the full-terrain RGBA texture.
        if (_splatTexture == null ||
            _splatTexture.width != _terrainWidth ||
            _splatTexture.height != _terrainHeight)
        {
            if (_splatTexture != null) Destroy(_splatTexture);
            _splatTexture = new Texture2D(_terrainWidth, _terrainHeight, TextureFormat.RGBA32, false);
        }

        var pixels = new Color32[_terrainWidth * _terrainHeight];

        for (int z = 0; z < _terrainHeight; z++)
        for (int x = 0; x < _terrainWidth;  x++)
        {
            TerrainChunkData.WorldToChunk(x, z, TerrainChunkData.ChunkSize,
                out int cx, out int cz);
            int idx = TerrainChunkData.WorldToLocalIndex(x, z, TerrainChunkData.ChunkSize);

            byte r = 255, g = 0, b = 0, a = 0;
            if (_chunks.TryGetValue((cx, cz), out var chunk))
            {
                r = chunk.SplatData[idx * 4];
                g = chunk.SplatData[idx * 4 + 1];
                b = chunk.SplatData[idx * 4 + 2];
                a = chunk.SplatData[idx * 4 + 3];
            }
            pixels[z * _terrainWidth + x] = new Color32(r, g, b, a);
        }

        _splatTexture.SetPixels32(pixels);
        _splatTexture.Apply();

        _terrainMaterial.SetTexture("_SplatTex", _splatTexture);
    }

    /// <summary>Patch only the vertices and splat pixels affected by a single chunk update.</summary>
    void PatchChunk(TerrainChunk chunk)
    {
        if (_terrainWidth == 0) return;
        // For simplicity in this initial implementation, rebuild the full mesh.
        // Optimise to partial vertex update in a follow-up if needed.
        BuildFullMesh();
    }

    void ClearSplatTexture()
    {
        if (_splatTexture != null)
        {
            Destroy(_splatTexture);
            _splatTexture = null;
        }
    }
}
