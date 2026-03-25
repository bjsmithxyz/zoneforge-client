using System.Collections;
using UnityEngine;
using SpacetimeDB.Types;

/// Scene singleton. Executes the zone transfer sequence:
/// 1. Call enter_zone reducer  2. Play Ripple Warp animation
/// 3. Wait for player row zone_id update  4. Flip CurrentZoneId gate
/// 5. Destroy old zone GOs  6. Background reconnect (2s delay)
public class ZoneTransferManager : MonoBehaviour
{
    public static ZoneTransferManager Instance { get; private set; }

    public bool IsTransferring { get; private set; }

    private ulong  _pendingZoneId;
    private bool   _transferConfirmed;
    private bool   _animationComplete;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// Called by PortalManager when the local player enters a portal trigger radius.
    public void BeginTransfer(Portal portal)
    {
        if (IsTransferring) return;
        var localPlayer = SpacetimeDBManager.Conn.Db.Player.Identity.Find(SpacetimeDBManager.LocalIdentity);
        if (localPlayer == null) return;
        bool reverse = portal.DestZoneId == SpacetimeDBManager.CurrentZoneId;
        _pendingZoneId    = reverse ? portal.SourceZoneId : portal.DestZoneId;
        IsTransferring    = true;
        _transferConfirmed = false;
        _animationComplete = false;

        Debug.Log($"[ZoneTransferManager] Transfer to zone {_pendingZoneId}");

        // Step 1: call enter_zone reducer
        SpacetimeDBManager.Conn.Reducers.EnterZone();

        // Step 2: play Ripple Warp
        if (RippleWarpEffect.Instance != null)
            RippleWarpEffect.Instance.Play(OnAnimationComplete);
        else
            OnAnimationComplete(); // fallback if effect not in scene

        // Step 3: listen for player zone update
        SpacetimeDBManager.OnPlayerUpdated += OnPlayerUpdated;
    }

    void OnPlayerUpdated(Player oldPlayer, Player newPlayer)
    {
        if (newPlayer.Identity != SpacetimeDBManager.LocalIdentity) return;
        if (newPlayer.ZoneId == _pendingZoneId)
        {
            _transferConfirmed = true;
            TryCompleteTransfer();
        }
    }

    void OnAnimationComplete()
    {
        _animationComplete = true;
        TryCompleteTransfer();
    }

    void TryCompleteTransfer()
    {
        if (!_transferConfirmed || !_animationComplete) return;
        SpacetimeDBManager.OnPlayerUpdated -= OnPlayerUpdated;
        CompleteTransfer();
    }

    void CompleteTransfer()
    {
        // Steps 4 & 5: flip rendering gate (managers fire OnZoneChanged to purge old GOs)
        SpacetimeDBManager.SetCurrentZoneId(_pendingZoneId);

        // Step 6: background reconnect
        StartCoroutine(SpacetimeDBManager.ReconnectForNewZone());

        IsTransferring = false;
        Debug.Log($"[ZoneTransferManager] Transfer complete. Now in zone {_pendingZoneId}");
    }
}
