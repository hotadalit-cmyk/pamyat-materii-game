using UnityEngine;

namespace PM.Systems.Run {
/// <summary>
/// Тьма пещеры (v0.4): один огромный спрайт с прозрачной «дырой» вокруг слизня.
/// Всё, что рисуется выше (sortingOrder > Order), светится сквозь тьму — споры, выход, души.
/// Без URP и шейдеров: процедурная текстура, масштаб = радиус света.
/// </summary>
public sealed class DarknessMask : MonoBehaviour {
    public const int Order = 50;
    // Дыра в нормированных координатах (0 — центр, 1 — край спрайта): прозрачно до HoleInner,
    // полная тьма с HoleOuter. Радиус света R соответствует HoleOuter → размер спрайта R/HoleOuter*2.
    private const float HoleInner = 0.045f;
    private const float HoleOuter = 0.12f;
    private static Sprite _sprite;

    public Transform Follow;
    private SpriteRenderer _sr;
    private float _radius = 3.5f, _targetRadius = 3.5f;
    private float _alpha = 0.7f, _targetAlpha = 0.7f;

    public static DarknessMask Create(Transform parent) {
        var go = new GameObject("darkness_mask");
        go.transform.SetParent(parent, false);
        var m = go.AddComponent<DarknessMask>();
        m._sr = go.AddComponent<SpriteRenderer>();
        m._sr.sprite = GetSprite();
        m._sr.sortingOrder = Order;
        m._sr.color = new Color(0.01f, 0.015f, 0.03f, m._alpha);
        return m;
    }

    /// <summary>darkness01 — непрозрачность тьмы; lightRadius — радиус света слизня (мир. единицы).</summary>
    public void Set(float darkness01, float lightRadius) {
        _targetAlpha = Mathf.Clamp01(darkness01);
        _targetRadius = Mathf.Max(1f, lightRadius);
    }

    void LateUpdate() {
        if (Follow != null) {
            var p = Follow.position;
            transform.position = new Vector3(p.x, p.y, -3f); // перед 3D-моделями (z≈-0.6), за ними — камера
        }
        float k = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
        _radius = Mathf.Lerp(_radius, _targetRadius, k);
        _alpha = Mathf.Lerp(_alpha, _targetAlpha, k);
        float size = _radius / HoleOuter * 2f;
        transform.localScale = new Vector3(size, size, 1f);
        var c = _sr.color; c.a = _alpha; _sr.color = c;
    }

    private static Sprite GetSprite() {
        if (_sprite != null) return _sprite;
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        float half = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HoleInner, HoleOuter, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        _sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return _sprite;
    }
}
}
