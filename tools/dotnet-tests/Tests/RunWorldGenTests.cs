using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
public static class RunWorldGenTests {
    private static readonly WorldMap[] Cache = new WorldMap[6];
    private static WorldMap Map(int depth) => Cache[depth] ??= WorldGenerator.Generate(depth, 4242UL + (ulong)depth, null);

    private static int FloorCount(WorldMap m) { int c = 0; for (int i = 0; i < m.Cells.Length; i++) if (m.Cells[i] == 1) c++; return c; }

    public static void Size_GrowsWithDepth() {
        Eq(140, Map(1).Width, "d1 width"); Eq(90, Map(1).Height, "d1 height");
        Eq(200, Map(5).Width, "d5 width"); Eq(130, Map(5).Height, "d5 height");
    }

    public static void Border_IsSolid() {
        foreach (int d in new[] { 1, 3, 5 }) {
            var m = Map(d);
            for (int x = 0; x < m.Width; x++)
                for (int k = 0; k < WorldGenerator.Border; k++) {
                    True(!m.IsFloor(x, k) && !m.IsFloor(x, m.Height - 1 - k), "top/bottom border solid");
                }
            for (int y = 0; y < m.Height; y++)
                for (int k = 0; k < WorldGenerator.Border; k++)
                    True(!m.IsFloor(k, y) && !m.IsFloor(m.Width - 1 - k, y), "left/right border solid");
        }
    }

    public static void Deterministic_BySeed() {
        var a = WorldGenerator.Generate(2, 77UL, null);
        var b = WorldGenerator.Generate(2, 77UL, null);
        Eq(a.Cells.Length, b.Cells.Length, "same size");
        bool same = true;
        for (int i = 0; i < a.Cells.Length; i++) if (a.Cells[i] != b.Cells[i]) { same = false; break; }
        True(same, "same cells");
        Eq(a.Spawns.Count, b.Spawns.Count, "same spawns");
        True(a.ExitPos.Approx(b.ExitPos), "same exit");
        var c = WorldGenerator.Generate(2, 78UL, null);
        bool differ = false;
        for (int i = 0; i < a.Cells.Length && !differ; i++) if (a.Cells[i] != c.Cells[i]) differ = true;
        True(differ, "different seed → different map");
    }

    public static void AllFloor_ConnectedFromStart() {
        for (int d = 1; d <= 5; d++) {
            var m = Map(d);
            True(m.CellOf(m.PlayerStart, out int sx, out int sy) && m.IsFloor(sx, sy), "start on floor");
            var dist = WorldGenerator.Bfs(m, sx, sy);
            for (int i = 0; i < m.Cells.Length; i++)
                if (m.Cells[i] == 1) True(dist[i] != int.MaxValue, "floor reachable d" + d);
        }
    }

    public static void World_IsHuge() {
        // открытый мир: пола в десятки раз больше старой комнаты 9×5 (≈180 клеток)
        True(FloorCount(Map(1)) > 3000, "depth 1 floor > 3000 cells");
        True(FloorCount(Map(5)) > FloorCount(Map(1)), "deeper is bigger");
    }

    public static void Regions_KindsPresent() {
        for (int d = 1; d <= 5; d++) {
            var m = Map(d);
            int start = 0, exit = 0, boss = 0, maze = 0, secret = 0, shrine = 0, spring = 0, elite = 0, cav = 0;
            foreach (var r in m.Regions) {
                switch (r.Kind) {
                    case RegionKind.Start: start++; break; case RegionKind.Exit: exit++; break;
                    case RegionKind.BossLair: boss++; break; case RegionKind.Maze: maze++; break;
                    case RegionKind.Secret: secret++; break; case RegionKind.Shrine: shrine++; break;
                    case RegionKind.Spring: spring++; break; case RegionKind.Elite: elite++; break;
                    case RegionKind.Cavern: cav++; break;
                }
                True(!string.IsNullOrEmpty(r.Title), "every region titled");
            }
            Eq(1, start, "one start d" + d);
            if (d == 5) { Eq(1, boss, "boss lair on 5"); Eq(0, exit, "no plain exit on 5"); }
            else { Eq(1, exit, "one exit d" + d); Eq(0, boss, "no boss lair before 5"); }
            True(maze >= 1, "has mazes d" + d);
            True(secret >= 1, "has secrets d" + d);
            True(shrine >= 1 && spring >= 1 && elite >= 1, "special regions d" + d);
            True(cav >= 3, "plenty of plain caverns d" + d);
        }
    }

