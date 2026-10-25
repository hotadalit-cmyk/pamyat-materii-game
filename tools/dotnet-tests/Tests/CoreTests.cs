using PM.Domain.Core;
using static PMHarness.Check;
namespace PMHarness {
public static class CoreTests {
    public static void Vec2_OperatorsAndLength() {
        var a = new Vec2(3, 4);
        Eq(5.0, a.Length, "vec2 length 3-4-5");
        Eq(25.0, a.SqrLength, "vec2 sqr length");
        True((a + new Vec2(1, 1)).Approx(new Vec2(4, 5)), "vec2 add");
        True((a - new Vec2(1, 1)).Approx(new Vec2(2, 3)), "vec2 sub");
        True((a * 2).Approx(new Vec2(6, 8)), "vec2 scale");
        True((2 * a).Approx(new Vec2(6, 8)), "vec2 scale left");
        True((a / 2).Approx(new Vec2(1.5, 2)), "vec2 div");
        True((-a).Approx(new Vec2(-3, -4)), "vec2 negate");
    }
    public static void Vec2_NormalizeDegenerate() {
        True(Vec2.Zero.Normalized().Approx(Vec2.Zero), "zero normalize safe");
        True(new Vec2(1e-13, 0).Normalized().Approx(Vec2.Zero), "tiny normalize safe");
        True(new Vec2(10, 0).Normalized().Approx(Vec2.Right), "normalize x");
    }
    public static void Vec2_Rotate() {
        var r = Vec2.Right.Rotated(System.Math.PI / 2);
        True(r.Approx(new Vec2(0, 1), 1e-9), "rotate right 90deg = up");
    }
    public static void MathUtil_ClampAndLerp() {
        Eq(1.0, MathUtil.Clamp01(1.5), "clamp01 upper");
        Eq(0.0, MathUtil.Clamp01(-0.5), "clamp01 lower");
        Eq(5.0, MathUtil.Clamp(5, 0, 10), "clamp inside");
        Eq(7.5, MathUtil.Lerp(5, 10, 0.5), "lerp half");
        Eq(10.0, MathUtil.Lerp(5, 10, 2), "lerp clamp t>1");
    }
    public static void MathUtil_MoveTowardsDoesNotOvershoot() {
        Eq(3.0, MathUtil.MoveTowards(0, 10, 3), "move towards step");
        Eq(10.0, MathUtil.MoveTowards(8, 10, 5), "move towards no overshoot");
        Eq(5.0, MathUtil.MoveTowards(10, 0, 5), "moves toward target by maxDelta");
    }
    public static void PmRandom_Deterministic() {
        var r1 = new PmRandom(42);
        var r2 = new PmRandom(42);
        for (int i = 0; i < 100; i++)
            Eq(r1.NextDouble01(), r2.NextDouble01(), $"random deterministic #{i}");
        var r3 = new PmRandom(7);
        bool inRange = true;
        for (int i = 0; i < 1000; i++) {
            double v = r3.NextDouble01();
            if (v < 0 || v >= 1) inRange = false;
        }
        True(inRange, "random values in [0;1)");
    }
    public static void TickClock_SameResultDifferentFrameRates() {
        // QA-4/FPS-независимость: 60 кадров по 1/60 == 4 кадра по 0.25
        var fast = new TickClock(1.0);
        int fastTicks = 0;
        for (int i = 0; i < 60; i++) fastTicks += fast.Advance(1.0 / 60);
        var slow = new TickClock(1.0);
        int slowTicks = 0;
        for (int i = 0; i < 4; i++) slowTicks += slow.Advance(0.25);
        Eq(fastTicks, slowTicks, "tick count independent of frame rate");
        Eq(1.0, fast.WorldTime, "world time fast");
        Eq(1.0, slow.WorldTime, "world time slow");
    }
    public static void TickClock_AccumulatesFraction() {
        var c = new TickClock(0.5);
        int fired = c.Advance(0.2);
        Eq(0, fired, "no tick yet");
        fired = c.Advance(0.2);
        Eq(0, fired, "still accumulating");
        fired = c.Advance(0.2);
        Eq(1, fired, "tick fired after 0.6 total");
        Eq(0.6, c.WorldTime, "world time counts all frame time");
    }

    public static void OrthoFraming_CoversDesignArea() {
        double s169 = PM.Domain.Core.OrthoFraming.ComputeOrthoSize(16.0 / 9.0);
        Eq(17.78 / (16.0 / 9.0) / 2.0, s169, "16:9 size width-limited", 1e-9);
        double s209 = PM.Domain.Core.OrthoFraming.ComputeOrthoSize(20.0 / 9.0);
        Eq(5.0, s209, "20:9 keeps height", 1e-9);
        double s43 = PM.Domain.Core.OrthoFraming.ComputeOrthoSize(4.0 / 3.0);
        Eq(17.78 / (4.0 / 3.0) / 2.0, s43, "4:3 expands to keep width");
        double s11 = PM.Domain.Core.OrthoFraming.ComputeOrthoSize(1.0);
        Eq(8.89, s11, "square keeps full width");
        True(PM.Domain.Core.OrthoFraming.ComputeOrthoSize(0) > 0, "degenerate aspect safe");
    }
}
}
