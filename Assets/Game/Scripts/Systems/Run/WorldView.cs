using UnityEngine;
using PM.Domain.Core;
using PM.Domain.Run;
using PM.Systems.Gameplay;

namespace PM.Systems.Run {
/// <summary>
/// Вид огромного подземелья (v0.6): вся карта — ОДНА процедурная текстура (стены, пол, оттенки регионов),
/// коллайдеры — из WorldMap.WallRects. Плюс туман войны: какие клетки игрок уже видел (для миникарты).
/// Начало координат мира — левый нижний угол карты, клетка = 1 единица.
/// </summary>
public sealed class WorldView {
    public const int PxPerCell = 8;
    public readonly WorldMap Map;
    public readonly Texture2D MiniTex;      // 1 px = 1 клетка; неоткрытое прозрачно
    public readonly bool[] Explored;
    private readonly Color32[] _mini;
    private readonly Color32 _miniFloor, _miniWall;
    private bool _miniDirty;
    private int _lastCx = -999, _lastCy = -999;

    public WorldView(WorldMap map, Transform root, BiomeDef biome, PhysicsMaterial2D wallMat) {
        Map = map;
        BuildTexture(root, biome);
        BuildProps(root, biome);
        BuildColliders(root, wallMat);
        Explored = new bool[map.Width * map.Height];
        _mini = new Color32[map.Width * map.Height];
        MiniTex = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
        };
        var floor = RunWorldBuilder.Hex(biome.AccentColor);
        _miniFloor = new Color(floor.r * 0.55f + 0.25f, floor.g * 0.55f + 0.25f, floor.b * 0.55f + 0.25f, 0.95f);
        _miniWall = new Color(0.08f, 0.09f, 0.12f, 0.9f);
        for (int i = 0; i < _mini.Length; i++) _mini[i] = new Color32(0, 0, 0, 0);
        MiniTex.SetPixels32(_mini);
        MiniTex.Apply(false);
    }

