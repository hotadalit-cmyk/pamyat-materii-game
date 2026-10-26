using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunSoulMetaTests {

    public static void UpgradeCost_Table() {
        Eq(20.0, SoulMeta.UpgradeCost(0), "уровень 0 → 1");
        Eq(35.0, SoulMeta.UpgradeCost(1), "уровень 1 → 2");
        Eq(55.0, SoulMeta.UpgradeCost(2), "уровень 2 → 3");
        Eq(80.0, SoulMeta.UpgradeCost(3), "уровень 3 → 4");
        Eq(110.0, SoulMeta.UpgradeCost(4), "уровень 4 → 5");
        True(SoulMeta.UpgradeCost(5) == double.MaxValue, "максимум — стоимость недоступна");
        True(SoulMeta.UpgradeCost(-1) == double.MaxValue, "отрицательный уровень невалиден");
    }

    public static void TryUpgrade_PaysEssenceAndLevels() {
        var meta = new SoulMeta { Essence = 100.0 };
        True(meta.TryUpgrade(SoulStat.Vitality), "первый апгрейд прошёл");
        Eq(80.0, meta.Essence, "100 − 20");
        Eq(1, meta.Level(SoulStat.Vitality), "уровень 1");
        True(meta.TryUpgrade(SoulStat.Vitality), "второй апгрейд прошёл");
        Eq(45.0, meta.Essence, "80 − 35");
        True(!meta.TryUpgrade(SoulStat.Vitality), "45 < 55 — не хватает");
        Eq(2, meta.Level(SoulStat.Vitality), "уровень остался 2");
        Eq(45.0, meta.Essence, "эссенция не списана при отказе");
    }

    public static void TryUpgrade_MaxLevelCapped() {
        var meta = new SoulMeta { Essence = 10000.0 };
        for (int i = 0; i < 5; i++) True(meta.TryUpgrade(SoulStat.Energy), "апгрейд " + i);
        Eq(5, meta.Level(SoulStat.Energy), "максимальный уровень 5");
        True(!meta.TryUpgrade(SoulStat.Energy), "шестой апгрейд невозможен");
        Eq(5, meta.Level(SoulStat.Energy), "уровень не вырос");
        Eq(0, meta.Level(SoulStat.Endurance), "другие статы не задеты");
    }

    public static void Derivatives_EnergyBonusCharges() {
        var meta = new SoulMeta { Essence = 10000.0 };
        Eq(0, meta.BonusDashCharges, "уровень 0 — без бонуса");
        meta.TryUpgrade(SoulStat.Energy); meta.TryUpgrade(SoulStat.Energy);
        Eq(2, meta.Level(SoulStat.Energy), "энергия 2");
        Eq(1, meta.BonusDashCharges, "+1 заряд");
        meta.TryUpgrade(SoulStat.Energy);
        Eq(1, meta.BonusDashCharges, "уровень 3 — всё ещё +1");
        meta.TryUpgrade(SoulStat.Energy);
        Eq(4, meta.Level(SoulStat.Energy), "энергия 4");
        Eq(2, meta.BonusDashCharges, "+2 заряда");
    }

    public static void Derivatives_EnduranceAndVitality() {
        var meta = new SoulMeta { Essence = 10000.0 };
        Eq(1.0, meta.DashCostMult, "без выносливости ×1");
        for (int i = 0; i < 3; i++) meta.TryUpgrade(SoulStat.Endurance);
        Eq(0.76, meta.DashCostMult, "выносливость 3 → ×0.76");
        meta.TryUpgrade(SoulStat.Endurance); meta.TryUpgrade(SoulStat.Endurance);
        Eq(0.6, meta.DashCostMult, "выносливость 5 → ×0.6");
        Eq(0.0, meta.StartMassBonus, "без живучести бонуса нет");
        for (int i = 0; i < 5; i++) meta.TryUpgrade(SoulStat.Vitality);
        Eq(3.0, meta.StartMassBonus, "живучесть 5 → +3.0 массы");
    }

    public static void ApplyLife_AggregatesImprint() {
        var meta = new SoulMeta { Essence = 10.0 };
        var imprint = new LifeImprint {
            DepthReached = 3, RoomsCleared = 5, Absorbed = 4, NewSpecies = 2,
            PeakMass = 12.5, BestHit = 0.8, EssenceEarned = 17.5,
            KilledBy = "jelly", Mutations = new string[] { "m_spikes", "m_spikes", "m_shell" },
        };
        meta.ApplyLife(imprint);
        Eq(27.5, meta.Essence, "10 + 17.5");
        Eq(1, meta.Runs, "забег засчитан");
        Eq(3, meta.BestDepth, "лучшая глубина 3");
        True(meta.KnownGenes.Contains("m_spikes"), "ген шипов изучен");
        True(meta.KnownGenes.Contains("m_shell"), "ген панциря изучен");
        Eq(2, meta.KnownGenes.Count, "дубли не важны");
        var weaker = new LifeImprint { DepthReached = 2, EssenceEarned = 1.0, Mutations = new string[0] };
        meta.ApplyLife(weaker);
        Eq(2, meta.Runs, "второй забег");
        Eq(3, meta.BestDepth, "лучшая глубина не падает");
        Eq(28.5, meta.Essence, "27.5 + 1");
        meta.ApplyLife(null);
        Eq(2, meta.Runs, "null-оттиск безопасен");
    }

    public static void Json_RoundTripKeepsEverything() {
        var meta = new SoulMeta { Essence = 1000.0 };
        for (int i = 0; i < 3; i++) meta.TryUpgrade(SoulStat.Energy);
        meta.TryUpgrade(SoulStat.Endurance);
        for (int i = 0; i < 5; i++) meta.TryUpgrade(SoulStat.Vitality);
        meta.Runs = 7;
        meta.BestDepth = 4;
        meta.KnownGenes.Add("m_spikes"); meta.KnownGenes.Add("m_hunger");
        meta.SeenSpecies.Add("spore"); meta.SeenSpecies.Add("jelly");
        meta.Nemeses.Records.Add(new NemesisRecord { SoulName = "Тлен", Level = 2, KillsOfPlayer = 3, Defeated = false, StolenMutation = "m_eyes" });
        meta.Nemeses.Records.Add(new NemesisRecord { SoulName = "Шёпот", Level = 1, KillsOfPlayer = 1, Defeated = true, StolenMutation = null });

        var restored = SoulMeta.FromJson(meta.ToJson());
        Eq(570.0, restored.Essence, "эссенция: 1000 − 430 за апгрейды");
        Eq(7, restored.Runs, "забеги");
        Eq(4, restored.BestDepth, "лучшая глубина");
        Eq(3, restored.Level(SoulStat.Energy), "энергия 3");
        Eq(1, restored.Level(SoulStat.Endurance), "выносливость 1");
        Eq(5, restored.Level(SoulStat.Vitality), "живучесть 5");
        Eq(1, restored.BonusDashCharges, "производные восстановились");
        Eq(0.92, restored.DashCostMult, "множитель рывка");
        Eq(3.0, restored.StartMassBonus, "бонус массы");
        True(restored.KnownGenes.Contains("m_spikes") && restored.KnownGenes.Contains("m_hunger"), "гены");
        True(restored.SeenSpecies.Contains("spore") && restored.SeenSpecies.Contains("jelly"), "виды");
        Eq(2, restored.Nemeses.Records.Count, "немезид 2");
        var n1 = restored.Nemeses.Records[0];
        Eq("Тлен", n1.SoulName, "имя души (кириллица)");
        Eq(2, n1.Level, "уровень немезиды");
        Eq(3, n1.KillsOfPlayer, "счёт убийств");
        True(!n1.Defeated, "жива");
        Eq("m_eyes", n1.StolenMutation, "украденная мутация");
        var n2 = restored.Nemeses.Records[1];
        True(n2.Defeated, "вторая повержена");
        True(n2.StolenMutation == null, "кражи не было");
        // и повторный round-trip стабилен
        Eq(meta.ToJson(), SoulMeta.FromJson(restored.ToJson()).ToJson(), "повторный цикл идентичен");
    }

    public static void Json_GarbageAndEmptyReturnDefaults() {
        foreach (string bad in new string[] { null, "", "   ", "мусор{[", "{unclosed", "{\"a\":", "42", "null", "true", "[1,2]" }) {
            var meta = SoulMeta.FromJson(bad);
            Eq(0.0, meta.Essence, "мусор '" + (bad ?? "null") + "' → эссенция 0");
            Eq(0, meta.Runs, "мусор → забегов 0");
            Eq(0, meta.BestDepth, "мусор → глубина 0");
            Eq(0, meta.Level(SoulStat.Energy), "мусор → уровни 0");
            Eq(0, meta.KnownGenes.Count, "мусор → гены пусты");
            Eq(0, meta.SeenSpecies.Count, "мусор → виды пусты");
            Eq(0, meta.Nemeses.Records.Count, "мусор → немезид нет");
            True(meta.Nemeses != null, "реестр создан даже из мусора");
        }
    }

    public static void Json_PartialDataKeepsDefaults() {
        var meta = SoulMeta.FromJson("{\"essence\": 5, \"runs\": 2}");
        Eq(5.0, meta.Essence, "эссенция прочитана");
        Eq(2, meta.Runs, "забеги прочитаны");
        Eq(0, meta.BestDepth, "остальное — дефолт");
        Eq(0, meta.Level(SoulStat.Vitality), "уровни — дефолт");
        Eq(0, meta.Nemeses.Records.Count, "немезид нет");
    }

    public static void Defaults_FreshMetaIsPlayable() {
        var meta = new SoulMeta();
        Eq(0.0, meta.Essence, "без эссенции");
        Eq(0, meta.Runs, "без забегов");
        True(meta.KnownGenes != null && meta.KnownGenes.Count == 0, "гены пусты");
        True(meta.SeenSpecies != null && meta.SeenSpecies.Count == 0, "виды пусты");
        True(meta.Nemeses != null && meta.Nemeses.Records.Count == 0, "реестр пуст");
        True(!meta.TryUpgrade(SoulStat.Energy), "без эссенции апгрейда нет");
    }
}
}
