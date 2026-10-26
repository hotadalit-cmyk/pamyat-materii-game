using System.Collections.Generic;
namespace PM.Domain.Run {

/// <summary>
/// Набор мутаций тела игрока. Хранит стаки в порядке получения; ToArray/FromArray —
/// round-trip без потерь (id повторяются по числу стаков). Без LINQ.
/// </summary>
public sealed class MutationSet {
    private const double BaseBounciness = 0.8;
    private const double MinBounciness = 0.2;
    private const double BaseLightRadius = 3.5;
    private const double BaseAbsorbRatio = 1.25;
    private const double HungerAbsorbRatio = 1.10;

    private readonly List<string> _stacks = new List<string>(8);

    public int Stacks(string id) {
        if (id == null) return 0;
        int n = 0;
        for (int i = 0; i < _stacks.Count; i++)
            if (_stacks[i] == id) n++;
        return n;
    }

    public bool CanAdd(string id) {
        var def = MutationCatalog.Get(id);
        if (def == null) return false;
        return Stacks(id) < def.MaxStacks;
    }

    /// <summary>Добавить стак; игнорируется, если нельзя.</summary>
    public void Add(string id) {
        if (CanAdd(id)) _stacks.Add(id);
    }

    /// <summary>Шипы: урон, наносимый ударом ×1.5 за стак.</summary>
    public double DamageDealtMult => Pow(1.5, Stacks("m_spikes"));

    /// <summary>Панцирь: получаемый урон ×0.7 за стак.</summary>
    public double DamageTakenMult => Pow(0.7, Stacks("m_shell"));

    /// <summary>Липкость: 0.8 − 0.25×стаки, не ниже 0.2.</summary>
    public double Bounciness {
        get {
            double b = BaseBounciness - 0.25 * Stacks("m_sticky");
            return b < MinBounciness ? MinBounciness : b;
        }
    }

    /// <summary>1 + стаки «Второго рывка» (бонус души добавляется в RunState).</summary>
    public int DashCharges => 1 + Stacks("m_dash2");

    /// <summary>Магнит: радиус притяжения ×1.5 за стак.</summary>
    public double PullRadiusMult => Pow(1.5, Stacks("m_magnet"));

    /// <summary>Глаза: радиус света 3.5 + 2.0 за стак.</summary>
    public double LightRadius => BaseLightRadius + 2.0 * Stacks("m_eyes");

    /// <summary>Ядовитый след: 0.4 массы/сек за стак.</summary>
    public double TrailDamagePerSec => 0.4 * Stacks("m_toxic");

    /// <summary>Лёгкость: стоимость рывка ×0.6 за стак.</summary>
    public double DashCostMult => Pow(0.6, Stacks("m_light"));

    /// <summary>Регенерация: +0.02 массы/сек за стак.</summary>
    public double RegenPerSec => 0.02 * Stacks("m_regrow");

    /// <summary>Голод: порог поглощения 1.25 (база) → 1.10.</summary>
    public double AbsorbRatio => Stacks("m_hunger") > 0 ? HungerAbsorbRatio : BaseAbsorbRatio;

    /// <summary>Кристалл: скорость рывка ×0.85.</summary>
    public double DashSpeedMult => Stacks("m_crystal") > 0 ? 0.85 : 1.0;

    /// <summary>Жаростойкость: шипы, жар и испарение ×0.4.</summary>
    public double HazardMult => Stacks("m_heatproof") > 0 ? 0.4 : 1.0;

    /// <summary>Вампиризм: доля нанесённого урона, возвращаемая массой (0.35 за стак).</summary>
    public double LifestealFrac => 0.35 * Stacks("m_vampire");

    /// <summary>Рикошет: +0.25 к урону за каждый отскок в текущем рывке (за стак).</summary>
    public double RicochetBonus => 0.25 * Stacks("m_ricochet");

    /// <summary>Эхолокация: радиус чувства существ вне света (9 с мутацией, иначе 0).</summary>
    public double SenseRadius => Stacks("m_echo") > 0 ? 9.0 : 0.0;

    public string[] ToArray() => _stacks.ToArray();

    public static MutationSet FromArray(string[] ids) {
        var set = new MutationSet();
        if (ids == null) return set;
        for (int i = 0; i < ids.Length; i++) set.Add(ids[i]);
        return set;
    }

    private static double Pow(double factor, int stacks) {
        double v = 1.0;
        for (int i = 0; i < stacks; i++) v *= factor;
        return v;
    }
}
}
