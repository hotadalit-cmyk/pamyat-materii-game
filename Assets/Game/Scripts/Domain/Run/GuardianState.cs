using System;
using PM.Domain.Core;
namespace PM.Domain.Run {

public enum GuardianPhase { Dormant = 0, Phase1 = 1, Phase2 = 2, Phase3 = 3, Exposed = 4 }

/// <summary>Вход такта стража (v0.12): + флаг рывка героя в этом такте и его направление.</summary>
public struct GuardianInput {
    public Vec2 SelfPos;
    public Vec2 PlayerPos;
    public double PlayerMass;
    public double Dt;
    public bool PlayerDashed;     // герой совершил рывок в этом такте (Гранильщик, Ф3)
    public Vec2 PlayerDashDir;    // направление последнего рывка героя
}

/// <summary>Зона угрозы: споры, лужи, эхо. Позиции отдаёт страж, урон/замедление применяет Unity.</summary>
public struct GuardianZone {
    public Vec2 Pos;
    public double Radius;       // 0 — зоны нет
    public double DamagePerSec; // урон массы в секунду
    public double Slow01;       // 0..1 замедление
    public double Life;         // секунд до перестановки (999 — живут, пока страж жив)
}

/// <summary>Предупреждение о будущем ударе: Unity рисует кольцо, заполняющееся за Seconds.
/// Window — полная длительность окна (для заполнения); 0 — считать равной Seconds.</summary>
public struct GuardianTelegraph {
    public Vec2 Pos;
    public double Radius;
    public double Seconds;      // сколько осталось до удара; 0 — телеграфа нет
    public double Window;       // полное окно предупреждения (v0.12)
}

/// <summary>Капля Горнила (v0.12): позиции ведёт домен, Unity отображает телом и уроном по partId.</summary>
public struct GuardianPart {
    public int Id;
    public Vec2 Pos;
    public double Hp;
    public double Radius;
}

/// <summary>Луч Гранильщика (v0.12): полоса From→To; Seconds — телеграф, 0 при вспышке удара.</summary>
public struct GuardianBeam {
    public Vec2 From, To;
    public double Width;
    public double Seconds;
}

/// <summary>Выход такта стража (v0.12): + отброс, капли, кольцо, течение, притяжение,
/// блок рывка, осколки и луч. Старые поля не тронуты — Unity этапа 1 совместим.</summary>
public struct GuardianOutput {
    public bool Impulse;
    public Vec2 Direction;
    public double Strength01;
    public int SpawnMinions;
    public string MinionSpecies;
    public double MinionMass;
    public bool Shockwave;
    public GuardianZone[] Zones;      // кэш стража — читать сразу после Think, не хранить
    public GuardianTelegraph Telegraph;   // одиночный телеграф (Спороносица, Хор)
    public GuardianTelegraph[] Telegraphs; // до 3 кругов (гейзеры Горнила)
    public bool WeakPointOpen;        // окно уязвимости открыто — урон ×WeakDamageMult
    public bool ResetFight;           // игрок отступил в фазе 1 — бой сброшен (штраф: RunState)
    public bool Strike;               // телеграф дотянулся: Unity применяет StrikeDamageFrac массы героя
    public double StrikeDamageFrac;
    public Vec2 StrikePos;
    public double StrikeRadius;
    // ---------- v0.12 ----------
    public Vec2 Knockback;            // отброс героя при Strike (полный вектор скорости)
    public GuardianPart[] Parts;      // капли Горнила (null — деления нет)
    public Vec2 Center;               // центр арены боя при делении (0 — брать позицию тела)
    public double ArenaRadiusNow;     // текущий радиус кипящего кольца (0 — кольца нет)
    public bool RingBurn;             // герой вне кольца — теряет RingDamagePerSec массы/с
    public Vec2 Current;              // течение Хора: вектор ускорения
    public double Pull;               // воронка Хора: ускорение к стражу
    public double DashLockSeconds;    // сколько герою нельзя делать рывок
    public Vec2[] Shards;             // осколки Гранильщика на орбите (null — их нет)
    public GuardianBeam Beam;         // луч Гранильщика (Seconds &gt; 0 — телеграф)
    public bool BeamFlash;            // кадр удара луча — вспышка
}

/// <summary>
/// Страж глубины (v0.12): общий автомат пяти боссов каталога. Спит, пока игрок дальше WakeRadius;
/// во сне таймеры стоят. Обнажён (&lt;30% HP) — не атакует, медленно уходит, поглощается касанием.
/// Пульсар — поведение BossState 1:1; Спороносица — зоны, корни, окно уязвимости;
/// Горнило — гейзеры, деление на капли, кипящее кольцо; Хор Бездны — ток, оглушение, воронка;
/// Гранильщик — осколки-блок, лучи, зеркало-контрудар.
/// Без UnityEngine, без LINQ, детерминирован по seed.
/// </summary>
public sealed class GuardianState {
    public const double ExposedBelowFrac = 0.30; // ниже 30% стартового HP — обнажён
    public const double RetreatMult = 1.5;       // сброс боя дальше арены × 1.5 (от поверхностей тел)
    private const double ArmorSpeed = 6.0;       // удары медленнее — не пробивают броню
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
    private double _scale;                       // v0.14: радиус тела стража = 0.35×√maxHp
    private GuardianPhase _phase;
    private double _dashTimer, _minionTimer, _waveTimer, _fleeTimer;
    private double _zoneTimer, _weakTimer, _rootCd, _pendingT;
    private GuardianTelegraph _pending;
    private bool _weakOpen, _fightActive;
    // ---------- v0.12: Горнило ----------
    private sealed class PartRun { public int Id; public Vec2 Pos; public double Hp, Radius; }
    private bool _divided;                       // бой идёт каплями
    private PartRun[] _parts;                    // null до деления
    private readonly GuardianPart[] _partsOut = new GuardianPart[3];
    private readonly double[] _pairT = new double[3]; // непрерывная близость пар (0,1)(0,2)(1,2)
    private int _nextPartId = 1;
    private readonly GuardianTelegraph[] _geysers;   // волна гейзеров (кэш выхода Telegraphs)
    private double _geyserCd, _geyserPendingT;
    private bool _ringActive; private double _ringFrac = 1.0, _ringTimer;
    private Vec2 _center;                        // центр арены при делении (центроид капель)
    // ---------- v0.12: Хор Бездны ----------
    private Vec2 _currentDir; private double _currentTimer;
    private GuardianTelegraph _pulse;
    private double _pulseCd, _pulsePendingT, _dashLock;
    // ---------- v0.12: Гранильщик ----------
    private readonly Vec2[] _shardOut;           // позиции осколков на орбите (выход)
    private readonly Vec2[] _shardWorld;         // кэш для проверки блока в ApplyHit
    private bool _shardsActive, _crabsOut;
    private double _orbitT, _beamCd, _beamPendingT, _beamLen;
    private Vec2 _beamDir;
    private GuardianBeam _beamPending;
    private Vec2 _counterDir; private double _counterT;
    private Vec2 _lastSelfPos;                   // кэш позиции для ApplyHit с позицией героя

