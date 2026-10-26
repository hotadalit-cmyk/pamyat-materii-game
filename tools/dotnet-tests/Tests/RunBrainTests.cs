using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunBrainTests {

    private static BrainInput MakeInput(CreatureBehavior behavior, double selfMass, double playerMass,
                                        double px, double py, double time) {
        return new BrainInput {
            SelfPos = new Vec2(0, 0), SelfMass = selfMass, Behavior = behavior,
            PlayerPos = new Vec2(px, py), PlayerMass = playerMass, Time = time,
        };
    }

    public static void Food_NeverImpulses() {
        var brain = new CreatureBrain(1UL);
        for (int i = 0; i < 12; i++) {
            var o = brain.Think(MakeInput(CreatureBehavior.Food, 0.3, 4.0, 1.0, 0.5, i * 0.7));
            True(!o.Impulse, "спора " + i + " не двигается сама");
            Eq(0.0, o.Strength01, "сила 0");
        }
    }

    public static void Drift_ImpulseWithRandomDirection() {
        var brain = new CreatureBrain(42UL);
        var o = brain.Think(MakeInput(CreatureBehavior.Drift, 2.5, 4.0, 3.0, 1.0, 0.0));
        True(o.Impulse, "первая мысль — импульс");
        Eq(0.3, o.Strength01, "дрифт — сила 0.3");
        Eq(1.0, o.Direction.Length, "нормированное направление");
        // разные seed дают разные направления (не все совпадают)
        bool anyDifferent = false;
        var first = new CreatureBrain(1UL).Think(MakeInput(CreatureBehavior.Drift, 2.5, 4.0, 3.0, 1.0, 0.0));
        for (ulong seed = 2; seed <= 12; seed++) {
            var other = new CreatureBrain(seed).Think(MakeInput(CreatureBehavior.Drift, 2.5, 4.0, 3.0, 1.0, 0.0));
            if (!other.Direction.Approx(first.Direction, 0.1)) anyDifferent = true;
        }
        True(anyDifferent, "направления дрифта различаются по seed");
    }

    public static void Cooldown_NoMoreThanOneImpulsePerInterval() {
        var brain = new CreatureBrain(7UL);
        True(brain.Think(MakeInput(CreatureBehavior.Drift, 2.0, 4.0, 5.0, 5.0, 0.0)).Impulse, "t=0 импульс");
        True(!brain.Think(MakeInput(CreatureBehavior.Drift, 2.0, 4.0, 5.0, 5.0, 0.8)).Impulse,
            "t=0.8 < минимального интервала 0.9 — пауза");
        True(!brain.Think(MakeInput(CreatureBehavior.Drift, 2.0, 4.0, 5.0, 5.0, 1.0)).Impulse,
            "t=1.0 — всё ещё возможен кулдаун");
        True(brain.Think(MakeInput(CreatureBehavior.Drift, 2.0, 4.0, 5.0, 5.0, 2.2)).Impulse,
            "t=2.2 >= любого интервала (макс 2.2) — новый импульс");
    }

    public static void Cooldown_TwoImpulsesMinimumGap() {
        var brain = new CreatureBrain(9UL);
        brain.Think(MakeInput(CreatureBehavior.Drift, 2.0, 4.0, 5.0, 5.0, 10.0));
        for (double t = 10.05; t < 10.9; t += 0.05)
            True(!brain.Think(MakeInput(CreatureBehavior.Drift, 2.0, 4.0, 5.0, 5.0, t)).Impulse,
                "t=" + t.ToString("0.00") + " раньше минимального интервала");
    }

    public static void Flee_RunsFromCloseHeavierPlayer() {
        var brain = new CreatureBrain(11UL);
        var o = brain.Think(MakeInput(CreatureBehavior.Flee, 2.0, 5.0, 1.5, 0.0, 0.0));
        True(o.Impulse, "близко и тяжелее — импульс");
        True(o.Direction.X < -0.9, "бежит от игрока (влево), X=" + o.Direction.X.ToString("0.##"));
        True(o.Strength01 > 0.9, "паника на полной силе");
    }

    public static void Flee_FarPlayerDriftsInstead() {
        var brain = new CreatureBrain(13UL);
        var o = brain.Think(MakeInput(CreatureBehavior.Flee, 2.0, 5.0, 10.0, 0.0, 0.0));
        True(o.Impulse, "двигается");
        Eq(0.3, o.Strength01, "далеко — спокойно дрейфует");
    }

    public static void Chase_MovesTowardWeakerPlayer() {
        var brain = new CreatureBrain(15UL);
        var o = brain.Think(MakeInput(CreatureBehavior.Chase, 5.0, 2.0, 0.0, 2.5, 0.0));
        True(o.Impulse, "близко и сам тяжелее — атака");
        True(o.Direction.Y > 0.9, "движется к игроку (вверх), Y=" + o.Direction.Y.ToString("0.##"));
        Eq(1.0, o.Strength01, "погоня на полной силе");
    }

    public static void Chase_FleesWhenPlayerHeavier() {
        var brain = new CreatureBrain(17UL);
        var o = brain.Think(MakeInput(CreatureBehavior.Chase, 2.0, 5.0, 0.0, 2.5, 0.0));
        True(o.Impulse, "импульс есть");
        True(o.Direction.Y < -0.9, "тяжёлый игрок близко — бегство вниз");
    }

    public static void Charger_PausesExactly2Seconds() {
        var brain = new CreatureBrain(19UL);
        var first = brain.Think(MakeInput(CreatureBehavior.Charger, 4.0, 2.0, 3.0, 0.0, 0.0));
        True(first.Impulse, "рывок к игроку");
        Eq(1.0, first.Strength01, "сильный рывок 1.0");
        True(first.Direction.X > 0.9, "в сторону игрока");
        True(!brain.Think(MakeInput(CreatureBehavior.Charger, 4.0, 2.0, 3.0, 0.0, 1.99)).Impulse,
            "пауза 2 с ещё не вышла");
        var second = brain.Think(MakeInput(CreatureBehavior.Charger, 4.0, 2.0, 3.0, 0.0, 2.0));
        True(second.Impulse, "ровно через 2 с — новый рывок");
        Eq(1.0, second.Strength01, "снова полная сила");
    }

    public static void Charger_WaitsWhenPlayerFar() {
        var brain = new CreatureBrain(21UL);
        True(!brain.Think(MakeInput(CreatureBehavior.Charger, 4.0, 2.0, 20.0, 0.0, 0.0)).Impulse, "далеко — ждёт");
        True(!brain.Think(MakeInput(CreatureBehavior.Charger, 4.0, 2.0, 20.0, 0.0, 1.0)).Impulse, "ждёт...");
        var near = brain.Think(MakeInput(CreatureBehavior.Charger, 4.0, 2.0, 5.0, 0.0, 2.5));
        True(near.Impulse, "игрок подошёл ближе 7 — рывок без задержки");
        Eq(1.0, near.Strength01, "полная сила");
    }

    public static void Brain_DeterministicBySeed() {
        var a = new CreatureBrain(2024UL);
        var b = new CreatureBrain(2024UL);
        for (int i = 0; i < 8; i++) {
            double t = i * 1.3;
            var oa = a.Think(MakeInput(CreatureBehavior.Flee, 2.0, 5.0, 1.0, 0.5, t));
            var ob = b.Think(MakeInput(CreatureBehavior.Flee, 2.0, 5.0, 1.0, 0.5, t));
            Eq(oa.Impulse, ob.Impulse, "импульсы совпадают");
            True(oa.Direction.Approx(ob.Direction), "направления совпадают");
            Eq(oa.Strength01, ob.Strength01, "силы совпадают");
        }
    }
}
}
