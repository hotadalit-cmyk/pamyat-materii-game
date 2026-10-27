// PM.Domain.Run — мозг автопилота «Марионетка» (v0.13). Чистая функция решения.
// C# 9, без LINQ и System.Random; вход — снимок мира, выход — движение/рывок/намерение.
using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Что делает автопилот сейчас — для подписи над героем и для тестов.</summary>
public enum PuppetIntent { Hunt = 0, Flee = 1, Graze = 2, Return = 3, Avoid = 4 }

/// <summary>Снимок одного существа, видимого автопилотом.</summary>
public struct PuppetSense {
    public Vec2 Pos;
    public double Mass;
    public bool IsFood;
    public bool IsShimmering;
    public bool Threatens;     // может ранить (съесть нас / крупнее)
}

/// <summary>Опасная зона или круг-телеграф стража.</summary>
public struct PuppetZone {
    public Vec2 Pos;
    public double Radius;
}

/// <summary>Вход мозга: снимок себя, окружения, ноши и связи с лидером.</summary>
public sealed class PuppetInput {
    public Vec2 SelfPos;
    public double SelfMass;
    public Vec2 SelfVel;
    public int DashCharges;
    public int MaxDashCharges;
    public PuppetSense[] Creatures;
    public PuppetZone[] Zones;
    public double Carry;           // ноша (для клона); 0 у героя
    public double CarryCapacity;
    public Vec2 LeaderPos;
    public double Leash;           // дальность связи (для клона); 0 у героя
    public double AbsorbRatio;     // жертва съедобна, если SelfMass >= её масса × AbsorbRatio
}

/// <summary>Решение мозга: куда двигаться, делать ли рывок и как это назвать.</summary>
public struct PuppetDecision {
    public Vec2 Move;              // желаемое направление (единичной длины или ноль)
    public bool Dash;
    public Vec2 DashDir;
    public PuppetIntent Intent;
}

public static class PuppetBrain {
    public const double FleeRadius = 4.0;        // от тех, кто ранит, ближе 4 — бегство
    public const double DashMinDist = 2.0;       // рывок — цель на линии 2–6
    public const double DashMaxDist = 6.0;
    public const double ShimmerBonus = 1.5;      // мерцающая цель ×1.5 к выгоде
    public const double ReturnCarryFrac = 0.6;   // ноша > 60% ёмкости — домой
    public const double ReturnLeashFrac = 0.85;  // у края связи — домой
    public const double ZoneMargin = 1.5;        // запас за границей зоны, когда начинаем обход

    /// <summary>
    /// Приоритеты: бегство от угроз ближе 4 → обход опасных зон → возврат клона с ношей
    /// или у края связи → охота на самую выгодную цель → пасётся (споры). Детерминировано.
    /// </summary>
    public static PuppetDecision Think(PuppetInput input) {
        var d = new PuppetDecision { Move = Vec2.Zero, Intent = PuppetIntent.Graze };
        if (input == null) return d;
        Vec2 me = input.SelfPos;

        // 1) бегство: те, кто ранит, ближе FleeRadius
        Vec2 flee = Vec2.Zero;
        bool fleeing = false;
        var cr = input.Creatures;
        if (cr != null) for (int i = 0; i < cr.Length; i++) {
            if (!cr[i].Threatens) continue;
            Vec2 away = me - cr[i].Pos;
            double dist = away.Length;
            if (dist >= FleeRadius) continue;
            if (dist < 1e-6) { away = Vec2.Up; dist = 1.0; }
            flee += away / dist * (1.0 - dist / FleeRadius);
            fleeing = true;
        }
        if (fleeing) {
            d.Move = flee.Normalized();
            d.Intent = PuppetIntent.Flee;
            return d;
        }

        // 2) обход опасных зон и телеграфов
        var zn = input.Zones;
        if (zn != null) for (int i = 0; i < zn.Length; i++) {
            if (zn[i].Radius <= 0.0) continue;
            Vec2 away = me - zn[i].Pos;
            double dist = away.Length;
            double reach = zn[i].Radius + ZoneMargin;
            if (dist >= reach) continue;
            if (dist < 1e-6) { away = Vec2.Up; dist = 1.0; }
            d.Move += away / dist * (1.0 - dist / reach);
            d.Intent = PuppetIntent.Avoid;
        }
        if (d.Intent == PuppetIntent.Avoid) {
            d.Move = d.Move.Normalized();
            return d;
        }

        // 3) клон: ноша больше 60% ёмкости или у края связи — возврат к лидеру
        double distToLead = (input.LeaderPos - me).Length;
        bool overCarry = input.CarryCapacity > 0.0 && input.Carry > input.CarryCapacity * ReturnCarryFrac;
        bool atEdge = input.Leash > 0.0 && distToLead > input.Leash * ReturnLeashFrac;
        if (overCarry || atEdge) {
            d.Move = distToLead > 1e-6 ? (input.LeaderPos - me) / distToLead : Vec2.Zero;
            d.Intent = PuppetIntent.Return;
            return d;
        }

        // 4) охота: лучшая не-спора, которую можно съесть — масса × (мерцание ×1.5) / дистанция
        double bestScore = 0.0;
        Vec2 bestDir = Vec2.Zero;
        double bestDist = 0.0;
        bool has = false;
        if (cr != null) for (int i = 0; i < cr.Length; i++) {
            var s = cr[i];
            if (s.IsFood) continue;                       // споры — выпас, не охота
            if (input.SelfMass < s.Mass * input.AbsorbRatio) continue;
            Vec2 to = s.Pos - me;
            double dist = to.Length;
            if (dist < 1e-6) continue;
            double score = s.Mass * (s.IsShimmering ? ShimmerBonus : 1.0) / dist;
            if (score > bestScore) {
                bestScore = score;
                bestDir = to / dist;
                bestDist = dist;
                has = true;
            }
        }
        if (has) {
            d.Move = bestDir;
            d.Intent = PuppetIntent.Hunt;
            // рывок — только с запасом: держим 1 заряд на побег
            if (input.DashCharges > 1 && bestDist >= DashMinDist && bestDist <= DashMaxDist) {
                d.Dash = true;
                d.DashDir = bestDir;
            }
            return d;
        }

        // 5) охоты нет — пасётся по спорам (ближайшая еда), совсем пусто — дрейф по инерции
        double foodDist = double.MaxValue;
        Vec2 foodDir = Vec2.Zero;
        if (cr != null) for (int i = 0; i < cr.Length; i++) {
            if (!cr[i].IsFood) continue;
            Vec2 to = cr[i].Pos - me;
            double dist = to.Length;
            if (dist < 1e-6) continue;
            if (dist < foodDist) { foodDist = dist; foodDir = to / dist; }
        }
        d.Intent = PuppetIntent.Graze;
        if (foodDist < double.MaxValue) d.Move = foodDir;
        else if (input.SelfVel.Length > 0.3) d.Move = input.SelfVel.Normalized() * 0.3;
        return d;
    }

    /// <summary>Человекочитаемое намерение — подпись над героем («охота», «бегство»…).</summary>
    public static string IntentName(PuppetIntent intent) {
        switch (intent) {
            case PuppetIntent.Hunt: return "охота";
            case PuppetIntent.Flee: return "бегство";
            case PuppetIntent.Return: return "возврат";
            case PuppetIntent.Avoid: return "обход";
            default: return "пасётся";
        }
    }
}
}
