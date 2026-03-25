using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Screen-space portal crossing effect. Attach to a GameObject in the scene
/// (or spawn on demand). Call Play(onComplete) to trigger the animation.
public class RippleWarpEffect : MonoBehaviour
{
    public static RippleWarpEffect Instance { get; private set; }

    private Canvas _canvas;

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
    }

    /// <summary>Plays the Ripple Warp animation then calls onComplete.</summary>
    public void Play(Action onComplete) => StartCoroutine(PlayRoutine(onComplete));

    IEnumerator PlayRoutine(Action onComplete)
    {
        var rings = new (Image img, float startDelay)[RingCount];
        for (int i = 0; i < RingCount; i++)
        {
            var go = new GameObject($"Ring_{i}");
            go.transform.SetParent(_canvas.transform, false);
            var img  = go.AddComponent<Image>();
            img.color   = new Color(RingColour.r, RingColour.g, RingColour.b, 0f);
            img.sprite  = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
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
                var rt  = rings[i].img.GetComponent<RectTransform>();
                rt.sizeDelta = Vector2.one * size;
                rings[i].img.color = new Color(RingColour.r, RingColour.g, RingColour.b, alpha);
            }
            yield return null;
        }

        foreach (var (img, _) in rings)
            Destroy(img.gameObject);

        onComplete?.Invoke();
    }
}
