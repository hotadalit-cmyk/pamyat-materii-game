using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Тип комнаты: обычная, элитная, святилище мутаций, родник, босс.</summary>
public enum RoomKind { Normal = 0, Elite = 1, Shrine = 2, Spring = 3, Boss = 4 }

/// <summary>Раскладка комнаты: тип, биом, препятствия, алтарь, подписи для UI.</summary>
public sealed class RoomLayout {
    public RoomKind Kind;
    public BiomeDef Biome;
    public List<ObstacleSpec> Obstacles;
    public string Title, Hint;        // «Святилище мутаций», «Коснитесь алтаря — выберите дар»
    public Vec2 AltarPos;             // для Shrine/Spring (иначе Zero)
}

/// <summary>
/// Детерминированная генерация типов и раскладок комнат от seed. Препятствия не ближе
/// 1.6 к старту/выходу/алтарю, не пересекаются со спавнами, проход старт→выход обязателен
/// (сетка 0.5 по твёрдым препятствиям, BFS). Без LINQ.
/// </summary>
public static class LayoutGenerator {
    private const double WallMargin = 1.0;         // отступ центров препятствий от стен арены
    private const double KeepOut = 1.6;            // центр препятствия ↔ старт/выход/алтарь
    private const double SpawnGap = 0.3;           // зазор препятствие ↔ спавн
    private const double ObstacleGap = 0.2;        // зазор препятствие ↔ препятствие
    private const double MinSolidRadius = 0.5, MaxSolidRadius = 1.3;
    private const double MinZoneRadius = 1.2, MaxZoneRadius = 2.2;
    private const double GridStep = 0.5, GridPad = 0.25;
    private const int PlaceAttempts = 120;
    private const int RegenRounds = 10;
    private const double BossPillarRadius = 0.9;
    private const double ShrineChance = 0.25, SpringChance = 0.20;
    private const double EliteBaseChance = 0.10, ElitePerDepth = 0.06;

    /// <summary>
    /// Тип комнаты. Depth 5 room 2 — Boss; room 0 глубины 1 — Normal; room 1 любой глубины:
    /// 25% Shrine, 20% Spring; остальные комнаты — Elite с шансом 10%+6%×depth, иначе Normal.
    /// Детерминировано от (seed, depth, room).
    /// </summary>
    public static RoomKind KindFor(int depth, int roomIndex, ulong seed) {
        if (depth < 1) depth = 1;
        if (depth > RunState.MaxDepth) depth = RunState.MaxDepth;
        if (roomIndex < 0) roomIndex = 0;
        if (roomIndex > RunState.RoomsPerDepth - 1) roomIndex = RunState.RoomsPerDepth - 1;
        if (depth == RunState.MaxDepth && roomIndex == RunState.RoomsPerDepth - 1) return RoomKind.Boss;
        if (depth == 1 && roomIndex == 0) return RoomKind.Normal; // первая комната забега

        var rng = new PmRandom(MixRoom(seed, depth, roomIndex));
        double roll = rng.NextDouble01();
        if (roomIndex == 1) {
            if (roll < ShrineChance) return RoomKind.Shrine;
            if (roll < ShrineChance + SpringChance) return RoomKind.Spring;
            roll = rng.NextDouble01(); // отдельный бросок на элиту
        }
        double eliteChance = EliteBaseChance + ElitePerDepth * depth;
        return roll < eliteChance ? RoomKind.Elite : RoomKind.Normal;
    }

