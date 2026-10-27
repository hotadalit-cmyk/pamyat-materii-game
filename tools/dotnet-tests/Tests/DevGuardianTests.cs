using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.13: тестовый старт у стража глубины (RunState.CreateDevGuardianRun, DevTools).</summary>
public static class DevGuardianTests {

    public static void Factory_StartsAtDepthWithAwakeGuardian() {
        var rs = RunState.CreateDevGuardianRun(new SoulMeta(), 777UL, 3);
        True(rs.OpenWorld, "open world");
        Eq(3, rs.Depth, "depth 3");
        True(rs.GuardianAwake, "guardian awake immediately");
        True(rs.Guardian != null, "guardian exists");
        True(rs.RoomAbsorbed >= rs.Room.GoalMass, "depth goal credited");
        Eq(rs.Room.GoalMass * 1.1, rs.Mass, "hero mass = goal x1.1");
        Eq(2, rs.Evolution.FreeTokens, "2 evolution tokens");
        for (int i = 0; i < ResourceInfo.Count; i++)
            Eq(20.0, rs.Resources.Get((ResourceKind)i), "20 of resource " + i);
        double dx = rs.Room.PlayerStart.X - rs.Room.ExitPos.X;
        double dy = rs.Room.PlayerStart.Y - rs.Room.ExitPos.Y;
        double d = System.Math.Sqrt(dx * dx + dy * dy);
        True(d > 5.2 && d < 6.8, "hero ~6 from shaft (got " + d.ToString("0.##") + ")");
        True(!rs.ExitOpen, "exit still locked until guardian absorbed");
        True(!rs.IsDead && !rs.IsVictory, "run alive");
    }

    public static void Factory_ClampsDepth_AndIsDeterministic() {
        var high = RunState.CreateDevGuardianRun(new SoulMeta(), 42UL, 99);
        Eq(RunState.MaxDepth, high.Depth, "clamped to max depth");
        var low = RunState.CreateDevGuardianRun(new SoulMeta(), 42UL, 0);
        Eq(1, low.Depth, "floored at 1");
        var a = RunState.CreateDevGuardianRun(new SoulMeta(), 1234UL, 2);
        var b = RunState.CreateDevGuardianRun(new SoulMeta(), 1234UL, 2);
        Eq(a.Room.GoalMass, b.Room.GoalMass, "same goal mass");
        Eq(a.Room.ExitPos.X, b.Room.ExitPos.X, "same exit x");
        Eq(a.Room.ExitPos.Y, b.Room.ExitPos.Y, "same exit y");
        Eq(a.Room.PlayerStart.X, b.Room.PlayerStart.X, "same start x");
        Eq(a.Room.PlayerStart.Y, b.Room.PlayerStart.Y, "same start y");
        Eq(a.GuardianMaxHp, b.GuardianMaxHp, "same guardian hp");
        // каждый страж — свой (каталог по глубине)
        True(RunState.CreateDevGuardianRun(new SoulMeta(), 5UL, 1).Guardian.Def.Kind == GuardianKind.SporeMother,
            "depth 1 is Спороносица");
        True(RunState.CreateDevGuardianRun(new SoulMeta(), 5UL, 2).Guardian.Def.Kind == GuardianKind.Forge,
            "depth 2 is Горнило");
        True(RunState.CreateDevGuardianRun(new SoulMeta(), 5UL, 3).Guardian.Def.Kind == GuardianKind.AbyssChoir,
            "depth 3 is Хор Бездны");
        True(RunState.CreateDevGuardianRun(new SoulMeta(), 5UL, 4).Guardian.Def.Kind == GuardianKind.Grinder,
            "depth 4 is Гранильщик");
        True(RunState.CreateDevGuardianRun(new SoulMeta(), 5UL, 5).Guardian.Def.Kind == GuardianKind.Pulsar,
            "depth 5 is Пульсар");
    }

    public static void Factory_GuardianFightActuallyWorks() {
        var rs = RunState.CreateDevGuardianRun(new SoulMeta(), 31415UL, 4);
        var g = rs.Guardian;
        // страж жив, HP по формуле max(8, GoalMass × HpMult)
        Eq(System.Math.Max(8.0, rs.Room.GoalMass * g.Def.HpMult), rs.GuardianFormulaHp, "formula hp");
        Eq(rs.GuardianFormulaHp, rs.GuardianMaxHp, "no pity yet");
        // удар по стражу снимает HP — бой идёт
        double hp0 = g.Hp;
        double loss = g.ApplyHit(9.0, rs.Mass, rs.Mutations, 0);
        True(loss > 0.0, "hit deals damage");
        True(g.Hp < hp0, "hp dropped");
        True(rs.GuardianDefeats(4) == 0, "no defeats yet");
    }
}
}
