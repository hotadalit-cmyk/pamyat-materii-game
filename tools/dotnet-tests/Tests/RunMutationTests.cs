using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunMutationTests {

    public static void Catalog_SpeciesAtLeastEight() {
        var all = SpeciesCatalog.All();
        True(all.Length >= 8, "минимум 8 видов, есть " + all.Length);
        var ids = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < all.Length; i++) {
            True(ids.Add(all[i].Id), "id видов уникальны: " + all[i].Id);
            True(!string.IsNullOrEmpty(all[i].Name), "у вида есть русское имя");
            True(all[i].BaseMass > 0, "масса вида положительна");
            True(all[i].MinDepth >= 1 && all[i].MaxDepth <= 5 && all[i].MinDepth <= all[i].MaxDepth,
                "глубины вида корректны: " + all[i].Id);
        }
    }

    public static void Catalog_KeySpeciesFields() {
        var spore = SpeciesCatalog.Get("spore");
        Eq("Спора", spore.Name, "имя споры");
        Eq(CreatureBehavior.Food, spore.Behavior, "спора — еда");
        Eq(0.3, spore.BaseMass, "масса споры");
        True(spore.GeneMutationId == null, "спора без гена");
        Eq(0, spore.ColorIndex, "спора — цвет еды");
        var mite = SpeciesCatalog.Get("mite");
        Eq(CreatureBehavior.Flee, mite.Behavior, "клещ убегает");
        Eq("m_light", mite.GeneMutationId, "клещ даёт лёгкость");
        Eq(1, mite.MinDepth, "клещ с глубины 1");
        Eq(3, mite.MaxDepth, "клещ до глубины 3");
        var spike = SpeciesCatalog.Get("spikeback");
        Eq(CreatureBehavior.Charger, spike.Behavior, "шипастик — чарджер");
        Eq("m_spikes", spike.GeneMutationId, "шипастик даёт шипы");
        Eq(2, spike.ColorIndex, "шипастик — опасность");
        var soul = SpeciesCatalog.Get("soul_slime");
        Eq(3, soul.ColorIndex, "душеслизень — цвет души");
        Eq("m_hunger", soul.GeneMutationId, "душеслизень даёт голод");
        var crystal = SpeciesCatalog.Get("crystal_mite");
        Eq(4, crystal.ColorIndex, "кристоклещ — цвет кристалла");
        Eq(4, crystal.MinDepth, "кристоклещ с глубины 4");
    }

    public static void Catalog_GetUnknownAndNull() {
        True(SpeciesCatalog.Get("no_such_species") == null, "неизвестный вид → null");
        True(SpeciesCatalog.Get(null) == null, "null → null");
        True(SpeciesCatalog.Get("") == null, "пустая строка → null");
        True(SpeciesCatalog.Get("beetle") != null, "известный вид находится");
    }

    public static void Catalog_MutationsAtLeastEleven() {
        var all = MutationCatalog.All();
        True(all.Length >= 11, "минимум 11 мутаций, есть " + all.Length);
        var ids = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < all.Length; i++) {
            True(ids.Add(all[i].Id), "id мутаций уникальны: " + all[i].Id);
            True(all[i].MaxStacks >= 1, "MaxStacks >= 1: " + all[i].Id);
            True(!string.IsNullOrEmpty(all[i].Name), "у мутации есть имя");
            True(!string.IsNullOrEmpty(all[i].Description), "у мутации есть описание");
        }
        True(MutationCatalog.Get("no_such") == null, "неизвестная мутация → null");
        True(MutationCatalog.Get(null) == null, "null → null");
        Eq(2, MutationCatalog.Get("m_spikes").MaxStacks, "шипы: 2 стака");
        Eq(3, MutationCatalog.Get("m_eyes").MaxStacks, "глаза: 3 стака");
        Eq(1, MutationCatalog.Get("m_toxic").MaxStacks, "след: 1 стак");
        Eq(1, MutationCatalog.Get("m_hunger").MaxStacks, "голод: 1 стак");
        Eq(1, MutationCatalog.Get("m_crystal").MaxStacks, "кристалл: 1 стак");
    }

    public static void Set_AddStacksAndMaxRespected() {
        var m = new MutationSet();
        Eq(0, m.Stacks("m_spikes"), "изначально пусто");
        True(m.CanAdd("m_spikes"), "можно добавить первый стак");
        m.Add("m_spikes"); m.Add("m_spikes"); m.Add("m_spikes");
        Eq(2, m.Stacks("m_spikes"), "третий стак игнорируется (макс 2)");
        True(!m.CanAdd("m_spikes"), "после максимума добавить нельзя");
        m.Add("m_eyes"); m.Add("m_eyes"); m.Add("m_eyes"); m.Add("m_eyes");
        Eq(3, m.Stacks("m_eyes"), "глаза максимум 3");
    }

    public static void Set_UnknownAndNullIgnored() {
        var m = new MutationSet();
        m.Add("m_nope"); m.Add(null); m.Add("");
        Eq(0, m.Stacks("m_nope"), "неизвестный id не добавляется");
        Eq(0, m.Stacks(null), "null безопасен");
        True(!m.CanAdd("m_nope"), "нельзя добавить неизвестное");
        True(!m.CanAdd(null), "нельзя добавить null");
        Eq(0, m.ToArray().Length, "набор остался пустым");
    }

    public static void Set_DamageMultipliers() {
        var empty = new MutationSet();
        Eq(1.0, empty.DamageDealtMult, "без шипов урон ×1");
        Eq(1.0, empty.DamageTakenMult, "без панциря получаемый ×1");
        var s1 = new MutationSet(); s1.Add("m_spikes");
        Eq(1.5, s1.DamageDealtMult, "шипы ×1.5");
        var s2 = new MutationSet(); s2.Add("m_spikes"); s2.Add("m_spikes");
        Eq(2.25, s2.DamageDealtMult, "шипы ×2 = 2.25");
        var sh2 = new MutationSet(); sh2.Add("m_shell"); sh2.Add("m_shell");
        Eq(0.49, sh2.DamageTakenMult, "панцирь ×2 = 0.49");
    }

    public static void Set_BouncinessSticky() {
        var empty = new MutationSet();
        Eq(0.8, empty.Bounciness, "база упругости 0.8");
        var s1 = new MutationSet(); s1.Add("m_sticky");
        Eq(0.55, s1.Bounciness, "липкость −0.25");
        var s2 = new MutationSet(); s2.Add("m_sticky"); s2.Add("m_sticky");
        Eq(0.3, s2.Bounciness, "липкость ×2 = 0.3 (выше минимума 0.2)");
    }

    public static void Set_DashChargesAndPullRadius() {
        Eq(1, new MutationSet().DashCharges, "база: 1 заряд");
        var d1 = new MutationSet(); d1.Add("m_dash2");
        Eq(2, d1.DashCharges, "второй рывок: 2 заряда");
        var d2 = new MutationSet(); d2.Add("m_dash2"); d2.Add("m_dash2");
        Eq(3, d2.DashCharges, "два стака: 3 заряда");
        Eq(1.0, new MutationSet().PullRadiusMult, "магнит база ×1");
        var m2 = new MutationSet(); m2.Add("m_magnet"); m2.Add("m_magnet");
        Eq(2.25, m2.PullRadiusMult, "магнит ×2 = 2.25");
    }

    public static void Set_LightTrailRegenCost() {
        Eq(3.5, new MutationSet().LightRadius, "свет база 3.5");
        var e3 = new MutationSet(); e3.Add("m_eyes"); e3.Add("m_eyes"); e3.Add("m_eyes");
        Eq(9.5, e3.LightRadius, "глаза ×3 = 9.5");
        var t = new MutationSet(); t.Add("m_toxic");
        Eq(0.4, t.TrailDamagePerSec, "след 0.4 массы/сек");
        Eq(0.0, new MutationSet().TrailDamagePerSec, "без следа 0");
        var r2 = new MutationSet(); r2.Add("m_regrow"); r2.Add("m_regrow");
        Eq(0.04, r2.RegenPerSec, "регенерация ×2 = 0.04/сек");
        Eq(1.0, new MutationSet().DashCostMult, "стоимость рывка база ×1");
        var l2 = new MutationSet(); l2.Add("m_light"); l2.Add("m_light");
        Eq(0.36, l2.DashCostMult, "лёгкость ×2 = 0.36");
    }

    public static void Set_AbsorbRatioAndDashSpeed() {
        Eq(1.25, new MutationSet().AbsorbRatio, "порог поглощения база 1.25");
        var h = new MutationSet(); h.Add("m_hunger");
        Eq(1.10, h.AbsorbRatio, "голод → 1.10");
        h.Add("m_hunger"); // сверх максимума — игнор
        Eq(1.10, h.AbsorbRatio, "второй стак голода игнорируется");
        Eq(1.0, new MutationSet().DashSpeedMult, "скорость рывка база ×1");
        var c = new MutationSet(); c.Add("m_crystal");
        Eq(0.85, c.DashSpeedMult, "кристалл → ×0.85");
    }

    public static void Set_ToArrayFromArrayRoundTrip() {
        var m = new MutationSet();
        m.Add("m_spikes"); m.Add("m_spikes"); m.Add("m_dash2");
        var arr = m.ToArray();
        Eq(3, arr.Length, "массив хранит стаки (повторы id)");
        var restored = MutationSet.FromArray(arr);
        Eq(2, restored.Stacks("m_spikes"), "round-trip: шипы ×2");
        Eq(1, restored.Stacks("m_dash2"), "round-trip: рывок ×1");
        Eq(2, restored.DashCharges, "производные восстановились");
        Eq(2.25, restored.DamageDealtMult, "множители восстановились (2 стака шипов)");
    }

    public static void Set_FromArrayFiltersGarbage() {
        var m = MutationSet.FromArray(new string[] { "m_spikes", "m_spikes", "m_spikes", "мусор", null });
        Eq(2, m.Stacks("m_spikes"), "лишние стаки отфильтрованы");
        Eq(2, m.ToArray().Length, "мусор не попал в набор");
        Eq(0, MutationSet.FromArray(null).ToArray().Length, "null-массив безопасен");
    }
}
}
