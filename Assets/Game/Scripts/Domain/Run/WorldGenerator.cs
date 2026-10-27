using System.Collections.Generic;
using PM.Domain.Core;

namespace PM.Domain.Run {
public enum RegionKind { Cavern = 0, Start = 1, Exit = 2, Maze = 3, Shrine = 4, Spring = 5, Elite = 6,
                         Lair = 7, Secret = 8, BossLair = 9 }

public sealed class RegionInfo {
    public int Id;
    public RegionKind Kind;
    public string Title;
    public Vec2 Center;
    public double Radius;
    public int MinX, MinY, MaxX, MaxY;
}

public enum AltarKind { Shrine = 0, Spring = 1, Relic = 2 }
public struct AltarSpec { public AltarKind Kind; public Vec2 Pos; public int RegionId; }
public struct WallRect { public int X, Y, W, H; }

/// <summary>Огромное подземелье одной глубины (v0.6). Клетка = 1 мировая единица, начало — левый нижний угол.</summary>
public sealed class WorldMap {
    public int Width, Height;
    public byte[] Cells;          // 0 стена, 1 пол
    public short[] RegionOf;      // id региона или -1 (тоннели)
    public Vec2 PlayerStart, ExitPos, BossPos;
    public List<RegionInfo> Regions = new List<RegionInfo>();
    public List<AltarSpec> Altars = new List<AltarSpec>();
    public List<SpawnSpec> Spawns = new List<SpawnSpec>();
    public List<ObstacleSpec> Obstacles = new List<ObstacleSpec>();
    public List<WallRect> WallRects = new List<WallRect>();
    public List<Vec2> CrackMouths = new List<Vec2>();   // входы в узкие трещины (подсказка: «разделитесь»)
    public double GoalMass;

    public bool IsFloor(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && Cells[y * Width + x] == 1;
    public Vec2 CellCenter(int x, int y) => new Vec2(x + 0.5, y + 0.5);
    public bool CellOf(Vec2 p, out int x, out int y) {
        x = (int)System.Math.Floor(p.X);
        y = (int)System.Math.Floor(p.Y);
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }
}

/// <summary>
/// Генератор огромных подземелий (v0.6): органичные залы (клеточный автомат), извилистые тоннели
/// по остовному дереву + петли, идеальные лабиринты с реликвией в центре, тайники, гарантированная
/// связность, опасность растёт с BFS-дистанцией от старта. Детерминирован по seed.
/// </summary>
public static class WorldGenerator {
    public const int Border = 3;
    private const int MazeCell = 3; // коридор 2 + стена 1

    private sealed class Chamber { public int Cx, Cy; public double R; public int Region; }

    public const double ShimmerChance = 0.12;       // v0.10: доля мерцающих существ
    public const double EliteShimmerChance = 0.3;   // в элитных гнёздах — чаще (риск ради добычи)

    /// <summary>
    /// v0.10: часть существ мерцает (+50% ресурсов). Отдельный поток случайных чисел — карта,
    /// позиции и массы не меняются. Споры и души-немезиды не мерцают.
    /// </summary>
    private static void AssignShimmer(WorldMap m, ulong seed) {
        var vr = new PmRandom(seed ^ 0x5A1E57A11UL);
        for (int i = 0; i < m.Spawns.Count; i++) {
            var s = m.Spawns[i];
            if (s.SpeciesId == "spore" || s.IsNemesis) continue;
            int cx = (int)s.Pos.X, cy = (int)s.Pos.Y;
            int reg = cx >= 0 && cy >= 0 && cx < m.Width && cy < m.Height ? m.RegionOf[cy * m.Width + cx] : -1;
            double chance = reg >= 0 && m.Regions[reg].Kind == RegionKind.Elite ? EliteShimmerChance : ShimmerChance;
            if (vr.NextDouble01() < chance) { s.Variant = SpawnVariant.Shimmering; m.Spawns[i] = s; }
        }
    }

