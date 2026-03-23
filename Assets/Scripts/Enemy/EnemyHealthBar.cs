using UnityEngine;
using UnityEngine.UI;
using SpacetimeDB.Types;

/// <summary>
/// World-space floating health bar above an enemy capsule.
/// Added by EnemyManager — call Init(enemy, def) immediately after AddComponent.
/// Bills toward the camera every LateUpdate. Hides on death.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    private ulong _enemyId;
    private int   _maxHealth;
    private Image _fill;
    private Canvas _canvas;

    public void Init(Enemy enemy, EnemyDefinition def)
    {
        _enemyId   = enemy.Id;
        _maxHealth = def?.MaxHealth ?? 100;
        BuildBar(def?.Name ?? "Enemy");
        UpdateFill(enemy.Health, _maxHealth);
    }

    void OnEnable()  => SpacetimeDBManager.OnEnemyUpdated += OnEnemyUpdated;
    void OnDisable() => SpacetimeDBManager.OnEnemyUpdated -= OnEnemyUpdated;

    void LateUpdate()
    {
        if (_canvas == null) return;
        var cam = Camera.main;
        if (cam != null)
            _canvas.transform.forward = cam.transform.forward;
    }

    void OnEnemyUpdated(Enemy _, Enemy e)
    {
        if (e.Id != _enemyId) return;
        if (e.IsDead)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            return;
        }
        UpdateFill(e.Health, _maxHealth);
    }

    void UpdateFill(int health, int maxHealth)
    {
        if (_fill == null) return;
        float pct    = maxHealth > 0 ? Mathf.Clamp01((float)health / maxHealth) : 1f;
        _fill.fillAmount = pct;
        _fill.color      = Color.Lerp(Color.red, new Color(0.15f, 0.75f, 0.15f), pct);
    }

    void BuildBar(string enemyName)
    {
        const float PixW    = 160f;
        const float PixH    = 36f;
        const float Scale   = 0.01f;
        const float YOffset = 2.8f;

        var canvasGo = new GameObject("EnemyHealthBarCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, YOffset, 0f);
        canvasGo.transform.localScale    = Vector3.one * Scale;

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.WorldSpace;
        _canvas.sortingOrder = 5;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(PixW, PixH);

        // Name text — top half
        var nameGo   = new GameObject("NameText");
        nameGo.transform.SetParent(canvasGo.transform, false);
        var nameRect = nameGo.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
        var nameText = nameGo.AddComponent<Text>();
        nameText.text      = enemyName;
        nameText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize  = 14;
        nameText.color     = new Color(1f, 0.6f, 0.6f);
        nameText.alignment = TextAnchor.MiddleCenter;

        // Bar background — bottom half
        var bgGo   = new GameObject("BarBG");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0.5f);
        bgRect.offsetMin = new Vector2(4f,  2f);
        bgRect.offsetMax = new Vector2(-4f, -2f);
        bgGo.AddComponent<Image>().color = new Color(0.15f, 0.05f, 0.05f, 0.9f);

        // Fill bar
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
        _fill.fillOrigin = 0;
    }
}
