using System;
using PM.Domain.Core;
namespace PM.Domain.Run {

public enum GuardianPhase { Dormant = 0, Phase1 = 1, Phase2 = 2, Phase3 = 3, Exposed = 4 }

/// <summary>Вход такта стража (v0.11, расширяет BossInput).</summary>
public struct GuardianInput {
    public Vec2 SelfPos;
    public Vec2 PlayerPos;
    public double PlayerMass;
    public double Dt;
}

/// <summary>Зона угрозы: споры, лужи, эхо. Позиции отдаёт страж, урон/замедление применяет Unity.</summary>
public struct GuardianZone {
    public Vec2 Pos;
    public double Radius;       // 0 — зоны нет
    public double DamagePerSec; // урон массы в секунду
    public double Slow01;       // 0..1 замедление
    public double Life;         // секунд до перестановки (999 — живут, пока страж жив)
}

/// <summary>Предупреждение о будущем ударе: Unity рисует кольцо, заполняющееся за Seconds.</summary>
public struct GuardianTelegraph {
    public Vec2 Pos;
    public double Radius;
    public double Seconds;      // сколько осталось до удара; 0 — телеграфа нет
}

/// <summary>Выход такта стража (v0.11): рывок, миньоны, волна, зоны, телеграфы, окно уязвимости.</summary>
public struct GuardianOutput {
    public bool Impulse;
    public Vec2 Direction;
    public double Strength01;
    public int SpawnMinions;
    public string MinionSpecies;
    public double MinionMass;
    public bool Shockwave;
    public GuardianZone[] Zones;      // кэш стража — читать сразу после Think, не хранить
    public GuardianTelegraph Telegraph;
    public bool WeakPointOpen;        // окно уязвимости открыто — урон ×WeakDamageMult
    public bool ResetFight;           // игрок отступил в фазе 1 — бой сброшен (штраф: RunState)
    public bool Strike;               // телеграф дотянулся: Unity применяет StrikeDamageFrac массы героя
    public double StrikeDamageFrac;
    public Vec2 StrikePos;
    public double StrikeRadius;
}

/// <summary>
/// Страж глубины (v0.11): общий автомат пяти боссов каталога. Спит, пока игрок дальше WakeRadius;
/// во сне таймеры стоят. Обнажён (&lt;30% HP) — не атакует, медленно уходит, поглощается касанием.
/// Пульсар — поведение старого BossState 1:1; Спороносица — зоны, корни, окно уязвимости.
/// Без UnityEngine, без LINQ, детерминирован по seed.
/// </summary>
public sealed class GuardianState {
    public const double ExposedBelowFrac = 0.30; // ниже 30% стартового HP — обнажён
    public const double RetreatMult = 1.5;       // сброс боя дальше ArenaRadius × 1.5
    private const double ArmorSpeed = 6.0;       // удары медленнее — не пробивают броню
    private const double HitK = 0.35;
    // Пульсар (BossState 1:1)
    private const double ChargeDashEvery = 2.4;
    private const double BroodDashEvery = 3.2;
    private const double FrenzyDashEvery = 1.4;
    private const double PulsarMinionsEvery = 7.0;
    private const double ShockwaveEvery = 5.0;
    private const double ExposedFleeEvery = 1.0;
    private const double ExposedFleeStrength = 0.3;

    private readonly GuardianDef _def;
    private readonly PmRandom _rng; // вырожденные направления; логика детерминирована
    private readonly GuardianZone[] _zones;
    private readonly bool _allowRetreat; // сброс боя при отступлении (в комнатном режиме выключен)
    private double _hp, _maxHp, _startHp;
    private GuardianPhase _phase;
    private double _dashTimer, _minionTimer, _waveTimer, _fleeTimer;
    private double _zoneTimer, _weakTimer, _rootCd, _pendingT;
    private GuardianTelegraph _pending;
    private bool _weakOpen, _fightActive;

    public GuardianDef Def => _def;
    public double Hp => _hp;
    public double MaxHp => _maxHp;
    /// <summary>Базовая HP текущего боя (от неё считаются фазы и обнажение).</summary>
    public double StartHp => _startHp;
    public GuardianPhase Phase => _phase;
    public bool IsExposed => _hp < _startHp * ExposedBelowFrac;
    public bool WeakPointOpen => _weakOpen;

    public GuardianState(GuardianDef def, double maxHp, ulong seed, bool allowRetreat = true) {
        _def = def;
        _rng = new PmRandom(seed);
        _allowRetreat = allowRetreat;
        maxHp = maxHp > 0.0 ? maxHp : 1.0;
        _hp = _maxHp = _startHp = maxHp;
        _phase = GuardianPhase.Dormant;
        _zones = new GuardianZone[def != null ? def.ZoneCount : 0];
        _zoneTimer = 0.0; _weakTimer = -1.0; _rootCd = 0.0; _pendingT = 0.0;
    }

