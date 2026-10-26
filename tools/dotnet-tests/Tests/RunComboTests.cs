using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunComboTests {

    public static void Fresh_ZeroChainUnitMultiplier() {
        var combo = new ComboTracker();
        Eq(0, combo.Chain, "цепочка пуста");
        Eq(1.0, combo.Multiplier, "множитель 1");
        Eq(0, combo.Best, "рекорд пуст");
    }

    public static void FirstAbsorb_NoBonus() {
        var combo = new ComboTracker();
        Eq(0.0, combo.OnAbsorb(false, 2.0), "первое поглощение без бонуса");
        Eq(1, combo.Chain, "цепочка 1");
        Eq(1.0, combo.Multiplier, "множитель ещё 1");
        Eq(1, combo.Best, "рекорд 1");
    }

    public static void Chain_GrowsMultiplierAndBonus() {
        var combo = new ComboTracker();
        combo.OnAbsorb(false, 2.0);
        Eq(0.5, combo.OnAbsorb(false, 2.0), "второе поглощение: бонус 2×0.25");
        Eq(2, combo.Chain, "цепочка 2");
        Eq(1.25, combo.Multiplier, "множитель 1.25");
        Eq(2.0, combo.OnAbsorb(false, 4.0), "третье поглощение: бонус 4×0.5");
        Eq(1.5, combo.Multiplier, "множитель 1.5");
        Eq(3, combo.Best, "рекорд 3");
    }

    public static void Multiplier_CapsAtThree() {
        var combo = new ComboTracker();
        double eighth = 0.0, ninth = 0.0, twelfth = 0.0;
        for (int i = 1; i <= 12; i++) {
            double bonus = combo.OnAbsorb(false, 1.0);
            if (i == 8) eighth = bonus;
            if (i == 9) ninth = bonus;
            if (i == 12) twelfth = bonus;
        }
        Eq(12, combo.Chain, "цепочка 12");
        Eq(1.75, eighth, "восьмое поглощение: бонус 1.75");
        Eq(2.0, ninth, "девятое поглощение: бонус упёрся в кап");
        Eq(2.0, twelfth, "двенадцатое поглощение: всё ещё кап");
        Eq(3.0, combo.Multiplier, "множитель не выше 3");
    }

    public static void OnDash_ResetsChainKeepsBest() {
        var combo = new ComboTracker();
        for (int i = 0; i < 5; i++) combo.OnAbsorb(false, 1.0);
        Eq(5, combo.Chain, "цепочка 5 до рывка");
        combo.OnDash();
        Eq(0, combo.Chain, "рывок сбросил цепочку");
        Eq(1.0, combo.Multiplier, "множитель снова 1");
        Eq(5, combo.Best, "рекорд пережил рывок");
        Eq(0.0, combo.OnAbsorb(false, 1.0), "новая цепочка начинается без бонуса");
        Eq(5, combo.Best, "рекорд не побит");
    }

    public static void Tick_ResetsAfterWindow() {
        var combo = new ComboTracker();
        combo.OnAbsorb(false, 1.0);
        combo.OnAbsorb(false, 1.0);
        combo.Tick(2.49);
        Eq(2, combo.Chain, "2.49 с — цепочка ещё жива");
        combo.Tick(0.02);
        Eq(0, combo.Chain, "2.51 с без поглощений — цепочка погасла");
        combo.OnAbsorb(false, 1.0);
        combo.Tick(2.5);
        Eq(0, combo.Chain, "ровно 2.5 с — цепочка погасла");
        Eq(2, combo.Best, "рекорд сохраняется после таймера");
    }

    public static void Tick_NoAbsorbNoEffect() {
        var combo = new ComboTracker();
        combo.Tick(100.0);
        Eq(0, combo.Chain, "без поглощений тикать бессмысленно");
        combo.Tick(0.0);
        Eq(0, combo.Chain, "нулевой dt безопасен");
    }

    public static void Spores_DoNotGrowChain() {
        var combo = new ComboTracker();
        Eq(0.0, combo.OnAbsorb(true, 5.0), "спора без бонуса");
        Eq(0, combo.Chain, "спора не растит цепочку");
        combo.OnAbsorb(false, 2.0);
        Eq(1, combo.Chain, "не-спора растит цепочку");
        Eq(0.0, combo.OnAbsorb(true, 5.0), "спора и при живой цепочке без бонуса");
        Eq(1, combo.Chain, "цепочка не изменилась от споры");
        Eq(1.0, combo.Multiplier, "множитель не вырос от споры");
    }

    public static void OnAbsorb_ZeroMassIgnored() {
        var combo = new ComboTracker();
        Eq(0.0, combo.OnAbsorb(false, 0.0), "нулевая масса — без бонуса");
        Eq(0, combo.Chain, "нулевая масса не растит цепочку");
    }
}
}