    public static WorldMap Generate(int depth, ulong seed, NemesisRegistry nemeses) {
        if (depth < 1) depth = 1;
        if (depth > RunState.MaxDepth) depth = RunState.MaxDepth;
        var rng = new PmRandom(seed ^ 0xC0FFEE1234UL);
        var biome = BiomeCatalog.ForDepth(depth);
        int w = 140 + 15 * (depth - 1), h = 90 + 10 * (depth - 1);
        var m = new WorldMap { Width = w, Height = h, Cells = new byte[w * h], RegionOf = new short[w * h] };
        for (int i = 0; i < m.RegionOf.Length; i++) m.RegionOf[i] = -1;

        // ---------- 1. Залы ----------
        var chambers = new List<Chamber>();
        int target = 12 + (depth - 1) * 3 / 2;             // 12..18
        for (int tries = 0; tries < 4000 && chambers.Count < target; tries++) {
            double r = rng.Range(5, 12);
            int cx = (int)rng.Range(Border + r + 2, w - Border - r - 2);
            int cy = (int)rng.Range(Border + r + 2, h - Border - r - 2);
            bool ok = true;
            for (int i = 0; i < chambers.Count; i++) {
                double dx = chambers[i].Cx - cx, dy = chambers[i].Cy - cy;
                if (dx * dx + dy * dy < 16 * 16 || System.Math.Sqrt(dx * dx + dy * dy) < r + chambers[i].R + 4) { ok = false; break; }
            }
            if (!ok) continue;
            var ch = new Chamber { Cx = cx, Cy = cy, R = r, Region = m.Regions.Count };
            chambers.Add(ch);
            m.Regions.Add(new RegionInfo { Id = ch.Region, Kind = RegionKind.Cavern, Center = new Vec2(cx + 0.5, cy + 0.5), Radius = r });
            CarveChamber(m, ch, rng);
        }

        // ---------- 2. Тоннели: остовное дерево (Прим) + 25% петель ----------
        int n = chambers.Count;
        var inTree = new bool[n];
        var edges = new List<int[]>();
        inTree[0] = true;
        for (int k = 1; k < n; k++) {
            double best = double.MaxValue; int bi = -1, bj = -1;
            for (int i = 0; i < n; i++) if (inTree[i])
                for (int j = 0; j < n; j++) if (!inTree[j]) {
                    double d = Dist2(chambers[i], chambers[j]);
                    if (d < best) { best = d; bi = i; bj = j; }
                }
            inTree[bj] = true;
            edges.Add(new[] { bi, bj });
        }
        int extra = System.Math.Max(1, n / 4);
        for (int e = 0, guard = 0; e < extra && guard < 400; guard++) {
            int i = rng.NextInt(n);
            int j = NearestOther(chambers, i, rng.NextInt(3));
            if (j < 0 || HasEdge(edges, i, j)) continue;
            edges.Add(new[] { i, j });
            e++;
        }
        for (int e = 0; e < edges.Count; e++) {
            var a = chambers[edges[e][0]]; var b = chambers[edges[e][1]];
            CarveTunnel(m, a.Cx, a.Cy, b.Cx, b.Cy, 1 + rng.NextInt(2), rng, true);
        }

        // ---------- 3. Лабиринты ----------
        int mazes = depth == 1 ? 2 : depth <= 3 ? 3 : 4;
        var mazeRelics = new List<AltarSpec>();
        for (int k = 0, tries = 0; k < mazes && tries < 600; tries++) {
            int mw = 7, mh = 5;                                    // ячеек → 22×16 клеток
            int rw = mw * MazeCell + 1, rh = mh * MazeCell + 1;
            int x0 = (int)rng.Range(Border + 2, w - Border - rw - 2), y0 = (int)rng.Range(Border + 2, h - Border - rh - 2);
            if (!AreaIsSolid(m, x0 - 3, y0 - 3, rw + 6, rh + 6)) continue;
            int region = m.Regions.Count;
            var info = new RegionInfo { Id = region, Kind = RegionKind.Maze, Title = MazeTitle(biome.Id, k),
                Center = new Vec2(x0 + rw * 0.5, y0 + rh * 0.5), Radius = System.Math.Max(rw, rh) * 0.5,
                MinX = x0, MinY = y0, MaxX = x0 + rw - 1, MaxY = y0 + rh - 1 };
            m.Regions.Add(info);
            CarveMaze(m, x0, y0, mw, mh, region, rng);
            // центр — реликвия
            int ccx = mw / 2, ccy = mh / 2;
            var relicPos = new Vec2(x0 + ccx * MazeCell + 2.0, y0 + ccy * MazeCell + 2.0);
            mazeRelics.Add(new AltarSpec { Kind = AltarKind.Relic, Pos = relicPos, RegionId = region });
            // 1–2 входа на краях, соединённые с ближайшим залом
            int entrances = 1 + rng.NextInt(2);
            for (int en = 0; en < entrances; en++) {
                int side = rng.NextInt(4);
                int ex, ey, ox, oy;
                if (side == 0) { int c = rng.NextInt(mw); ex = x0 + c * MazeCell + 1; ey = y0; ox = ex; oy = ey - 3; }
                else if (side == 1) { int c = rng.NextInt(mw); ex = x0 + c * MazeCell + 1; ey = y0 + rh - 1; ox = ex; oy = ey + 3; }
                else if (side == 2) { int c = rng.NextInt(mh); ex = x0; ey = y0 + c * MazeCell + 1; ox = ex - 3; oy = ey; }
                else { int c = rng.NextInt(mh); ex = x0 + rw - 1; ey = y0 + c * MazeCell + 1; ox = ex + 3; oy = ey; }
                SetFloor(m, ex, ey, region); SetFloor(m, ex + (side >= 2 ? 0 : 1), ey + (side >= 2 ? 1 : 0), region);
                var near = NearestChamber(chambers, ox, oy);
                if (near != null) CarveTunnel(m, ox, oy, near.Cx, near.Cy, 1, rng, true, ex, ey);
            }
            k++;
        }

        // ---------- 4. Тайники ----------
        int secrets = 2 + rng.NextInt(2);
        var secretRegions = new List<int>();
        for (int k = 0, tries = 0; k < secrets && tries < 600; tries++) {
            int r = 3 + rng.NextInt(2);
            int cx = (int)rng.Range(Border + r + 3, w - Border - r - 3), cy = (int)rng.Range(Border + r + 3, h - Border - r - 3);
            if (!AreaIsSolid(m, cx - r - 4, cy - r - 4, 2 * r + 9, 2 * r + 9)) continue;
            int region = m.Regions.Count;
            m.Regions.Add(new RegionInfo { Id = region, Kind = RegionKind.Secret, Title = "Тайник",
                Center = new Vec2(cx + 0.5, cy + 0.5), Radius = r });
            CarveDisc(m, cx, cy, r, region);
            var near = NearestChamber(chambers, cx, cy);
            // тайник доступен только через трещину шириной 1 клетку — целым телом не пролезть
            if (near != null) CarveCrack(m, cx, cy, near.Cx, near.Cy, rng, true);
            secretRegions.Add(region);
            k++;
        }

        // ---------- 4б. Трещины-срезки между залами (только для фрагментов) ----------
        int cracks = 2 + depth;
        for (int k = 0, tries = 0; k < cracks && tries < 200 && chambers.Count > 2; tries++) {
            var a = chambers[rng.NextInt(chambers.Count)];
            var b = chambers[NearestOther(chambers, chambers.IndexOf(a), 1 + rng.NextInt(2))];
            double d = System.Math.Sqrt(Dist2(a, b));
            if (d > 34 || HasEdge(edges, chambers.IndexOf(a), chambers.IndexOf(b))) continue;
            if (CarveCrack(m, a.Cx, a.Cy, b.Cx, b.Cy, rng, false)) k++;
        }

        // ---------- 5. Старт и связность ----------
        Chamber start = chambers[0];
        int bestEdge = int.MaxValue;
        for (int i = 0; i < chambers.Count; i++) {
            var c = chambers[i];
            int edge = System.Math.Min(System.Math.Min(c.Cx, w - 1 - c.Cx), System.Math.Min(c.Cy, h - 1 - c.Cy));
            if (edge < bestEdge) { bestEdge = edge; start = c; }
        }
        int sx = start.Cx, sy = start.Cy;
        CarveDisc(m, sx, sy, 2, start.Region);
        EnsureConnected(m, sx, sy, rng);
        var dist = Bfs(m, sx, sy);
        m.PlayerStart = new Vec2(sx + 0.5, sy + 0.5);
        FillRegionBounds(m);

        // ---------- 6. Назначение регионов ----------
        Chamber exit = start; int far = -1;
        for (int i = 0; i < chambers.Count; i++) {
            var c = chambers[i];
            int d = dist[c.Cy * w + c.Cx];
            if (c != start && d > far) { far = d; exit = c; }
        }
        m.Regions[start.Region].Kind = RegionKind.Start;
        bool bossDepth = depth == RunState.MaxDepth;
        m.Regions[exit.Region].Kind = bossDepth ? RegionKind.BossLair : RegionKind.Exit;
        if (bossDepth) {
            m.BossPos = new Vec2(exit.Cx + 0.5, exit.Cy + 0.5);
            m.ExitPos = FarthestInRegion(m, dist, exit.Region, exit.Cx, exit.Cy);
        } else m.ExitPos = new Vec2(exit.Cx + 0.5, exit.Cy + 0.5);

        var free = new List<Chamber>();
        for (int i = 0; i < chambers.Count; i++) if (chambers[i] != start && chambers[i] != exit) free.Add(chambers[i]);
        Shuffle(free, rng);
        int fi = 0;
        int shrines = depth >= 3 ? 2 : 1, springs = depth >= 3 ? 2 : 1, elites = 1 + (depth >= 3 ? 1 : 0);
        for (int k = 0; k < shrines && fi < free.Count; k++) m.Regions[free[fi++].Region].Kind = RegionKind.Shrine;
        for (int k = 0; k < springs && fi < free.Count; k++) m.Regions[free[fi++].Region].Kind = RegionKind.Spring;
        for (int k = 0; k < elites && fi < free.Count; k++) m.Regions[free[fi++].Region].Kind = RegionKind.Elite;
        NemesisRecord nemesis = null;
        if (nemeses != null && fi < free.Count) {
            for (int i = 0; i < nemeses.Records.Count; i++)
                if (!nemeses.Records[i].Defeated) { nemesis = nemeses.Records[rng.NextInt(nemeses.Records.Count)]; break; }
            if (nemesis != null && nemesis.Defeated) nemesis = null;
            if (nemesis != null) m.Regions[free[fi++].Region].Kind = RegionKind.Lair;
        }
        for (int i = 0; i < m.Regions.Count; i++) {
            var r = m.Regions[i];
            if (r.Title == null) r.Title = RegionTitle(r.Kind, biome.Id, nemesis, rng);
        }

        // ---------- 7. Алтари ----------
        for (int i = 0; i < m.Regions.Count; i++) {
            var r = m.Regions[i];
            if (r.Kind == RegionKind.Shrine || r.Kind == RegionKind.Spring)
                m.Altars.Add(new AltarSpec { Kind = r.Kind == RegionKind.Shrine ? AltarKind.Shrine : AltarKind.Spring,
                    Pos = NearestRegionFloor(m, r), RegionId = i });
        }
        for (int i = 0; i < mazeRelics.Count; i++) {
            var a = mazeRelics[i]; a.Pos = NearestFloor(m, a.Pos); m.Altars.Add(a);
        }
        var secretHeaps = new List<int>();
        for (int i = 0; i < secretRegions.Count; i++) {
            var r = m.Regions[secretRegions[i]];
            if (rng.NextDouble01() < 0.6)
                m.Altars.Add(new AltarSpec { Kind = AltarKind.Relic, Pos = NearestFloor(m, r.Center), RegionId = r.Id });
            else secretHeaps.Add(r.Id);
        }

        // ---------- 8. Препятствия (только залы) ----------
        var solidBlocked = new bool[w * h];
        for (int i = 0; i < chambers.Count; i++) {
            var c = chambers[i];
            var kind = m.Regions[c.Region].Kind;
            int count = 2 + rng.NextInt(4);
            for (int k = 0, tries = 0; k < count && tries < 40; tries++) {
                var ok = biome.ObstaclePool[rng.NextInt(biome.ObstaclePool.Length)];
                bool solid = ObstacleRules.IsSolid(ok);
                double rad = solid ? rng.Range(0.5, 1.3) : rng.Range(1.2, 2.2);
                double ang = rng.Range(0, System.Math.PI * 2), dd = rng.Range(0, c.R * 0.7);
                var p = new Vec2(c.Cx + 0.5 + System.Math.Cos(ang) * dd, c.Cy + 0.5 + System.Math.Sin(ang) * dd);
                if (!Clear(m, p, rad + 0.6)) continue;
                if (!m.CellOf(p, out int ocx, out int ocy) || m.RegionOf[ocy * w + ocx] != c.Region) continue; // только в самом зале
                if ((p - m.PlayerStart).Length < rad + 2.5 || (p - m.ExitPos).Length < rad + 2.5) continue;
                if (bossDepth && kind == RegionKind.BossLair && (p - m.BossPos).Length < rad + 4) continue;
                bool near = false;
                for (int a = 0; a < m.Altars.Count && !near; a++) if ((m.Altars[a].Pos - p).Length < rad + 2) near = true;
                for (int o = 0; o < m.Obstacles.Count && !near; o++) if ((m.Obstacles[o].Pos - p).Length < rad + m.Obstacles[o].Radius + 0.6) near = true;
                if (near) continue;
                if (solid) {
                    MarkDisc(solidBlocked, w, h, p, rad);
                    if (!KeyPointsReachable(m, solidBlocked, sx, sy)) { RebuildBlocked(solidBlocked, m); continue; }
                }
                m.Obstacles.Add(new ObstacleSpec { Kind = ok, Pos = p, Radius = rad });
                k++;
            }
        }

        // ---------- 9. Спавны ----------
        int maxDist = 1;
        for (int i = 0; i < dist.Length; i++) if (dist[i] > maxDist && dist[i] < int.MaxValue) maxDist = dist[i];
        var pool = SpeciesPoolFor(depth, biome);
        int floorCount = 0;
        for (int i = 0; i < m.Cells.Length; i++) if (m.Cells[i] == 1) floorCount++;
        // споры
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) {
                int i = y * w + x;
                if (m.Cells[i] != 1 || solidBlocked[i]) continue;
                var p = new Vec2(x + 0.5 + rng.Range(-0.3, 0.3), y + 0.5 + rng.Range(-0.3, 0.3));
                bool heap = m.RegionOf[i] >= 0 && secretHeaps.Contains(m.RegionOf[i]);
                if (rng.NextDouble01() > (heap ? 0.35 : 1.0 / 22.0)) continue;
                if ((p - m.PlayerStart).Length < 6) continue;
                m.Spawns.Add(new SpawnSpec { SpeciesId = "spore", Pos = p, Mass = 0.3 });
            }
        // существа: плотность и опасность растут к шахте
        int creatures = (int)(floorCount / 70.0 * (1.0 + 0.15 * (depth - 1)));
        int souls = 0;
        for (int k = 0, tries = 0; k < creatures && tries < creatures * 30; tries++) {
            int x = rng.NextInt(w), y = rng.NextInt(h);
            int i = y * w + x;
            if (m.Cells[i] != 1 || solidBlocked[i] || dist[i] == int.MaxValue) continue;
            double t = (double)dist[i] / maxDist;
            if (rng.NextDouble01() > 0.25 + 0.75 * t) continue;          // ближе к шахте — гуще
            var p = new Vec2(x + 0.5, y + 0.5);
            if ((p - m.PlayerStart).Length < 8) continue;
            int idx = (int)(t * (pool.Count - 1) + rng.Range(-1.2, 1.2));
            if (idx < 0) idx = 0; if (idx >= pool.Count) idx = pool.Count - 1;
            var sp = pool[idx];
            double mass;
            if (sp.Id == "soul_slime") {
                if (souls >= 2 || (p - m.PlayerStart).Length < 25) continue;
                souls++;
                mass = sp.MassAtDepth(depth) * rng.Range(0.9, 1.1); // v0.14: кривая вместо спец-формулы
            } else mass = sp.MassAtDepth(depth) * rng.Range(0.85, 1.15); // v0.14: свой темп роста у каждого вида
            int reg = m.RegionOf[i];
            if (reg >= 0 && m.Regions[reg].Kind == RegionKind.Elite) mass *= RunState.EliteMassMult;
            if (reg >= 0 && (m.Regions[reg].Kind == RegionKind.Start || m.Regions[reg].Kind == RegionKind.Maze)) continue;
            if (!Clear(m, p, SizeRule.RadiusForMass(mass) + 0.2)) continue;
            m.Spawns.Add(new SpawnSpec { SpeciesId = sp.Id, Pos = p, Mass = mass });
            k++;
        }
        // элитным гнёздам — гарантированные обитатели
        for (int r = 0; r < m.Regions.Count; r++) {
            if (m.Regions[r].Kind != RegionKind.Elite) continue;
            for (int k = 0; k < 3; k++) {
                var sp = pool[System.Math.Max(0, pool.Count - 1 - rng.NextInt(2))];
                if (sp.Id == "soul_slime") sp = pool[0];
                var p = NearestFloor(m, m.Regions[r].Center + new Vec2(rng.Range(-3, 3), rng.Range(-3, 3)));
                double mass = sp.MassAtDepth(depth) * RunState.EliteMassMult;
                if (Clear(m, p, SizeRule.RadiusForMass(mass) + 0.1)) m.Spawns.Add(new SpawnSpec { SpeciesId = sp.Id, Pos = p, Mass = mass });
            }
        }
        // немезида в своём логове
        if (nemesis != null) {
            for (int r = 0; r < m.Regions.Count; r++)
                if (m.Regions[r].Kind == RegionKind.Lair) {
                    double pm = 4.0 + 3.0 * (depth - 1);
                    m.Spawns.Add(new SpawnSpec { SpeciesId = "soul_slime", Pos = NearestRegionFloor(m, m.Regions[r]),
                        Mass = NemesisRegistry.NemesisMass(nemesis, pm), IsNemesis = true, SoulName = nemesis.SoulName });
                    break;
                }
        }
        // спавны внутри твёрдых препятствий — убрать
        for (int s = m.Spawns.Count - 1; s >= 0; s--) {
            var sp = m.Spawns[s];
            if (sp.IsNemesis) continue; // немезида ждёт в логове — двигаем препятствия, не её
            for (int o = 0; o < m.Obstacles.Count; o++) {
                var ob = m.Obstacles[o];
                if (ObstacleRules.IsSolid(ob.Kind) && (ob.Pos - sp.Pos).Length < ob.Radius + SizeRule.RadiusForMass(sp.Mass) + 0.3) {
                    m.Spawns.RemoveAt(s); break;
                }
            }
        }

