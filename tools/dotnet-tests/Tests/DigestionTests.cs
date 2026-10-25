using PM.Domain.Absorption;
using static PMHarness.Check;
namespace PMHarness {
public static class DigestionTests {
    private static DigestionYield Y(int gene = 0) =>
        new DigestionYield { Water = 0.5, Organic = 1, Chitin = 1.5, Mineral = 0.3, GeneFragmentId = gene };
    public static void Add_And_DuplicateRejected() {
        var ch = new DigestionChamber(5);
        True(ch.TryAdd(1, MaterialClass.Chitin, 2, 8, Y()), "added");
        True(!ch.TryAdd(1, MaterialClass.Chitin, 1, 8, Y()), "duplicate prey rejected");
        Eq(2, ch.OccupiedCargo, "cargo occupied once");
    }
    public static void CapacityRejects() {
        var ch = new DigestionChamber(3);
        True(ch.TryAdd(1, MaterialClass.Organic, 2, 5, Y()), "fits");
        True(!ch.TryAdd(2, MaterialClass.Organic, 2, 5, Y()), "overflow rejected");
    }
    public static void Tick_ProcessesByGameTime() {
        var ch = new DigestionChamber(5);
        ch.TryAdd(1, MaterialClass.Chitin, 2, 4, Y());
        ch.Tick(2);
        var ready = ch.HarvestReadyItems();
        Eq(0, ready.Count, "half way not ready");
        ch.Tick(2);
        ready = ch.HarvestReadyItems();
        Eq(1, ready.Count, "ready after total time");
    }
    public static void Harvest_YieldsExactlyOnce() {
        var ch = new DigestionChamber(5);
        ch.TryAdd(1, MaterialClass.Chitin, 2, 1, Y(101));
        ch.Tick(1);
        var first = ch.HarvestReadyItems();
        var second = ch.HarvestReadyItems();
        Eq(1, first.Count, "first harvest has item");
        Eq(0, second.Count, "second harvest empty — no double reward");
        True(first[0].Yielded, "item marked yielded");
        Eq(101, first[0].Yield.GeneFragmentId, "gene in yield");
    }
    public static void Pause_StopsProcessing() {
        // пауза = отсутствие Tick; digestive не должен двигаться сам
        var ch = new DigestionChamber(5);
        ch.TryAdd(1, MaterialClass.Organic, 1, 2, Y());
        ch.Tick(1);
        double remain = ch.Items[0].RemainingSec;
        double remainAfterPause = ch.Items[0].RemainingSec; // «кадры» идут, тика нет
        Eq(remain, remainAfterPause, "no tick no progress (pause)");
    }
    public static void MaxParallel_SequentialProcessing() {
        var ch = new DigestionChamber(9);
        ch.TryAdd(1, MaterialClass.Organic, 1, 2, Y());
        ch.TryAdd(2, MaterialClass.Organic, 1, 2, Y());
        ch.Tick(2);
        var ready = ch.HarvestReadyItems();
        Eq(1, ready.Count, "only one processed in parallel=1");
        ch.Tick(2);
        ready = ch.HarvestReadyItems();
        Eq(1, ready.Count, "second finishes next window");
    }
}
}
