using PM.Domain.Absorption;
using static PMHarness.Check;
namespace PMHarness {
public static class CaptureTests {
    public static void Progress_GrowsWhenConditionsOk() {
        var c = new CaptureProcess();
        True(c.Start(7), "started");
        double energy = 100;
        bool done = c.Tick(true, strengthRatio: 2.0, targetHealth01: 0.2, targetMassFactor: 1.0,
                           dt: 0.5, ref energy);
        True(c.Progress > 0, "progress grows");
        True(!done, "not done yet");
    }
    public static void Progress_RegressesOnConditionBreak_NotInstantReset() {
        var c = new CaptureProcess();
        c.Start(7);
        double energy = 100;
        c.Tick(true, 2, 0.2, 1, 1.0, ref energy);
        double p1 = c.Progress;
        c.Tick(false, 0, 0.2, 1, 0.2, ref energy); // добыча вырвалась на миг
        True(c.Progress < p1, "progress decreases when broken");
        True(c.Progress > 0, "regression is gradual, not instant reset");
    }
    public static void Escape_ResetsWhenFullyRegressed() {
        var c = new CaptureProcess();
        c.Start(7);
        double energy = 100;
        c.Tick(true, 2, 0.2, 1, 0.3, ref energy);
        for (int i = 0; i < 100 && c.Active; i++) c.Tick(false, 0, 0.2, 1, 1.0, ref energy);
        True(!c.Active, "escaped: process stopped");
        Eq(0.0, c.Progress, "progress cleared");
    }
    public static void Completion_ReturnsTrueOnce() {
        var c = new CaptureProcess();
        c.Start(7);
        double energy = 1000;
        bool done = false;
        for (int i = 0; i < 200 && !done; i++) done = c.Tick(true, 3, 0.1, 0.5, 0.1, ref energy);
        True(done, "completed");
        Eq(1.0, c.Progress, "progress at 1");
        True(!c.Active, "process finished");
    }
    public static void EnergyDrainsOnAttempt() {
        var c = new CaptureProcess();
        c.Start(7);
        double energy = 10;
        c.Tick(true, 2, 0.2, 1, 1.0, ref energy);
        True(energy < 10, "energy spent");
        double none = 0;
        double before = none;
        c.Tick(true, 2, 0.2, 1, 1.0, ref none);
        True(c.Progress >= 0, "no energy: survives");
        True(none >= before, "energy never negative");
    }
    public static void SingleActiveProcess() {
        var c = new CaptureProcess();
        True(c.Start(1), "first target");
        True(!c.Start(2), "second target rejected while active");
        True(c.Start(1), "same target idempotent");
    }
    public static void WeakPreyCapturedFaster() {
        var healthy = new CaptureProcess();
        var weak = new CaptureProcess();
        healthy.Start(1); weak.Start(2);
        double e1 = 1000, e2 = 1000;
        for (int i = 0; i < 10; i++) healthy.Tick(true, 2, 0.9, 1, 0.1, ref e1);
        for (int i = 0; i < 10; i++) weak.Tick(true, 2, 0.1, 1, 0.1, ref e2);
        True(weak.Progress > healthy.Progress, "weakened prey captured faster");
    }
}
}
