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
    // Pre-decoded byte arrays (List<byte>.ToArray() is expensive; cache once per chunk update).
    private readonly Dictionary<(int, int), byte[]> _heightArrays = new();
    private readonly Dictionary<(int, int), byte[]> _splatArrays  = new();

    // Persistent mesh buffers — reused across patches to avoid per-update array allocation.
    private Vector3[] _vertices;
    private Vector2[] _uv0;
    private Vector2[] _uv1;
    private int[]     _triangles;

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
        _heightArrays.Clear();
        _splatArrays.Clear();
        ClearSplatTexture();
        RebuildFromActiveZone();
    }

    void OnChunkUpdated(TerrainChunk chunk)
    {
        if (chunk.ZoneId != SpacetimeDBManager.CurrentZoneId) return;
        var key = ((int)chunk.ChunkX, (int)chunk.ChunkZ);
        _chunks[key]       = chunk;
        _heightArrays[key] = chunk.HeightData.ToArray();
        _splatArrays[key]  = chunk.SplatData.ToArray();
        PatchChunk(chunk);
    }

    // -----------------------------------------------------------------------

    void RebuildFromActiveZone()
    {
        if (SpacetimeDBManager.Conn == null || SpacetimeDBManager.CurrentZoneId == 0) return;

        var activeZone = SpacetimeDBManager.Conn.Db.Zone.Id.Find(SpacetimeDBManager.CurrentZoneId);
        if (activeZone == null) { _terrainWidth = _terrainHeight = 0; return; }
        _terrainWidth  = (int)activeZone.TerrainWidth;
        _terrainHeight = (int)activeZone.TerrainHeight;

        // Cache all chunks for this zone.
        _chunks.Clear();
        _heightArrays.Clear();
        _splatArrays.Clear();
        foreach (var chunk in SpacetimeDBManager.Conn.Db.TerrainChunk.Iter())
        {
            if (chunk.ZoneId != SpacetimeDBManager.CurrentZoneId) continue;
            var key = ((int)chunk.ChunkX, (int)chunk.ChunkZ);
            _chunks[key]       = chunk;
            _heightArrays[key] = chunk.HeightData.ToArray();
            _splatArrays[key]  = chunk.SplatData.ToArray();
        }

        BuildFullMesh();
    }

    // -----------------------------------------------------------------------
    // Mesh construction

    void BuildFullMesh()
    {
        int w = _terrainWidth;
        int h = _terrainHeight;
        int vertCount = w * h;
        int quadCount = (w - 1) * (h - 1);
        int triCount  = quadCount * 6;

        // Reuse persistent buffers across rebuilds. Only realloc on size change.
        if (_vertices == null || _vertices.Length != vertCount)
        {
            _vertices = new Vector3[vertCount];
            _uv0      = new Vector2[vertCount];
            _uv1      = new Vector2[vertCount];
        }
        if (_triangles == null || _triangles.Length != triCount)
            _triangles = new int[triCount];

        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            int vi = z * w + x;
            float height = SampleHeight(x, z);
            _vertices[vi] = new Vector3(x, height, z);
            _uv0[vi]      = new Vector2(x, z);
            _uv1[vi]      = new Vector2((float)x / w, (float)z / h);
        }

        int t = 0;
        for (int z = 0; z < h - 1; z++)
        for (int x = 0; x < w - 1; x++)
        {
            int vi = z * w + x;
            _triangles[t++] = vi;
            _triangles[t++] = vi + w;
            _triangles[t++] = vi + 1;
            _triangles[t++] = vi + 1;
            _triangles[t++] = vi + w;
            _triangles[t++] = vi + w + 1;
        }

        _mesh.Clear();
        _mesh.vertices  = _vertices;
        _mesh.uv        = _uv0;
        _mesh.uv2       = _uv1;
        _mesh.triangles = _triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        BuildSplatTexture();

        _renderer.sharedMaterial = _terrainMaterial;

        OnMeshBuilt?.Invoke(_mesh, transform);
    }

    float SampleHeight(int gx, int gz)
    {
        TerrainChunkData.WorldToChunk(gx, gz, TerrainChunkData.ChunkSize,
            out int cx, out int cz);
        int idx = TerrainChunkData.WorldToLocalIndex(gx, gz, TerrainChunkData.ChunkSize);

        if (_heightArrays.TryGetValue((cx, cz), out var heights))
            return TerrainChunkData.GetHeight(heights, idx);
        return 0f;
    }

    void BuildSplatTexture()
    {
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
            if (_splatArrays.TryGetValue((cx, cz), out var splat))
            {
                r = splat[idx * 4];
                g = splat[idx * 4 + 1];
                b = splat[idx * 4 + 2];
                a = splat[idx * 4 + 3];
            }
            pixels[z * _terrainWidth + x] = new Color32(r, g, b, a);
        }

        _splatTexture.SetPixels32(pixels);
        _splatTexture.Apply();

        _terrainMaterial.SetTexture("_SplatTex", _splatTexture);
    }

    /// <summary>
    /// Incremental patch: only re-samples vertices in the updated chunk's region
    /// (plus a +1 sample-point overlap to update the shared edge with neighbour chunks),
    /// and writes only that chunk's pixels into the splatmap texture region.
    /// Falls back to a full rebuild on first call if buffers aren't allocated yet.
    /// </summary>
    void PatchChunk(TerrainChunk chunk)
    {
        if (_terrainWidth == 0) return;
        if (_vertices == null || _vertices.Length != _terrainWidth * _terrainHeight ||
            _splatTexture == null)
        {
            BuildFullMesh();
            return;
        }

        int CS = TerrainChunkData.ChunkSize;
        int cx = (int)chunk.ChunkX, cz = (int)chunk.ChunkZ;
        int x0 = cx * CS, z0 = cz * CS;
        int xEnd = Mathf.Min(_terrainWidth  - 1, x0 + CS);
        int zEnd = Mathf.Min(_terrainHeight - 1, z0 + CS);

        for (int z = z0; z <= zEnd; z++)
        for (int x = x0; x <= xEnd; x++)
        {
            int vi = z * _terrainWidth + x;
            _vertices[vi] = new Vector3(x, SampleHeight(x, z), z);
        }
        _mesh.vertices = _vertices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        // Splat patch: chunk's pixel rect (no overlap — splat is per-pixel, not per-vertex).
        if (_splatArrays.TryGetValue((cx, cz), out var splat))
        {
            int splatW = Mathf.Min(CS, _terrainWidth  - x0);
            int splatH = Mathf.Min(CS, _terrainHeight - z0);
            var pixels = new Color32[splatW * splatH];
            for (int z = 0; z < splatH; z++)
            for (int x = 0; x < splatW; x++)
            {
                int idx = z * CS + x;
                pixels[z * splatW + x] = new Color32(
                    splat[idx * 4], splat[idx * 4 + 1],
                    splat[idx * 4 + 2], splat[idx * 4 + 3]);
            }
            _splatTexture.SetPixels32(x0, z0, splatW, splatH, pixels);
            _splatTexture.Apply(false);
        }

        OnMeshBuilt?.Invoke(_mesh, transform);
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
