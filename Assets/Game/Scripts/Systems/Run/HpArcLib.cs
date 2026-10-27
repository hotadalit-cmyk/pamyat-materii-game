using UnityEngine;
namespace PM.Systems.Run {
/// <summary>
/// v0.13: 32 процедурные дуги-спрайта для шкалы HP вокруг существа. Заполнение — по часовой
/// стрелке от верха. Текстуры строятся один раз при первом обращении; в кадре — только выбор
/// готового спрайта по доле и цвет, без шейдеров и без аллокаций.
/// Спрайт «нативно» 1 юнит (pixelsPerUnit = размер текстуры): localScale = мировой диаметр.
/// </summary>
public static class HpArcLib {
    public const int Count = 32;
    private const int N = 128;                 // размер текстуры
    private const float RingIn = 0.72f;        // внутренний край дуги (доля радиуса)
    private const float RingOut = 0.96f;       // внешний край
    private const float Feather = 0.025f;      // мягкость краёв

    private static Sprite[] _arcs;

    private static readonly Color Green = new Color(0.45f, 1f, 0.5f);
    private static readonly Color Yellow = new Color(1f, 0.85f, 0.3f);
    private static readonly Color Red = new Color(1f, 0.35f, 0.3f);

    /// <summary>Спрайт с заполнением frac01 (по часовой от верха).</summary>
    public static Sprite Get(float frac01) {
        if (_arcs == null) _arcs = Build();
        int idx = Mathf.CeilToInt(Mathf.Clamp01(frac01) * Count);
        if (idx < 1) idx = 1;
        if (idx > Count) idx = Count;
        return _arcs[idx - 1];
    }

    /// <summary>Цвет шкалы: зелёный > 60%, жёлтый 30–60%, красный &lt; 30%.</summary>
    public static Color ColorFor(float frac01) {
        if (frac01 > 0.6f) return Green;
        if (frac01 >= 0.3f) return Yellow;
        return Red;
    }

    private static Sprite[] Build() {
        var arr = new Sprite[Count];
        for (int k = 1; k <= Count; k++) arr[k - 1] = MakeArc((float)k / Count);
        return arr;
    }

    private static Sprite MakeArc(float sweep01) {
        var tex = NewTex();
        var px = new Color32[N * N];
        float sweep = sweep01 * Mathf.PI * 2f;
        bool full = sweep01 >= 0.999f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++) {
                float dx = (x + 0.5f) / N * 2f - 1f;
                float dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.InverseLerp(RingIn - Feather, RingIn + Feather, r) *
                             (1f - Mathf.InverseLerp(RingOut - Feather, RingOut + Feather, r));
                if (ring <= 0f) { px[y * N + x] = new Color32(255, 255, 255, 0); continue; }
                // угол по часовой от верха: верх — atan2(dy,dx) = +90°
                float phi = Mathf.Repeat(Mathf.PI * 0.5f - Mathf.Atan2(dy, dx), Mathf.PI * 2f);
                float arc = full ? 1f : phi <= sweep ? 1f
                    : phi <= sweep + Feather * 4f ? 1f - (phi - sweep) / (Feather * 4f) : 0f;
                px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ring * arc) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    private static Texture2D NewTex() {
        return new Texture2D(N, N, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
    }
}
}