    public GuardianDef Def => _def;
    public double Hp => _hp;
    public double MaxHp => _maxHp;
    /// <summary>v0.14: радиус тела стража — от него считаются все радиусы боя.</summary>
    public double Scale => _scale;
    public double BodyRadius => _scale;
    /// <summary>v0.14: масштабированные радиусы боя (доли тела + запас).</summary>
    public double ArenaRadius => _scale * _def.ArenaMult + GuardianDef.ArenaPad;
    public double WakeRadius => _scale * _def.WakeMult + GuardianDef.WakePad;
    public double ZoneRadius => _scale * _def.ZoneMult;
    public double RootRadius => _scale * _def.RootMult;
    public double GeyserRadius => _scale * _def.GeyserMult;
    public double PulseRadius => _scale * _def.PulseMult;
    public double ShardRadius => _scale * _def.ShardRadiusFrac;
    public double ShardOrbitRadius => _scale * (1.0 + _def.ShardOrbitFrac);
    public double BeamWidth => _scale * _def.BeamWidthFrac;
    public double MinionMass => _scale * _scale * _def.MinionMassFrac;
    /// <summary>Базовая HP текущего боя (от неё считаются фазы и обнажение).</summary>
    public double StartHp => _startHp;
    public GuardianPhase Phase => _phase;
    public bool IsExposed => _hp < _startHp * ExposedBelowFrac;
    public bool WeakPointOpen => _weakOpen;
    /// <summary>v0.12: последний удар героя заблокирован осколком Гранильщика (0 урона, отскок, −5% массы — Unity).</summary>
    public bool LastBlock { get; private set; }
    public Vec2 LastBlockBounce { get; private set; }

