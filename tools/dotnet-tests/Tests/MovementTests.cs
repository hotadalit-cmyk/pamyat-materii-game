using PM.Domain.Core;
using PM.Domain.Body;
using PM.Domain.Movement;
using static PMHarness.Check;
namespace PMHarness {
public static class MovementTests {
    private static MovementConfig Cfg() => new MovementConfig();

    public static void PullCharge_DirectionIsOppositeToDrag() {
        // D-04: оттягивание ПРОТИВ движения: origin=(0,0), pointer=(2,0) → движение влево
        var r = PullCharge.Compute(Vec2.Zero, new Vec2(2, 0), dragMax: 4, minDrag: 0.35);
        True(r.Valid, "gesture valid");
        True(r.Direction.Approx(new Vec2(-1, 0), 1e-9), "direction = origin - pointer");
        Eq(0.5, r.Charge01, "charge = len/dragMax");
    }
    public static void PullCharge_MinDragRejectsAccidentalTouch() {
        var r = PullCharge.Compute(Vec2.Zero, new Vec2(0.1, 0), 4, 0.35);
        True(!r.Valid, "below min drag invalid");
        Eq("below_min_drag", r.FailReason, "reason");
    }
    public static void PullCharge_ClampsAtDragMax() {
        var r = PullCharge.Compute(Vec2.Zero, new Vec2(0, -10), 4, 0.35);
        Eq(1.0, r.Charge01, "charge capped at 1");
        True(r.Direction.Approx(new Vec2(0, 1), 1e-9), "direction up when pulled down");
    }
    public static void JetPlanner_SpeedCurveBetweenMinAndMax() {
        var body = new BodyMassState(1, 5);
        var organs = OrganStats.BaseSlime();
        var low = JetPlanner.Plan(body, organs, PullCharge.Compute(Vec2.Zero, new Vec2(0, -0.4), 4, 0.35), 10, Cfg());
        True(low.Valid, "low jet valid");
        True(low.Impulse.Length > organs.MinSpeed * 0.4, "low charge speed near min");
        var full = JetPlanner.Plan(body, organs, PullCharge.Compute(Vec2.Zero, new Vec2(0, -4), 4, 0.35), 10, Cfg());
        Eq(organs.MaxSpeed, full.Impulse.Length, "full charge speed = max", 0.01);
    }
    public static void JetPlanner_LowEnergyWeakensNotBlocks() {
        var body = new BodyMassState(1, 5);
        var organs = OrganStats.BaseSlime();
        var full = JetPlanner.Plan(body, organs, PullCharge.Compute(Vec2.Zero, new Vec2(0, -4), 4, 0.35), 10, Cfg());
        var empty = JetPlanner.Plan(body, organs, PullCharge.Compute(Vec2.Zero, new Vec2(0, -4), 4, 0.35), 0, Cfg());
        Eq(organs.MaxSpeed * Cfg().MinEnergyFactor, empty.Impulse.Length,
            "no energy = min factor speed", 0.01);
        True(empty.Impulse.Length < full.Impulse.Length, "weakened vs full");
    }
    public static void JetPlanner_WaterCostByChargeAndClampedBySupply() {
        var rich = new BodyMassState(1, 5);
        var plan = JetPlanner.Plan(rich, OrganStats.BaseSlime(),
            PullCharge.Compute(Vec2.Zero, new Vec2(0, -4), 4, 0.35), 10, Cfg());
        double fullCost = Cfg().LaunchWaterBase * 1.0; // 0.5+0.5*1
        Eq(fullCost, plan.WaterSpent, "full charge launch cost");
        True(!plan.DehydrationRisk, "no risk with water");
        // D-16: при нехватке расход ограничен запасом
        var poor = new BodyMassState(1, 0.02);
        var plan2 = JetPlanner.Plan(poor, OrganStats.BaseSlime(),
            PullCharge.Compute(Vec2.Zero, new Vec2(0, -4), 4, 0.35), 10, Cfg());
        Eq(0.02, plan2.WaterSpent, "cost clamped to available");
        True(plan2.DehydrationRisk, "dehydration risk flagged");
        Eq(0.0, poor.Water, "water emptied");
    }
    public static void JetPlanner_InvalidGestureRejected() {
        var body = new BodyMassState(1, 5);
        var plan = JetPlanner.Plan(body, OrganStats.BaseSlime(),
            new PullChargeResult { Valid = false }, 10, Cfg());
        True(!plan.Valid, "invalid gesture -> no jet");
    }
    public static void JetPlanner_DirectionMatchesGesture() {
        var body = new BodyMassState(1, 5);
        var gesture = PullCharge.Compute(new Vec2(1, 1), new Vec2(0, 0), 4, 0.35);
        var plan = JetPlanner.Plan(body, OrganStats.BaseSlime(), gesture, 10, Cfg());
        True(plan.Impulse.Normalized().Approx(new Vec2(1, 1).Normalized(), 1e-9),
            "impulse along aim direction");
    }
    public static void WaterSpent_ReducedByRetentionDoesNotAffectLaunchCost() {
        // retention влияет только на след (путь), не на цену запуска — проверяем разделение D-16
        var body = new BodyMassState(1, 5);
        var organs = OrganStats.BaseSlime();
        organs.WaterRetention = 0.5;
        var plan = JetPlanner.Plan(body, organs, PullCharge.Compute(Vec2.Zero, new Vec2(0, -4), 4, 0.35), 10, Cfg());
        Eq(Cfg().LaunchWaterBase, plan.WaterSpent, "launch cost independent of retention");
    }
}
}
