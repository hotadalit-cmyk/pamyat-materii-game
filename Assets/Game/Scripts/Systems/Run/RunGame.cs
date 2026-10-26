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
public sealed class RunGame : MonoBehaviour {
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

    private SimEntityView2D _player;
    private float _playerBaseRadius;
    private PhysicsMaterial2D _playerMat;
    private Transform _roomRoot;
    private readonly List<RunCreature> _creatures = new List<RunCreature>();
    private readonly List<RunObstacle> _obstacles = new List<RunObstacle>();
    private SpriteRenderer _exitRing, _exitGlow, _exitPointer, _altarRing;
    private RunCreature _boss;
    private SpriteRenderer _shockRing;
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

    void Awake() {
        Instance = this;
        Application.targetFrameRate = 60;
        Meta = LoadMeta();
        SetupCameraAndEvents();
        var input = new GameObject("pull_input");
        input.transform.SetParent(transform, false);
        input.AddComponent<PullGestureInput>();
        PullGestureInput.ReleaseHandler = OnDashRelease;
        var sfx = new GameObject("procedural_sfx");
        sfx.transform.SetParent(transform, false);
        sfx.AddComponent<ProceduralSfx>();
        var splash = new GameObject("splash_burst");
        splash.transform.SetParent(transform, false);
        splash.AddComponent<SplashBurst2D>();
        GameFeel.Create(transform);
        _dark = DarknessMask.Create(transform);
        _ui = RunUi.Build(this);
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
    public void StartNewRun() {
        _ui.HideImprint();
        Time.timeScale = 1f;
        PullGestureInput.Blocked = false;
        _seed = (ulong)System.DateTime.UtcNow.Ticks;
        _rng = new PmRandom(_seed ^ 0x5DEECE66DUL);
        Run = new RunState(Meta, _seed);
        _ended = false;
        _offerShown = false;
        _lastHitSource = null;
        _builtRoomKey = -1;
        _lastDepthShown = 0;
        EnsurePlayer();
        _player.gameObject.SetActive(true);
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
        _player.gameObject.SetActive(false);
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

    public void UpgradeSoul(SoulStat stat) {
        if (Meta.TryUpgrade(stat)) {
            SaveMeta();
            ProceduralSfx.Instance?.Play("unlock");
        } else ProceduralSfx.Instance?.Play("hit");
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
        if (_roomRoot != null) Destroy(_roomRoot.gameObject);
        _creatures.Clear();
        _obstacles.Clear();
        _boss = null;
        _altarRing = null;
        _roomRoot = new GameObject("room").transform;
        _roomRoot.SetParent(transform, false);
        var plan = Run.Room;
        var layout = Run.Layout;
        var biome = layout != null ? layout.Biome : BiomeCatalog.ForDepth(plan.Depth);
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

        _player.transform.position = ToV3(plan.PlayerStart);
        _player.Rb.position = ToV2(plan.PlayerStart);
        _player.Rb.linearVelocity = Vector2.zero;
        FollowCamera.Snap = true;
        _dark.Set((float)biome.Darkness01, (float)Run.Mutations.LightRadius);

        if (plan.Depth != _lastDepthShown) {
            _lastDepthShown = plan.Depth;
            _ui.BiomeCard(plan.Depth, biome.Name, biome.Subtitle, RunWorldBuilder.Hex(biome.AccentColor));
        } else if (layout != null && layout.Kind != RoomKind.Normal) {
            _ui.Banner(layout.Title, 2.4f);
            if (!string.IsNullOrEmpty(layout.Hint)) _ui.Flash(layout.Hint, 3f);
        } else _ui.Banner(string.Format("Комната {0}/{1}", plan.RoomIndex + 1, RunState.RoomsPerDepth), 1.2f);
        if (layout != null && layout.Kind == RoomKind.Boss) _ui.Banner("Страж глубины пробуждается…", 3f);
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
        if (!food && traits.Glows) {
            var g = ColorFor(sp, false); g.a = 0.5f;
            SpriteUtil.AddSprite(view.transform, SpriteUtil.GlowSprite, r * 4f, r * 4f, g, DarknessMask.Order + 1);
        }
        var c = view.gameObject.AddComponent<RunCreature>();
        c.IsNemesis = spec.IsNemesis;
        c.SoulName = spec.SoulName;
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

    private void SpawnBoss(RoomPlan plan) {
        var sp = SpeciesCatalog.Get(BossState.SpeciesId);
        var pos = new Vec2(plan.ExitPos.X * 0.35, plan.ExitPos.Y * 0.35); // ближе к центру, на пути к выходу
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
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = DarknessMask.Order + 10;
    }

    // ---------- Игрок ----------
    private void EnsurePlayer() {
        if (_player != null) {
            _player.Rb.linearVelocity = Vector2.zero;
            ApplyPlayerSize();
            return;
        }
        _playerBaseRadius = (float)SizeRule.RadiusForMass(4.0);
        _player = SimEntityView2D.Create(transform, "player_slime", SimEntityKind.Player, 1, _playerBaseRadius, GameBootstrap.CSlime);
        _player.Body.sortingOrder = 8;
        _player.Rb.linearDamping = PlayerBaseDamping;
        _playerMat = new PhysicsMaterial2D("player") { bounciness = 0.8f, friction = 0f };
        _player.GetComponent<CircleCollider2D>().sharedMaterial = _playerMat;
        VisualFactory.DecorateSlime(_player);
        var slime = _player.gameObject.AddComponent<SlimeVisual>();
        slime.BodyRoot = _player.Body.transform;
        slime.Membrane = _player.Membrane;
        slime.Core = _player.Core;
        _player.gameObject.AddComponent<RunContactRelay>().Game = this;
        var aim = new GameObject("aim_indicator");
        aim.transform.SetParent(_player.transform, false);
        aim.AddComponent<LineRenderer>();
        aim.AddComponent<AimIndicator>();
        _dark.Follow = _player.transform;
        var cam = Camera.main;
        if (cam != null) {
            var follow = cam.GetComponent<FollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<FollowCamera>(); // не «??»: Unity-null
            follow.Target = _player.transform;
        }
        ApplyPlayerSize();
    }

    private void ApplyPlayerSize() {
        float r = (float)SizeRule.RadiusForMass(Run.Mass);
        float s = r / _playerBaseRadius;
        _player.transform.localScale = new Vector3(s, s, 1f);
        _player.Rb.mass = Mathf.Max(0.1f, (float)Run.Mass);
        float b = (float)Run.Mutations.Bounciness;
        if (_playerMat != null && !Mathf.Approximately(_playerMat.bounciness, b)) {
            _playerMat.bounciness = b;
            var col = _player.GetComponent<CircleCollider2D>();
            col.sharedMaterial = null; col.sharedMaterial = _playerMat; // применить изменённый материал
        }
        // крупное тело — камера отъезжает, чтобы видеть больше пещеры
        FollowCamera.ZoomMult = Mathf.Clamp(0.75f + 0.125f * Mathf.Sqrt((float)Run.Mass), 0.95f, 1.6f);
    }

    private float PlayerRadius => (float)SizeRule.RadiusForMass(Run.Mass);

    private void OnDashRelease(Vec2 dir, double charge) {
        if (!InRun || _ui.ModalOpen) return;
        if (!Run.TryDash()) {
            _ui.Flash("Нет зарядов рывка — подождите");
            ProceduralSfx.Instance?.Play("hit");
            return;
        }
        _ricochets = 0;
        _sinceDash = 0f;
        float speed = DashSpeed * (float)Run.Mutations.DashSpeedMult * (float)charge;
        _player.Rb.linearVelocity += new Vector2((float)dir.X, (float)dir.Y) * speed;
        ProceduralSfx.Instance?.Play("jet");
        GameFeel.Shake(0.08f * (float)charge);
        // капля влаги остаётся позади — видно, что рывок стоит массы
        SplashBurst2D.Instance?.Burst(_player.transform.position, new Vector2((float)-dir.X, (float)-dir.Y), 3);
        if (Run.IsDead) { _lastHitSource = "dash"; EndRun(); }
    }

    // ---------- Контакты ----------
    public void OnPlayerCollision(Collision2D col) {
        if (!InRun) return;
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
        if (c.IsBoss) { OnBossContact(c, rel, point); return; }
        var res = SizeRule.Resolve(Run.Mass, Run.Mutations, c.Mass, c.Mutations, rel);
        ResolveContact(c, res, point, rel);
    }

    private void OnObstacleHit(RunObstacle o, float rel, Vector2 point) {
        switch (o.Kind) {
            case ObstacleKind.Thorns: {
                double loss = ObstacleRules.ThornLoss(rel, Run.Mutations);
                _lastHitSource = "thorns";
                Run.OnMassLost(loss, "thorns");
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
                    ProceduralSfx.Instance?.Play("unlock");
                    SplashBurst2D.Instance?.Burst(o.transform.position, Vector2.up, 10);
                    for (int i = 0; i < 3; i++) SpawnDroplet(o.transform.position, 0.4, i * 2.1f);
                    _obstacles.Remove(o);
                    Destroy(o.gameObject);
                } else ProceduralSfx.Instance?.Play("weaken");
                break;
            default:
                if (rel > 4f) GameFeel.Shake(0.06f);
                break;
        }
    }

    private System.Collections.IEnumerator Squash(Transform t) {
        if (t == null) yield break;
        var baseS = t.localScale;
        for (float k = 0; k < 1f; k += Time.deltaTime * 6f) {
            if (t == null) yield break;
            float s = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
            t.localScale = new Vector3(baseS.x * s, baseS.y * s, 1f);
            yield return null;
        }
        if (t != null) t.localScale = baseS;
    }

    private void ResolveContact(RunCreature c, ContactResult res, Vector2 point, float rel) {
        if (res.Absorbed == 1) { Absorb(c); return; }
        if (res.Absorbed == 2) {
            _lastHitSource = c.SourceId;
            Run.OnMassLost(Run.Mass, c.SourceId);
            EndRun();
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
            _lastHitSource = c.SourceId;
            Run.OnMassLost(res.MassLossA, c.SourceId);
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
        if (boss.IsExposed && Run.Mass >= boss.Mass * 0.6) {
            Run.OnBossAbsorbed();
            GameFeel.HitStop(0.25f);
            GameFeel.Shake(1f);
            _ui.Banner("Страж поглощён! Сердце пещеры открыто", 4f);
            ProceduralSfx.Instance?.Play("capture");
            SplashBurst2D.Instance?.Burst(c.transform.position, Vector2.up, 20);
            KillCreature(c);
            _boss = null;
            return;
        }
        double loss = boss.ApplyHit(rel, Run.Mass, Run.Mutations, _ricochets);
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
            ProceduralSfx.Instance?.Play("hit");
        }
        // страж бьёт в ответ своей массой
        var res = SizeRule.Resolve(Run.Mass, Run.Mutations, boss.Mass, new MutationSet(), rel);
        if (res.MassLossA > 0 && !boss.IsExposed) {
            _lastHitSource = BossState.SpeciesId;
            Run.OnMassLost(res.MassLossA, BossState.SpeciesId);
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

    private void Absorb(RunCreature c) {
        double bonus = Run.OnAbsorbEx(c.Species.Id, c.Mass);
        var pos = c.transform.position;
        if (c.IsNemesis) {
            Meta.Nemeses.OnSoulAbsorbed(c.SoulName);
            _ui.Banner(c.SoulName + " поглощена — её память теперь ваша", 3f);
        } else if (!c.IsFood) _ui.Flash("Поглощено: " + c.Species.Name);
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
        float pull = (1.6f + 0.45f * Mathf.Sqrt((float)Run.Mass)) * (float)Run.Mutations.PullRadiusMult;
        for (int i = _creatures.Count - 1; i >= 0; i--) {
            var c = _creatures[i];
            if (c == null) { _creatures.RemoveAt(i); continue; }
            if (c.IsBoss) continue;
            var rb = c.View.Rb;
            Vector2 d = pPos - rb.position;
            float dist = d.magnitude;
            if (c.IsFood) {
                // биогравитация: споры тянутся к слизню, касание — поглощение
                if (dist < pull && dist > 0.001f)
                    rb.AddForce(d / dist * (9f * rb.mass * (1f - dist / pull)), ForceMode2D.Force);
                if (dist <= pr + c.Radius * 0.6f) Absorb(c);
                continue;
            }
            bool touching = dist <= pr + c.Radius + 0.06f;
            // крупное, но уже меньше нас после роста — съедаем при касании без удара
            if (touching && Run.Mass >= c.Mass * Run.Mutations.AbsorbRatio) { Absorb(c); continue; }
            if (touching && c.Traits.LeechPerSec > 0) {
                double drain = c.Traits.LeechPerSec * dt;
                _lastHitSource = "leech";
                Run.OnMassLost(drain, "leech");
                c.Mass += drain;
                c.ApplySize();
                if (Run.IsDead) { EndRun(); return; }
            }
            var o = c.Brain.Think(new BrainInput {
                SelfPos = new Vec2(rb.position.x, rb.position.y), SelfMass = c.Mass, Behavior = c.Species.Behavior,
                PlayerPos = new Vec2(pPos.x, pPos.y), PlayerMass = Run.Mass, Time = _time
            });
            if (o.Impulse)
                rb.linearVelocity += new Vector2((float)o.Direction.X, (float)o.Direction.Y) * (CreatureSpeed * (float)o.Strength01);
        }
        BossStep(dt, pPos);
        ZoneStep(dt, pPos, pr);
        ToxicStep(dt, pPos, pr);
        AltarStep(pPos, pr);
        // выход
        if (Run.ExitOpen && Vector2.Distance(pPos, ToV2(Run.Room.ExitPos)) <= ExitRadius + pr) {
            if (Run.TryEnterExit()) {
                ProceduralSfx.Instance?.Play("unlock");
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
            ProceduralSfx.Instance?.Play("death");
            Vector2 away = pPos - rb.position;
            if (away.magnitude < 5f)
                _player.Rb.linearVelocity += away.normalized * (10f * (1f - away.magnitude / 5f) + 3f);
        }
        _boss.Mass = boss.Mass;
        _boss.ApplySize();
        if (boss.IsExposed && _boss.View.Body != null)
            _boss.View.Body.color = Color.Lerp(GameBootstrap.CDanger, Color.white, 0.4f + 0.3f * Mathf.Sin(_time * 8f));
    }

    private void ZoneStep(float dt, Vector2 pPos, float pr) {
        float extraDamping = 0f;
        for (int i = 0; i < _obstacles.Count; i++) {
            var o = _obstacles[i];
            if (o == null || o.Solid) continue;
            Vector2 d = pPos - (Vector2)o.transform.position;
            float dist = d.magnitude;
            if (dist > o.Radius + pr * 0.5f) continue;
            double perSec = ObstacleRules.ZoneMassPerSec(o.Kind, Run.Mass, Run.Mutations);
            if (perSec > 0) Run.Heal(perSec * dt);
            else if (perSec < 0) {
                _lastHitSource = "vent";
                Run.OnMassLost(-perSec * dt, "vent");
                if (Run.IsDead) { EndRun(); return; }
            }
            extraDamping = Mathf.Max(extraDamping, (float)ObstacleRules.ZoneDamping(o.Kind));
            if (o.Kind == ObstacleKind.Vent && dist > 0.01f)
                _player.Rb.AddForce(d / dist * ((float)ObstacleRules.VentPush(dist, o.Radius) * _player.Rb.mass), ForceMode2D.Force);
        }
        _player.Rb.linearDamping = PlayerBaseDamping + extraDamping;
    }

    private void ToxicStep(float dt, Vector2 pPos, float pr) {
        double tox = Run.Mutations.TrailDamagePerSec;
        if (tox <= 0 || _player.Rb.linearVelocity.magnitude <= 1.5f) return;
        Vector2 back = pPos - _player.Rb.linearVelocity.normalized * (pr + 0.6f);
        for (int i = _creatures.Count - 1; i >= 0; i--) {
            var c = _creatures[i];
            if (c.IsFood || c.IsBoss || Vector2.Distance(back, c.View.Rb.position) > c.Radius + 0.7f) continue;
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
        ProceduralSfx.Instance?.Play("unlock");
        GameFeel.Shake(0.2f);
        if (layout.Kind == RoomKind.Spring)
            GameFeel.Text(_player.transform.position, "+" + (Run.Mass - before).ToString("0.0") + " родник", new Color(0.5f, 0.85f, 1f), 1.2f);
        var c = _altarRing.color; c.a = 0.15f; _altarRing.color = c;
    }

    void Update() {
        if (!InRun) return;
        if (!_ui.ModalOpen) Run.Tick(Time.deltaTime);
        ApplyPlayerSize();
        var biome = Run.Layout != null ? Run.Layout.Biome : null;
        _dark.Set((float)(biome != null ? biome.Darkness01 : Run.Room.Darkness01), (float)Run.Mutations.LightRadius);
        UpdateExitVisual();
        UpdateEcho();
        UpdateShockwave();
        if (Run.IsDead) { EndRun(); return; }
        if (Run.PendingOffer != null && !_offerShown) {
            _offerShown = true;
            GameFeel.CancelHitStop();
            Time.timeScale = 0f;
            PullGestureInput.Blocked = true;
            ProceduralSfx.Instance?.Play("unlock");
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

    private void UpdateEcho() {
        double sense = Run.Mutations.SenseRadius;
        float light = (float)Run.Mutations.LightRadius;
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
    void OnCollisionEnter2D(Collision2D col) { if (Game != null) Game.OnPlayerCollision(col); }
}
}
