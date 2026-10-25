using PM.Domain.Core;
namespace PM.Domain.Movement {
/// <summary>Результат расчёта натяжения жеста (MATTER_PHYSICS «Траектория рывка»).</summary>
public struct PullChargeResult {
    public Vec2 Direction;   // aimVector = origin - pointer (тянем ПРОТИВ движения)
    public double Charge01;  // clamp(length / dragMax, 0, 1)
    public bool Valid;       // false: жест короче минимального порога
    public string FailReason;
}

/// <summary>Чистый расчёт прицеливания. Минимальный порог отсекает случайные касания (QA-1).</summary>
public static class PullCharge {
    public static PullChargeResult Compute(Vec2 origin, Vec2 pointer, double dragMax, double minDrag) {
        var aim = origin - pointer;
        double len = aim.Length;
        if (len < minDrag) {
            return new PullChargeResult { Valid = false, FailReason = "below_min_drag" };
        }
        var dir = len < 1e-9 ? Vec2.Zero : aim / len;
        return new PullChargeResult {
            Valid = true,
            Direction = dir,
            Charge01 = MathUtil.Clamp01(len / MathUtil.Max(0.01, dragMax))
        };
    }
}
}
