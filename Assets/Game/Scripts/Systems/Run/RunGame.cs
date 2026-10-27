using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using PM.Domain.Core;
using PM.Domain.Run;
using PM.Systems.Bootstrap;
using PM.Systems.CameraRig;
using PM.Systems.Gameplay;
using PM.Systems.Input;

namespace PM.Systems.Run {
/// <summary>
/// Забег «одна жизнь — один забег» (Docs/CONCEPT_v4.md, v0.5: биомы, препятствия, страж).
/// Правила — в Domain.Run; здесь физика Unity, вид, ввод и «сочность».
/// Сцена строится целиком в рантайме: ничего не запекается, слушатели не теряются.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed partial class RunGame : MonoBehaviour {
    public static RunGame Instance { get; private set; }
    private const string SoulPrefKey = "pm_soul_v4";
    private const float DashSpeed = 10f;
    private const float CreatureSpeed = 6f;
    private const float ExitRadius = 0.9f;
    private const float WallThickness = 0.6f;
    private const float PlayerBaseDamping = 0.9f;

    public SoulMeta Meta { get; private set; }
    public RunState Run { get; private set; }
    public bool InRun => Run != null && !_ended;
    public bool UiModalOpen => _ui != null && _ui.ModalOpen;
    // для обучения: где капля, где нужные кнопки, открыта ли эволюция
    public Vector2 PlayerPosition => _player != null ? (Vector2)_player.transform.position : Vector2.zero;
    public bool UiEvolutionOpen => _ui != null && _ui.Evo != null && _ui.Evo.EvolutionOpen;
    public RectTransform UiEvolutionButton => _ui != null && _ui.Evo != null ? _ui.Evo.EvoButtonRt : null;
    public RectTransform UiDashButton => _ui != null ? _ui.DashRt : null;
    public RectTransform UiSplitButton => _ui != null ? _ui.SplitRt : null;
    public RectTransform UiGatherButton => _ui != null ? _ui.GatherRt : null;

    /// <summary>Ближайшая к капле цель для стрелки обучения: спора (food) или существо, которое можно съесть.</summary>
    public Vector3? TutorialTarget(bool food) {
        if (_player == null || Run == null) return null;
        Vector2 p = _player.Rb.position;
        double m = Run.Fragments.Count > 0 ? Run.Fragments[0].Mass : Run.Mass;
        RunCreature best = null; float bd = 14f;
        for (int i = 0; i < _creatures.Count; i++) {
            var c = _creatures[i];
            if (c == null || c.IsBoss || !c.gameObject.activeSelf || c.IsFood != food) continue;
            if (!food && m < c.Mass * Run.Mutations.AbsorbRatio) continue;
            float d = Vector2.Distance(p, c.View.Rb.position);
            if (d < bd) { bd = d; best = c; }
        }
        return best != null ? best.transform.position : (Vector3?)null;
    }

    private SimEntityView2D _player;
    private PhysicsMaterial2D _playerMat;
    private Transform _roomRoot;
    private readonly List<RunCreature> _creatures = new List<RunCreature>();
    private readonly List<RunObstacle> _obstacles = new List<RunObstacle>();
    private SpriteRenderer _exitRing, _exitGlow, _exitPointer, _altarRing;
    private RunCreature _boss;
    private SpriteRenderer _shockRing;
    // v0.11: страж глубины у шахты (открытый мир)
    private RunCreature _guardian;
    private readonly SpriteRenderer[] _guardianZones = new SpriteRenderer[4];
    private SpriteRenderer _guardianTelegraph, _guardianWeakRing;
    private bool _guardianBannerShown;
    private float _guardianZoneSlow;
    // v0.12: механики этапа 2
    private readonly RunCreature[] _guardianParts = new RunCreature[3];   // капли Горнила
    private readonly int[] _guardianPartIds = new int[3];
    private readonly SpriteRenderer[] _guardianTelegraphs = new SpriteRenderer[3]; // круги гейзеров
    private SpriteRenderer _guardianRing, _guardianBeam;                  // кипящее кольцо, луч
    private readonly SpriteRenderer[] _guardianShards = new SpriteRenderer[4];     // осколки Гранильщика
    private readonly SpriteRenderer[] _currentDots = new SpriteRenderer[8];        // частицы течения Хора
    private float _guardianDashLock;                                            // запрет рывка (Хор)
    private bool _playerDashed; private Vec2 _playerDashDir;             // рывок в этом такте (Гранильщик)
    private float _shockT = -1f;
    private DarknessMask _dark;
    private RunUi _ui;
    private PmRandom _rng;
    private ulong _seed;
    private bool _ended = true;
    private bool _offerShown;
    private string _lastHitSource;
    private int _builtRoomKey = -1;
    private int _lastDepthShown;
    private float _time;
    private int _ricochets;            // отскоков в текущем рывке (m_ricochet)
    private float _sinceDash = 99f;
    // v0.6 открытый мир: ленивое появление существ рядом с игроком, усыпление дальних, туман войны
    private const float SpawnRadius = 24f, SleepRadius = 30f, WakeRadius = 25f, RevealRadius = 9f;
    private WorldView _world;
    public WorldView World => _world;
    private readonly List<SpawnSpec> _pending = new List<SpawnSpec>();
    private readonly List<int> _pendingIndex = new List<int>();
    private readonly List<SpriteRenderer> _altarRings = new List<SpriteRenderer>();
    private readonly HashSet<int> _visitedRegions = new HashSet<int>();
    private float _streamTimer, _miniTimer;

    void Awake() {
        Instance = this;
        Application.targetFrameRate = 60;
        Meta = LoadMeta();
        SetupCameraAndEvents();
        var input = new GameObject("pull_input");
        input.transform.SetParent(transform, false);
        input.AddComponent<PullGestureInput>();
        PullGestureInput.ReleaseHandler = OnDashRelease;
        GameSettings.Apply(); // громкость, FPS, чувствительность, камера — из настроек игрока
        var lore = Resources.Load<TextAsset>("Codex/lore");     // тексты энциклопедии (написаны в Z.ai)
        if (lore != null) Codex.LoadLore(lore.text);
        var sfx = new GameObject("procedural_sfx");
        sfx.transform.SetParent(transform, false);
        sfx.AddComponent<ProceduralSfx>();
        var splash = new GameObject("splash_burst");
        splash.transform.SetParent(transform, false);
        splash.AddComponent<SplashBurst2D>();
        GameFeel.Create(transform);
        SetupLightAndMusic();
        _dark = DarknessMask.Create(transform);
        _ui = RunUi.Build(this);
        RunTutorial.Build(_ui.Root, this);
        // первая комната строится сразу — за титулом видна живая пещера
        StartNewRun();
        _ui.ShowTitle();
    }

    void OnDestroy() {
        if (PullGestureInput.ReleaseHandler == (System.Action<Vec2, double>)OnDashRelease)
            PullGestureInput.ReleaseHandler = null;
        PullGestureInput.Blocked = false;
    }

    // ---------- Жизнь ----------
    private string _legacyNode;   // v0.13: узел, выбранный для наследия (перед применением)

    public void StartNewRun() {
        var pack = Meta.TakeLegacy();   // наследие сгорает, если жизнь начата без применения
        string node = _legacyNode;
        _legacyNode = null;
        StartRun(new RunState(Meta, NewSeed(), true), pack, node);
    }

    /// <summary>v0.13: реклама просмотрена — сохранить наследие и сразу начать жизнь с ним.</summary>
    public void AcceptLegacy(LegacyPack pack, string chosenNode) {
        if (pack == null) return;
        Meta.Legacy = pack;
        _legacyNode = chosenNode;
        SaveMeta();
        StartNewRun();
    }

    /// <summary>v0.13: тестовый бой (DevTools) — новая жизнь сразу у стража глубины. Наследие сгорает.</summary>
    public void StartDevGuardianRun(int depth) {
        Meta.TakeLegacy();
        _legacyNode = null;
        StartRun(RunState.CreateDevGuardianRun(Meta, NewSeed(), depth), null, null);
        _ui.HideTitle(); // v0.14: DevTools-старт может быть из главного меню — титул закрываем принудительно
    }

    private ulong NewSeed() => (ulong)System.DateTime.UtcNow.Ticks;

    private void StartRun(RunState run, LegacyPack legacy, string legacyNode) {
        _ui.HideImprint();
        Time.timeScale = 1f;
        PullGestureInput.Blocked = false;
        _seed = NewSeed();
        _rng = new PmRandom(_seed ^ 0x5DEECE66DUL);
        Run = run;
        _ended = false;
        _offerShown = false;
        _lastHitSource = null;
        _builtRoomKey = -1;
        _lastDepthShown = 0;
        PuppetOn = false;              // новая жизнь — автопилот сброшен
        _manualT = 0f;
        _puppetMarked.Clear();
        if (_intentLabel != null) _intentLabel.text = "";
        if (legacy != null && Run.ApplyLegacy(legacy,
                !string.IsNullOrEmpty(legacyNode) ? legacyNode
                    : (legacy.Nodes != null && legacy.Nodes.Length > 0 ? legacy.Nodes[0] : null))) {
            _ui.Banner("Наследие прошлой жизни принято", 3f);
            ProceduralSfx.Instance?.Play("evolve");
        }
        EnsurePlayer();
        BuildRoom();
    }

    private void EndRun() {
        if (_ended) return;
        _ended = true;
        GameFeel.CancelHitStop();
        var imprint = Run.Finish();
        bool victory = Run.IsVictory;
        string killer = null;
        if (!victory) {
            string src = imprint.KilledBy ?? _lastHitSource;
            if (IsSoulName(src)) {
                Meta.Nemeses.OnPlayerKilledBy(src, imprint.Mutations, _rng);
                killer = src + " поглотила вас. Теперь она помнит вас и станет сильнее.";
            } else if (!string.IsNullOrEmpty(src)) {
                var sp = SpeciesCatalog.Get(src);
                killer = "Причина гибели: " + (sp != null ? sp.Name : HazardName(src));
            } else killer = "Масса иссякла.";
        }
        Meta.ApplyLife(imprint);
        SaveMeta();
        SplashBurst2D.Instance?.Burst(_player.transform.position, Vector2.up, 14);
        GameFeel.Shake(0.7f);
        ProceduralSfx.Instance?.Play("death");
        for (int i = 0; i < _fragList.Count; i++) _fragList[i].View.gameObject.SetActive(false);
        PullGestureInput.Blocked = true;
        _ui.ShowImprint(imprint, victory, killer);
    }

    private static string HazardName(string id) {
        switch (id) {
            case "thorns": return "шипы";
            case "vent": return "жар из трещины";
            case "dash": return "собственный рывок — масса иссякла";
            case "leech": return "пиявка";
            case BossState.SpeciesId: return "Страж глубины";
            default: return id;
        }
    }

    /// <summary>Титул закрыт — показать карточку биома (за титулом её бы никто не увидел).</summary>
    public void OnTitleClosed() {
        RunTutorial.Instance?.BeginIfNeeded();
        if (Run == null || Run.Room == null) return;
        var biome = Run.Layout != null ? Run.Layout.Biome : BiomeCatalog.ForDepth(Run.Depth);
        _ui.BiomeCard(Run.Depth, biome.Name, biome.Subtitle, RunWorldBuilder.Hex(biome.AccentColor), biome.Id.ToString().ToLowerInvariant());
    }

    /// <summary>Пауза для экранов эволюции и показателей (мир замирает, жест рывка выключен).</summary>
    public void SetPaused(bool paused) {
        GameFeel.CancelHitStop();
        Time.timeScale = paused ? 0f : 1f;
        PullGestureInput.Blocked = paused;
    }

    /// <summary>Развить узел дерева эволюции (из экрана «Эволюция»).</summary>
    public bool TryEvolve(string nodeId, bool free) {
        if (!InRun || !Run.TryEvolve(nodeId, free)) return false;
        var node = EvolutionTree.Get(nodeId);
        ApplyPlayerSize();
        _ui.Flash("Эволюция: " + (node != null ? node.Name : nodeId), 2f);
        _lastTokens = Run.Evolution.FreeTokens;
        return true;
    }
    private int _lastTokens;

    public void UpgradeSoul(SoulStat stat) {
        if (Meta.TryUpgrade(stat)) {
            SaveMeta();
            ProceduralSfx.Instance?.Play("evolve");
        } else ProceduralSfx.Instance?.Play("deny");
        _ui.RefreshSoul();
    }

    /// <summary>Сброс прогресса души (из меню) — только по явному подтверждению в UI.</summary>
    public void ResetSoul() {
        Meta = new SoulMeta();
        SaveMeta();
        _ui.RefreshSoul();
    }

