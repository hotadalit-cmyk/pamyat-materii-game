using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Результат контакта двух тел. Поглощение — потерь массы нет.</summary>
public struct ContactResult {
    public int Absorbed;                      // 0 нет; 1 — A поглотил B; 2 — B поглотил A
    public double MassLossA, MassLossB;       // потери массы (>=0), если не поглощение
}

/// <summary>Правила размера и массы: радиус, поглощение, удар, капли, стоимость рывка.</summary>
public static class SizeRule {
    public const double MinViableMass = 1.0;  // ниже — смерть игрока
    public const double HitK = 0.12;          // коэффициент удара
    public const double TouchSpeed = 1.0;     // медленнее — просто касание, потерь нет
    public const double DropletMass = 0.25;   // типичный размер капли
    public const int MaxDroplets = 8;

    /// <summary>0.35 × sqrt(max(mass, 0.05)).</summary>
    public static double RadiusForMass(double mass) {
        double m = mass < 0.05 ? 0.05 : mass;
        return 0.35 * System.Math.Sqrt(m);
    }

    /// <summary>
    /// Поглощение: massA >= massB × AbsorbRatio(A) → A поглотил B (и наоборот, приоритет A).
    /// Иначе удар: потеря B = k × relSpeed × massA/(massA+massB) × DamageDealt(A) × DamageTaken(B),
    /// симметрично для A. relSpeed &lt; 1.0 → касание без потерь.
    /// </summary>
    public static ContactResult Resolve(double massA, MutationSet mutA, double massB, MutationSet mutB, double relSpeed) {
        var r = new ContactResult();
        double ratioA = mutA != null ? mutA.AbsorbRatio : 1.25;
        double ratioB = mutB != null ? mutB.AbsorbRatio : 1.25;
        if (massA >= massB * ratioA) { r.Absorbed = 1; return r; }
        if (massB >= massA * ratioB) { r.Absorbed = 2; return r; }
        if (relSpeed < TouchSpeed) return r;
        double sum = massA + massB;
        if (sum <= 0.0) return r;
        double dealtA = mutA != null ? mutA.DamageDealtMult : 1.0;
        double takenA = mutA != null ? mutA.DamageTakenMult : 1.0;
        double dealtB = mutB != null ? mutB.DamageDealtMult : 1.0;
        double takenB = mutB != null ? mutB.DamageTakenMult : 1.0;
        r.MassLossB = HitK * relSpeed * (massA / sum) * dealtA * takenB;
        r.MassLossA = HitK * relSpeed * (massB / sum) * dealtB * takenA;
        if (r.MassLossA < 0.0) r.MassLossA = 0.0;
        if (r.MassLossB < 0.0) r.MassLossB = 0.0;
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
