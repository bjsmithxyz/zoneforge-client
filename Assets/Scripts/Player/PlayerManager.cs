using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SpacetimeDB.Types;

/// <summary>
/// Scene singleton. Subscribes to SpacetimeDBManager player events in Awake
/// (before SpacetimeDBManager.Start) and maintains a capsule GameObject per
/// player row. Backfills existing rows in OnConnected because the SDK fires
/// initial OnInsert callbacks before SpacetimeDBManager registers them in
/// OnSubscriptionApplied.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    private readonly Dictionary<ulong, GameObject> _players = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SpacetimeDBManager.OnPlayerInserted += OnPlayerInserted;
        SpacetimeDBManager.OnPlayerUpdated  += OnPlayerUpdated;
        SpacetimeDBManager.OnPlayerDeleted  += OnPlayerDeleted;
        SpacetimeDBManager.OnConnected      += OnConnected;
        SpacetimeDBManager.OnZoneChanged    += OnZoneChanged;
        NavMeshManager.OnNavMeshReady       += OnNavMeshBaked;
    }

    void OnDestroy()
    {
        SpacetimeDBManager.OnPlayerInserted -= OnPlayerInserted;
        SpacetimeDBManager.OnPlayerUpdated  -= OnPlayerUpdated;
        SpacetimeDBManager.OnPlayerDeleted  -= OnPlayerDeleted;
        SpacetimeDBManager.OnConnected      -= OnConnected;
        SpacetimeDBManager.OnZoneChanged    -= OnZoneChanged;
        NavMeshManager.OnNavMeshReady       -= OnNavMeshBaked;
    }

    void OnConnected()
    {
        // Backfill: initial rows arrive before SpacetimeDBManager registers
        // Conn.Db.Player.OnInsert, so OnPlayerInserted never fires for them.
        bool hasLocalPlayer = false;
        foreach (var player in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            bool inCurrentZone = player.Identity == SpacetimeDBManager.LocalIdentity
                || player.ZoneId == SpacetimeDBManager.CurrentZoneId;
            if (!_players.ContainsKey(player.Id) && inCurrentZone)
                SpawnPlayer(player);
            if (player.Identity == SpacetimeDBManager.LocalIdentity)
                hasLocalPlayer = true;
        }
        if (!hasLocalPlayer)
        {
            Debug.Log("[PlayerManager] No local player row found — calling create_player");
            SpacetimeDBManager.Conn.Reducers.CreatePlayer("Player");
        }
    }

    void OnZoneChanged(ulong oldZoneId)
    {
        // Destroy remote player capsules that belonged to the old zone
        var toRemove = new List<ulong>();
        foreach (var kvp in _players)
        {
            var player = SpacetimeDBManager.Conn.Db.Player.Id.Find(kvp.Key);
            if (player == null) continue;
            if (player.Identity == SpacetimeDBManager.LocalIdentity) continue; // never destroy local
            if (player.ZoneId != SpacetimeDBManager.CurrentZoneId)
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var id in toRemove) _players.Remove(id);

        // Backfill: spawn remote players now in the new zone (pre-loaded rows)
        foreach (var player in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (player.Identity == SpacetimeDBManager.LocalIdentity) continue;
            if (player.ZoneId != SpacetimeDBManager.CurrentZoneId) continue;
            if (!_players.ContainsKey(player.Id))
                SpawnPlayer(player);
        }
    }

    void OnPlayerInserted(Player player)
    {
        if (_players.ContainsKey(player.Id)) return; // guard against backfill duplicate
        // Zone-gate: only spawn remote players in the current zone
        if (player.Identity != SpacetimeDBManager.LocalIdentity
            && player.ZoneId != SpacetimeDBManager.CurrentZoneId) return;
        SpawnPlayer(player);
    }

    void OnPlayerUpdated(Player oldPlayer, Player newPlayer)
    {
        bool isLocal = newPlayer.Identity == SpacetimeDBManager.LocalIdentity;

        // Remote player moved to a different zone — destroy their capsule
        if (!isLocal && newPlayer.ZoneId != SpacetimeDBManager.CurrentZoneId)
        {
            if (_players.TryGetValue(newPlayer.Id, out var go))
            {
                Destroy(go);
                _players.Remove(newPlayer.Id);
            }
            return;
        }

        // Remote player arrived in this zone — spawn their capsule
        if (!isLocal && newPlayer.ZoneId == SpacetimeDBManager.CurrentZoneId
            && !_players.ContainsKey(newPlayer.Id))
        {
            SpawnPlayer(newPlayer);
            return;
        }

        // Normal update (position, health, etc.)
        if (!_players.TryGetValue(newPlayer.Id, out var playerGo)) return;
        var ctrl = playerGo.GetComponent<PlayerController>();
        if (ctrl == null) { Debug.LogWarning($"[PlayerManager] PlayerController missing on {playerGo.name}"); return; }
        ctrl.ReceiveServerPosition(newPlayer);
        CombatManager.Instance?.RegisterPlayerPosition(
            newPlayer.Id,
            new Vector3(newPlayer.PositionX, 1f, newPlayer.PositionY));
    }

    void OnPlayerDeleted(Player player)
    {
        if (!_players.TryGetValue(player.Id, out var go)) return;
        // If the local player is deleted, detach the camera before destroying the GO
        if (player.Identity == SpacetimeDBManager.LocalIdentity)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetParent(null);
                Debug.LogWarning("[PlayerManager] Local player deleted — camera detached");
            }
        }
        Destroy(go);
        _players.Remove(player.Id);
    }

    void OnNavMeshBaked()
    {
        foreach (var go in _players.Values)
        {
            var ctrl = go.GetComponent<PlayerController>();
            if (ctrl == null || !ctrl.IsLocal) continue;
            if (go.GetComponent<NavMeshAgent>() != null) continue;  // already added
            AddNavMeshAgent(go, ctrl);
        }
    }

    /// <summary>Returns the GameObject for a player id, or null if not spawned.</summary>
    public GameObject GetPlayerObject(ulong playerId) =>
        _players.TryGetValue(playerId, out var go) ? go : null;

    static void AddNavMeshAgent(GameObject go, PlayerController ctrl)
    {
        var agent = go.AddComponent<NavMeshAgent>();
        agent.speed           = 5f;
        agent.angularSpeed    = 0f;    // camera-relative input handles facing
        agent.acceleration    = 100f;  // instant acceleration for responsive feel
        agent.stoppingDistance = 0f;
        agent.autoBraking     = false;
        agent.radius          = 0.5f;
        agent.height          = 2f;
        agent.baseOffset      = 1f;    // pivot sits 1 unit above NavMesh (capsule center)
        agent.updateRotation  = false;
        ctrl.SetAgent(agent);
        Debug.Log("[PlayerManager] NavMeshAgent added to local player");
    }

    void SpawnPlayer(Player player)
    {
        bool isLocal = player.Identity == SpacetimeDBManager.LocalIdentity;

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = isLocal ? "LocalPlayer" : $"RemotePlayer_{player.Id}";
        var rend = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = isLocal ? Color.cyan : Color.red;
        rend.material = mat;

        // Disable the auto-added CapsuleCollider — NavMeshAgent handles movement and
        // the collider causes projectiles to trigger OnCollisionEnter at launch origin.
        var capsuleCol = go.GetComponent<CapsuleCollider>();
        if (capsuleCol != null) capsuleCol.enabled = false;

        var ctrl = go.AddComponent<PlayerController>();
        ctrl.Init(player, isLocal);

        if (isLocal)
        {
            AttachCamera(go);
            if (NavMeshManager.IsReady)
                AddNavMeshAgent(go, ctrl);
            // else OnNavMeshBaked will add the agent once the bake completes
        }

        var healthBar = go.AddComponent<PlayerHealthBar>();
        healthBar.Init(player, isLocal);

        _players[player.Id] = go;
        CombatManager.Instance?.RegisterPlayerPosition(
            player.Id,
            new Vector3(player.PositionX, 1f, player.PositionY));
        Debug.Log($"[PlayerManager] Spawned {go.name} at (X={player.PositionX}, Z={player.PositionY})");
    }

    static void AttachCamera(GameObject playerGo)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[PlayerManager] No main camera found — skipping camera attachment");
            return;
        }
        cam.transform.SetParent(playerGo.transform);
        cam.transform.localPosition = new Vector3(0f, 10f, -7f);
        cam.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
    }
}
