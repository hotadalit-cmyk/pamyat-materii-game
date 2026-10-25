using PM.Domain.Damage;
using static PMHarness.Check;
namespace PMHarness {
public static class DamageTests {
    public static void ReducedMass_Formula() {
        Eq(0.5, ImpactDamage.ReducedMass(1, 1), "reduced mass 1-1");
        Eq(0.66666, ImpactDamage.ReducedMass(1, 2), "reduced mass 1-2");
        Eq(0.0, ImpactDamage.ReducedMass(0, 2), "zero mass -> zero");
    }
    public static void Damage_GrowsWithSpeedSquared() {
        double slow = ImpactDamage.ComputeDamage(2, 2, 2, 1, 1, 1, 0.05, 10);
        double fast = ImpactDamage.ComputeDamage(2, 2, 4, 1, 1, 1, 0.05, 10);
        Eq(4.0, fast / slow, "2x speed = 4x damage", 1e-6);
        True(fast > slow, "faster hit hurts more");
    }
    public static void Damage_Capped() {
        double d = ImpactDamage.ComputeDamage(100, 100, 50, 3, 1, 0.2, 1.0, cap: 0.4);
        Eq(0.4, d, "damage capped");
    }
    public static void Damage_ResistanceReduces() {
        double bare = ImpactDamage.ComputeDamage(2, 2, 5, 1, 1, 1.0, 0.05, 10);
        double armored = ImpactDamage.ComputeDamage(2, 2, 5, 1, 1, 2.0, 0.05, 10);
        Eq(bare / 2, armored, "double resistance = half damage", 1e-6);
    }
    public static void Damage_HitZoneScales() {
        double plain = ImpactDamage.ComputeDamage(2, 2, 5, 1.0, 1, 1, 0.05, 10);
        double weakSpot = ImpactDamage.ComputeDamage(2, 2, 5, 1.5, 1, 1, 0.05, 10);
        Eq(plain * 1.5, weakSpot, "hit zone multiplier", 1e-6);
    }
    public static void Debouncer_OneHitPerWindow() {
        var d = new DamageDebouncer(0.35);
        True(d.TryRegister(1, 2, 0.0), "first hit registered");
        True(!d.TryRegister(1, 2, 0.1), "same pair within cooldown rejected");
        d.BeginFrame(); // новый кадр — frame-guard снят, кулдаун живёт
        True(!d.TryRegister(1, 2, 0.1), "cooldown persists across frames");
        d.BeginFrame();
        True(d.TryRegister(1, 2, 0.4), "after cooldown allowed");
    }
    public static void Debouncer_PairOrderIrrelevant() {
        var d = new DamageDebouncer(0.5);
        True(d.TryRegister(5, 3, 0.0), "pair a-b");
        True(!d.TryRegister(3, 5, 0.1), "pair b-a same pair");
    }
    public static void Debouncer_FrameCascadeGuard() {
        var d = new DamageDebouncer(10); // длинное окно: работает только frame-guard
        True(d.TryRegister(1, 2, 0), "frame hit 1");
        True(!d.TryRegister(1, 2, 0.001), "second registration same frame blocked");
        d.BeginFrame();
        True(d.TryRegister(1, 2, 10.5), "new frame and cooldown elapsed allowed");
    }
    public static void Debouncer_DifferentPairsIndependent() {
        var d = new DamageDebouncer(0.5);
        True(d.TryRegister(1, 2, 0), "pair 1-2");
        True(d.TryRegister(1, 3, 0), "pair 1-3 independent");
        True(d.TryRegister(2, 4, 0), "pair 2-4 independent");
    }
}
}
