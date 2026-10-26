using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunObstacleTests {

    public static void IsSolid_Kinds() {
        True(ObstacleRules.IsSolid(ObstacleKind.Pillar), "колонна твёрдая");
        True(ObstacleRules.IsSolid(ObstacleKind.Mushroom), "гриб твёрдый");
        True(ObstacleRules.IsSolid(ObstacleKind.Crystal), "кристалл твёрдый");
        True(ObstacleRules.IsSolid(ObstacleKind.Thorns), "шипы твёрдые");
        True(!ObstacleRules.IsSolid(ObstacleKind.Vent), "жерло — зона");
        True(!ObstacleRules.IsSolid(ObstacleKind.Pool), "лужа — зона");
        True(!ObstacleRules.IsSolid(ObstacleKind.Sticky), "липкость — зона");
    }

    public static void Bounciness_Values() {
        Eq(0.75, ObstacleRules.Bounciness(ObstacleKind.Pillar), "колонна: упругость 0.75");
        Eq(1.35, ObstacleRules.Bounciness(ObstacleKind.Mushroom), "гриб-бампер: 1.35 (разгоняет)");
        Eq(1.1, ObstacleRules.Bounciness(ObstacleKind.Crystal), "кристалл: 1.1");
        Eq(0.5, ObstacleRules.Bounciness(ObstacleKind.Thorns), "шипы гасят отскок: 0.5");
        Eq(0.0, ObstacleRules.Bounciness(ObstacleKind.Vent), "жерло не твёрдое");
        Eq(0.0, ObstacleRules.Bounciness(ObstacleKind.Pool), "лужа не твёрдая");
        Eq(0.0, ObstacleRules.Bounciness(ObstacleKind.Sticky), "липкость не твёрдая");
    }

    public static void ShatterSpeed_Constant() {
        Eq(6.0, ObstacleRules.ShatterSpeed, "кристалл раскалывается от скорости 6");
    }

    public static void ThornLoss_FormulaAndHeatproof() {
        Eq(0.15, ObstacleRules.ThornLoss(0.0, null), "нулевая скорость: 0.15");
        Eq(0.31, ObstacleRules.ThornLoss(2.0, null), "скорость 2: 0.15 + 0.16");
        Eq(0.71, ObstacleRules.ThornLoss(7.0, null), "скорость 7: 0.15 + 0.56");
        var plain = new MutationSet();
        Eq(0.31, ObstacleRules.ThornLoss(2.0, plain), "пустой набор мутаций = без множителя");
        var proof = new MutationSet();
        proof.Add("m_heatproof");
        Eq(0.124, ObstacleRules.ThornLoss(2.0, proof), "жаростойкость ×0.4");
        Eq(0.0, ObstacleRules.ThornLoss(-3.0, null), "отрицательная скорость — нет потери");
    }

    public static void ZoneMassPerSec_PoolAndVent() {
        Eq(0.06, ObstacleRules.ZoneMassPerSec(ObstacleKind.Pool, 10.0, null), "лужа: +0.06/сек независимо от массы");
        Eq(0.06, ObstacleRules.ZoneMassPerSec(ObstacleKind.Pool, 0.5, null), "лужа и при малой массе +0.06");
        Eq(-0.2, ObstacleRules.ZoneMassPerSec(ObstacleKind.Vent, 10.0, null), "жерло: −0.02×10");
        var proof = new MutationSet();
        proof.Add("m_heatproof");
        Eq(-0.08, ObstacleRules.ZoneMassPerSec(ObstacleKind.Vent, 10.0, proof), "жерло с жаростойкостью ×0.4");
        Eq(0.0, ObstacleRules.ZoneMassPerSec(ObstacleKind.Sticky, 10.0, null), "липкость не меняет массу");
        Eq(0.0, ObstacleRules.ZoneMassPerSec(ObstacleKind.Thorns, 10.0, null), "шипы — не зона массы");
        Eq(0.0, ObstacleRules.ZoneMassPerSec(ObstacleKind.Pillar, 10.0, null), "колонна — не зона массы");
    }

    public static void ZoneDamping_Values() {
        Eq(2.5, ObstacleRules.ZoneDamping(ObstacleKind.Pool), "лужа: вязкость 2.5");
        Eq(6.0, ObstacleRules.ZoneDamping(ObstacleKind.Sticky), "липкость: торможение 6");
        Eq(0.0, ObstacleRules.ZoneDamping(ObstacleKind.Vent), "жерло не тормозит");
        Eq(0.0, ObstacleRules.ZoneDamping(ObstacleKind.Pillar), "колонна не тормозит");
        Eq(0.0, ObstacleRules.ZoneDamping(ObstacleKind.Thorns), "шипы не тормозят");
    }

    public static void VentPush_CenterToEdge() {
        Eq(14.0, ObstacleRules.VentPush(0.0, 2.0), "в центре — полный толчок 14");
        Eq(7.0, ObstacleRules.VentPush(1.0, 2.0), "на полпути — половина силы");
        Eq(3.5, ObstacleRules.VentPush(1.5, 2.0), "на 3/4 радиуса — 3.5");
        Eq(0.0, ObstacleRules.VentPush(2.0, 2.0), "на краю — 0");
        Eq(0.0, ObstacleRules.VentPush(3.0, 2.0), "за пределами — 0");
    }

    public static void VentPush_EdgeCases() {
        Eq(0.0, ObstacleRules.VentPush(0.5, 0.0), "нулевой радиус — никакого толчка");
        Eq(0.0, ObstacleRules.VentPush(-1.0, 2.0), "отрицательная дистанция не даёт силы");
        True(ObstacleRules.VentPush(0.1, 2.0) > 13.0, "почти в центре — почти 14");
    }
}
}
