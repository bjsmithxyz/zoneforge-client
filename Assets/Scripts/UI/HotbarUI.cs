using System;
using UnityEngine;
using UnityEngine.UI;
using SpacetimeDB.Types;

/// <summary>
/// Bottom-center HUD: 5 ability slots with WoW-style radial cooldown indicators.
/// Attach to any persistent GameObject (e.g. SpacetimeDBManager).
/// </summary>
public class HotbarUI : MonoBehaviour
{
    // Ability IDs match server seed order (0 = empty slot)
    private static readonly ulong[] SlotAbilityIds = { 1, 2, 3, 0, 0 };
    private static readonly string[] SlotKeys      = { "1", "2", "3", "4", "5" };

    private class SlotData
    {
        public ulong  AbilityId;
        public ulong  CooldownTotalMs;
        public Image  CooldownOverlay;
        public Text   CooldownText;
        public Text   NameLabel;
    }

    private readonly SlotData[] _slots = new SlotData[5];
    private ulong _localPlayerId;

    // ─── lifecycle ───────────────────────────────────────────────────────────

    void Awake()
    {
        BuildCanvas();
    }

    void OnEnable()
    {
        SpacetimeDBManager.OnConnected          += OnConnected;
        SpacetimeDBManager.OnAbilityInserted    += OnAbilityRow;
        SpacetimeDBManager.OnPlayerInserted     += OnPlayerInserted;
        SpacetimeDBManager.OnPlayerUpdated      += OnPlayerUpdated;
    }

    void OnDisable()
    {
        SpacetimeDBManager.OnConnected          -= OnConnected;
        SpacetimeDBManager.OnAbilityInserted    -= OnAbilityRow;
        SpacetimeDBManager.OnPlayerInserted     -= OnPlayerInserted;
        SpacetimeDBManager.OnPlayerUpdated      -= OnPlayerUpdated;
    }

    // ─── event handlers ──────────────────────────────────────────────────────

    void OnConnected()
    {
        // Backfill: initial Ability rows don't fire OnInsert (arrives before callback registration)
        foreach (var a in SpacetimeDBManager.Conn.Db.Ability.Iter())
            OnAbilityRow(a);

        ResolveLocalPlayerId();
    }

