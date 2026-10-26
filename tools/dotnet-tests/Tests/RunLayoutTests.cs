using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunLayoutTests {

    // ---------- KindFor ----------

    public static void KindFor_BossAtDepth5Room2() {
        for (ulong seed = 1; seed <= 100; seed++) {
            Eq(RoomKind.Boss, LayoutGenerator.KindFor(5, 2, seed * 31UL), "глубина 5 комната 2 — всегда босс (seed " + seed + ")");
        }
    }

    public static void KindFor_FirstRoomOfDepth1IsNormal() {
        for (ulong seed = 1; seed <= 100; seed++) {
            Eq(RoomKind.Normal, LayoutGenerator.KindFor(1, 0, seed * 17UL), "первая комната — обычная (seed " + seed + ")");
        }
    }

    public static void KindFor_Deterministic() {
        for (ulong seed = 1; seed <= 50; seed++) {
            RoomKind a = LayoutGenerator.KindFor(3, 1, seed);
            RoomKind b = LayoutGenerator.KindFor(3, 1, seed);
            Eq(a, b, "повторный вызов даёт тот же тип (seed " + seed + ")");
        }
        Eq(LayoutGenerator.KindFor(2, 0, 777UL), LayoutGenerator.KindFor(2, 0, 777UL), "стабильность на фиксированном seed");
    }

    public static void KindFor_ShrineFrequencyAtRoom1() {
        int shrine = 0;
        const int n = 2000;
        for (ulong seed = 1; seed <= n; seed++)
            if (LayoutGenerator.KindFor(1, 1, seed) == RoomKind.Shrine) shrine++;
        double frac = shrine / (double)n;
        True(frac > 0.20 && frac < 0.30, "святилищ ~25% на комнате 1, получено " + frac.ToString("0.0%"));
    }

    public static void KindFor_SpringFrequencyAtRoom1() {
        int spring = 0;
        const int n = 2000;
        for (ulong seed = 1; seed <= n; seed++)
            if (LayoutGenerator.KindFor(2, 1, seed) == RoomKind.Spring) spring++;
        double frac = spring / (double)n;
        True(frac > 0.15 && frac < 0.25, "родников ~20% на комнате 1, получено " + frac.ToString("0.0%"));
    }

    public static void KindFor_RestIsEliteOrNormal() {
        int elite = 0, normal = 0, invalid = 0;
        const int n = 2000;
        for (ulong seed = 1; seed <= n; seed++) {
            var kind = LayoutGenerator.KindFor(1, 1, seed);
            if (kind == RoomKind.Elite) elite++;
            else if (kind == RoomKind.Normal) normal++;
            else if (kind != RoomKind.Shrine && kind != RoomKind.Spring) invalid++;
        }
        Eq(0, invalid, "на комнате 1 бывает только Shrine/Spring/Elite/Normal");
        double rest = (elite + normal) / (double)n;
        True(rest > 0.50 && rest < 0.60, "остальные ~55%, получено " + rest.ToString("0.0%"));
        True(normal > elite, "на глубине 1 обычных больше, чем элитных");
    }

    public static void KindFor_EliteChanceGrowsWithDepth() {
        int d2 = 0, d5 = 0;
        const int n = 2000;
        for (ulong seed = 1; seed <= n; seed++) {
            if (LayoutGenerator.KindFor(2, 0, seed) == RoomKind.Elite) d2++;
            if (LayoutGenerator.KindFor(5, 0, seed) == RoomKind.Elite) d5++;
        }
        True(d2 / (double)n > 0.17 && d2 / (double)n < 0.27, "глубина 2: элита ~22%, получено " + (d2 / (double)n).ToString("0.0%"));
        True(d5 / (double)n > 0.35 && d5 / (double)n < 0.45, "глубина 5: элита ~40%, получено " + (d5 / (double)n).ToString("0.0%"));
        True(d5 > d2, "элитных комнат глубже — больше");
    }

    public static void KindFor_NoShrineSpringOutsideRoom1() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 500; seed++) {
                var k0 = LayoutGenerator.KindFor(depth, 0, seed * 7UL);
                True(k0 == RoomKind.Normal || k0 == RoomKind.Elite, "комната 0 без святилищ и родников");
                var k2 = LayoutGenerator.KindFor(depth, 2, seed * 7UL);
                True(k2 == RoomKind.Normal || k2 == RoomKind.Elite || k2 == RoomKind.Boss,
                    "комната 2 без святилищ и родников");
            }
    }

    // ---------- Generate ----------

    public static void Generate_NormalObstacleCount() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 12; seed++) {
                var plan = RoomGenerator.Generate(depth, (int)(seed % 3), seed * 13UL, null);
                var layout = LayoutGenerator.Generate(plan, RoomKind.Normal, seed * 101UL);
                True(layout.Obstacles.Count >= 3 && layout.Obstacles.Count <= 7,
                    "обычная комната: 3–7 препятствий, получено " + layout.Obstacles.Count
                    + " (глубина " + depth + ", seed " + seed + ")");
            }
    }

    public static void Generate_ShrineSpringFewerObstacles() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 12; seed++) {
                var plan = RoomGenerator.Generate(depth, (int)(seed % 3), seed * 13UL, null);
                var shrine = LayoutGenerator.Generate(plan, RoomKind.Shrine, seed * 101UL);
                True(shrine.Obstacles.Count >= 2 && shrine.Obstacles.Count <= 3,
                    "святилище: 2–3 препятствия, получено " + shrine.Obstacles.Count);
                var spring = LayoutGenerator.Generate(plan, RoomKind.Spring, seed * 101UL);
                True(spring.Obstacles.Count >= 2 && spring.Obstacles.Count <= 3,
                    "родник: 2–3 препятствия, получено " + spring.Obstacles.Count);
            }
    }

    public static void Generate_BossFourCornerPillars() {
        for (ulong seed = 1; seed <= 8; seed++) {
            var plan = RoomGenerator.Generate(5, 2, seed * 13UL, null);
            var layout = LayoutGenerator.Generate(plan, RoomKind.Boss, seed * 101UL);
            Eq(4, layout.Obstacles.Count, "в комнате босса 4 колонны");
            for (int i = 0; i < 4; i++) {
                Eq(ObstacleKind.Pillar, layout.Obstacles[i].Kind, "колонна " + i + " — Pillar");
                Eq(0.9, layout.Obstacles[i].Radius, "радиус колонны 0.9");
                double ax = System.Math.Abs(layout.Obstacles[i].Pos.X);
                double ay = System.Math.Abs(layout.Obstacles[i].Pos.Y);
                True(ax >= plan.HalfWidth - 3.1, "колонна у угла по X (" + ax.ToString("0.##") + ")");
                True(ay >= plan.HalfHeight - 3.1, "колонна у угла по Y (" + ay.ToString("0.##") + ")");
            }
        }
    }

    public static void Generate_InsideArenaAndKeepOut() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 10; seed++) {
                var plan = RoomGenerator.Generate(depth, (int)(seed % 3), seed * 19UL, null);
                var layout = LayoutGenerator.Generate(plan, RoomKind.Normal, seed * 7UL);
                for (int i = 0; i < layout.Obstacles.Count; i++) {
                    var o = layout.Obstacles[i];
                    True(System.Math.Abs(o.Pos.X) <= plan.HalfWidth - 1.0 + 1e-6, "препятствие " + i + " внутри арены по X");
                    True(System.Math.Abs(o.Pos.Y) <= plan.HalfHeight - 1.0 + 1e-6, "препятствие " + i + " внутри арены по Y");
                    True((o.Pos - plan.PlayerStart).Length >= 1.6 - 1e-6, "препятствие " + i + " дальше 1.6 от старта");
                    True((o.Pos - plan.ExitPos).Length >= 1.6 - 1e-6, "препятствие " + i + " дальше 1.6 от выхода");
                    True(o.Radius >= 0.5 - 1e-6 && o.Radius <= (ObstacleRules.IsSolid(o.Kind) ? 1.3 : 2.2) + 1e-6,
                        "радиус препятствия " + i + " в диапазоне (" + o.Radius.ToString("0.##") + ")");
                    True(ObstacleKindInPool(o.Kind, BiomeCatalog.ForDepth(depth).ObstaclePool),
                        "тип препятствия " + i + " из пула биома: " + o.Kind);
                }
            }
    }

    public static void Generate_NoIntersections() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 10; seed++) {
                var plan = RoomGenerator.Generate(depth, (int)(seed % 3), seed * 23UL, null);
                var layout = LayoutGenerator.Generate(plan, RoomKind.Elite, seed * 11UL);
                var obs = layout.Obstacles;
                for (int i = 0; i < obs.Count; i++) {
                    for (int j = i + 1; j < obs.Count; j++) {
                        double minDist = obs[i].Radius + obs[j].Radius + 0.2;
                        True((obs[i].Pos - obs[j].Pos).Length >= minDist - 1e-6,
                            "препятствия " + i + "/" + j + " не пересекаются");
                    }
                    for (int s = 0; s < plan.Spawns.Count; s++) {
                        if (!ObstacleRules.IsSolid(obs[i].Kind)) continue; // зоны проходимы — спавны не задевают
                        True((obs[i].Pos - plan.Spawns[s].Pos).Length >= obs[i].Radius + 0.3 - 1e-6,
                            "твёрдое препятствие " + i + " не накрывает точку спавна " + s);
                    }
                }
            }
    }

    public static void Generate_PathFromStartToExit() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 8; seed++) {
                var plan = RoomGenerator.Generate(depth, (int)(seed % 3), seed * 29UL, null);
                for (int k = 0; k <= 4; k++) {
                    var layout = LayoutGenerator.Generate(plan, (RoomKind)k, seed * 37UL + (ulong)k);
                    True(HasPath(plan, layout.Obstacles),
                        "проход старт→выход есть (" + (RoomKind)k + ", глубина " + depth + ", seed " + seed + ")");
                }
            }
    }

    public static void Generate_AltarOnlyForShrineAndSpring() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 10; seed++) {
                var plan = RoomGenerator.Generate(depth, 1, seed * 41UL, null);
                var normal = LayoutGenerator.Generate(plan, RoomKind.Normal, seed);
                True(normal.AltarPos.Approx(Vec2.Zero), "в обычной комнате алтаря нет");
                var elite = LayoutGenerator.Generate(plan, RoomKind.Elite, seed);
                True(elite.AltarPos.Approx(Vec2.Zero), "в элитной комнате алтаря нет");
                var boss = LayoutGenerator.Generate(plan, RoomKind.Boss, seed);
                True(boss.AltarPos.Approx(Vec2.Zero), "в комнате босса алтаря нет");

                var shrine = LayoutGenerator.Generate(plan, RoomKind.Shrine, seed);
                CheckAltarValid(plan, shrine, "святилище");
                var spring = LayoutGenerator.Generate(plan, RoomKind.Spring, seed);
                CheckAltarValid(plan, spring, "родник");
            }
    }

    public static void Generate_TitlesAndHints() {
        var plan = RoomGenerator.Generate(2, 1, 5UL, null);
        var shrine = LayoutGenerator.Generate(plan, RoomKind.Shrine, 9UL);
        Eq("Святилище мутаций", shrine.Title, "заголовок святилища");
        Eq("Коснитесь алтаря — выберите дар", shrine.Hint, "подсказка святилища");
        var spring = LayoutGenerator.Generate(plan, RoomKind.Spring, 9UL);
        Eq("Родник", spring.Title, "заголовок родника");
        var boss = LayoutGenerator.Generate(plan, RoomKind.Boss, 9UL);
        Eq("Сердце пещеры", boss.Title, "заголовок комнаты босса");
        var elite = LayoutGenerator.Generate(plan, RoomKind.Elite, 9UL);
        Eq("Элитный зал", elite.Title, "заголовок элитной комнаты");
        var normal = LayoutGenerator.Generate(plan, RoomKind.Normal, 9UL);
        Eq(BiomeCatalog.ForDepth(2).Name, normal.Title, "обычная комната зовётся биомом");
        Eq(BiomeCatalog.ForDepth(2).Subtitle, normal.Hint, "подсказка обычной комнаты — подзаголовок биома");
    }

    public static void Generate_Deterministic() {
        var planA = RoomGenerator.Generate(3, 1, 55UL, null);
        var planB = RoomGenerator.Generate(3, 1, 55UL, null);
        var a = LayoutGenerator.Generate(planA, RoomKind.Normal, 77UL);
        var b = LayoutGenerator.Generate(planB, RoomKind.Normal, 77UL);
        Eq(a.Obstacles.Count, b.Obstacles.Count, "число препятствий совпадает");
        bool same = true;
        for (int i = 0; i < a.Obstacles.Count; i++) {
            if (a.Obstacles[i].Kind != b.Obstacles[i].Kind) same = false;
            if (!a.Obstacles[i].Pos.Approx(b.Obstacles[i].Pos)) same = false;
            if (System.Math.Abs(a.Obstacles[i].Radius - b.Obstacles[i].Radius) > 1e-9) same = false;
        }
        True(same, "препятствия идентичны для одинакового seed");
        True(a.AltarPos.Approx(b.AltarPos), "алтарь совпадает");
        Eq(a.Title, b.Title, "заголовки совпадают");
    }

    // ---------- helpers ----------

    private static void CheckAltarValid(RoomPlan plan, RoomLayout layout, string what) {
        True((layout.AltarPos - plan.PlayerStart).Length >= 1.6 - 1e-6, what + ": алтарь дальше 1.6 от старта");
        True((layout.AltarPos - plan.ExitPos).Length >= 1.6 - 1e-6, what + ": алтарь дальше 1.6 от выхода");
        for (int i = 0; i < layout.Obstacles.Count; i++)
            True((layout.AltarPos - layout.Obstacles[i].Pos).Length >= 1.6 - 1e-6,
                what + ": алтарь дальше 1.6 от препятствия " + i);
    }

    private static bool ObstacleKindInPool(ObstacleKind kind, ObstacleKind[] pool) {
        for (int i = 0; i < pool.Length; i++) if (pool[i] == kind) return true;
        return false;
    }

    /// <summary>Независимая проверка прохода: сетка 0.5, блокируются клетки внутри r+0.25 твёрдых препятствий.</summary>
    private static bool HasPath(RoomPlan plan, List<ObstacleSpec> obstacles) {
        const double step = 0.5, pad = 0.25;
        int nx = (int)System.Math.Floor((plan.HalfWidth - pad) * 2.0 / step) + 1;
        int ny = (int)System.Math.Floor((plan.HalfHeight - pad) * 2.0 / step) + 1;
        if (nx < 1 || ny < 1) return true;
        int cells = nx * ny;
        var blocked = new bool[cells];
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++) {
                double x = -plan.HalfWidth + pad + i * step;
                double y = -plan.HalfHeight + pad + j * step;
                for (int k = 0; k < obstacles.Count; k++) {
                    if (!ObstacleRules.IsSolid(obstacles[k].Kind)) continue;
                    double dx = x - obstacles[k].Pos.X, dy = y - obstacles[k].Pos.Y;
                    if (System.Math.Sqrt(dx * dx + dy * dy) < obstacles[k].Radius + pad) {
                        blocked[i + j * nx] = true;
                        break;
                    }
                }
            }
        int si = Clamp((int)System.Math.Round((plan.PlayerStart.X + plan.HalfWidth - pad) / step), 0, nx - 1);
        int sj = Clamp((int)System.Math.Round((plan.PlayerStart.Y + plan.HalfHeight - pad) / step), 0, ny - 1);
        int gi = Clamp((int)System.Math.Round((plan.ExitPos.X + plan.HalfWidth - pad) / step), 0, nx - 1);
        int gj = Clamp((int)System.Math.Round((plan.ExitPos.Y + plan.HalfHeight - pad) / step), 0, ny - 1);
        int start = si + sj * nx, goal = gi + gj * nx;
        if (blocked[start] || blocked[goal]) return false;
        var visited = new bool[cells];
        var queue = new Queue<int>();
        queue.Enqueue(start);
        visited[start] = true;
        while (queue.Count > 0) {
            int c = queue.Dequeue();
            if (c == goal) return true;
            int ci = c % nx;
            if (ci + 1 < nx && !visited[c + 1] && !blocked[c + 1]) { visited[c + 1] = true; queue.Enqueue(c + 1); }
            if (ci - 1 >= 0 && !visited[c - 1] && !blocked[c - 1]) { visited[c - 1] = true; queue.Enqueue(c - 1); }
            if (c + nx < cells && !visited[c + nx] && !blocked[c + nx]) { visited[c + nx] = true; queue.Enqueue(c + nx); }
            if (c - nx >= 0 && !visited[c - nx] && !blocked[c - nx]) { visited[c - nx] = true; queue.Enqueue(c - nx); }
        }
        return false;
    }

    private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
}
}