    /// <summary>Раскладка комнаты по плану: 3–7 препятствий (Shrine/Spring 2–3, Boss — 4 колонны по углам).</summary>
    public static RoomLayout Generate(RoomPlan plan, RoomKind kind, ulong seed) {
        var biome = BiomeCatalog.ForDepth(plan.Depth);
        var rng = new PmRandom(seed);
        var layout = new RoomLayout { Kind = kind, Biome = biome, Obstacles = new List<ObstacleSpec>(8) };
        if (kind == RoomKind.Boss) {
            layout.Obstacles = PlaceBossPillars(plan);
        } else {
            int count = (kind == RoomKind.Shrine || kind == RoomKind.Spring)
                ? 2 + rng.NextInt(2) : 3 + rng.NextInt(5);
            for (int round = 0; round < RegenRounds; round++) {
                layout.Obstacles = PlaceObstacles(rng, plan, biome.ObstaclePool, count);
                if (PathExists(plan, layout.Obstacles)) break;
                if (round == RegenRounds - 1) layout.Obstacles = new List<ObstacleSpec>(0); // проход гарантирован
            }
        }
        layout.AltarPos = (kind == RoomKind.Shrine || kind == RoomKind.Spring)
            ? FindAltar(rng, plan, layout.Obstacles) : Vec2.Zero;
        SetTitles(layout);
        return layout;
    }

    // ----- тип комнаты -----

    private static ulong MixRoom(ulong seed, int depth, int room) {
        ulong x = seed;
        x ^= (ulong)depth * 0x9E3779B97F4A7C15UL;
        x ^= (ulong)room * 0xC2B2AE3D27D4EB4FUL;
        x ^= (ulong)(depth * 131 + room) * 0x165667B19E3779F9UL;
        return x;
    }

    // ----- препятствия -----

    /// <summary>
    /// Размещение препятствий: 1) заявленный радиус; 2) повтор с минимальным радиусом.
    /// Твёрдые не накрывают точки спавнов (запас r + 0.3); зоны проходимы и спавнам не мешают.
    /// </summary>
    private static List<ObstacleSpec> PlaceObstacles(PmRandom rng, RoomPlan plan, ObstacleKind[] pool, int count) {
        var list = new List<ObstacleSpec>(count);
        var failed = new List<ObstacleSpec>(count);
        for (int i = 0; i < count; i++) {
            var spec = RandomObstacle(rng, pool);
            if (TryPlace(rng, plan, list, ref spec)) list.Add(spec);
            else failed.Add(spec);
        }
        for (int i = 0; i < failed.Count; i++) { // второй проход с минимальным радиусом
            var spec = failed[i];
            spec.Radius = ObstacleRules.IsSolid(spec.Kind) ? MinSolidRadius : MinZoneRadius;
            if (TryPlace(rng, plan, list, ref spec)) list.Add(spec);
        }
        return list;
    }

    private static ObstacleSpec RandomObstacle(PmRandom rng, ObstacleKind[] pool) {
        var kind = pool[rng.NextInt(pool.Length)];
        bool solid = ObstacleRules.IsSolid(kind);
        double radius = solid
            ? MinSolidRadius + rng.NextDouble01() * (MaxSolidRadius - MinSolidRadius)
            : MinZoneRadius + rng.NextDouble01() * (MaxZoneRadius - MinZoneRadius);
        return new ObstacleSpec { Kind = kind, Radius = radius };
    }

    private static bool TryPlace(PmRandom rng, RoomPlan plan, List<ObstacleSpec> placed, ref ObstacleSpec spec) {
        double maxX = plan.HalfWidth - WallMargin, maxY = plan.HalfHeight - WallMargin;
        var spawns = plan.Spawns;
        bool solid = ObstacleRules.IsSolid(spec.Kind);
        for (int attempt = 0; attempt < PlaceAttempts; attempt++) {
            Vec2 p = new Vec2((rng.NextDouble01() * 2.0 - 1.0) * maxX, (rng.NextDouble01() * 2.0 - 1.0) * maxY);
            if ((p - plan.PlayerStart).Length < KeepOut) continue;
            if ((p - plan.ExitPos).Length < KeepOut) continue;
            bool ok = true;
            for (int i = 0; i < placed.Count && ok; i++) {
                if ((p - placed[i].Pos).Length < placed[i].Radius + spec.Radius + ObstacleGap) ok = false;
            }
            if (ok && solid && spawns != null)
                for (int i = 0; i < spawns.Count && ok; i++) {
                    // точка спавна вне твёрдого препятствия с запасом r + 0.3
                    if ((p - spawns[i].Pos).Length < spec.Radius + SpawnGap) ok = false;
                }
            if (!ok) continue;
            spec.Pos = p;
            return true;
        }
        return false;
    }

