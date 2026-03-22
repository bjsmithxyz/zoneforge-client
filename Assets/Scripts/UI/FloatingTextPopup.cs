using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-destroying world-space floating text popup.
/// Floats upward and fades out over its lifetime.
/// Usage: FloatingTextPopup.Show(worldPos, "25", Color.red);
/// </summary>
public class FloatingTextPopup : MonoBehaviour
{
    private const float Duration   = 1.4f;   // total seconds before destroy
    private const float FloatSpeed = 1.5f;   // world units per second upward
    private const float FadeDelay  = 0.7f;   // fraction of Duration before fade starts

    private Text  _text;
    private Color _baseColor;
    private float _elapsed;

    // ─── public factory ─────────────────────────────────────────────────────

    /// <summary>Spawn a floating text popup at a world-space position.</summary>
    public static void Show(Vector3 worldPos, string message, Color color)
    {
        var go = new GameObject("FloatingTextPopup");
        go.transform.position = worldPos;
        var popup = go.AddComponent<FloatingTextPopup>();
        popup.Init(message, color);
    }

    // ─── init ────────────────────────────────────────────────────────────────

    void Init(string message, Color color)
    {
        _baseColor = color;

        // World-space canvas: 200×60 px at 0.01 scale = 2.0 × 0.6 world units
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localScale = Vector3.one * 0.01f;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200f, 60f);

        // Text element filling the canvas
        var textGo   = new GameObject("Text");
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _text           = textGo.AddComponent<Text>();
        _text.text      = message;
        _text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _text.fontSize  = 40;
        _text.fontStyle = FontStyle.Bold;
        _text.color     = color;
        _text.alignment = TextAnchor.MiddleCenter;
    }

    // ─── update ──────────────────────────────────────────────────────────────

    void Update()
    {
        _elapsed += Time.deltaTime;

        // Float upward
        transform.position += Vector3.up * FloatSpeed * Time.deltaTime;

        // Billboard — always face the camera
        var cam = Camera.main;
        if (cam != null && _text != null)
            _text.canvas.transform.forward = cam.transform.forward;

        // Fade out after FadeDelay fraction of lifetime
        float t = _elapsed / Duration;
        if (t >= FadeDelay && _text != null)
        {
            float alpha = 1f - (t - FadeDelay) / (1f - FadeDelay);
            _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, Mathf.Clamp01(alpha));
        }

        if (_elapsed >= Duration)
            Destroy(gameObject);
    }
}
