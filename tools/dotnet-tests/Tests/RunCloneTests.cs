using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.9: способности только после эволюции; клоны, перенос души, разрыв связи.</summary>
public static class RunCloneTests {
    private static RunState Unlocked(params string[] nodes) {
        var rs = new RunState(new SoulMeta(), 61UL, true);
        foreach (var n in nodes) { rs.Evolution.FreeTokens++; True(rs.TryEvolve(n, true), "unlock " + n); }
        return rs;
    }

    public static void Split_LockedUntilEvolution() {
        var rs = new RunState(new SoulMeta(), 60UL, true);
        True(!rs.HasAbility("split"), "no split at start");
        Eq(0, rs.TrySplit().Count, "cannot split at start");
        Eq(1, rs.Fragments.Count, "one body");
        rs.Evolution.FreeTokens = 1;
        True(rs.TryEvolve("div_split_basics", true), "unlock");
        Eq(1, rs.TrySplit().Count, "split works after unlock");
    }

    public static void NoAbilitiesOrBonusesAtStart() {
        var rs = new RunState(new SoulMeta(), 62UL, true);
        foreach (var k in new[] { "split", "soul_transfer", "clone_return", "clone_hunter" }) True(!rs.HasAbility(k), "no " + k);
        foreach (EvoEffectKind k in System.Enum.GetValues(typeof(EvoEffectKind))) Eq(0.0, rs.Evolution.Sum(k), "no bonus " + k);
        Eq(0, rs.Mutations.ToArray().Length, "no mutations");
    }

    public static void SoulTransfer_NeedsAbility_AndSwapsLead() {
        var rs = Unlocked("div_split_basics");
        rs.TrySplit();
        int lead = rs.Fragments.LeadId, clone = rs.Fragments[1].Id;
        True(!rs.TransferSoul(clone), "locked without node");
        rs = Unlocked("div_split_basics", "clone_leash", "clone_soul");
        rs.TrySplit();
        lead = rs.Fragments.LeadId; clone = rs.Fragments[1].Id;
        rs.OnAbsorbAt(clone, "beetle", 2.5);
        double carried = rs.CarryOf(clone, ResourceKind.Chitin);
        True(rs.TransferSoul(clone), "transfer");
        Eq(clone, rs.Fragments.LeadId, "clone is now the soul");
        Eq(carried, rs.Resources.Get(ResourceKind.Chitin), "its carry is stored");
        True(!rs.TransferSoul(clone), "cannot transfer into self");
    }

    public static void Drift_WeakensThenPops() {
        var rs = Unlocked("div_split_basics");
        rs.TrySplit();
        int clone = rs.Fragments[1].Id;
        rs.OnAbsorbAt(clone, "mite", 1.0);
        double m0 = rs.FragmentMass(clone);
        Eq((int)RunState.Drift.Ok, (int)rs.DriftAt(clone, 5, 1.0), "near — fine");
        Eq((int)RunState.Drift.Weak, (int)rs.DriftAt(clone, RunState.BaseCloneLeash + 4, 1.0), "far — weak");
        True(rs.FragmentMass(clone) < m0, "loses mass far away");
        double total = rs.Mass;
        Eq((int)RunState.Drift.Popped, (int)rs.DriftAt(clone, RunState.BaseCloneLeash * 2, 0.1), "beyond — pops");
        Eq(1, rs.Fragments.Count, "clone gone");
        True(rs.LastDropped.Length > 0, "carry dropped");
        True(rs.LastPoppedMass > 0, "mass scattered");
        True(rs.Mass < total, "body lost the clone mass");
        Eq((int)RunState.Drift.Ok, (int)rs.DriftAt(rs.Fragments.LeadId, 999, 1.0), "lead never drifts");
    }

    public static void CloneBranch_Bonuses() {
        var rs = Unlocked("div_split_basics", "clone_leash", "clone_instinct", "clone_shell", "clone_hunter");
        Eq(RunState.BaseCloneLeash + 6, rs.CloneLeash, "leash +6");
        Eq(3.0, rs.CloneSightBonus, "sight +3");
        Eq(1.0, rs.CloneAbsorbRatio, "hunter clones eat equals");
        rs.TrySplit();
        int clone = rs.Fragments[1].Id;
        double before = rs.FragmentMass(clone);
        rs.OnMassLostAt(clone, 0.5, "test");
        Eq(before - 0.35, rs.FragmentMass(clone), "clone takes 30% less damage");
        True(!rs.TryEvolve("clone_packrat", false), "fork: packrat blocked after hunter");
    }

    public static void CloneBranch_AllNodesNeedSplit() {
        foreach (var n in EvolutionTree.Branch(EvoBranch.Clones)) {
            var cur = n; int safety = 8;
            while (cur.Requires.Length > 0 && safety-- > 0) cur = EvolutionTree.Get(cur.Requires[0]);
            Eq("div_split_basics", cur.Id, n.Id + " roots at Деление");
        }
    }
}
}
