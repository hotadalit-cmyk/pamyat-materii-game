using System;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

/// <summary>
/// v0.14: боевая модель из Docs/BALANCE_v14.md §3 — урон героя по существу от массы героя,
/// скорости и √доли (делится на Toughness); урон существа герою — процент массы героя
/// (границы 3–25%), проглатывание героя только от ×2, вилка 1.25–2× — сильный укус.
/// </summary>
public static class RunSizeRuleTests {

    public static void RadiusForMass_BasicAndClamp() {
        Eq(0.7, SizeRule.RadiusForMass(4.0), "радиус массы 4 = 0.7");
        Eq(0.35, SizeRule.RadiusForMass(1.0), "радиус массы 1 = 0.35");
        Eq(0.35 * Math.Sqrt(0.05), SizeRule.RadiusForMass(0.0), "масса 0 клампится к 0.05");
        Eq(0.35 * Math.Sqrt(0.05), SizeRule.RadiusForMass(-3.0), "отрицательная масса клампится");
        Eq(0.35 * Math.Sqrt(24.0), SizeRule.RadiusForMass(24.0), "масса 24 (пик забега)");
    }

    public static void Resolve_AbsorbsWhenBigEnough() {
        var empty = new MutationSet();
        var r = SizeRule.Resolve(5.0, empty, 4.0, empty, 3.0);
        Eq(1, r.Absorbed, "5 >= 4×1.25 → A поглотил B");
        Eq(0.0, r.MassLossA, "при поглощении A без потерь");
        Eq(0.0, r.MassLossB, "при поглощении B без потерь");
    }

