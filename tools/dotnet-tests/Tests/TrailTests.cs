using PM.Domain.Core;
using PM.Domain.Trail;
using static PMHarness.Check;
namespace PMHarness {
public static class TrailTests {
    public static void Feed_EmitsByDistanceNotByCalls() {
        var t = new TrailLedger();
        t.SetTime(0);
        t.Feed(new Vec2(0, 0), 0.01);
        t.Feed(new Vec2(0.1, 0), 0.01); // всё ещё в одном сегменте
        Eq(0, t.Records.Count, "segment not complete");
        t.Feed(new Vec2(0.6, 0), 0.01); // расстояние >= 0.5
        Eq(1, t.Records.Count, "one segment emitted");
    }
    public static void Feed_ZeroPathNoEmission() {
        var t = new TrailLedger();
        t.Feed(new Vec2(0, 0), 0.01);
        t.Feed(new Vec2(0, 0), 0.01); // стоим на месте — пути нет
        Eq(0, t.Records.Count, "no movement no segment");
    }
    public static void Evaporate_RemovesOldRecords() {
        var t = new TrailLedger();
        t.Feed(new Vec2(0, 0), 0.01);
        t.Feed(new Vec2(1, 0), 0.01);
        t.Evaporate(1000); // очень долго
        Eq(0, t.Records.Count, "all evaporated");
    }
    public static void Reabsorb_ConsumesOnce_NoDuplication() {
        var t = new TrailLedger();
        t.Feed(new Vec2(0, 0), 0.01);
        t.Feed(new Vec2(0.4, 0), 0.01);
        t.Feed(new Vec2(0.8, 0), 0.01); // 2 сегмента по 0.02-0.03
        double totalInLedger = 0;
        for (int i = 0; i < t.Records.Count; i++) totalInLedger += t.Records[i].Mass;
        double first = t.Reabsorb(new Vec2(0, 0), 1.0, 10);
        double second = t.Reabsorb(new Vec2(0, 0), 1.0, 10);
        True(first > 0, "first reabsorb returns water");
        Eq(0.0, second, "second reabsorb returns nothing — no duplication");
        Eq(0, t.Records.Count, "records consumed");
        True(first <= totalInLedger + 1e-9, "cannot take more than stored");
    }
    public static void Reabsorb_RespectsCapacityAndRadius() {
        var t = new TrailLedger();
        t.Feed(new Vec2(0, 0), 0.01);
        t.Feed(new Vec2(0.5, 0), 0.01);
        t.Feed(new Vec2(5, 0), 0.01); // вне радиуса
        double taken = t.Reabsorb(new Vec2(0, 0), 1.0, 0.01); // маленькая ёмкость
        True(taken > 0 && taken <= 0.01 + 1e-9, "capacity respected, some taken");
        True(t.Records.Count == 2, "partially consumed record kept (no premature delete)");
        double rest = t.Reabsorb(new Vec2(0, 0), 1.0, 10);
        True(rest > 0, "rest consumed with bigger capacity");
        True(t.Records.Count == 1, "near record emptied, far record untouched");
    }
    public static void MaxRecords_Consolidation() {
        var t = new TrailLedger(maxRecords: 4);
        for (int i = 0; i < 20; i++) t.Feed(new Vec2(i * 0.6, 0), 0.01);
        True(t.Records.Count <= 4, "record cap enforced");
    }
}
}
