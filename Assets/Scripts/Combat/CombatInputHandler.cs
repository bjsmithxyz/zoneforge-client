using System;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Handles combat input:
///   Tab       — cycle target through other living players and enemies
///   1         — Auto-Attack selected target (player → UseAbility, enemy → AttackEnemy)
///   2         — Fireball selected target (player → UseAbility, enemy → AttackEnemy)
///   3         — Heal self
///   R         — Respawn (only when dead)
/// Attach to any persistent GameObject (e.g. the SpacetimeDBManager GO).
/// </summary>
public class CombatInputHandler : MonoBehaviour
{
    // Ability IDs as seeded by init reducer — must match server seed order
    private const ulong AbilityAutoAttack = 1;
    private const ulong AbilityFireball   = 2;
    private const ulong AbilityHeal       = 3;

    private ulong _selectedTargetId;   // 0 = no target
    private bool _targetIsEnemy;       // false = player target, true = enemy target
    private GameObject _selectionRing; // flat cylinder under the target

    void Start()
    {
        // Create the selection ring (flat yellow cylinder, placed under target)
        _selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _selectionRing.name = "SelectionRing";
        _selectionRing.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);
        var rend = _selectionRing.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rend.material.color = Color.yellow;
        // Disable collider so the ring doesn't interfere with physics
        Destroy(_selectionRing.GetComponent<Collider>());
        _selectionRing.SetActive(false);
    }

    void Update()
    {
        if (!SpacetimeDBManager.IsSubscribed) return;

        var localPlayer = GetLocalPlayer();
        if (localPlayer == null) return;

        if (localPlayer.IsDead)
        {
            // Clear target selection when dead so the ring doesn't persist
            _selectedTargetId = 0;
            _targetIsEnemy    = false;
            if (_selectionRing != null)
                _selectionRing.SetActive(false);

            if (Input.GetKeyDown(KeyCode.R))
                SpacetimeDBManager.Conn.Reducers.Respawn();
            // Suppress all other input while dead
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
            CycleTarget();

        if (Input.GetKeyDown(KeyCode.Alpha1))
            TryUseAbility(AbilityAutoAttack, requireTarget: true, localPlayer);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            TryUseAbility(AbilityFireball, requireTarget: true, localPlayer);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            TryUseAbility(AbilityHeal, requireTarget: false, localPlayer);

        // Keep selection ring stuck to target
        UpdateSelectionRing();
    }

    private void CycleTarget()
    {
        // Build a unified candidate list: (id, isEnemy, position for ring)
        var candidates = new System.Collections.Generic.List<(ulong id, bool isEnemy)>();

        foreach (var p in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (p.Identity == SpacetimeDBManager.LocalIdentity) continue;
            if (p.IsDead) continue;
            candidates.Add((p.Id, false));
        }

        foreach (var e in SpacetimeDBManager.Conn.Db.Enemy.Iter())
        {
            if (e.IsDead) continue;
            candidates.Add((e.Id, true));
        }

        if (candidates.Count == 0)
        {
            _selectedTargetId = 0;
            _targetIsEnemy    = false;
            _selectionRing.SetActive(false);
            return;
        }

        // Find current target index in unified list; advance by 1
        int currentIndex = candidates.FindIndex(c => c.id == _selectedTargetId && c.isEnemy == _targetIsEnemy);
        int nextIndex    = (currentIndex + 1) % candidates.Count;

        _selectedTargetId = candidates[nextIndex].id;
        _targetIsEnemy    = candidates[nextIndex].isEnemy;
        _selectionRing.SetActive(true);

        Debug.Log($"[CombatInput] Target: {(_targetIsEnemy ? "enemy" : "player")} id={_selectedTargetId}");
    }

    private void TryUseAbility(ulong abilityId, bool requireTarget, Player localPlayer)
    {
        if (requireTarget && _selectedTargetId == 0)
        {
            Debug.Log("[CombatInput] No target selected");
            return;
        }

        if (IsOnCooldown(abilityId, localPlayer.Id))
        {
            Debug.Log($"[CombatInput] Ability {abilityId} on cooldown");
            return;
        }

        if (!requireTarget)
        {
            // Self-cast (Heal) — always targets local player
            SpacetimeDBManager.Conn.Reducers.UseAbility(abilityId, localPlayer.Id);
            Debug.Log($"[CombatInput] UseAbility({abilityId}, self)");
            return;
        }

        if (_targetIsEnemy)
        {
            SpacetimeDBManager.Conn.Reducers.AttackEnemy(abilityId, _selectedTargetId);
            Debug.Log($"[CombatInput] AttackEnemy({abilityId}, enemy={_selectedTargetId})");
        }
        else
        {
            SpacetimeDBManager.Conn.Reducers.UseAbility(abilityId, _selectedTargetId);
            Debug.Log($"[CombatInput] UseAbility({abilityId}, player={_selectedTargetId})");
        }
    }

    private bool IsOnCooldown(ulong abilityId, ulong playerId)
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var cd in SpacetimeDBManager.Conn.Db.PlayerCooldown.Iter())
        {
            if (cd.PlayerId != playerId || cd.AbilityId != abilityId) continue;
            // ReadyAt is a SpacetimeDB Timestamp — compare microseconds
            long readyUs = (long)cd.ReadyAt.MicrosecondsSinceUnixEpoch;
            long nowUs = nowMs * 1000L;
            return readyUs > nowUs;
        }
        return false;
    }

    private void UpdateSelectionRing()
    {
        if (_selectedTargetId == 0) { _selectionRing.SetActive(false); return; }

        GameObject targetGo = _targetIsEnemy
            ? EnemyManager.Instance?.GetEnemyObject(_selectedTargetId)
            : PlayerManager.Instance?.GetPlayerObject(_selectedTargetId);

        if (targetGo == null)
        {
            _selectedTargetId = 0;
            _targetIsEnemy    = false;
            _selectionRing.SetActive(false);
            return;
        }

        var pos = targetGo.transform.position;
        float surfaceY = TerrainRenderer.GetSurfaceHeight(pos.x, pos.z);
        _selectionRing.transform.position = new Vector3(pos.x, surfaceY + 0.05f, pos.z);
        _selectionRing.SetActive(true);
    }

    private Player GetLocalPlayer()
    {
        if (SpacetimeDBManager.Conn == null) return null;
        foreach (var p in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (p.Identity == SpacetimeDBManager.LocalIdentity)
                return p;
        }
        return null;
    }
}
