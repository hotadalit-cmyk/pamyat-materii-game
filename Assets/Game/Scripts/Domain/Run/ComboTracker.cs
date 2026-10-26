namespace PM.Domain.Run {

/// <summary>
/// Цепочка поглощений между рывками: каждое поглощение не-споры наращивает множитель
/// получаемой массы (1 + 0.25×(Chain−1), максимум 3). Рывок или 2.5 с без поглощений
/// сбрасывают цепочку. Без UnityEngine, без LINQ.
/// </summary>
public sealed class ComboTracker {
    private const double ComboWindowSec = 2.5;
    private const double MultPerChain = 0.25;
    private const double MaxMultiplier = 3.0;

    private int _chain;
    private int _best;
    private double _sinceAbsorb;

    /// <summary>Поглощений (не спор) с момента последнего рывка.</summary>
    public int Chain => _chain;

    /// <summary>1 + 0.25×(Chain−1), максимум 3.</summary>
    public double Multiplier {
        get {
            if (_chain <= 1) return 1.0;
            double m = 1.0 + MultPerChain * (_chain - 1);
            return m > MaxMultiplier ? MaxMultiplier : m;
        }
    }

    /// <summary>Самая длинная цепочка за всё время.</summary>
    public int Best => _best;

    /// <summary>Рывок сбросил цепочку (Best сохраняется).</summary>
    public void OnDash() {
        _chain = 0;
        _sinceAbsorb = 0.0;
    }

    /// <summary>
    /// Поглощение: не-споры наращивают цепочку, возврат — БОНУС массы mass×(Multiplier−1);
    /// споры бонуса не дают и цепочку не растят.
    /// </summary>
    public double OnAbsorb(bool isSpore, double mass) {
        if (isSpore || mass <= 0.0) return 0.0;
        _chain++;
        _sinceAbsorb = 0.0;
        if (_chain > _best) _best = _chain;
        double bonus = mass * (Multiplier - 1.0);
        return bonus > 0.0 ? bonus : 0.0;
    }

    /// <summary>2.5 с без поглощений — цепочка гаснет.</summary>
    public void Tick(double dt) {
        if (dt <= 0.0 || _chain <= 0) return;
        _sinceAbsorb += dt;
        if (_sinceAbsorb >= ComboWindowSec) {
            _chain = 0;
            _sinceAbsorb = 0.0;
        }
    }
}
}
