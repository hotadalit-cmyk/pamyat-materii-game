using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.15: опасности биомов и формы карт по глубинам.</summary>
public static class BiomeHazardTests {
    private static WorldMap Map(int depth, ulong seed) =>
        WorldGenerator.Generate(depth, seed, new NemesisRegistry());

    public static void LavaRivers_OnDepth2_WithSafeGap() {
        for (int s = 0; s < 3; s++) {
            var m = Map(2, 300u + (ulong)s);
            int rivers = 0;
            foreach (var h in m.Hazards) {
                if (h.Kind != HazardKind.LavaRiver) continue;
                rivers++;
                True(h.GapR >= HazardRules.MinRiverGapR, "river gap wide enough");
                True(h.HalfLen > h.HalfWidth, "river elongated");
                // центр разрыва — безопасен, края полосы — жгут
                Vec2 gap = h.Pos + h.Dir.Scaled(h.GapAlong);
                True(!HazardRules.Inside(gap, h), "gap is safe");
                Vec2 far = h.Pos + h.Dir.Scaled(h.HalfLen * 0.8);
                True(HazardRules.Inside(far, h), "strip away from gap burns");
            }
            True(rivers >= 2, "d2 seed " + s + ": at least 2 rivers (" + rivers + ")");
        }
    }

    public static void LavaBurns_ByFraction_AndHeatproofHelps() {
        var h = new HazardSpec { Kind = HazardKind.LavaRiver, Pos = new Vec2(0, 0), Dir = Vec2.Right,
            HalfLen = 4, HalfWidth = 1.3, GapAlong = 0, GapR = 0 };
        True(HazardRules.Inside(new Vec2(2, 0), h), "inside strip");
        True(!HazardRules.Inside(new Vec2(0, 2.5), h), "across beyond width");
        Eq(0.2, HazardRules.LavaLossPerSec(10.0, null), "2% of mass per second");
        var heatproof = new MutationSet();
        heatproof.Add("m_heatproof");
        Eq(0.08, HazardRules.LavaLossPerSec(10.0, heatproof), "heatproof ×0.4");
    }

    public static void Currents_OnDepth3() {
        for (int s = 0; s < 3; s++) {
            var m = Map(3, 310u + (ulong)s);
            int currents = 0;
            foreach (var h in m.Hazards) {
                if (h.Kind != HazardKind.Current) continue;
                currents++;
                True(h.Dir.SqrLength > 0.99 && h.Dir.SqrLength < 1.01, "current dir is unit");
                True(h.GapR == 0.0, "current has no gap");
                True(HazardRules.Inside(h.Pos, h), "center inside strip");
            }
            True(currents >= 3, "d3 seed " + s + ": at least 3 currents (" + currents + ")");
        }
    }

    public static void Veins_OnDepth5_PeriodAndStages() {
        for (int s = 0; s < 3; s++) {
            var m = Map(5, 320u + (ulong)s);
            int veins = 0;
            foreach (var h in m.Hazards) {
                if (h.Kind != HazardKind.Vein) continue;
                veins++;
                True(h.Period >= HazardRules.MinVeinPeriod && h.Period <= HazardRules.MaxVeinPeriod,
                    "vein period 4..6 s");
                True(h.Phase >= 0 && h.Phase < h.Period, "phase within period");
            }
            True(veins >= 4, "d5 seed " + s + ": at least 4 veins (" + veins + ")");
        }
        var v = new HazardSpec { Kind = HazardKind.Vein, Pos = new Vec2(0, 0), Dir = Vec2.Right,
            HalfLen = 5, HalfWidth = 1.3, Period = 5.0, Phase = 0.0 };
        Eq(0, HazardRules.VeinStage(1.0, v), "idle mid-cycle");
        Eq(1, HazardRules.VeinStage(4.1, v), "telegraph 0.8 s before strike");
        Eq(2, HazardRules.VeinStage(4.85, v), "strike at cycle end");
        Eq(0, HazardRules.VeinStage(3.0, v), "idle again after strike window");
    }

    public static void CrystalMazes_OnDepth4() {
        for (int s = 0; s < 3; s++) {
            var m = Map(4, 330u + (ulong)s);
            int mazes = 0, crystals = 0;
            foreach (var r in m.Regions) if (r.Kind == RegionKind.Maze) mazes++;
            foreach (var o in m.Obstacles) if (o.Kind == ObstacleKind.Crystal) crystals++;
            True(mazes >= 4, "d4 seed " + s + ": narrow mazes (" + mazes + ")"); // 6 попыток, часть не влезает — это норма
            True(crystals >= 4, "d4 seed " + s + ": crystal columns (" + crystals + ")");
            True(Connected(m), "d4 seed " + s + ": map still connected");
        }
    }

    public static void MushroomGlades_OnDepth1() {
        int withGlades = 0;
        for (int s = 0; s < 5; s++) {
            var m = Map(1, 340u + (ulong)s);
            int shrooms = 0;
            foreach (var o in m.Obstacles) if (o.Kind == ObstacleKind.Mushroom) shrooms++;
            if (shrooms >= 5) withGlades++;
            True(Connected(m), "d1 seed " + s + ": connected");
        }
        True(withGlades >= 2, "most d1 maps have mushroom glades (" + withGlades + "/5)");
    }

    public static void HeartRing_AroundCentralLair() {
        for (int s = 0; s < 3; s++) {
            var m = Map(5, 350u + (ulong)s);
            int boss = -1;
            for (int r = 0; r < m.Regions.Count; r++)
                if (m.Regions[r].Kind == RegionKind.BossLair) boss = r;
            True(boss >= 0, "d5 seed " + s + ": boss lair exists");
            if (boss < 0) continue;
            var c = m.Regions[boss].Center;
            // логово — в средней трети карты (кольцо вокруг центра)
            True(c.X > m.Width * 0.28 && c.X < m.Width * 0.72, "d5 seed " + s + ": lair near center X");
            True(c.Y > m.Height * 0.28 && c.Y < m.Height * 0.72, "d5 seed " + s + ": lair near center Y");
            True(Connected(m), "d5 seed " + s + ": ring keeps map connected");
        }
    }

    /// <summary>Все центры регионов достижимы от старта (связность после резьбы).</summary>
    private static bool Connected(WorldMap m) {
        m.CellOf(m.PlayerStart, out int sx, out int sy);
        var dist = WorldGenerator.Bfs(m, sx, sy);
        for (int r = 0; r < m.Regions.Count; r++) {
            var c = WorldGenerator.NearestRegionFloor(m, m.Regions[r]);
            m.CellOf(c, out int x, out int y);
            if (dist[y * m.Width + x] == int.MaxValue) return false;
        }
        return true;
    }
}
}