    public GuardianState(GuardianDef def, double maxHp, ulong seed, bool allowRetreat = true) {
        _def = def;
        _rng = new PmRandom(seed);
        _allowRetreat = allowRetreat;
        maxHp = maxHp > 0.0 ? maxHp : 1.0;
        _hp = _maxHp = _startHp = maxHp;
        _scale = SizeRule.RadiusForMass(_maxHp);
        _phase = GuardianPhase.Dormant;
        _zones = new GuardianZone[def != null ? def.ZoneCount : 0];
        _zoneTimer = 0.0; _weakTimer = -1.0; _rootCd = 0.0; _pendingT = 0.0;
        _geysers = new GuardianTelegraph[def != null && def.GeyserCount > 0 ? def.GeyserCount : 0];
        int shards = def != null && def.ShardCount > 0 ? def.ShardCount : 0;
        _shardOut = new Vec2[shards];
        _shardWorld = new Vec2[shards];
    }

    /// <summary>Восстановление после сброса боя: HP = maxHp, таймеры, зоны, капли и окно уязвимости сброшены.</summary>
    public void Restore(double maxHp) {
        _maxHp = maxHp > 0.0 ? maxHp : 1.0;
        _hp = _maxHp;
        _startHp = _maxHp;
        _scale = SizeRule.RadiusForMass(_maxHp); // v0.14: тело и радиусы — от актуального максимума
        _phase = GuardianPhase.Phase1;
        _dashTimer = _minionTimer = _waveTimer = _fleeTimer = 0.0;
        _zoneTimer = 0.0; _weakTimer = -1.0; _weakOpen = false; _rootCd = 0.0;
        _pendingT = 0.0; _pending = default;
        ClearZones();
        _fightActive = false;
        // v0.12: сброс механик этапа 2
        _divided = false; _parts = null; _nextPartId = 1; _center = Vec2.Zero;
        for (int i = 0; i < 3; i++) { _partsOut[i] = default; _pairT[i] = 0.0; }
        _geyserCd = 0.0; _geyserPendingT = 0.0;
        for (int i = 0; i < _geysers.Length; i++) _geysers[i] = default;
        _ringActive = false; _ringFrac = 1.0; _ringTimer = 0.0;
        _currentDir = Vec2.Zero; _currentTimer = 0.0;
        _pulse = default; _pulseCd = 0.0; _pulsePendingT = 0.0; _dashLock = 0.0;
        _orbitT = 0.0; _shardsActive = false; _crabsOut = false;
        for (int i = 0; i < _shardOut.Length; i++) { _shardOut[i] = Vec2.Zero; _shardWorld[i] = Vec2.Zero; }
        _beamCd = 0.0; _beamPendingT = 0.0; _beamPending = default; _beamDir = Vec2.Zero;
        _counterDir = Vec2.Zero; _counterT = 0.0;
    }

    /// <summary>Такт стража. В Dormant — никаких действий, таймеры стоят.
    /// v0.14: дистанции до героя — от поверхностей тел (dist − rSelf − rPlayer).</summary>
    public GuardianOutput Think(GuardianInput input) {
        var o = new GuardianOutput { Zones = _zones };
        Vec2 toPlayer = input.PlayerPos - input.SelfPos;
        double dist = toPlayer.Length;
        double surf = SurfaceDist(dist, input.PlayerMass); // расстояние между поверхностями тел

        if (IsExposed) { // обнажён: не атакует, медленно уходит
            _phase = GuardianPhase.Exposed;
            _shardsActive = false; // осколки Гранильщика при обнажении не блокируют
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
            if (_divided) { FillPartsOut(); o.Parts = _partsOut; o.Center = _center; } // капли видны до конца
            return o;
        }
        // v0.11: окно отступления — пока фаза 1, выход из арены сбрасывает бой
        // (проверка до радиуса сна: арена × 1.5 всегда больше WakeRadius).
        if (_allowRetreat && _fightActive && _phase == GuardianPhase.Phase1
            && surf > ArenaRadius * RetreatMult) {
            Restore(_maxHp); // штраф герою (−25% массы, счёт поражений) — RunState.OnGuardianFightReset
            o.ResetFight = true;
            return o;
        }
        if (surf > WakeRadius) { // спит, пока игрок далеко (от поверхностей тел); таймеры стоят
            _phase = GuardianPhase.Dormant;
            return o;
        }
        if (surf <= ArenaRadius) _fightActive = true;

        _phase = ComputePhase();
        _lastSelfPos = input.SelfPos; // кэш для проверок в ApplyHit (осколки, деление)
        switch (Def.Kind) {
            case GuardianKind.SporeMother: ThinkSpore(input, ref o); break;
            case GuardianKind.Forge: ThinkForge(input, ref o); break;
            case GuardianKind.AbyssChoir: ThinkChoir(input, ref o); break;
            case GuardianKind.Grinder: ThinkGrinder(input, ref o); break;
            default: ThinkPulsar(input, toPlayer, dist, ref o); break;
        }
        return o;
    }

