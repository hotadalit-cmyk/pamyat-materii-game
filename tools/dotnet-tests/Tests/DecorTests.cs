using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.15: план декора — детерминизм, запретные зоны, плотность, ярусы, свечение.</summary>
public static class DecorTests {
    private static WorldMap Map(int depth, ulong seed) =>
        WorldGenerator.Generate(depth, seed, new NemesisRegistry());

    public static void Deterministic_And_BiomePopulated() {
        for (int depth = 1; depth <= 5; depth++) {
            var a = Map(depth, 1000u + (ulong)depth).Decor;
            var b = Map(depth, 1000u + (ulong)depth).Decor;
            Eq(a.Items.Count, b.Items.Count, "d" + depth + ": same seed — same count");
            for (int i = 0; i < a.Items.Count && i < 40; i++) {
                True(a.Items[i].Pos.Approx(b.Items[i].Pos), "d" + depth + " item " + i + ": same pos");
                Eq(a.Items[i].PropIndex, b.Items[i].PropIndex, "d" + depth + " item " + i + ": same prop");
                Eq(a.Items[i].Scale, b.Items[i].Scale, "d" + depth + " item " + i + ": same scale");
                Eq(a.Items[i].RotationDeg, b.Items[i].RotationDeg, "d" + depth + " item " + i + ": same rotation");
                Eq(a.Items[i].Flip, b.Items[i].Flip, "d" + depth + " item " + i + ": same flip");
                Eq(a.Items[i].Glow, b.Items[i].Glow, "d" + depth + " item " + i + ": same glow");
            }
            True(a.Items.Count > 40, "d" + depth + ": decor populated (" + a.Items.Count + ")");
        }
    }

    public static void Forbidden_Zones_Respected() {
        for (int depth = 1; depth <= 5; depth++) {
            var m = Map(depth, 700u + (ulong)depth);
            foreach (var it in m.Decor.Items) {
                True((it.Pos - m.ExitPos).SqrLength >= DecorPlanner.ClearAltar * DecorPlanner.ClearAltar,
                    "d" + depth + ": away from exit");
                True((it.Pos - m.PlayerStart).SqrLength >= DecorPlanner.ClearAltar * DecorPlanner.ClearAltar,
                    "d" + depth + ": away from start");
                for (int a = 0; a < m.Altars.Count; a++)
                    True((it.Pos - m.Altars[a].Pos).SqrLength >= DecorPlanner.ClearAltar * DecorPlanner.ClearAltar,
                        "d" + depth + ": away from altar " + a);
                for (int o = 0; o < m.Obstacles.Count; o++) {
                    var ob = m.Obstacles[o];
                    if (!ObstacleRules.IsSolid(ob.Kind)) continue;
                    double rr = ob.Radius + DecorPlanner.ClearObstacle;
                    True((it.Pos - ob.Pos).SqrLength >= rr * rr, "d" + depth + ": away from solid obstacle");
                }
                // не в узких проходах: клетка минимум в 2 от стены
                True(m.CellOf(it.Pos, out int x, out int y), "d" + depth + ": item inside map");
            }
        }
    }

    public static void Not_In_Narrow_Passages() {
        // прямая проверка: ни один пропс не стоит в клетке с стеной ближе 2 клеток
        for (int depth = 1; depth <= 5; depth++) {
            var m = Map(depth, 900u + (ulong)depth);
            var narrow = new bool[m.Width * m.Height];
            var q = new System.Collections.Generic.Queue<int>();
            for (int i = 0; i < narrow.Length; i++)
                if (m.Cells[i] == 0) { narrow[i] = true; q.Enqueue(i); }
            // одна волна: клетки, соседние со стеной (проход шириной 1–2)
            var wave = new bool[m.Width * m.Height];
            while (q.Count > 0) {
                int i = q.Dequeue();
                int x = i % m.Width, y = i / m.Width;
                Mark(wave, m.Width, m.Height, x - 1, y); Mark(wave, m.Width, m.Height, x + 1, y);
                Mark(wave, m.Width, m.Height, x, y - 1); Mark(wave, m.Width, m.Height, x, y + 1);
            }
            foreach (var it in m.Decor.Items) {
                m.CellOf(it.Pos, out int x, out int y);
                True(!wave[y * m.Width + x], "d" + depth + ": prop not adjacent to wall (wide passage only)");
            }
        }
    }