    void OnAbilityRow(Ability ability)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].AbilityId != ability.Id) continue;
            _slots[i].NameLabel.text    = ability.Name;
            _slots[i].CooldownTotalMs   = ability.CooldownMs;
        }
    }

    void OnPlayerInserted(Player p)
    {
        if (_localPlayerId == 0 && p.Identity == SpacetimeDBManager.LocalIdentity)
            _localPlayerId = p.Id;
    }

    void OnPlayerUpdated(Player _, Player p)
    {
        if (p.Identity == SpacetimeDBManager.LocalIdentity)
            _localPlayerId = p.Id;
    }

    // ─── update ──────────────────────────────────────────────────────────────

    void Update()
    {
        if (!SpacetimeDBManager.IsSubscribed) return;

        // Resolve local player id if not yet set (e.g. create_player fired after OnConnected)
        if (_localPlayerId == 0) ResolveLocalPlayerId();
        if (_localPlayerId == 0) return;

        long nowUs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].AbilityId == 0)
            {
                _slots[i].CooldownOverlay.fillAmount = 0f;
                _slots[i].CooldownText.text          = "";
                continue;
            }

            float  fill      = 0f;
            string timerText = "";

            foreach (var cd in SpacetimeDBManager.Conn.Db.PlayerCooldown.Iter())
            {
                if (cd.PlayerId != _localPlayerId || cd.AbilityId != _slots[i].AbilityId) continue;

                long remainUs = (long)cd.ReadyAt.MicrosecondsSinceUnixEpoch - nowUs;
                if (remainUs > 0 && _slots[i].CooldownTotalMs > 0)
                {
                    float totalUs = _slots[i].CooldownTotalMs * 1000f;
                    fill      = Mathf.Clamp01(remainUs / totalUs);
                    float sec = remainUs / 1_000_000f;
                    timerText = sec >= 1f ? $"{sec:F0}" : $"{sec:F1}";
                }
                break;
            }

            _slots[i].CooldownOverlay.fillAmount = fill;
            _slots[i].CooldownText.text          = timerText;
        }
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    void ResolveLocalPlayerId()
    {
        if (SpacetimeDBManager.Conn == null) return;
        foreach (var p in SpacetimeDBManager.Conn.Db.Player.Iter())
        {
            if (p.Identity != SpacetimeDBManager.LocalIdentity) continue;
            _localPlayerId = p.Id;
            return;
        }
    }

    // ─── canvas construction ─────────────────────────────────────────────────

    void BuildCanvas()
    {
        var canvasGo = new GameObject("HotbarCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel anchored to bottom-center
        var panel     = new GameObject("HotbarPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0f);
        panelRect.anchorMax        = new Vector2(0.5f, 0f);
        panelRect.pivot            = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 20f);

        var hLayout = panel.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing                = 8f;
        hLayout.childControlWidth      = false;  // respect child sizeDelta
        hLayout.childControlHeight     = false;
        hLayout.childForceExpandWidth  = false;
        hLayout.childForceExpandHeight = false;

        var sizer = panel.AddComponent<ContentSizeFitter>();
        sizer.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizer.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < 5; i++)
        {
            _slots[i] = new SlotData { AbilityId = SlotAbilityIds[i] };
            BuildSlot(panel.transform, i);
        }
    }

    void BuildSlot(Transform parent, int index)
    {
        const float S = 70f;

        // Root slot GO
        var slotGo   = new GameObject($"Slot_{index + 1}");
        slotGo.transform.SetParent(parent, false);
        var slotRect = slotGo.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(S, S);

        // Background
        var bg   = slotGo.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // Border highlight (1 px inset)
        var border = MakeChild(slotGo.transform, "Border", 0, 0, 0, 0);
        var borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        var borderFill = MakeChild(border.transform, "BorderFill", 1, 1, -1, -1);
        var borderFillImg = borderFill.AddComponent<Image>();
        borderFillImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // Cooldown overlay — radial fill, WoW-style (counter-clockwise from top)
        var overlay     = MakeChild(slotGo.transform, "CooldownOverlay", 0, 0, 0, 0);
        var overlayImg  = overlay.AddComponent<Image>();
        overlayImg.color        = new Color(0f, 0f, 0f, 0.72f);
        overlayImg.type         = Image.Type.Filled;
        overlayImg.fillMethod   = Image.FillMethod.Radial360;
        overlayImg.fillOrigin   = 2;       // Top
        overlayImg.fillClockwise = false;  // counter-clockwise → bright area grows clockwise
        overlayImg.fillAmount   = 0f;
        _slots[index].CooldownOverlay = overlayImg;

        // Key label — top-left corner
        var keyGo    = new GameObject("KeyLabel");
        keyGo.transform.SetParent(slotGo.transform, false);
        var keyRect  = keyGo.AddComponent<RectTransform>();
        keyRect.anchorMin        = new Vector2(0f, 1f);
        keyRect.anchorMax        = new Vector2(0f, 1f);
        keyRect.pivot            = new Vector2(0f, 1f);
        keyRect.anchoredPosition = new Vector2(4f, -3f);
        keyRect.sizeDelta        = new Vector2(20f, 16f);
        var keyText  = keyGo.AddComponent<Text>();
        keyText.text      = SlotKeys[index];
        keyText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        keyText.fontSize  = 11;
        keyText.color     = new Color(0.8f, 0.8f, 0.8f, 1f);
        keyText.alignment = TextAnchor.UpperLeft;

        // Ability name — bottom-center
        var nameGo   = new GameObject("NameLabel");
        nameGo.transform.SetParent(slotGo.transform, false);
        var nameRect = nameGo.AddComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0f, 0f);
        nameRect.anchorMax        = new Vector2(1f, 0f);
        nameRect.pivot            = new Vector2(0.5f, 0f);
        nameRect.anchoredPosition = new Vector2(0f, 4f);
        nameRect.sizeDelta        = new Vector2(0f, 14f);
        var nameText = nameGo.AddComponent<Text>();
        nameText.text      = _slots[index].AbilityId == 0 ? "" : "...";
        nameText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize  = 9;
        nameText.color     = new Color(0.75f, 0.75f, 0.75f, 1f);
        nameText.alignment = TextAnchor.LowerCenter;
        _slots[index].NameLabel = nameText;

        // Cooldown timer — centered
        var timerGo   = new GameObject("CooldownTimer");
        timerGo.transform.SetParent(slotGo.transform, false);
        var timerRect = timerGo.AddComponent<RectTransform>();
        timerRect.anchorMin  = Vector2.zero;
        timerRect.anchorMax  = Vector2.one;
        timerRect.offsetMin  = Vector2.zero;
        timerRect.offsetMax  = Vector2.zero;
        var timerText = timerGo.AddComponent<Text>();
        timerText.text      = "";
        timerText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.fontSize  = 20;
        timerText.fontStyle = FontStyle.Bold;
        timerText.color     = Color.white;
        timerText.alignment = TextAnchor.MiddleCenter;
        _slots[index].CooldownText = timerText;
    }

    /// Creates a child GO with a RectTransform stretched by pixel offsets from parent edges.
    static GameObject MakeChild(Transform parent, string name, float left, float bottom, float right, float top)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
        return go;
    }
}
