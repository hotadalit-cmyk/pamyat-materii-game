using System.Collections.Generic;
using PM.Domain.Core;

namespace PM.Domain.Run {

/// <summary>
/// v0.15: элемент декора биома — чистая визуализация (без коллайдеров).
/// Pos — центр пропса в мировых координатах; PropIndex — индекс в BiomeDecorProps.Props(biome).
/// </summary>
public struct DecorItem {
    public Vec2 Pos;
    public int PropIndex;     // 0..5 — пропс биома
    public int Tier;          // 0 малый, 1 средний, 2 крупный
    public double Scale;      // диаметр пропса в мировых единицах
    public double RotationDeg;
    public bool Flip;
    public bool Glow;         // светится сквозь тьму (мерцание — на стороне Unity)
}

/// <summary>План декора карты: строится генератором (детерминирован по сиду), рисует WorldView.</summary>
public sealed class DecorPlan {
    public List<DecorItem> Items = new List<DecorItem>();
}

/// <summary>
/// Таблица пропсов биома (Resources-пути без расширения, AiArt/Props/*) и индексы светящихся.
/// Единственный источник путей для Unity и индексов для тестов. Без LINQ.
/// </summary>
public static class BiomeDecorProps {
    /// <summary>Пропсы биома по индексам 0..5.</summary>
    public static string[] Props(BiomeId b) {
        switch (b) {
            case BiomeId.Fungal: return new[] {
                "b1_p1_mushroom_cluster", "b1_p2_mushroom_trio", "b1_p3_glow_moss",
                "b1_p4_spore_puffball", "b1_p5_glow_fern", "b1_p6_mossy_rock" };
            case BiomeId.Warm: return new[] {
                "b2_p1_lava_crack", "b2_p2_ember_mound", "b2_p3_obsidian_rocks",
                "b2_p4_steam_vent", "b2_p5_lava_bubble", "b2_p6_charred_rock" };
            case BiomeId.Ponds: return new[] {
                "b3_p1_lily_pad", "b3_p2_glow_algae", "b3_p3_glow_coral",
                "b3_p4_wet_rock", "b3_p5_small_jellyfish", "b3_p6_bubbles_reeds" };
            case BiomeId.Crystal: return new[] {
                "b4_p1_blue_crystals", "b4_p2_white_spikes", "b4_p3_crystal_shards",
                "b4_p4_geode", "b4_p5_crystal_rock", "b4_p6_gem_cluster" };
            default: return new[] {
                "b5_p1_crimson_veins", "b5_p2_flesh_pod", "b5_p3_bone_arch",
                "b5_p4_bone_pile", "b5_p5_tendril", "b5_p6_red_nodule" };
        }
    }

    /// <summary>Индексы светящихся пропсов (рисуются поверх тьмы, с «дыханием»).</summary>
    public static bool IsGlowing(BiomeId b, int propIndex) {
        switch (b) {
            case BiomeId.Fungal: return propIndex == 2 || propIndex == 4;          // мох, папоротник
            case BiomeId.Warm: return propIndex == 0 || propIndex == 4;            // трещина, пузырь
            case BiomeId.Ponds: return propIndex == 1 || propIndex == 2 || propIndex == 4; // водоросли, коралл, медузка
            case BiomeId.Crystal: return propIndex == 0 || propIndex == 3 || propIndex == 5; // кристаллы, жеода, самоцветы
            default: return propIndex == 0 || propIndex == 1 || propIndex == 4;    // жилы, нарост, усик
        }
    }
}

/// <summary>
/// v0.15: детерминированная расстановка пропсов по полу карты.
/// Правила (ТЗ): не на проходах шириной меньше 2 (клетки вплотную к стенам), не ближе 1.5 к выходу,
/// старту и алтарям (и 2 к логову стража), вне твёрдых препятствий; плотность ~1 на 12–20 м²;
/// 3 яруса размера, случайный поворот и отражение; в Г3 часть пропсов — кувшинки на воде (Pool).
/// </summary>
public static class DecorPlanner {
    public const double MinSpacing = 2.2;        // между пропсами
    public const double ClearAltar = 1.5;        // от выхода/старта/алтарей
    public const double ClearBoss = 2.0;         // от тела стража
    public const double ClearObstacle = 0.8;     // за границей твёрдого препятствия
    public const double FloorPerItemMin = 12.0;  // плотность: не гуще 1 на 12 м²
    public const double FloorPerItemMax = 20.0;  // и не реже 1 на 20 м²

