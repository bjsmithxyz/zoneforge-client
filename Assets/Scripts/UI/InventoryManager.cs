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
        SpacetimeDBManager.OnItemDropInserted  -= OnItemDropInserted;
        SpacetimeDBManager.OnItemDropDeleted   -= OnItemDropDeleted;
    }

    void OnConnected()
    {
        foreach (var def in SpacetimeDBManager.Conn.Db.ItemDef.Iter())
            ItemDefs[def.Id] = def;

        var localPlayerId = GetLocalPlayerId();
        if (localPlayerId == 0) return;

        foreach (var inv in SpacetimeDBManager.Conn.Db.Inventory.Iter())
            if (inv.PlayerId == localPlayerId) Inventory[inv.Id] = inv;

        foreach (var eq in SpacetimeDBManager.Conn.Db.Equipment.Iter())
            if (eq.PlayerId == localPlayerId) { Equipment = eq; break; }

        foreach (var drop in SpacetimeDBManager.Conn.Db.ItemDrop.Iter())
            ItemDrops[drop.Id] = drop;

        OnInventoryChanged?.Invoke();
        OnEquipmentChanged?.Invoke();
    }

    void OnZoneChanged(ulong _oldZoneId)
    {
        ItemDrops.Clear();
    }

    static ulong GetLocalPlayerId()
    {
        foreach (var p in SpacetimeDBManager.Conn.Db.Player.Iter())
            if (p.Identity == SpacetimeDBManager.LocalIdentity) return p.Id;
        return 0;
    }

    void OnItemDefInserted(ItemDefinition def) => ItemDefs[def.Id] = def;
    void OnItemDefDeleted(ItemDefinition def)  => ItemDefs.Remove(def.Id);

    void OnInventoryRowInserted(Inventory row)
    {
        if (row.PlayerId != GetLocalPlayerId()) return;
        Inventory[row.Id] = row;
        OnInventoryChanged?.Invoke();
    }

    void OnInventoryRowUpdated(Inventory oldRow, Inventory newRow)
    {
        if (newRow.PlayerId != GetLocalPlayerId()) return;
        Inventory[newRow.Id] = newRow;
        OnInventoryChanged?.Invoke();
    }

    void OnInventoryRowDeleted(Inventory row)
    {
        if (row.PlayerId != GetLocalPlayerId()) return;
        Inventory.Remove(row.Id);
        OnInventoryChanged?.Invoke();
    }

    void OnEquipmentRowInserted(Equipment eq)
    {
        if (eq.PlayerId != GetLocalPlayerId()) return;
        Equipment = eq;
        OnEquipmentChanged?.Invoke();
    }

    void OnEquipmentRowUpdated(Equipment oldEq, Equipment newEq)
    {
        if (newEq.PlayerId != GetLocalPlayerId()) return;
        Equipment = newEq;
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
