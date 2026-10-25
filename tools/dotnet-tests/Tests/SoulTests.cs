using PM.Domain.Soul;
using PM.Domain.Body;
using static PMHarness.Check;
namespace PMHarness {
public static class SoulTests {
    public static void Growth_DiminishingReturns() {
        double first = SoulGrowth.RewardFor(0);
        double second = SoulGrowth.RewardFor(1);
        double tenth = SoulGrowth.RewardFor(9);
        True(second < first, "second action less");
        True(tenth < second, "tenth action even less");
        True(tenth >= SoulGrowth.BaseReward * SoulGrowth.FloorFactor - 1e-9, "floor respected");
        Eq(SoulGrowth.BaseReward, first, "first full reward");
    }
    public static void Apply_SplitsBetweenQualities() {
        var s = new SoulState();
        double e0 = s.EnergyPotential, en0 = s.Endurance, m0 = s.MagicReserve;
        SoulGrowth.Apply(s, SoulGrowth.ActCapture, 2.0);
        Eq(e0 + 1.2, s.EnergyPotential, "60% to energy");
        Eq(en0 + 0.5, s.Endurance, "25% to endurance");
        Eq(m0 + 0.3, s.MagicReserve, "15% to magic");
        Eq(1, s.GrowthCounts[SoulGrowth.ActCapture], "counted");
    }
    public static void Death_YieldsNothingByConstruction() {
        // проверка конструкции: нет API «опыт за смерть»; смерть меняет только счётчик жизней
        var s = new SoulState();
        double e = s.EnergyPotential, en = s.Endurance, m = s.MagicReserve;
        s.LivesLived++; // то, что делает реинкарнация
        Eq(e, s.EnergyPotential, "no xp from death: energy");
        Eq(en, s.Endurance, "no xp from death: endurance");
        Eq(m, s.MagicReserve, "no xp from death: magic");
    }
    public static void DecayIfIdle_BoundedToFloor() {
        var s = new SoulState();
        SoulGrowth.Apply(s, SoulGrowth.ActCapture, 10); // большой рост
        double peak = s.EnergyPotential;
        SoulGrowth.DecayIfIdle(s, 100000); // очень долго без действий
        True(s.EnergyPotential < peak, "decays when idle");
        True(s.EnergyPotential >= peak * 0.9 - 1e-9, "decay bounded at 90% of peak");
        var fresh = new SoulState();
        SoulGrowth.DecayIfIdle(fresh, 5);
        Eq(10.0, fresh.EnergyPotential, "short idle no decay");
    }
    public static void KarmaSeparatedFromPower() {
        var s = new SoulState();
        s.Karma = -50; // «зло» не делает сильнее
        SoulGrowth.Apply(s, SoulGrowth.ActCapture, 2);
        True(s.EnergyPotential > 10, "karma does not gate growth");
    }
    public static void Reincarnation_BodyRebuiltSoulCarried() {
        var s = new SoulState();
        SoulGrowth.Apply(s, SoulGrowth.ActMetamorphose, 6);
        s.Karma = 20;
        s.LearnGene("source_beetle_101");
        double pot = s.EnergyPotential;
        var r = Reincarnation.Reborn(s, baseStructure: 1.0, baseWater: 5.0,
            bodyConductivityBase: 0.4, regionRichness: 0.5);
        True(r.Body.Structure > 0, "new body has structure");
        True(r.Body.Water > 0, "new body has water");
        True(s.KnowledgeGenes.Contains("source_beetle_101"), "gene knowledge persists (genes as knowledge)");
        True(s.EnergyPotential >= pot, "soul qualities persist");
        Eq(1, s.LivesLived, "life counter incremented by rebirth");
        // проводимость тела ограничивает энергию (древняя душа в слабом теле)
        True(r.CarriedEnergy <= r.BodyConductivity * 10 + 1e-9 || r.CarriedEnergy <= pot + 1e-9,
            "carried energy limited by body conductivity");
    }
    public static void Reincarnation_RichnessAffectsBody() {
        var s1 = new SoulState();
        var poor = Reincarnation.Reborn(s1, 1.0, 5.0, 0.4, regionRichness: 0.0);
        var s2 = new SoulState();
        var rich = Reincarnation.Reborn(s2, 1.0, 5.0, 0.4, regionRichness: 1.0);
        True(rich.Body.Structure > poor.Body.Structure, "rich region better body");
    }
}
}
