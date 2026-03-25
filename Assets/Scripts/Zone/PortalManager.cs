using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// Scene singleton. Manages portal ring visuals and proximity logic.
/// Add to SampleScene. Requires SpacetimeDBManager and ZoneTransferManager.
public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    private readonly Dictionary<ulong, GameObject> _portals  = new();
    private readonly HashSet<ulong>                _preloaded = new();

    private const float PreloadRadius = 5f;
    private const float TriggerRadius = 1.5f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SpacetimeDBManager.OnPortalInserted += OnPortalInserted;
        SpacetimeDBManager.OnPortalDeleted  += OnPortalDeleted;
        SpacetimeDBManager.OnConnected      += OnConnected;
        SpacetimeDBManager.OnZoneChanged    += OnZoneChanged;
    }

    void OnDestroy()
    {
        SpacetimeDBManager.OnPortalInserted -= OnPortalInserted;
        SpacetimeDBManager.OnPortalDeleted  -= OnPortalDeleted;
        SpacetimeDBManager.OnConnected      -= OnConnected;
        SpacetimeDBManager.OnZoneChanged    -= OnZoneChanged;
    }

    void OnConnected()
    {
        foreach (var portal in SpacetimeDBManager.Conn.Db.Portal.Iter())
        {
            if (portal.SourceZoneId == SpacetimeDBManager.CurrentZoneId
                || (portal.Bidirectional && portal.DestZoneId == SpacetimeDBManager.CurrentZoneId))
                SpawnPortalGo(portal);
        }
    }

    void OnPortalInserted(Portal portal)
    {
        if (_portals.ContainsKey(portal.Id)) return;
        if (portal.SourceZoneId == SpacetimeDBManager.CurrentZoneId
            || (portal.Bidirectional && portal.DestZoneId == SpacetimeDBManager.CurrentZoneId))
            SpawnPortalGo(portal);
    }

    void OnPortalDeleted(Portal portal)
    {
        if (!_portals.TryGetValue(portal.Id, out var go)) return;
        Destroy(go);
        _portals.Remove(portal.Id);
        _preloaded.Remove(portal.Id);
    }

    void OnZoneChanged(ulong oldZoneId)
    {
        // Destroy all portal GOs — OnConnected backfill spawns the new zone's portals
        foreach (var go in _portals.Values) Destroy(go);
        _portals.Clear();
        _preloaded.Clear();
    }

    void Update()
    {
        if (!SpacetimeDBManager.IsSubscribed) return;
        var localPlayer = GetLocalPlayerRow();
        if (localPlayer == null) return;
        var localPos = new Vector3(localPlayer.PositionX, 0f, localPlayer.PositionY);

        foreach (var kvp in _portals)
        {
            var portal = SpacetimeDBManager.Conn.Db.Portal.Id.Find(kvp.Key);
            if (portal == null) continue;
            bool reverse = portal.DestZoneId == SpacetimeDBManager.CurrentZoneId && portal.Bidirectional;
            float px = reverse ? portal.DestSpawnX : portal.SourceX;
            float py = reverse ? portal.DestSpawnY : portal.SourceY;
            var portalPos = new Vector3(px, 0f, py);
            float dist = Vector3.Distance(localPos, portalPos);

            if (dist < PreloadRadius && !_preloaded.Contains(kvp.Key))
            {
                _preloaded.Add(kvp.Key);
                ulong destZoneId = reverse ? portal.SourceZoneId : portal.DestZoneId;
                PreloadZone(destZoneId);
            }

            if (dist < TriggerRadius && ZoneTransferManager.Instance != null
                && !ZoneTransferManager.Instance.IsTransferring)
            {
                ZoneTransferManager.Instance.BeginTransfer(portal);
            }
        }
    }

    void SpawnPortalGo(Portal portal)
    {
        bool reverse = portal.DestZoneId == SpacetimeDBManager.CurrentZoneId && portal.Bidirectional;
        float px = reverse ? portal.DestSpawnX : portal.SourceX;
        float py = reverse ? portal.DestSpawnY : portal.SourceY;

        var go = new GameObject($"Portal_{portal.Id}");
        go.transform.position = new Vector3(px, 0f, py);

        // Invisible trigger cylinder for reference (no MeshRenderer)
        var col = go.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius    = TriggerRadius;
        col.height    = 2f;
        col.center    = Vector3.up;

        // Glowing ring — thin cylinder scaled flat
        var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringGo.transform.SetParent(go.transform);
        ringGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        ringGo.transform.localScale    = new Vector3(2f, 0.03f, 2f);
        Destroy(ringGo.GetComponent<Collider>());
        var rend = ringGo.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogWarning("[PortalManager] URP Lit shader not found — portal ring will be pink"); }
        var mat  = new Material(shader);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 2f);
        mat.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        rend.sharedMaterial = mat;

        _portals[portal.Id] = go;
    }

    void PreloadZone(ulong destZoneId)
    {
        Debug.Log($"[PortalManager] Pre-loading zone {destZoneId}");
        SpacetimeDBManager.Conn.SubscriptionBuilder()
            .OnApplied(_ => Debug.Log($"[PortalManager] Pre-load complete for zone {destZoneId}"))
            .Subscribe(new[]
            {
                $"SELECT * FROM terrain_chunk WHERE zone_id = {destZoneId}",
                $"SELECT * FROM entity_instance WHERE zone_id = {destZoneId}",
                $"SELECT * FROM enemy WHERE zone_id = {destZoneId}",
                $"SELECT * FROM portal WHERE source_zone_id = {destZoneId}",
                $"SELECT * FROM portal WHERE dest_zone_id = {destZoneId}",
            });
    }

    static Player GetLocalPlayerRow()
    {
        if (!SpacetimeDBManager.IsSubscribed) return null;
        return SpacetimeDBManager.Conn.Db.Player.Identity.Find(SpacetimeDBManager.LocalIdentity);
    }
}
