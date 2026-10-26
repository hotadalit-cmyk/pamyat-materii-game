using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.8: RunState + дерево эволюции — ресурсы, жетоны, лимиты.</summary>
public static class RunEvolutionIntegrationTests {
    public static void Absorb_FillsStorage() {
        var rs = new RunState(new SoulMeta(), 21UL, true);
        rs.OnAbsorb("beetle", 2.5);
        True(rs.Resources.Get(ResourceKind.Chitin) > 0, "beetle gives chitin");
        True(rs.LastYield.Length > 0, "last yield recorded");
        True(rs.KnownSpecies.Contains("beetle"), "gene known after absorbing");
    }

    public static void Growth_GivesTokens_NotOffers() {
        var rs = new RunState(new SoulMeta(), 22UL, true);
        rs.OnAbsorb("spore", 10);          // пересекаем порог роста
        True(rs.PendingOffer == null, "no random offer in open world");
        True(rs.Evolution.FreeTokens >= 1, "growth gives a token");
    }

    public static void Evolve_PaysAndRaisesLimits() {
        var rs = new RunState(new SoulMeta(), 23UL, true);
        Eq(2, rs.Fragments.MaxCount, "open world starts with 2 fragments max");
        rs.Resources.Add(ResourceKind.Slime, 40); rs.Resources.Add(ResourceKind.Organic, 40);
        double slime = rs.Resources.Get(ResourceKind.Slime);
        True(rs.TryEvolve("div_split_basics", false), "basics unlocked");
        True(rs.Resources.Get(ResourceKind.Slime) < slime, "resources spent");
        Eq(0.7, rs.Fragments.MinMass, "split threshold lowered");
        True(!rs.TryEvolve("div_split_basics", false), "cannot buy twice");
        True(rs.TryEvolve("div_triplet", false), "triplet unlocked");
        Eq(3, rs.Fragments.MaxCount, "three fragments allowed");
    }

    public static void Evolve_FreeTokenWorksWithoutResources() {
        var rs = new RunState(new SoulMeta(), 24UL, true);
        rs.Evolution.FreeTokens = 1;
        True(!rs.TryEvolve("div_split_basics", false), "no resources — no paid unlock");
        True(rs.TryEvolve("div_split_basics", true), "token unlocks");
        Eq(0, rs.Evolution.FreeTokens, "token spent");
    }

    public static void Stats_ReflectEvolution() {
        var rs = new RunState(new SoulMeta(), 25UL, true);
        var lines = rs.Stats();
        True(lines.Count >= 14, "stat sheet has lines");
        bool hasMass = false;
        foreach (var l in lines) if (l.Key == "mass") hasMass = true;
        True(hasMass, "mass line present");
    }
}
}
