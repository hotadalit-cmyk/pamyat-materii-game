using PM.Domain.Core;
using PM.Domain.Body;
namespace PM.Domain.Movement {
/// <summary>
/// Планировщик рывка (T02/D-16). Кривая скорости линейна по заряду в прототипе;
/// импульс ослабевает при нехватке духовной энергии; цена воды = цена запуска,
/// масштабированная зарядом; при нехватке воды расход ограничен запасом.
/// </summary>
public static class JetPlanner {
    public static JetPlan Plan(BodyMassState body, OrganStats organs,
                               PullChargeResult gesture, double availableEnergy,
                               MovementConfig cfg) {
        if (!gesture.Valid) {
            return new JetPlan { Valid = false, FailReason = "gesture_invalid" };
        }
        if (cfg == null) cfg = new MovementConfig();
        // Кривая импульса: minSpeed + (max-min)*charge, множитель органов.
        double speed = MathUtil.Lerp(organs.MinSpeed, organs.MaxSpeed, gesture.Charge01);
        speed *= MathUtil.Max(0.05, organs.ImpulsePower);
        // Дефицит энергии ослабляет рывок, не блокирует (доступно минимум MinEnergyFactor).
        double energyFactor = MathUtil.Clamp(
            availableEnergy / MathUtil.Max(0.001, cfg.FullJetEnergy),
            cfg.MinEnergyFactor, 1.0);
        speed *= energyFactor;
        // Экономика.
        double waterRequested = cfg.LaunchWaterBase * (0.5 + 0.5 * gesture.Charge01);
        double waterSpent = body.SpendWater(waterRequested);
        bool risk = waterSpent < waterRequested - 1e-9;
        double energySpent = cfg.FullJetEnergy * gesture.Charge01 * energyFactor;
        return new JetPlan {
            Valid = true,
            Impulse = gesture.Direction * speed,
            WaterRequested = waterRequested,
            WaterSpent = waterSpent,
            EnergySpent = energySpent,
            DehydrationRisk = risk
        };
    }
}
}
