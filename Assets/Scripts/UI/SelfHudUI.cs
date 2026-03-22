using UnityEngine;
using UnityEngine.UI;
using SpacetimeDB.Types;

/// <summary>
/// Bottom-left screen HUD showing the local player's HP and Mana bars.
/// Attach to any persistent GameObject (e.g. SpacetimeDBManager).
/// </summary>
public class SelfHudUI : MonoBehaviour
{
    private Image _hpFill;
    private Image _mpFill;
    private Text  _hpText;
    private Text  _mpText;

    // ─── lifecycle ───────────────────────────────────────────────────────────

    void Awake()
    {
        BuildCanvas();
    }

    void OnEnable()
    {
        SpacetimeDBManager.OnConnected      += OnConnected;
        SpacetimeDBManager.OnPlayerInserted += OnPlayerInserted;
        SpacetimeDBManager.OnPlayerUpdated  += OnPlayerUpdated;
    }

    void OnDisable()
    {
        SpacetimeDBManager.OnConnected      -= OnConnected;
        SpacetimeDBManager.OnPlayerInserted -= OnPlayerInserted;
        SpacetimeDBManager.OnPlayerUpdated  -= OnPlayerUpdated;
    }

    // ─── event handlers ──────────────────────────────────────────────────────

    void OnConnected()
    {
        // Backfill: find local player and set initial bar values
        foreach (var p in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (p.Identity != SpacetimeDBManager.LocalIdentity) continue;
            Refresh(p);
            return;
        }
    }

    void OnPlayerInserted(Player p)
    {
        if (p.Identity == SpacetimeDBManager.LocalIdentity)
            Refresh(p);
    }

    void OnPlayerUpdated(Player _, Player p)
    {
        if (p.Identity == SpacetimeDBManager.LocalIdentity)
            Refresh(p);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    void Refresh(Player p)
    {
        float hpPct = p.MaxHealth > 0 ? Mathf.Clamp01((float)p.Health    / p.MaxHealth) : 1f;
        float mpPct = p.MaxMana   > 0 ? Mathf.Clamp01((float)p.Mana      / p.MaxMana)   : 1f;

        _hpFill.fillAmount = hpPct;
        _mpFill.fillAmount = mpPct;
        _hpText.text       = $"{p.Health} / {p.MaxHealth}";
        _mpText.text       = $"{p.Mana} / {p.MaxMana}";

        // Tint HP bar green → yellow → red based on health percent
        _hpFill.color = Color.Lerp(Color.red, new Color(0.15f, 0.75f, 0.15f), hpPct);
    }

    // ─── canvas construction ─────────────────────────────────────────────────

    void BuildCanvas()
    {
        var canvasGo = new GameObject("SelfHudCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Outer panel — bottom-left, 210 × 64 px
        var panel     = new GameObject("SelfHudPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0f, 0f);
        panelRect.anchorMax        = new Vector2(0f, 0f);
        panelRect.pivot            = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(20f, 20f);
        panelRect.sizeDelta        = new Vector2(210f, 64f);

        var panelBg    = panel.AddComponent<Image>();
        panelBg.color  = new Color(0.06f, 0.06f, 0.06f, 0.82f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding                = new RectOffset(8, 8, 6, 6);
        layout.spacing                = 6f;
        layout.childControlWidth      = true;
        layout.childControlHeight     = false;  // respect child sizeDelta for row height
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;

        (_hpFill, _hpText) = BuildBar(panel.transform, "HP", new Color(0.15f, 0.75f, 0.15f));
        (_mpFill, _mpText) = BuildBar(panel.transform, "MP", new Color(0.2f,  0.4f,  0.9f));

        // Initial placeholder text
        _hpText.text = "-- / --";
        _mpText.text = "-- / --";
    }

    (Image fill, Text valueText) BuildBar(Transform parent, string label, Color fillColor)
    {
        const float RowH = 22f;

        var row     = new GameObject($"{label}Row");
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, RowH);

        // Label (left, fixed width)
        var labelGo   = new GameObject($"{label}Label");
        labelGo.transform.SetParent(row.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin        = new Vector2(0f, 0f);
        labelRect.anchorMax        = new Vector2(0f, 1f);
        labelRect.pivot            = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta        = new Vector2(24f, 0f);
        var labelText  = labelGo.AddComponent<Text>();
        labelText.text      = label;
        labelText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize  = 11;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color     = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;

        // Bar background (dark, next to label)
        var bgGo   = new GameObject($"{label}BG");
        bgGo.transform.SetParent(row.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0f, 0f);
        bgRect.anchorMax        = new Vector2(1f, 1f);
        bgRect.offsetMin        = new Vector2(28f, 2f);
        bgRect.offsetMax        = new Vector2(0f,  -2f);
        var bgImg   = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        // Fill
        var fillGo   = new GameObject($"{label}Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);
        var fillImg   = fillGo.AddComponent<Image>();
        fillImg.color        = fillColor;
        fillImg.type         = Image.Type.Filled;
        fillImg.fillMethod   = Image.FillMethod.Horizontal;
        fillImg.fillOrigin   = 0; // Left
        fillImg.fillAmount   = 1f;

        // Value text (overlaid on bar, right-aligned)
        var valGo   = new GameObject($"{label}Value");
        valGo.transform.SetParent(bgGo.transform, false);
        var valRect = valGo.AddComponent<RectTransform>();
        valRect.anchorMin  = Vector2.zero;
        valRect.anchorMax  = Vector2.one;
        valRect.offsetMin  = new Vector2(2f, 0f);
        valRect.offsetMax  = new Vector2(-2f, 0f);
        var valText  = valGo.AddComponent<Text>();
        valText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valText.fontSize  = 10;
        valText.color     = Color.white;
        valText.alignment = TextAnchor.MiddleRight;

        return (fillImg, valText);
    }
}
