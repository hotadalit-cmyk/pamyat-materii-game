using System;
namespace PM.Domain.Core {
/// <summary>Детерминированные числовые помощники. Округление в Domain не выполняется —
/// политика зафиксирована: все расчёты в double, форматирование только на стороне UI.</summary>
public static class MathUtil {
    public static double Clamp(double v, double min, double max) =>
        v < min ? min : (v > max ? max : v);
    public static double Clamp01(double v) => Clamp(v, 0, 1);
    public static double Lerp(double a, double b, double t) => a + (b - a) * Clamp01(t);
    public static bool Approx(double a, double b, double eps = 1e-5) =>
        Math.Abs(a - b) <= eps;
    public static bool ApproxZero(double v, double eps = 1e-9) => Math.Abs(v) <= eps;
    /// <summary>Движение от текущего значения к целевому не дальше maxDelta (не проскакивает цель).</summary>
    public static double MoveTowards(double current, double target, double maxDelta) {
        if (Math.Abs(target - current) <= maxDelta) return target;
        return current + Math.Sign(target - current) * maxDelta;
    }
    public static double Min(double a, double b) => a < b ? a : b;
    public static double Max(double a, double b) => a > b ? a : b;
}
}
