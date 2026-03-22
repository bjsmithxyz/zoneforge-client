using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bakes a NavMesh at runtime from the terrain mesh whenever TerrainRenderer fires
/// OnMeshBuilt. Uses the legacy NavMeshBuilder API — no extra Unity package required.
/// Attach to any persistent scene GameObject (e.g. SpacetimeDBManager's GO, or its own).
/// </summary>
public class NavMeshManager : MonoBehaviour
{
    public static NavMeshManager Instance { get; private set; }

    /// <summary>Fires once after the first successful NavMesh bake.</summary>
    public static event System.Action OnNavMeshReady;

    /// <summary>True after at least one successful bake has completed.</summary>
    public static bool IsReady { get; private set; }

    private NavMeshData _navMeshData;
    private NavMeshDataInstance _dataInstance;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()  => TerrainRenderer.OnMeshBuilt += Bake;
    void OnDisable() => TerrainRenderer.OnMeshBuilt -= Bake;

    void Bake(Mesh mesh, Transform terrainTransform)
    {
        // Remove stale NavMesh data from previous bake
        if (_navMeshData != null)
        {
            _dataInstance.Remove();
            _navMeshData = null;
        }

        // Build a single source from the terrain mesh
        var source = new NavMeshBuildSource
        {
            shape        = NavMeshBuildSourceShape.Mesh,
            sourceObject = mesh,
            transform    = terrainTransform.localToWorldMatrix,
            area         = 0,  // Walkable
        };

        // Use default Humanoid agent settings (ID 0)
        var settings = NavMesh.GetSettingsByID(0);

        // World-space bounds: cover the terrain footprint + generous vertical range
        var worldCenter = terrainTransform.TransformPoint(mesh.bounds.center);
        var worldSize   = Vector3.Scale(mesh.bounds.size, terrainTransform.lossyScale)
                          + new Vector3(0f, 20f, 0f);
        var bounds = new Bounds(worldCenter, worldSize);

        _navMeshData = NavMeshBuilder.BuildNavMeshData(
            settings,
            new List<NavMeshBuildSource> { source },
            bounds,
            Vector3.zero,
            Quaternion.identity);

        if (_navMeshData == null)
        {
            Debug.LogError("[NavMeshManager] NavMesh bake failed — BuildNavMeshData returned null");
            return;
        }

        _dataInstance = NavMesh.AddNavMeshData(_navMeshData);
        Debug.Log("[NavMeshManager] NavMesh baked successfully");

        bool firstBake = !IsReady;
        IsReady = true;
        if (firstBake)
            OnNavMeshReady?.Invoke();
    }
}
