using System;
using PM.Domain.Core;
namespace PM.Domain.Run {

public enum BossPhase { Dormant = 0, Charge = 1, Brood = 2, Frenzy = 3, Exposed = 4 }

/// <summary>Вход такта стража.</summary>
public struct BossInput {
    public Vec2 SelfPos;
    public Vec2 PlayerPos;
    public double PlayerMass;
    public double Dt;
}

/// <summary>Выход такта: рывок, спавн миньонов, ударная волна.</summary>
public struct BossOutput {
    public bool Impulse;
    public Vec2 Direction;
    public double Strength01;
    public int SpawnMinions;
    public bool Shockwave;
}

/// <summary>
/// Фасад над GuardianState (v0.11): Пульсар — страж пятой глубины, поведение BossState 1:1.
/// Старый API сохранён (Mass/Phase/ApplyHit/IsExposed/Think) — Unity и RunBossTests не меняются.
/// Полный вывод стража (зоны, телеграфы, окно уязвимости) — через RunState.Guardian.
/// </summary>
public sealed class BossState {
    public const string SpeciesId = "warden";

    private readonly GuardianState _guardian;
    private readonly double _startMass;

    public double Mass => _guardian.Hp;
    public double StartMass => _startMass;
    public BossPhase Phase => MapPhase(_guardian.Phase);
    /// <summary>Масса ниже 30% стартовой — можно поглотить касанием.</summary>
    public bool IsExposed => _guardian.IsExposed;

    public BossState(double startMass, ulong seed) {
        _startMass = startMass > 0.0 ? startMass : 1.0;
        // allowRetreat: false — старое поведение без арены и сброса боя (RunBossTests 1:1)
        _guardian = new GuardianState(GuardianCatalog.ForDepth(RunState.MaxDepth), _startMass, seed, false);
    }

    /// <summary>Такт стража. В Dormant — никаких действий, таймеры стоят.</summary>
    public BossOutput Think(BossInput input) {
        var g = _guardian.Think(new GuardianInput {
            SelfPos = input.SelfPos, PlayerPos = input.PlayerPos,
            PlayerMass = input.PlayerMass, Dt = input.Dt,
        });
        return new BossOutput {
            Impulse = g.Impulse, Direction = g.Direction, Strength01 = g.Strength01,
            SpawnMinions = g.SpawnMinions, Shockwave = g.Shockwave,
        };
    }

    /// <summary>
    /// Удар игрока по стражу: relSpeed ниже брони (6) — 0. Иначе потеря
    /// 0.35×relSpeed×playerMass/(playerMass+Mass)×DamageDealtMult×(1+RicochetBonus×ricochets).
    /// </summary>
    public double ApplyHit(double relSpeed, double playerMass, MutationSet playerMut, int ricochets) =>
        _guardian.ApplyHit(relSpeed, playerMass, playerMut, ricochets);

    internal static BossPhase MapPhase(GuardianPhase p) {
        switch (p) {
            case GuardianPhase.Phase1: return BossPhase.Charge;
            case GuardianPhase.Phase2: return BossPhase.Brood;
            case GuardianPhase.Phase3: return BossPhase.Frenzy;
            case GuardianPhase.Exposed: return BossPhase.Exposed;
            default: return BossPhase.Dormant;
        }
    }
}
}
