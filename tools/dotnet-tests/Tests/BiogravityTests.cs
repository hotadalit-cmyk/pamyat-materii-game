using PM.Domain.Core;
using PM.Domain.Fields;
using static PMHarness.Check;
namespace PMHarness {
public static class BiogravityTests {
    private static BiogravityPull.Config Cfg() => new BiogravityPull.Config();
    public static void Pull_ZeroOutsideRange() {
        var a = BiogravityPull.ComputeAcceleration(5, 0.2, Vec2.Up, 5.9, Cfg());
        var b = BiogravityPull.ComputeAcceleration(5, 0.2, Vec2.Up, 6.1, Cfg());
        True(a.Length > 0, "inside range pulls");
        True(b.Approx(Vec2.Zero), "outside range no pull");
    }
    public static void Pull_DeadZoneNearSource() {
        var a = BiogravityPull.ComputeAcceleration(5, 0.2, Vec2.Up, 0.1, Cfg());
        True(a.Approx(Vec2.Zero), "dead zone prevents singularity");
    }
    public static void Pull_DirectionTowardsSource() {
        var acc = BiogravityPull.ComputeAcceleration(5, 0.2, new Vec2(3, 4), 5, Cfg());
        True(acc.Normalized().Approx(new Vec2(3, 4).Normalized(), 1e-9), "pull directed to source");
    }
    public static void Pull_ForceCapped() {
        // огромная масса источника — ускорение не превышает MaxForce/targetMass
        var acc = BiogravityPull.ComputeAcceleration(1e6, 0.2, Vec2.Up, 1, Cfg());
        True(acc.Length <= Cfg().MaxForce / 0.2 + 1e-9, "force capped");
    }
    public static void Pull_StrongerSourceMoreForce() {
        var weak = BiogravityPull.ComputeAcceleration(1, 0.2, Vec2.Up, 2, Cfg());
        var strong = BiogravityPull.ComputeAcceleration(10, 0.2, Vec2.Up, 2, Cfg());
        True(strong.Length > weak.Length, "more source mass = more pull");
    }
    public static void Pull_FalloffWithDistance() {
        var near = BiogravityPull.ComputeAcceleration(5, 0.2, Vec2.Up, 1.0, Cfg());
        var far = BiogravityPull.ComputeAcceleration(5, 0.2, Vec2.Up, 4.0, Cfg());
        True(near.Length > far.Length, "closer is stronger");
    }
    public static void Pull_LivingOrLargeTargetNotPulled() {
        // T07: магнит только для мелкой неживой еды
        var heavy = BiogravityPull.ComputeAcceleration(5, 5.0, Vec2.Up, 2, Cfg());
        True(heavy.Approx(Vec2.Zero), "heavy target ignored");
        var tiny = BiogravityPull.ComputeAcceleration(5, 0.001, Vec2.Up, 2, Cfg());
        True(tiny.Approx(Vec2.Zero), "below min target mass ignored");
    }
    public static void Pull_TwoMassesTwoDistances_DistinctResults() {
        // сравнение из T07: две массы и два расстояния дают разные силы
        var a1 = BiogravityPull.ComputeAcceleration(2, 0.2, Vec2.Up, 2, Cfg());
        var a2 = BiogravityPull.ComputeAcceleration(4, 0.2, Vec2.Up, 2, Cfg());
        var a3 = BiogravityPull.ComputeAcceleration(2, 0.2, Vec2.Up, 4, Cfg());
        True(a2.Length > a1.Length, "mass increases pull");
        True(a3.Length < a1.Length, "distance decreases pull");
    }
}
}
