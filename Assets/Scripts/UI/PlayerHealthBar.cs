using UnityEngine;
using UnityEngine.UI;
using SpacetimeDB.Types;

/// <summary>
/// World-space floating health bar above a player capsule.
/// Added to each player GO by PlayerManager — call Init(player, isLocal) immediately after AddComponent.
/// Bills toward the camera every LateUpdate.
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    private ulong  _playerId;
    private Image  _fill;
    private Canvas _canvas;

    // ─── public init ─────────────────────────────────────────────────────────

    public void Init(Player player, bool isLocal)
    {
        _playerId = player.Id;
        BuildBar(player, isLocal);
    }

    // ─── lifecycle ───────────────────────────────────────────────────────────

    void OnEnable()  => SpacetimeDBManager.OnPlayerUpdated += OnPlayerUpdated;
    void OnDisable() => SpacetimeDBManager.OnPlayerUpdated -= OnPlayerUpdated;

    void LateUpdate()
    {
        if (_canvas == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        // Face the camera (billboard — match camera forward, don't tilt)
        _canvas.transform.forward = cam.transform.forward;
    }

    // ─── event handler ───────────────────────────────────────────────────────

    void OnPlayerUpdated(Player _, Player p)
    {
        if (p.Id != _playerId) return;
        UpdateFill(p.Health, p.MaxHealth);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    void UpdateFill(int health, int maxHealth)
    {
        if (_fill == null) return;
        _fill.fillAmount = maxHealth > 0 ? Mathf.Clamp01((float)health / maxHealth) : 1f;
        _fill.color      = Color.Lerp(Color.red, new Color(0.15f, 0.75f, 0.15f), _fill.fillAmount);
    }

    // ─── canvas construction ─────────────────────────────────────────────────

    void BuildBar(Player player, bool isLocal)
    {
        // Canvas: 160 × 36 pixels at 0.01 scale = 1.6 × 0.36 world units
        const float PixW   = 160f;
        const float PixH   = 36f;
        const float Scale  = 0.01f;
        const float YOffset = 2.8f;  // above capsule top

        var canvasGo = new GameObject("HealthBarCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, YOffset, 0f);
        canvasGo.transform.localScale    = Vector3.one * Scale;

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.WorldSpace;
        _canvas.sortingOrder = 5;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(PixW, PixH);

        // Name text — top half (18 px)
        var nameGo   = new GameObject("NameText");
        nameGo.transform.SetParent(canvasGo.transform, false);
        var nameRect = nameGo.AddComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0f, 0.5f);
        nameRect.anchorMax        = new Vector2(1f, 1f);
        nameRect.offsetMin        = Vector2.zero;
        nameRect.offsetMax        = Vector2.zero;
        var nameText = nameGo.AddComponent<Text>();
        nameText.text      = player.Name;
        nameText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize  = 14;
        nameText.fontStyle = isLocal ? FontStyle.Bold : FontStyle.Normal;
        nameText.color     = isLocal ? Color.cyan : new Color(1f, 0.55f, 0.55f);
        nameText.alignment = TextAnchor.MiddleCenter;

        // Bar background — bottom half (16 px, 2 px inset each side)
        var bgGo   = new GameObject("BarBG");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0.5f);
        bgRect.offsetMin = new Vector2(4f,  2f);
        bgRect.offsetMax = new Vector2(-4f, -2f);
        var bgImg   = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.05f, 0.05f, 0.9f);

        // Fill
        var fillGo   = new GameObject("BarFill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);
        _fill = fillGo.AddComponent<Image>();
        _fill.type       = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = 0; // Left

        float initialPct = player.MaxHealth > 0 ? Mathf.Clamp01((float)player.Health / player.MaxHealth) : 1f;
        _fill.fillAmount = initialPct;
        _fill.color      = Color.Lerp(Color.red, new Color(0.15f, 0.75f, 0.15f), initialPct);
    }
}
