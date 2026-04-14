using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Each frame finds the nearest ItemDrop within 1.5 units of the local player.
/// Shows an on-screen "F — Pick Up [item name]" prompt.
/// Pressing F calls the PickupItem reducer.
/// </summary>
public class ItemPickupManager : MonoBehaviour
{
    public static ItemPickupManager Instance { get; private set; }

    private const float PickupRadius = 1.5f;

    private ulong  _nearestDropId;
    private string _nearestDropName;
    private bool   _promptVisible;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        _promptVisible = false;
        _nearestDropId = 0;

        if (!SpacetimeDBManager.IsSubscribed) return;

        // Find local player position
        Vector3 playerPos = Vector3.zero;
        bool foundPlayer = false;
        foreach (var p in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (p.Identity == SpacetimeDBManager.LocalIdentity)
            {
                playerPos   = new Vector3(p.PositionX, 0, p.PositionY);
                foundPlayer = true;
                break;
            }
        }
        if (!foundPlayer) return;

        float bestDistSq = PickupRadius * PickupRadius;
        foreach (var drop in SpacetimeDBManager.Conn.Db.ItemDrop.Iter())
        {
            var dropPos = new Vector3(drop.PosX, 0, drop.PosY);
            float distSq = (playerPos - dropPos).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq     = distSq;
                _nearestDropId = drop.Id;

                string itemName = "Item";
                foreach (var def in SpacetimeDBManager.Conn.Db.ItemDef.Iter())
                {
                    if (def.Id == drop.ItemDefId) { itemName = def.Name; break; }
                }
                _nearestDropName = $"{itemName} x{drop.Quantity}";
                _promptVisible   = true;
            }
        }

        if (_promptVisible && Input.GetKeyDown(KeyCode.F))
        {
            SpacetimeDBManager.Conn.Reducers.PickupItem(_nearestDropId);
            _promptVisible = false;
        }
    }

    void OnGUI()
    {
        if (!_promptVisible) return;
        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter,
        };
        style.normal.textColor = Color.white;
        float w = 280, h = 32;
        GUI.Box(new Rect(Screen.width / 2f - w / 2f, 24, w, h),
            $"F — Pick Up {_nearestDropName}", style);
    }
}
