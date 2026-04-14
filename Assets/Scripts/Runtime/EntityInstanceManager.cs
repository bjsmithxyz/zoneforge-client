using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Scene singleton. Maintains one GameObject per EntityInstance row for decorative
/// NPCs and props placed via the world editor. Combat enemies use EnemyManager.
/// </summary>
public class EntityInstanceManager : MonoBehaviour
{
    public static EntityInstanceManager Instance { get; private set; }

    private readonly Dictionary<ulong, GameObject> _entities = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SpacetimeDBManager.OnEntityInserted += OnEntityInserted;
        SpacetimeDBManager.OnEntityDeleted  += OnEntityDeleted;
        SpacetimeDBManager.OnConnected      += OnConnected;
    }

    void OnDestroy()
    {
        SpacetimeDBManager.OnEntityInserted -= OnEntityInserted;
        SpacetimeDBManager.OnEntityDeleted  -= OnEntityDeleted;
        SpacetimeDBManager.OnConnected      -= OnConnected;
    }

    void OnConnected()
    {
        // Backfill: initial rows may arrive before callbacks are registered
        foreach (var entity in SpacetimeDBManager.Conn.Db.EntityInstance.Iter())
        {
            if (!_entities.ContainsKey(entity.Id))
                SpawnEntity(entity);
        }
    }

    void OnEntityInserted(EntityInstance entity)
    {
        if (_entities.ContainsKey(entity.Id)) return;
        SpawnEntity(entity);
    }

    void OnEntityDeleted(EntityInstance entity)
    {
        if (!_entities.TryGetValue(entity.Id, out var go)) return;
        Destroy(go);
        _entities.Remove(entity.Id);
    }

    void SpawnEntity(EntityInstance entity)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Entity_{entity.Id}_{entity.PrefabName}";
        // Reducer stores world X as position_x, world Z (depth) as position_y, world Y (up) as elevation
        go.transform.position = new Vector3(entity.PositionX, entity.Elevation + 0.5f, entity.PositionY);
        go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

        var rend = go.GetComponent<Renderer>();
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = GetEntityColor(entity.EntityType);
        rend.material = mat;

        var col = go.GetComponent<BoxCollider>();
        if (col != null) col.enabled = false;

        _entities[entity.Id] = go;
    }

    static Color GetEntityColor(string entityType) => entityType switch
    {
        "NPC"  => new Color(0.2f, 0.5f, 1.0f),   // blue
        "Prop" => new Color(0.3f, 0.8f, 0.3f),   // green
        _      => Color.white,
    };
}
