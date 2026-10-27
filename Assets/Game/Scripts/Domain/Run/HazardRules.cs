using PM.Domain.Core;

namespace PM.Domain.Run {

/// <summary>Вид опасности биома (v0.15): вытянутая зона в форме полосы.</summary>
public enum HazardKind { LavaRiver = 0, Current = 1, Vein = 2 }

/// <summary>
/// Опасность карты: полоса вдоль Dir полуширинами HalfLen/HalfWidth вокруг Pos.
/// Лава — «мостик»-разрыв GapR на расстоянии GapAlong вдоль полосы (проход не перекрыт наглухо).
/// Жила бьёт раз в Period секунд (телеграф VeinTelegraph, удар VeinStrikeSec).
/// </summary>
public struct HazardSpec {
    public HazardKind Kind;
    public Vec2 Pos;
    public Vec2 Dir;              // единичный
    public double HalfLen, HalfWidth;
    public double GapAlong, GapR; // лава: круг-разрыв
    public double Period, Phase;  // жила: период и фаза удара
}

/// <summary>Правила опасностей биомов (v0.15). Чистый C# без UnityEngine, без LINQ.</summary>
public static class HazardRules {
    /// <summary>Лава: доля массы героя, сгорающая в секунду (до жаростойкости).</summary>
    public const double LavaLossFracPerSec = 0.02;
    /// <summary>Течение: ускорение сноса, ед/с².</summary>
    public const double CurrentForce = 3.0;
    public const double VeinTelegraph = 0.8;   // с предупреждения
    public const double VeinStrikeSec = 0.3;   // с длительность удара
    public const double VeinDamageFrac = 0.08; // доля массы героя за удар жилы
    public const double MinRiverGapR = 1.6;    // мостик лавы не уже этого
    public const double MinVeinPeriod = 4.0, MaxVeinPeriod = 6.0;

    /// <summary>Потеря массы в лаве за секунду (жаростойкость смягчает, как шипы).</summary>
    public static double LavaLossPerSec(double mass, MutationSet m) {
        if (mass < 0) mass = 0;
        return mass * LavaLossFracPerSec * (m != null ? m.HazardMult : 1.0);
    }

    /// <summary>Точка внутри полосы (с учётом мостика лавы).</summary>
    public static bool Inside(Vec2 p, HazardSpec h) {
        Vec2 d = p - h.Pos;
        double along = Vec2.Dot(d, h.Dir);
        if (along < -h.HalfLen || along > h.HalfLen) return false;
        double across = Vec2.Dot(d, new Vec2(-h.Dir.Y, h.Dir.X));
        if (across < -h.HalfWidth || across > h.HalfWidth) return false;
        if (h.GapR > 0.0) {
            Vec2 g = h.Pos + h.Dir.Scaled(h.GapAlong);
            if ((p - g).SqrLength <= h.GapR * h.GapR) return false;
        }
        return true;
    }

    /// <summary>Жила: 0 — покой, 1 — телеграф (0.8 с до удара), 2 — удар (0.3 с).</summary>
    public static int VeinStage(double time, HazardSpec h) {
        if (h.Period <= 0.0) return 0;
        double t = time >= 0 ? (time + h.Phase) % h.Period : 0.0;
        if (t >= h.Period - VeinStrikeSec) return 2;
        if (t >= h.Period - VeinStrikeSec - VeinTelegraph) return 1;
        return 0;
    }
}
}
