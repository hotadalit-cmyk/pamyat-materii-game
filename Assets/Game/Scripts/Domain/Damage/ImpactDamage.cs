using PM.Domain.Core;
namespace PM.Domain.Damage {
/// <summary>
/// Контактный урон (MATTER_PHYSICS): impactEnergy = 0.5 * reducedMass * normalRelSpeed^2;
/// damage = clamp(K * E * hitZone * penetration / resistance, 0, cap).
/// reducedMass = m1*m2/(m1+m2) — корректно для пары любых масс, включая стену (бесконечная масса
/// передаётся как m2=0 → reduced=0? Нет: для статической стены используем эффективную массу стены).
/// </summary>
public static class ImpactDamage {
    public static double ReducedMass(double m1, double m2) {
        double sum = m1 + m2;
        if (sum <= 0) return 0;
        return m1 * m2 / sum;
    }
    public static double ComputeDamage(double m1, double m2, double relNormalSpeed,
                                       double hitZoneMult, double penetration,
                                       double resistance, double k, double cap) {
        double e = 0.5 * ReducedMass(m1, m2) * relNormalSpeed * relNormalSpeed;
        double dmg = k * e * hitZoneMult * penetration / MathUtil.Max(0.05, resistance);
        return MathUtil.Clamp(dmg, 0, cap);
    }
}
/// <summary>Событие удара, передаваемое из физического адаптера.</summary>
public struct ImpactEvent {
    public int AttackerId;   // кто движется (инициатор пары: меньший id — для дедупа)
    public int TargetId;
    public double RelativeNormalSpeed; // |v1 - v2| вдоль нормали контакта
    public double AttackerMass;
    public double TargetMass;
    public double HitZoneMultiplier;
    public double Penetration;   // атакующего
    public double TargetResistance;
}
/// <summary>
/// Антидребезг (QA-3): одна пара — один удар за окно кулдауна; каскады за один кадр ограничены.
/// </summary>
public sealed class DamageDebouncer {
    private readonly double _cooldownSeconds;
    private readonly System.Collections.Generic.Dictionary<long, double> _lastHit =
        new System.Collections.Generic.Dictionary<long, double>();
    private readonly System.Collections.Generic.HashSet<long> _frameHits =
        new System.Collections.Generic.HashSet<long>();
    public DamageDebouncer(double cooldownSeconds = 0.35) { _cooldownSeconds = cooldownSeconds; }
    private static long Key(int a, int b) {
        // упорядоченная пара: маленький id — старшие биты
        int lo = a < b ? a : b, hi = a < b ? b : a;
        return ((long)lo << 32) | (uint)hi;
    }
    public void BeginFrame() => _frameHits.Clear();
    /// <summary>true, если удар по этой паре должен быть засчитан сейчас.</summary>
    public bool TryRegister(int a, int b, double now) {
        long k = Key(a, b);
        if (_frameHits.Contains(k)) return false;                    // один удар за кадр на пару
        if (_lastHit.TryGetValue(k, out double last) && now - last < _cooldownSeconds) return false;
        _lastHit[k] = now;
        _frameHits.Add(k);
        return true;
    }
}
}