    /// <summary>Восстановление после сброса боя: HP = maxHp, таймеры, зоны и окно уязвимости сброшены.</summary>
    public void Restore(double maxHp) {
        _maxHp = maxHp > 0.0 ? maxHp : 1.0;
        _hp = _maxHp;
        _startHp = _maxHp;
        _phase = GuardianPhase.Phase1;
        _dashTimer = _minionTimer = _waveTimer = _fleeTimer = 0.0;
        _zoneTimer = 0.0; _weakTimer = -1.0; _weakOpen = false; _rootCd = 0.0;
        _pendingT = 0.0; _pending = default;
        ClearZones();
        _fightActive = false;
    }

    /// <summary>Такт стража. В Dormant — никаких действий, таймеры стоят.</summary>
    public GuardianOutput Think(GuardianInput input) {
        var o = new GuardianOutput { Zones = _zones };
        Vec2 toPlayer = input.PlayerPos - input.SelfPos;
        double dist = toPlayer.Length;

        if (IsExposed) { // обнажён: не атакует, медленно уходит
            _phase = GuardianPhase.Exposed;
            _fleeTimer -= input.Dt;
            if (_fleeTimer <= 0.0) {
                Vec2 away = dist < 1e-6 ? RandomDirection() : (-toPlayer).Normalized();
                if (away.Length < 0.5) away = RandomDirection();
                o.Impulse = true;
                o.Direction = away;
                o.Strength01 = ExposedFleeStrength;
                _fleeTimer = ExposedFleeEvery;
            }
            o.WeakPointOpen = false;
            return o;
        }
        // v0.11: окно отступления — пока фаза 1, выход из арены сбрасывает бой
        // (проверка до радиуса сна: арена × 1.5 всегда больше WakeRadius).
        if (_allowRetreat && _fightActive && _phase == GuardianPhase.Phase1
            && dist > Def.ArenaRadius * RetreatMult) {
            Restore(_maxHp); // штраф герою (−25% массы, счёт поражений) — RunState.OnGuardianFightReset
            o.ResetFight = true;
            return o;
        }
        if (dist > Def.WakeRadius) { // спит, пока игрок далеко; таймеры стоят
            _phase = GuardianPhase.Dormant;
            return o;
        }
        if (dist <= Def.ArenaRadius) _fightActive = true;

        _phase = ComputePhase();
        if (Def.Kind == GuardianKind.SporeMother) ThinkSpore(input, ref o);
        else ThinkPulsar(input, toPlayer, dist, ref o);
        return o;
    }

    /// <summary>
    /// Удар игрока по стражу. Пульсар — формула старого BossState 1:1 (потеря
    /// 0.35×relSpeed×playerMass/(playerMass+Hp)×модификаторы). Спороносица: закрытая
    /// шляпка ×ClosedCapMult, открытое окно уязвимости ×WeakDamageMult.
    /// </summary>
    public double ApplyHit(double relSpeed, double playerMass, MutationSet playerMut, int ricochets) {
        if (relSpeed < ArmorSpeed) return 0.0;
        double mult = 1.0;
        if (Def.Kind == GuardianKind.SporeMother) mult = _weakOpen ? Def.WeakDamageMult : Def.ClosedCapMult;
        double dealt = playerMut != null ? playerMut.DamageDealtMult : 1.0;
        double ricochet = playerMut != null ? playerMut.RicochetBonus : 0.0;
        double sum = playerMass + _hp;
        if (sum <= 0.0) return 0.0;
        double loss = HitK * relSpeed * (playerMass / sum) * dealt * (1.0 + ricochet * ricochets) * mult;
        if (loss <= 0.0) return 0.0;
        if (loss > _hp) loss = _hp;
        _hp -= loss;
        _phase = IsExposed ? GuardianPhase.Exposed : ComputePhase(); // фаза не отстаёт от HP
        return loss;
    }

    private GuardianPhase ComputePhase() {
        double frac = _hp / _startHp;
        if (frac > Def.PhaseThreshold2) return GuardianPhase.Phase1;
        if (frac > Def.PhaseThreshold3) return GuardianPhase.Phase2;
        return GuardianPhase.Phase3;
    }

    // ---------- Пульсар: поведение BossState 1:1 ----------
    private void ThinkPulsar(GuardianInput input, Vec2 toPlayer, double dist, ref GuardianOutput o) {
        _dashTimer -= input.Dt;
        switch (_phase) {
            case GuardianPhase.Phase1: // рывок к игроку каждые 2.4 с
                if (_dashTimer <= 0.0) { Dash(ref o, toPlayer, dist); _dashTimer = ChargeDashEvery; }
                break;
            case GuardianPhase.Phase2: // рывки 3.2 с + выводок каждые 7 с
                _minionTimer -= input.Dt;
                if (_dashTimer <= 0.0) { Dash(ref o, toPlayer, dist); _dashTimer = BroodDashEvery; }
                if (_minionTimer <= 0.0) {
                    o.SpawnMinions = Def.MinionsPerWave;
                    o.MinionSpecies = Def.MinionSpecies;
                    o.MinionMass = Def.MinionMass;
                    _minionTimer = PulsarMinionsEvery;
                }
                break;
            default:                   // рывки 1.4 с + волна каждые 5 с
                _waveTimer -= input.Dt;
                if (_dashTimer <= 0.0) { Dash(ref o, toPlayer, dist); _dashTimer = FrenzyDashEvery; }
                if (_waveTimer <= 0.0) { o.Shockwave = true; _waveTimer = ShockwaveEvery; }
                break;
        }
    }

