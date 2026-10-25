using PM.Domain.Core;
namespace PM.Domain.Fields {
/// <summary>
/// Биогравитация (D-07/T07): притягивает ТОЛЬКО мелкую неживую пищу.
/// Сила растёт с массой источника, спадает с расстоянием, обрезана радиусом и потолком силы.
/// Стены блокируют — проверка прямой видимости на стороне физического адаптера.
/// </summary>
public static class BiogravityPull {
    public sealed class Config {
        public double MaxRange = 6.0;
        public double MaxForce = 6.0;
        public double MinDistance = 0.35; // мёртвая зона — сингулярность недопустима
        public double Softening = 0.25;   // сглаживание знаменателя
        public double Gain = 0.9;
        public double MinTargetMass = 0.01; // «мелкая» еда: потолок массы объекта
        public double MaxTargetMass = 0.6;
    }
    /// <summary>Возвращает вектор УСКОРЕНИЯ цели к источнику (0 — вне условий).</summary>
    public static Vec2 ComputeAcceleration(double sourceMass, double targetMass,
                                           Vec2 targetToSourceDir, double distance,
                                           Config cfg) {
        if (cfg == null) cfg = new Config();
        if (distance > cfg.MaxRange) return Vec2.Zero;
        if (distance < cfg.MinDistance) return Vec2.Zero;
        if (targetMass <= 0 || targetMass < cfg.MinTargetMass || targetMass > cfg.MaxTargetMass)
            return Vec2.Zero; // живую/крупную добычу магнитом не тянем (T07)
        var dir = targetToSourceDir.Normalized();
        if (dir.Approx(Vec2.Zero)) return Vec2.Zero;
        double force = cfg.Gain * sourceMass * targetMass /
                       (distance * distance + cfg.Softening);
        force = MathUtil.Min(force, cfg.MaxForce);
        return dir * (force / targetMass); // ускорение
    }
}
}