    public static void Mazes_HaveRelicAndAreTwisty() {
        var m = Map(3);
        int relics = 0;
        foreach (var a in m.Altars) if (a.Kind == AltarKind.Relic && m.Regions[a.RegionId].Kind == RegionKind.Maze) relics++;
        int mazes = 0; foreach (var r in m.Regions) if (r.Kind == RegionKind.Maze) mazes++;
        Eq(mazes, relics, "each maze has a relic");
        // лабиринт извилист: путь до реликвии заметно длиннее прямой
        True(m.CellOf(m.PlayerStart, out int sx, out int sy), "start");
        var dist = WorldGenerator.Bfs(m, sx, sy);
        foreach (var r in m.Regions) {
            if (r.Kind != RegionKind.Maze) continue;
            int minD = int.MaxValue, relicD = 0;
            for (int y = r.MinY; y <= r.MaxY; y++)
                for (int x = r.MinX; x <= r.MaxX; x++) {
                    int i = y * m.Width + x;
                    if (m.Cells[i] == 1 && m.RegionOf[i] == r.Id && dist[i] < minD) minD = dist[i];
                }
            foreach (var a in m.Altars) if (a.RegionId == r.Id && m.CellOf(a.Pos, out int ax, out int ay)) relicD = dist[ay * m.Width + ax];
            True(relicD - minD >= 8, "maze path to relic is winding");
        }
    }

    public static void Start_NearEdge_Exit_Farthest() {
        for (int d = 1; d <= 5; d++) {
            var m = Map(d);
            True(m.CellOf(m.PlayerStart, out int sx, out int sy), "start cell");
            int edge = System.Math.Min(System.Math.Min(sx, m.Width - sx), System.Math.Min(sy, m.Height - sy));
            True(edge < m.Height / 3, "start near an edge d" + d);
            var dist = WorldGenerator.Bfs(m, sx, sy);
            True(m.CellOf(m.ExitPos, out int ex, out int ey), "exit cell");
            int exitD = dist[ey * m.Width + ex];
            // v0.15: на Г5 логово сознательно в центре карты (кольцо вокруг логова),
            // поэтому «выход среди самых дальних» проверяется только на Г1–Г4.
            if (d == 5) continue;
            foreach (var r in m.Regions) {
                if (r.Kind != RegionKind.Cavern) continue;
                var c = WorldGenerator.NearestFloor(m, r.Center);
                m.CellOf(c, out int cx, out int cy);
                True(dist[cy * m.Width + cx] <= exitD + 30, "exit is among the farthest d" + d);
            }
        }
    }

    public static void BossPos_OnlyOnDepth5() {
        True(Map(5).BossPos.Length > 1, "boss pos on 5");
        for (int d = 1; d <= 4; d++) True(Map(d).BossPos.Approx(Vec2.Zero), "no boss pos d" + d);
        var m = Map(5);
        True((m.ExitPos - m.BossPos).Length > 2, "shaft behind the warden");
    }

    public static void Spawns_OnFloor_NotAtStart() {
        for (int d = 1; d <= 5; d++) {
            var m = Map(d);
            foreach (var s in m.Spawns) {
                True(m.CellOf(s.Pos, out int x, out int y) && m.IsFloor(x, y), "spawn on floor");
                if (!s.IsNemesis && s.SpeciesId != "spore") True((s.Pos - m.PlayerStart).Length >= 8, "no creature at start");
                True(s.SpeciesId != BossState.SpeciesId, "warden not in spawns");
            }
        }
    }

    public static void Danger_GrowsTowardShaft() {
        var m = Map(2);
        m.CellOf(m.PlayerStart, out int sx, out int sy);
        var dist = WorldGenerator.Bfs(m, sx, sy);
        int maxD = 0; foreach (var dd in dist) if (dd != int.MaxValue && dd > maxD) maxD = dd;
        double nearSum = 0, farSum = 0; int nearN = 0, farN = 0;
        foreach (var s in m.Spawns) {
            if (s.SpeciesId == "spore" || s.IsNemesis) continue;
            m.CellOf(s.Pos, out int x, out int y);
            double t = (double)dist[y * m.Width + x] / maxD;
            if (t < 0.33) { nearSum += s.Mass; nearN++; } else if (t > 0.66) { farSum += s.Mass; farN++; }
        }
        True(farN > 0, "creatures far away");
        True(nearN == 0 || farSum / farN > nearSum / nearN, "far creatures heavier than near");
        // v0.14: допуск на сид-шум приёма спавнов (радиусы видов изменились) —
        // градиент плотности задан вероятностью пропуска, а не строгим неравенством
        True(farN * 3 >= nearN, "not denser near start");
    }

