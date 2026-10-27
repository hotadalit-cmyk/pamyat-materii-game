using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Результат контакта двух тел. Поглощение — потерь массы нет.</summary>
public struct ContactResult {
    public int Absorbed;                      // 0 нет; 1 — A поглотил B; 2 — B поглотил A
    public double MassLossA, MassLossB;       // потери массы (>=0), если не поглощение
}

/// <summary>
/// Правила размера и массы: радиус, поглощение, удар, капли, стоимость рывка.
/// v0.14 (Docs/BALANCE_v14.md §3): Attack/Toughness у видов, урон существа — процент массы
/// героя с границами 3–25%, проглатывание героя только от ×2 (иначе сильный укус 12%),
/// урон героя масштабируется массой героя, скоростью и жёсткостью цели.
/// </summary>
public static class SizeRule {
    public const double MinViableMass = 1.0;  // ниже — смерть игрока
    public const double HitK = 0.12;          // коэффициент удара (старые формулы стражей-фасада)
    public const double TouchSpeed = 1.0;     // медленнее — просто касание, потерь нет
    public const double DropletMass = 0.25;   // типичный размер капли
    public const int MaxDroplets = 8;

    // ---------- v0.14: боевая модель ----------
    public const double CreatureHitK = 0.025;      // база урона героя по существу (§3.2)
    public const double NominalSpeed = 6.0;        // скорость полной силы (быстрее — без прироста)
    public const double SwallowRatio = 2.0;        // существо глотает героя только от ×2 (§3.1)
    public const double StrongBiteRatio = 1.25;    // с этого отношения — сильный укус
    public const double StrongBiteFrac = 0.12;     // его гарантированная доля массы героя
    public const double MinHitFrac = 0.03;         // нижняя граница урона, % массы героя
    public const double MaxHitFrac = 0.25;         // верхняя граница урона, % массы героя
    public const double CreatureGrowthCap = 1.5;   // потолок роста существа от MaxMass спавна (§3.6)

    /// <summary>Множитель урона врагов по глубинам (GDD: ×1.0 … ×2.1).</summary>
    private static readonly double[] DepthDamageMults = { 1.0, 1.2, 1.45, 1.75, 2.1 };

    public static double DepthDamageMult(int depth) {
        if (depth < 1) depth = 1;
        if (depth > DepthDamageMults.Length) depth = DepthDamageMults.Length;
        return DepthDamageMults[depth - 1];
    }

    /// <summary>Доля массы героя за удар: 0..1 до границ; attack — доля вида при полной скорости.</summary>
    public static double HitFrac(double attack, double relSpeed, int depth, double takenMult) {
        if (attack <= 0.0) return 0.0;
        double sf = relSpeed < NominalSpeed ? relSpeed / NominalSpeed : 1.0;
        double f = attack * sf * DepthDamageMult(depth) * takenMult;
        if (f > MaxHitFrac) f = MaxHitFrac;
        if (f < MinHitFrac) f = MinHitFrac;
        return f;
    }

    /// <summary>0.35 × sqrt(max(mass, 0.05)).</summary>
    public static double RadiusForMass(double mass) {
        double m = mass < 0.05 ? 0.05 : mass;
        return 0.35 * System.Math.Sqrt(m);
    }

    /// <summary>
    /// Поглощение: massA >= massB × AbsorbRatio(A) → A поглотил B (и наоборот, приоритет A).
    /// v0.14: B поглощает A только при massB >= massA × SwallowRatio (2.0);
    /// иначе удар: B теряет CreatureHitK×relSpeed×√доли×… / Toughness(B),
    /// A теряет HitFrac(Attack(B), relSpeed, depth)×massA. relSpeed &lt; 1.0 → касание без потерь.
    /// </summary>
    public static ContactResult Resolve(double massA, MutationSet mutA, double massB, MutationSet mutB, double relSpeed) {
        return Resolve(massA, mutA, massB, mutB, relSpeed, 0.08, 1.0, 1);
    }

    /// <summary>v0.14: полный контракт — attack/toughness из SpeciesDef, depth для множителя урона.</summary>
    public static ContactResult Resolve(double massA, MutationSet mutA, double massB, MutationSet mutB,
                                        double relSpeed, double attackB, double toughnessB, int depth) {
        var r = new ContactResult();
        double ratioA = mutA != null ? mutA.AbsorbRatio : 1.25;
        if (massA >= massB * ratioA) { r.Absorbed = 1; return r; }
        if (massB >= massA * SwallowRatio) { r.Absorbed = 2; return r; }
        if (relSpeed < TouchSpeed) return r;
        double sf = relSpeed < NominalSpeed ? relSpeed / NominalSpeed : 1.0;
        double dealtA = mutA != null ? mutA.DamageDealtMult : 1.0;
        double takenB = mutB != null ? mutB.DamageTakenMult : 1.0;
        double takenA = mutA != null ? mutA.DamageTakenMult : 1.0;
        // урон существу: масса героя × скорость × √доля; жёсткость делит
        // (рикошет, как и раньше, добавляет Unity поверх — RunGame.ResolveContact)
        double share = massA + massB > 0.0 ? massA / (massA + massB) : 0.5;
        double lossB = CreatureHitK * massA * relSpeed * System.Math.Sqrt(share) * sf
                       * dealtA * takenB / (toughnessB > 0.0 ? toughnessB : 1.0);
        if (lossB > massB) lossB = massB;
        if (lossB < 0.0) lossB = 0.0;
        r.MassLossB = lossB;
        // урон герою: процент массы героя от Attack вида, глубины и скорости; сильный укус крупнее ×1.25
        double f = HitFrac(attackB, relSpeed, depth, takenA);
        if (massB >= massA * StrongBiteRatio && f < StrongBiteFrac) f = StrongBiteFrac;
        r.MassLossA = massA * f;
        return r;
    }

    /// <summary>Потерянная масса разлетается каплями по ~0.25 (не больше 8 штук), сумма = потеря.</summary>
    public static double[] SplitIntoDroplets(double lostMass) {
        if (lostMass <= 1e-9) return new double[0];
        int n = (int)System.Math.Ceiling(lostMass / DropletMass);
        if (n < 1) n = 1;
        if (n > MaxDroplets) n = MaxDroplets;
        double each = lostMass / n;
        var droplets = new double[n];
        for (int i = 0; i < n; i++) droplets[i] = each;
        return droplets;
    }

    /// <summary>max(0.04, 0.025×mass) × DashCostMult(мутации) × soulCostMult (душа).</summary>
    public static double DashCost(double mass, MutationSet m, double soulCostMult) {
        double baseCost = 0.025 * mass;
        if (baseCost < 0.04) baseCost = 0.04;
        double mutationMult = m != null ? m.DashCostMult : 1.0;
        return baseCost * mutationMult * soulCostMult;
    }
}
}
