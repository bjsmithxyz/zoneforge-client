using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Scene singleton. Caches the local player's inventory, equipment, and nearby
/// item drops. Fires events that InventoryUI and EquipmentUI listen to.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    public readonly Dictionary<ulong, Inventory> Inventory = new();
    public Equipment Equipment;
    public readonly Dictionary<ulong, ItemDefinition> ItemDefs = new();
    public readonly Dictionary<ulong, ItemDrop> ItemDrops = new();

    private ulong _localPlayerId;

    public static event System.Action OnInventoryChanged;
    public static event System.Action OnEquipmentChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SpacetimeDBManager.OnConnected         += OnConnected;
        SpacetimeDBManager.OnZoneChanged       += OnZoneChanged;
        SpacetimeDBManager.OnItemDefInserted   += OnItemDefInserted;
        SpacetimeDBManager.OnItemDefDeleted    += OnItemDefDeleted;
        SpacetimeDBManager.OnInventoryInserted += OnInventoryRowInserted;
        SpacetimeDBManager.OnInventoryUpdated  += OnInventoryRowUpdated;
        SpacetimeDBManager.OnInventoryDeleted  += OnInventoryRowDeleted;
        SpacetimeDBManager.OnEquipmentInserted += OnEquipmentRowInserted;
        SpacetimeDBManager.OnEquipmentUpdated  += OnEquipmentRowUpdated;
        SpacetimeDBManager.OnEquipmentDeleted  += OnEquipmentRowDeleted;
        SpacetimeDBManager.OnItemDropInserted  += OnItemDropInserted;
        SpacetimeDBManager.OnItemDropDeleted   += OnItemDropDeleted;
    }

    void OnDestroy()
    {
        SpacetimeDBManager.OnConnected         -= OnConnected;
        SpacetimeDBManager.OnZoneChanged       -= OnZoneChanged;
        SpacetimeDBManager.OnItemDefInserted   -= OnItemDefInserted;
        SpacetimeDBManager.OnItemDefDeleted    -= OnItemDefDeleted;
        SpacetimeDBManager.OnInventoryInserted -= OnInventoryRowInserted;
        SpacetimeDBManager.OnInventoryUpdated  -= OnInventoryRowUpdated;
        SpacetimeDBManager.OnInventoryDeleted  -= OnInventoryRowDeleted;
        SpacetimeDBManager.OnEquipmentInserted -= OnEquipmentRowInserted;
        SpacetimeDBManager.OnEquipmentUpdated  -= OnEquipmentRowUpdated;
        SpacetimeDBManager.OnEquipmentDeleted  -= OnEquipmentRowDeleted;
        SpacetimeDBManager.OnItemDropInserted  -= OnItemDropInserted;
        SpacetimeDBManager.OnItemDropDeleted   -= OnItemDropDeleted;
    }

    void OnConnected()
    {
        foreach (var def in SpacetimeDBManager.Conn.Db.ItemDef.Iter())
            ItemDefs[def.Id] = def;

        _localPlayerId = LookupLocalPlayerId();
        if (_localPlayerId == 0)
        {
            Debug.Log("[InventoryManager] Local player row not yet present — deferring inventory/equipment backfill until player insert arrives.");
            SpacetimeDBManager.OnPlayerInserted += OnPlayerInsertedDeferred;
        }
        else
        {
            BackfillLocalPlayerState();
        }

        foreach (var drop in SpacetimeDBManager.Conn.Db.ItemDrop.Iter())
            ItemDrops[drop.Id] = drop;

        OnInventoryChanged?.Invoke();
        OnEquipmentChanged?.Invoke();
    }

    void OnPlayerInsertedDeferred(Player player)
    {
        if (player.Identity != SpacetimeDBManager.LocalIdentity) return;
        _localPlayerId = player.Id;
        BackfillLocalPlayerState();
        SpacetimeDBManager.OnPlayerInserted -= OnPlayerInsertedDeferred;
        OnInventoryChanged?.Invoke();
        OnEquipmentChanged?.Invoke();
    }

    void BackfillLocalPlayerState()
    {
        foreach (var inv in SpacetimeDBManager.Conn.Db.Inventory.Iter())
            if (inv.PlayerId == _localPlayerId) Inventory[inv.Id] = inv;

        foreach (var eq in SpacetimeDBManager.Conn.Db.Equipment.Iter())
            if (eq.PlayerId == _localPlayerId) { Equipment = eq; break; }
    }

    static ulong LookupLocalPlayerId()
    {
        foreach (var p in SpacetimeDBManager.Conn.Db.Player.Iter())
            if (p.Identity == SpacetimeDBManager.LocalIdentity) return p.Id;
        return 0;
    }

    void OnZoneChanged(ulong _oldZoneId)
    {
        ItemDrops.Clear();
    }

    void OnItemDefInserted(ItemDefinition def) => ItemDefs[def.Id] = def;
    void OnItemDefDeleted(ItemDefinition def)  => ItemDefs.Remove(def.Id);

    void OnInventoryRowInserted(Inventory row)
    {
        if (_localPlayerId == 0 || row.PlayerId != _localPlayerId) return;
        Inventory[row.Id] = row;
        OnInventoryChanged?.Invoke();
    }

    void OnInventoryRowUpdated(Inventory _oldRow, Inventory newRow)
    {
        if (_localPlayerId == 0 || newRow.PlayerId != _localPlayerId) return;
        Inventory[newRow.Id] = newRow;
        OnInventoryChanged?.Invoke();
    }

    void OnInventoryRowDeleted(Inventory row)
    {
        if (_localPlayerId == 0 || row.PlayerId != _localPlayerId) return;
        Inventory.Remove(row.Id);
        OnInventoryChanged?.Invoke();
    }

    void OnEquipmentRowInserted(Equipment eq)
    {
        if (_localPlayerId == 0 || eq.PlayerId != _localPlayerId) return;
        Equipment = eq;
        OnEquipmentChanged?.Invoke();
    }

    void OnEquipmentRowUpdated(Equipment _oldEq, Equipment newEq)
    {
        if (_localPlayerId == 0 || newEq.PlayerId != _localPlayerId) return;
        Equipment = newEq;
        OnEquipmentChanged?.Invoke();
    }

    void OnEquipmentRowDeleted(Equipment eq)
    {
        if (_localPlayerId != 0 && eq.PlayerId != _localPlayerId) return;
        Equipment = null;
        OnEquipmentChanged?.Invoke();
    }

    void OnItemDropInserted(ItemDrop drop)
    {
        ItemDrops[drop.Id] = drop;
    }

    void OnItemDropDeleted(ItemDrop drop)
    {
        ItemDrops.Remove(drop.Id);
    }
}