    private bool IsSoulName(string s) {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < _creatures.Count; i++)
            if (_creatures[i] != null && _creatures[i].SoulName == s) return true;
        var recs = Meta.Nemeses.Records;
        for (int i = 0; i < recs.Count; i++) if (recs[i].SoulName == s) return true;
        return false;
    }

    // ---------- Комната ----------
    private void BuildRoom() {
        if (Run.OpenWorld && Run.World != null) { BuildWorld(); return; }
        if (_roomRoot != null) Destroy(_roomRoot.gameObject);
        _creatures.Clear();
        _obstacles.Clear();
        _squashBase.Clear(); // v0.14: старые отбойники уничтожены — кэш масштабов не нужен
        _boss = null;
        _altarRing = null;
        _roomRoot = new GameObject("room").transform;
        _roomRoot.SetParent(transform, false);
        var plan = Run.Room;
        var layout = Run.Layout;
        var biome = layout != null ? layout.Biome : BiomeCatalog.ForDepth(plan.Depth);
        if (biome != null) _audio?.SetBiome(biome.Id, Run.Boss != null);
        _builtRoomKey = plan.Depth * 100 + plan.RoomIndex;
        bool elite = layout != null && layout.Kind == RoomKind.Elite;

        RunWorldBuilder.BuildArena(_roomRoot, plan, biome, elite, WallMaterial, WallThickness);
        if (Camera.main != null) Camera.main.backgroundColor = RunWorldBuilder.Hex(biome.WallColor) * 0.35f;
        if (layout != null)
            for (int i = 0; i < layout.Obstacles.Count; i++)
                _obstacles.Add(RunWorldBuilder.BuildObstacle(_roomRoot, layout.Obstacles[i], biome));
        if (layout != null && (layout.Kind == RoomKind.Shrine || layout.Kind == RoomKind.Spring))
            _altarRing = RunWorldBuilder.BuildAltar(_roomRoot, ToV2(layout.AltarPos), layout.Kind == RoomKind.Spring);

        // выход: кольцо + свечение поверх тьмы, указатель у слизня
        var exitGo = new GameObject("exit");
        exitGo.transform.SetParent(_roomRoot, false);
        exitGo.transform.position = ToV3(plan.ExitPos);
        _exitGlow = SpriteUtil.AddSprite(exitGo.transform, SpriteUtil.GlowSprite, 3.2f, 3.2f, GameBootstrap.CFood, DarknessMask.Order + 6);
        _exitRing = SpriteUtil.AddSprite(exitGo.transform, SpriteUtil.RingSprite, 1.6f, 1.6f, GameBootstrap.CFood, DarknessMask.Order + 7);
        _exitPointer = SpriteUtil.AddSprite(_roomRoot, SpriteUtil.CircleSprite, 0.22f, 0.22f, GameBootstrap.CFood, DarknessMask.Order + 8);
        _shockRing = SpriteUtil.AddSprite(_roomRoot, SpriteUtil.RingSprite, 1f, 1f, new Color(1f, 0.5f, 0.4f, 0f), DarknessMask.Order + 9);

        for (int i = 0; i < plan.Spawns.Count; i++) SpawnCreature(plan.Spawns[i], i);
        if (Run.Boss != null) SpawnBoss(plan);

        PlaceFragmentsAt(ToV2(plan.PlayerStart));
        FollowCamera.Snap = true;
        _dark.Set((float)biome.Darkness01, (float)Run.LightRadius);

        if (plan.Depth != _lastDepthShown) {
            _lastDepthShown = plan.Depth;
            _ui.BiomeCard(plan.Depth, biome.Name, biome.Subtitle, RunWorldBuilder.Hex(biome.AccentColor), biome.Id.ToString().ToLowerInvariant());
        } else if (layout != null && layout.Kind != RoomKind.Normal) {
            _ui.Banner(layout.Title, 2.4f);
            if (!string.IsNullOrEmpty(layout.Hint)) _ui.Flash(layout.Hint, 3f);
        } else _ui.Banner(string.Format("Комната {0}/{1}", plan.RoomIndex + 1, RunState.RoomsPerDepth), 1.2f);
        if (layout != null && layout.Kind == RoomKind.Boss) _ui.Banner("Страж глубины пробуждается…", 3f);
    }

    /// <summary>Огромное подземелье глубины: карта-текстура, стены, алтари, шахта, отложенные спавны.</summary>
    private void BuildWorld() {
        if (_roomRoot != null) Destroy(_roomRoot.gameObject);
        _creatures.Clear();
        _obstacles.Clear();
        _squashBase.Clear(); // v0.14: старые отбойники уничтожены — кэш масштабов не нужен
        _pending.Clear();
        _pendingIndex.Clear();
        _altarRings.Clear();
        _visitedRegions.Clear();
        _boss = null;
        _altarRing = null;
        _roomRoot = new GameObject("world").transform;
        _roomRoot.SetParent(transform, false);
        var map = Run.World;
        var plan = Run.Room;
        var layout = Run.Layout;
        var biome = layout != null && layout.Biome != null ? layout.Biome : BiomeCatalog.ForDepth(plan.Depth);
        _builtRoomKey = plan.Depth * 100 + plan.RoomIndex;
        _world = new WorldView(map, _roomRoot, biome, WallMaterial);
        _audio?.SetBiome(biome.Id, Run.Boss != null || Run.GuardianAwake);
        if (Camera.main != null) Camera.main.backgroundColor = RunWorldBuilder.Hex(biome.WallColor) * 0.3f;
        for (int i = 0; i < map.Obstacles.Count; i++)
            _obstacles.Add(RunWorldBuilder.BuildObstacle(_roomRoot, map.Obstacles[i], biome));
        for (int i = 0; i < map.Altars.Count; i++) {
            var a = map.Altars[i];
            _altarRings.Add(RunWorldBuilder.BuildAltar(_roomRoot, ToV2(a.Pos), a.Kind == AltarKind.Spring, a.Kind == AltarKind.Relic));
        }
        // шахта вниз — крупнее обычного выхода
        var exitGo = new GameObject("shaft");
        exitGo.transform.SetParent(_roomRoot, false);
        exitGo.transform.position = ToV3(plan.ExitPos);
        _exitGlow = SpriteUtil.AddSprite(exitGo.transform, SpriteUtil.GlowSprite, 5f, 5f, GameBootstrap.CFood, DarknessMask.Order + 6);
        _exitRing = SpriteUtil.AddSprite(exitGo.transform, SpriteUtil.RingSprite, 2.2f, 2.2f, GameBootstrap.CFood, DarknessMask.Order + 7);
        SpriteUtil.AddSprite(exitGo.transform, SpriteUtil.CircleSprite, 1.4f, 1.4f, new Color(0, 0, 0, 0.85f), 3);
        _exitPointer = SpriteUtil.AddSprite(_roomRoot, SpriteUtil.CircleSprite, 0.22f, 0.22f, GameBootstrap.CFood, DarknessMask.Order + 8);
        _shockRing = SpriteUtil.AddSprite(_roomRoot, SpriteUtil.RingSprite, 1f, 1f, new Color(1f, 0.5f, 0.4f, 0f), DarknessMask.Order + 9);
        for (int i = 0; i < plan.Spawns.Count; i++) { _pending.Add(plan.Spawns[i]); _pendingIndex.Add(i); }
        // v0.11: страж появится у шахты сам, когда набрана цель глубины (GuardianStep)
        _guardian = null;
        _guardianBannerShown = false;
        _guardianZoneSlow = 0f;
        ClearGuardianVisuals();

        PlaceFragmentsAt(ToV2(plan.PlayerStart));
        FollowCamera.Snap = true;
        _dark.Set((float)biome.Darkness01, (float)Run.LightRadius);
        StreamCreatures(true);
        _ui.SetMinimap(_world);
        _lastDepthShown = plan.Depth;
        _ui.BiomeCard(plan.Depth, biome.Name, biome.Subtitle, RunWorldBuilder.Hex(biome.AccentColor), biome.Id.ToString().ToLowerInvariant());
        _ui.Hint("Найдите шахту вниз. Поглощайте — чтобы её открыть", 4f);
    }

    /// <summary>Появление отложенных существ рядом и усыпление дальних — сотни существ без потерь FPS.</summary>
    private void StreamCreatures(bool force) {
        Vector2 p = _player.Rb.position;
        for (int i = _pending.Count - 1; i >= 0; i--) {
            var sp = _pending[i];
            if (Vector2.Distance(p, ToV2(sp.Pos)) > SpawnRadius) continue;
            int idx = _pendingIndex[i];
            _pending.RemoveAt(i);
            _pendingIndex.RemoveAt(i);
            SpawnCreature(sp, idx);
        }
        for (int i = 0; i < _creatures.Count; i++) {
            var c = _creatures[i];
            if (c == null || c.IsBoss) continue;
            float d = Vector2.Distance(p, c.transform.position);
            bool active = c.gameObject.activeSelf;
            if (active && d > SleepRadius) c.gameObject.SetActive(false);
            else if (!active && d < WakeRadius) c.gameObject.SetActive(true);
        }
    }

    private void WorldStep(Vector2 pPos, float pr) {
        if (_world == null) return;
        _streamTimer -= Time.fixedDeltaTime;
        if (_streamTimer <= 0f) { _streamTimer = 0.25f; StreamCreatures(false); }
        int reg = _world.Reveal(pPos, RevealRadius);
        if (reg >= 0 && _visitedRegions.Add(reg)) {
            Run.OnRegionEntered(reg);
            var info = Run.World.Regions[reg];
            if (info.Kind != RegionKind.Cavern && info.Kind != RegionKind.Start && !string.IsNullOrEmpty(info.Title))
                _ui.Banner(info.Title, 2.6f);
            else if (!string.IsNullOrEmpty(info.Title)) _ui.Flash(info.Title, 2f);
        }
        var altars = Run.World.Altars;
        for (int i = 0; i < altars.Count; i++) {
            if (Run.IsAltarUsed(i) || Vector2.Distance(pPos, ToV2(altars[i].Pos)) > pr + 0.8f) continue;
            double before = Run.Mass;
            Run.UseAltarAt(i);
            ProceduralSfx.Instance?.Play("altar");
            GameFeel.Shake(0.25f);
            if (altars[i].Kind == AltarKind.Spring)
                GameFeel.Text(_player.transform.position, "+" + (Run.Mass - before).ToString("0.0") + " родник", new Color(0.5f, 0.85f, 1f), 1.2f);
            else if (altars[i].Kind == AltarKind.Relic)
                GameFeel.Text(_player.transform.position, "Реликвия! +15 эссенции", new Color(1f, 0.85f, 0.4f), 1.2f);
            if (altars[i].Kind != AltarKind.Spring)
                _ui.Hint("Алтарь дал жетон и " + RunState.MaxTransmutesPerShrine + " обмена «Алхимии» (Эволюция → Алхимия)", 3.5f);
            var c = _altarRings[i].color; c.a = 0.15f; _altarRings[i].color = c;
        }
    }

    private static PhysicsMaterial2D _wallMat;
    private static PhysicsMaterial2D WallMaterial {
        get {
            if (_wallMat == null) _wallMat = new PhysicsMaterial2D("wall") { bounciness = 0.75f, friction = 0f };
            return _wallMat;
        }
    }

    private RunCreature SpawnCreature(SpawnSpec spec, int index) {
        var sp = SpeciesCatalog.Get(spec.SpeciesId);
        if (sp == null) return null;
        if (sp.ColorIndex == 3 && string.IsNullOrEmpty(spec.SoulName))
            spec.SoulName = Meta.Nemeses.PickSoulName(_rng); // у каждой души есть имя — иначе не станет немезидой
        var traits = SpeciesTraitsCatalog.Get(sp.Id);
        MonsterModel pendingModel = null;
        SpriteRenderer pendingRing = null;
        bool food = sp.Behavior == CreatureBehavior.Food;
        float r = (float)SizeRule.RadiusForMass(spec.Mass);
        var view = SimEntityView2D.Create(_roomRoot, sp.Id + "_" + index,
            spec.IsNemesis || sp.ColorIndex == 3 ? SimEntityKind.Ai : SimEntityKind.Beetle, index, r, ColorFor(sp, spec.IsNemesis));
        view.transform.position = ToV3(spec.Pos);
        view.Rb.position = ToV2(spec.Pos);
        view.Rb.linearDamping = food ? 3f : 1.1f;
        view.GetComponent<CircleCollider2D>().sharedMaterial = WallMaterial;
        if (food) {
            view.GetComponent<CircleCollider2D>().isTrigger = true; // споры не толкаются — их всасывают
            view.Body.sortingOrder = 4;
            var g = GameBootstrap.CFood; g.a = 0.45f;
            SpriteUtil.AddSprite(view.transform, SpriteUtil.GlowSprite, r * 5f, r * 5f, g, DarknessMask.Order + 2);
        } else if (sp.ColorIndex == 3 || spec.IsNemesis) {
            view.Body.sortingOrder = 6;
            VisualFactory.DecorateAi(view);
            AddLabel(view.transform, r, spec.IsNemesis ? spec.SoulName + " — помнит вас" : spec.SoulName, new Color(0.85f, 0.8f, 1f, 0.95f));
        } else {
            view.Body.sortingOrder = 6;
            VisualFactory.DecorateBeetle(view);
        }
        if (!food) {
            // 3D-модель вместо круга: объёмное существо с анимацией (вид 3/4 сверху)
            string modelKey = spec.IsNemesis ? "nemesis" : sp.Id;
            var mm = ThirdPartyArt.AttachLook(view.transform, modelKey, r * 2f, spec.IsNemesis ? ColorFor(sp, true) : default);
            if (mm != null) {
                view.Body.gameObject.SetActive(false);
                pendingModel = mm;
            }
            // кольцо под существом — сразу видно, кого можно съесть
            pendingRing = SpriteUtil.AddSprite(view.transform, SpriteUtil.RingSprite, r * 2.5f, r * 2.5f, new Color(1, 1, 1, 0.25f), 5);
        }
        if (!food && traits.Glows) {
            var g = ColorFor(sp, false); g.a = 0.5f;
            SpriteUtil.AddSprite(view.transform, SpriteUtil.GlowSprite, r * 4f, r * 4f, g, DarknessMask.Order + 1);
        }
        var c = view.gameObject.AddComponent<RunCreature>();
        c.Model = pendingModel;
        c.Ring = pendingRing;
        c.IsNemesis = spec.IsNemesis;
        c.SoulName = spec.SoulName;
        c.Variant = spec.Variant;  // v0.10: мерцание — визуальная подсказка и ×1.5 ресурсов
        if (c.IsShimmering)   // сияние поверх тьмы: «ценную» добычу видно издалека (пульс — в UpdateRings)
            c.Sparkle = SpriteUtil.AddSprite(view.transform, SpriteUtil.GlowSprite, r * 4f, r * 4f,
                new Color(0.55f, 1f, 1f, 0.5f), DarknessMask.Order + 2);
        c.Traits = traits;
        c.Init(sp, spec.Mass, new CreatureBrain(_seed + (ulong)(index * 7919 + 1)), view, r);
        if (!food) c.EchoDot = SpriteUtil.AddSprite(view.transform, SpriteUtil.RingSprite, r * 2.6f, r * 2.6f,
            new Color(0.6f, 0.9f, 1f, 0.55f), DarknessMask.Order + 4);
        if (c.EchoDot != null) c.EchoDot.enabled = false;
        if (spec.IsNemesis) {
            var recs = Meta.Nemeses.Records;
            for (int i = 0; i < recs.Count; i++)
                if (recs[i].SoulName == spec.SoulName && !string.IsNullOrEmpty(recs[i].StolenMutation))
                    c.Mutations.Add(recs[i].StolenMutation);
        }
        _creatures.Add(c);
        return c;
    }

    private void SpawnBoss(RoomPlan plan) =>
        SpawnBossAt(new Vec2(plan.ExitPos.X * 0.35, plan.ExitPos.Y * 0.35)); // ближе к центру, на пути к выходу

    private void SpawnBossAt(Vec2 pos) {
        var sp = SpeciesCatalog.Get(BossState.SpeciesId);
        var c = SpawnCreature(new SpawnSpec { SpeciesId = BossState.SpeciesId, Pos = pos, Mass = Run.Boss.Mass }, 9000);
        if (c == null) return;
        c.IsBoss = true;
        c.View.Rb.linearDamping = 1.6f;
        var glow = new Color(1f, 0.3f, 0.25f, 0.45f);
        SpriteUtil.AddSprite(c.transform, SpriteUtil.GlowSprite, c.Radius * 4.5f / c.transform.localScale.x,
            c.Radius * 4.5f / c.transform.localScale.x, glow, DarknessMask.Order + 1);
        AddLabel(c.transform, (float)SizeRule.RadiusForMass(Run.Boss.Mass) / c.transform.localScale.x,
            sp != null ? sp.Name : "Страж глубины", new Color(1f, 0.6f, 0.55f));
        _boss = c;
    }

    private static Color ColorFor(SpeciesDef sp, bool nemesis) {
        if (nemesis) return Color.Lerp(GameBootstrap.CSoul, GameBootstrap.CDanger, 0.35f);
        switch (sp.ColorIndex) {
            case 0: return GameBootstrap.CFood;
            case 1: return new Color(0.85f, 0.72f, 0.42f);
            case 2: return GameBootstrap.CDanger;
            case 3: return GameBootstrap.CSoul;
            case 4: return new Color(0.55f, 0.85f, 0.95f);
            default: return GameBootstrap.CDanger;
        }
    }

    private static void AddLabel(Transform parent, float radius, string text, Color color) {
        var go = new GameObject("label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, radius + 0.45f, 0);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.characterSize = 0.07f;
        tm.anchor = TextAnchor.LowerCenter;
        tm.color = color;
        var font = ThirdPartyArt.Body != null ? ThirdPartyArt.Body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = DarknessMask.Order + 10;
    }

    // ---------- Игрок ----------
    private void EnsurePlayer() {
        if (_playerMat == null) _playerMat = new PhysicsMaterial2D("player") { bounciness = 0.8f, friction = 0f };
        ResetFragments(ToV2(Run.Room.PlayerStart)); // новая жизнь — одно тело
        ApplyPlayerSize();
    }

    private void ApplyPlayerSize() {
        SizeFragments();
        float b = (float)Run.Mutations.Bounciness;
        if (_playerMat != null && !Mathf.Approximately(_playerMat.bounciness, b)) {
            _playerMat.bounciness = b;
            for (int i = 0; i < _fragList.Count; i++) { // применить изменённый материал
                var col = _fragList[i].View.GetComponent<CircleCollider2D>();
                col.sharedMaterial = null; col.sharedMaterial = _playerMat;
            }
        }
        // крупное тело или разбежавшаяся стая — камера отъезжает
        float zoom = Mathf.Clamp(0.75f + 0.125f * Mathf.Sqrt((float)Run.Mass), 0.95f, 1.6f) * (Run.OpenWorld ? 1.9f : 1f);
        if (_player != null && _fragList.Count > 1) zoom = Mathf.Max(zoom, Mathf.Min(2.0f, FragmentSpread() / 7f)); // клоны самостоятельны — камера почти не отъезжает
        FollowCamera.ZoomMult = zoom;
    }

    // ---------- Джойстик (v0.9) ----------
    private const float JoyAccel = 14f;      // ползание: заметно медленнее рывка — охота и броня всё ещё про рывок
    private const float JoyMaxSpeed = 3.8f;

    /// <summary>Ползание ведущего по джойстику (двойники тянутся за ним сами).</summary>
    private void JoystickStep() {
        var j = RunJoystick.Instance;
        if (j == null || !GameSettings.Joystick || _player == null) return;
        Vector2 d = j.Dir;
        if (d.sqrMagnitude < 0.0001f) return;
        var rb = _player.Rb;
        Vector2 n = d.normalized;
        float max = JoyMaxSpeed * (float)Run.DashSpeedMult * Mathf.Min(1f, d.magnitude * 1.25f);
        if (Vector2.Dot(rb.linearVelocity, n) < max) rb.AddForce(n * (JoyAccel * rb.mass), ForceMode2D.Force);
    }

    /// <summary>Кнопка «Рывок»: туда, куда наклонён джойстик, иначе — по ходу движения.</summary>
    public void JoystickDash() {
        if (!InRun || _player == null) return;
        Vector2 d = RunJoystick.Instance != null ? RunJoystick.Instance.Dir : Vector2.zero;
        if (d.sqrMagnitude < 0.04f) d = _player.Rb.linearVelocity;
        if (d.sqrMagnitude < 0.0001f) d = Vector2.up;
        d.Normalize();
        OnDashRelease(new Vec2(d.x, d.y), 1.0);
    }

    /// <summary>Радиус ведущего фрагмента.</summary>
    private float PlayerRadius => (float)SizeRule.RadiusForMass(Run.Fragments.Count > 0 ? Run.Fragments[0].Mass : Run.Mass);

    private void OnDashRelease(Vec2 dir, double charge) {
        if (!InRun || _ui.ModalOpen) return;
        if (_guardianDashLock > 0f) { // v0.12: Хор Бездны оглушил — рывок недоступен (обе схемы)
            _ui.Flash("Оглушён! Рывок через " + _guardianDashLock.ToString("0.0") + " с");
            ProceduralSfx.Instance?.Play("deny");
            return;
        }
        if (!Run.TryDash()) {
            _ui.Flash("Нет зарядов рывка — подождите");
            ProceduralSfx.Instance?.Play("deny");
            return;
        }
        _ricochets = 0;
        _sinceDash = 0f;
        _playerDashed = true;      // v0.12: страж видит рывок (зеркало Гранильщика)
        _playerDashDir = dir;
        float speed = DashSpeed * (float)Run.DashSpeedMult * (float)charge;
        var impulse = new Vector2((float)dir.X, (float)dir.Y) * speed;
        _player.Rb.linearVelocity += impulse;
        RunTutorial.Instance?.Flow.OnDash(); // v0.9: рывок — только у души; клоны живут сами
        ProceduralSfx.Instance?.Play("jet");
        GameFeel.Shake(0.08f * (float)charge);
        // капля влаги остаётся позади — видно, что рывок стоит массы
        SplashBurst2D.Instance?.Burst(_player.transform.position, new Vector2((float)-dir.X, (float)-dir.Y), 3);
        if (Run.IsDead) { _lastHitSource = "dash"; EndRun(); }
    }

    // ---------- Марионетка: автопилот героя и клонов (v0.13) ----------
    public bool PuppetAvailable => InRun && Run.HasAbility("puppet");
    public bool PuppetOn { get; private set; }
    private float _manualT;                 // ручной перехват: сколько ещё управлять самим
    private float _autoDashT;               // пауза между автопилотными рывками
    private PuppetIntent _lastIntent = (PuppetIntent)(-1);
    private readonly HashSet<int> _puppetMarked = new HashSet<int>();   // клоны под нитью
    private readonly List<PuppetZone> _puppetZoneBuf = new List<PuppetZone>();
    private PuppetZone[] _puppetZones = new PuppetZone[0];
    private PuppetSense[] _senseTmp = new PuppetSense[48];
    private TextMesh _intentLabel;

    /// <summary>Кнопка «Авто»: включить/выключить автопилот героя.</summary>
    public void TogglePuppet() {
        if (!PuppetAvailable) {
            _ui.Flash("«Марионетка» открывается в ветви «Клоны»");
            ProceduralSfx.Instance?.Play("deny");
            return;
        }
        PuppetOn = !PuppetOn;
        _manualT = 0f;
        _lastIntent = (PuppetIntent)(-1);
        ProceduralSfx.Instance?.Play(PuppetOn ? "evolve" : "deny");
        _ui.Flash(PuppetOn ? "Автопилот включён — касание управления вернёт вам руку на 3 с"
                           : "Автопилот выключен", 1.8f);
        if (!PuppetOn && _intentLabel != null) _intentLabel.text = "";
    }

    private void PuppetStep(float dt) {
        if (!PuppetOn || _player == null || Run.IsDead) return;
        _autoDashT = Mathf.Max(0f, _autoDashT - dt);
        // касание джойстика/рогатки — управление возвращается игроку на 3 с
        bool manual = (RunJoystick.Instance != null && RunJoystick.Instance.Dir.sqrMagnitude > 0.0001f)
                      || (PullGestureInput.Instance != null && PullGestureInput.Instance.Active);
        if (manual) _manualT = 3f;
        if (_manualT > 0f) {
            _manualT -= dt;
            SetIntentLabel("");
            return;
        }
        var dec = PuppetBrain.Think(BuildPuppetInput(_player.Rb.position, Run.FragmentMass(Run.Fragments.LeadId),
            Run.Fragments.LeadId, false, _player.Rb.position));
        Vector2 dir = new Vector2((float)dec.Move.X, (float)dec.Move.Y);
        if (dir.sqrMagnitude > 0.001f) {
            dir.Normalize();
            var rb = _player.Rb;
            if (Vector2.Dot(rb.linearVelocity, dir) < JoyMaxSpeed * (float)Run.DashSpeedMult)
                rb.AddForce(dir * (JoyAccel * rb.mass), ForceMode2D.Force);
        }
        if (dec.Dash && _autoDashT <= 0f && _guardianDashLock <= 0f) {
            _autoDashT = 0.8f;
            OnDashRelease(dec.DashDir, 1.0);
        }
        if (dec.Intent != _lastIntent) {
            _lastIntent = dec.Intent;
            SetIntentLabel(PuppetBrain.IntentName(dec.Intent));
        }
    }

    /// <summary>Снимок мира для PuppetBrain: существа в радиусе 14, зоны стража, ноша/лидер для клона.</summary>
    private PuppetInput BuildPuppetInput(Vector2 me, double selfMass, int fragId, bool clone, Vector2 lead) {
        int n = 0;
        for (int i = 0; i < _creatures.Count && n < _senseTmp.Length; i++) {
            var c = _creatures[i];
            if (c == null || c.IsBoss || !c.gameObject.activeSelf) continue;
            Vector2 cp = c.View.Rb.position;
            if (Vector2.Distance(me, cp) > 14f) continue;
            double cm = c.Mass;
            bool edible = c.IsFood || selfMass >= cm * (clone ? Run.CloneAbsorbRatio : Run.Mutations.AbsorbRatio);
            _senseTmp[n++] = new PuppetSense {
                Pos = new Vec2(cp.x, cp.y), Mass = cm, IsFood = c.IsFood,
                IsShimmering = c.IsShimmering,
                Threatens = !c.IsFood && !edible && cm > selfMass * 0.9,
            };
        }
        var sense = new PuppetSense[n];
        System.Array.Copy(_senseTmp, sense, n);
        return new PuppetInput {
            SelfPos = new Vec2(me.x, me.y), SelfMass = selfMass,
            SelfVel = new Vec2(0, 0),
            DashCharges = clone ? 0 : Run.DashCharges,
            MaxDashCharges = clone ? 0 : Run.MaxDashCharges,
            Creatures = sense, Zones = _puppetZones,
            Carry = clone ? Run.CarryTotal(fragId) : 0.0,
            CarryCapacity = clone ? Run.Resources.Capacity : 0.0,
            LeaderPos = new Vec2(lead.x, lead.y),
            Leash = clone ? Run.CloneLeash : 0.0,
            AbsorbRatio = clone ? Run.CloneAbsorbRatio : Run.Mutations.AbsorbRatio,
        };
    }

    /// <summary>Круги-телеграфы и зоны стража — автопилот обходит их (вызывается из GuardianStep).</summary>
    private void UpdatePuppetZones(GuardianOutput o) {
        _puppetZoneBuf.Clear();
        if (o.Telegraph.Seconds > 0.0 && o.Telegraph.Radius > 0.0)
            _puppetZoneBuf.Add(new PuppetZone { Pos = o.Telegraph.Pos, Radius = o.Telegraph.Radius });
        if (o.Telegraphs != null)
            for (int i = 0; i < o.Telegraphs.Length; i++)
                if (o.Telegraphs[i].Seconds > 0.0 && o.Telegraphs[i].Radius > 0.0)
                    _puppetZoneBuf.Add(new PuppetZone { Pos = o.Telegraphs[i].Pos, Radius = o.Telegraphs[i].Radius });
        if (o.Zones != null)
            for (int i = 0; i < o.Zones.Length; i++)
                if (o.Zones[i].Radius > 0.0 && o.Zones[i].Life > 0.0)
                    _puppetZoneBuf.Add(new PuppetZone { Pos = o.Zones[i].Pos, Radius = o.Zones[i].Radius });
        _puppetZones = _puppetZoneBuf.ToArray();
    }

    private void SetIntentLabel(string text) {
        if (_player == null) return;
        if (_intentLabel == null) {
            var go = new GameObject("puppet_intent");
            var tm = go.AddComponent<TextMesh>();
            tm.fontSize = 48;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            var font = ThirdPartyArt.Body != null ? ThirdPartyArt.Body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.GetComponent<MeshRenderer>().sortingOrder = DarknessMask.Order + 10;
            _intentLabel = tm;
        }
        _intentLabel.text = text;
        _intentLabel.transform.position = _player.transform.position + Vector3.up * (PlayerRadius + 0.7f);
    }

    /// <summary>Тап по клону (Кукловод): выдать или снять нить автопилота — максимум две.</summary>
    private void PuppetTapStep() {
        if (!Run.HasAbility("puppet_master") || _ui.ModalOpen || _player == null) return;
        bool tap = false;
        Vector2 screen = Vector2.zero;
        if (Application.isMobilePlatform) {
            for (int i = 0; i < UnityEngine.Input.touchCount; i++) {
                var t = UnityEngine.Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) continue;
                tap = true; screen = t.position; break;
            }
        } else if (UnityEngine.Input.GetMouseButtonDown(0) &&
                   (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) {
            tap = true; screen = UnityEngine.Input.mousePosition;
        }
        if (!tap) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
        var fv = NearestFragment(w, out float d);
        if (fv == null || fv.View == _player || d > FragRadius(fv) + 0.45f) return;
        ToggleClonePuppet(fv);
    }

    private void ToggleClonePuppet(FragView fv) {
        if (_puppetMarked.Contains(fv.Id)) {
            _puppetMarked.Remove(fv.Id);
            if (fv.Thread != null) fv.Thread.enabled = false;
            _ui.Flash("Нить снята", 1.2f);
            ProceduralSfx.Instance?.Play("deny");
            return;
        }
        if (_puppetMarked.Count >= 2) {
            _ui.Flash("Нитей только две — снимите одну", 1.6f);
            ProceduralSfx.Instance?.Play("deny");
            return;
        }
        _puppetMarked.Add(fv.Id);
        if (fv.Thread == null)
            fv.Thread = SpriteUtil.AddSprite(fv.View.transform, SpriteUtil.RingSprite, 1f, 1f,
                new Color(1f, 0.82f, 0.3f, 0.95f), DarknessMask.Order + 7);
        fv.Thread.enabled = true;
        _ui.Flash("Клон под нитью — он играет сам", 1.6f);
        ProceduralSfx.Instance?.Play("evolve");
    }

    /// <summary>Метка-нить над клоном: держим мировой размер и высоту при любом масштабе тела.</summary>
    private void ThreadStep(FragView fv) {
        if (fv.Thread == null || !fv.Thread.enabled) return;
        float parentScale = Mathf.Max(0.0001f, fv.View.transform.localScale.x);
        float r = FragRadius(fv);
        fv.Thread.transform.localPosition = new Vector3(0f, (r + 0.35f) / parentScale, 0f);
        float s = 0.42f / parentScale;
        fv.Thread.transform.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>Клон под нитью: движение по решению PuppetBrain (сила — как в TwinForage).</summary>
    private void PuppetCloneStep(FragView fv, Rigidbody2D rb, Vector2 lead, float dist, float dt) {
        var me = rb.position;
        double m = Run.FragmentMass(fv.Id);
        var dec = PuppetBrain.Think(BuildPuppetInput(me, m, fv.Id, true, lead));
        Vector2 dir = new Vector2((float)dec.Move.X, (float)dec.Move.Y);
        if (dir.sqrMagnitude > 0.001f) {
            dir.Normalize();
            if (Vector2.Dot(rb.linearVelocity, dir) < CloneMaxSpeed(fv))
                rb.AddForce(dir * ((dec.Intent == PuppetIntent.Flee ? 18f : 11f) * rb.mass), ForceMode2D.Force);
        } else Wander(fv, rb, lead, dist, dt);
    }

    // ---------- Контакты ----------
    private int _hitFrag = -1;   // фрагмент, участвующий в текущем контакте

    public void OnPlayerCollision(Collision2D col, int fragmentId) {
        if (!InRun) return;
        if (!_frags.ContainsKey(fragmentId)) return;
        _hitFrag = fragmentId;
        float rel = col.relativeVelocity.magnitude;
        Vector2 point = col.contactCount > 0 ? col.GetContact(0).point : (Vector2)_player.transform.position;
        var obstacle = col.collider.GetComponent<RunObstacle>();
        bool isWall = obstacle == null && col.collider.GetComponent<RunCreature>() == null;
        if ((obstacle != null || isWall) && rel > 2f && _sinceDash < 2.5f) {
            _ricochets++;
            if (Run.Mutations.RicochetBonus > 0 && _ricochets >= 1)
                GameFeel.Text(point, "Рикошет ×" + _ricochets, new Color(0.6f, 0.9f, 1f), 0.8f);
        }
        if (obstacle != null) { OnObstacleHit(obstacle, rel, point); return; }
        var c = col.collider.GetComponent<RunCreature>();
        if (c == null || c.IsFood) return;
        if (c.IsBoss) {
            if (Run.OpenWorld && _guardian == c) { OnGuardianContact(c, rel, point); return; } // v0.11
            if (Run.OpenWorld && GuardianPartIndex(c) >= 0) { OnGuardianPartContact(c, rel, point); return; } // v0.12: капли Горнила
            OnBossContact(c, rel, point); return;
        }
        var sp = c.Species;
        var res = SizeRule.Resolve(Run.FragmentMass(fragmentId), Run.Mutations, c.Mass, c.Mutations, rel,
            sp != null ? sp.Attack : 0.08,                     // v0.14: Attack/Toughness вида, глубина
            sp != null && sp.Toughness > 0.0 ? sp.Toughness : 1.0, Run.Depth);
        ResolveContact(c, res, point, rel);
    }

    /// <summary>Урон по фрагменту, участвующему в контакте; растворившийся — убрать тело.</summary>
    private void HurtFragment(double amount, string source) {
        if (amount > 0.02) _ui.HurtFlash(Mathf.Clamp01((float)amount / 1.2f));
        int id = _hitFrag >= 0 && _frags.ContainsKey(_hitFrag) ? _hitFrag : Run.Fragments.LeadId;
        _lastHitSource = source;
        Vector2 at = _frags.TryGetValue(id, out var hv) ? hv.View.Rb.position : (Vector2)_player.transform.position;
        if (Run.OnMassLostAt(id, amount, source)) {
            _ui.Flash("Фрагмент растворился", 1.4f);
            ProceduralSfx.Instance?.Play("dissolve");
            DropCarry(at);
            SyncFragments();
        }
    }

    private void OnObstacleHit(RunObstacle o, float rel, Vector2 point) {
        switch (o.Kind) {
            case ObstacleKind.Thorns: {
                double loss = ObstacleRules.ThornLoss(rel, Run.Mutations);
                HurtFragment(loss, "thorns");
                SpawnDroplets(point, loss * 0.5);
                GameFeel.Text(point, "−" + loss.ToString("0.0"), GameBootstrap.CDanger);
                GameFeel.Shake(0.25f);
                ProceduralSfx.Instance?.Play("hit");
                if (Run.IsDead) EndRun();
                break;
            }
            case ObstacleKind.Mushroom:
                GameFeel.Shake(0.12f);
                ProceduralSfx.Instance?.Play("assist");
                if (o.Main != null) StartCoroutine(Squash(o.Main.transform));
                break;
            case ObstacleKind.Crystal:
                if (!o.Shattered && rel >= ObstacleRules.ShatterSpeed) {
                    o.Shattered = true;
                    GameFeel.Shake(0.35f);
                    GameFeel.HitStop(0.05f);
                    GameFeel.Text(o.transform.position, "Кристалл расколот!", new Color(0.6f, 0.95f, 1f));
                    ProceduralSfx.Instance?.Play("crystal");
                    SplashBurst2D.Instance?.Burst(o.transform.position, Vector2.up, 10);
                    for (int i = 0; i < 3; i++) SpawnDroplet(o.transform.position, 0.4, i * 2.1f);
                    _obstacles.Remove(o);
                    Destroy(o.gameObject);
                } else ProceduralSfx.Instance?.Play("glass", 0.8f, 0f, 0.7f);
                break;
            default:
                if (rel > 4f) GameFeel.Shake(0.06f);
                break;
        }
    }

    // v0.14: исходный масштаб отбойников — иначе частые удары стартуют корутину от уже сжатого
    // масштаба и гриб растёт бесконечно. Кэш живёт до пересборки комнаты/мира.
    private readonly Dictionary<Transform, Vector3> _squashBase = new Dictionary<Transform, Vector3>();

    private System.Collections.IEnumerator Squash(Transform t) {
        if (t == null) yield break;
        if (!_squashBase.TryGetValue(t, out var baseS)) {
            baseS = t.localScale;
            _squashBase[t] = baseS;
        }
        for (float k = 0; k < 1f; k += Time.deltaTime * 6f) {
            if (t == null) { _squashBase.Remove(t); yield break; }
            float s = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
            t.localScale = new Vector3(baseS.x * s, baseS.y * s, 1f);
            yield return null;
        }
        if (t != null) t.localScale = baseS;
    }

    private void ResolveContact(RunCreature c, ContactResult res, Vector2 point, float rel) {
        if (res.Absorbed == 1) { Absorb(c); return; }
        if (res.Absorbed == 2) {
            // существо проглотило фрагмент целиком; если это было всё тело — конец
            HurtFragment(Run.FragmentMass(_hitFrag >= 0 ? _hitFrag : Run.Fragments.LeadId) + 0.01, c.SourceId);
            c.Mass += 0.5;
            c.ApplySize();
            if (Run.IsDead) EndRun();
            return;
        }
        double lossB = res.MassLossB;
        if (lossB > 0) {
            if (rel < c.Traits.ArmorSpeed) {
                GameFeel.Text(point, "Броня! Быстрее", new Color(0.7f, 0.85f, 1f), 0.8f);
                ProceduralSfx.Instance?.Play("hit");
                lossB = 0;
            } else lossB *= 1.0 + Run.Mutations.RicochetBonus * _ricochets;
        }
        if (lossB > 0) {
            c.Model?.Flinch();
            c.Mass -= lossB;
            Run.OnHit(lossB);
            if (Run.Mutations.LifestealFrac > 0) Run.OnLifesteal(lossB);
            SpawnDroplets(point, lossB);
            GameFeel.Text(c.transform.position, "−" + lossB.ToString("0.0"), new Color(1f, 0.85f, 0.4f));
            GameFeel.Shake(Mathf.Clamp(0.1f + (float)lossB * 0.25f, 0.1f, 0.6f));
            if (lossB > 0.6) GameFeel.HitStop(0.06f);
            ProceduralSfx.Instance?.Play("weaken");
            if (c.Mass < 0.35) { SpawnDroplets(c.transform.position, System.Math.Max(0, c.Mass)); KillCreature(c); }
            else if (c.Traits.SplitsOnHit && c.Mass >= 1.5) SplitCreature(c);
            else c.ApplySize();
        }
        if (res.MassLossA > 0) {
            HurtFragment(res.MassLossA, c.SourceId);
            SpawnDroplets(point, res.MassLossA);
            GameFeel.Text(_player.transform.position, "−" + res.MassLossA.ToString("0.0"), GameBootstrap.CDanger);
            GameFeel.Shake(0.3f);
            ProceduralSfx.Instance?.Play("hit");
            if (Run.IsDead) EndRun();
        }
    }

    private void OnBossContact(RunCreature c, float rel, Vector2 point) {
        var boss = Run.Boss;
        if (boss == null) return;
        double fragMass = Run.FragmentMass(_hitFrag >= 0 ? _hitFrag : Run.Fragments.LeadId);
        if (boss.IsExposed && fragMass >= boss.Mass * 0.6) {
            Run.OnBossAbsorbed();
            CodexOnAbsorb("warden");
            GameFeel.HitStop(0.25f);
            GameFeel.Shake(1f);
            _ui.Banner("Страж поглощён! Сердце пещеры открыто", 4f);
            ProceduralSfx.Instance?.Play("capture");
            SplashBurst2D.Instance?.Burst(c.transform.position, Vector2.up, 20);
            KillCreature(c);
            _boss = null;
            return;
        }
        double loss = boss.ApplyHit(rel, fragMass, Run.Mutations, _ricochets);
        if (loss > 0) {
            Run.OnHit(loss);
            if (Run.Mutations.LifestealFrac > 0) Run.OnLifesteal(loss);
            c.Mass = boss.Mass;
            c.ApplySize();
            SpawnDroplets(point, loss * 0.6);
            GameFeel.Text(c.transform.position, "−" + loss.ToString("0.0"), new Color(1f, 0.8f, 0.3f), 1.3f);
            GameFeel.Shake(0.55f);
            GameFeel.HitStop(0.09f);
            ProceduralSfx.Instance?.Play("weaken");
            if (boss.IsExposed) _ui.Banner("Страж обнажён — поглотите его!", 3f);
        } else if (rel > 1.5f) {
            GameFeel.Text(point, "Броня! Нужна скорость и рикошеты", new Color(0.7f, 0.85f, 1f), 0.8f);
            ProceduralSfx.Instance?.Play("glass", 0.9f, 0f, 0.45f);
        }
        // страж бьёт в ответ своим Attack (v0.14: процент массы героя, границы 3–25%)
        var res = SizeRule.Resolve(fragMass, Run.Mutations, boss.Mass, new MutationSet(), rel,
            0.11, 1.2, Run.Depth);
        if (res.MassLossA > 0 && !boss.IsExposed) {
            HurtFragment(res.MassLossA, BossState.SpeciesId);
            GameFeel.Text(_player.transform.position, "−" + res.MassLossA.ToString("0.0"), GameBootstrap.CDanger);
            if (Run.IsDead) EndRun();
        }
    }

    private void SplitCreature(RunCreature c) {
        double half = c.Mass * 0.5;
        c.Mass = half;
        c.ApplySize();
        var pos = c.View.Rb.position + Random.insideUnitCircle.normalized * (c.Radius + 0.2f);
        var twin = SpawnCreature(new SpawnSpec { SpeciesId = c.Species.Id, Pos = new Vec2(pos.x, pos.y), Mass = half }, 2000 + _creatures.Count);
        if (twin != null) twin.View.Rb.linearVelocity = (pos - c.View.Rb.position).normalized * 4f;
        GameFeel.Text(c.transform.position, "Делится!", new Color(0.6f, 0.85f, 1f), 0.8f);
    }

    /// <summary>Первая встреча с видом за всю историю души — карточка энциклопедии.</summary>
    private void CodexOnAbsorb(string speciesId) {
        if (string.IsNullOrEmpty(speciesId) || SpeciesCatalog.Get(speciesId) == null) return;
        bool firstEver = Meta.SeenSpecies == null || !Meta.SeenSpecies.Contains(speciesId);
        if (!Codex.Unlock(Meta, Codex.SpeciesKey(speciesId))) return;
        SaveMeta();
        if (firstEver) _ui.Codex.ShowCard(Codex.ForSpecies(speciesId));
    }

    /// <summary>Область и ресурсы открываются в энциклопедии без карточки — коротким сообщением.</summary>
    private void CodexStep() {
        if (Run == null || Run.Resources == null) return;
        _codexTimer -= Time.unscaledDeltaTime;
        if (_codexTimer > 0f) return;
        _codexTimer = 0.5f;
        for (int i = 0; i < ResourceInfo.Count; i++) {
            var k = (ResourceKind)i;
            if (Run.Resources.Get(k) > 0 && Codex.Unlock(Meta, Codex.ResourceKey(k))) {
                SaveMeta();
                _ui.Flash("Энциклопедия: новый ресурс — " + ResourceInfo.Name(k), 1.6f);
            }
        }
        var biome = Run.Layout != null && Run.Layout.Biome != null ? Run.Layout.Biome : BiomeCatalog.ForDepth(Run.Depth);
        if (biome != null && Codex.Unlock(Meta, Codex.BiomeKey(biome.Id))) SaveMeta();
    }
    private float _codexTimer;

    private void Absorb(RunCreature c) {
        // v0.10: передаём вариант (мерцание ×1.5 ресурсов) в зависимости от того, кто поглощает.
        double bonus = _hitFrag >= 0
            ? Run.OnAbsorbAt(_hitFrag, c.Species.Id, c.Mass, c.Variant)
            : Run.OnAbsorbVariant(c.Species.Id, c.Mass, c.Variant);
        var pos = c.transform.position;
        if (c.IsNemesis) {
            Meta.Nemeses.OnSoulAbsorbed(c.SoulName);
            _ui.Banner(c.SoulName + " поглощена — её память теперь ваша", 3f);
        } else if (!c.IsFood) _ui.Flash("Поглощено: " + c.Species.Name + (c.IsShimmering ? " (мерцание!)" : ""));
        if (c.IsShimmering) {
            GameFeel.Text(pos + Vector3.up * 0.9f, "Мерцание: ресурсы ×1.5", new Color(0.55f, 1f, 1f), 1.1f);
            ProceduralSfx.Instance?.Play("pickup");
        }
        if (!c.IsFood && Run.LastReclaimedMutation != null) {
            // v0.10: возвращённая из немезиды мутация — показать игроку.
            var mut = MutationCatalog.Get(Run.LastReclaimedMutation);
            GameFeel.Text(pos + Vector3.up * 1.2f, "Возвращено: " + (mut != null ? mut.Name : Run.LastReclaimedMutation),
                new Color(0.9f, 0.6f, 1f), 1.6f);
        }
        if (Run.LastComboMilestone > 0) {
            // v0.10: порог цепочки 3/5/8 — флеш «Цепочка 3!» отдельным текстом.
            GameFeel.Text(pos + Vector3.up * 1.6f, "Цепочка " + Run.LastComboMilestone + "!",
                new Color(1f, 0.85f, 0.3f), 1.3f);
        }
        if (!c.IsFood) {
            // добыча в хранилище — цветом ресурса; у двойника — «в ноше», до слияния
            bool twin = _hitFrag >= 0 && _hitFrag != Run.Fragments.LeadId;
            for (int y = 0; y < Run.LastYield.Length; y++) {
                var it = Run.LastYield[y];
                GameFeel.Text(pos + new Vector3(0.6f, -0.3f - 0.45f * y, 0), "+" + it.Amount.ToString("0.#") + " " + ResourceInfo.Name(it.Kind) + (twin ? " (в ноше)" : ""),
                    RunEvolutionUi.Hex(ResourceInfo.Color(it.Kind)), 0.75f);
            }
            if (twin && Run.LastYield.Length > 0) OnTwinLoot();
        }
        if (!c.IsFood) {
            GameFeel.Text(pos, "+" + (c.Mass + bonus).ToString("0.0"), GameBootstrap.CFood);
            if (Run.Combo.Chain >= 2) {
                GameFeel.Text(pos + Vector3.up * 0.6f, "Цепочка ×" + Run.Combo.Chain, new Color(1f, 0.9f, 0.4f), 1f + 0.12f * Run.Combo.Chain);
                GameFeel.HitStop(0.04f);
            }
            GameFeel.Shake(0.15f);
            SplashBurst2D.Instance?.Burst(pos, Vector2.up, 8);
        }
        ProceduralSfx.Instance?.Play(c.IsFood ? "eat" : "capture");
        RunTutorial.Instance?.Flow.OnAbsorb(c.IsFood);
        if (!c.IsNemesis) CodexOnAbsorb(c.Species != null ? c.Species.Id : null);
        KillCreature(c);
    }

    private void KillCreature(RunCreature c) {
        _creatures.Remove(c);
        Destroy(c.gameObject);
    }

    /// <summary>Оторванная масса разлетается каплями-спорами: её можно подобрать обратно.</summary>
    private void SpawnDroplets(Vector2 at, double lost) {
        if (lost <= 0.01) return;
        var parts = SizeRule.SplitIntoDroplets(lost);
        for (int i = 0; i < parts.Length; i++) SpawnDroplet(at, parts[i], (float)_rng.Range(0, System.Math.PI * 2));
    }

    private void SpawnDroplet(Vector2 at, double mass, float angle) {
        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var pos = at + dir * 0.3f;
        var c = SpawnCreature(new SpawnSpec { SpeciesId = "spore", Pos = new Vec2(pos.x, pos.y), Mass = mass }, 1000 + _creatures.Count);
        if (c != null) c.View.Rb.linearVelocity = dir * 3.5f;
    }

    // ---------- Цикл ----------
    void FixedUpdate() {
        if (!InRun || _ui.ModalOpen) return;
        float dt = Time.fixedDeltaTime;
        _time += dt;
        _sinceDash += dt;
        var pPos = _player.Rb.position;
        float pr = PlayerRadius;
        JoystickStep();
        PuppetStep(dt);   // v0.13: автопилот героя («Марионетка»)
        FragmentsStep(dt);
        LootStep();
        if (!InRun) return;
        pPos = _player.Rb.position;
        pr = PlayerRadius;
        for (int i = _creatures.Count - 1; i >= 0; i--) {
            var c = _creatures[i];
            if (c == null) { _creatures.RemoveAt(i); continue; }
            if (c.IsBoss || !c.gameObject.activeSelf) continue; // спящие далеко — не думают
            var rb = c.View.Rb;
            // каждое существо видит ближайший фрагмент: мелкий фрагмент слабее — его едят крупные
            var fv = NearestFragment(rb.position, out _);
            if (fv == null) continue;
            _hitFrag = fv.Id;
            Vector2 fPos = fv.View.Rb.position;
            float fr = FragRadius(fv);
            double fMass = Run.FragmentMass(fv.Id);
            Vector2 d = fPos - rb.position;
            float dist = d.magnitude;
            if (c.IsFood) {
                // биогравитация: споры тянутся к фрагменту, касание — поглощение
                float pull = (1.6f + 0.45f * Mathf.Sqrt((float)fMass) + (float)Run.PullBonus) * (float)Run.Mutations.PullRadiusMult;
                if (dist < pull && dist > 0.001f)
                    rb.AddForce(d / dist * (9f * rb.mass * (1f - dist / pull)), ForceMode2D.Force);
                if (dist <= fr + c.Radius * 0.6f) Absorb(c);
                continue;
            }
            bool touching = dist <= fr + c.Radius + 0.06f;
            // крупное, но уже меньше этого фрагмента — съедаем при касании без удара
            double ratio = fv.View == _player ? Run.Mutations.AbsorbRatio : Run.CloneAbsorbRatio;
            if (touching && fMass >= c.Mass * ratio) { Absorb(c); continue; }
            if (touching && c.Traits.LeechPerSec > 0) {
                double drain = c.Traits.LeechPerSec * dt;
                HurtFragment(drain, "leech");
                c.Mass += drain;
                c.ApplySize();
                if (Run.IsDead) { EndRun(); return; }
            }
            var o = c.Brain.Think(new BrainInput {
                SelfPos = new Vec2(rb.position.x, rb.position.y), SelfMass = c.Mass, Behavior = c.Species.Behavior,
                PlayerPos = new Vec2(fPos.x, fPos.y), PlayerMass = fMass, Time = _time
            });
            if (o.Impulse)
                rb.linearVelocity += new Vector2((float)o.Direction.X, (float)o.Direction.Y) * (CreatureSpeed * (float)o.Strength01);
        }
        _hitFrag = -1;
        BossStep(dt, pPos);
        GuardianStep(dt, pPos); // v0.11: страж у шахты (открытый мир)
        WorldStep(pPos, pr);
        CrackHintStep();
        ZoneStep(dt, pPos, pr);
        ToxicStep(dt, pPos, pr);
        AltarStep(pPos, pr);
        // выход
        if (Run.ExitOpen && Vector2.Distance(pPos, ToV2(Run.Room.ExitPos)) <= ExitRadius + pr) {
            if (Run.TryEnterExit()) {
                ProceduralSfx.Instance?.Play("portal");
                if (Run.IsVictory) { EndRun(); return; }
                BuildRoom();
            }
        }
    }

    private void BossStep(float dt, Vector2 pPos) {
        var boss = Run.Boss;
        if (boss == null || _boss == null) return;
        var rb = _boss.View.Rb;
        var o = boss.Think(new BossInput {
            SelfPos = new Vec2(rb.position.x, rb.position.y), PlayerPos = new Vec2(pPos.x, pPos.y),
            PlayerMass = Run.Mass, Dt = dt
        });
        if (o.Impulse) {
            rb.linearVelocity += new Vector2((float)o.Direction.X, (float)o.Direction.Y) * (9f * (float)o.Strength01);
            GameFeel.Shake(0.12f);
        }
        for (int i = 0; i < o.SpawnMinions; i++) {
            float a = i * 2.1f + _time;
            var p = rb.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (_boss.Radius + 0.8f);
            SpawnCreature(new SpawnSpec { SpeciesId = "mite", Pos = new Vec2(p.x, p.y), Mass = 1.1 }, 3000 + _creatures.Count);
        }
        if (o.SpawnMinions > 0) _ui.Flash("Страж выпускает выводок!");
        if (o.Shockwave) {
            _shockT = 0f;
            _shockRing.transform.position = rb.position;
            GameFeel.Shake(0.5f);
            ProceduralSfx.Instance?.Play("shock");
            for (int f = 0; f < _fragList.Count; f++) {
                var frb = _fragList[f].View.Rb;
                Vector2 away = frb.position - rb.position;
                if (away.magnitude < 5f) frb.linearVelocity += away.normalized * (10f * (1f - away.magnitude / 5f) + 3f);
            }
        }
        _boss.Mass = boss.Mass;
        _boss.ApplySize();
        if (boss.IsExposed && _boss.View.Body != null)
            _boss.View.Body.color = Color.Lerp(GameBootstrap.CDanger, Color.white, 0.4f + 0.3f * Mathf.Sin(_time * 8f));
    }

    // ---------- Страж глубины (v0.11): спит у шахты, пока не набрана цель ----------

    private void GuardianStep(float dt, Vector2 pPos) {
        var g = Run.Guardian;
        _guardianZoneSlow = 0f;
        if (g == null || !Run.GuardianAwake || Run.GuardianAbsorbed) {
            HideGuardianVisuals();
            DespawnGuardianParts();
            _guardianDashLock = 0f;
            _ui.SetDashLocked(0f);
            return;
        }
        if (_guardian == null) {
            SpawnGuardianAt(Run.Room.ExitPos); // страж ждёт у шахты (ExitPos — Vec2)
            if (_guardian == null) return;
        }
        if (!_guardianBannerShown) {
            _guardianBannerShown = true;
            _ui.Banner(g.Def.Name + " пробуждается…", 3f);
            ProceduralSfx.Instance?.Play("shock");
            GameFeel.Shake(0.3f);
        }
        var rb = _guardian.View.Rb;
        var o = g.Think(new GuardianInput {
            SelfPos = new Vec2(rb.position.x, rb.position.y), PlayerPos = new Vec2(pPos.x, pPos.y),
            PlayerMass = Run.Mass, Dt = dt,
            PlayerDashed = _playerDashed, PlayerDashDir = _playerDashDir, // v0.12: зеркало Гранильщика
        });
        _playerDashed = false; // флаг однотактный
        UpdatePuppetZones(o); // v0.13: телеграфы/зоны стража — для автопилота
        _guardianDashLock = Mathf.Max(0f, (float)o.DashLockSeconds);
        _ui.SetDashLocked(_guardianDashLock); // серая кнопка рывка с таймером «Оглушён!»
        if (o.ResetFight) { // отступление в фазе 1: штраф и восстановление — в Domain
            Run.OnGuardianFightReset();
            _ui.Banner(g.Def.Name + ": отступление — страж восстанавливает силы (−25% массы)", 3f);
            ProceduralSfx.Instance?.Play("deny");
            DespawnGuardianParts();
            if (Run.IsDead) { EndRun(); return; }
        }
        if (o.Impulse) {
            rb.linearVelocity += new Vector2((float)o.Direction.X, (float)o.Direction.Y) * (9f * (float)o.Strength01);
            GameFeel.Shake(0.12f);
        }
        for (int i = 0; i < o.SpawnMinions; i++) {
            float a = i * 2.1f + _time;
            var p = rb.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (_guardian.Radius + 0.8f);
            SpawnCreature(new SpawnSpec { SpeciesId = o.MinionSpecies, Pos = new Vec2(p.x, p.y), Mass = o.MinionMass }, 3000 + _creatures.Count);
        }
        if (o.SpawnMinions > 0) _ui.Flash(g.Def.Name + (g.Def.Kind == GuardianKind.Grinder ? ": осколки сходят с орбиты!" : " выпускает выводок!"));
        if (o.Shockwave) {
            _shockT = 0f;
            _shockRing.transform.position = rb.position;
            GameFeel.Shake(0.5f);
            ProceduralSfx.Instance?.Play("shock");
            for (int f = 0; f < _fragList.Count; f++) {
                var frb = _fragList[f].View.Rb;
                Vector2 away = frb.position - rb.position;
                if (away.magnitude < 5f) frb.linearVelocity += away.normalized * (10f * (1f - away.magnitude / 5f) + 3f);
            }
        }
        if (o.Strike) { // удар по площади: доля массы героя (Domain отдал телеграф заранее)
            _hitFrag = Run.Fragments.LeadId;
            HurtFragment(Run.Mass * o.StrikeDamageFrac, g.Def.SpeciesId);
            _hitFrag = -1;
            GameFeel.Shake(0.5f);
            GameFeel.Text(pPos + Vector2.up * 0.8f, StrikeLabel(g.Def.Kind), new Color(0.65f, 1f, 0.55f), 1.1f);
            ProceduralSfx.Instance?.Play("hit");
        }
        if (o.Knockback.Length > 0.001) // v0.12: отброс гейзера Горнила
            _player.Rb.linearVelocity += new Vector2((float)o.Knockback.X, (float)o.Knockback.Y);
        if (o.Current.Length > 0.001) { // v0.12: течение Хора — сила на героя + частицы
            _player.Rb.AddForce(new Vector2((float)o.Current.X, (float)o.Current.Y) * _player.Rb.mass, ForceMode2D.Force);
            DrawCurrentDots(o.Current);
        }
        if (o.Pull > 0.001) { // v0.12: воронка Ф3 — притяжение к стражу
            Vector2 toG = (Vector2)_guardian.transform.position - pPos;
            if (toG.sqrMagnitude > 0.01f)
                _player.Rb.AddForce(toG.normalized * ((float)o.Pull * _player.Rb.mass), ForceMode2D.Force);
        }
        if (o.RingBurn) { // v0.14: вне кипящего кольца Горнила — доля массы героя в секунду
            _hitFrag = Run.Fragments.LeadId;
            HurtFragment(Run.Mass * g.Def.RingDamagePerSec * dt, g.Def.SpeciesId);
            _hitFrag = -1;
        }
        if (Run.IsDead) { EndRun(); return; }
        GuardianPartsStep(o);          // v0.12: капли Горнила
        GuardianZonesStep(dt, pPos, o);
        GuardianTelegraphStep(o);      // одиночный телеграф (Спороносица, Хор)
        GuardianTelegraphsStep(o);     // до трёх кругов (гейзеры Горнила)
        DrawGuardianRing(o, rb.position); // кипящее кольцо арены
        DrawGuardianShards(o);         // осколки Гранильщика
        DrawGuardianBeam(o);           // луч Гранильщика
        DrawGuardianZones(o);
        if (_guardianWeakRing != null) _guardianWeakRing.enabled = o.WeakPointOpen; // зелёное кольцо — бей сейчас
        if (g.LastBlock) { // v0.12: осколок Гранильщика отбил удар — отскок и −5% массы герою
            _player.Rb.linearVelocity += new Vector2((float)g.LastBlockBounce.X, (float)g.LastBlockBounce.Y) * 7f;
            _hitFrag = Run.Fragments.LeadId;
            HurtFragment(Run.Mass * g.Def.BlockMassFrac, g.Def.SpeciesId);
            _hitFrag = -1;
            GameFeel.Text(pPos + Vector2.up * 0.8f, "Осколок отбил удар!", new Color(0.85f, 0.9f, 1f), 1.0f);
            ProceduralSfx.Instance?.Play("glass");
            if (Run.IsDead) { EndRun(); return; }
        }
        if (_guardianParts[0] == null && _guardianParts[1] == null && _guardianParts[2] == null) {
            _guardian.Mass = g.Hp; // без деления тело стража несёт весь HP
            _guardian.ApplySize();
        }
        if (g.IsExposed && _guardian.View.Body != null)
            _guardian.View.Body.color = Color.Lerp(GameBootstrap.CDanger, Color.white, 0.4f + 0.3f * Mathf.Sin(_time * 8f));
    }

    private static string StrikeLabel(GuardianKind kind) {
        switch (kind) {
            case GuardianKind.Forge: return "Гейзер!";
            case GuardianKind.AbyssChoir: return "Импульс!";
            case GuardianKind.Grinder: return "Луч!";
            default: return "Корни бьют!";
        }
    }

    /// <summary>Урон и замедление зон стража по ведущему фрагменту (замедление применит ZoneStepOne).</summary>
    private void GuardianZonesStep(float dt, Vector2 pPos, GuardianOutput o) {
        if (o.Zones == null) return;
        for (int i = 0; i < o.Zones.Length && i < _guardianZones.Length; i++) {
            var z = o.Zones[i];
            if (z.Radius <= 0.0 || z.Life <= 0.0) continue;
            var zp = new Vector2((float)z.Pos.X, (float)z.Pos.Y);
            if (Vector2.Distance(pPos, zp) > (float)z.Radius) continue;
            _guardianZoneSlow = Mathf.Max(_guardianZoneSlow, (float)z.Slow01);
            _hitFrag = Run.Fragments.LeadId;
            HurtFragment(z.DamagePerSec * dt, Run.Guardian.Def.SpeciesId);
            _hitFrag = -1;
            if (Run.IsDead) { EndRun(); return; }
        }
    }

    /// <summary>Красное кольцо телеграфа: растёт от нуля до полного радиуса за окно предупреждения.</summary>
    private void GuardianTelegraphStep(GuardianOutput o) {
        bool show = o.Telegraph.Seconds > 0.0 && o.Telegraph.Radius > 0.0;
        if (!show) { if (_guardianTelegraph != null) _guardianTelegraph.enabled = false; return; }
        if (_guardianTelegraph == null) {
            var go = new GameObject("guardian_telegraph");
            go.transform.SetParent(_roomRoot, false);
            _guardianTelegraph = SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, 1f, 1f,
                new Color(1f, 0.25f, 0.2f, 0.85f), DarknessMask.Order + 9);
        }
        _guardianTelegraph.enabled = true;
        _guardianTelegraph.transform.position = new Vector3((float)o.Telegraph.Pos.X, (float)o.Telegraph.Pos.Y, 0f);
        float full = (float)o.Telegraph.Radius * 2f;
        double window = o.Telegraph.Window > 0.0 ? o.Telegraph.Window
            : (Run.Guardian != null && Run.Guardian.Def.RootTelegraph > 0.0 ? Run.Guardian.Def.RootTelegraph : 0.8);
        float frac = 1f - Mathf.Clamp01((float)(o.Telegraph.Seconds / window));
        _guardianTelegraph.transform.localScale = new Vector3(full * frac, full * frac, 1f);
    }

    /// <summary>v0.12: до трёх красных колец телеграфов (гейзеры Горнила); пустые слоты гасятся.</summary>
    private void GuardianTelegraphsStep(GuardianOutput o) {
        var list = o.Telegraphs;
        for (int i = 0; i < _guardianTelegraphs.Length; i++) {
            var sr = _guardianTelegraphs[i];
            bool show = list != null && i < list.Length && list[i].Seconds > 0.0 && list[i].Radius > 0.0;
            if (!show) { if (sr != null) sr.enabled = false; continue; }
            if (sr == null) {
                var go = new GameObject("guardian_telegraph" + i);
                go.transform.SetParent(_roomRoot, false);
                sr = SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, 1f, 1f,
                    new Color(1f, 0.35f, 0.15f, 0.85f), DarknessMask.Order + 9);
                _guardianTelegraphs[i] = sr;
            }
            var t = list[i];
            sr.enabled = true;
            sr.transform.position = new Vector3((float)t.Pos.X, (float)t.Pos.Y, 0f);
            float full = (float)t.Radius * 2f;
            double window = t.Window > 0.0 ? t.Window : (t.Seconds > 0.0 ? t.Seconds : 1.0);
            float frac = 1f - Mathf.Clamp01((float)(t.Seconds / window));
            sr.transform.localScale = new Vector3(full * frac, full * frac, 1f);
        }
    }

    /// <summary>v0.12: кипящее кольцо Горнила — оранжевая граница арены, сжимается по ArenaRadiusNow.</summary>
    private void DrawGuardianRing(GuardianOutput o, Vector2 gPos) {
        bool show = o.ArenaRadiusNow > 0.01;
        if (!show) { if (_guardianRing != null) _guardianRing.enabled = false; return; }
        if (_guardianRing == null) {
            var go = new GameObject("guardian_ring");
            go.transform.SetParent(_roomRoot, false);
            _guardianRing = SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, 1f, 1f,
                new Color(1f, 0.55f, 0.15f, 0.75f), DarknessMask.Order + 4);
        }
        _guardianRing.enabled = true;
        Vector2 c = o.Center.Length > 0.001 ? new Vector2((float)o.Center.X, (float)o.Center.Y) : gPos;
        _guardianRing.transform.position = c;
        float full = (float)o.ArenaRadiusNow * 2f;
        _guardianRing.transform.localScale = new Vector3(full, full, 1f);
    }

    /// <summary>v0.12: осколки Гранильщика на орбите.</summary>
    private void DrawGuardianShards(GuardianOutput o) {
        var list = o.Shards;
        for (int i = 0; i < _guardianShards.Length; i++) {
            var sr = _guardianShards[i];
            bool show = list != null && i < list.Length;
            if (!show) { if (sr != null) sr.enabled = false; continue; }
            if (sr == null) {
                var go = new GameObject("guardian_shard" + i);
                go.transform.SetParent(_roomRoot, false);
                sr = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 1f, 1f,
                    new Color(0.8f, 0.85f, 0.95f, 0.95f), DarknessMask.Order + 6);
                _guardianShards[i] = sr;
            }
            sr.enabled = true;
            sr.transform.position = new Vector3((float)list[i].X, (float)list[i].Y, 0f);
            float d = (float)Run.Guardian.ShardRadius * 2.4f;
            sr.transform.localScale = new Vector3(d, d, 1f);
        }
    }

    /// <summary>v0.12: луч Гранильщика — полоса-телеграф (тонкая → толстая), затем вспышка.</summary>
    private void DrawGuardianBeam(GuardianOutput o) {
        bool telegraph = o.Beam.Seconds > 0.0;
        bool show = telegraph || o.BeamFlash;
        if (!show) { if (_guardianBeam != null) _guardianBeam.enabled = false; return; }
        if (_guardianBeam == null) {
            var go = new GameObject("guardian_beam");
            go.transform.SetParent(_roomRoot, false);
            _guardianBeam = SpriteUtil.AddSprite(go.transform, SpriteUtil.SoftRectSprite, 1f, 1f,
                new Color(1f, 0.45f, 0.3f, 0.7f), DarknessMask.Order + 8);
        }
        _guardianBeam.enabled = true;
        Vector2 from = new Vector2((float)o.Beam.From.X, (float)o.Beam.From.Y);
        Vector2 to = new Vector2((float)o.Beam.To.X, (float)o.Beam.To.Y);
        Vector2 mid = (from + to) * 0.5f;
        Vector2 d = to - from;
        float len = d.magnitude;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        double window = Run.Guardian != null && Run.Guardian.Def.BeamTelegraph > 0.0 ? Run.Guardian.Def.BeamTelegraph : 0.9;
        float thick = o.BeamFlash
            ? (float)o.Beam.Width * 1.6f // вспышка удара
            : Mathf.Lerp(0.12f, (float)o.Beam.Width, 1f - Mathf.Clamp01((float)(o.Beam.Seconds / window)));
        _guardianBeam.transform.position = mid;
        _guardianBeam.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        _guardianBeam.transform.localScale = new Vector3(len, Mathf.Max(0.06f, thick), 1f);
        _guardianBeam.color = o.BeamFlash ? new Color(1f, 0.9f, 0.7f, 0.95f) : new Color(1f, 0.45f, 0.3f, 0.6f);
    }

    /// <summary>v0.12: полупрозрачные частицы по направлению течения Хора.</summary>
    private void DrawCurrentDots(Vec2 current) {
        var dir = new Vector2((float)current.X, (float)current.Y).normalized;
        Vector2 anchor = _player != null ? (Vector2)_player.transform.position : Vector2.zero;
        for (int i = 0; i < _currentDots.Length; i++) {
            var sr = _currentDots[i];
            if (sr == null) {
                var go = new GameObject("current_dot" + i);
                go.transform.SetParent(_roomRoot, false);
                sr = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 1f, 1f,
                    new Color(0.55f, 0.65f, 1f, 0.22f), DarknessMask.Order + 2);
                _currentDots[i] = sr;
            }
            sr.enabled = true;
            float phase = _time * 1.6f + i * 1.7f;
            Vector2 side = new Vector2(-dir.y, dir.x);
            sr.transform.position = anchor + dir * (1.2f + (phase % 1f) * 3.2f) + side * Mathf.Sin(phase * 2.4f) * 0.9f;
            sr.transform.localScale = new Vector3(0.24f, 0.24f, 1f);
        }
    }

    // ---------- капли Горнила (v0.12) ----------

    /// <summary>Слот капли по телу; −1 если это не капля.</summary>
    private int GuardianPartIndex(RunCreature c) {
        for (int i = 0; i < _guardianParts.Length; i++) if (_guardianParts[i] == c) return i;
        return -1;
    }

    /// <summary>Капли Горнила: домен ведёт позиции, Unity держит тела и наносит урон по partId.</summary>
    private void GuardianPartsStep(GuardianOutput o) {
        var g = Run.Guardian;
        if (o.Parts == null || g == null) { DespawnGuardianParts(); return; }
        int alive = 0;
        for (int i = 0; i < o.Parts.Length && i < _guardianParts.Length; i++)
            if (o.Parts[i].Hp > 0.0 && o.Parts[i].Radius > 0.0) alive++;
        if (alive == 0) { DespawnGuardianParts(); return; }
        if (alive == 1) {
            // осталась одна капля — она и есть страж: тело переносим на её место, капли убираем
            for (int i = 0; i < o.Parts.Length && i < _guardianParts.Length; i++) {
                if (o.Parts[i].Hp <= 0.0) continue;
                DespawnGuardianParts();
                if (_guardian != null)
                    _guardian.View.Rb.position = new Vector2((float)o.Parts[i].Pos.X, (float)o.Parts[i].Pos.Y);
                return;
            }
        }
        for (int i = 0; i < o.Parts.Length && i < _guardianParts.Length; i++) {
            var p = o.Parts[i];
            if (p.Hp <= 0.0 || p.Radius <= 0.0) {
                if (_guardianParts[i] != null) { KillCreature(_guardianParts[i]); _guardianParts[i] = null; _guardianPartIds[i] = 0; }
                continue;
            }
            if (_guardianParts[i] == null) {
                var c = SpawnCreature(new SpawnSpec { SpeciesId = g.Def.SpeciesId, Pos = p.Pos, Mass = p.Hp }, 9002 + i);
                if (c == null) continue;
                c.IsBoss = true; // капля — часть стража: не спит, не съедается
                c.View.Rb.linearDamping = 1.6f;
                _guardianParts[i] = c;
                _guardianPartIds[i] = p.Id;
            }
            var pc = _guardianParts[i];
            pc.View.Rb.position = new Vector2((float)p.Pos.X, (float)p.Pos.Y); // домен ведёт каплю
            pc.Mass = p.Hp;
            pc.ApplySize();
        }
    }

    private void DespawnGuardianParts() {
        for (int i = 0; i < _guardianParts.Length; i++) {
            var c = _guardianParts[i];
            if (c != null && c) KillCreature(c); // c == false — тело уже разрушено с комнатой
            _guardianParts[i] = null;
            _guardianPartIds[i] = 0;
        }
    }

    /// <summary>Контакт героя с каплей Горнила: урон конкретной капле (partId), ответка — как у стража.</summary>
    private void OnGuardianPartContact(RunCreature c, float rel, Vector2 point) {
        var g = Run.Guardian;
        if (g == null) return;
        int slot = GuardianPartIndex(c);
        if (slot < 0) return;
        int partId = _guardianPartIds[slot];
        double fragMass = Run.FragmentMass(_hitFrag >= 0 ? _hitFrag : Run.Fragments.LeadId);
        if (g.IsExposed && fragMass >= g.Hp * 0.6) { // обнажённого стража поглощаем с любой капли
            Run.OnBossAbsorbed();
            CodexOnAbsorb(g.Def.SpeciesId);
            GameFeel.HitStop(0.25f);
            GameFeel.Shake(1f);
            _ui.Banner(g.Def.Name + " поглощён! Шахта открыта", 4f);
            ProceduralSfx.Instance?.Play("capture");
            SplashBurst2D.Instance?.Burst(c.transform.position, Vector2.up, 20);
            KillCreature(c);
            _guardianParts[slot] = null;
            DespawnGuardianParts();
            if (_guardian != null) KillCreature(_guardian);
            _guardian = null;
            HideGuardianVisuals();
            return;
        }
        double loss = g.ApplyHit(rel, fragMass, Run.Mutations, _ricochets, partId);
        if (loss > 0) {
            Run.OnHit(loss);
            if (Run.Mutations.LifestealFrac > 0) Run.OnLifesteal(loss);
            SpawnDroplets(point, loss * 0.6); // массу капли синхронизирует GuardianPartsStep из выхода домена
            GameFeel.Text(c.transform.position, "−" + loss.ToString("0.0"), new Color(1f, 0.8f, 0.3f), 1.3f);
            GameFeel.Shake(0.55f);
            GameFeel.HitStop(0.09f);
            ProceduralSfx.Instance?.Play("weaken");
            if (g.IsExposed) _ui.Banner(g.Def.Name + " обнажён — поглотите его!", 3f);
        } else if (rel > 1.5f) {
            GameFeel.Text(point, "Броня! Нужна скорость и рикошеты", new Color(0.7f, 0.85f, 1f), 0.8f);
            ProceduralSfx.Instance?.Play("glass", 0.9f, 0f, 0.45f);
        }
        // капля бьёт в ответ суммарной массой стража
        var res = SizeRule.Resolve(fragMass, Run.Mutations, g.Hp, new MutationSet(), rel);
        if (res.MassLossA > 0 && !g.IsExposed) {
            HurtFragment(res.MassLossA, g.Def.SpeciesId);
            GameFeel.Text(_player.transform.position, "−" + res.MassLossA.ToString("0.0"), GameBootstrap.CDanger);
            if (Run.IsDead) EndRun();
        }
    }

    /// <summary>Полупрозрачные круги зон спор.</summary>
    private void DrawGuardianZones(GuardianOutput o) {
        if (o.Zones == null) return;
        for (int i = 0; i < o.Zones.Length && i < _guardianZones.Length; i++) {
            var z = o.Zones[i];
            var sr = _guardianZones[i];
            if (z.Radius <= 0.0 || z.Life <= 0.0) { if (sr != null) sr.enabled = false; continue; }
            if (sr == null) {
                var go = new GameObject("guardian_zone");
                go.transform.SetParent(_roomRoot, false);
                sr = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 1f, 1f,
                    new Color(0.45f, 1f, 0.5f, 0.16f), DarknessMask.Order + 3);
                _guardianZones[i] = sr;
            }
            sr.enabled = true;
            sr.transform.position = new Vector3((float)z.Pos.X, (float)z.Pos.Y, 0f);
            sr.transform.localScale = new Vector3((float)z.Radius * 2f, (float)z.Radius * 2f, 1f);
        }
    }

    private void HideGuardianVisuals() {
        if (_guardianTelegraph != null) _guardianTelegraph.enabled = false;
        if (_guardianWeakRing != null) _guardianWeakRing.enabled = false;
        for (int i = 0; i < _guardianZones.Length; i++) if (_guardianZones[i] != null) _guardianZones[i].enabled = false;
        // v0.12
        for (int i = 0; i < _guardianTelegraphs.Length; i++) if (_guardianTelegraphs[i] != null) _guardianTelegraphs[i].enabled = false;
        if (_guardianRing != null) _guardianRing.enabled = false;
        if (_guardianBeam != null) _guardianBeam.enabled = false;
        for (int i = 0; i < _guardianShards.Length; i++) if (_guardianShards[i] != null) _guardianShards[i].enabled = false;
        for (int i = 0; i < _currentDots.Length; i++) if (_currentDots[i] != null) _currentDots[i].enabled = false;
    }

    private void ClearGuardianVisuals() {
        HideGuardianVisuals();
        for (int i = 0; i < _guardianZones.Length; i++) {
            if (_guardianZones[i] != null) Destroy(_guardianZones[i].gameObject);
            _guardianZones[i] = null;
        }
        if (_guardianTelegraph != null) { Destroy(_guardianTelegraph.gameObject); _guardianTelegraph = null; }
        _guardianWeakRing = null;
        // v0.12
        for (int i = 0; i < _guardianTelegraphs.Length; i++) {
            if (_guardianTelegraphs[i] != null) Destroy(_guardianTelegraphs[i].gameObject);
            _guardianTelegraphs[i] = null;
        }
        if (_guardianRing != null) { Destroy(_guardianRing.gameObject); _guardianRing = null; }
        if (_guardianBeam != null) { Destroy(_guardianBeam.gameObject); _guardianBeam = null; }
        for (int i = 0; i < _guardianShards.Length; i++) {
            if (_guardianShards[i] != null) Destroy(_guardianShards[i].gameObject);
            _guardianShards[i] = null;
        }
        for (int i = 0; i < _currentDots.Length; i++) {
            if (_currentDots[i] != null) Destroy(_currentDots[i].gameObject);
            _currentDots[i] = null;
        }
        DespawnGuardianParts();
        _guardianDashLock = 0f;
        _playerDashed = false;
    }

    private void SpawnGuardianAt(Vec2 pos) {
        var g = Run.Guardian;
        if (g == null) return;
        var c = SpawnCreature(new SpawnSpec { SpeciesId = g.Def.SpeciesId, Pos = pos, Mass = g.Hp }, 9001);
        if (c == null) return;
        c.IsBoss = true; // общий путь стража: не спит, не съедается, контакт через OnGuardianContact
        c.View.Rb.linearDamping = 1.6f;
        var tint = RunWorldBuilder.Hex(g.Def.Tint);
        var glow = new Color(tint.r, tint.g, tint.b, 0.45f);
        SpriteUtil.AddSprite(c.transform, SpriteUtil.GlowSprite, c.Radius * 4.5f / c.transform.localScale.x,
            c.Radius * 4.5f / c.transform.localScale.x, glow, DarknessMask.Order + 1);
        _guardianWeakRing = SpriteUtil.AddSprite(c.transform, SpriteUtil.RingSprite,
            c.Radius * 2.6f / c.transform.localScale.x, c.Radius * 2.6f / c.transform.localScale.x,
            new Color(0.4f, 1f, 0.5f, 0.9f), DarknessMask.Order + 5);
        _guardianWeakRing.enabled = false;
        AddLabel(c.transform, (float)SizeRule.RadiusForMass(g.Hp) / c.transform.localScale.x,
            g.Def.Name, tint);
        _guardian = c;
    }

    private void OnGuardianContact(RunCreature c, float rel, Vector2 point) {
        var g = Run.Guardian;
        if (g == null) return;
        double fragMass = Run.FragmentMass(_hitFrag >= 0 ? _hitFrag : Run.Fragments.LeadId);
        if (g.IsExposed && fragMass >= g.Hp * 0.6) {
            Run.OnBossAbsorbed(); // Domain: награда по глубине, жетон, выход открыт
            CodexOnAbsorb(g.Def.SpeciesId);
            GameFeel.HitStop(0.25f);
            GameFeel.Shake(1f);
            _ui.Banner(g.Def.Name + " поглощён! Шахта открыта", 4f);
            ProceduralSfx.Instance?.Play("capture");
            SplashBurst2D.Instance?.Burst(c.transform.position, Vector2.up, 20);
            KillCreature(c);
            _guardian = null;
            HideGuardianVisuals();
            return;
        }
        double loss = g.ApplyHit(rel, fragMass, Run.Mutations, _ricochets);
        if (loss > 0) {
            Run.OnHit(loss);
            if (Run.Mutations.LifestealFrac > 0) Run.OnLifesteal(loss);
            c.Mass = g.Hp;
            c.ApplySize();
            SpawnDroplets(point, loss * 0.6);
            GameFeel.Text(c.transform.position, "−" + loss.ToString("0.0"), new Color(1f, 0.8f, 0.3f), 1.3f);
            GameFeel.Shake(0.55f);
            GameFeel.HitStop(0.09f);
            ProceduralSfx.Instance?.Play("weaken");
            if (g.IsExposed) _ui.Banner(g.Def.Name + " обнажён — поглотите его!", 3f);
        } else if (rel > 1.5f) {
            GameFeel.Text(point, "Броня! Нужна скорость и рикошеты", new Color(0.7f, 0.85f, 1f), 0.8f);
            ProceduralSfx.Instance?.Play("glass", 0.9f, 0f, 0.45f);
        }
        // страж бьёт в ответ своим Attack (v0.14: процент массы героя, границы 3–25%)
        var res = SizeRule.Resolve(fragMass, Run.Mutations, g.Hp, new MutationSet(), rel,
            0.11, 1.2, Run.Depth);
        if (res.MassLossA > 0 && !g.IsExposed) {
            HurtFragment(res.MassLossA, g.Def.SpeciesId);
            GameFeel.Text(_player.transform.position, "−" + res.MassLossA.ToString("0.0"), GameBootstrap.CDanger);
            if (Run.IsDead) EndRun();
        }
    }

    private void ZoneStep(float dt, Vector2 leadPos, float leadR) {
        for (int f = 0; f < _fragList.Count; f++) {
            var fv = _fragList[f];
            if (fv.View == null) continue;
            _hitFrag = fv.Id;
            ZoneStepOne(dt, fv.View.Rb, fv.View.Rb.position, FragRadius(fv));
            if (!InRun) return;
        }
        _hitFrag = -1;
    }

    private void ZoneStepOne(float dt, Rigidbody2D body, Vector2 pPos, float pr) {
        float extraDamping = 0f;
        for (int i = 0; i < _obstacles.Count; i++) {
            var o = _obstacles[i];
            if (o == null || o.Solid) continue;
            Vector2 d = pPos - (Vector2)o.transform.position;
            float dist = d.magnitude;
            if (dist > o.Radius + pr * 0.5f) continue; // дёшево: одна дистанция на зону
            double perSec = ObstacleRules.ZoneMassPerSec(o.Kind, Run.Mass, Run.Mutations);
            if (perSec > 0) Run.Heal(perSec * dt);
            else if (perSec < 0) {
                HurtFragment(-perSec * dt, "vent");
                if (Run.IsDead) { EndRun(); return; }
                if (body == null) return; // фрагмент растворился
            }
            extraDamping = Mathf.Max(extraDamping, (float)ObstacleRules.ZoneDamping(o.Kind));
            if (o.Kind == ObstacleKind.Vent && dist > 0.01f)
                body.AddForce(d / dist * ((float)ObstacleRules.VentPush(dist, o.Radius) * body.mass), ForceMode2D.Force);
        }
        if (body == _player.Rb) extraDamping = Mathf.Max(extraDamping, _guardianZoneSlow); // v0.11: споры стража тормозят
        body.linearDamping = PlayerBaseDamping + extraDamping;
    }

    private void ToxicStep(float dt, Vector2 pPos, float pr) {
        double tox = Run.Mutations.TrailDamagePerSec;
        if (tox <= 0 || _player.Rb.linearVelocity.magnitude <= 1.5f) return;
        Vector2 back = pPos - _player.Rb.linearVelocity.normalized * (pr + 0.6f);
        for (int i = _creatures.Count - 1; i >= 0; i--) {
            var c = _creatures[i];
            if (c.IsFood || c.IsBoss || !c.gameObject.activeSelf || Vector2.Distance(back, c.View.Rb.position) > c.Radius + 0.7f) continue;
            c.Mass -= tox * dt;
            if (c.Mass < 0.35) KillCreature(c); else c.ApplySize();
        }
    }

    private void AltarStep(Vector2 pPos, float pr) {
        var layout = Run.Layout;
        if (layout == null || _altarRing == null || Run.AltarUsed) return;
        if (Vector2.Distance(pPos, ToV2(layout.AltarPos)) > pr + 0.8f) return;
        double before = Run.Mass;
        Run.UseAltar();
        ProceduralSfx.Instance?.Play("altar");
        GameFeel.Shake(0.2f);
        if (layout.Kind == RoomKind.Spring)
            GameFeel.Text(_player.transform.position, "+" + (Run.Mass - before).ToString("0.0") + " родник", new Color(0.5f, 0.85f, 1f), 1.2f);
        var c = _altarRing.color; c.a = 0.15f; _altarRing.color = c;
    }

    void Update() {
        AudioStep();
        if (!InRun) return;
        if (!_ui.ModalOpen) Run.Tick(Time.deltaTime);
        ApplyPlayerSize();
        var biome = Run.Layout != null ? Run.Layout.Biome : null;
        _dark.Set((float)(biome != null ? biome.Darkness01 : Run.Room.Darkness01),
                  (float)Run.LightRadius * (Run.OpenWorld ? 1.8f : 1f));
        UpdateExitVisual();
        if (!_ui.ModalOpen) TwoFingerStep();
        if (InRun) PuppetTapStep();   // v0.13: тап по клону — нить «Кукловода»
        if (InRun) TwinGlowStep();
        if (InRun && Run.CompletedBiomesCount > _biomesShown) {
            // v0.10: область изучена целиком — праздник и эссенция
            _biomesShown = Run.CompletedBiomesCount;
            _ui.Banner("«" + Run.LastBiomeCompleted + "» изучена целиком! +" + RunState.BiomeCompletionEssence.ToString("0") + " эссенции души", 3.5f);
            ProceduralSfx.Instance?.Play("evolve");
            GameFeel.Shake(0.2f);
            SplashBurst2D.Instance?.Burst(_player.transform.position, Vector2.up, 16);
        }
        CodexStep();
        if (Run.Evolution != null && Run.Evolution.FreeTokens > _lastTokens) {
            _ui.Banner("Жетон эволюции! Откройте «Эволюцию» и выберите путь", 3f);
            ProceduralSfx.Instance?.Play("evolve");
        }
        if (Run.Evolution != null) _lastTokens = Run.Evolution.FreeTokens;
        if (!_splitHinted && SplitUnlocked) { _splitHinted = true; _ui.Hint("Деление открыто! Кнопка «Разделиться» справа", 3.5f); }
        if (!_soulHinted && Run.HasAbility("soul_transfer")) { _soulHinted = true; _ui.Hint("Перенос души: кнопка «Душа ⇄» переселит вас в клона", 3.5f); }
        if (_world != null) {
            _miniTimer -= Time.unscaledDeltaTime;
            if (_miniTimer <= 0f) { _miniTimer = 0.2f; _world.FlushMini(); }
        }
        UpdateEcho();
        UpdateRings();
        UpdateHpArcs();   // v0.13: дуга-шкала HP вокруг не-съедобных существ
        UpdateShockwave();
        if (Run.IsDead) { EndRun(); return; }
        if (Run.PendingOffer != null && !_offerShown) {
            _offerShown = true;
            GameFeel.CancelHitStop();
            Time.timeScale = 0f;
            PullGestureInput.Blocked = true;
            ProceduralSfx.Instance?.Play("evolve");
            _ui.ShowOffer(Run.PendingOffer.Options, id => {
                Run.ChooseMutation(id);
                _offerShown = false;
                Time.timeScale = 1f;
                PullGestureInput.Blocked = false;
                ApplyPlayerSize();
            });
        }
        // защита: план комнаты сменился не через выход (не должно случаться) — пересобрать
        if (Run.Room != null && Run.Room.Depth * 100 + Run.Room.RoomIndex != _builtRoomKey) BuildRoom();
    }

    /// <summary>Кольца: зелёное — ближайший фрагмент может съесть существо, красное — существо съест его.</summary>
    private void UpdateRings() {
        for (int i = 0; i < _creatures.Count; i++) {
            var c = _creatures[i];
            if (c == null || c.Ring == null || !c.gameObject.activeSelf) continue;
            var fv = NearestFragment(c.transform.position, out float d);
            if (fv == null) continue;
            double fm = Run.FragmentMass(fv.Id);
            Color col;
            if (fm >= c.Mass * Run.Mutations.AbsorbRatio) col = new Color(0.4f, 1f, 0.5f, 0.7f);
            else if (c.Mass >= fm * 1.25 || c.IsBoss) col = new Color(1f, 0.35f, 0.3f, 0.75f);
            else col = new Color(1f, 0.85f, 0.4f, 0.45f);
            if (c.IsShimmering) {
                // мерцание: кольцо переливается бирюзой, сияние дышит
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f + i);
                col = Color.Lerp(col, new Color(0.55f, 1f, 1f, 0.9f), 0.35f + 0.4f * t);
                if (c.Sparkle != null) {
                    c.Sparkle.color = new Color(0.55f, 1f, 1f, 0.3f + 0.35f * t);
                    float s = 0.9f + 0.2f * t;
                    c.Sparkle.transform.localScale = new Vector3(s, s, 1f);
                }
            }
            c.Ring.color = col;
        }
    }

    private RunAudio _audio;
    private bool _splitHinted, _soulHinted;
    private int _biomesShown;

    /// <summary>
    /// v0.13: тонкая дуга-шкала HP снаружи кольца угрозы у каждого не-съедобного существа.
    /// Заполнение = Mass / MaxMass по часовой от верха; у стражей и капель Горнила — всегда,
    /// у остальных — когда ранены. Цвет: зелёный &gt;60%, жёлтый 30–60%, красный &lt;30%.
    /// 32 спрайта закэшированы (HpArcLib) — в кадре только выбор спрайта и цвет.
    /// </summary>
    private void UpdateHpArcs() {
        for (int i = 0; i < _creatures.Count; i++) {
            var c = _creatures[i];
            if (c == null || c.IsFood) continue;
            if (c.HpArc == null) {
                c.HpArc = SpriteUtil.AddSprite(c.transform, HpArcLib.Get(1f), 1f, 1f,
                    Color.white, DarknessMask.Order + 5);
                c.HpArc.transform.localPosition = Vector3.zero;
            }
            float frac = c.MaxMass > 0.001 ? Mathf.Clamp01((float)(c.Mass / c.MaxMass)) : 1f;
            bool show = (c.IsBoss || frac < 0.995f) && c.gameObject.activeSelf;
            c.HpArc.enabled = show;
            if (!show) continue;
            c.HpArc.sprite = HpArcLib.Get(frac);
            var col = HpArcLib.ColorFor(frac);
            col.a = 0.95f;
            c.HpArc.color = col;
            // дуга шире кольца угрозы (r×2.5): диаметр = r×2.7; спрайт 1 юнит,
            // родительский масштаб (r/BaseRadius) компенсируем
            float worldD = (float)SizeRule.RadiusForMass(c.Mass) * 2.7f;
            float local = worldD / Mathf.Max(0.0001f, c.transform.localScale.x);
            c.HpArc.transform.localScale = new Vector3(local, local, 1f);
        }
    }

    /// <summary>Свет для 3D-моделей (тёплый, сверху-сбоку) и фоновая музыка пещеры.</summary>
    private void SetupLightAndMusic() {
        var lightGo = new GameObject("sun");
        lightGo.transform.SetParent(transform, false);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.95f, 0.86f);
        l.intensity = 1.5f;
        l.shadows = LightShadows.None;
        lightGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.35f, -0.55f, 1f));
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.68f, 0.68f, 0.78f);
        _audio = RunAudio.Create(transform);
    }

    /// <summary>Слои атмосферы: насколько близко опасность и кто стрекочет рядом.</summary>
    private void AudioStep() {
        if (_audio == null) return;
        if (!InRun || _player == null) { _audio.Tick(Vector2.zero, 0f, null, true); return; }
        Vector2 lead = _player.Rb.position;
        double leadMass = Run.Fragments.Count > 0 ? Run.Fragments[0].Mass : Run.Mass;
        float danger = 0f;
        for (int i = 0; i < _creatures.Count; i++) {
            var c = _creatures[i];
            if (c == null || c.IsFood || !c.gameObject.activeSelf || c.Mass <= leadMass) continue;
            float d = Vector2.Distance(lead, c.View.Rb.position) - c.Radius - PlayerRadius;
            danger = Mathf.Max(danger, 1f - d / 7f);
        }
        Vector2? near = null;
        if (_creatures.Count > 0) {
            var c = _creatures[Random.Range(0, _creatures.Count)];
            if (c != null && !c.IsFood && c.gameObject.activeSelf) near = c.View.Rb.position;
        }
        _audio.Tick(lead, danger, near, _ui.ModalOpen);
    }

    private void UpdateEcho() {
        double sense = Run.Mutations.SenseRadius;
        float light = (float)Run.LightRadius;
        Vector2 p = _player.transform.position;
        for (int i = 0; i < _creatures.Count; i++) {
            var c = _creatures[i];
            if (c == null || c.EchoDot == null) continue;
            float d = Vector2.Distance(p, c.transform.position);
            bool show = sense > 0 && d > light * 0.8f && d <= sense;
            c.EchoDot.enabled = show;
            if (show) { var col = c.EchoDot.color; col.a = 0.3f + 0.25f * Mathf.Sin(Time.time * 5f + i); c.EchoDot.color = col; }
        }
    }

    private void UpdateShockwave() {
        if (_shockRing == null || _shockT < 0f) return;
        _shockT += Time.deltaTime;
        float k = _shockT / 0.6f;
        if (k >= 1f) { _shockT = -1f; var c0 = _shockRing.color; c0.a = 0f; _shockRing.color = c0; return; }
        float s = Mathf.Lerp(1f, 10f, k);
        _shockRing.transform.localScale = new Vector3(s, s, 1f);
        var c = _shockRing.color; c.a = 0.8f * (1f - k); _shockRing.color = c;
    }

    private void UpdateExitVisual() {
        bool open = Run.ExitOpen;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        var c = open ? GameBootstrap.CFood : new Color(0.4f, 0.5f, 0.55f);
        c.a = open ? 0.6f + 0.4f * pulse : 0.25f;
        if (_exitRing != null) _exitRing.color = c;
        if (_exitGlow != null) { var g = c; g.a = open ? 0.35f + 0.2f * pulse : 0.06f; _exitGlow.color = g; }
        if (_exitPointer != null) {
            _exitPointer.gameObject.SetActive(open);
            if (open) {
                Vector2 p = _player.transform.position;
                Vector2 dir = (ToV2(Run.Room.ExitPos) - p).normalized;
                _exitPointer.transform.position = p + dir * (PlayerRadius + 0.55f);
            }
        }
        for (int i = 0; i < _altarRings.Count; i++) {
            if (_altarRings[i] == null || Run.IsAltarUsed(i)) continue;
            float s2 = 1.4f + 0.15f * pulse;
            _altarRings[i].transform.localScale = new Vector3(s2, s2, 1f);
        }
        if (_altarRing != null && !Run.AltarUsed) {
            float s = 1.4f + 0.15f * pulse;
            _altarRing.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    // ---------- Сервис ----------
    private static Vector3 ToV3(Vec2 v) => new Vector3((float)v.X, (float)v.Y, 0f);
    private static Vector2 ToV2(Vec2 v) => new Vector2((float)v.X, (float)v.Y);

    private static void SetupCameraAndEvents() {
        var cam = Camera.main;
        if (cam == null) {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.transform.position = new Vector3(0, 0, -10);
        }
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.015f, 0.03f);
        if (Object.FindFirstObjectByType<EventSystem>() == null) {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    private static SoulMeta LoadMeta() {
        string json = PlayerPrefs.GetString(SoulPrefKey, "");
        var m = SoulMeta.FromJson(json);
        return m ?? new SoulMeta();
    }

    private void SaveMeta() {
        PlayerPrefs.SetString(SoulPrefKey, Meta.ToJson());
        PlayerPrefs.Save();
    }
}

/// <summary>Передаёт столкновения слизня в RunGame (OnCollisionEnter2D живёт только на теле).</summary>
public sealed class RunContactRelay : MonoBehaviour {
    public RunGame Game;
    public int FragmentId;
    void OnCollisionEnter2D(Collision2D col) { if (Game != null) Game.OnPlayerCollision(col, FragmentId); }
}
}
