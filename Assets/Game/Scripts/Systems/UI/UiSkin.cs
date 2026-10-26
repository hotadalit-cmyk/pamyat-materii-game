using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PM.Systems.UI {
/// <summary>
/// Скин интерфейса (v0.9, в духе мобильных MOBA): процедурные спрайты без ассетов —
/// панели с подсвеченной кромкой, «пилюли»-кнопки с бликом, круглые кнопки умений
/// с металлическим ободком, мягкое свечение. Всё белое/серое — цвет задаёт Image.color.
/// </summary>
public static class UiSkin {
    private static Sprite _panel, _pill, _disc, _rim, _glow, _ring;

    /// <summary>Панель: тёмная заливка с вертикальным градиентом, светлая кромка 2px, тонкая внутренняя тень.</summary>
    public static Sprite Panel => _panel != null ? _panel : (_panel = MakeRounded(96, 22, true));
    /// <summary>Кнопка-«пилюля»: светлый верх, тёмный низ, блик и кромка — тонируется цветом кнопки.</summary>
    public static Sprite Pill => _pill != null ? _pill : (_pill = MakeRounded(96, 30, false));
    /// <summary>Круг кнопки умения: радиальный градиент (центр светлее).</summary>
    public static Sprite Disc => _disc != null ? _disc : (_disc = MakeDisc(256));
    /// <summary>Металлический ободок поверх круга (не тонировать — уже «золотой»).</summary>
    public static Sprite Rim => _rim != null ? _rim : (_rim = MakeRim(256));
    /// <summary>Мягкое свечение вокруг готового умения.</summary>
    public static Sprite Glow => _glow != null ? _glow : (_glow = MakeGlow(128));
    /// <summary>Тонкое кольцо — вспышка «заряд готов».</summary>
    public static Sprite Ring => _ring != null ? _ring : (_ring = MakeRing(256, 0.06f));

    public static readonly Color Gold = new Color(1f, 0.82f, 0.42f);
    public static readonly Color PanelTint = new Color(0.55f, 0.75f, 1f, 0.92f);

    /// <summary>Оформить Image как панель (сохраняя цвет-подложку, если нужен).</summary>
    public static void AsPanel(Image img, Color tint) {
        img.sprite = Panel; img.type = Image.Type.Sliced; img.color = tint;
    }

    /// <summary>
    /// Фон блока: тёмные подложки становятся панелью скина (кромка, градиент), цветные — остаются
    /// цветными скруглёнными плашками (карточки узлов, полосы прогресса).
    /// </summary>
    public static void Box(Image img, Color c, Vector2 size) {
        float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        if (lum < 0.16f && size.x >= 80f && size.y >= 60f) {
            img.sprite = Panel;
            img.color = new Color(1f, 1f, 1f, Mathf.Clamp(c.a + 0.1f, 0.75f, 1f));
        } else {
            img.sprite = PM.Systems.Gameplay.SpriteUtil.UiRoundedSprite;
            img.color = c;
        }
        img.type = Image.Type.Sliced;
    }

    /// <summary>Оформить кнопку: «пилюля», эффект нажатия.</summary>
    public static void AsPill(Image img, Color tint) {
        img.sprite = Pill; img.type = Image.Type.Sliced; img.color = tint;
        if (img.GetComponent<UiPressFx>() == null) img.gameObject.AddComponent<UiPressFx>();
    }

    // ---------- генераторы ----------
    private static Sprite MakeRounded(int n, int radius, bool panel) {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float d = RoundRectDist(x + 0.5f, y + 0.5f, n, n, radius);       // <0 внутри
                float a = Mathf.Clamp01(0.5f - d);
                float t = (float)y / (n - 1);                                     // 0 — низ, 1 — верх
                float edge = Mathf.Clamp01(1f - Mathf.Abs(d + 1.5f) / 1.6f);      // кромка
                Color c;
                if (panel) {
                    float v = Mathf.Lerp(0.10f, 0.22f, t);
                    c = new Color(v * 0.7f, v * 0.9f, v * 1.4f, 0.86f);
                    float inner = Mathf.Clamp01(1f - Mathf.Abs(d + 5f) / 4f) * 0.25f; // внутренняя тень
                    c = Color.Lerp(c, new Color(0, 0, 0, 0.9f), inner);
                    c = Color.Lerp(c, new Color(0.55f, 0.85f, 1f, 0.95f), edge * (0.55f + 0.45f * t));
                } else {
                    float v = Mathf.Lerp(0.62f, 1f, Mathf.SmoothStep(0f, 1f, t));
                    c = new Color(v, v, v, 1f);
                    if (t > 0.52f && t < 0.62f) c = Color.Lerp(c, Color.white, 0.25f); // блик-полоса
                    c = Color.Lerp(c, new Color(1f, 1f, 1f, 1f), edge * 0.9f * t);    // светлая кромка сверху
                    c = Color.Lerp(c, new Color(0.25f, 0.25f, 0.3f, 1f), edge * (1f - t) * 0.8f); // тёмная снизу
                }
                c.a *= a;
                px[y * n + x] = c;
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        float b = radius + 4;
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    private static float RoundRectDist(float x, float y, float w, float h, float r) {
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
        float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static Sprite MakeDisc(int n) {
        var tex = NewTex(n);
        var px = new Color32[n * n];
        float h = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - r) * h * 0.5f);
                float v = Mathf.Lerp(1f, 0.55f, r * r) * (0.85f + 0.15f * Mathf.Clamp01(dy + 0.3f));  // свет сверху
                px[y * n + x] = new Color(v, v, v, a);
            }
        return Finish(tex, px, n);
    }

    private static Sprite MakeRim(int n) {
        var tex = NewTex(n);
        var px = new Color32[n * n];
        float h = n * 0.5f, inner = 0.86f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - r) * h * 0.5f) * Mathf.Clamp01((r - inner) * h * 0.5f);
                // «металл»: светлый верх-лево, тёмный низ-право, блик по центру кольца
                float shade = 0.55f + 0.45f * Mathf.Clamp01((dy - dx) * 0.6f + 0.5f);
                float mid = 1f - Mathf.Abs((r - (inner + 1f) * 0.5f) / ((1f - inner) * 0.5f));
                Color c = new Color(1f, 0.83f, 0.45f) * shade + new Color(0.35f, 0.3f, 0.2f) * Mathf.Clamp01(mid) * 0.6f;
                c.a = a;
                px[y * n + x] = c;
            }
        return Finish(tex, px, n);
    }

    private static Sprite MakeGlow(int n) {
        var tex = NewTex(n);
        var px = new Color32[n * n];
        float h = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                px[y * n + x] = new Color(1, 1, 1, a);
            }
        return Finish(tex, px, n);
    }

    private static Sprite MakeRing(int n, float thick) {
        var tex = NewTex(n);
        var px = new Color32[n * n];
        float h = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - (1f - thick)) / thick);
                px[y * n + x] = new Color(1, 1, 1, a);
            }
        return Finish(tex, px, n);
    }

    private static Texture2D NewTex(int n) => new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    private static Sprite Finish(Texture2D tex, Color32[] px, int n) {
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}

/// <summary>Отклик нажатия: кнопка проседает и пружинит обратно (на unscaled-времени — работает и на паузе).</summary>
public sealed class UiPressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    private float _target = 1f, _scale = 1f, _vel;

    public void OnPointerDown(PointerEventData e) { _target = 0.9f; }
    public void OnPointerUp(PointerEventData e) { _target = 1f; _vel += 3.5f; }
    public void OnPointerExit(PointerEventData e) { _target = 1f; }

    void Update() {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        // пружина: быстрый отклик + лёгкий «отскок»
        _vel += (_target - _scale) * 260f * dt;
        _vel *= Mathf.Exp(-18f * dt);
        _scale += _vel * dt;
        if (Mathf.Abs(_scale - 1f) < 0.0005f && Mathf.Abs(_vel) < 0.001f && _target >= 1f) { _scale = 1f; _vel = 0f; }
        transform.localScale = new Vector3(_scale, _scale, 1f);
    }
}
}
