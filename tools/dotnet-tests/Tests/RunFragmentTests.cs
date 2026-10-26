using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
public static class RunFragmentTests {
    public static void Split_HalvesAndKeepsTotal() {
        var f = new FragmentSet(4.0);
        var pairs = f.SplitAll();
        Eq(1, pairs.Count, "one split");
        Eq(2, f.Count, "two fragments");
        Eq(4.0, f.Total, "total kept");
        Eq(2.0, f[0].Mass, "lead half"); Eq(2.0, f[1].Mass, "new half");
        True(f[0].Id != f[1].Id, "distinct ids");
        Eq(pairs[0][0], f[0].Id, "parent is lead");
    }

    public static void Split_RespectsMinimumAndMax() {
        var small = new FragmentSet(1.9);
        True(!small.CanSplit, "1.9 cannot split");
        Eq(0, small.SplitAll().Count, "no split");
        var big = new FragmentSet(100);
        for (int i = 0; i < 6; i++) big.SplitAll();
        Eq(FragmentSet.MaxFragments, big.Count, "capped at max");
        Eq(100.0, big.Total, "mass conserved");
        True(!big.CanSplit, "cannot exceed max");
    }

    public static void Merge_CombinesAndLeadTransfers() {
        var f = new FragmentSet(8);
        f.SplitAll(); f.SplitAll();                 // 4 × 2.0
        Eq(4, f.Count, "four");
        int lead = f.LeadId, other = f[2].Id;
        True(f.Merge(other, lead), "merge lead into other");
        Eq(3, f.Count, "three left");
        Eq(other, f.LeadId, "keeper becomes lead");
        Eq(4.0, f.MassOf(other), "masses combined");
        Eq(8.0, f.Total, "total kept");
        True(!f.Merge(other, other), "no self merge");
        True(!f.Merge(999, other), "unknown id");
    }

    public static void Remove_DissolvesSmallFragment() {
        var f = new FragmentSet(4);
        f.SplitAll();
        int b = f[1].Id;
        True(!f.Remove(b, 1.0), "1.0 left — alive");
        True(f.Remove(b, 0.6), "below 0.5 — dissolved");
        Eq(1, f.Count, "one left");
        True(!f.Remove(f.LeadId, 10), "last fragment never removed from list");
    }

    public static void ApplyDelta_GainToLead_LossProportional() {
        var f = new FragmentSet(6);
        f.SplitAll();                                // 3 + 3
        f.Remove(f[1].Id, 1.0);                      // 3 + 2
        f.ApplyDelta(1.0);
        Eq(4.0, f[0].Mass, "gain to lead");
        f.ApplyDelta(-3.0);                          // 4+2=6 → 3, пропорционально
        Eq(2.0, f[0].Mass, "lead loses its share");
        Eq(1.0, f[1].Mass, "other loses its share");
    }

    public static void RunState_SplitAndAbsorbAt() {
        var rs = new RunState(new SoulMeta(), 1UL, true);
        rs.Evolution.FreeTokens++; rs.TryEvolve("div_split_basics", true); // v0.9: деление — узел эволюции
        double total = rs.Mass;
        var pairs = rs.TrySplit();
        Eq(1, pairs.Count, "split once");
        Eq(total, rs.Mass, "total unchanged by split");
        int child = pairs[0][1];
        double before = rs.FragmentMass(child);
        rs.OnAbsorbAt(child, "spore", 0.3);
        Eq(before + 0.3, rs.FragmentMass(child), "absorbing fragment gets the mass");
        Eq(total + 0.3, rs.Mass, "total grows");
        True(rs.RoomAbsorbed >= 0.3 - 1e-9, "counts toward goal");
    }

