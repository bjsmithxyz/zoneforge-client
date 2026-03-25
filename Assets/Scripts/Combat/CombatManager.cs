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

    // Cache of enemy world positions — updated by EnemyManager via RegisterEnemyPosition
    // Key: enemy id, Value: world position
    private readonly Dictionary<ulong, Vector3> _enemyPositions = new();

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

    /// <summary>Called by EnemyManager after spawning or moving an enemy capsule.</summary>
    public void RegisterEnemyPosition(ulong enemyId, Vector3 worldPos)
    {
        _enemyPositions[enemyId] = worldPos;
    }

    private void OnCombatLogInserted(CombatLog log)
    {
        if (SpacetimeDBManager.Conn == null) return;

        // Look up the ability — ability_id 0 means DoT tick (no ability row)
        Ability ability = null;
        if (log.AbilityId != 0)
        {
            foreach (var a in SpacetimeDBManager.Conn.Db.Ability.Iter())
            {
                if (a.Id == log.AbilityId) { ability = a; break; }
            }
        }

        // Determine attacker position — check enemies first, then players
        Vector3 attackerPos = default;
        if (!_enemyPositions.TryGetValue(log.AttackerId, out attackerPos) &&
            !_playerPositions.TryGetValue(log.AttackerId, out attackerPos))
            Debug.LogWarning($"[CombatManager] No position for attacker {log.AttackerId}");

        // Determine target position — check enemies first, then players
        Vector3 targetPos = default;
        if (!_enemyPositions.TryGetValue(log.TargetId, out targetPos) &&
            !_playerPositions.TryGetValue(log.TargetId, out targetPos))
            Debug.LogWarning($"[CombatManager] No position for target {log.TargetId}");

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

        // Floating damage / heal number above target
        if (log.DamageDealt != 0)
        {
            bool isHeal   = log.DamageDealt < 0;
            string label  = isHeal ? $"+{-log.DamageDealt}" : $"{log.DamageDealt}";
            Color  color  = isHeal
                ? new Color(0.25f, 1f, 0.35f)          // green for heals
                : new Color(1f,    0.25f, 0.2f);        // red for damage
            // Slight random X jitter so stacked hits don't overlap exactly
            var jitter = new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0f, 0f);
            FloatingTextPopup.Show(targetPos + Vector3.up * 2.5f + jitter, label, color);
        }
    }

    private void OnPlayerUpdated(Player oldPlayer, Player newPlayer)
    {
        if (SpacetimeDBManager.Conn == null) return;

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