    /// <summary>Босс-комната: 4 колонны по углам; при конфликте со спавном — сдвиг к центру.</summary>
    private static List<ObstacleSpec> PlaceBossPillars(RoomPlan plan) {
        var list = new List<ObstacleSpec>(4);
        double sx = plan.HalfWidth - WallMargin, sy = plan.HalfHeight - WallMargin;
        var corners = new Vec2[] { new Vec2(-sx, -sy), new Vec2(sx, -sy), new Vec2(-sx, sy), new Vec2(sx, sy) };
        for (int c = 0; c < 4; c++) {
            Vec2 p = corners[c];
            Vec2 toCenter = (new Vec2(0.0, 0.0) - corners[c]).Normalized(); // шаги по 0.25 к центру
            for (int step = 0; step < 8; step++) {
                if (PillarClearOfSpawns(plan.Spawns, p)) break;
                p = p + toCenter * 0.25;
            }
            list.Add(new ObstacleSpec { Kind = ObstacleKind.Pillar, Pos = p, Radius = BossPillarRadius });
        }
        while (list.Count > 0 && !PathExists(plan, list)) list.RemoveAt(list.Count - 1);
        return list;
    }

    private static bool PillarClearOfSpawns(List<SpawnSpec> spawns, Vec2 p) {
        if (spawns == null) return true;
        for (int i = 0; i < spawns.Count; i++)
            if ((p - spawns[i].Pos).Length < BossPillarRadius + SpawnGap) return false;
        return true;
    }

    // ----- алтарь -----

    private static Vec2 FindAltar(PmRandom rng, RoomPlan plan, List<ObstacleSpec> obstacles) {
        double maxX = plan.HalfWidth - WallMargin, maxY = plan.HalfHeight - WallMargin;
        for (int attempt = 0; attempt < 80; attempt++) {
            Vec2 p = new Vec2((rng.NextDouble01() * 2.0 - 1.0) * maxX, (rng.NextDouble01() * 2.0 - 1.0) * maxY);
            if (AltarValid(plan, obstacles, p)) return p;
        }
        Vec2[] fallbacks = new Vec2[] {
            new Vec2(0.0, 0.0), new Vec2(0.0, maxY * 0.5), new Vec2(0.0, -maxY * 0.5),
            new Vec2(maxX * 0.5, 0.0), new Vec2(-maxX * 0.5, 0.0),
        };
        for (int i = 0; i < fallbacks.Length; i++)
            if (AltarValid(plan, obstacles, fallbacks[i])) return fallbacks[i];
        // крайний случай: сеткой ищем точку с максимальным удалением от препятствий
        Vec2 best = new Vec2(0.0, maxY * 0.5);
        double bestDist = -1.0;
        for (double gx = -maxX; gx <= maxX + 1e-9; gx += 0.5)
            for (double gy = -maxY; gy <= maxY + 1e-9; gy += 0.5) {
                Vec2 p = new Vec2(gx, gy);
                if ((p - plan.PlayerStart).Length < KeepOut) continue;
                if ((p - plan.ExitPos).Length < KeepOut) continue;
                double d = MinDistanceTo(obstacles, p);
                if (d > bestDist) { bestDist = d; best = p; }
            }
        return best;
    }

    private static double MinDistanceTo(List<ObstacleSpec> obstacles, Vec2 p) {
        double min = double.MaxValue;
        for (int i = 0; i < obstacles.Count; i++) {
            double d = (p - obstacles[i].Pos).Length;
            if (d < min) min = d;
        }
        return min;
    }

    private static bool AltarValid(RoomPlan plan, List<ObstacleSpec> obstacles, Vec2 p) {
        if ((p - plan.PlayerStart).Length < KeepOut) return false;
        if ((p - plan.ExitPos).Length < KeepOut) return false;
        for (int i = 0; i < obstacles.Count; i++)
            if ((p - obstacles[i].Pos).Length < KeepOut) return false;
        var spawns = plan.Spawns;
        if (spawns != null)
            for (int i = 0; i < spawns.Count; i++) {
                double minDist = SizeRule.RadiusForMass(spawns[i].Mass) + 0.5;
                if ((p - spawns[i].Pos).Length < minDist) return false;
            }
        return true;
    }

