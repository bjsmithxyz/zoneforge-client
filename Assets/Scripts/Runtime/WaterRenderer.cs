using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Renders a flat quad at zone.water_level Y.
/// The water shader samples the terrain splatmap and clips Ravine-dominant pixels.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterRenderer : MonoBehaviour
{
    [SerializeField] private Material _waterMaterial;

    private MeshFilter _filter;
    private MeshRenderer _renderer;

    void Awake()
    {
        _filter   = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();
    }

    void OnEnable()
    {
        SpacetimeDBManager.OnConnected          += RefreshWater;
        SpacetimeDBManager.OnZoneChanged        += OnZoneChanged;

        if (SpacetimeDBManager.IsSubscribed) RefreshWater();
    }

    void OnDisable()
    {
        SpacetimeDBManager.OnConnected      -= RefreshWater;
        SpacetimeDBManager.OnZoneChanged    -= OnZoneChanged;
    }

    void OnZoneChanged(ulong _) => RefreshWater();

    void RefreshWater()
    {
        if (SpacetimeDBManager.Conn == null || SpacetimeDBManager.CurrentZoneId == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        foreach (var zone in SpacetimeDBManager.Conn.Db.Zone.Iter())
        {
            if (zone.Id != SpacetimeDBManager.CurrentZoneId) continue;
            BuildWaterMesh(zone.TerrainWidth, zone.TerrainHeight, zone.WaterLevel);
            return;
        }

        gameObject.SetActive(false);
    }

    void BuildWaterMesh(uint width, uint height, float waterLevel)
    {
        float w = width;
        float h = height;

        var mesh = new Mesh { name = "WaterMesh" };
        mesh.vertices  = new[]
        {
            new Vector3(0, waterLevel, 0),
            new Vector3(w, waterLevel, 0),
            new Vector3(w, waterLevel, h),
            new Vector3(0, waterLevel, h),
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();

        _filter.mesh = mesh;
        _renderer.sharedMaterial = _waterMaterial;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Called by TerrainRenderer after building the splatmap texture,
    /// so the water shader can discard Ravine-dominant pixels.
    /// </summary>
    public void SetSplatTexture(Texture2D tex, int terrainWidth, int terrainHeight)
    {
        if (_waterMaterial == null) return;
        _waterMaterial.SetTexture("_SplatTex", tex);
        _waterMaterial.SetVector("_TerrainSize", new Vector4(terrainWidth, terrainHeight, 0, 0));
    }
}