    // ---------- Спороносица: неподвижна, зоны + миньоны + корни + окно уязвимости ----------
    private void ThinkSpore(GuardianInput input, ref GuardianOutput o) {
        UpdateZones(input, ref o);
        _minionTimer -= input.Dt;
        if (_minionTimer <= 0.0) {
            o.SpawnMinions = Def.MinionsPerWave;
            o.MinionSpecies = Def.MinionSpecies;
            o.MinionMass = Def.MinionMass;
            _minionTimer = _phase == GuardianPhase.Phase3 ? Def.MinionEvery3 : Def.MinionEvery1;
        }
        if (_phase != GuardianPhase.Phase1) {
            ThinkRoots(input, ref o);
            ThinkWeakPoint(input.Dt, ref o);
        }
    }

    /// <summary>Фазы 1–2: зоны переставляются каждые ZoneShuffleEvery; фаза 3 — дрейфуют к игроку.</summary>
    private void UpdateZones(GuardianInput input, ref GuardianOutput o) {
        if (_zones.Length == 0) return;
        if (_phase == GuardianPhase.Phase3) {
            for (int i = 0; i < _zones.Length; i++) {
                Vec2 d = input.PlayerPos - _zones[i].Pos;
                double len = d.Length;
                if (len > _def.ZoneRadius * 0.5 && len > 0.01)
                    _zones[i].Pos = _zones[i].Pos + d.Scaled(_def.ZoneDriftSpeed * input.Dt / len);
                _zones[i].Life = 999.0; // живут, пока страж жив
            }
            return;
        }
        _zoneTimer -= input.Dt;
        if (_zoneTimer <= 0.0) ShuffleZones();
        for (int i = 0; i < _zones.Length; i++) _zones[i].Life = _zoneTimer;
    }

    private void ShuffleZones() {
        double reach = _def.ArenaRadius * 0.85;
        for (int i = 0; i < _zones.Length; i++) {
            _zones[i] = new GuardianZone {
                Pos = RandomDirection().Scaled(_rng.NextDouble01() * reach),
                Radius = _def.ZoneRadius,
                DamagePerSec = _def.ZoneDamagePerSec,
                Slow01 = _def.ZoneSlow,
                Life = _def.ZoneShuffleEvery,
            };
        }
        _zoneTimer = _def.ZoneShuffleEvery;
    }

    /// <summary>Фаза 2+: удар корней по площади — телеграф RootTelegraph с, потом Strike событие.</summary>
    private void ThinkRoots(GuardianInput input, ref GuardianOutput o) {
        if (_pendingT > 0.0) {
            _pendingT -= input.Dt;
            if (_pendingT <= 0.0) {
                o.Strike = true;
                o.StrikeDamageFrac = _def.RootDamageFrac;
                o.StrikePos = _pending.Pos;
                o.StrikeRadius = _pending.Radius;
                _pendingT = 0.0;
                _pending = default;
            } else {
                _pending.Seconds = _pendingT;
                o.Telegraph = _pending;
            }
            return;
        }
        _rootCd -= input.Dt;
        if (_rootCd <= 0.0) {
            _pending = new GuardianTelegraph { Pos = input.PlayerPos, Radius = _def.RootRadius, Seconds = _def.RootTelegraph };
            _pendingT = _def.RootTelegraph;
            o.Telegraph = _pending;
            _rootCd = _phase == GuardianPhase.Phase3 ? _def.RootEvery3 : _def.RootEvery2;
        }
    }

    /// <summary>Фаза 2+: цикл «WeakClosedSec закрыта → WeakOpenSec открыта»; урон ×WeakDamageMult.</summary>
    private void ThinkWeakPoint(double dt, ref GuardianOutput o) {
        if (_weakTimer < 0.0) _weakTimer = _def.WeakClosedSec; // только вошли в фазу 2
        _weakTimer -= dt;
        if (_weakOpen) {
            if (_weakTimer <= 0.0) { _weakOpen = false; _weakTimer = _def.WeakClosedSec; }
        } else if (_weakTimer <= 0.0) {
            _weakOpen = true;
            _weakTimer = _def.WeakOpenSec;
        }
        o.WeakPointOpen = _weakOpen;
    }

    private void Dash(ref GuardianOutput output, Vec2 toPlayer, double dist) {
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

    private void ClearZones() {
        for (int i = 0; i < _zones.Length; i++) _zones[i] = default;
    }
}
}
