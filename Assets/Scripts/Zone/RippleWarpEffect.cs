using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Screen-space portal crossing effect. Attach to a GameObject in the scene
/// (or spawn on demand). Call Play(onComplete) to trigger the animation.
public class RippleWarpEffect : MonoBehaviour
{
    public static RippleWarpEffect Instance { get; private set; }

    private Canvas  _canvas;
    private Sprite  _circleSprite;
    private Coroutine _activeRoutine;

    private const float Duration    = 0.6f;
    private const int   RingCount   = 3;
    private static readonly Color RingColour = new Color(0.4f, 0.8f, 1f, 0.85f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // UI/Skin/Knob.psd is a built-in RP asset unavailable in URP.
        // Generate an equivalent white circle texture at runtime instead.
        _circleSprite = CreateCircleSprite(64);
    }

    void OnDestroy()
    {
        if (_circleSprite != null)
        {
            Destroy(_circleSprite.texture);
            Destroy(_circleSprite);
        }
    }

    static Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - r + 0.5f;
            float dy = y - r + 0.5f;
            tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Plays the Ripple Warp animation then calls onComplete. No-ops if already playing.</summary>
    public void Play(Action onComplete)
    {
        if (_activeRoutine != null) return;
        _activeRoutine = StartCoroutine(PlayRoutine(onComplete));
    }

    IEnumerator PlayRoutine(Action onComplete)
    {
        var rings = new (Image img, float startDelay)[RingCount];
        for (int i = 0; i < RingCount; i++)
        {
            var go = new GameObject($"Ring_{i}");
            go.transform.SetParent(_canvas.transform, false);
            var img  = go.AddComponent<Image>();
            img.color   = new Color(RingColour.r, RingColour.g, RingColour.b, 0f);
            img.sprite  = _circleSprite;
            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * 10f;
            rings[i] = (img, i * (Duration / RingCount));
        }

        float elapsed = 0f;
        float maxSize = Mathf.Max(Screen.width, Screen.height) * 1.5f;

        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < RingCount; i++)
            {
                float t = Mathf.Clamp01((elapsed - rings[i].startDelay) / (Duration - rings[i].startDelay));
                if (t <= 0f) continue;
                float size  = Mathf.Lerp(10f, maxSize, t);
                float alpha = Mathf.Lerp(RingColour.a, 0f, t);
                var rt  = rings[i].img.rectTransform;
                rt.sizeDelta = Vector2.one * size;
                rings[i].img.color = new Color(RingColour.r, RingColour.g, RingColour.b, alpha);
            }
            yield return null;
        }

        foreach (var (img, _) in rings)
            Destroy(img.gameObject);

        _activeRoutine = null;
        onComplete?.Invoke();
    }
}
