using PM.Domain.Core;
namespace PM.Domain.Movement {
/// <summary>Параметры тела, влияющие на движение (форма/органы).</summary>
public struct OrganStats {
    public double ImpulsePower;    // множитель силы рывка
    public double MinSpeed;        // скорость при нулевом заряде
    public double MaxSpeed;        // скорость при полном заряде
    public double WaterRetention;  // 0..1 снижение потерь следа (узлы генов)
    public double Conductivity;    // пропускная способность тела для духовной энергии
    public double ArmorResistance; // 0..1 сопротивление удару
    public double CaptureStrength; // сила удержания добычи
    public static OrganStats BaseSlime() => new OrganStats {
        ImpulsePower = 1.0, MinSpeed = 1.2, MaxSpeed = 9.0,
        WaterRetention = 0.0, Conductivity = 0.4, ArmorResistance = 0.0, CaptureStrength = 1.0
    };
}
/// <summary>Настройки планировщика рывка. Значения стартовые, не согласованный баланс.</summary>
public sealed class MovementConfig {
    public double DragMax = 3.2;          // пикселей условного экрана до полного заряда
    public double MinDrag = 0.35;         // минимальный порог жеста
    public double FullJetEnergy = 10.0;   // духовная энергия полного рывка
    public double LaunchWaterBase = 0.05; // базовая цена запуска (D-16: малая отдельная цена)
    public double MinEnergyFactor = 0.25; // при пустой энергии рывок слабеет, но не исчезает
}
/// <summary>План рывка: что именно применит физический адаптер (Systems) и сколько стоит.</summary>
public struct JetPlan {
    public Vec2 Impulse;        // скорость-импульс (units/s), применяется как set/добавка скорости
    public double WaterRequested;
    public double WaterSpent;   // ограничен доступной водой (D-16)
    public double EnergySpent;
    public bool Valid;
    public string FailReason;
    public bool DehydrationRisk; // вода кончилась в момент рывка — риск урона телу
}
}