    // ---------- Большая текстура мира ----------
    private void BuildTexture(Transform root, BiomeDef biome) {
        int w = Map.Width, h = Map.Height;
        // расстояние стены до пола (0 — пол, 1..3 — стена рядом, 4 — толща) — для светлой кромки
        var dist = new byte[w * h];
        var q = new System.Collections.Generic.Queue<int>();
        for (int i = 0; i < dist.Length; i++) {
            if (Map.Cells[i] == 1) { dist[i] = 0; q.Enqueue(i); } else dist[i] = 255;
        }
        while (q.Count > 0) {
            int i = q.Dequeue();
            int x = i % w, y = i / w;
            int nd = dist[i] + 1;
            if (nd > 4) continue;
            if (x > 0 && dist[i - 1] > nd) { dist[i - 1] = (byte)nd; q.Enqueue(i - 1); }
            if (x < w - 1 && dist[i + 1] > nd) { dist[i + 1] = (byte)nd; q.Enqueue(i + 1); }
            if (y > 0 && dist[i - w] > nd) { dist[i - w] = (byte)nd; q.Enqueue(i - w); }
            if (y < h - 1 && dist[i + w] > nd) { dist[i + w] = (byte)nd; q.Enqueue(i + w); }
        }
        Color floorC = RunWorldBuilder.Hex(biome.FloorColor), wallC = RunWorldBuilder.Hex(biome.WallColor);
        Color accent = RunWorldBuilder.Hex(biome.AccentColor), glow = RunWorldBuilder.Hex(biome.GlowColor);
        // оттенок по типу региона — у каждого места свой характер
        var regionTint = new Color[Map.Regions.Count];
        for (int r = 0; r < Map.Regions.Count; r++) regionTint[r] = TintFor(Map.Regions[r].Kind, floorC, accent);
        int P = PxPerCell, tw = w * P, th = h * P;
        // оттенок клеток пола — для плавного (билинейного) перехода между регионами
        _cellTint = new Color[w * h];
        Color tunnel = Color.Lerp(floorC, wallC, 0.2f);
        for (int i = 0; i < _cellTint.Length; i++) {
            int reg = Map.RegionOf != null ? Map.RegionOf[i] : -1;
            _cellTint[i] = reg >= 0 && reg < regionTint.Length ? regionTint[reg] : tunnel;
        }
        var bid = biome.Id;
        var px = new Color32[tw * th];
        for (int ty = 0; ty < th; ty++) {
            int cy = ty / P;
            float fy = (ty + 0.5f) / P - 0.5f;               // координата в «центрах клеток»
            for (int tx = 0; tx < tw; tx++) {
                int cx = tx / P, ci = cy * w + cx;
                float fx = (tx + 0.5f) / P - 0.5f;
                float n1 = Noise(tx * 0.045f, ty * 0.045f);      // крупные пятна
                float n2 = Noise(tx * 0.19f, ty * 0.19f);        // зерно
                // органичный край: билинейная интерполяция пола между центрами клеток + порог с шумом
                float edgeT = FloorAt(fx, fy, w, h) - 0.5f + (n1 - 0.5f) * 0.22f;
                Color c;
                if (edgeT > 0f) {
                    c = TintAt(fx, fy, w, h);
                    c = Color.Lerp(c, glow, 0.08f + 0.16f * n1) * (0.8f + 0.32f * n2);
                    float ao = Mathf.Clamp01(edgeT * 3.4f);         // затенение у стен — объём
                    c *= 0.58f + 0.42f * ao;
                    c = FloorDetail(bid, c, accent, tx, ty, n1, n2);
                } else {
                    if (Map.Cells[ci] == 1) ci = NearestWallCell(cx, cy, w, h, ci);
                    int d = dist[ci] == 0 ? 1 : dist[ci];
                    float depthK = Mathf.Clamp01((d - 1) / 3f + (-edgeT) * 0.6f); // 0 у кромки → 1 в толще
                    float strata = Mathf.Sin(ty * 0.11f + tx * 0.02f + n1 * 7f) * 0.5f + 0.5f;  // слои породы
                    c = Color.Lerp(wallC * 1.25f, wallC * 0.35f, depthK) * (0.78f + 0.18f * strata + 0.16f * n2);
                    if (depthK < 0.14f) c = Color.Lerp(c, accent, (0.14f - depthK) / 0.14f * (0.35f + 0.2f * n2)); // светящаяся кромка
                    float ridge = 1f - Mathf.Abs(2f * Noise(tx * 0.07f + 31f, ty * 0.07f) - 1f);
                    if (ridge > 0.93f) c = Color.Lerp(c, accent * 1.2f, (ridge - 0.93f) / 0.07f * (0.8f - 0.5f * depthK)); // жилы
                }
                c.a = 1f;
                px[ty * tw + tx] = c;
            }
        }
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var go = new GameObject("world_map");
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), Vector2.zero, P);
        sr.sortingOrder = 0;
        go.transform.position = Vector3.zero;
    }

    private Color[] _cellTint;

    /// <summary>Плавный оттенок пола: билинейно между центрами клеток (без «ступенек» на границах регионов).</summary>
    private Color TintAt(float fx, float fy, int w, int h) {
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        Color a = TintCell(x0, y0, w, h), b = TintCell(x0 + 1, y0, w, h), c = TintCell(x0, y0 + 1, w, h), d = TintCell(x0 + 1, y0 + 1, w, h);
        return Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
    }
    private Color TintCell(int x, int y, int w, int h) {
        x = Mathf.Clamp(x, 0, w - 1); y = Mathf.Clamp(y, 0, h - 1);
        int i = y * w + x;
        if (Map.Cells[i] == 1) return _cellTint[i];
        // стеновая клетка у пола: берём оттенок соседнего пола, чтобы край не темнел полосой
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
            int xx = x + dx, yy = y + dy;
            if (xx >= 0 && yy >= 0 && xx < w && yy < h && Map.Cells[yy * w + xx] == 1) return _cellTint[yy * w + xx];
        }
        return _cellTint[i];
    }

    /// <summary>Фактура пола по биому: мох и споры, лавовые трещины, лужи, грани кристаллов, жилы сердца.</summary>
    private static Color FloorDetail(BiomeId b, Color c, Color accent, int tx, int ty, float n1, float n2) {
        // общая галька
        float peb = Hash(tx / 3, ty / 3);
        if (peb > 0.986f) c *= peb > 0.993f ? 1.25f : 0.7f;
        switch (b) {
            case BiomeId.Fungal: {
                float moss = Noise(tx * 0.03f + 50f, ty * 0.03f);
                if (moss > 0.6f) c = Color.Lerp(c, new Color(0.22f, 0.42f, 0.24f), Mathf.Clamp01((moss - 0.6f) * 3f) * 0.45f);
                if (Hash(tx / 2 + 7, ty / 2) > 0.997f) c = Color.Lerp(c, accent * 1.4f, 0.8f);   // светящиеся споры
                break;
            }
            case BiomeId.Warm: {
                float r = 1f - Mathf.Abs(2f * Noise(tx * 0.05f + 9f, ty * 0.05f) - 1f);
                if (r > 0.94f) c = Color.Lerp(c, new Color(1f, 0.42f, 0.08f), (r - 0.94f) / 0.06f * 0.9f); // лавовые трещины
                else c = Color.Lerp(c, new Color(0.18f, 0.12f, 0.1f), (1f - n1) * 0.15f);              // пепел
                break;
            }
            case BiomeId.Ponds: {
                float wet = Noise(tx * 0.025f + 3f, ty * 0.025f);
                if (wet > 0.58f) {
                    float k = Mathf.Clamp01((wet - 0.58f) * 4f);
                    c = Color.Lerp(c, new Color(0.12f, 0.28f, 0.48f), k * 0.55f);
                    float ripple = Mathf.Sin((tx + ty * 0.7f) * 0.35f + n1 * 9f);
                    if (ripple > 0.92f) c = Color.Lerp(c, new Color(0.5f, 0.75f, 1f), k * 0.35f);       // блик воды
                }
                break;
            }
            case BiomeId.Crystal: {
                c *= 0.88f + 0.24f * Hash(tx / 6, ty / 6);                                              // грани
                if (Hash(tx, ty + 13) > 0.9975f) c = Color.Lerp(c, new Color(0.8f, 1f, 1f), 0.9f);      // искры
                break;
            }
            case BiomeId.Heart: {
                float r = 1f - Mathf.Abs(2f * Noise(tx * 0.06f + 17f, ty * 0.06f) - 1f);
                if (r > 0.9f) c = Color.Lerp(c, new Color(0.62f, 0.08f, 0.14f), (r - 0.9f) / 0.1f * 0.7f); // жилы
                break;
            }
        }
        return c;
    }

    private float FloorAt(float fx, float fy, int w, int h) {
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        float a = Cell(x0, y0, w, h), b = Cell(x0 + 1, y0, w, h), c = Cell(x0, y0 + 1, w, h), d = Cell(x0 + 1, y0 + 1, w, h);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }
    private float Cell(int x, int y, int w, int h) => x < 0 || y < 0 || x >= w || y >= h ? 0f : Map.Cells[y * w + x];
    private int NearestFloorCell(int cx, int cy, int w, int h, int fallback) {
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
            int x = cx + dx, y = cy + dy;
            if (x >= 0 && y >= 0 && x < w && y < h && Map.Cells[y * w + x] == 1) return y * w + x;
        }
        return fallback;
    }
    private int NearestWallCell(int cx, int cy, int w, int h, int fallback) {
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
            int x = cx + dx, y = cy + dy;
            if (x >= 0 && y >= 0 && x < w && y < h && Map.Cells[y * w + x] == 0) return y * w + x;
        }
        return fallback;
    }

    /// <summary>
    /// Декор у стен (не мешает движению): грибы, угли, пузырьки, кристаллы, узлы-сердца.
    /// Светящиеся части рисуются поверх тьмы — пещера мерцает, как в биолюминесцентном гроте.
    /// </summary>
    private void BuildProps(Transform root, BiomeDef biome) {
        var propsRoot = new GameObject("props").transform;
        propsRoot.SetParent(root, false);
        Color accent = RunWorldBuilder.Hex(biome.AccentColor);
        int w = Map.Width, h = Map.Height, placed = 0;
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++) {
                if (Map.Cells[y * w + x] != 1) continue;
                // только у стены, ~1 из 12 таких клеток
                int wx = 0, wy = 0, walls = 0;
                if (Map.Cells[y * w + x - 1] == 0) { wx -= 1; walls++; }
                if (Map.Cells[y * w + x + 1] == 0) { wx += 1; walls++; }
                if (Map.Cells[(y - 1) * w + x] == 0) { wy -= 1; walls++; }
                if (Map.Cells[(y + 1) * w + x] == 0) { wy += 1; walls++; }
                if (walls == 0 || walls >= 3) continue;                    // не в тупиках-трещинах
                float hsh = Hash(x * 7 + 3, y * 13 + 1);
                if (hsh > 0.085f) continue;
                var pos = new Vector3(x + 0.5f + wx * 0.28f, y + 0.5f + wy * 0.28f, 0);
                float sc = 0.6f + Hash(x, y + 99) * 0.7f;
                var go = new GameObject("prop");
                go.transform.SetParent(propsRoot, false);
                go.transform.position = pos;
                switch (biome.Id) {
                    case BiomeId.Fungal: {
                        var cap = Color.Lerp(new Color(0.9f, 0.45f, 0.8f), accent, Hash(x, y) );
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 0.12f * sc, 0.3f * sc, new Color(0.85f, 0.8f, 0.7f), 2)
                            .transform.localPosition = new Vector3(0, -0.1f * sc, 0);
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 0.36f * sc, 0.22f * sc, cap, 2);
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, 1.1f * sc, 1.1f * sc, new Color(cap.r, cap.g, cap.b, 0.32f), DarknessMask.Order + 1);
                        break;
                    }
                    case BiomeId.Warm: {
                        var ember = new Color(1f, 0.5f + 0.3f * hsh * 10f, 0.15f);
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 0.16f * sc, 0.16f * sc, ember, 2);
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, 1.0f * sc, 1.0f * sc, new Color(1f, 0.45f, 0.1f, 0.4f), DarknessMask.Order + 1);
                        break;
                    }
                    case BiomeId.Ponds:
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, 0.34f * sc, 0.34f * sc, new Color(0.55f, 0.8f, 1f, 0.55f), 2);
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 0.12f * sc, 0.12f * sc, new Color(0.85f, 0.95f, 1f, 0.8f), 2)
                            .transform.localPosition = new Vector3(0.18f * sc, 0.1f * sc, 0);
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, 0.9f * sc, 0.9f * sc, new Color(0.4f, 0.7f, 1f, 0.25f), DarknessMask.Order + 1);
                        break;
                    case BiomeId.Crystal: {
                        var cr = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 0.18f * sc, 0.5f * sc, new Color(0.6f, 0.95f, 1f, 0.9f), 2);
                        cr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-wy, -wx) * Mathf.Rad2Deg - 90f + (hsh * 400f - 17f));
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, 1.2f * sc, 1.2f * sc, new Color(0.5f, 0.9f, 1f, 0.38f), DarknessMask.Order + 1);
                        break;
                    }
                    default:
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 0.26f * sc, 0.26f * sc, new Color(0.7f, 0.1f, 0.18f), 2);
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, 0.34f * sc, 0.34f * sc, new Color(1f, 0.3f, 0.35f, 0.6f), 2);
                        SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, 1.0f * sc, 1.0f * sc, new Color(1f, 0.2f, 0.25f, 0.3f), DarknessMask.Order + 1);
                        break;
                }
                placed++;
            }
    }

    private static Color TintFor(RegionKind k, Color floor, Color accent) {
        switch (k) {
            case RegionKind.Start: return Color.Lerp(floor, new Color(0.25f, 0.45f, 0.55f), 0.35f);
            case RegionKind.Exit: return Color.Lerp(floor, new Color(0.25f, 0.55f, 0.35f), 0.35f);
            case RegionKind.Maze: return Color.Lerp(floor, new Color(0.35f, 0.4f, 0.22f), 0.4f);
            case RegionKind.Shrine: return Color.Lerp(floor, new Color(0.45f, 0.3f, 0.6f), 0.45f);
            case RegionKind.Spring: return Color.Lerp(floor, new Color(0.2f, 0.4f, 0.65f), 0.45f);
            case RegionKind.Elite: return Color.Lerp(floor, new Color(0.55f, 0.15f, 0.15f), 0.4f);
            case RegionKind.Lair: return Color.Lerp(floor, new Color(0.4f, 0.2f, 0.5f), 0.45f);
            case RegionKind.Secret: return Color.Lerp(floor, new Color(0.6f, 0.5f, 0.2f), 0.45f);
            case RegionKind.BossLair: return Color.Lerp(floor, new Color(0.5f, 0.08f, 0.1f), 0.5f);
            default: return floor;
        }
    }

    // дешёвый value-noise без аллокаций
    private static float Hash(int x, int y) {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }
    private static float Noise(float x, float y) {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private void BuildColliders(Transform root, PhysicsMaterial2D mat) {
        var go = new GameObject("world_walls");
        go.transform.SetParent(root, false);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        for (int i = 0; i < Map.WallRects.Count; i++) {
            var r = Map.WallRects[i];
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(r.W, r.H);
            col.offset = new Vector2(r.X + r.W * 0.5f, r.Y + r.H * 0.5f);
            col.sharedMaterial = mat;
        }
    }

    // ---------- Туман войны ----------
    /// <summary>Открыть клетки вокруг игрока. Возвращает id региона под игроком (или -1).</summary>
    public int Reveal(Vector2 pos, float radius) {
        if (!Map.CellOf(new Vec2(pos.x, pos.y), out int cx, out int cy)) return -1;
        int reg = Map.RegionOf != null ? Map.RegionOf[cy * Map.Width + cx] : -1;
        if (cx == _lastCx && cy == _lastCy) return reg;
        _lastCx = cx; _lastCy = cy;
        int r = Mathf.CeilToInt(radius), r2 = r * r;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++) {
                if (dx * dx + dy * dy > r2) continue;
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) continue;
                int i = y * Map.Width + x;
                if (Explored[i]) continue;
                Explored[i] = true;
                _mini[i] = Map.Cells[i] == 1 ? _miniFloor : _miniWall;
                _miniDirty = true;
            }
        return reg;
    }

    public bool IsExplored(Vec2 p) => Map.CellOf(p, out int x, out int y) && Explored[y * Map.Width + x];

    /// <summary>Залить изменения в текстуру миникарты (вызывать не чаще 5 раз в секунду).</summary>
    public void FlushMini() {
        if (!_miniDirty) return;
        _miniDirty = false;
        MiniTex.SetPixels32(_mini);
        MiniTex.Apply(false);
    }
}
}