    // ----- проход старт → выход (сетка 0.5, BFS по твёрдым препятствиям) -----

    private static int CellCount(double half) {
        double span = 2.0 * (half - GridPad);
        if (span <= 0.0) return 0;
        return (int)System.Math.Floor(span / GridStep) + 1;
    }

    private static Vec2 CellCenter(RoomPlan plan, int i, int j) {
        return new Vec2(-plan.HalfWidth + GridPad + i * GridStep,
                        -plan.HalfHeight + GridPad + j * GridStep);
    }

    private static int NearestCell(RoomPlan plan, Vec2 p, int nx, int ny) {
        int i = (int)System.Math.Round((p.X + plan.HalfWidth - GridPad) / GridStep);
        int j = (int)System.Math.Round((p.Y + plan.HalfHeight - GridPad) / GridStep);
        if (i < 0) i = 0; if (i > nx - 1) i = nx - 1;
        if (j < 0) j = 0; if (j > ny - 1) j = ny - 1;
        return i + j * nx;
    }

    private static bool PathExists(RoomPlan plan, List<ObstacleSpec> obstacles) {
        int nx = CellCount(plan.HalfWidth), ny = CellCount(plan.HalfHeight);
        if (nx < 1 || ny < 1) return true;
        int cells = nx * ny;
        bool[] blocked = new bool[cells];
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++) {
                Vec2 c = CellCenter(plan, i, j);
                for (int k = 0; k < obstacles.Count; k++) {
                    if (!ObstacleRules.IsSolid(obstacles[k].Kind)) continue;
                    if ((c - obstacles[k].Pos).Length < obstacles[k].Radius + GridPad) {
                        blocked[i + j * nx] = true;
                        break;
                    }
                }
            }
        int start = NearestCell(plan, plan.PlayerStart, nx, ny);
        int goal = NearestCell(plan, plan.ExitPos, nx, ny);
        if (blocked[start] || blocked[goal]) return false;
        var queue = new Queue<int>(cells);
        bool[] seen = new bool[cells];
        queue.Enqueue(start);
        seen[start] = true;
        while (queue.Count > 0) {
            int cell = queue.Dequeue();
            if (cell == goal) return true;
            int x = cell % nx;
            if (x + 1 < nx && !seen[cell + 1] && !blocked[cell + 1]) { seen[cell + 1] = true; queue.Enqueue(cell + 1); }
            if (x - 1 >= 0 && !seen[cell - 1] && !blocked[cell - 1]) { seen[cell - 1] = true; queue.Enqueue(cell - 1); }
            if (cell + nx < cells && !seen[cell + nx] && !blocked[cell + nx]) { seen[cell + nx] = true; queue.Enqueue(cell + nx); }
            if (cell - nx >= 0 && !seen[cell - nx] && !blocked[cell - nx]) { seen[cell - nx] = true; queue.Enqueue(cell - nx); }
        }
        return false;
    }

    // ----- подписи -----

    private static void SetTitles(RoomLayout layout) {
        switch (layout.Kind) {
            case RoomKind.Elite: layout.Title = "Элитный зал"; layout.Hint = "Добыча здесь тяжелее — и награда щедрее"; break;
            case RoomKind.Shrine: layout.Title = "Святилище мутаций"; layout.Hint = "Коснитесь алтаря — выберите дар"; break;
            case RoomKind.Spring: layout.Title = "Родник"; layout.Hint = "Коснитесь родника — тело восполнится"; break;
            case RoomKind.Boss: layout.Title = "Сердце пещеры"; layout.Hint = "Страж глубины пробудился"; break;
            default: layout.Title = layout.Biome.Name; layout.Hint = layout.Biome.Subtitle; break;
        }
    }
}
}