        // ---------- 10. Цель шахты и коллайдеры ----------
        // v0.14: цель табличная с допуском ±10% от карты (Docs/BALANCE_v14.md §2.2):
        // близкая к таблице карта работает как раньше (25% массы), выбившаяся — зажимается.
        double total = 0;
        for (int s = 0; s < m.Spawns.Count; s++) total += m.Spawns[s].Mass;
        double goalTarget = RunState.GoalMassFor(depth);
        double mapGoal = total * 0.25;
        double goal = mapGoal;
        if (goal > goalTarget * 1.1) goal = goalTarget * 1.1;
        if (goal < goalTarget * 0.9) goal = goalTarget * 0.9;
        m.GoalMass = System.Math.Max(6.0, goal);
        BuildWallRects(m);
        AssignShimmer(m, seed);
        return m;
    }

    // ===================== Резьба =====================
    private static void CarveChamber(WorldMap m, Chamber ch, PmRandom rng) {
        int w = m.Width, h = m.Height;
        int shape = rng.NextInt(3);
        double rx = ch.R, ry = ch.R;
        if (shape == 1) { rx = ch.R * rng.Range(1.1, 1.5); ry = ch.R * rng.Range(0.6, 0.9); }
        var blobs = new List<double[]>();
        blobs.Add(new[] { (double)ch.Cx, ch.Cy, 1.0 });
        if (shape == 2) {
            int extra = 1 + rng.NextInt(3);
            for (int k = 0; k < extra; k++) {
                double a = rng.Range(0, System.Math.PI * 2), d = ch.R * rng.Range(0.3, 0.6);
                blobs.Add(new[] { ch.Cx + System.Math.Cos(a) * d, ch.Cy + System.Math.Sin(a) * d, rng.Range(0.5, 0.8) });
            }
        }
        int ext = (int)(System.Math.Max(rx, ry) * 1.6) + 2;
        int x0 = System.Math.Max(Border, ch.Cx - ext), x1 = System.Math.Min(w - Border - 1, ch.Cx + ext);
        int y0 = System.Math.Max(Border, ch.Cy - ext), y1 = System.Math.Min(h - Border - 1, ch.Cy + ext);
        int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
        var grid = new bool[bw * bh];
        var inside = new double[bw * bh];
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) {
                double best = 9;
                for (int b = 0; b < blobs.Count; b++) {
                    double sc = blobs[b][2];
                    double dx = (x - blobs[b][0]) / (rx * sc), dy = (y - blobs[b][1]) / (ry * sc);
                    double v = System.Math.Sqrt(dx * dx + dy * dy);
                    if (v < best) best = v;
                }
                int li = (y - y0) * bw + (x - x0);
                inside[li] = best;
                double p = best < 0.55 ? 0.97 : best < 1.0 ? 0.62 : best < 1.25 ? 0.25 : 0.0;
                grid[li] = rng.NextDouble01() < p;
            }
        for (int it = 0; it < 5; it++) {
            var next = new bool[grid.Length];
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++) {
                    int c = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++) {
                            if (dx == 0 && dy == 0) continue;
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= bw || yy >= bh) continue;
                            if (grid[yy * bw + xx]) c++;
                        }
                    int li = y * bw + x;
                    bool v = grid[li] ? c >= 4 : c >= 5;
                    if (inside[li] > 1.3) v = false;
                    next[li] = v;
                }
            grid = next;
        }
        for (int y = 0; y < bh; y++)
            for (int x = 0; x < bw; x++)
                if (grid[y * bw + x]) SetFloor(m, x0 + x, y0 + y, ch.Region);
        CarveDisc(m, ch.Cx, ch.Cy, 2, ch.Region);
    }

    /// <summary>Извилистый тоннель: блуждание к цели с шумом, иногда «карманы».</summary>
    private static void CarveTunnel(WorldMap m, int ax, int ay, int bx, int by, int radius, PmRandom rng, bool pockets,
                                    int fromX = int.MinValue, int fromY = int.MinValue) {
        double x = ax + 0.5, y = ay + 0.5;
        if (fromX != int.MinValue) CarveLine(m, fromX, fromY, ax, ay, radius);
        double wobble = rng.Range(0, System.Math.PI * 2);
        for (int step = 0; step < 4000; step++) {
            double dx = bx + 0.5 - x, dy = by + 0.5 - y;
            double d = System.Math.Sqrt(dx * dx + dy * dy);
            if (d < 1.5) break;
            wobble += rng.Range(-0.5, 0.5);
            double ang = System.Math.Atan2(dy, dx) + System.Math.Sin(wobble) * 0.9;
            x += System.Math.Cos(ang); y += System.Math.Sin(ang);
            int r = radius + (pockets && rng.NextDouble01() < 0.04 ? 2 : 0);
            CarveDisc(m, (int)x, (int)y, r, -1);
        }
        CarveDisc(m, bx, by, radius, -1);
    }

    /// <summary>
    /// Трещина шириной ровно 1 клетка, 4-связная (по диагонали физика не пропустит), через случайную
    /// середину — слегка изломанная. Не режет лабиринты. Отмечает входы (CrackMouths).
    /// </summary>
    private static bool CarveCrack(WorldMap m, int ax, int ay, int bx, int by, PmRandom rng, bool force) {
        int mx = (ax + bx) / 2 + rng.NextInt(7) - 3, my = (ay + by) / 2 + rng.NextInt(7) - 3;
        var cells = new List<int>();
        CrackPath(m, ax, ay, mx, my, cells);
        CrackPath(m, mx, my, bx, by, cells);
        for (int i = 0; i < cells.Count; i++) {
            int reg = m.RegionOf[cells[i]];
            if (reg >= 0 && m.Regions[reg].Kind == RegionKind.Maze) return false; // лабиринт не портим
        }
        bool wasWall = false, mouthsAdded = false;
        for (int i = 0; i < cells.Count; i++) {
            int c = cells[i];
            int x = c % m.Width, y = c / m.Width;
            if (x < Border || y < Border || x >= m.Width - Border || y >= m.Height - Border) continue;
            bool wall = m.Cells[c] == 0;
            if (wall && !wasWall && i > 0) { m.CrackMouths.Add(new Vec2(x + 0.5, y + 0.5)); mouthsAdded = true; }
            if (!wall && wasWall) m.CrackMouths.Add(new Vec2(x + 0.5, y + 0.5));
            m.Cells[c] = 1;
            wasWall = wall;
        }
        return mouthsAdded || force;
    }

    private static void CrackPath(WorldMap m, int x0, int y0, int x1, int y1, List<int> cells) {
        int x = x0, y = y0;
        int dx0 = System.Math.Abs(x1 - x0), dy0 = System.Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        cells.Add(y * m.Width + x);
        while (x != x1 || y != y1) {
            int rx = System.Math.Abs(x1 - x), ry = System.Math.Abs(y1 - y);
            // ровно один шаг за раз — по оси x или y, никогда диагонально; доля пути по осям выравнивается
            if (ry == 0 || (rx > 0 && (long)rx * dy0 >= (long)ry * dx0)) x += sx; else y += sy;
            cells.Add(y * m.Width + x);
        }
    }

    private static void CarveLine(WorldMap m, int ax, int ay, int bx, int by, int radius) {
        int steps = System.Math.Max(System.Math.Abs(bx - ax), System.Math.Abs(by - ay));
        for (int s = 0; s <= steps; s++) {
            double t = steps == 0 ? 0 : (double)s / steps;
            CarveDisc(m, (int)System.Math.Round(ax + (bx - ax) * t), (int)System.Math.Round(ay + (by - ay) * t), radius, -1);
        }
    }

    private static void CarveDisc(WorldMap m, int cx, int cy, int r, int region) {
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r + r) SetFloor(m, x, y, region);
    }

    private static void SetFloor(WorldMap m, int x, int y, int region) {
        if (x < Border || y < Border || x >= m.Width - Border || y >= m.Height - Border) return;
        int i = y * m.Width + x;
        m.Cells[i] = 1;
        if (region >= 0 && m.RegionOf[i] < 0) m.RegionOf[i] = (short)region;
    }

    /// <summary>Идеальный лабиринт (recursive backtracker): коридоры 2, стены 1.</summary>
    private static void CarveMaze(WorldMap m, int x0, int y0, int mw, int mh, int region, PmRandom rng) {
        var seen = new bool[mw * mh];
        var stack = new Stack<int>();
        stack.Push(0); seen[0] = true;
        CarveMazeCell(m, x0, y0, 0, 0, region);
        var nbr = new int[4];
        while (stack.Count > 0) {
            int cur = stack.Peek();
            int cx = cur % mw, cy = cur / mw, cnt = 0;
            if (cx > 0 && !seen[cur - 1]) nbr[cnt++] = cur - 1;
            if (cx < mw - 1 && !seen[cur + 1]) nbr[cnt++] = cur + 1;
            if (cy > 0 && !seen[cur - mw]) nbr[cnt++] = cur - mw;
            if (cy < mh - 1 && !seen[cur + mw]) nbr[cnt++] = cur + mw;
            if (cnt == 0) { stack.Pop(); continue; }
            int nx = nbr[rng.NextInt(cnt)];
            seen[nx] = true;
            int ncx = nx % mw, ncy = nx / mw;
            CarveMazeCell(m, x0, y0, ncx, ncy, region);
            // проём между ячейками
            int px = x0 + 1 + System.Math.Min(cx, ncx) * MazeCell, py = y0 + 1 + System.Math.Min(cy, ncy) * MazeCell;
            if (ncx != cx) for (int dy = 0; dy < 2; dy++) SetFloor(m, px + 2, py + dy, region);
            else for (int dx = 0; dx < 2; dx++) SetFloor(m, px + dx, py + 2, region);
            stack.Push(nx);
        }
    }

    private static void CarveMazeCell(WorldMap m, int x0, int y0, int cx, int cy, int region) {
        int bx = x0 + 1 + cx * MazeCell, by = y0 + 1 + cy * MazeCell;
        for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) SetFloor(m, bx + dx, by + dy, region);
    }

    // ===================== Связность и анализ =====================
    public static int[] Bfs(WorldMap m, int sx, int sy, bool[] blocked = null) {
        int w = m.Width, h = m.Height;
        var dist = new int[w * h];
        for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
        var q = new Queue<int>();
        int s = sy * w + sx;
        if (m.Cells[s] != 1) return dist;
        dist[s] = 0; q.Enqueue(s);
        while (q.Count > 0) {
            int i = q.Dequeue();
            int x = i % w, y = i / w, nd = dist[i] + 1;
            Visit(m, blocked, dist, q, x - 1, y, nd); Visit(m, blocked, dist, q, x + 1, y, nd);
            Visit(m, blocked, dist, q, x, y - 1, nd); Visit(m, blocked, dist, q, x, y + 1, nd);
        }
        return dist;
    }

    private static void Visit(WorldMap m, bool[] blocked, int[] dist, Queue<int> q, int x, int y, int nd) {
        if (x < 0 || y < 0 || x >= m.Width || y >= m.Height) return;
        int i = y * m.Width + x;
        if (m.Cells[i] != 1 || dist[i] != int.MaxValue || (blocked != null && blocked[i])) return;
        dist[i] = nd; q.Enqueue(i);
    }

    /// <summary>Недостижимые острова пола соединяем тоннелем; если не вышло — засыпаем.</summary>
    private static void EnsureConnected(WorldMap m, int sx, int sy, PmRandom rng) {
        int w = m.Width;
        for (int pass = 0; pass < 6; pass++) {
            var dist = Bfs(m, sx, sy);
            int island = -1;
            for (int i = 0; i < dist.Length; i++) if (m.Cells[i] == 1 && dist[i] == int.MaxValue) { island = i; break; }
            if (island < 0) return;
            // ближайшая достижимая клетка (по прямой — дёшево и достаточно)
            int ix = island % w, iy = island / w, best = -1; double bd = double.MaxValue;
            for (int i = 0; i < dist.Length; i += 3) {
                if (dist[i] == int.MaxValue) continue;
                int x = i % w, y = i / w;
                double d = (x - ix) * (x - ix) + (y - iy) * (y - iy);
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0) CarveLine(m, ix, iy, best % w, best / w, 1);
        }
        var final = Bfs(m, sx, sy);
        for (int i = 0; i < final.Length; i++) if (m.Cells[i] == 1 && final[i] == int.MaxValue) { m.Cells[i] = 0; m.RegionOf[i] = -1; }
    }

    private static bool KeyPointsReachable(WorldMap m, bool[] blocked, int sx, int sy) {
        var d = Bfs(m, sx, sy, blocked);
        if (!Reached(m, d, m.ExitPos)) return false;
        for (int a = 0; a < m.Altars.Count; a++) if (!Reached(m, d, m.Altars[a].Pos)) return false;
        for (int r = 0; r < m.Regions.Count; r++) {
            var c = NearestFloor(m, m.Regions[r].Center);
            if (!Reached(m, d, c)) return false;
        }
        return true;
    }

    private static bool Reached(WorldMap m, int[] d, Vec2 p) =>
        m.CellOf(p, out int x, out int y) && d[y * m.Width + x] != int.MaxValue;

    private static void MarkDisc(bool[] b, int w, int h, Vec2 p, double r) {
        int r1 = (int)System.Math.Ceiling(r);
        int cx = (int)p.X, cy = (int)p.Y;
        for (int y = cy - r1; y <= cy + r1; y++)
            for (int x = cx - r1; x <= cx + r1; x++) {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                double dx = x + 0.5 - p.X, dy = y + 0.5 - p.Y;
                if (dx * dx + dy * dy <= (r + 0.5) * (r + 0.5)) b[y * w + x] = true;
            }
    }

    /// <summary>Пересчёт маски твёрдых препятствий из уже принятых (после отката кандидата).</summary>
    private static void RebuildBlocked(bool[] b, WorldMap m) {
        for (int i = 0; i < b.Length; i++) b[i] = false;
        for (int o = 0; o < m.Obstacles.Count; o++)
            if (ObstacleRules.IsSolid(m.Obstacles[o].Kind)) MarkDisc(b, m.Width, m.Height, m.Obstacles[o].Pos, m.Obstacles[o].Radius);
    }

    private static bool Clear(WorldMap m, Vec2 p, double r) {
        int r1 = (int)System.Math.Ceiling(r);
        int cx = (int)p.X, cy = (int)p.Y;
        for (int y = cy - r1; y <= cy + r1; y++)
            for (int x = cx - r1; x <= cx + r1; x++) {
                double dx = x + 0.5 - p.X, dy = y + 0.5 - p.Y;
                if (dx * dx + dy * dy > (r + 0.5) * (r + 0.5)) continue;
                if (!m.IsFloor(x, y)) return false;
            }
        return true;
    }

    public static Vec2 NearestFloor(WorldMap m, Vec2 p) {
        int cx = (int)p.X, cy = (int)p.Y;
        for (int r = 0; r < 30; r++)
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++) {
                    if (System.Math.Abs(x - cx) != r && System.Math.Abs(y - cy) != r) continue;
                    if (m.IsFloor(x, y)) return new Vec2(x + 0.5, y + 0.5);
                }
        return p;
    }

    /// <summary>Ближайшая к центру клетка пола, принадлежащая именно этому региону.</summary>
    public static Vec2 NearestRegionFloor(WorldMap m, RegionInfo r) {
        int cx = (int)r.Center.X, cy = (int)r.Center.Y;
        for (int rad = 0; rad < 40; rad++)
            for (int y = cy - rad; y <= cy + rad; y++)
                for (int x = cx - rad; x <= cx + rad; x++) {
                    if (System.Math.Abs(x - cx) != rad && System.Math.Abs(y - cy) != rad) continue;
                    if (m.IsFloor(x, y) && m.RegionOf[y * m.Width + x] == r.Id) return new Vec2(x + 0.5, y + 0.5);
                }
        return NearestFloor(m, r.Center);
    }

    private static Vec2 FarthestInRegion(WorldMap m, int[] dist, int region, int cx, int cy) {
        int best = -1, bi = cy * m.Width + cx;
        for (int i = 0; i < dist.Length; i++)
            if (m.RegionOf[i] == region && dist[i] != int.MaxValue && dist[i] > best) { best = dist[i]; bi = i; }
        // не вплотную к стене: шаг назад к центру
        int x = bi % m.Width, y = bi / m.Width;
        double vx = cx - x, vy = cy - y, len = System.Math.Sqrt(vx * vx + vy * vy);
        if (len > 2) { x += (int)(vx / len * 2); y += (int)(vy / len * 2); }
        return NearestFloor(m, new Vec2(x + 0.5, y + 0.5));
    }

    private static void FillRegionBounds(WorldMap m) {
        foreach (var r in m.Regions) { r.MinX = int.MaxValue; r.MinY = int.MaxValue; r.MaxX = -1; r.MaxY = -1; }
        for (int i = 0; i < m.RegionOf.Length; i++) {
            int reg = m.RegionOf[i];
            if (reg < 0 || m.Cells[i] != 1) continue;
            var r = m.Regions[reg];
            int x = i % m.Width, y = i / m.Width;
            if (x < r.MinX) r.MinX = x; if (y < r.MinY) r.MinY = y;
            if (x > r.MaxX) r.MaxX = x; if (y > r.MaxY) r.MaxY = y;
        }
        foreach (var r in m.Regions) if (r.MaxX < 0) { r.MinX = r.MaxX = (int)r.Center.X; r.MinY = r.MaxY = (int)r.Center.Y; }
    }

    private static bool AreaIsSolid(WorldMap m, int x0, int y0, int ww, int hh) {
        if (x0 < Border || y0 < Border || x0 + ww > m.Width - Border || y0 + hh > m.Height - Border) return false;
        for (int y = y0; y < y0 + hh; y++)
            for (int x = x0; x < x0 + ww; x++)
                if (m.Cells[y * m.Width + x] == 1) return false;
        return true;
    }

    /// <summary>Жадное слияние стеновых клеток в прямоугольники (толща стен — крупными блоками).</summary>
    private static void BuildWallRects(WorldMap m) {
        int w = m.Width, h = m.Height;
        var used = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) {
                int i = y * w + x;
                if (m.Cells[i] == 1 || used[i]) continue;
                int rw = 1;
                while (x + rw < w && m.Cells[i + rw] == 0 && !used[i + rw]) rw++;
                int rh = 1;
                while (y + rh < h) {
                    bool full = true;
                    int row = (y + rh) * w + x;
                    for (int k = 0; k < rw; k++) if (m.Cells[row + k] == 1 || used[row + k]) { full = false; break; }
                    if (!full) break;
                    rh++;
                }
                for (int yy = y; yy < y + rh; yy++) for (int xx = x; xx < x + rw; xx++) used[yy * w + xx] = true;
                m.WallRects.Add(new WallRect { X = x, Y = y, W = rw, H = rh });
            }
    }

    // ===================== Наполнение =====================
    private static List<SpeciesDef> SpeciesPoolFor(int depth, BiomeDef biome) {
        var list = new List<SpeciesDef>();
        var all = SpeciesCatalog.All();
        for (int i = 0; i < all.Length; i++) {
            var s = all[i];
            if (s.Behavior == CreatureBehavior.Food || s.Id == BossState.SpeciesId) continue;
            bool inBiome = false;
            if (biome.SpeciesPool != null) for (int k = 0; k < biome.SpeciesPool.Length; k++) if (biome.SpeciesPool[k] == s.Id) inBiome = true;
            if (inBiome || (s.MinDepth <= depth && depth <= s.MaxDepth)) list.Add(s);
        }
        // от мелких к крупным: у старта — мелочь, у шахты — опасные
        list.Sort((a, b) => a.BaseMass.CompareTo(b.BaseMass));
        return list;
    }

    private static string MazeTitle(BiomeId b, int k) {
        string[] t = { "Лабиринт корней", "Сотовый лабиринт", "Лабиринт эха", "Петляющие норы" };
        return t[((int)b + k) % t.Length];
    }

    private static string RegionTitle(RegionKind k, BiomeId b, NemesisRecord nem, PmRandom rng) {
        switch (k) {
            case RegionKind.Start: return "Колыбель";
            case RegionKind.Exit: return "Устье шахты";
            case RegionKind.BossLair: return "Логово Стража";
            case RegionKind.Shrine: return "Святилище мутаций";
            case RegionKind.Spring: return "Живой родник";
            case RegionKind.Elite: return b == BiomeId.Warm ? "Гнездо огненных жуков" : b == BiomeId.Crystal ? "Гнездо кристальных клещей" : "Гнездо хищников";
            case RegionKind.Lair: return nem != null ? "Логово: " + nem.SoulName : "Логово души";
            case RegionKind.Secret: return "Тайник";
        }
        string[] names;
        switch (b) {
            case BiomeId.Warm: names = new[] { "Огненная яма", "Дымная галерея", "Тлеющий грот" }; break;
            case BiomeId.Ponds: names = new[] { "Студёный пруд", "Зал капели", "Илистая заводь" }; break;
            case BiomeId.Crystal: names = new[] { "Звонкий зал", "Грот призм", "Хрустальная жила" }; break;
            case BiomeId.Heart: names = new[] { "Пульсирующий зал", "Жилы пещеры", "Красная полость" }; break;
            default: names = new[] { "Грибная роща", "Мшистый грот", "Споровый зал" }; break;
        }
        return names[rng.NextInt(names.Length)];
    }

    // ===================== Мелочи =====================
    private static double Dist2(Chamber a, Chamber b) { double dx = a.Cx - b.Cx, dy = a.Cy - b.Cy; return dx * dx + dy * dy; }
    private static bool HasEdge(List<int[]> e, int i, int j) {
        for (int k = 0; k < e.Count; k++) if ((e[k][0] == i && e[k][1] == j) || (e[k][0] == j && e[k][1] == i)) return true;
        return false;
    }
    private static int NearestOther(List<Chamber> c, int i, int rank) {
        var idx = new List<int>();
        for (int j = 0; j < c.Count; j++) if (j != i) idx.Add(j);
        idx.Sort((a, b) => Dist2(c[i], c[a]).CompareTo(Dist2(c[i], c[b])));
        return idx.Count == 0 ? -1 : idx[System.Math.Min(rank, idx.Count - 1)];
    }
    private static Chamber NearestChamber(List<Chamber> c, int x, int y) {
        Chamber best = null; double bd = double.MaxValue;
        for (int i = 0; i < c.Count; i++) {
            double dx = c[i].Cx - x, dy = c[i].Cy - y, d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = c[i]; }
        }
        return best;
    }
    private static void Shuffle<T>(List<T> l, PmRandom rng) {
        for (int i = l.Count - 1; i > 0; i--) { int j = rng.NextInt(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
    }
}
}
