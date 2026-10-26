using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.9: ноша двойников — добыча копится у фрагмента, сдаётся при слиянии, выпадает при гибели.</summary>
public static class RunCarryTests {
    private static RunState Split() {
        var rs = new RunState(new SoulMeta(), 41UL, true);
        True(rs.TrySplit().Count == 1, "split");
        return rs;
    }

    public static void Twin_CarriesLoot_NotStorage() {
        var rs = Split();
        int twin = rs.Fragments[1].Id;
        rs.OnAbsorbAt(twin, "beetle", 2.5);
        Eq(0.0, rs.Resources.Get(ResourceKind.Chitin), "storage untouched");
        True(rs.CarryOf(twin, ResourceKind.Chitin) > 0, "twin carries chitin");
        True(rs.CarryTotal(rs.Fragments.LeadId) == 0, "lead carries nothing");
    }

    public static void Lead_LootGoesToStorage() {
        var rs = Split();
        rs.OnAbsorbAt(rs.Fragments.LeadId, "beetle", 2.5);
        True(rs.Resources.Get(ResourceKind.Chitin) > 0, "lead loot stored");
    }

    public static void Merge_DepositsCarry() {
        var rs = Split();
        int lead = rs.Fragments.LeadId, twin = rs.Fragments[1].Id;
        rs.OnAbsorbAt(twin, "beetle", 2.5);
        double carried = rs.CarryOf(twin, ResourceKind.Chitin);
        True(rs.MergeFragments(lead, twin), "merge");
        Eq(carried, rs.Resources.Get(ResourceKind.Chitin), "carry deposited");
        True(rs.LastDeposit.Length > 0, "deposit reported");
        Eq(0.0, rs.CarryTotal(twin), "nothing left on twin");
    }

    public static void MergeIntoLead_FromOtherSide_AlsoDeposits() {
        var rs = Split();
        int lead = rs.Fragments.LeadId, twin = rs.Fragments[1].Id;
        rs.OnAbsorbAt(twin, "beetle", 2.5);
        True(rs.MergeFragments(twin, lead), "lead merges into twin");
        Eq(twin, rs.Fragments.LeadId, "twin is now lead");
        True(rs.Resources.Get(ResourceKind.Chitin) > 0, "new lead deposits its carry");
    }

    public static void Dissolve_DropsCarry_AndPickupRestores() {
        var rs = Split();
        int twin = rs.Fragments[1].Id;
        rs.OnAbsorbAt(twin, "beetle", 2.5);
        double carried = rs.CarryOf(twin, ResourceKind.Chitin);
        True(rs.OnMassLostAt(twin, 100, "test"), "twin dissolved");
        Eq(1, rs.LastDropped.Length > 0 ? 1 : 0, "carry dropped");
        Eq(0.0, rs.Resources.Get(ResourceKind.Chitin), "dropped loot not stored");
        foreach (var it in rs.LastDropped) rs.PickupAt(rs.Fragments.LeadId, it.Kind, it.Amount);
        Eq(carried, rs.Resources.Get(ResourceKind.Chitin), "picked up by lead → stored");
    }
}
}
