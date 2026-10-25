using PM.Domain.Core;
using PM.Domain.Absorption;
using PM.Domain.World;
using PM.Persistence;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.3.0: химическое чутьё (T14), помощь игрока ИИ (T16), статистика жизни, демо-цикл.</summary>
public static class SocialSenseTests {
    // ---------- Химическое чутьё ----------
    public static void ChemSense_InactiveWithoutNode() {
        var sim = new GameSim(SimMode.Headless);
        sim.AddPellet(new Vec2(3, 0), 0.3);
        double d; int id;
        var dir = sim.ChemSenseDirection(out d, out id);
        True(dir.Approx(Vec2.Zero, 1e-9), "no direction without sense node");
        Eq(0.0, d, "dist is zero");
        Eq(0, id, "no pellet id");
    }
    public static void ChemSense_PointsToNearestPelletInRadius() {
        var sim = new GameSim(SimMode.Headless);
        sim.Unlocked.Add("n_chem_sense");
        sim.AddPellet(new Vec2(4, 0), 0.3);
        sim.AddPellet(new Vec2(2, 0), 0.3);
        double d; int id;
        var dir = sim.ChemSenseDirection(out d, out id);
        True(!dir.Approx(Vec2.Zero, 1e-9), "direction found");
        True(dir.X > 0.99, "points +X toward nearest");
        Eq(2.0, d, "nearest pellet distance");
        Eq(sim.Food[1].Id, id, "nearest pellet id");
    }
    public static void ChemSense_IgnoresFarAndConsumed() {
        var sim = new GameSim(SimMode.Headless);
        sim.Unlocked.Add("n_chem_sense");
        sim.AddPellet(new Vec2(9, 0), 0.3);   // за радиусом чутья
        var near = sim.AddPellet(new Vec2(3, 0), 0.3);
        near.Consumed = true;                  // поглощённое не чувствуется
        double d; int id;
        var dir = sim.ChemSenseDirection(out d, out id);
        True(dir.Approx(Vec2.Zero, 1e-9), "far and consumed are ignored");
        Eq(0, id, "no pellet id");
    }

    // ---------- Помощь игрока ИИ-носителю души ----------
    public static void PlayerAssist_TransfersWaterAndBuildsTrust() {
        var sim = new GameSim(SimMode.Headless);
        var ally = sim.AddAiActor(true, new Vec2(2, 0));
        double playerBefore = sim.Player.Body.Water;
        double allyBefore = ally.Body.Water;
        string err = sim.PlayerAssistAi(ally.Id);
        Eq(null, err, "assist succeeds");
        True(sim.Player.Body.Water < playerBefore, "player spent water");
        True(ally.Body.Water > allyBefore, "ally gained water");
        Eq(20.0, sim.ProfileOf(ally.Id).Memory.Trust, "trust grew by HelpedBy");
        Eq(1, sim.LifeAssists, "life assist counted");
        bool found = false;
        for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Kind == "player_assisted") found = true;
        True(found, "event recorded");
    }
    public static void PlayerAssist_TooFarRejected() {
        var sim = new GameSim(SimMode.Headless);
        var ally = sim.AddAiActor(true, new Vec2(8, 8)); // дальше AssistRadius
        string err = sim.PlayerAssistAi(ally.Id);
        Eq("too_far", err, "rejected: too far");
        Eq(0, sim.LifeAssists, "nothing counted");
    }
    public static void PlayerAssist_NotEnoughWaterRejected() {
        var sim = new GameSim(SimMode.Headless);
        var ally = sim.AddAiActor(true, new Vec2(1, 0));
        sim.Player.Body.Water = sim.Player.Body.MinimumViableWater + 0.1;
        string err = sim.PlayerAssistAi(ally.Id);
        Eq("not_enough_water", err, "rejected: water reserve too low");
        True(System.Math.Abs(ally.Body.Water - 4.0) < 1e-9, "ally got nothing");
    }

    // ---------- Статистика жизни и сохранение ----------
    public static void LifeStats_CountedAndReset() {
        var sim = new GameSim(SimMode.Headless);
        var p = sim.AddPellet(new Vec2(0.2, 0), 0.3);
        True(sim.TryConsumePellet(p.Id), "pellet consumed");
        Eq(1, sim.LifePellets, "pellet counted");
        sim.ResetLifeStats();
        Eq(0, sim.LifePellets, "stats reset");
    }
    public static void Save_RoundtripKeepsLifeStats() {
        var sim = new GameSim(SimMode.Headless);
        var p = sim.AddPellet(new Vec2(0.2, 0), 0.3);
        sim.TryConsumePellet(p.Id);
        sim.LifeNodes = 2;
        sim.LifeAssists = 1;
        var d = GameSimSave.Export(sim);
        var sim2 = new GameSim(SimMode.Headless);
        GameSimSave.Import(sim2, d);
        Eq(1, sim2.LifePellets, "pellets restored");
        Eq(2, sim2.LifeNodes, "nodes restored");
        Eq(1, sim2.LifeAssists, "assists restored");
    }

    // ---------- Полный демо-цикл (DEMO_SCOPE) ----------
    public static void DemoCycle_CompletesOnBeetlePlusFormNode() {
        var sim = new GameSim(SimMode.Headless);
        sim.Unlocked.Add("n_beetle_form");
        sim.Soul.CurrentEnergy = 1000;
        var b = sim.AddBeetle(new Vec2(1, 0));
        b.Phase = PreyPhase.Weakened;
        b.Health01 = 0.2;
        sim.Player.Pos = new Vec2(1.5, 0);
        sim.CaptureRequested = true;
        for (int i = 0; i < 300 && sim.LifeBeetles == 0; i++) sim.Step(0.2);
        Eq(1, sim.LifeBeetles, "beetle internalized");
        bool found = false;
        for (int i = 0; i < sim.Events.Count; i++)
            if (sim.Events[i].Kind == "demo_cycle_complete") found = true;
        True(found, "demo cycle recorded once beetle + form node");
    }
    public static void DemoCycle_NotRecordedWithoutFormNode() {
        var sim = new GameSim(SimMode.Headless);
        sim.Soul.CurrentEnergy = 1000;
        var b = sim.AddBeetle(new Vec2(1, 0));
        b.Phase = PreyPhase.Weakened;
        b.Health01 = 0.2;
        sim.Player.Pos = new Vec2(1.5, 0);
        sim.CaptureRequested = true;
        for (int i = 0; i < 300 && sim.LifeBeetles == 0; i++) sim.Step(0.2);
        Eq(1, sim.LifeBeetles, "beetle internalized");
        bool found = false;
        for (int i = 0; i < sim.Events.Count; i++)
            if (sim.Events[i].Kind == "demo_cycle_complete") found = true;
        True(!found, "no demo cycle without n_beetle_form");
    }
}
}