    /// <summary>
    /// Удар игрока по стражу. Пульсар — формула старого BossState 1:1 (потеря
    /// 0.35×relSpeed×playerMass/(playerMass+Hp)×модификаторы). Спороносица: закрытая
    /// шляпка ×ClosedCapMult, открытое окно уязвимости ×WeakDamageMult. Хор (Ф2+): вне окна ядра
    /// ×0.5, в окне ×1.5. Горнило в делении: удар уходит первой живой капле.
    /// </summary>
    public double ApplyHit(double relSpeed, double playerMass, MutationSet playerMut, int ricochets) {
        LastBlock = false; LastBlockBounce = Vec2.Zero;
        if (_divided) { // старый путь: каплям бьём первую живую (Unity этапа 2 бьёт по partId)
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i].Hp > 0.0) return ApplyHitPart(relSpeed, playerMass, playerMut, ricochets, _parts[i]);
        }
        if (relSpeed < ArmorSpeed) return 0.0;
        double mult = 1.0;
        if (Def.Kind == GuardianKind.SporeMother) mult = _weakOpen ? Def.WeakDamageMult : Def.ClosedCapMult;
        else if (Def.Kind == GuardianKind.AbyssChoir && CoreCapped())
            mult = _weakOpen ? Def.WeakDamageMult : Def.ClosedCapMult;
        double dealt = playerMut != null ? playerMut.DamageDealtMult : 1.0;
        double ricochet = playerMut != null ? playerMut.RicochetBonus : 0.0;
        double share = _hp > 0.0 ? playerMass / (playerMass + _hp) : 1.0;
        if (share < 0.0) share = 0.0;
        double sf = relSpeed < SizeRule.NominalSpeed ? relSpeed / SizeRule.NominalSpeed : 1.0;
        // v0.14 (Docs/BALANCE_v14.md §3.2): урон от массы героя, скорости и √доли — TTK стабилен на любом весе
        double loss = SizeRule.CreatureHitK * playerMass * relSpeed * System.Math.Sqrt(share) * sf
                      * dealt * (1.0 + ricochet * ricochets) * mult;
        if (loss <= 0.0) return 0.0;
        if (loss > _hp) loss = _hp;
        _hp -= loss;
        _phase = IsExposed ? GuardianPhase.Exposed : ComputePhase(); // фаза не отстаёт от HP
        return loss;
    }

    /// <summary>v0.12: удар по конкретной капле Горнила (partId из Output.Parts).</summary>
    public double ApplyHit(double relSpeed, double playerMass, MutationSet playerMut, int ricochets, int partId) {
        LastBlock = false; LastBlockBounce = Vec2.Zero;
        if (!_divided) return ApplyHit(relSpeed, playerMass, playerMut, ricochets);
        PartRun target = null;
        for (int i = 0; i < _parts.Length; i++)
            if (_parts[i].Hp > 0.0 && _parts[i].Id == partId) { target = _parts[i]; break; }
        if (target == null) { // капли с таким id нет — первая живая
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i].Hp > 0.0) { target = _parts[i]; break; }
        }
        if (target == null) return ApplyHit(relSpeed, playerMass, playerMut, ricochets);
        return ApplyHitPart(relSpeed, playerMass, playerMut, ricochets, target);
    }

    /// <summary>v0.12: удар по Гранильщику с учётом позиции героя — линия герой→страж через осколок блокирует.</summary>
    public double ApplyHit(double relSpeed, double playerMass, MutationSet playerMut, int ricochets, Vec2 playerPos) {
        LastBlock = false; LastBlockBounce = Vec2.Zero;
        if (_shardsActive) {
            for (int i = 0; i < _shardWorld.Length; i++) {
                if (DistPointSegment(playerPos, _lastSelfPos, _shardWorld[i]) <= ShardRadius) {
                    LastBlock = true; // урона нет; Unity отражает героя и снимает BlockMassFrac массы
                    Vec2 away = playerPos - _shardWorld[i];
                    LastBlockBounce = away.Length > 1e-6 ? away.Normalized() : RandomDirection();
                    return 0.0;
                }
            }
        }
        return ApplyHit(relSpeed, playerMass, playerMut, ricochets);
    }

    private bool CoreCapped() => _phase == GuardianPhase.Phase2 || _phase == GuardianPhase.Phase3;

    private double ApplyHitPart(double relSpeed, double playerMass, MutationSet playerMut, int ricochets, PartRun target) {
        if (relSpeed < ArmorSpeed) return 0.0;
        double dealt = playerMut != null ? playerMut.DamageDealtMult : 1.0;
        double ricochet = playerMut != null ? playerMut.RicochetBonus : 0.0;
        double share = target.Hp > 0.0 ? playerMass / (playerMass + target.Hp) : 1.0;
        if (share < 0.0) share = 0.0;
        double sf = relSpeed < SizeRule.NominalSpeed ? relSpeed / SizeRule.NominalSpeed : 1.0;
        double loss = SizeRule.CreatureHitK * playerMass * relSpeed * System.Math.Sqrt(share) * sf
                      * dealt * (1.0 + ricochet * ricochets); // у капель нет брони/шляпки
        if (loss <= 0.0) return 0.0;
        if (loss > target.Hp) loss = target.Hp;
        target.Hp -= loss;
        _hp -= loss; // инвариант: _hp — сумма живых капель
        _phase = IsExposed ? GuardianPhase.Exposed : ComputePhase();
        return loss;
    }

    /// <summary>v0.14: расстояние между поверхностями тел стража и героя.</summary>
    private double SurfaceDist(double centerDist, double playerMass) {
        double rSelf = SizeRule.RadiusForMass(System.Math.Max(_hp, 0.05));
        double rPlayer = SizeRule.RadiusForMass(System.Math.Max(playerMass, 0.05));
        double surf = centerDist - rSelf - rPlayer;
        return surf > 0.0 ? surf : 0.0;
    }

    /// <summary>Расстояние от точки c до отрезка ab (блок осколком: линия герой→страж).</summary>
    private static double DistPointSegment(Vec2 a, Vec2 b, Vec2 c) {
        Vec2 ab = b - a;
        double len2 = ab.SqrLength;
        if (len2 < 1e-12) return (c - a).Length;
        double t = Vec2.Dot(c - a, ab) / len2;
        if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
        return (c - a - ab.Scaled(t)).Length;
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
                    o.MinionMass = MinionMass; // v0.14: от радиуса тела
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
            o.MinionMass = MinionMass; // v0.14: от радиуса тела
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
            double drift = _scale * _def.ZoneDriftFrac; // v0.14: доля радиуса тела
            for (int i = 0; i < _zones.Length; i++) {
                Vec2 d = input.PlayerPos - _zones[i].Pos;
                double len = d.Length;
                if (len > ZoneRadius * 0.5 && len > 0.01)
                    _zones[i].Pos = _zones[i].Pos + d.Scaled(drift * input.Dt / len);
                _zones[i].Life = 999.0; // живут, пока страж жив
            }
            return;
        }
        _zoneTimer -= input.Dt;
        if (_zoneTimer <= 0.0) ShuffleZones();
        for (int i = 0; i < _zones.Length; i++) _zones[i].Life = _zoneTimer;
    }

    private void ShuffleZones() {
        double reach = ArenaRadius * 0.85;
        for (int i = 0; i < _zones.Length; i++) {
            _zones[i] = new GuardianZone {
                // v0.14: зоны строились вокруг (0,0) карты — теперь вокруг текущей позиции стража
                Pos = _lastSelfPos + RandomDirection().Scaled(_rng.NextDouble01() * reach),
                Radius = ZoneRadius,
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
            _pending = new GuardianTelegraph { Pos = input.PlayerPos, Radius = RootRadius, Seconds = _def.RootTelegraph };
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

    // ---------- Горнило (Г2): гейзеры, деление на капли, кипящее кольцо ----------

    private void ThinkForge(GuardianInput input, ref GuardianOutput o) {
        ForgeDivide();                 // деление — по факту HP (ApplyHit мог сработать между тактами)
        UpdateParts(input, ref o);     // капли ползут, сливаются; одна капля = страж, Ф3
        ThinkGeysers(input, ref o);
        UpdateRing(input, ref o);      // кипение в Ф3 (в том числе после слияния до одной капли)
    }

    private void ForgeDivide() {
        if (!_divided && _hp <= _startHp * Def.PhaseThreshold2 && _hp > 0.0) {
            _divided = true;
            _parts = new PartRun[3];
            double share = _hp / 3.0;
            double baseAngle = _rng.NextDouble01() * 2.0 * System.Math.PI;
            double sep = _scale * 0.9; // v0.14: разбег капель — доля тела
            for (int i = 0; i < _parts.Length; i++) {
                double a = baseAngle + i * 2.0 * System.Math.PI / 3.0;
                _parts[i] = new PartRun {
                    Id = _nextPartId++,
                    Pos = _lastSelfPos + new Vec2(System.Math.Cos(a), System.Math.Sin(a)) * sep,
                    Hp = share,
                    Radius = PartRadius(share),
                };
            }
            _center = _lastSelfPos;
        }
    }

    private double PartRadius(double hp) => _scale * 0.55 + 0.03 * hp; // v0.14: от радиуса тела

    private void UpdateParts(GuardianInput input, ref GuardianOutput o) {
        if (!_divided) return;
        // капли медленно ползут к герою (шаг ограничен дистанцией — без перескока на больших тактах)
        for (int i = 0; i < _parts.Length; i++) {
            var p = _parts[i];
            if (p.Hp <= 0.0) continue;
            Vec2 d = input.PlayerPos - p.Pos;
            double len = d.Length;
            if (len <= 0.05) continue;
            double step = _scale * Def.PartCrawlFrac * input.Dt; // v0.14: доля тела
            if (step > len) step = len;
            p.Pos += d.Scaled(step / len);
        }
        // слияние: пара ближе rA+rB+запас непрерывно PartMergeEvery секунд
        double gap = _scale * 0.15; // v0.14: запас — доля тела
        for (int i = 0; i < _parts.Length; i++) {
            var a = _parts[i];
            if (a.Hp <= 0.0) continue;
            for (int j = i + 1; j < _parts.Length; j++) {
                var b = _parts[j];
                if (b.Hp <= 0.0) continue;
                int pi = PairIndex(i, j);
                double dist = (a.Pos - b.Pos).Length;
                if (dist <= a.Radius + b.Radius + gap && input.Dt > 0.0) _pairT[pi] += input.Dt;
                else _pairT[pi] = 0.0;
                if (_pairT[pi] >= Def.PartMergeEvery) {
                    double bonus = Def.PartMergeBonus * System.Math.Min(a.Hp, b.Hp);
                    a.Hp += b.Hp + bonus; // HP суммируется +30% от меньшей
                    _hp += bonus;
                    a.Radius = PartRadius(a.Hp);
                    b.Hp = 0.0; b.Radius = 0.0;
                    _pairT[0] = _pairT[1] = _pairT[2] = 0.0;
                }
            }
        }
        int alive = FillPartsOut();
        if (alive == 1 && _phase < GuardianPhase.Phase3) _phase = GuardianPhase.Phase3; // осталась одна — она и есть страж
        o.Parts = _partsOut;
        o.Center = _center;
    }

    /// <summary>Заполняет кэш выхода каплями; возвращает число живых.</summary>
    private int FillPartsOut() {
        int alive = 0;
        double sx = 0.0, sy = 0.0;
        for (int i = 0; i < _parts.Length; i++) {
            var p = _parts[i];
            if (p.Hp <= 0.0) { _partsOut[i] = default; continue; }
            _partsOut[i] = new GuardianPart { Id = p.Id, Pos = p.Pos, Hp = p.Hp, Radius = p.Radius };
            sx += p.Pos.X; sy += p.Pos.Y;
            alive++;
        }
        if (alive > 0) _center = new Vec2(sx / alive, sy / alive); // центроид живых капель
        return alive;
    }

    private static int PairIndex(int i, int j) => i == 0 ? j - 1 : 2; // (0,1)(0,2)(1,2)

    private void ThinkGeysers(GuardianInput input, ref GuardianOutput o) {
        int n = _geysers.Length;
        if (n == 0) return;
        if (_geyserPendingT > 0.0) {
            _geyserPendingT -= input.Dt;
            if (_geyserPendingT <= 0.0) {
                _geyserPendingT = 0.0;
                // круги держат позиции на момент создания: ушел — не попал; кольца не лгут
                for (int i = 0; i < n; i++) {
                    if ((input.PlayerPos - _geysers[i].Pos).Length <= GeyserRadius) {
                        o.Strike = true;
                        o.StrikeDamageFrac = Def.GeyserDamageFrac;
                        o.StrikePos = _geysers[i].Pos;
                        o.StrikeRadius = GeyserRadius;
                        Vec2 away = input.PlayerPos - _geysers[i].Pos;
                        o.Knockback = away.Length > 1e-6
                            ? away.Normalized().Scaled(Def.GeyserKnockback)
                            : RandomDirection().Scaled(Def.GeyserKnockback);
                        break; // удар один, даже если кругов рядом два
                    }
                }
                for (int i = 0; i < n; i++) _geysers[i].Seconds = 0.0;
            } else {
                for (int i = 0; i < n; i++) { _geysers[i].Seconds = _geyserPendingT; _geysers[i].Window = Def.GeyserTelegraph; }
            }
            o.Telegraphs = _geysers;
            return;
        }
        _geyserCd -= input.Dt;
        if (_geyserCd <= 0.0) {
            for (int i = 0; i < n; i++) {
                Vec2 pos = i == 0 ? input.PlayerPos
                    : input.PlayerPos + RandomDirection().Scaled(_scale * 1.2 + _rng.NextDouble01() * _scale * 1.3);
                _geysers[i] = new GuardianTelegraph {
                    Pos = pos, Radius = GeyserRadius,
                    Seconds = Def.GeyserTelegraph, Window = Def.GeyserTelegraph,
                };
            }
            _geyserPendingT = Def.GeyserTelegraph;
            // «каждые N с» — от удара до удара: вычитаем время телеграфа
            double every = _phase == GuardianPhase.Phase3 ? Def.GeyserEvery3 : Def.GeyserEvery1;
            _geyserCd = System.Math.Max(0.1, every - Def.GeyserTelegraph);
        }
        o.Telegraphs = _geysers; // Seconds = 0 — круги погашены
    }

    private void UpdateRing(GuardianInput input, ref GuardianOutput o) {
        if (!_ringActive && _phase == GuardianPhase.Phase3) {
            _ringActive = true;
            _ringFrac = 1.0;
            _ringTimer = Def.RingShrinkEvery;
        }
        if (!_ringActive) { o.ArenaRadiusNow = 0.0; o.RingBurn = false; return; }
        if (_phase == GuardianPhase.Phase3) {
            _ringTimer -= input.Dt;
            if (_ringTimer <= 0.0) {
                _ringFrac = System.Math.Max(Def.RingMinFrac, _ringFrac - Def.RingShrinkStep);
                _ringTimer += Def.RingShrinkEvery;
            }
        }
        o.ArenaRadiusNow = ArenaRadius * _ringFrac;
        Vec2 center = _center.Length > 1e-9 ? _center : _lastSelfPos;
        o.RingBurn = (input.PlayerPos - center).Length > o.ArenaRadiusNow; // вне кольца — доля массы/с (Unity)
    }

    // ---------- Хор Бездны (Г3): ток, оглушение, воронка ----------

    private void ThinkChoir(GuardianInput input, ref GuardianOutput o) {
        // блокировка рывка: считаем сверху, чтобы такт удара показывал полные 3 с
        if (_dashLock > 0.0) { _dashLock -= input.Dt; if (_dashLock < 0.0) _dashLock = 0.0; }
        // течение арены — во всех фазах, направление каждые 6 с
        _currentTimer -= input.Dt;
        if (_currentTimer <= 0.0 || _currentDir.Length < 0.5) {
            _currentDir = RandomDirection();
            _currentTimer = Def.CurrentChangeEvery;
        }
        o.Current = _currentDir.Scaled(Def.CurrentForce);
        o.Pull = _phase == GuardianPhase.Phase3 ? Def.PullForce : 0.0; // воронка
        // синхронный импульс: кольцо вокруг стража, телеграф, урон внутри
        if (_pulsePendingT > 0.0) {
            _pulsePendingT -= input.Dt;
            if (_pulsePendingT <= 0.0) {
                _pulsePendingT = 0.0;
                if ((input.PlayerPos - _pulse.Pos).Length <= PulseRadius) { // внутри — удар
                    o.Strike = true;
                    o.StrikeDamageFrac = Def.PulseDamageFrac;
                    o.StrikePos = _pulse.Pos;
                    o.StrikeRadius = PulseRadius;
                }
                if (CoreCapped()) { // Ф2+: импульс оглушает и открывает ядро
                    _dashLock = Def.DashLockSec;
                    _weakOpen = true;
                    _weakTimer = Def.CoreOpenSec;
                }
                _pulse.Seconds = 0.0;
            } else {
                _pulse.Seconds = _pulsePendingT;
            }
            o.Telegraph = _pulse;
        } else {
            _pulseCd -= input.Dt;
            if (_pulseCd <= 0.0) {
                _pulse = new GuardianTelegraph {
                    Pos = _lastSelfPos, Radius = PulseRadius,
                    Seconds = Def.PulseTelegraph, Window = Def.PulseTelegraph,
                };
                _pulsePendingT = Def.PulseTelegraph;
                // «каждые N с» — от удара до удара: вычитаем время телеграфа
                double every = _phase == GuardianPhase.Phase3 ? Def.PulseEvery3 : Def.PulseEvery1;
                _pulseCd = System.Math.Max(0.1, every - Def.PulseTelegraph);
                o.Telegraph = _pulse;
            }
        }
        o.DashLockSeconds = _dashLock;
        // ядро открыто CoreOpenSec после импульса (Ф2+); вне окна урон ×0.5 — ApplyHit
        if (_weakOpen) {
            _weakTimer -= input.Dt;
            if (_weakTimer <= 0.0) { _weakOpen = false; _weakTimer = -1.0; }
        }
        o.WeakPointOpen = _weakOpen;
    }

    // ---------- Гранильщик (Г4): осколки, луч, зеркало ----------

    private void ThinkGrinder(GuardianInput input, ref GuardianOutput o) {
        GrinderCrabs(ref o);          // вход в Ф2: осколки сходят с орбиты → 4 shard_crab
        UpdateShards(input, ref o);
        ThinkBeam(input, ref o);
        ThinkCounter(input, ref o);   // зеркало Ф3
    }

    private void GrinderCrabs(ref GuardianOutput o) {
        if (_crabsOut || _phase == GuardianPhase.Phase1) return;
        _crabsOut = true;             // однократно
        o.SpawnMinions = Def.MinionsPerWave;
        o.MinionSpecies = Def.MinionSpecies;
        o.MinionMass = MinionMass;    // v0.14: от радиуса тела
    }

    private void UpdateShards(GuardianInput input, ref GuardianOutput o) {
        if (_phase != GuardianPhase.Phase1) { _shardsActive = false; o.Shards = null; return; }
        _orbitT += input.Dt;
        double orbit = ShardOrbitRadius; // v0.14: доля радиуса тела
        for (int i = 0; i < _shardOut.Length; i++) {
            double a = _orbitT * 2.0 * System.Math.PI / Def.ShardOrbitPeriod
                + i * 2.0 * System.Math.PI / _shardOut.Length;
            Vec2 pos = _lastSelfPos + new Vec2(System.Math.Cos(a), System.Math.Sin(a)) * orbit;
            _shardOut[i] = pos;
            _shardWorld[i] = pos;
        }
        _shardsActive = true;
        o.Shards = _shardOut;
    }

    private void ThinkBeam(GuardianInput input, ref GuardianOutput o) {
        if (_beamPendingT > 0.0) {
            _beamPendingT -= input.Dt;
            if (_beamPendingT <= 0.0) {
                _beamPendingT = 0.0;
                o.Beam = _beamPending;
                o.Beam.Seconds = 0.0;
                o.BeamFlash = true; // вспышка удара
                Vec2 rel = input.PlayerPos - _beamPending.From;
                double proj = Vec2.Dot(rel, _beamDir);
                double perp = (rel - _beamDir.Scaled(proj)).Length;
                if (proj >= 0.0 && proj <= _beamLen && perp <= BeamWidth * 0.5) {
                    o.Strike = true; // кольца не лгут: герой в коридоре луча
                    o.StrikeDamageFrac = Def.BeamDamageFrac;
                    o.StrikePos = input.PlayerPos;
                    o.StrikeRadius = BeamWidth;
                }
                _beamPending = default;
                return;
            }
            _beamPending.Seconds = _beamPendingT;
            o.Beam = _beamPending;
            return;
        }
        _beamCd -= input.Dt;
        if (_beamCd <= 0.0) {
            Vec2 d = input.PlayerPos - _lastSelfPos;
            _beamDir = d.Length > 1e-6 ? d.Normalized() : RandomDirection();
            if (_beamDir.Length < 0.5) _beamDir = RandomDirection();
            _beamLen = ArenaRadius * Def.BeamLengthMult;
            _beamPending = new GuardianBeam {
                From = _lastSelfPos,
                To = _lastSelfPos + _beamDir.Scaled(_beamLen),
                Width = BeamWidth,
                Seconds = Def.BeamTelegraph,
            };
            _beamPendingT = Def.BeamTelegraph;
            _beamCd = Def.BeamEvery;
            o.Beam = _beamPending;
        }
    }

    private void ThinkCounter(GuardianInput input, ref GuardianOutput o) {
        if (_phase != GuardianPhase.Phase3) { _counterT = 0.0; return; }
        if (input.PlayerDashed && input.PlayerDashDir.Length > 0.01) {
            _counterDir = input.PlayerDashDir.Normalized(); // запомнить последний рывок
            if (_counterDir.Length < 0.5) _counterDir = RandomDirection();
            _counterT = Def.CounterDelay;
            return;
        }
        if (_counterT <= 0.0) return;
        _counterT -= input.Dt;
        if (_counterT <= 0.0) {
            _counterT = 0.0;
            o.Impulse = true;             // контрудар тем же направлением, что рывок героя
            o.Direction = _counterDir;
            o.Strength01 = 1.0;
        }
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