    public static DecorPlan Plan(WorldMap m, BiomeId biome, ulong seed) {
        var rng = new PmRandom(seed ^ 0xD3C0A57UL);
        var plan = new DecorPlan();
        var dist = WallDistance(m);                       // расстояние до стены (0 у стены, 255 — далеко)
        int w = m.Width, h = m.Height;

        var pools = new List<ObstacleSpec>();
        for (int i = 0; i < m.Obstacles.Count; i++)
            if (m.Obstacles[i].Kind == ObstacleKind.Pool) pools.Add(m.Obstacles[i]);

        int attempts = 0;
        for (int gy = 1; gy < (h - 2) / 3; gy++) {
            for (int gx = 1; gx < (w - 2) / 3; gx++) {
                // кандидат на каждую ячейку сетки 3×3 (9 м²): отсевы дают итог ~1 на 13–18 м² пола
                if (++attempts > 5000) break;
                double px = gx * 3 + 1.5 + rng.Range(-1.1, 1.1);
                double py = gy * 3 + 1.5 + rng.Range(-1.1, 1.1);
                int cx = (int)px, cy = (int)py;
                if (!m.IsFloor(cx, cy)) continue;
                if (dist[cy * w + cx] < 2) continue;                   // не в узких проходах
                var p = new Vec2(px, py);
                // кувшинки-островки Г3: часть пропсов садится на воду
                if (biome == BiomeId.Ponds && pools.Count > 0 && rng.NextDouble01() < 0.35) {
                    var pool = pools[rng.NextInt(pools.Count)];
                    var off = new Vec2(rng.Range(-0.5, 0.5), rng.Range(-0.5, 0.5));
                    p = pool.Pos + off * pool.Radius;
                    int ix = (int)p.X, iy = (int)p.Y;
                    if (!m.IsFloor(ix, iy)) continue;
                    if (dist[iy * w + ix] < 2) continue;
                }
                if (!ClearOf(m, p)) continue;
                // точный минимальный зазор между пропсами (список ~сотни — дёшево)
                bool busy = false;
                for (int k = 0; k < plan.Items.Count && !busy; k++)
                    if ((plan.Items[k].Pos - p).SqrLength < MinSpacing * MinSpacing) busy = true;
                if (busy) continue;

                double t = rng.NextDouble01();
                int tier = t < 0.5 ? 0 : t < 0.85 ? 1 : 2;
                double scale = tier == 0 ? rng.Range(0.55, 0.8) : tier == 1 ? rng.Range(0.9, 1.2) : rng.Range(1.35, 1.75);
                int prop = rng.NextInt(6);
                plan.Items.Add(new DecorItem {
                    Pos = p, PropIndex = prop, Tier = tier, Scale = scale,
                    RotationDeg = rng.Range(0.0, 360.0), Flip = rng.NextDouble01() < 0.5,
                    Glow = BiomeDecorProps.IsGlowing(biome, prop),
                });
            }
        }
        return plan;
    }

    /// <summary>Запретные зоны: выход, старт, алтари, логово стража, твёрдые препятствия.</summary>
    private static bool ClearOf(WorldMap m, Vec2 p) {
        if ((p - m.ExitPos).SqrLength < ClearAltar * ClearAltar) return false;
        if ((p - m.PlayerStart).SqrLength < ClearAltar * ClearAltar) return false;
        if (m.BossPos.SqrLength > 0.01 && (p - m.BossPos).SqrLength < ClearBoss * ClearBoss) return false;
        for (int a = 0; a < m.Altars.Count; a++)
            if ((p - m.Altars[a].Pos).SqrLength < ClearAltar * ClearAltar) return false;
        for (int o = 0; o < m.Obstacles.Count; o++) {
            var ob = m.Obstacles[o];
            if (!ObstacleRules.IsSolid(ob.Kind)) continue;         // зоны (вода) — можно, там кувшинки
            double rr = ob.Radius + ClearObstacle;
            if ((p - ob.Pos).SqrLength < rr * rr) return false;
        }
        return true;
    }

    /// <summary>BFS от всех стен: 0 — стеновая клетка, 1..4 — у стены, 255 — глубоко в полу.</summary>
    private static byte[] WallDistance(WorldMap m) {
        int w = m.Width, h = m.Height;
        var dist = new byte[w * h];
        var q = new Queue<int>();
        for (int i = 0; i < dist.Length; i++) {
            if (m.Cells[i] == 0) { dist[i] = 0; q.Enqueue(i); } else dist[i] = 255;
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
        return dist;
    }
}
}
