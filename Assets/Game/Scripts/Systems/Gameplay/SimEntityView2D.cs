using UnityEngine;
using PM.Domain;
using PM.Domain.World;
using PM.Systems.Sim;
using PM.Systems.Bootstrap;

namespace PM.Systems.Gameplay {
/// <summary>Идентификатор сущности симуляции на объекте (для сопоставления коллизий).</summary>
public interface ISimEntity {
    int SimId { get; }
    SimEntityKind Kind { get; }
}
/// <summary>Типы игровых сущностей сцены.</summary>
public enum SimEntityKind { Player = 0, Ai = 1, Beetle = 2 }

/// <summary>
/// Общий вид сущности: круг, цвет, категория. Создаётся программно (без ассетов).
/// Спрайт-круг нативно 1 юнит → AddCircle(diameter) рисует ровно diameter (1:1 с коллайдером).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class SimEntityView2D : MonoBehaviour, ISimEntity {
    public SimEntityKind Kind;
    SimEntityKind ISimEntity.Kind => Kind;
    public int SimId { get; set; }
    public SpriteRenderer Body;
    public Rigidbody2D Rb { get; private set; }
    // декор (заполняет VisualFactory)
    public SpriteRenderer Membrane;   // ободок мембраны (игрок)
    public SpriteRenderer Core;       // ядро (игрок)
    public Transform EyeL, EyeR;      // глаза (жуки)

    void Awake() {
        Rb = GetComponent<Rigidbody2D>();
        Rb.gravityScale = 0f;
        Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public static SimEntityView2D Create(Transform parent, string name, SimEntityKind kind,
                                         int simId, float radius, Color color) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<SimEntityView2D>();
        view.Kind = kind;
        view.SimId = simId;
        var rb = go.GetComponent<Rigidbody2D>();
        rb.mass = Mathf.Max(0.5f, radius);
        rb.linearDamping = 0.9f;
        rb.angularDamping = 0f;
        var col = go.GetComponent<CircleCollider2D>();
        col.radius = radius;
        view.Body = SpriteUtil.AddCircle(go.transform, radius * 2f, color);
        return view;
    }
}
/// <summary>
/// Процедурные спрайты без внешних ассетов: круг (1 юнит), кольцо, свечение,
/// мягкий прямоугольник + 9-slice формы для uGUI. Всё генерируется в рантайме.
/// </summary>
public static class SpriteUtil {
    public static Sprite CircleSprite {
        get { if (_circle == null) _circle = MakeCircle(128); return _circle; }
    }
    public static Sprite RingSprite {
        get { if (_ring == null) _ring = MakeRing(192, 0.22f); return _ring; }
    }
    public static Sprite GlowSprite {
        get { if (_glow == null) _glow = MakeGlow(256); return _glow; }
    }
    public static Sprite SoftRectSprite {
        get { if (_softRect == null) _softRect = MakeSoftRect(256, 0.16f, 0.05f); return _softRect; }
    }
    public static Sprite UiRoundedSprite {
        get { if (_uiRounded == null) _uiRounded = MakeUiShape(96, 0.30f, false); return _uiRounded; }
    }
    public static Sprite UiCircleSprite {
        get { if (_uiCircle == null) _uiCircle = MakeUiShape(64, 0.5f, true); return _uiCircle; }
    }
    private static Sprite _circle, _ring, _glow, _softRect, _uiRounded, _uiCircle;

