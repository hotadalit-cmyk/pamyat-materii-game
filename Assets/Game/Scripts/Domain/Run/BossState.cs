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
/// Страж глубины — босс пятой глубины. Фазы по массе: &gt;60% Charge, 45–60% Brood,
/// 30–45% Frenzy, &lt;30% Exposed (можно поглотить касанием). Спит, пока игрок дальше 7;
/// кулдауны во сне стоят. Без UnityEngine, без LINQ.
/// </summary>
public sealed class BossState {
    public const string SpeciesId = "warden";

    private const double WakeRadius = 7.0;
    private const double ChargeDashEvery = 2.4;
    private const double BroodDashEvery = 3.2;
    private const double FrenzyDashEvery = 1.4;
    private const double MinionsEvery = 7.0;
    private const int MinionsPerWave = 3;
    private const double ShockwaveEvery = 5.0;
    private const double ExposedFleeEvery = 1.0;
    private const double ExposedFleeStrength = 0.3;
    private const double BroodBelowFrac = 0.60;   // ниже 60% стартовой массы — Brood
    private const double FrenzyBelowFrac = 0.45;  // ниже 45% — Frenzy
    private const double ExposedBelowFrac = 0.30; // ниже 30% — Exposed
    private const double ArmorSpeed = 6.0;        // удары медленнее — не пробивают броню
    private const double HitK = 0.35;

    private readonly PmRandom _rng; // для вырожденных направлений; логика детерминирована
    private double _mass;
    private double _dashTimer, _minionTimer, _waveTimer, _fleeTimer;
    private BossPhase _phase;

    public double Mass => _mass;
    public double StartMass { get; }
    public BossPhase Phase => _phase;
    /// <summary>Масса ниже 30% стартовой — можно поглотить касанием.</summary>
    public bool IsExposed => _mass < StartMass * ExposedBelowFrac;

    public BossState(double startMass, ulong seed) {
        StartMass = startMass > 0.0 ? startMass : 1.0;
        _mass = StartMass;
        _rng = new PmRandom(seed);
        _phase = BossPhase.Dormant;
        _dashTimer = 0.0;
        _minionTimer = 0.0;
        _waveTimer = 0.0;
        _fleeTimer = 0.0;
    }

    /// <summary>Такт стража. В Dormant — никаких действий, таймеры стоят.</summary>
    public BossOutput Think(BossInput input) {
        var output = new BossOutput();
        Vec2 toPlayer = input.PlayerPos - input.SelfPos;
        double dist = toPlayer.Length;

        if (IsExposed) { // обнажён: не атакует, медленно уходит от игрока
            _phase = BossPhase.Exposed;
            _fleeTimer -= input.Dt;
            if (_fleeTimer <= 0.0) {
                Vec2 away = dist < 1e-6 ? RandomDirection() : (-toPlayer).Normalized();
                if (away.Length < 0.5) away = RandomDirection();
                output.Impulse = true;
                output.Direction = away;
                output.Strength01 = ExposedFleeStrength;
                _fleeTimer = ExposedFleeEvery;
            }
            return output;
        }
        if (dist > WakeRadius) { // спит, пока игрок далеко
            _phase = BossPhase.Dormant;
            return output;
        }

        _phase = PhaseByMass();
        _dashTimer -= input.Dt;
        switch (_phase) {
            case BossPhase.Charge: // >60%: рывок к игроку каждые 2.4 с
                if (_dashTimer <= 0.0) { Dash(ref output, toPlayer, dist); _dashTimer = ChargeDashEvery; }
                break;
            case BossPhase.Brood:  // 45–60%: рывки 3.2 с + 3 миньона ("mite") каждые 7 с
                _minionTimer -= input.Dt;
                if (_dashTimer <= 0.0) { Dash(ref output, toPlayer, dist); _dashTimer = BroodDashEvery; }
                if (_minionTimer <= 0.0) { output.SpawnMinions = MinionsPerWave; _minionTimer = MinionsEvery; }
                break;
            default:               // Frenzy 30–45%: рывки 1.4 с + волна каждые 5 с
                _waveTimer -= input.Dt;
                if (_dashTimer <= 0.0) { Dash(ref output, toPlayer, dist); _dashTimer = FrenzyDashEvery; }
                if (_waveTimer <= 0.0) { output.Shockwave = true; _waveTimer = ShockwaveEvery; }
                break;
        }
        return output;
    }

    /// <summary>
    /// Удар игрока по стражу: relSpeed ниже брони (6) — 0. Иначе потеря
    /// 0.35×relSpeed×playerMass/(playerMass+Mass)×DamageDealtMult×(1+RicochetBonus×ricochets).
    /// </summary>
    public double ApplyHit(double relSpeed, double playerMass, MutationSet playerMut, int ricochets) {
        if (relSpeed < ArmorSpeed) return 0.0;
        double dealt = playerMut != null ? playerMut.DamageDealtMult : 1.0;
        double ricochet = playerMut != null ? playerMut.RicochetBonus : 0.0;
        double sum = playerMass + _mass;
        if (sum <= 0.0) return 0.0;
        double loss = HitK * relSpeed * (playerMass / sum) * dealt * (1.0 + ricochet * ricochets);
        if (loss <= 0.0) return 0.0;
        if (loss > _mass) loss = _mass;
        _mass -= loss;
        _phase = IsExposed ? BossPhase.Exposed : PhaseByMass(); // фаза не отстаёт от массы
        return loss;
    }

    private BossPhase PhaseByMass() {
        double frac = _mass / StartMass;
        if (frac > BroodBelowFrac) return BossPhase.Charge;
        if (frac > FrenzyBelowFrac) return BossPhase.Brood;
        return BossPhase.Frenzy;
    }

    private void Dash(ref BossOutput output, Vec2 toPlayer, double dist) {
        Vec2 dir = dist < 1e-6 ? RandomDirection() : toPlayer.Normalized();
        if (dir.Length < 0.5) dir = RandomDirection();
        output.Impulse = true;
        output.Direction = dir;
        output.Strength01 = 1.0;
    }

    private Vec2 RandomDirection() {
        double angle = _rng.NextDouble01() * 2.0 * Math.PI;
        return new Vec2(Math.Cos(angle), Math.Sin(angle));
    }
}
}
