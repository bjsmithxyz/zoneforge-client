using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Renders ItemDrop rows as small colored cubes on the terrain surface so
/// players can see where loot landed. Pickup is handled by ItemPickupManager.
/// </summary>
public class ItemDropRenderer : MonoBehaviour
{
    public static ItemDropRenderer Instance { get; private set; }

    private readonly Dictionary<ulong, GameObject> _markers = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SpacetimeDBManager.OnItemDropInserted += OnItemDropInserted;
        SpacetimeDBManager.OnItemDropDeleted  += OnItemDropDeleted;
        SpacetimeDBManager.OnConnected        += OnConnected;
        SpacetimeDBManager.OnZoneChanged      += OnZoneChanged;
    }

    void OnDestroy()
    {
        SpacetimeDBManager.OnItemDropInserted -= OnItemDropInserted;
        SpacetimeDBManager.OnItemDropDeleted  -= OnItemDropDeleted;
        SpacetimeDBManager.OnConnected        -= OnConnected;
        SpacetimeDBManager.OnZoneChanged      -= OnZoneChanged;
    }

    void OnConnected()
    {
        foreach (var drop in SpacetimeDBManager.Conn.Db.ItemDrop.Iter())
            if (!_markers.ContainsKey(drop.Id))
                SpawnMarker(drop);
    }

    void OnZoneChanged(ulong _oldZoneId)
    {
        foreach (var go in _markers.Values)
            if (go != null) Destroy(go);
        _markers.Clear();
    }

    void OnItemDropInserted(ItemDrop drop)
    {
        if (_markers.ContainsKey(drop.Id)) return;
        SpawnMarker(drop);
    }

    void OnItemDropDeleted(ItemDrop drop)
    {
        if (!_markers.TryGetValue(drop.Id, out var go)) return;
        Destroy(go);
        _markers.Remove(drop.Id);
    }

    void SpawnMarker(ItemDrop drop)
    {
        // Look up def for name/rarity coloring
        ItemDefinition def = null;
        foreach (var d in SpacetimeDBManager.Conn.Db.ItemDef.Iter())
        {
            if (d.Id == drop.ItemDefId) { def = d; break; }
        }

        float y = TerrainRenderer.GetSurfaceHeight(drop.PosX, drop.PosY) + 0.2f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"ItemDrop_{drop.Id}_{def?.Name ?? "Unknown"}";
        go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        go.transform.position   = new Vector3(drop.PosX, y, drop.PosY);
        go.transform.rotation   = Quaternion.Euler(45f, 45f, 0f);

        var rend = go.GetComponent<Renderer>();
        rend.material       = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rend.material.color = GetRarityColor(def?.Rarity);

        // No collider needed — pickup is proximity-based, not physics
        Destroy(go.GetComponent<Collider>());

        go.AddComponent<ItemDropBob>();

        _markers[drop.Id] = go;
    }

    static Color GetRarityColor(Rarity? rarity) => rarity switch
    {
        Rarity.Common    => new Color(0.85f, 0.85f, 0.85f),
        Rarity.Uncommon  => new Color(0.30f, 0.95f, 0.30f),
        Rarity.Rare      => new Color(0.30f, 0.55f, 1.00f),
        Rarity.Epic      => new Color(0.75f, 0.30f, 1.00f),
        _                => Color.white,
    };
}

/// <summary>Gentle hover + rotation so loot is easy to spot.</summary>
public class ItemDropBob : MonoBehaviour
{
    private float _baseY;
    private float _t;

    void Start() { _baseY = transform.position.y; }

    void Update()
    {
        _t += Time.deltaTime;
        var p = transform.position;
        p.y = _baseY + Mathf.Sin(_t * 2f) * 0.08f;
        transform.position = p;
        transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
    }
}
