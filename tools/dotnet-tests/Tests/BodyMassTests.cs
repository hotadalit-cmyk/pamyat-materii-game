using PM.Domain.Body;
using static PMHarness.Check;
namespace PMHarness {
public static class BodyMassTests {
    public static void TotalMass_IsSum() {
        var b = new BodyMassState(1, 5, 2, 0.5);
        Eq(8.5, b.Total, "total = structure+water+reserve+cargo");
    }
    public static void SpendWater_ClampsToAvailable() {
        var b = new BodyMassState(1, 0.3);
        double spent = b.SpendWater(0.5);
        Eq(0.3, spent, "spent limited by available water");
        Eq(0.0, b.Water, "water is zero after spend");
        // T05-проверка из бэклога: расход 0.1 воды уменьшает total ровно на 0.1
        var b2 = new BodyMassState(1, 2);
        double before = b2.Total;
        b2.SpendWater(0.1);
        Eq(before - 0.1, b2.Total, "total decreases exactly by spent water");
    }
    public static void Mass_NeverNegative() {
        var b = new BodyMassState(1, 1);
        b.SpendWater(100);
        b.SpendReserve(100);
        b.RemoveCargo(100);
        True(b.Water >= 0 && b.Reserve >= 0 && b.Cargo >= 0 && b.Structure >= 0, "no negative mass");
        b.AddWater(-50); // отрицательные добавки игнорируются
        Eq(0.0, b.Water, "negative add ignored");
    }
    public static void Conversions_ConserveMassWithEfficiency() {
        var b = new BodyMassState(1, 0.5, 2);
        double totalBefore = b.Total;
        True(b.TryConvertReserveToWater(1, 0.8), "convert reserve->water");
        Eq(totalBefore - 0.2, b.Total, "efficiency 0.8 loses 20 percent");
        Eq(1.3, b.Water, "water gained");
        Eq(1.0, b.Reserve, "reserve spent");
    }
    public static void DehydrationFlag() {
        var b = new BodyMassState(1, 0.5);
        b.MinimumViableWater = 1.0;
        True(b.IsDehydrated, "dehydrated below minimum");
        b.AddWater(0.6);
        True(!b.IsDehydrated, "not dehydrated above minimum");
    }
    public static void CargoRemove_OnlyWhatExists() {
        var b = new BodyMassState(1, 1, 0, 0.4);
        double removed = b.RemoveCargo(1.0);
        Eq(0.4, removed, "remove limited by cargo");
        Eq(0.0, b.Cargo, "cargo empty");
    }
    public static void Vitals_DamageClampsAtZero() {
        var v = new BodyVitals();
        v.ApplyDamage(0.3);
        Eq(0.7, v.Integrity, "damage applied");
        v.ApplyDamage(5);
        Eq(0.0, v.Integrity, "integrity floors at zero");
        True(!v.IsAlive, "dead at zero integrity");
        v.Heal(0.2);
        True(v.IsAlive, "alive after heal");
        v.Heal(10);
        Eq(1.0, v.Integrity, "integrity caps at one");
    }
}
}
