using PM.Domain.Core;
namespace PM.Domain.Absorption {
/// <summary>
/// Процесс удержания (D-09/T09): шкала 0..1 растёт при выполнении условий и убывает при разрыве
/// (не мгновенный сброс). Активен один процесс (демо). Тратит энергию попыткой.
/// </summary>
public sealed class CaptureProcess {
    public sealed class Config {
        public double BaseRate = 0.34;          // 1/с при эталонных условиях
        public double DecayRate = 0.25;         // 1/с при разрыве условий
        public double EnergyPerSecond = 1.5;    // расход энергии попыткой
        public double CompleteThreshold = 0.999;
    }
    public int TargetId { get; private set; } = -1;
    public double Progress { get; private set; }
    public bool Active { get; private set; }
    public double EnergyDrainedThisTick { get; private set; }
    private readonly Config _cfg;

    public CaptureProcess(Config cfg = null) { _cfg = cfg ?? new Config(); }

    public bool Start(int targetId) {
        if (Active && TargetId == targetId) return true; // идемпотентно
        if (Active) return false;                        // один активный захват в демо
        TargetId = targetId;
        Progress = 0;
        Active = true;
        return true;
    }
    public void Stop() { Active = false; Progress = 0; TargetId = -1; }
    /// <summary>Восстановление прогресса из сохранения (между Start и Tick).</summary>
    public void RestoreProgress(double value) {
        Progress = MathUtil.Clamp01(value);
        Active = Active || TargetId >= 0;
    }

    /// <summary>
    /// Обновление. conditionsOk: добыча в радиусе, ослаблена, препятствий нет.
    /// strengthRatio: сила удержания / сопротивление цели (>1 — превосходство).
    /// targetHealth01: чем слабее добыча, тем быстрее захват.
    /// targetMassFactor: тяжёлая добыча захватывается медленнее (1 = эталон).
    /// availableEnergy: попытки тратят энергию; без энергии рост останавливается.
    /// </summary>
    /// <returns>true — захват завершён (добыча готова к интернализации).</returns>
    public bool Tick(bool conditionsOk, double strengthRatio, double targetHealth01,
                     double targetMassFactor, double dt, ref double availableEnergy) {
        EnergyDrainedThisTick = 0;
        if (!Active) return false;
        if (!conditionsOk) {
            Progress -= _cfg.DecayRate * 1.5 * dt;   // регресс, не мгновенный сброс
            if (Progress <= 0) { Stop(); return false; }
            return false;
        }
        double rate = _cfg.BaseRate *
                      MathUtil.Max(0.2, strengthRatio) *
                      MathUtil.Lerp(1.2, 0.6, MathUtil.Clamp01(targetHealth01)) /
                      MathUtil.Max(0.2, targetMassFactor);
        double energyWant = _cfg.EnergyPerSecond * dt;
        if (availableEnergy < energyWant) rate *= MathUtil.Max(0, availableEnergy / energyWant);
        availableEnergy = MathUtil.Max(0, availableEnergy - energyWant);
        EnergyDrainedThisTick = energyWant;
        Progress = MathUtil.Clamp01(Progress + rate * dt);
        if (Progress >= _cfg.CompleteThreshold) {
            Active = false;
            return true;
        }
        return false;
    }
}
}
