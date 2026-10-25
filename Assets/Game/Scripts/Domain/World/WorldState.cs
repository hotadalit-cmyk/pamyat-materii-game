using System.Collections.Generic;
using PM.Domain.Absorption;
using PM.Domain.Body;
using PM.Domain.Core;
using PM.Domain.Movement;
namespace PM.Domain.World {
/// <summary>Кинематический актёр симуляции (игрок или ИИ-носитель души).</summary>
public sealed class SimActor {
    public int Id;
    public bool IsPlayer;
    public Vec2 Pos;
    public Vec2 Vel;
    public double Radius = 0.6;
    public BodyMassState Body;
    public BodyVitals Vitals = new BodyVitals();
    public OrganStats Organs;
    public string FormId = "form_slime";
    public bool Alive = true;
    public double PathLenAccum;      // пройденный путь с последней синхронизации/шага
    public double LastSignificantAt; // для угасания души (игрок)
}
/// <summary>Мелкая неживая пища (объект биогравитации, T07).</summary>
public sealed class FoodPellet {
    public int Id;
    public Vec2 Pos;
    public double Mass;
    public bool Consumed;
}
/// <summary>Грибной жук — живая добыча (T08/T09).</summary>
public sealed class BeetlePrey {
    public int Id;
    public Vec2 Pos;
    public Vec2 Vel;
    public double Radius = 0.55;
    public double Mass = 2.0;
    public double Health01 = 1.0;
    public double Resistance = 1.0;  // сопротивление удержанию
    public PreyPhase Phase = PreyPhase.Free;
    public bool Consumed;
}
/// <summary>Событие истории мира (судьбы существ — SOUL_WORLD «WorldState»).</summary>
public struct WorldEvent {
    public string Kind;
    public long Tick;
    public string Detail;
}
/// <summary>Настройки арены и физики прототипа. Значения стартовые.</summary>
public sealed class ArenaConfig {
    public double HalfWidth = 8.89;   // 16:9 → 17.78 x 10
    public double HalfHeight = 5.0;
    public double Restitution = 0.55; // отскок от стен
    public double LinearDragPerSec = 0.9;
    public double FixedStep = 1.0 / 50.0;
    public double SurfaceLossRate = 0.004; // кг воды за unit пути (D-16)
    public double EnergyRegenPerSec = 0.6;
    public double CaptureRadius = 1.7;
    public double WeakenThreshold = 0.35;
    public double ReabsorbRadius = 1.0;
    public double MetabolizeReserveRate = 0.8; // эффективность запасы→вода
}
}
