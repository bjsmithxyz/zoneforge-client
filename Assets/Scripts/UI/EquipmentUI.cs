using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB.Types;

/// <summary>
/// Equipment character sheet. Toggle with C key.
/// Shows three equipment slots (Weapon, Armor, Accessory).
/// Click a filled slot -> calls unequip_item reducer.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class EquipmentUI : MonoBehaviour
{
    private UIDocument    _doc;
    private VisualElement _root;
    private VisualElement _panel;
    private VisualElement _weaponSlot;
    private VisualElement _armorSlot;
    private VisualElement _accessorySlot;
    private Label         _weaponLabel;
    private Label         _armorLabel;
    private Label         _accessoryLabel;

    void Awake()
    {
        _doc  = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;
        _root.pickingMode = PickingMode.Ignore;
        BuildUI();
        _panel.style.display = DisplayStyle.None;
    }

    void OnEnable()
    {
        InventoryManager.OnEquipmentChanged += Refresh;
        InventoryManager.OnInventoryChanged += Refresh; // item defs load after
    }

    void OnDisable()
    {
        InventoryManager.OnEquipmentChanged -= Refresh;
        InventoryManager.OnInventoryChanged -= Refresh;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
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
        _panel.style.right            = 240;  // left of inventory panel
        _panel.style.top              = 10;
        _panel.style.width            = 180;
        _panel.style.backgroundColor  = new StyleColor(new Color(0.1f, 0.1f, 0.15f, 0.92f));
        _panel.style.borderTopLeftRadius     = 6;
        _panel.style.borderTopRightRadius    = 6;
        _panel.style.borderBottomLeftRadius  = 6;
        _panel.style.borderBottomRightRadius = 6;
        _panel.style.paddingTop    = 8;
        _panel.style.paddingBottom = 8;
        _panel.style.paddingLeft   = 8;
        _panel.style.paddingRight  = 8;

        var title = new Label("Equipment");
        title.style.color                   = Color.white;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom            = 8;
        _panel.Add(title);

        (_weaponSlot,    _weaponLabel)    = AddSlot("Weapon");
        (_armorSlot,     _armorLabel)     = AddSlot("Armor");
        (_accessorySlot, _accessoryLabel) = AddSlot("Accessory");

        _weaponSlot.RegisterCallback<ClickEvent>(_ => TryUnequip("weapon"));
        _armorSlot.RegisterCallback<ClickEvent>(_ => TryUnequip("armor"));
        _accessorySlot.RegisterCallback<ClickEvent>(_ => TryUnequip("accessory"));

        _root.Add(_panel);
    }

    (VisualElement slot, Label label) AddSlot(string slotName)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.marginBottom  = 6;

        var headerLabel = new Label(slotName);
        headerLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        headerLabel.style.width = 70;

        var slot = new VisualElement();
        slot.style.width                     = 44;
        slot.style.height                    = 44;
        slot.style.borderTopLeftRadius       = 3;
        slot.style.borderTopRightRadius      = 3;
        slot.style.borderBottomLeftRadius    = 3;
        slot.style.borderBottomRightRadius   = 3;
        slot.style.backgroundColor           = new StyleColor(new Color(0.2f, 0.2f, 0.25f));
        slot.style.justifyContent            = Justify.Center;
        slot.style.alignItems                = Align.Center;

        var label = new Label("—");
        label.style.color    = Color.white;
        label.style.fontSize = 10;
        label.pickingMode    = PickingMode.Ignore;
        slot.Add(label);

        row.Add(headerLabel);
        row.Add(slot);
        _panel.Add(row);
        return (slot, label);
    }

    void Refresh()
    {
        if (InventoryManager.Instance == null) return;
        var eq = InventoryManager.Instance.Equipment;
        RefreshSlot(_weaponSlot,    _weaponLabel,    eq?.WeaponId);
        RefreshSlot(_armorSlot,     _armorLabel,     eq?.ArmorId);
        RefreshSlot(_accessorySlot, _accessoryLabel, eq?.AccessoryId);
    }

    void RefreshSlot(VisualElement slot, Label label, ulong? itemDefId)
    {
        if (itemDefId == null || itemDefId == 0)
        {
            slot.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.25f));
            label.text = "—";
            return;
        }
        if (InventoryManager.Instance != null &&
            InventoryManager.Instance.ItemDefs.TryGetValue(itemDefId.Value, out var def))
        {
            slot.style.backgroundColor = new StyleColor(GetRarityColor(def.Rarity));
            label.text = def.Name.Length > 10 ? def.Name.Substring(0, 10) + "…" : def.Name;
        }
    }

    void TryUnequip(string slot)
    {
        if (!SpacetimeDBManager.IsSubscribed) return;
        SpacetimeDBManager.Conn.Reducers.UnequipItem(slot);
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
