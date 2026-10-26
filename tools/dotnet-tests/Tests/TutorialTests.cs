using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.10: обучение первой жизни — порядок шагов и события.</summary>
public static class TutorialTests {
    public static void FullPath_InOrder() {
        var t = new TutorialFlow();
        Eq((int)TutorialStep.Move, t.Index, "starts with move");
        t.OnAbsorb(true);                       // события не своего шага игнорируются
        Eq((int)TutorialStep.Move, t.Index, "still move");
        t.OnMoved(2); t.OnMoved(2.5);
        Eq((int)TutorialStep.EatSpores, t.Index, "moved enough");
        for (int i = 0; i < TutorialFlow.SporesToEat; i++) t.OnAbsorb(true);
        Eq((int)TutorialStep.Rings, t.Index, "spores eaten");
        t.OnAbsorb(true);
        Eq((int)TutorialStep.Rings, t.Index, "a spore is not a creature");
        t.OnAbsorb(false);
        t.OnDash();
        Eq((int)TutorialStep.Evolve, t.Index, "dash done");
        t.OnEvolutionOpened(); t.OnSplit(); t.OnMerged();
        Eq((int)TutorialStep.Goal, t.Index, "evolve, split, gather");
        t.Tick(TutorialFlow.GoalShowSeconds + 0.1, true);
        True(!t.Active, "finished");
    }

    public static void Evolve_WaitsForToken_OthersTimeOut() {
        var t = new TutorialFlow();
        t.Tick(TutorialFlow.SoftTimeout + 1, false);
        Eq((int)TutorialStep.EatSpores, t.Index, "stuck step gives way");
        t.Skip();
        True(!t.Active, "skip");
        var e = new TutorialFlow();
        e.OnMoved(10); for (int i = 0; i < 3; i++) e.OnAbsorb(true); e.OnAbsorb(false); e.OnDash();
        e.Tick(999, false);
        Eq((int)TutorialStep.Evolve, e.Index, "evolve waits for a token");
        e.Tick(TutorialFlow.SoftTimeout + 1, true);
        Eq((int)TutorialStep.Evolve, e.Index, "and never times out — it needs the button");
    }

    public static void DoneFlag_AndTexts() {
        True(!new TutorialFlow(true).Active, "already done");
        for (int i = 0; i < TutorialFlow.Count; i++) {
            True(TutorialFlow.Text((TutorialStep)i, true).Length > 10, "text " + i);
            True(TutorialFlow.Text((TutorialStep)i, false).Length > 10, "text pull " + i);
        }
    }
}
}
