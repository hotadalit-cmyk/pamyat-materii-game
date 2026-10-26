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
    public const int PxPerCell = 4;
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
        var px = new Color32[tw * th];
        for (int ty = 0; ty < th; ty++) {
            int cy = ty / P;
            for (int tx = 0; tx < tw; tx++) {
                int cx = tx / P, ci = cy * w + cx;
                Color c;
                float n = Noise(tx * 0.09f, ty * 0.09f) * 0.6f + Noise(tx * 0.31f, ty * 0.31f) * 0.4f;
                if (Map.Cells[ci] == 1) {
                    int reg = Map.RegionOf != null ? Map.RegionOf[ci] : -1;
                    c = reg >= 0 && reg < regionTint.Length ? regionTint[reg] : Color.Lerp(floorC, wallC, 0.25f);
                    c = Color.Lerp(c, glow, 0.12f + 0.18f * n);
                    c *= 0.88f + 0.24f * n;
                } else {
                    int d = dist[ci];
                    float k = d >= 4 ? 0f : (4 - d) / 3f;          // 1 у самого пола
                    c = Color.Lerp(wallC * 0.45f, wallC, k) * (0.85f + 0.3f * n);
                    if (d == 1) c = Color.Lerp(c, accent, 0.18f + 0.12f * n); // светящаяся кромка стены
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