    private static void Mark(bool[] w, int mw, int mh, int x, int y) {
        if (x < 0 || y < 0 || x >= mw || y >= mh) return;
        w[y * mw + x] = true;
    }

    public static void Density_WithinTarget() {
        for (int depth = 1; depth <= 5; depth++) {
            var m = Map(depth, 500u + (ulong)depth);
            // «широкий» пол: клетки минимум в 2 от стены (в узких проходах декора не бывает по ТЗ)
            var wide = WideFloor(m);
            int floor = 0;
            for (int i = 0; i < wide.Length; i++) if (wide[i]) floor++;
            double per = floor / (double)System.Math.Max(1, m.Decor.Items.Count);
            True(per >= DecorPlanner.FloorPerItemMin - 1.5,
                "d" + depth + ": not denser than 1/" + DecorPlanner.FloorPerItemMin + " m² (got 1/" + per.ToString("0.0") + ")");
            True(per <= DecorPlanner.FloorPerItemMax + 2.0,
                "d" + depth + ": not sparser than 1/" + DecorPlanner.FloorPerItemMax + " m² (got 1/" + per.ToString("0.0") + ")");
        }
    }

    /// <summary>Клетки пола, у которых стена не ближе 2 клеток (доступные для декора).</summary>
    private static bool[] WideFloor(WorldMap m) {
        int w = m.Width, h = m.Height;
        var wall = new bool[w * h];
        var q = new System.Collections.Generic.Queue<int>();
        for (int i = 0; i < wall.Length; i++)
            if (m.Cells[i] == 0) { wall[i] = true; q.Enqueue(i); }
        var near = new bool[w * h];
        while (q.Count > 0) {
            int i = q.Dequeue();
            int x = i % w, y = i / w;
            Mark(near, w, h, x - 1, y); Mark(near, w, h, x + 1, y);
            Mark(near, w, h, x, y - 1); Mark(near, w, h, x, y + 1);
        }
        var wide = new bool[w * h];
        for (int i = 0; i < wide.Length; i++) wide[i] = m.Cells[i] == 1 && !near[i];
        return wide;
    }

    public static void Tiers_Rotation_Flip_Glow_InRange() {
        var m = Map(3, 4242u);
        int glow = 0;
        foreach (var it in m.Decor.Items) {
            True(it.Tier >= 0 && it.Tier <= 2, "tier in 0..2");
            True(it.Scale >= 0.5 && it.Scale <= 1.8, "scale in range");
            True(it.RotationDeg >= 0.0 && it.RotationDeg < 360.0, "rotation in range");
            True(it.PropIndex >= 0 && it.PropIndex < 6, "prop index in 0..5");
            True(BiomeDecorProps.IsGlowing(BiomeId.Ponds, it.PropIndex) == it.Glow, "glow flag matches table");
            if (it.Glow) glow++;
        }
        True(glow > 0, "some props glow");
    }

    public static void PropsTable_CoversAllBiomes() {
        foreach (BiomeId b in new[] { BiomeId.Fungal, BiomeId.Warm, BiomeId.Ponds, BiomeId.Crystal, BiomeId.Heart }) {
            var props = BiomeDecorProps.Props(b);
            Eq(6, props.Length, "biome " + b + ": 6 props");
            for (int i = 0; i < 6; i++) True(!string.IsNullOrEmpty(props[i]), "biome " + b + " prop " + i + " named");
        }
    }
}
}
