using System;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Handles combat input:
///   Tab       — cycle target through other living players
///   1         — Auto-Attack selected target
///   2         — Fireball selected target
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
    private GameObject _selectionRing; // flat cylinder under the target

    void Start()
    {
        // Create the selection ring (flat yellow cylinder, placed under target)
        _selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _selectionRing.name = "SelectionRing";
        _selectionRing.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);
        var rend = _selectionRing.GetComponent<Renderer>();
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
        var candidates = new List<Player>();
        foreach (var p in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (p.Identity == SpacetimeDBManager.LocalIdentity) continue;
            if (p.IsDead) continue;
            candidates.Add(p);
        }

        if (candidates.Count == 0)
        {
            _selectedTargetId = 0;
            _selectionRing.SetActive(false);
            return;
        }

        // Find the index of the current target in the list; advance by 1
        int currentIndex = candidates.FindIndex(p => p.Id == _selectedTargetId);
        int nextIndex = (currentIndex + 1) % candidates.Count;
        _selectedTargetId = candidates[nextIndex].Id;
        _selectionRing.SetActive(true);
        Debug.Log($"[CombatInput] Target selected: player {_selectedTargetId}");
    }

    private void TryUseAbility(ulong abilityId, bool requireTarget, Player localPlayer)
    {
        ulong targetId;
        if (requireTarget)
        {
            if (_selectedTargetId == 0)
            {
                Debug.Log("[CombatInput] No target selected");
                return;
            }
            targetId = _selectedTargetId;
        }
        else
        {
            // Self-cast — use local player's id
            targetId = localPlayer.Id;
        }

        // Client-side cooldown check (server also validates, this prevents spamming)
        if (IsOnCooldown(abilityId, localPlayer.Id))
        {
            Debug.Log($"[CombatInput] Ability {abilityId} on cooldown");
            return;
        }

        SpacetimeDBManager.Conn.Reducers.UseAbility(abilityId, targetId);
        Debug.Log($"[CombatInput] UseAbility({abilityId}, target={targetId})");
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

        // Find target GO via PlayerManager
        if (PlayerManager.Instance == null) return;
        var targetGo = PlayerManager.Instance.GetPlayerObject(_selectedTargetId);
        if (targetGo == null)
        {
            _selectedTargetId = 0;
            _selectionRing.SetActive(false);
            return;
        }

        var pos = targetGo.transform.position;
        _selectionRing.transform.position = new Vector3(pos.x, 0.05f, pos.z);
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
