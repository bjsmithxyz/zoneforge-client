using System;
using System.Collections;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

public class SpacetimeDBManager : MonoBehaviour
{
    public static SpacetimeDBManager Instance { get; private set; }
    public static DbConnection Conn { get; private set; }
    public static bool IsSubscribed { get; private set; }
    public static Identity LocalIdentity { get; private set; }
    public static ulong CurrentZoneId { get; private set; }

    private const string ZonePrefKey = "spacetimedb_zone";
    private static string _serverUri;
    private static string _dbName;

    public static event Action OnConnected;
    public static event Action<Zone> OnZoneInserted;
    public static event Action<Zone, Zone> OnZoneUpdated;
    public static event Action<Zone> OnZoneDeleted;
    public static event Action<EntityInstance> OnEntityInserted;
    public static event Action<Player> OnPlayerInserted;
    public static event Action<Player, Player> OnPlayerUpdated;
    public static event Action<Player> OnPlayerDeleted;
    public static event Action<TerrainChunk> OnTerrainChunkUpdated;
    public static event Action<CombatLog> OnCombatLogInserted;
    public static event Action<Ability> OnAbilityInserted;
    public static event Action<PlayerCooldown> OnPlayerCooldownInserted;
    public static event Action<PlayerCooldown, PlayerCooldown> OnPlayerCooldownUpdated;
    public static event Action<StatusEffect> OnStatusEffectInserted;
    public static event Action<StatusEffect> OnStatusEffectDeleted;
    public static event Action<Enemy> OnEnemyInserted;
    public static event Action<Enemy, Enemy> OnEnemyUpdated;
    public static event Action<Enemy> OnEnemyDeleted;
    public static event Action<Portal> OnPortalInserted;
    public static event Action<Portal> OnPortalDeleted;
    public static event Action<ulong> OnZoneChanged; // fired with OLD zone id

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
        _serverUri = serverUri;
        _dbName    = databaseName;
        CurrentZoneId = ulong.TryParse(PlayerPrefs.GetString(ZonePrefKey, "1"), out var savedZone) ? savedZone : 1UL;
    }

    private static string TokenPrefKey => Application.isEditor ? "spacetimedb_token_editor" : "spacetimedb_token";

    void Start()
    {
        string savedToken = PlayerPrefs.GetString(TokenPrefKey, null);
        Debug.Log($"[SpacetimeDBManager] Connecting{(string.IsNullOrEmpty(savedToken) ? " (new identity)" : " (resuming identity)")}...");
        Conn = DbConnection.Builder()
            .WithUri(serverUri)
            .WithDatabaseName(databaseName)
            .WithToken(savedToken)
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
        PlayerPrefs.SetString(TokenPrefKey, token);
        PlayerPrefs.Save();
        Debug.Log($"[SpacetimeDBManager] Connected. Identity: {identity}. Zone: {CurrentZoneId}");
        LocalIdentity = identity;
        conn.SubscriptionBuilder()
            .OnApplied(OnSubscriptionApplied)
            .Subscribe(new[]
            {
                // Light tables — unfiltered
                "SELECT * FROM player",
                "SELECT * FROM zone",
                "SELECT * FROM ability",
                "SELECT * FROM player_cooldown",
                "SELECT * FROM status_effect",
                "SELECT * FROM combat_log",
                "SELECT * FROM enemy_def",
                // Heavy tables — filtered to current zone
                $"SELECT * FROM terrain_chunk WHERE zone_id = {CurrentZoneId}",
                $"SELECT * FROM entity_instance WHERE zone_id = {CurrentZoneId}",
                $"SELECT * FROM enemy WHERE zone_id = {CurrentZoneId}",
                $"SELECT * FROM portal WHERE source_zone_id = {CurrentZoneId}",
                $"SELECT * FROM portal WHERE dest_zone_id = {CurrentZoneId}",
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

        Conn.Db.CombatLog.OnInsert += (eventCtx, log) => OnCombatLogInserted?.Invoke(log);
        Conn.Db.Ability.OnInsert += (eventCtx, ability) => OnAbilityInserted?.Invoke(ability);
        Conn.Db.PlayerCooldown.OnInsert += (eventCtx, cd) => OnPlayerCooldownInserted?.Invoke(cd);
        Conn.Db.PlayerCooldown.OnUpdate += (eventCtx, oldCd, newCd) => OnPlayerCooldownUpdated?.Invoke(oldCd, newCd);
        Conn.Db.StatusEffect.OnInsert += (eventCtx, effect) => OnStatusEffectInserted?.Invoke(effect);
        Conn.Db.StatusEffect.OnDelete += (eventCtx, effect) => OnStatusEffectDeleted?.Invoke(effect);
        Conn.Db.Enemy.OnInsert += (eventCtx, enemy) => OnEnemyInserted?.Invoke(enemy);
        Conn.Db.Enemy.OnUpdate += (eventCtx, oldEnemy, newEnemy) => OnEnemyUpdated?.Invoke(oldEnemy, newEnemy);
        Conn.Db.Enemy.OnDelete += (eventCtx, enemy) => OnEnemyDeleted?.Invoke(enemy);

        Conn.Db.Portal.OnInsert += (eventCtx, portal) => OnPortalInserted?.Invoke(portal);
        Conn.Db.Portal.OnDelete += (eventCtx, portal) => OnPortalDeleted?.Invoke(portal);

        IsSubscribed = true;
        OnConnected?.Invoke();
    }

    void OnConnectError(Exception e)
    {
        Debug.LogError($"[SpacetimeDBManager] Connection failed: {e.Message}");
    }

    void OnDisconnect(DbConnection conn, Exception e)
    {
        // Ignore stale close events from a previous connection that was replaced during zone transfer.
        if (conn != Conn) return;
        LocalIdentity = default;
        IsSubscribed = false;
        if (e != null)
            Debug.LogWarning($"[SpacetimeDBManager] Disconnected with error: {e.Message}");
        else
            Debug.Log("[SpacetimeDBManager] Disconnected cleanly");
    }

    /// <summary>
    /// Called by ZoneTransferManager after transfer animation + server confirmation.
    /// Flips the rendering gate and fires OnZoneChanged so managers can purge old GOs.
    /// </summary>
    public static void SetCurrentZoneId(ulong newZoneId)
    {
        ulong oldZoneId = CurrentZoneId;
        CurrentZoneId = newZoneId;
        PlayerPrefs.SetString(ZonePrefKey, newZoneId.ToString());
        PlayerPrefs.Save();
        Debug.Log($"[SpacetimeDBManager] Zone changed: {oldZoneId} -> {newZoneId}");
        OnZoneChanged?.Invoke(oldZoneId);
    }

    /// <summary>
    /// Call from ZoneTransferManager via StartCoroutine. Rebuilds the connection
    /// with zone-filtered queries for the current zone only, flushing old zone data.
    /// </summary>
    public static IEnumerator ReconnectForNewZone()
    {
        yield return new WaitForSeconds(2f);
        Debug.Log($"[SpacetimeDBManager] Background reconnect for zone {CurrentZoneId}");
        IsSubscribed = false;
        string savedToken = PlayerPrefs.GetString(TokenPrefKey, null);
        Conn = DbConnection.Builder()
            .WithUri(_serverUri)
            .WithDatabaseName(_dbName)
            .WithToken(savedToken)
            .OnConnect(Instance.OnConnect)
            .OnConnectError(Instance.OnConnectError)
            .OnDisconnect(Instance.OnDisconnect)
            .Build();
        // New OnConnect fires → re-subscribes with updated CurrentZoneId
    }
}
