using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.9: прокачка показателей за ресурсы; эволюция открывает новые показатели.</summary>
public static class RunTrainingTests {
    private static RunState Rich() {
        var rs = new RunState(new SoulMeta(), 51UL, true);
        foreach (ResourceKind k in System.Enum.GetValues(typeof(ResourceKind))) rs.Resources.Add(k, 40);
        return rs;
    }

    public static void Strength_PaysAndRaisesDamage() {
        var rs = Rich();
        double chitin = rs.Resources.Get(ResourceKind.Chitin);
        Eq(1.0, rs.Mutations.DamageDealtMult, "base damage");
        True(rs.TryTrain("strength"), "train strength");
        Eq(1, rs.Training.Level("strength"), "level 1");
        True(rs.Resources.Get(ResourceKind.Chitin) < chitin, "paid");
        Eq(1.08, rs.Mutations.DamageDealtMult, "+8% damage");
    }

    public static void CostGrows_AndCapsAtMax() {
        var d = TrainCatalog.Get("agility");
        True(StatTraining.CostFor(d, 5).Items[0].Amount > StatTraining.CostFor(d, 0).Items[0].Amount, "cost grows");
        var t = new StatTraining();
        var store = new ResourceStore { Capacity = 100000 };
        store.Add(ResourceKind.Slime, 100000);
        for (int i = 0; i < 20; i++) t.TryTrain("agility", store, new EvolutionState());
        Eq(StatTraining.MaxLevel, t.Level("agility"), "capped");
        Eq((int)TrainBlock.Maxed, (int)t.CanTrain("agility", store, new EvolutionState()), "maxed block");
    }

    public static void Locked_UntilEvolutionUnlocks() {
        var rs = Rich();
        Eq((int)TrainBlock.Locked, (int)rs.CanTrain("division"), "division locked at start");
        True(!rs.TryTrain("division"), "cannot train locked");
        True(rs.TryEvolve("div_split_basics", false), "evolve unlocks");
        Eq((int)TrainBlock.Ok, (int)rs.CanTrain("division"), "now trainable");
        double before = rs.Fragments.MinMass;
        True(rs.TryTrain("division"), "train division");
        True(rs.Fragments.MinMass < before, "split threshold lowered");
    }

    public static void Training_AddsToEvolutionBonus() {
        var rs = Rich();
        double baseSpeed = rs.DashSpeedMult;
        True(rs.TryTrain("agility"), "agility");
        Eq(baseSpeed * 1.05, rs.DashSpeedMult, "dash +5%");
        bool found = false;
        foreach (var l in rs.Stats()) if (l.Key == "dash_speed") foreach (var s in l.Sources) if (s.Contains("Ловкость")) found = true;
        True(found, "stat sheet names the training source");
    }

    public static void NoResources_Blocks() {
        var rs = new RunState(new SoulMeta(), 52UL, true);
        Eq((int)TrainBlock.NoResources, (int)rs.CanTrain("strength"), "empty storage");
        True(!rs.TryTrain("strength"), "no free levels");
    }

    public static void Catalog_UnlockersExistInTree() {
        foreach (var d in TrainCatalog.All())
            if (d.UnlockedBy != null) foreach (var id in d.UnlockedBy) True(EvolutionTree.Get(id) != null, "node exists: " + id);
        True(TrainCatalog.UnlockedByNode("senses_eyes").Count > 0, "eyes unlocks sight");
    }
}
}
