using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Тип препятствия. Pillar/Mushroom/Crystal/Thorns — твёрдые; Vent/Pool/Sticky — зоны.</summary>
public enum ObstacleKind { Pillar = 0, Mushroom = 1, Crystal = 2, Thorns = 3, Vent = 4, Pool = 5, Sticky = 6 }

/// <summary>Препятствие в комнате: тип, позиция, радиус.</summary>
public struct ObstacleSpec {
    public ObstacleKind Kind;
    public Vec2 Pos;
    public double Radius;
}

/// <summary>
/// Правила препятствий: твёрдость, упругость, урон шипов, эффекты зон, толчок жерла.
/// Чистый C# без UnityEngine, без LINQ.
/// </summary>
public static class ObstacleRules {
    /// <summary>Кристалл раскалывается ударом с этой скоростью (3 капли по 0.4).</summary>
    public const double ShatterSpeed = 6.0;

    private const double ThornBase = 0.15, ThornPerSpeed = 0.08;
    private const double PoolRegenPerSec = 0.06;
    private const double VentEvapFrac = 0.02;
    private const double VentPushMax = 14.0;
    private const double PoolDamping = 2.5, StickyDamping = 6.0;
    private const double ThornsBounciness = 0.5; // спецификацией не задан: шипы гасят отскок

    /// <summary>Твёрдые препятствия останавливают движение; зоны проходимы.</summary>
    public static bool IsSolid(ObstacleKind k) {
        return k == ObstacleKind.Pillar || k == ObstacleKind.Mushroom
            || k == ObstacleKind.Crystal || k == ObstacleKind.Thorns;
    }

    /// <summary>Упругость: колонна 0.75, гриб-бампер 1.35 (разгоняет), кристалл 1.1, шипы 0.5.</summary>
    public static double Bounciness(ObstacleKind k) {
        switch (k) {
            case ObstacleKind.Pillar: return 0.75;
            case ObstacleKind.Mushroom: return 1.35;
            case ObstacleKind.Crystal: return 1.1;
            case ObstacleKind.Thorns: return ThornsBounciness;
            default: return 0.0; // зоны не твёрдые
        }
    }

    /// <summary>Шипы: потеря массы (0.15 + 0.08×relSpeed) × HazardMut (жаростойкость ×0.4).</summary>
    public static double ThornLoss(double relSpeed, MutationSet m) {
        double mult = m != null ? m.HazardMult : 1.0;
        double loss = (ThornBase + ThornPerSpeed * relSpeed) * mult;
        return loss < 0.0 ? 0.0 : loss;
    }

    /// <summary>Зона: Pool +0.06 массы/сек; Vent −0.02×mass×HazardMult/сек; иначе 0.</summary>
    public static double ZoneMassPerSec(ObstacleKind k, double mass, MutationSet m) {
        if (k == ObstacleKind.Pool) return PoolRegenPerSec;
        if (k == ObstacleKind.Vent) {
            double mult = m != null ? m.HazardMult : 1.0;
            return -VentEvapFrac * mass * mult;
        }
        return 0.0;
    }

    /// <summary>Дополнительное линейное торможение в зоне: Pool 2.5, Sticky 6, иначе 0.</summary>
    public static double ZoneDamping(ObstacleKind k) {
        if (k == ObstacleKind.Pool) return PoolDamping;
        if (k == ObstacleKind.Sticky) return StickyDamping;
        return 0.0;
    }

    /// <summary>Жерло: толчок от центра — 14 в центре, линейно до 0 на краю (и дальше).</summary>
    public static double VentPush(double dist, double radius) {
        if (radius <= 0.0 || dist < 0.0 || dist >= radius) return 0.0;
        double push = VentPushMax * (1.0 - dist / radius);
        return push < 0.0 ? 0.0 : push;
    }
}
}