    // v0.14: существо глотает героя только от ×2; вилка 1.25–2× — сильный укус (12% массы героя)
    public static void Resolve_StrongBiteInsteadOfSwallow() {
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, empty, 5.05, empty, 3.0);
        Eq(0, r.Absorbed, "5.05 < 4×2 → проглотa нет (было 1.25)");
        Eq(0.12 * 4.0, r.MassLossA, "сильный укус: 12% массы героя (пол формулы)");
        True(r.MassLossB > 0.0, "и существо тоже получает урон");
        // ровно ×2 — проглот
        var r2 = SizeRule.Resolve(4.0, empty, 8.0, empty, 3.0);
        Eq(2, r2.Absorbed, "8 >= 4×2 → B поглотил A");
        // чуть меньше ×2 — ещё сильный укус
        var r3 = SizeRule.Resolve(4.0, empty, 7.9, empty, 3.0);
        Eq(0, r3.Absorbed, "7.9 < 8 — только укус");
        Eq(0.48, r3.MassLossA, "пол сильного укуса не зависит от скорости (rel 3: 0.08×0.5 < 0.12)");
    }

    public static void Resolve_HitWhenComparableMasses() {
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, empty, 3.5, empty, 5.0);
        Eq(0, r.Absorbed, "сопоставимые массы → удар");
        // урон герою: Attack 0.08 (по умолчанию) × скорость 5/6 × глубина 1 = 6.7% массы
        Eq(4.0 * 0.08 * (5.0 / 6.0), r.MassLossA, "потеря A = масса × HitFrac");
        // урон существу: 0.025×4×5×√(4/7.5)×(5/6)
        Eq(0.025 * 4.0 * 5.0 * Math.Sqrt(4.0 / 7.5) * (5.0 / 6.0), r.MassLossB, "потеря B по формуле v0.14");
        True(r.MassLossA > 0.0 && r.MassLossB > 0.0, "потери положительные");
    }

    public static void Resolve_HeroDamageScalesWithHeroMass() {
        var empty = new MutationSet();
        // масштабная инвариантность: герой и цель ×4 тяжелее — TTK тот же (потери ×4)
        double l1 = SizeRule.Resolve(10.0, empty, 10.0, empty, 8.0, 0.0, 1.0, 1).MassLossB;
        double l2 = SizeRule.Resolve(40.0, empty, 40.0, empty, 8.0, 0.0, 1.0, 1).MassLossB;
        Eq(4.0, l2 / l1, "массы выросли ×4 — потери ×4, ударов до смерти столько же");
        // герой чуть тяжелее (в пределах 1.25×) — √доля растёт медленнее массы
        double heavy = SizeRule.Resolve(12.0, empty, 10.0, empty, 8.0, 0.0, 1.0, 1).MassLossB;
        double expectedRatio = (12.0 * Math.Sqrt(12.0 / 22.0)) / (10.0 * Math.Sqrt(0.5));
        Eq(expectedRatio, heavy / (0.025 * 10.0 * 8.0 * Math.Sqrt(0.5)),
            "√доля: превосходство в массе даёт меньше линейного");
        // одинаковые массы — TTK ~7 ударов (Docs/BALANCE_v14.md §3.4)
        double equal = SizeRule.Resolve(50.0, empty, 50.0, empty, 8.0, 0.0, 1.0, 1).MassLossB;
        Eq(5.0 * Math.Sqrt(2.0), 50.0 / equal, "равного врага — 7.07 удара до смерти");
    }

    public static void Resolve_ToughnessDividesDamage() {
        var empty = new MutationSet();
        double soft = SizeRule.Resolve(10.0, empty, 10.0, empty, 8.0, 0.0, 1.0, 1).MassLossB;
        double armored = SizeRule.Resolve(10.0, empty, 10.0, empty, 8.0, 0.0, 2.0, 1).MassLossB;
        Eq(soft / 2.0, armored, "Toughness 2 — вдвое меньше потери массы (HP = масса × Toughness)");
    }

    public static void Resolve_HungerLowersAbsorbThreshold() {
        var hunger = new MutationSet(); hunger.Add("m_hunger");
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, hunger, 3.6, empty, 0.5);
        Eq(1, r.Absorbed, "с голодом 4 >= 3.6×1.10");
        var r2 = SizeRule.Resolve(4.0, empty, 3.6, empty, 0.5);
        Eq(0, r2.Absorbed, "без голода 4 < 3.7×1.25");
        Eq(0.0, r2.MassLossA, "медленное касание без потерь A");
        Eq(0.0, r2.MassLossB, "медленное касание без потерь B");
    }

    // v0.14: голод существа больше не даёт ему проглот героя — только герой ест по своему порогу
    public static void Resolve_BigEnemyBitesHardNotSwallows() {
        var hungerB = new MutationSet(); hungerB.Add("m_hunger");
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, empty, 7.0, hungerB, 2.0);
        Eq(0, r.Absorbed, "7 < 4×2 — проглотa нет, даже с голодом (было 1.25)");
        Eq(0.12 * 4.0, r.MassLossA, "но это сильный укус: 12% массы героя");
        // совсем крупный — проглатывает, как и раньше по правилу ×2
        var r2 = SizeRule.Resolve(4.0, empty, 8.1, hungerB, 2.0);
        Eq(2, r2.Absorbed, "8.1 >= 8 — проглот");
    }

    public static void Resolve_NoLossBelowTouchSpeed() {
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, empty, 3.5, empty, 0.999);
        Eq(0, r.Absorbed, "нет поглощения");
        Eq(0.0, r.MassLossA, "скорость < 1: касание без потерь A");
        Eq(0.0, r.MassLossB, "скорость < 1: касание без потерь B");
    }

    public static void Resolve_SpikesIncreaseDealtDamage() {
        var spikes = new MutationSet(); spikes.Add("m_spikes");
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, spikes, 3.5, empty, 5.0);
        Eq(0.025 * 4.0 * 5.0 * Math.Sqrt(4.0 / 7.5) * (5.0 / 6.0) * 1.5, r.MassLossB, "шипы ×1.5 к урону по B");
        Eq(4.0 * 0.08 * (5.0 / 6.0), r.MassLossA, "по A шипы не защищают");
    }

    public static void Resolve_ShellReducesTakenDamage() {
        var shell1 = new MutationSet(); shell1.Add("m_shell");
        var shell2 = new MutationSet(); shell2.Add("m_shell"); shell2.Add("m_shell");
        var empty = new MutationSet();
        double baseLoss = 0.025 * 4.0 * 5.0 * Math.Sqrt(4.0 / 7.5) * (5.0 / 6.0);
        Eq(baseLoss * 0.7, SizeRule.Resolve(4.0, empty, 3.5, shell1, 5.0).MassLossB, "панцирь ×0.7");
        Eq(baseLoss * 0.49, SizeRule.Resolve(4.0, empty, 3.5, shell2, 5.0).MassLossB, "панцирь ×2 = ×0.49");
    }

    // v0.14: границы урона герою 3–25% массы за удар, множитель глубин ×1.0…×2.1
    public static void Resolve_HitFracBounds() {
        var empty = new MutationSet();
        // герой 10 против существа 9 — окно удара (не еда и не проглот)
        // слабая атака и медленный удар — нижняя граница 3%
        double lo = SizeRule.Resolve(10.0, empty, 9.0, empty, 2.0, 0.001, 1.0, 1).MassLossA;
        Eq(0.03 * 10.0, lo, "нижняя граница 3% массы героя");
        // мощная атака на глубине 5 — верхняя граница 25%
        double hi = SizeRule.Resolve(10.0, empty, 9.0, empty, 9.0, 0.5, 1.0, 5).MassLossA;
        Eq(0.25 * 10.0, hi, "верхняя граница 25% массы героя");
        // промежуточно: клещ (0.05) на Г1 — 5% массы
        double mite = SizeRule.Resolve(10.0, empty, 9.0, empty, 9.0, 0.05, 1.0, 1).MassLossA;
        Eq(0.05 * 10.0, mite, "клещ Г1: 5% массы героя за удар");
        // шипастик (0.16) на Г4: 0.16×1.75 = 28% → срезано до 25%
        double spike = SizeRule.Resolve(10.0, empty, 9.0, empty, 9.0, 0.16, 1.0, 4).MassLossA;
        Eq(0.25 * 10.0, spike, "шипастик Г4: зажат верхней границей");
    }

    public static void DepthDamageMult_Table() {
        double[] expect = { 1.0, 1.2, 1.45, 1.75, 2.1 };
        for (int d = 1; d <= 5; d++) Eq(expect[d - 1], SizeRule.DepthDamageMult(d), "множитель глубины " + d);
        Eq(1.0, SizeRule.DepthDamageMult(0), "ниже 1 — как Г1");
        Eq(2.1, SizeRule.DepthDamageMult(9), "выше 5 — как Г5");
    }

    public static void Resolve_AttackZeroMeansNoContactDamage() {
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, empty, 3.5, empty, 5.0, 0.0, 1.0, 3);
        Eq(0.0, r.MassLossA, "Attack 0 (пища/споры) — контакта без урона по герою");
        True(r.MassLossB > 0.0, "герой при этом ест существо как обычно");
    }

    public static void Resolve_NullMutationSetsAreSafe() {
        var r = SizeRule.Resolve(5.0, null, 4.0, null, 3.0);
        Eq(1, r.Absorbed, "null-наборы трактуются как пустые");
        var hit = SizeRule.Resolve(4.0, null, 3.5, null, 5.0);
        Eq(4.0 * 0.08 * (5.0 / 6.0), hit.MassLossA, "базовая потеря A");
        Eq(0.025 * 4.0 * 5.0 * Math.Sqrt(4.0 / 7.5) * (5.0 / 6.0), hit.MassLossB, "базовая потеря B");
    }

    public static void SpeciesCatalog_AttackToughnessGrowth() {
        var spore = SpeciesCatalog.Get("spore");
        Eq(0.0, spore.Attack, "спора не ранит");
        var mite = SpeciesCatalog.Get("mite");
        Eq(0.05, mite.Attack, "клещ: Attack 5%");
        Eq(0.8, mite.Toughness, "клещ: жёсткость 0.8");
        Eq(1.28, mite.MassAtDepth(5), "клещ Г5: 0.8×(1+0.15×4)");
        var crab = SpeciesCatalog.Get("shard_crab");
        Eq(1.8, crab.Toughness, "краб: жёсткость 1.8");
        Eq(12.5, crab.MassAtDepth(4), "краб Г4: 5×(1+0.5×3)");
        var slime = SpeciesCatalog.Get("soul_slime");
        Eq(16.0, slime.MassAtDepth(5), "душеслизень Г5: 4×4.0 = старая спец-формула 4+3×4");
        var warden = SpeciesCatalog.Get("warden");
        Eq(0.11, warden.Attack, "тело стража: Attack 11%");
        Eq(1.2, warden.Toughness, "тело стража: жёсткость 1.2");
    }

    public static void CreatureGrowthCap_Constant() {
        Eq(1.5, SizeRule.CreatureGrowthCap, "потолок роста существа ×1.5 от массы спавна");
    }

    public static void Droplets_SumEqualsLossAndCapped() {
        var d = SizeRule.SplitIntoDroplets(2.0);
        Eq(8, d.Length, "2.0 массы → 8 капель по 0.25");
        double sum = 0; for (int i = 0; i < d.Length; i++) sum += d[i];
        Eq(2.0, sum, "сумма капель = потере");
        var big = SizeRule.SplitIntoDroplets(3.0);
        Eq(8, big.Length, "не больше 8 капель");
        double bigSum = 0; for (int i = 0; i < big.Length; i++) bigSum += big[i];
        Eq(3.0, bigSum, "сумма больших капель = потере");
        Eq(0.375, big[0], "3.0/8 = 0.375 на каплю");
    }

    public static void Droplets_SmallAndZeroLosses() {
        var one = SizeRule.SplitIntoDroplets(0.1);
        Eq(1, one.Length, "0.1 → одна капля");
        Eq(0.1, one[0], "капля = вся потеря");
        var two = SizeRule.SplitIntoDroplets(0.5);
        Eq(2, two.Length, "0.5 → две капли");
        Eq(0.25, two[0], "по 0.25");
        Eq(0, SizeRule.SplitIntoDroplets(0.0).Length, "нулевая потеря → без капель");
        Eq(0, SizeRule.SplitIntoDroplets(-1.0).Length, "отрицательная потеря → без капель");
    }

    public static void DashCost_BaseAndMinClamp() {
        var empty = new MutationSet();
        Eq(0.1, SizeRule.DashCost(4.0, empty, 1.0), "0.025×4 = 0.1");
        Eq(0.04, SizeRule.DashCost(1.0, empty, 1.0), "минимум 0.04");
        Eq(0.04, SizeRule.DashCost(0.5, empty, 1.0), "мелкая масса → минимум 0.04");
        Eq(0.25, SizeRule.DashCost(10.0, empty, 1.0), "0.025×10 = 0.25");
    }

    public static void DashCost_Multipliers() {
        var light1 = new MutationSet(); light1.Add("m_light");
        var light2 = new MutationSet(); light2.Add("m_light"); light2.Add("m_light");
        Eq(0.06, SizeRule.DashCost(4.0, light1, 1.0), "лёгкость ×0.6");
        Eq(0.036, SizeRule.DashCost(4.0, light2, 1.0), "лёгкость ×2 = ×0.36");
        Eq(0.076, SizeRule.DashCost(4.0, new MutationSet(), 0.76), "душа Endurance lvl 3 → ×0.76");
        Eq(0.1, SizeRule.DashCost(4.0, null, 1.0), "null-набор безопасен");
    }
}
}
