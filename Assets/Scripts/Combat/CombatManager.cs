using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

    [Header("Combat Feel")]
    [Tooltip("Time.timeScale during a hit-pause. 0.05 freezes briefly without total stop.")]
    [SerializeField] private float _hitPauseTimeScale = 0.05f;
    [Tooltip("How long the hit-pause lasts in real seconds.")]
    [SerializeField] private float _hitPauseDuration = 0.06f;
    [Tooltip("Damage threshold (>=) that triggers a hit-pause. Avoids freezing on every DoT tick.")]
    [SerializeField] private int _hitPauseMinDamage = 10;

    private bool _hitPauseRunning;
    private Image _deathFadeImage;

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

        BuildDeathFadeOverlay();
    }

    /// <summary>Procedural full-screen black Image used for the death cam fade.</summary>
    private void BuildDeathFadeOverlay()
    {
        var go = new GameObject("DeathFadeCanvas");
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above HUD; respawn overlay should sit above this if assigned
        go.AddComponent<CanvasScaler>();

        var imgGo = new GameObject("Fade");
        imgGo.transform.SetParent(go.transform, false);
        var rect = imgGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        _deathFadeImage = imgGo.AddComponent<Image>();
        _deathFadeImage.color = new Color(0f, 0f, 0f, 0f);
        _deathFadeImage.raycastTarget = false; // never block clicks
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

        // Look up the ability via cache — ability_id 0 means DoT tick (no ability row)
        Ability ability = null;
        if (log.AbilityId != 0)
            LookupCache.Abilities.TryGetValue(log.AbilityId, out ability);

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

        // Hit-pause: brief Time.timeScale freeze for punchy impact. Skip on heals
        // and on small damage values (DoT ticks shouldn't stutter the world).
        if (log.DamageDealt >= _hitPauseMinDamage)
            StartCoroutine(HitPause());

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

        if (justDied)
        {
            if (_respawnOverlay != null) _respawnOverlay.SetActive(true);
            StartCoroutine(FadeDeathOverlay(0f, 0.55f, 0.6f));
        }
        else if (justRespawned)
        {
            if (_respawnOverlay != null) _respawnOverlay.SetActive(false);
            StartCoroutine(FadeDeathOverlay(_deathFadeImage != null ? _deathFadeImage.color.a : 0f, 0f, 0.4f));
        }
    }

    /// <summary>
    /// Triggered by OnCombatLogInserted for damage above the hit-pause threshold.
    /// Briefly drops Time.timeScale for a punchy impact feel.
    /// </summary>
    private IEnumerator HitPause()
    {
        if (_hitPauseRunning) yield break;
        _hitPauseRunning = true;
        float prev = Time.timeScale;
        Time.timeScale = _hitPauseTimeScale;
        // Real-time wait so the freeze duration is independent of our own timescale.
        yield return new WaitForSecondsRealtime(_hitPauseDuration);
        Time.timeScale = prev;
        _hitPauseRunning = false;
    }

    private IEnumerator FadeDeathOverlay(float fromA, float toA, float duration)
    {
        if (_deathFadeImage == null) yield break;
        float elapsed = 0f;
        var c = _deathFadeImage.color;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(fromA, toA, Mathf.Clamp01(elapsed / duration));
            _deathFadeImage.color = c;
            yield return null;
        }
        c.a = toA;
        _deathFadeImage.color = c;
    }
}