    private static Texture2D NewTex(int size) {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    /// <summary>Круг с мягким краем; pixelsPerUnit = size → нативный размер 1 юнит.</summary>
    public static Sprite MakeCircle(int size) {
        var tex = NewTex(size);
        float r = size * 0.5f - 1f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float dx = x - size / 2f + 0.5f, dy = y - size / 2f + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01((r - d) / 3f + 1f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>Кольцо (мембрана/ободок): мягкая внешняя и внутренняя кромка.</summary>
    public static Sprite MakeRing(int size, float thickness01) {
        var tex = NewTex(size);
        float r = size * 0.5f - 1f;
        float rIn = r * (1f - thickness01);
        float soft = Mathf.Max(1.5f, size * 0.045f);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float dx = x - size / 2f + 0.5f, dy = y - size / 2f + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Clamp01((r - d) / soft + 1f);   // мягкий внешний край
                float inner = Mathf.Clamp01((d - rIn) / soft);      // мягкий внутренний край
                byte a = (byte)(Mathf.Clamp01(outer * inner) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>Радиальное свечение: максимум в центре → 0 к краю.</summary>
    public static Sprite MakeGlow(int size) {
        var tex = NewTex(size);
        float r = size * 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float dx = x - size / 2f + 0.5f, dy = y - size / 2f + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float a = Mathf.Clamp01(1f - d);
                a *= a; // квадратичное затухание
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>Прямоугольник со скруглёнными углами и мягкой кромкой (пол, стены).</summary>
    public static Sprite MakeSoftRect(int size, float radius01, float soft01) {
        var tex = NewTex(size);
        float soft = Mathf.Max(2f, size * soft01);
        float half = size * 0.5f - soft - 1f;
        float rad = radius01 * half;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float qx = Mathf.Abs(x - size / 2f + 0.5f) - (half - rad);
                float qy = Mathf.Abs(y - size / 2f + 0.5f) - (half - rad);
                float ax = Mathf.Max(qx, 0f), ay = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
                byte a = (byte)(Mathf.Clamp01(-d / soft + 1f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// Форма для uGUI: 9-slice border для скруглений, чтобы панели любого размера держали
    /// одинаковый радиус углов. ppu = 100 = Canvas.referencePixelsPerUnit → 1px текстуры = 1px
    /// канваса. При ppu = 1 бордеры раздуваются ×100 и Unity ужимает их в эллипс.
    /// </summary>
    public static Sprite MakeUiShape(int size, float radius01, bool circle) {
        var tex = NewTex(size);
        float half = size * 0.5f - 1f;
        float rad = circle ? half : radius01 * size;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float qx = Mathf.Abs(x - size / 2f + 0.5f) - (half - rad);
                float qy = Mathf.Abs(y - size / 2f + 0.5f) - (half - rad);
                float ax = Mathf.Max(qx, 0f), ay = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
                byte a = (byte)(Mathf.Clamp01(-d / 1.5f + 1f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        float b = circle ? size * 0.5f : rad + 2f;
        var border = new Vector4(b, b, b, b);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border, true);
    }

    /// <summary>Круг диаметром diameter мировых юнитов (1:1 с коллайдером).</summary>
    public static SpriteRenderer AddCircle(Transform parent, float diameter, Color color) {
        return AddSprite(parent, CircleSprite, diameter, diameter, color, 5);
    }

    public static SpriteRenderer AddSprite(Transform parent, Sprite sprite,
                                           float scaleX, float scaleY, Color color, int order) {
        var go = new GameObject("spr");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        go.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        sr.sortingOrder = order;
        return sr;
    }
}

/// <summary>Процедурный декор сущностей: мембрана, ядро, глаза, свечение — без ассетов.</summary>
public static class VisualFactory {
    private static SpriteRenderer AddChild(Transform parent, Sprite sprite, float scale,
                                          Color color, int order, Vector2 localPos) {
        var go = new GameObject("deco");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>Слайм: светлая мембрана + тёмное ядро + блик. Дети тела → сквош вместе с ним.</summary>
    public static void DecorateSlime(SimEntityView2D v) {
        var body = v.Body.transform;
        v.Membrane = AddChild(body, SpriteUtil.RingSprite, 1.12f,
            GameBootstrap.COutline, 9, Vector2.zero);
        v.Core = AddChild(body, SpriteUtil.CircleSprite, 0.38f,
            GameBootstrap.CCore, 10, Vector2.zero);
        AddChild(body, SpriteUtil.CircleSprite, 0.15f,
            new Color(1f, 1f, 1f, 0.35f), 10, new Vector2(-0.10f, 0.14f));
    }

    /// <summary>Жук: тёмный ободок панциря, голова, глаза.</summary>
    public static void DecorateBeetle(SimEntityView2D v) {
        var body = v.Body.transform;
        Color shell = GameBootstrap.CDanger * 0.55f;
        AddChild(body, SpriteUtil.RingSprite, 1.10f, shell, 7, Vector2.zero);
        AddChild(body, SpriteUtil.CircleSprite, 0.44f, GameBootstrap.CDanger * 0.8f, 7,
            new Vector2(0f, -0.06f));
        var el = AddChild(body, SpriteUtil.CircleSprite, 0.11f,
            new Color(1f, 1f, 1f, 0.92f), 8, new Vector2(-0.13f, 0.15f));
        var er = AddChild(body, SpriteUtil.CircleSprite, 0.11f,
            new Color(1f, 1f, 1f, 0.92f), 8, new Vector2(0.13f, 0.15f));
        v.EyeL = el != null ? el.transform : null;
        v.EyeR = er != null ? er.transform : null;
    }

    /// <summary>ИИ-душа: светлый ободок по цвету тела.</summary>
    public static void DecorateAi(SimEntityView2D v) {
        Color rim = Color.Lerp(v.Body.color, Color.white, 0.45f);
        rim.a = 0.55f;
        v.Membrane = AddChild(v.Body.transform, SpriteUtil.RingSprite, 1.12f, rim, 7, Vector2.zero);
    }

    /// <summary>Гранула: мягкое свечение позади (order 3 ниже тела 4).</summary>
    public static void DecoratePellet(SimEntityView2D v) {
        Color g = GameBootstrap.CFood;
        g.a = 0.22f;
        AddChild(v.transform, SpriteUtil.GlowSprite, 1.15f, g, 3, Vector2.zero);
    }
}
}
