using PM.Domain.Core;
namespace PM.Domain.Body {
/// <summary>Целостность тела (0..1). Структура не исчезает автоматически — урон только явный (P-03).</summary>
public sealed class BodyVitals {
    public double Integrity = 1.0;
    public bool IsAlive => Integrity > 0;
    public const double DehydrationDamagePerSecond = 0.02;

    public double ApplyDamage(double amount) {
        if (amount <= 0) return Integrity;
        Integrity = MathUtil.Max(0, Integrity - amount);
        return Integrity;
    }
    public double Heal(double amount) {
        Integrity = MathUtil.Clamp01(Integrity + MathUtil.Max(0, amount));
        return Integrity;
    }
}
}