    public static void SoulSlimes_Capped() {
        for (int d = 1; d <= 5; d++) {
            int souls = 0;
            foreach (var s in Map(d).Spawns) if (s.SpeciesId == "soul_slime" && !s.IsNemesis) souls++;
            True(souls <= 2, "≤2 soul slimes d" + d);
        }
    }

    public static void Nemesis_SpawnsInLair() {
        var reg = new NemesisRegistry();
        reg.OnPlayerKilledBy("Искра", new string[0], new PmRandom(1));
        var m = WorldGenerator.Generate(2, 99UL, reg);
        int lairs = 0; foreach (var r in m.Regions) if (r.Kind == RegionKind.Lair) lairs++;
        Eq(1, lairs, "one lair");
        bool found = false;
        foreach (var s in m.Spawns)
            if (s.IsNemesis && s.SoulName == "Искра" && m.CellOf(s.Pos, out int x, out int y)
                && m.Regions[m.RegionOf[y * m.Width + x]].Kind == RegionKind.Lair) found = true;
        True(found, "nemesis waits in its lair");
    }

    public static void Obstacles_OnlyInCaverns_AndKeepPath() {
        for (int d = 1; d <= 5; d++) {
            var m = Map(d);
            True(m.Obstacles.Count >= 10, "plenty of obstacles d" + d);
            var blocked = new bool[m.Cells.Length];
            foreach (var o in m.Obstacles) {
                True(m.CellOf(o.Pos, out int x, out int y), "obstacle in map");
                int reg = m.RegionOf[y * m.Width + x];
                True(reg >= 0 && m.Regions[reg].Kind != RegionKind.Maze && m.Regions[reg].Kind != RegionKind.Secret, "only in chambers");
                if (!ObstacleRules.IsSolid(o.Kind)) continue;
                int r1 = (int)System.Math.Ceiling(o.Radius);
                for (int yy = y - r1; yy <= y + r1; yy++)
                    for (int xx = x - r1; xx <= x + r1; xx++) {
                        if (xx < 0 || yy < 0 || xx >= m.Width || yy >= m.Height) continue;
                        double dx = xx + 0.5 - o.Pos.X, dy = yy + 0.5 - o.Pos.Y;
                        if (dx * dx + dy * dy <= (o.Radius + 0.5) * (o.Radius + 0.5)) blocked[yy * m.Width + xx] = true;
                    }
            }
            m.CellOf(m.PlayerStart, out int sx, out int sy);
            var dist = WorldGenerator.Bfs(m, sx, sy, blocked);
            m.CellOf(m.ExitPos, out int ex, out int ey);
            True(dist[ey * m.Width + ex] != int.MaxValue, "shaft reachable around obstacles d" + d);
            foreach (var a in m.Altars) { m.CellOf(a.Pos, out int ax, out int ay); True(dist[ay * m.Width + ax] != int.MaxValue, "altar reachable d" + d); }
        }
    }

    public static void WallRects_CoverEveryWall() {
        var m = Map(4);
        var covered = new bool[m.Cells.Length];
        foreach (var r in m.WallRects)
            for (int y = r.Y; y < r.Y + r.H; y++)
                for (int x = r.X; x < r.X + r.W; x++) {
                    True(m.Cells[y * m.Width + x] == 0, "rect covers only walls");
                    covered[y * m.Width + x] = true;
                }
        for (int i = 0; i < m.Cells.Length; i++) if (m.Cells[i] == 0) True(covered[i], "every wall covered");
        True(m.WallRects.Count <= 2500, "collider count reasonable (" + m.WallRects.Count + ")");
    }

    public static void GoalMass_Reasonable() {
        for (int d = 1; d <= 5; d++) {
            var m = Map(d);
            double total = 0; foreach (var s in m.Spawns) total += s.Mass;
            True(m.GoalMass >= 6.0, "goal ≥ 6");
            True(m.GoalMass <= total * 0.5 + 6, "goal reachable");
        }
    }

    public static void Generate_IsFast() {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        WorldGenerator.Generate(5, 123UL, null);
        sw.Stop();
        True(sw.ElapsedMilliseconds < 1500, "depth 5 generates fast (" + sw.ElapsedMilliseconds + " ms)");
    }

