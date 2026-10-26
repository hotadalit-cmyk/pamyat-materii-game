using System;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

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

    public static void Resolve_AbsorbsPlayerWhenLarger() {
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, empty, 5.05, empty, 3.0);
        Eq(2, r.Absorbed, "5.05 >= 4×1.25 → B поглотил A");
        Eq(0.0, r.MassLossA, "поглощение: A без потерь");
        Eq(0.0, r.MassLossB, "поглощение: B без потерь");
    }

    public static void Resolve_HitWhenComparableMasses() {
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, empty, 3.5, empty, 5.0);
        Eq(0, r.Absorbed, "сопоставимые массы → удар");
        Eq(0.12 * 5.0 * (3.5 / 7.5), r.MassLossA, "потеря A по формуле");
        Eq(0.12 * 5.0 * (4.0 / 7.5), r.MassLossB, "потеря B по формуле");
        True(r.MassLossA > 0.0 && r.MassLossB > 0.0, "потери положительные");
        True(r.MassLossB > r.MassLossA, "тяжелый бьёт больнее");
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

    public static void Resolve_HungerOnDefenderSide() {
        var hungerB = new MutationSet(); hungerB.Add("m_hunger");
        var empty = new MutationSet();
        var r = SizeRule.Resolve(4.0, empty, 4.5, hungerB, 2.0);
        Eq(2, r.Absorbed, "голод у B: 4.5 >= 4×1.10 → B поглотил A");
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
        Eq(0.12 * 5.0 * (4.0 / 7.5) * 1.5, r.MassLossB, "шипы ×1.5 к урону по B");
        Eq(0.12 * 5.0 * (3.5 / 7.5), r.MassLossA, "по A шипы не защищают");
    }

    public static void Resolve_ShellReducesTakenDamage() {
        var shell1 = new MutationSet(); shell1.Add("m_shell");
        var shell2 = new MutationSet(); shell2.Add("m_shell"); shell2.Add("m_shell");
        var empty = new MutationSet();
        double baseLoss = 0.12 * 5.0 * (4.0 / 7.5);
        Eq(baseLoss * 0.7, SizeRule.Resolve(4.0, empty, 3.5, shell1, 5.0).MassLossB, "панцирь ×0.7");
        Eq(baseLoss * 0.49, SizeRule.Resolve(4.0, empty, 3.5, shell2, 5.0).MassLossB, "панцирь ×2 = ×0.49");
    }

    public static void Resolve_NullMutationSetsAreSafe() {
        var r = SizeRule.Resolve(5.0, null, 4.0, null, 3.0);
        Eq(1, r.Absorbed, "null-наборы трактуются как пустые");
        var hit = SizeRule.Resolve(4.0, null, 3.5, null, 5.0);
        Eq(0.28, hit.MassLossA, "базовая потеря A");
        Eq(0.32, hit.MassLossB, "базовая потеря B");
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
