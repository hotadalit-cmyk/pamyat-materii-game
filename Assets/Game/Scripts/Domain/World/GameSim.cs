using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Body;
using PM.Domain.Movement;
using PM.Domain.Damage;
using PM.Domain.Trail;
using PM.Domain.Absorption;
using PM.Domain.Genetics;
using PM.Domain.Metamorphosis;
using PM.Domain.Soul;
using PM.Domain.AI;

namespace PM.Domain.World {
/// <summary>
/// Оркестратор игровой симуляции. Единая точка применения намерений (игрок и ИИ — D-15),
/// фиксированный шаг экономики. Headless — для тестов; UnityDriven — физику ведёт Unity,
/// Domain аккумулирует позиции/путь/удары через Sync*/Register*.
/// </summary>
public sealed partial class GameSim {
    public readonly SimMode Mode;
    public readonly ArenaConfig Cfg = new ArenaConfig();
    public readonly TickClock Clock = new TickClock(0.2);

    public SimActor Player;
    public readonly List<SimActor> AiActors = new List<SimActor>();
    public readonly List<FoodPellet> Food = new List<FoodPellet>();
    public readonly List<BeetlePrey> Beetles = new List<BeetlePrey>();
    public readonly List<WorldEvent> Events = new List<WorldEvent>();

    public readonly TrailLedger Trail = new TrailLedger();
    public readonly CaptureProcess Capture = new CaptureProcess();
    public readonly DigestionChamber Chamber = new DigestionChamber(3.0);
    public readonly Genome Genome = new Genome();
    public readonly ComponentsStock Stock = new ComponentsStock();
    public readonly UnlockedNodes Unlocked = new UnlockedNodes();
    public readonly SoulState Soul = new SoulState();
    public GeneNode[] GeneCatalogRef = GeneCatalog.Demo();

    public OrganStats PlayerOrgans = OrganStats.BaseSlime();
    private readonly MovementConfig _moveCfg = new MovementConfig();
    private readonly DamageDebouncer _debouncer = new DamageDebouncer();
    private readonly Dictionary<int, Queue<ActorCommand>> _commands = new Dictionary<int, Queue<ActorCommand>>();
    private readonly Queue<Vec2> _playerPendingImpulses = new Queue<Vec2>();
    private readonly Dictionary<int, Queue<Vec2>> _aiPendingImpulses = new Dictionary<int, Queue<Vec2>>();
    private readonly Dictionary<int, AiProfile> _aiProfiles = new Dictionary<int, AiProfile>();
    private double _aiCadence;
    private readonly List<double> _impactStreak = new List<double>();
    private bool _firstDiscoverDone;
    private bool _dangerMarked;
    private int _nextActorId = 10;
    private int _nextPelletId = 100;
    private int _nextBeetleId = 200;
    private double _fixedAccum;

    public GameSim(SimMode mode = SimMode.Headless) {
        Mode = mode;
        Player = new SimActor {
            Id = 1, IsPlayer = true,
            Body = new BodyMassState(structure: 1.0, water: 5.0, reserve: 1.0),
            Organs = OrganStats.BaseSlime()
        };
        // базовые компоненты для обучения
        Stock.Add(2.0, 2.0, 0.0, 0.0);
    }

    // ---------- Спавн (тесты и демо-контент) ----------
    public SimActor AddAiActor(bool ally, Vec2 pos) => AddAiActorWithId(_nextActorId++, ally, pos);
    public SimActor AddAiActorWithId(int id, bool ally, Vec2 pos) {
        var a = new SimActor { Id = id, IsPlayer = false, Pos = pos,
            Body = new BodyMassState(0.8, 4.0, 0.5), Organs = OrganStats.BaseSlime() };
        AiActors.Add(a);
        _aiProfiles[a.Id] = new AiProfile { Id = ally ? "ai_ally" : "ai_rival", Ally = ally, ActorId = a.Id };
        _aiPendingImpulses[a.Id] = new Queue<Vec2>();
        if (id >= _nextActorId) _nextActorId = id + 1;
        return a;
    }
    public AiProfile ProfileOf(int actorId) => _aiProfiles.TryGetValue(actorId, out var p) ? p : null;
    public FoodPellet AddPellet(Vec2 pos, double mass) {
        var p = new FoodPellet { Id = _nextPelletId++, Pos = pos, Mass = mass };
        Food.Add(p);
        return p;
    }
    public BeetlePrey AddBeetle(Vec2 pos) {
        var b = new BeetlePrey { Id = _nextBeetleId++, Pos = pos };
        Beetles.Add(b);
        return b;
    }

    // ---------- Единый вход команд (D-15) ----------
    public void EnqueueCommand(int actorId, ActorCommand cmd) {
        if (cmd == null || cmd.Version != ActorCommand.CurrentVersion) return;
        if (!_commands.TryGetValue(actorId, out var q)) { q = new Queue<ActorCommand>(); _commands[actorId] = q; }
        q.Enqueue(cmd);
    }
    public Queue<Vec2> PlayerPendingImpulses => _playerPendingImpulses;
    public bool TryDrainAiImpulse(int actorId, out Vec2 impulse) {
        impulse = Vec2.Zero;
        if (!_aiPendingImpulses.TryGetValue(actorId, out var q) || q.Count == 0) return false;
        impulse = q.Dequeue();
        return true;
    }

    // ---------- Синхронизация с Unity-физикой (режим UnityDriven) ----------
    public void SyncPlayerKinematics(Vec2 pos, Vec2 vel, double pathLenDelta) {
        Player.Pos = pos; Player.Vel = vel; Player.PathLenAccum += MathUtil.Max(0, pathLenDelta);
    }
    public void SyncAiKinematics(int actorId, Vec2 pos, Vec2 vel, double pathLenDelta) {
        var a = FindAi(actorId);
        if (a == null) return;
        a.Pos = pos; a.Vel = vel; a.PathLenAccum += MathUtil.Max(0, pathLenDelta);
    }
    public void SyncBeetleKinematics(int beetleId, Vec2 pos) {
        var b = FindBeetle(beetleId);
        if (b != null) b.Pos = pos;
    }
    public SimActor FindAi(int id) { for (int i = 0; i < AiActors.Count; i++) if (AiActors[i].Id == id) return AiActors[i]; return null; }
    public BeetlePrey FindBeetle(int id) { for (int i = 0; i < Beetles.Count; i++) if (Beetles[i].Id == id) return Beetles[i]; return null; }
    public FoodPellet FindPellet(int id) { for (int i = 0; i < Food.Count; i++) if (Food[i].Id == id) return Food[i]; return null; }

    // ---------- Главный шаг ----------
    public void Step(double frameDelta) {
        _debouncer.BeginFrame();
        _fixedAccum += MathUtil.Max(0, frameDelta);
        while (_fixedAccum >= Cfg.FixedStep) {
            _fixedAccum -= Cfg.FixedStep;
            FixedStep(Cfg.FixedStep);
        }
    }
    private void FixedStep(double dt) {
        ExecuteCommands(dt);
        if (Mode == SimMode.Headless) IntegrateAndCollide(dt);
        WaterEconomy(dt);
        BiogravityAndFood(dt);
        UpdateCapture(dt);
        Chamber.Tick(dt);
        HarvestChamber();
        TrailEconomy(dt);
        SoulEconomy(dt);
        AiThink(dt);
        Clock.Advance(dt);
        Trail.SetTime((double)Clock.TickIndex * Clock.TickDuration);
    }
}
}