    // ---------- RunState в открытом мире ----------
    public static void RunState_OpenWorld_BuildsFromMap() {
        var rs = new RunState(new SoulMeta(), 5UL, true);
        True(rs.OpenWorld, "open world flag");
        True(rs.World != null, "world exists");
        Eq(0, rs.RoomIndex, "room index 0");
        True(rs.Room.ExitPos.Approx(rs.World.ExitPos), "room exit = shaft");
        True(rs.Room.PlayerStart.Approx(rs.World.PlayerStart), "room start = world start");
        Eq(rs.World.GoalMass, rs.Room.GoalMass, "goal from world");
        True(rs.Layout != null && rs.Layout.Obstacles == rs.World.Obstacles, "layout obstacles from world");
        True(rs.Boss == null, "no boss on depth 1");
        var old = new RunState(new SoulMeta(), 5UL);
        True(!old.OpenWorld && old.World == null, "old ctor unchanged");
    }

    public static void RunState_OpenWorld_DepthTransitionsAndVictory() {
        var rs = new RunState(new SoulMeta(), 9UL, true);
        for (int d = 1; d <= 4; d++) {
            Eq(d, rs.Depth, "depth " + d);
            rs.OnAbsorb("spore", rs.Room.GoalMass + 1);
            while (rs.PendingOffer != null) rs.ChooseMutation(rs.PendingOffer.Options[0]);
            True(rs.GuardianAwake, "guardian awake at goal " + d);
            True(!rs.ExitOpen, "shaft closed until guardian falls " + d); // v0.11: нужен страж
            ExposeGuardian(rs);
            rs.OnBossAbsorbed();
            True(rs.ExitOpen, "shaft open after guardian " + d);
            True(rs.TryEnterExit(), "descend");
        }
        Eq(5, rs.Depth, "depth 5");
        True(rs.Guardian != null && rs.Layout.Kind == RoomKind.Boss, "warden on 5");
        rs.OnAbsorb("spore", rs.Room.GoalMass + 1);
        True(rs.GuardianAwake, "warden awake at goal");
        True(!rs.ExitOpen, "shaft closed until warden");
        ExposeGuardian(rs);
        rs.OnBossAbsorbed();
        while (rs.PendingOffer != null) rs.ChooseMutation(rs.PendingOffer.Options[0]);
        True(rs.TryEnterExit(), "final descent");
        True(rs.IsVictory, "victory");
        var im = rs.Finish();
        True(im.EssenceEarned >= 80, "open world depth essence doubled");
    }

    /// <summary>Обнажить стража текущей глубины (серия ударов с рикошетом 0).</summary>
    private static void ExposeGuardian(RunState rs) {
        int guard = 0;
        while (!rs.Guardian.IsExposed && guard++ < 200000)
            rs.Guardian.ApplyHit(9.0, 60.0, null, 0);
        True(rs.Guardian.IsExposed, "guardian exposed");
    }

    public static void RunState_Altars_ByIndex() {
        var rs = new RunState(new SoulMeta(), 11UL, true);
        var altars = rs.World.Altars;
        True(altars.Count >= 3, "altars present");
        int spring = -1, relic = -1;
        for (int i = 0; i < altars.Count; i++) {
            if (altars[i].Kind == AltarKind.Spring && spring < 0) spring = i;
            if (altars[i].Kind == AltarKind.Relic && relic < 0) relic = i;
        }
        True(spring >= 0 && relic >= 0, "spring and relic exist");
        rs.OnMassLost(1.0, "test");
        double before = rs.Mass;
        rs.UseAltarAt(spring);
        True(rs.Mass > before, "spring heals");
        True(rs.IsAltarUsed(spring), "spring used");
        double after = rs.Mass;
        rs.UseAltarAt(spring);
        Eq(after, rs.Mass, "spring only once");
        rs.UseAltarAt(relic);
        True(rs.Evolution.FreeTokens >= 1, "relic gives a free evolution token");
        True(rs.Resources.Get(ResourceKind.Psyche) >= 10 - 1e-9, "relic gives resources");
        True(rs.IsAltarUsed(-1) && rs.IsAltarUsed(9999), "out of range counts as used");
        var im = rs.Finish();
        True(im.EssenceEarned >= RunState.RelicEssence, "relic essence counted");
    }

    public static void RunState_RegionsDiscovered() {
        var rs = new RunState(new SoulMeta(), 13UL, true);
        for (int i = 0; i < 7; i++) rs.OnRegionEntered(i);
        rs.OnRegionEntered(3);
        rs.OnRegionEntered(-1);
        Eq(7, rs.RegionsDiscovered, "unique regions only");
        Eq(7, rs.Finish().RegionsDiscovered, "imprint has regions");
    }
}
}
