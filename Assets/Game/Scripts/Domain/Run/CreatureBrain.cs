using System;
using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Вход мозга существа: где игрок, кто тяжелее, поведение, время.</summary>
public struct BrainInput {
    public Vec2 SelfPos;
    public double SelfMass;
    public CreatureBehavior Behavior;
    public Vec2 PlayerPos;
    public double PlayerMass;
    public double Time;
}

/// <summary>Выход мозга: импульс, направление (единичной длины), сила 0..1.</summary>
public struct BrainOutput {
    public bool Impulse;
    public Vec2 Direction;
    public double Strength01;
}

/// <summary>
/// Детерминированный мозг существа. Не чаще одного импульса в 0.9–2.2 с (случайно);
/// Charger — пауза ровно 2 с. Без UnityEngine, без LINQ.
/// </summary>
public sealed class CreatureBrain {
    private const double FleeRadius = 4.0;
    private const double ChaseRadius = 6.0;
    private const double ChargeRadius = 7.0;
    private const double MinInterval = 0.9;
    private const double MaxInterval = 2.2;
    private const double ChargerPause = 2.0;
    private const double DriftStrength = 0.3;
    private const double BurstStrength = 1.0;

    private readonly PmRandom _rng;
    private double _nextAllowedTime;

    public CreatureBrain(ulong seed) {
        _rng = new PmRandom(seed);
        _nextAllowedTime = 0.0;
    }

    public BrainOutput Think(BrainInput input) {
        var output = new BrainOutput();
        if (input.Behavior == CreatureBehavior.Food) return output; // еда никогда не импульсит
        if (input.Time < _nextAllowedTime) return output;           // кулдаун

        Vec2 toPlayer = input.PlayerPos - input.SelfPos;
        double dist = toPlayer.Length;
        bool playerHeavier = input.PlayerMass > input.SelfMass;
        bool selfHeavier = input.SelfMass > input.PlayerMass;
        Vec2 dir;
        double strength;

        switch (input.Behavior) {
            case CreatureBehavior.Charger:
                if (dist >= ChargeRadius) return output; // далеко: ждёт, кулдаун не тратит
                dir = toPlayer.Normalized();
                strength = BurstStrength;
                break;
            case CreatureBehavior.Flee:
                if (dist < FleeRadius && playerHeavier) { dir = NormalizeSafe(-toPlayer); strength = BurstStrength; }
                else { dir = RandomDirection(); strength = DriftStrength; }
                break;
            case CreatureBehavior.Chase:
                if (dist < ChaseRadius && selfHeavier) { dir = NormalizeSafe(toPlayer); strength = BurstStrength; }
                else if (dist < FleeRadius && playerHeavier) { dir = NormalizeSafe(-toPlayer); strength = BurstStrength; }
                else { dir = RandomDirection(); strength = DriftStrength; }
                break;
            default: // Drift
                dir = RandomDirection();
                strength = DriftStrength;
                break;
        }

        double interval = input.Behavior == CreatureBehavior.Charger
            ? ChargerPause
            : MinInterval + _rng.NextDouble01() * (MaxInterval - MinInterval);
        _nextAllowedTime = input.Time + interval;
        output.Impulse = true;
        output.Direction = dir;
        output.Strength01 = strength;
        return output;
    }

    private Vec2 NormalizeSafe(Vec2 v) {
        Vec2 n = v.Normalized();
        return n.Length < 0.5 ? RandomDirection() : n; // вырожденный ноль → случайное направление
    }

    private Vec2 RandomDirection() {
        double angle = _rng.NextDouble01() * 2.0 * Math.PI;
        return new Vec2(Math.Cos(angle), Math.Sin(angle));
    }
}
}
