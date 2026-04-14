using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB.Types;

/// <summary>
/// Persistent singleton. Inventory panel. Toggle with I key.
/// 20-slot grid (4 columns × 5 rows) showing the local player's items.
/// Supports drag-and-drop between slots and hover tooltips.
/// Requires a UIDocument on the same GameObject.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    private UIDocument    _doc;
    private VisualElement _root;
    private VisualElement _panel;
    private VisualElement _grid;
    private VisualElement _tooltip;

    private const int SlotCount = 20;
    private readonly VisualElement[] _slots          = new VisualElement[SlotCount];
    private readonly ulong[]         _slotItemDefIds = new ulong[SlotCount];

    private VisualElement _dragGhost;
    private int           _dragSourceSlot = -1;

    private readonly System.Collections.Generic.List<ulong> _localSlotOrder = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _doc  = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;
        _root.pickingMode = PickingMode.Ignore;
        BuildUI();
        _panel.style.display = DisplayStyle.None;
    }

    void OnEnable()
    {
        InventoryManager.OnInventoryChanged += Refresh;
    }

    void OnDisable()
    {
        InventoryManager.OnInventoryChanged -= Refresh;
        CancelDrag();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            TogglePanel();
    }

    void TogglePanel()
    {
        bool visible = _panel.style.display == DisplayStyle.Flex;
        _panel.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        if (!visible) Refresh();
    }

    void BuildUI()
    {
        _panel = new VisualElement();
        _panel.style.position         = Position.Absolute;
        _panel.style.right            = 10;
        _panel.style.top              = 10;
        _panel.style.width            = 220;
        _panel.style.backgroundColor  = new StyleColor(new Color(0.1f, 0.1f, 0.15f, 0.92f));
        _panel.style.borderTopLeftRadius     = 6;
        _panel.style.borderTopRightRadius    = 6;
        _panel.style.borderBottomLeftRadius  = 6;
        _panel.style.borderBottomRightRadius = 6;
        _panel.style.paddingTop    = 8;
        _panel.style.paddingBottom = 8;
        _panel.style.paddingLeft   = 8;
        _panel.style.paddingRight  = 8;

        var title = new Label("Inventory");
        title.style.color                   = Color.white;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom            = 6;
        _panel.Add(title);

        _grid = new VisualElement();
        _grid.style.flexDirection = FlexDirection.Row;
        _grid.style.flexWrap      = Wrap.Wrap;
        _grid.style.width         = 200;
        _panel.Add(_grid);

        for (int i = 0; i < SlotCount; i++)
        {
            var slot = BuildSlot(i);
            _slots[i] = slot;
            _grid.Add(slot);
        }

        _tooltip = new VisualElement();
        _tooltip.style.position                = Position.Absolute;
        _tooltip.style.backgroundColor         = new StyleColor(new Color(0.05f, 0.05f, 0.1f, 0.95f));
        _tooltip.style.borderTopLeftRadius     = 4;
        _tooltip.style.borderTopRightRadius    = 4;
        _tooltip.style.borderBottomLeftRadius  = 4;
        _tooltip.style.borderBottomRightRadius = 4;
        _tooltip.style.paddingTop    = 6;
        _tooltip.style.paddingBottom = 6;
        _tooltip.style.paddingLeft   = 8;
        _tooltip.style.paddingRight  = 8;
        _tooltip.style.width         = 220;
        _tooltip.pickingMode         = PickingMode.Ignore;
        _tooltip.style.display       = DisplayStyle.None;

        _root.Add(_panel);
        _root.Add(_tooltip);
    }

    VisualElement BuildSlot(int index)
    {
        var slot = new VisualElement();
        slot.style.width                     = 44;
        slot.style.height                    = 44;
        slot.style.marginTop                 = 2;
        slot.style.marginBottom              = 2;
        slot.style.marginLeft                = 2;
        slot.style.marginRight               = 2;
        slot.style.borderTopLeftRadius       = 3;
        slot.style.borderTopRightRadius      = 3;
        slot.style.borderBottomLeftRadius    = 3;
        slot.style.borderBottomRightRadius   = 3;
        slot.style.backgroundColor           = new StyleColor(new Color(0.2f, 0.2f, 0.25f, 1f));
        slot.userData = index;

        var qty = new Label("");
        qty.name                 = "qty";
        qty.style.position       = Position.Absolute;
        qty.style.bottom         = 2;
        qty.style.right          = 4;
        qty.style.color          = Color.white;
        qty.style.fontSize       = 11;
        qty.pickingMode          = PickingMode.Ignore;
        slot.Add(qty);

        slot.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(index, slot));
        slot.RegisterCallback<MouseLeaveEvent>(evt => _tooltip.style.display = DisplayStyle.None);
        slot.RegisterCallback<MouseMoveEvent>(evt => PositionTooltip(evt.mousePosition));

        int capturedIndex = index;
        slot.RegisterCallback<PointerDownEvent>(evt => {
            if (_slotItemDefIds[capturedIndex] == 0) return;
            BeginDrag(capturedIndex, evt);
            evt.StopPropagation();
        });

        // Double-click equips
        slot.RegisterCallback<ClickEvent>(evt => {
            if (evt.clickCount == 2 && _slotItemDefIds[capturedIndex] != 0)
            {
                if (SpacetimeDBManager.IsSubscribed)
                    SpacetimeDBManager.Conn.Reducers.EquipItem(_slotItemDefIds[capturedIndex]);
            }
        });

        return slot;
    }

    void Refresh()
    {
        if (InventoryManager.Instance == null) return;

        // Drop inventory ids that no longer exist
        for (int i = _localSlotOrder.Count - 1; i >= 0; i--)
        {
            if (!InventoryManager.Instance.Inventory.ContainsKey(_localSlotOrder[i]))
                _localSlotOrder.RemoveAt(i);
        }

        // Append newly present ids
        foreach (var kv in InventoryManager.Instance.Inventory)
        {
            if (!_localSlotOrder.Contains(kv.Key))
                _localSlotOrder.Add(kv.Key);
        }

        // Clear all slots
        for (int i = 0; i < SlotCount; i++)
        {
            _slotItemDefIds[i] = 0;
            _slots[i].style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.25f, 1f));
            _slots[i].Q<Label>("qty").text = "";
        }

        // Render from stable order
        for (int i = 0; i < _localSlotOrder.Count && i < SlotCount; i++)
        {
            if (!InventoryManager.Instance.Inventory.TryGetValue(_localSlotOrder[i], out var inv)) continue;
            _slotItemDefIds[i] = inv.ItemDefId;
            if (InventoryManager.Instance.ItemDefs.TryGetValue(inv.ItemDefId, out var def))
                _slots[i].style.backgroundColor = new StyleColor(GetRarityColor(def.Rarity));
            _slots[i].Q<Label>("qty").text = inv.Quantity > 1 ? inv.Quantity.ToString() : "";
        }
    }

    // Drag-and-drop

    void BeginDrag(int slotIndex, PointerDownEvent evt)
    {
        if (_dragSourceSlot >= 0) return;  // drag already in progress
        _dragSourceSlot = slotIndex;

        _dragGhost = new VisualElement();
        _dragGhost.style.width           = 40;
        _dragGhost.style.height          = 40;
        _dragGhost.style.position        = Position.Absolute;
        _dragGhost.style.backgroundColor = _slots[slotIndex].style.backgroundColor;
        _dragGhost.style.opacity         = 0.75f;
        _dragGhost.style.left            = evt.position.x - 20;
        _dragGhost.style.top             = evt.position.y - 20;
        _dragGhost.pickingMode           = PickingMode.Ignore;
        _root.Add(_dragGhost);

        _root.CapturePointer(evt.pointerId);
        _root.RegisterCallback<PointerMoveEvent>(OnDragMove);
        _root.RegisterCallback<PointerUpEvent>(OnDragRelease);
    }

    void OnDragMove(PointerMoveEvent evt)
    {
        if (_dragGhost == null) return;
        _dragGhost.style.left = evt.position.x - 20;
        _dragGhost.style.top  = evt.position.y - 20;
    }

    void OnDragRelease(PointerUpEvent evt)
    {
        if (_dragGhost == null) return;
        _root.ReleasePointer(evt.pointerId);
        _root.UnregisterCallback<PointerMoveEvent>(OnDragMove);
        _root.UnregisterCallback<PointerUpEvent>(OnDragRelease);

        int targetSlot = FindSlotIndexAt(evt.position);
        if (targetSlot >= 0 && targetSlot != _dragSourceSlot)
            SwapSlots(_dragSourceSlot, targetSlot);

        _root.Remove(_dragGhost);
        _dragGhost      = null;
        _dragSourceSlot = -1;
    }

    int FindSlotIndexAt(Vector2 screenPos)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            var bounds = _slots[i].worldBound;
            if (bounds.Contains(screenPos)) return i;
        }
        return -1;
    }

    void CancelDrag()
    {
        if (_dragGhost != null && _dragGhost.parent != null)
            _dragGhost.parent.Remove(_dragGhost);
        _dragGhost = null;
        _dragSourceSlot = -1;

        if (_root != null)
        {
            _root.UnregisterCallback<PointerMoveEvent>(OnDragMove);
            _root.UnregisterCallback<PointerUpEvent>(OnDragRelease);
        }
    }

    /// Visual-only slot swap (client-side — no server call).
    void SwapSlots(int a, int b)
    {
        // Extend _localSlotOrder if user swapped into a gap
        while (_localSlotOrder.Count <= System.Math.Max(a, b))
            _localSlotOrder.Add(0);

        (_localSlotOrder[a], _localSlotOrder[b]) = (_localSlotOrder[b], _localSlotOrder[a]);
        Refresh();
    }

    // Tooltip

    void ShowTooltip(int slotIndex, VisualElement slot)
    {
        ulong defId = _slotItemDefIds[slotIndex];
        if (defId == 0) { _tooltip.style.display = DisplayStyle.None; return; }
        if (InventoryManager.Instance == null) { _tooltip.style.display = DisplayStyle.None; return; }
        if (!InventoryManager.Instance.ItemDefs.TryGetValue(defId, out var def))
        { _tooltip.style.display = DisplayStyle.None; return; }

        // Recreate labels each show — post-construction .text updates don't
        // always trigger a repaint in runtime UIToolkit.
        _tooltip.Clear();

        string statLine = BuildStatLine(def);
        string descText = string.IsNullOrEmpty(def.Description) ? "—" : def.Description;

        var name = new Label($"{def.Name}  [{def.Rarity}]");
        name.style.color                   = Color.white;
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        name.style.fontSize                = 13;
        name.style.whiteSpace              = WhiteSpace.Normal;
        _tooltip.Add(name);

        var desc = new Label(descText);
        desc.style.color      = new Color(0.8f, 0.8f, 0.8f);
        desc.style.fontSize   = 11;
        desc.style.whiteSpace = WhiteSpace.Normal;
        desc.style.marginTop  = 2;
        _tooltip.Add(desc);

        if (!string.IsNullOrWhiteSpace(statLine))
        {
            var stats = new Label(statLine);
            stats.style.color      = new Color(0.5f, 0.9f, 0.5f);
            stats.style.fontSize   = 11;
            stats.style.whiteSpace = WhiteSpace.Normal;
            stats.style.marginTop  = 2;
            _tooltip.Add(stats);
        }

        _tooltip.style.display = DisplayStyle.Flex;

        // Position LEFT of slot (panel is anchored to right edge of screen,
        // so spawning right of slot would go off-screen).
        var sb = slot.worldBound;
        _tooltip.style.left = Mathf.Max(4f, sb.xMin - 228);
        _tooltip.style.top  = sb.yMin;
        _tooltip.BringToFront();
    }

    void PositionTooltip(Vector2 mousePos)
    {
        if (_tooltip.style.display != DisplayStyle.Flex) return;
        _tooltip.style.left = mousePos.x + 12;
        _tooltip.style.top  = mousePos.y + 4;
    }

    string BuildStatLine(ItemDefinition def)
    {
        var parts = new System.Text.StringBuilder();
        if (def.DamageBonus != 0) parts.Append($"+{def.DamageBonus} DMG  ");
        if (def.ArmorBonus  != 0) parts.Append($"+{def.ArmorBonus} ARM  ");
        if (def.Healing     != 0) parts.Append($"+{def.Healing} HP  ");
        if (def.Value       > 0)  parts.Append($"{def.Value}g");
        return parts.ToString();
    }

    static Color GetRarityColor(Rarity rarity) => rarity switch
    {
        Rarity.Common   => new Color(0.35f, 0.35f, 0.4f),
        Rarity.Uncommon => new Color(0.1f,  0.55f, 0.1f),
        Rarity.Rare     => new Color(0.1f,  0.3f,  0.8f),
        Rarity.Epic     => new Color(0.6f,  0.1f,  0.8f),
        _               => new Color(0.35f, 0.35f, 0.4f),
    };
}
