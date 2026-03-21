using System;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

public class SpacetimeDBManager : MonoBehaviour
{
    public static SpacetimeDBManager Instance { get; private set; }
    public static DbConnection Conn { get; private set; }
    public static bool IsSubscribed { get; private set; }
    public static Identity LocalIdentity { get; private set; }

    public static event Action OnConnected;
    public static event Action<Zone> OnZoneInserted;
    public static event Action<Zone, Zone> OnZoneUpdated;
    public static event Action<Zone> OnZoneDeleted;
    public static event Action<EntityInstance> OnEntityInserted;
    public static event Action<Player> OnPlayerInserted;
    public static event Action<Player, Player> OnPlayerUpdated;
    public static event Action<Player> OnPlayerDeleted;
    public static event Action<TerrainChunk> OnTerrainChunkUpdated;

    [SerializeField] private string serverUri = "http://localhost:3000";
    [SerializeField] private string databaseName = "zoneforge-server";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        Debug.Log("[SpacetimeDBManager] Connecting...");
        Conn = DbConnection.Builder()
            .WithUri(serverUri)
            .WithDatabaseName(databaseName)
            .OnConnect(OnConnect)
            .OnConnectError(OnConnectError)
            .OnDisconnect(OnDisconnect)
            .Build();
    }

    void Update()
    {
        Conn?.FrameTick();
    }

    void OnConnect(DbConnection conn, Identity identity, string token)
    {
        Debug.Log($"[SpacetimeDBManager] Connected. Identity: {identity}");
        LocalIdentity = identity;
        conn.SubscriptionBuilder()
            .OnApplied(OnSubscriptionApplied)
            .Subscribe(new[]
            {
                "SELECT * FROM player",
                "SELECT * FROM zone",
                "SELECT * FROM entity_instance",
                "SELECT * FROM terrain_chunk"
            });
    }

    void OnSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("[SpacetimeDBManager] Subscription applied");

        Conn.Db.Zone.OnInsert += (eventCtx, zone) => OnZoneInserted?.Invoke(zone);
        Conn.Db.Zone.OnUpdate += (eventCtx, oldZone, newZone) => OnZoneUpdated?.Invoke(oldZone, newZone);
        Conn.Db.Zone.OnDelete += (eventCtx, zone) => OnZoneDeleted?.Invoke(zone);

        Conn.Db.EntityInstance.OnInsert += (eventCtx, entity) => OnEntityInserted?.Invoke(entity);

        Conn.Db.Player.OnInsert += (eventCtx, player) => OnPlayerInserted?.Invoke(player);
        Conn.Db.Player.OnUpdate += (eventCtx, oldPlayer, newPlayer) => OnPlayerUpdated?.Invoke(oldPlayer, newPlayer);
        Conn.Db.Player.OnDelete += (eventCtx, player) => OnPlayerDeleted?.Invoke(player);

        Conn.Db.TerrainChunk.OnInsert += (eventCtx, chunk) => OnTerrainChunkUpdated?.Invoke(chunk);
        Conn.Db.TerrainChunk.OnUpdate += (eventCtx, _oldChunk, newChunk) => OnTerrainChunkUpdated?.Invoke(newChunk);

        IsSubscribed = true;
        OnConnected?.Invoke();
    }

    void OnConnectError(Exception e)
    {
        Debug.LogError($"[SpacetimeDBManager] Connection failed: {e.Message}");
    }

    void OnDisconnect(DbConnection conn, Exception e)
    {
        LocalIdentity = default;
        IsSubscribed = false;
        if (e != null)
            Debug.LogWarning($"[SpacetimeDBManager] Disconnected with error: {e.Message}");
        else
            Debug.Log("[SpacetimeDBManager] Disconnected cleanly");
    }
}
