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

    /// <summary>Пороги цепочки, на которых срабатывает «эпический момент» — мгновенная награда.</summary>
    public const int MilestoneChain3 = 3;
    public const int MilestoneChain5 = 5;
    public const int MilestoneChain8 = 8;

    private int _chain;
    private int _best;
    private double _sinceAbsorb;
    // v0.10: пороги мести — каждый срабатывает один раз за текущую цепочку.
    // Рывок или таймаут сбрасывают _lastFired, так что после нового рывка пороги
    // могут сработать снова (поощрение агрессивной игры с риском).
    private int _lastFired;
    private int _pendingMilestone;

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
        _lastFired = 0;
        _pendingMilestone = 0;
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
        // v0.10: пороги 3/5/8 — каждый срабатывает один раз за цепочку.
        if (_pendingMilestone == 0) {
            if (_chain == MilestoneChain8 && _lastFired < MilestoneChain8) {
                _pendingMilestone = MilestoneChain8; _lastFired = MilestoneChain8;
            } else if (_chain == MilestoneChain5 && _lastFired < MilestoneChain5) {
                _pendingMilestone = MilestoneChain5; _lastFired = MilestoneChain5;
            } else if (_chain == MilestoneChain3 && _lastFired < MilestoneChain3) {
                _pendingMilestone = MilestoneChain3; _lastFired = MilestoneChain3;
            }
        }
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
            _lastFired = 0;
            _pendingMilestone = 0;
        }
    }

    /// <summary>
    /// Забрать сработавший порог (3/5/8) — RunState применяет награду. 0, если порога нет.
    /// Несколько порогов в одном поглощении невозможны: цепочка растёт по +1.
    /// </summary>
    public int PopPendingMilestone() {
        int m = _pendingMilestone;
        _pendingMilestone = 0;
        return m;
    }
}
}