    public static void RunState_FragmentLoss_DissolveAndDeath() {
        var rs = new RunState(new SoulMeta(), 2UL, true);
        rs.Evolution.FreeTokens++; rs.TryEvolve("div_split_basics", true); // v0.9: деление — узел эволюции
        var pairs = rs.TrySplit();
        int child = pairs[0][1];
        True(rs.OnMassLostAt(child, rs.FragmentMass(child) - 0.2, "mite"), "child dissolved");
        Eq(1, rs.Fragments.Count, "one fragment left");
        True(!rs.IsDead, "alive while lead lives");
        rs.OnMassLostAt(rs.Fragments.LeadId, rs.Mass, "beetle");
        True(rs.IsDead, "dead when total below minimum");
        Eq("beetle", rs.Finish().KilledBy, "killer recorded");
    }

    public static void RunState_MergeFragments() {
        var rs = new RunState(new SoulMeta(), 3UL, true);
        rs.Evolution.FreeTokens++; rs.TryEvolve("div_split_basics", true); // v0.9: деление — узел эволюции
        var pairs = rs.TrySplit();
        double total = rs.Mass;
        True(rs.MergeFragments(pairs[0][0], pairs[0][1]), "merge");
        Eq(1, rs.Fragments.Count, "whole again");
        Eq(total, rs.Mass, "mass kept");
    }

    public static void RunState_GenericLossIsProportional() {
        var rs = new RunState(new SoulMeta(), 4UL, true);
        rs.Evolution.FreeTokens++; rs.TryEvolve("div_split_basics", true); // v0.9: деление — узел эволюции
        rs.TrySplit();
        rs.OnMassLost(1.0, "vent");
        Eq(rs.Fragments[0].Mass, rs.Fragments[1].Mass, "equal halves lose equally");
    }

    // ---------- Трещины в генераторе ----------
    public static void World_HasOneCellCracks() {
        for (int d = 1; d <= 5; d += 2) {
            var m = WorldGenerator.Generate(d, 500UL + (ulong)d, null);
            True(m.CrackMouths.Count >= 2, "crack mouths d" + d);
            int narrow = 0;
            for (int y = 1; y < m.Height - 1; y++)
                for (int x = 1; x < m.Width - 1; x++) {
                    if (!m.IsFloor(x, y)) continue;
                    bool horiz = !m.IsFloor(x, y - 1) && !m.IsFloor(x, y + 1) && m.IsFloor(x - 1, y) && m.IsFloor(x + 1, y);
                    bool vert = !m.IsFloor(x - 1, y) && !m.IsFloor(x + 1, y) && m.IsFloor(x, y - 1) && m.IsFloor(x, y + 1);
                    if (horiz || vert) narrow++;
                }
            True(narrow >= 6, "one-cell-wide passages exist d" + d + " (" + narrow + ")");
        }
    }

    public static void World_SecretsOnlyThroughCracks() {
        // целое тело (радиус ≥ 0.6) не проходит в клетку шириной 1: блокируем узкие клетки и проверяем,
        // что хотя бы один тайник недостижим, а для фрагмента (без блокировки) — достижим
        var m = WorldGenerator.Generate(2, 777UL, null);
        var blocked = new bool[m.Cells.Length];
        for (int y = 1; y < m.Height - 1; y++)
            for (int x = 1; x < m.Width - 1; x++) {
                if (!m.IsFloor(x, y)) continue;
                bool narrowH = !m.IsFloor(x, y - 1) && !m.IsFloor(x, y + 1);
                bool narrowV = !m.IsFloor(x - 1, y) && !m.IsFloor(x + 1, y);
                if (narrowH || narrowV) blocked[y * m.Width + x] = true;
            }
        m.CellOf(m.PlayerStart, out int sx, out int sy);
        var wide = WorldGenerator.Bfs(m, sx, sy, blocked);
        var thin = WorldGenerator.Bfs(m, sx, sy);
        int hidden = 0;
        foreach (var r in m.Regions) {
            if (r.Kind != RegionKind.Secret) continue;
            var c = WorldGenerator.NearestRegionFloor(m, r);
            m.CellOf(c, out int x, out int y);
            True(thin[y * m.Width + x] != int.MaxValue, "secret reachable by fragments");
            if (wide[y * m.Width + x] == int.MaxValue) hidden++;
        }
        True(hidden >= 1, "at least one secret needs splitting");
    }
}
}
