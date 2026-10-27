using System;
using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

/// <summary>
/// v0.14: проверка кривых из Docs/BALANCE_v14.md — табличная цель глубины (±10% от карты),
/// герой DevTools в коридоре глубин, масштаб стражей от радиуса тела (герой массой 300
/// рядом со стражем — бой просыпается), порядок радиусов.
/// </summary>
public static class BalanceV14Tests {

    private static readonly double[] GoalTable = { 15.0, 32.0, 65.0, 115.0, 190.0 };
    private static readonly double[] CorridorLo = { 6.0, 20.0, 45.0, 90.0, 160.0 };
    private static readonly double[] CorridorHi = { 20.0, 45.0, 90.0, 160.0, 250.0 };

    public static void GoalMassFor_Table() {
        for (int d = 1; d <= 5; d++) Eq(GoalTable[d - 1], RunState.GoalMassFor(d), "таблица цели, глубина " + d);
        Eq(GoalTable[0], RunState.GoalMassFor(0), "ниже 1 — как Г1");
        Eq(GoalTable[4], RunState.GoalMassFor(9), "выше 5 — как Г5");
    }

    public static void WorldGoal_ClampedToTable() {
        for (int d = 1; d <= 5; d++) {
            for (uint seed = 1; seed <= 3; seed++) {
                var w = WorldGenerator.Generate(d, seed * 7919UL + 13, null);
                double t = RunState.GoalMassFor(d);
                True(w.GoalMass >= t * 0.9 - 1e-9, "цель не ниже 0.9 таблицы (Г" + d + ", сид " + seed + ")");
                True(w.GoalMass <= t * 1.1 + 1e-9, "цель не выше 1.1 таблицы (Г" + d + ", сид " + seed + ")");
            }
        }
    }

    public static void DevStart_HeroMassInsideCorridor() {
        for (int d = 1; d <= 5; d++) {
            var rs = RunState.CreateDevGuardianRun(new SoulMeta(), 100UL + (ulong)d, d);
            True(rs.Mass >= CorridorLo[d - 1], "герой Г" + d + " не мельче коридора: " + rs.Mass.ToString("0.0"));
            True(rs.Mass <= CorridorHi[d - 1], "герой Г" + d + " не крупнее коридора: " + rs.Mass.ToString("0.0"));
            True(rs.GuardianAwake, "страж Г" + d + " в тестовом старте просыпается сразу");
        }
    }

    // Симптом с телефона: герой массой 300 (радиус 6.15) вплотную к Гранильщику (HP ~183, радиус 4.74) —
    // центры тел в 11 ед., старый WakeRadius 7 считал стража спящим. v0.14: дистанции от поверхностей.
    public static void GuardianScaling_BigHeroWakesUp() {
        var g = new GuardianState(GuardianCatalog.ForDepth(4), 183.0, 301UL);
        // тела почти соприкасаются: центры в 11 ед.
        var near = g.Think(new GuardianInput { SelfPos = Vec2.Zero, PlayerPos = new Vec2(11, 0), PlayerMass = 300.0, Dt = 0.0 });
        Eq(GuardianPhase.Phase1, g.Phase, "герой 300 вплотную — страж проснулся (было: дремлет)");
        True(near.Strike == false && near.Telegraph.Radius == 0.0 || g.Phase != GuardianPhase.Dormant,
            "бой активен");
        // герой отошёл — по поверхностям тел далеко: страж спит
        var far = g.Think(new GuardianInput { SelfPos = Vec2.Zero, PlayerPos = new Vec2(32, 0), PlayerMass = 300.0, Dt = 0.0 });
        Eq(GuardianPhase.Dormant, g.Phase, "герой 300 в 32 ед. от центра — по поверхностям далеко, страж спит");
        // и герой массой 139 (коридор Г4) рядом — тоже бой
        var g2 = new GuardianState(GuardianCatalog.ForDepth(4), 183.0, 303UL);
        g2.Think(new GuardianInput { SelfPos = Vec2.Zero, PlayerPos = new Vec2(9, 0), PlayerMass = 139.0, Dt = 0.0 });
        Eq(GuardianPhase.Phase1, g2.Phase, "герой из коридора Г4 рядом — бой");
    }

    public static void GuardianRadii_ScaleWithBody() {
        for (int d = 1; d <= 5; d++) {
            double hp = Math.Max(8.0, RunState.GoalMassFor(d) * GuardianCatalog.ForDepth(d).HpMult);
            var g = new GuardianState(GuardianCatalog.ForDepth(d), hp, 310UL + (ulong)d);
            double body = SizeRule.RadiusForMass(hp);
            Eq(body, g.BodyRadius, "Г" + d + ": тело = 0.35×√HP");
            True(Math.Abs(g.ArenaRadius - (body * 4.5 + 2.0)) < 1e-9, "Г" + d + ": арена = BodyR×4.5+2");
            True(g.WakeRadius < g.ArenaRadius, "Г" + d + ": сон внутри арены");
            if (d == 3) True(g.PulseRadius > 1.5 * body, "Г3: импульс заметно больше тела (было r=4 < тела)");
            if (d == 4) {
                True(g.ShardOrbitRadius > 2.0 * body, "Г4: осколки снаружи тела (было +1.2)");
                True(g.BeamWidth > 0.4, "Г4: луч шире при большом теле");
            }
            // миньоны есть не у всех стражей (Г2/Г3 без выводка)
            if (GuardianCatalog.ForDepth(d).MinionsPerWave > 0) {
                True(g.MinionMass > 0.5, "Г" + d + ": миньоны не нулевые");
                var small = new GuardianState(GuardianCatalog.ForDepth(d), hp * 0.25, 320UL);
                True(small.MinionMass < g.MinionMass, "Г" + d + ": меньше тело — мельче миньоны");
            }
        }
    }

    public static void GuardianHit_TtkStableAcrossMasses() {
        // v0.14: TTK стража не зависит от веса героя — при равных долях урон растёт с массой героя
        var def = GuardianCatalog.ForDepth(1);
        var small = new GuardianState(def, 21.5, 331UL);
        var big = new GuardianState(def, 21.5, 331UL);
        double lossSmall = small.ApplyHit(8.0, 5.0, null, 0);
        double lossBig = big.ApplyHit(8.0, 50.0, null, 0);
        True(lossBig > lossSmall, "крупный герой сносит стража быстрее: " +
            lossBig.ToString("0.##") + " > " + lossSmall.ToString("0.##"));
        // ударов до обнажения в порядке Docs/BALANCE_v14.md §3.4 (полная скорость)
        var g = new GuardianState(def, 21.5, 333UL);
        int hits = 0;
        while (!g.IsExposed && hits++ < 500) g.ApplyHit(8.0, 18.2, null, 0);
        True(hits >= 8 && hits <= 25, "страж Г1 от героя 18.2: " + hits + " ударов до обнажения (ожидание 10–20)");
    }
}
}
