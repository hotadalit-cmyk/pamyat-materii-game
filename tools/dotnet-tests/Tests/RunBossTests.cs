using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunBossTests {

    private static BossInput NearInput(double dt) {
        return new BossInput { SelfPos = new Vec2(0, 0), PlayerPos = new Vec2(3, 0), PlayerMass = 10.0, Dt = dt };
    }

    private static BossInput FarInput(double dt) {
        return new BossInput { SelfPos = new Vec2(0, 0), PlayerPos = new Vec2(20, 0), PlayerMass = 10.0, Dt = dt };
    }

    public static void Ctor_InitialState() {
        var boss = new BossState(30.0, 1UL);
        Eq(30.0, boss.Mass, "стартовая масса 30");
        Eq(30.0, boss.StartMass, "StartMass 30");
        Eq(BossPhase.Dormant, boss.Phase, "до пробуждения дремлет");
        True(!boss.IsExposed, "не обнажён");
        var degenerate = new BossState(0.0, 1UL);
        Eq(1.0, degenerate.StartMass, "нулевая стартовая масса заменяется на 1");
        Eq(1.0, degenerate.Mass, "масса равна стартовой");
    }

    public static void Think_DormantWhileFar() {
        var boss = new BossState(30.0, 3UL);
        for (int i = 0; i < 5; i++) {
            var o = boss.Think(FarInput(1.0));
            Eq(BossPhase.Dormant, boss.Phase, "далеко — дремлет (такт " + i + ")");
            True(!o.Impulse, "без рывков (такт " + i + ")");
            Eq(0, o.SpawnMinions, "без миньонов");
            True(!o.Shockwave, "без волн");
        }
        Eq(30.0, boss.Mass, "масса не меняется ото сна");
    }

    public static void Think_WakesAndCharges() {
        var boss = new BossState(30.0, 5UL);
        var o = boss.Think(NearInput(0.0));
        Eq(BossPhase.Charge, boss.Phase, "при массе 100% — фаза Charge");
        True(o.Impulse, "пробуждение — немедленный рывок");
        Eq(1.0, o.Strength01, "рывок в полную силу");
        True(o.Direction.Approx(new Vec2(1, 0)), "рывок к игроку");
        Eq(0, o.SpawnMinions, "в Charge миньонов нет");
        True(!o.Shockwave, "в Charge волн нет");
    }

    public static void Think_ChargeDashEvery24Sec() {
        var boss = new BossState(30.0, 7UL);
        boss.Think(NearInput(0.0)); // первый рывок
        var in1 = NearInput(1.0);
        True(!boss.Think(in1).Impulse, "1.0 c — кулдаун");
        True(!boss.Think(in1).Impulse, "2.0 c — кулдаун");
        True(boss.Think(in1).Impulse, "3.0 c >= 2.4 — новый рывок");
        True(!boss.Think(in1).Impulse, "ещё 1.0 c — снова кулдаун");
    }

    public static void Think_DormantFreezesTimers() {
        var boss = new BossState(30.0, 9UL);
        boss.Think(NearInput(0.0)); // рывок, кулдаун 2.4
        boss.Think(FarInput(100.0)); // сон: таймер стоит
        Eq(BossPhase.Dormant, boss.Phase, "уснул");
        True(!boss.Think(NearInput(2.3)).Impulse, "после сна 2.3 c — кулдаун ещё жив (2.4)");
        True(boss.Think(NearInput(0.2)).Impulse, "2.5 c от пробуждения — рывок");
    }

    public static void ApplyHit_ArmorBelowSix() {
        var boss = new BossState(30.0, 11UL);
        Eq(0.0, boss.ApplyHit(5.99, 100.0, null, 5), "медленный удар не пробивает броню");
        Eq(30.0, boss.Mass, "масса не тронута");
        double loss = boss.ApplyHit(6.0, 10.0, null, 0);
        Eq(0.75, loss, "скорость 6 уже ранит: v0.14 — 0.025×6×10×√(10/40)");
        Eq(29.25, boss.Mass, "масса уменьшилась");
    }

    public static void ApplyHit_Formula() {
        var boss = new BossState(30.0, 13UL);
        double loss = boss.ApplyHit(8.0, 10.0, null, 0);
        Eq(1.0, loss, "v0.14: 0.025×8×10×√(10/40) — от массы героя и √доли");
        Eq(29.0, boss.Mass, "30 − 1.0");
        double loss2 = boss.ApplyHit(8.0, 10.0, null, 0);
        Eq(0.025 * 8.0 * 10.0 * System.Math.Sqrt(10.0 / (10.0 + 29.0)), loss2, "формула использует текущую массу");
    }

    public static void ApplyHit_RicochetAndSpikesBonus() {
        var rico = new MutationSet();
        rico.Add("m_ricochet");
        rico.Add("m_ricochet");
        var boss = new BossState(30.0, 15UL);
        double loss = boss.ApplyHit(8.0, 10.0, rico, 2);
        Eq(2.0, loss, "1.0 × (1 + 0.5×2 отскока)");

        var spikes = new MutationSet();
        spikes.Add("m_spikes");
        var boss2 = new BossState(30.0, 15UL);
        Eq(1.5, boss2.ApplyHit(8.0, 10.0, spikes, 0), "шипы: 1.0 × 1.5");

        var both = new MutationSet();
        both.Add("m_spikes");
        both.Add("m_ricochet");
        var boss3 = new BossState(30.0, 15UL);
        Eq(2.25, boss3.ApplyHit(8.0, 10.0, both, 2), "шипы ×1.5 и рикошет ×1.5 (1 стак) складываются");
    }

    public static void Phases_ByMass() {
        var boss = new BossState(100.0, 17UL);
        boss.Think(NearInput(0.0));
        Eq(BossPhase.Charge, boss.Phase, "100% — Charge");
        while (boss.Mass > 70.0) boss.ApplyHit(9.0, 60.0, null, 0);
        boss.Think(NearInput(0.0));
        Eq(BossPhase.Charge, boss.Phase, "~69% — всё ещё Charge");
        while (boss.Mass > 50.0) boss.ApplyHit(9.0, 60.0, null, 0);
        boss.Think(NearInput(0.0));
        Eq(BossPhase.Brood, boss.Phase, "~49% — Brood");
        while (boss.Mass > 40.0) boss.ApplyHit(9.0, 60.0, null, 0);
        boss.Think(NearInput(0.0));
        Eq(BossPhase.Frenzy, boss.Phase, "~39% — Frenzy");
        True(!boss.IsExposed, "выше 30% — ещё не обнажён");
        while (boss.Mass > 25.0) boss.ApplyHit(9.0, 60.0, null, 0);
        True(boss.IsExposed, "ниже 30% — обнажён");
        boss.Think(NearInput(0.0));
        Eq(BossPhase.Exposed, boss.Phase, "~24% — Exposed");
    }

    public static void Brood_MinionsEvery7Sec() {
        var boss = new BossState(100.0, 19UL);
        while (boss.Mass > 50.0) boss.ApplyHit(9.0, 60.0, null, 0);
        var o = boss.Think(NearInput(0.0));
        Eq(BossPhase.Brood, boss.Phase, "фаза Brood");
        Eq(3, o.SpawnMinions, "первая волна — 3 миньона сразу");
        var in3 = NearInput(3.0);
        Eq(0, boss.Think(in3).SpawnMinions, "3 c — миньонов нет");
        Eq(0, boss.Think(in3).SpawnMinions, "6 c — миньонов нет");
        Eq(3, boss.Think(in3).SpawnMinions, "9 c >= 7 — новая волна миньонов");
    }

    public static void Frenzy_DashFasterAndShockwave() {
        var boss = new BossState(100.0, 21UL);
        while (boss.Mass > 40.0) boss.ApplyHit(9.0, 60.0, null, 0);
        var o = boss.Think(NearInput(0.0));
        Eq(BossPhase.Frenzy, boss.Phase, "фаза Frenzy");
        True(o.Shockwave, "первая волна — сразу");
        True(o.Impulse, "и рывок тоже");
        var in14 = NearInput(1.3);
        True(!boss.Think(in14).Impulse, "1.3 c — рывковый кулдаун 1.4 ещё держит");
        True(boss.Think(NearInput(0.2)).Impulse, "1.5 c от рывка — новый рывок");
        // таймер волны после первых тактов: 5 − 1.3 − 0.2 = 3.5
        True(!boss.Think(NearInput(3.4)).Shockwave, "ещё не прошло 5 с от волны");
        True(boss.Think(NearInput(0.2)).Shockwave, "5 с от волны — новая волна");
    }

    public static void Exposed_FleesSlowly() {
        var boss = new BossState(100.0, 23UL);
        while (boss.Mass > 25.0) boss.ApplyHit(9.0, 60.0, null, 0);
        var o = boss.Think(NearInput(0.0));
        Eq(BossPhase.Exposed, boss.Phase, "фаза Exposed");
        True(o.Impulse, "обнажённый страж двигается");
        Eq(0.3, o.Strength01, "медленно — сила 0.3");
        True(Vec2.Dot(o.Direction, new Vec2(1, 0)) < 0.0, "направление — прочь от игрока");
        Eq(0, o.SpawnMinions, "не атакует миньонами");
        True(!o.Shockwave, "не атакует волнами");
        True(!boss.Think(NearInput(0.5)).Impulse, "0.5 c — отдыхает от побега");
        True(boss.Think(NearInput(0.6)).Impulse, "1.1 c — снова медленно уходит");
    }

    public static void Determinism_SameSeedSameBehavior() {
        var a = new BossState(100.0, 42UL);
        var b = new BossState(100.0, 42UL);
        bool same = true;
        for (int k = 0; k < 30; k++) {
            var input = new BossInput {
                SelfPos = new Vec2(0, 0),
                PlayerPos = new Vec2(3.0 * System.Math.Cos(0.3 * k), 3.0 * System.Math.Sin(0.3 * k)),
                PlayerMass = 12.0,
                Dt = 0.7,
            };
            var oa = a.Think(input);
            var ob = b.Think(input);
            if (oa.Impulse != ob.Impulse) same = false;
            if (!oa.Direction.Approx(ob.Direction)) same = false;
            if (System.Math.Abs(oa.Strength01 - ob.Strength01) > 1e-9) same = false;
            if (oa.SpawnMinions != ob.SpawnMinions) same = false;
            if (oa.Shockwave != ob.Shockwave) same = false;
            a.ApplyHit(7.0, 20.0, null, 0);
            b.ApplyHit(7.0, 20.0, null, 0);
            if (System.Math.Abs(a.Mass - b.Mass) > 1e-9) same = false;
        }
        True(same, "одинаковый seed — одинаковое поведение стража");
    }
}
}
