using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Singleton. Subscribes to CombatLog inserts and spawns VFX. Also handles
/// the local player's death/respawn overlay.
/// </summary>
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("Respawn Overlay")]
    [Tooltip("Assign a UI Canvas Text/Image that reads 'Press R to respawn'. Enable/disable in Inspector.")]
    [SerializeField] private GameObject _respawnOverlay;

    // Cache of player world positions — updated by PlayerManager via RegisterPlayerPosition
    // Key: player id, Value: world position
    private readonly Dictionary<ulong, Vector3> _playerPositions = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_respawnOverlay != null)
            _respawnOverlay.SetActive(false);
    }

    void OnEnable()
    {
        SpacetimeDBManager.OnCombatLogInserted += OnCombatLogInserted;
        SpacetimeDBManager.OnPlayerUpdated += OnPlayerUpdated;
    }

    void OnDisable()
    {
        SpacetimeDBManager.OnCombatLogInserted -= OnCombatLogInserted;
        SpacetimeDBManager.OnPlayerUpdated -= OnPlayerUpdated;
    }

    /// <summary>Called by PlayerManager after spawning or moving a player capsule.</summary>
    public void RegisterPlayerPosition(ulong playerId, Vector3 worldPos)
    {
        _playerPositions[playerId] = worldPos;
    }

    private void OnCombatLogInserted(CombatLog log)
    {
        // Look up the ability — ability_id 0 means DoT tick (no ability row)
        Ability ability = null;
        if (log.AbilityId != 0)
        {
            foreach (var a in SpacetimeDBManager.Conn.Db.Ability.Iter())
            {
                if (a.Id == log.AbilityId) { ability = a; break; }
            }
        }

        // Determine positions
        _playerPositions.TryGetValue(log.AttackerId, out var attackerPos);
        _playerPositions.TryGetValue(log.TargetId, out var targetPos);

        if (ability != null && ability.AbilityType == AbilityType.Projectile)
        {
            // Spawn projectile from attacker toward target
            var go = ZoneForgePoolManager.Instance?.Get("projectile_fireball");
            if (go != null)
            {
                var proj = go.GetComponent<PooledProjectile>();
                if (proj != null)
                    proj.Launch(attackerPos + Vector3.up, targetPos + Vector3.up);
            }
        }
        else
        {
            // MeleeAttack, SelfCast, or DoT tick — instant impact VFX at target
            // Use positioned overload to avoid one-frame wrong-position flash
            ZoneForgePoolManager.Instance?.Get("vfx_impact_generic", targetPos + Vector3.up);
        }
    }

    private void OnPlayerUpdated(Player oldPlayer, Player newPlayer)
    {
        // Track position for VFX targeting
        _playerPositions[newPlayer.Id] = new Vector3(newPlayer.PositionX, 1f, newPlayer.PositionY);

        // Local player death/respawn overlay
        if (newPlayer.Identity != SpacetimeDBManager.LocalIdentity) return;

        bool justDied = !oldPlayer.IsDead && newPlayer.IsDead;
        bool justRespawned = oldPlayer.IsDead && !newPlayer.IsDead;

        if (justDied && _respawnOverlay != null)
            _respawnOverlay.SetActive(true);
        else if (justRespawned && _respawnOverlay != null)
            _respawnOverlay.SetActive(false);
    }
}
