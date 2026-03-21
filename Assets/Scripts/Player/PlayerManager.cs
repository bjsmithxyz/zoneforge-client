using System.Collections.Generic;
using UnityEngine;
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
    }

    void OnDestroy()
    {
        SpacetimeDBManager.OnPlayerInserted -= OnPlayerInserted;
        SpacetimeDBManager.OnPlayerUpdated  -= OnPlayerUpdated;
        SpacetimeDBManager.OnPlayerDeleted  -= OnPlayerDeleted;
        SpacetimeDBManager.OnConnected      -= OnConnected;
    }

    void OnConnected()
    {
        // Backfill: initial rows arrive before SpacetimeDBManager registers
        // Conn.Db.Player.OnInsert, so OnPlayerInserted never fires for them.
        foreach (var player in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (!_players.ContainsKey(player.Id))
                SpawnPlayer(player);
        }

        // Call create_player only if no row exists for our identity
        bool hasLocalPlayer = false;
        foreach (var player in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (player.Identity == SpacetimeDBManager.LocalIdentity)
            {
                hasLocalPlayer = true;
                break;
            }
        }
        if (!hasLocalPlayer)
            SpacetimeDBManager.Conn.Reducers.CreatePlayer("Player");
    }

    void OnPlayerInserted(Player player)
    {
        if (_players.ContainsKey(player.Id)) return; // guard against backfill duplicate
        SpawnPlayer(player);
    }

    void OnPlayerUpdated(Player oldPlayer, Player newPlayer)
    {
        if (!_players.TryGetValue(newPlayer.Id, out var go)) return;
        go.GetComponent<PlayerController>().ReceiveServerPosition(newPlayer);
    }

    void OnPlayerDeleted(Player player)
    {
        if (!_players.TryGetValue(player.Id, out var go)) return;
        Destroy(go);
        _players.Remove(player.Id);
    }

    void SpawnPlayer(Player player)
    {
        bool isLocal = player.Identity == SpacetimeDBManager.LocalIdentity;

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = isLocal ? "LocalPlayer" : $"RemotePlayer_{player.Id}";

        var ctrl = go.AddComponent<PlayerController>();
        ctrl.Init(player, isLocal);

        if (isLocal)
            AttachCamera(go);

        _players[player.Id] = go;
        Debug.Log($"[PlayerManager] Spawned {go.name} at ({player.PositionX}, {player.PositionY})");
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
