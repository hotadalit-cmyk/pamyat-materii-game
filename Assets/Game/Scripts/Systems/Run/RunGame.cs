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
/// Забег v0.4 «одна жизнь — один забег» (Docs/CONCEPT_v4.md). Правила — в Domain.Run
/// (RunState, SizeRule, RoomGenerator, SoulMeta); здесь только физика Unity, вид и ввод.
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

    public SoulMeta Meta { get; private set; }
    public RunState Run { get; private set; }

    private SimEntityView2D _player;
    private float _playerBaseRadius;
    private PhysicsMaterial2D _playerMat;
    private Transform _roomRoot;
    private readonly List<RunCreature> _creatures = new List<RunCreature>();
    private SpriteRenderer _exitRing, _exitGlow, _exitPointer;
    private DarknessMask _dark;
    private RunUi _ui;
    private PmRandom _rng;
    private ulong _seed;
    private bool _ended;
    private bool _offerShown;
    private string _lastHitSource;
    private bool _nemesisAbsorbed;
    private int _builtRoomKey = -1;
    private float _time;

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
        _dark = DarknessMask.Create(transform);
        _ui = RunUi.Build(this);
        StartNewRun();
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
        _nemesisAbsorbed = false;
        _lastHitSource = null;
        _builtRoomKey = -1;
        EnsurePlayer();
        _player.gameObject.SetActive(true);
        BuildRoom();
        _ui.Banner("Новая жизнь");
    }

    private void EndRun() {
        if (_ended) return;
        _ended = true;
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
                killer = "Причина гибели: " + (sp != null ? sp.Name : src);
            } else killer = "Масса иссякла.";
        }
        Meta.ApplyLife(imprint);
        SaveMeta();
        if (SplashBurst2D.Instance != null)
            SplashBurst2D.Instance.Burst(_player.transform.position, Vector2.up, 14);
        ProceduralSfx.Instance?.Play("death");
        _player.gameObject.SetActive(false);
        PullGestureInput.Blocked = true;
        _ui.ShowImprint(imprint, victory, killer);
    }

    public void UpgradeSoul(SoulStat stat) {
        if (Meta.TryUpgrade(stat)) {
            SaveMeta();
            ProceduralSfx.Instance?.Play("unlock");
        } else ProceduralSfx.Instance?.Play("hit");
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
        _roomRoot = new GameObject("room").transform;
        _roomRoot.SetParent(transform, false);
        var plan = Run.Room;
        _builtRoomKey = plan.Depth * 100 + plan.RoomIndex;
        float hw = (float)plan.HalfWidth, hh = (float)plan.HalfHeight;

        var glow = SpriteUtil.AddSprite(_roomRoot, SpriteUtil.GlowSprite, hw * 3.2f, hh * 3.6f, GameBootstrap.CGlow, -1);
        glow.name = "bg_glow";
        var floor = SpriteUtil.AddSprite(_roomRoot, SpriteUtil.SoftRectSprite, hw * 2f + 0.2f, hh * 2f + 0.2f,
            Color.Lerp(GameBootstrap.CStone, GameBootstrap.CBackground, 0.25f * (plan.Depth - 1)), 0);
        floor.name = "floor";
        MakeWall(new Vector2(0, hh + WallThickness * 0.5f), new Vector2(hw * 2f + WallThickness * 2f, WallThickness));
        MakeWall(new Vector2(0, -hh - WallThickness * 0.5f), new Vector2(hw * 2f + WallThickness * 2f, WallThickness));
        MakeWall(new Vector2(-hw - WallThickness * 0.5f, 0), new Vector2(WallThickness, hh * 2f));
        MakeWall(new Vector2(hw + WallThickness * 0.5f, 0), new Vector2(WallThickness, hh * 2f));

        // выход: кольцо + свечение поверх тьмы, указатель у слизня
        var exitGo = new GameObject("exit");
        exitGo.transform.SetParent(_roomRoot, false);
        exitGo.transform.position = ToV3(plan.ExitPos);
        _exitGlow = SpriteUtil.AddSprite(exitGo.transform, SpriteUtil.GlowSprite, 3.2f, 3.2f, GameBootstrap.CFood, DarknessMask.Order + 6);
        _exitRing = SpriteUtil.AddSprite(exitGo.transform, SpriteUtil.RingSprite, 1.6f, 1.6f, GameBootstrap.CFood, DarknessMask.Order + 7);
        _exitPointer = SpriteUtil.AddSprite(_roomRoot, SpriteUtil.CircleSprite, 0.22f, 0.22f, GameBootstrap.CFood, DarknessMask.Order + 8);

        for (int i = 0; i < plan.Spawns.Count; i++) SpawnCreature(plan.Spawns[i], i);

        _player.transform.position = ToV3(plan.PlayerStart);
        _player.Rb.position = new Vector2((float)plan.PlayerStart.X, (float)plan.PlayerStart.Y);
        _player.Rb.linearVelocity = Vector2.zero;
        _dark.Set((float)plan.Darkness01, (float)Run.Mutations.LightRadius);
        if (plan.RoomIndex == 0 && plan.Depth > 1) _ui.Banner("Глубина " + plan.Depth);
        else _ui.Banner(string.Format("Комната {0}/{1}", plan.RoomIndex + 1, RunState.RoomsPerDepth), 1.4f);
    }

    private void MakeWall(Vector2 pos, Vector2 size) {
        var go = new GameObject("wall");
        go.transform.SetParent(_roomRoot, false);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.sharedMaterial = WallMaterial;
        SpriteUtil.AddSprite(go.transform, SpriteUtil.SoftRectSprite, size.x, size.y, GameBootstrap.CStone * 0.72f, 1);
    }

    private static PhysicsMaterial2D _wallMat;
    private static PhysicsMaterial2D WallMaterial {
        get {
            if (_wallMat == null) _wallMat = new PhysicsMaterial2D("wall") { bounciness = 0.75f, friction = 0f };
            return _wallMat;
        }
    }

    private void SpawnCreature(SpawnSpec spec, int index) {
        var sp = SpeciesCatalog.Get(spec.SpeciesId);
        if (sp == null) return;
        bool food = sp.Behavior == CreatureBehavior.Food;
        float r = (float)SizeRule.RadiusForMass(spec.Mass);
        var view = SimEntityView2D.Create(_roomRoot, sp.Id + "_" + index,
            spec.IsNemesis || sp.ColorIndex == 3 ? SimEntityKind.Ai : SimEntityKind.Beetle, index, r, ColorFor(sp, spec.IsNemesis));
        view.transform.position = ToV3(spec.Pos);
        view.Rb.position = new Vector2((float)spec.Pos.X, (float)spec.Pos.Y);
        view.Rb.linearDamping = food ? 3f : 1.1f;
        view.GetComponent<CircleCollider2D>().sharedMaterial = WallMaterial;
        if (food) {
            view.GetComponent<CircleCollider2D>().isTrigger = true; // споры не толкаются — их всасывают
            view.Body.sortingOrder = 4;
            // свечение спор пробивает тьму — видно, куда плыть
            var g = GameBootstrap.CFood; g.a = 0.45f;
            SpriteUtil.AddSprite(view.transform, SpriteUtil.GlowSprite, r * 5f, r * 5f, g, DarknessMask.Order + 2);
        } else if (sp.ColorIndex == 3 || spec.IsNemesis) {
            view.Body.sortingOrder = 6;
            VisualFactory.DecorateAi(view);
            AddLabel(view.transform, r, spec.IsNemesis ? spec.SoulName + " — помнит вас" : spec.SoulName);
        } else {
            view.Body.sortingOrder = 6;
            VisualFactory.DecorateBeetle(view);
            if (sp.Id == "glowworm" || sp.ColorIndex == 4) {
                var g = ColorFor(sp, false); g.a = 0.5f;
                SpriteUtil.AddSprite(view.transform, SpriteUtil.GlowSprite, r * 4f, r * 4f, g, DarknessMask.Order + 1);
            }
        }
        var c = view.gameObject.AddComponent<RunCreature>();
        c.IsNemesis = spec.IsNemesis;
        c.SoulName = spec.SoulName;
        c.Init(sp, spec.Mass, new CreatureBrain(_seed + (ulong)(index * 7919 + 1)), view, r);
        if (spec.IsNemesis) {
            var recs = Meta.Nemeses.Records;
            for (int i = 0; i < recs.Count; i++)
                if (recs[i].SoulName == spec.SoulName && !string.IsNullOrEmpty(recs[i].StolenMutation))
                    c.Mutations.Add(recs[i].StolenMutation);
        }
        _creatures.Add(c);
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

    private static void AddLabel(Transform parent, float radius, string text) {
        var go = new GameObject("label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, radius + 0.45f, 0);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.characterSize = 0.07f;
        tm.anchor = TextAnchor.LowerCenter;
        tm.color = new Color(0.85f, 0.8f, 1f, 0.95f);
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
        _player.Rb.linearDamping = 0.9f;
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
    }

    private float PlayerRadius => (float)SizeRule.RadiusForMass(Run.Mass);

    private void OnDashRelease(Vec2 dir, double charge) {
        if (Run == null || _ended || _ui.ModalOpen) return;
        if (!Run.TryDash()) {
            _ui.Flash("Нет зарядов рывка — подождите");
            ProceduralSfx.Instance?.Play("hit");
            return;
        }
        float speed = DashSpeed * (float)Run.Mutations.DashSpeedMult * (float)charge;
        _player.Rb.linearVelocity += new Vector2((float)dir.X, (float)dir.Y) * speed;
        ProceduralSfx.Instance?.Play("jet");
        // капля влаги остаётся позади — видно, что рывок стоит массы
        SplashBurst2D.Instance?.Burst(_player.transform.position, new Vector2((float)-dir.X, (float)-dir.Y), 3);
        if (Run.IsDead) { _lastHitSource = null; EndRun(); }
    }

    // ---------- Контакты ----------
    public void OnPlayerCollision(Collision2D col) {
        if (_ended || Run == null) return;
        var c = col.collider.GetComponent<RunCreature>();
        if (c == null || c.IsFood) return;
        float rel = col.relativeVelocity.magnitude;
        var res = SizeRule.Resolve(Run.Mass, Run.Mutations, c.Mass, c.Mutations, rel);
        ResolveContact(c, res, col.contactCount > 0 ? col.GetContact(0).point : (Vector2)c.transform.position);
    }

    private void ResolveContact(RunCreature c, ContactResult res, Vector2 point) {
        if (res.Absorbed == 1) { Absorb(c); return; }
        if (res.Absorbed == 2) {
            _lastHitSource = c.SourceId;
            Run.OnMassLost(Run.Mass, c.SourceId);
            EndRun();
            return;
        }
        if (res.MassLossB > 0) {
            c.Mass -= res.MassLossB;
            Run.OnHit(res.MassLossB);
            SpawnDroplets(point, res.MassLossB);
            ProceduralSfx.Instance?.Play("weaken");
            if (c.Mass < 0.35) { SpawnDroplets(c.transform.position, System.Math.Max(0, c.Mass)); KillCreature(c); }
            else c.ApplySize();
        }
        if (res.MassLossA > 0) {
            _lastHitSource = c.SourceId;
            Run.OnMassLost(res.MassLossA, c.SourceId);
            SpawnDroplets(point, res.MassLossA);
            ProceduralSfx.Instance?.Play("hit");
            if (Run.IsDead) EndRun();
        }
    }

    private void Absorb(RunCreature c) {
        Run.OnAbsorb(c.Species.Id, c.Mass);
        if (c.IsNemesis) {
            Meta.Nemeses.OnSoulAbsorbed(c.SoulName);
            _nemesisAbsorbed = true;
            _ui.Banner(c.SoulName + " поглощена — её память теперь ваша", 3f);
        } else if (!c.IsFood) _ui.Flash("Поглощено: " + c.Species.Name);
        ProceduralSfx.Instance?.Play(c.IsFood ? "eat" : "capture");
        if (!c.IsFood) SplashBurst2D.Instance?.Burst(c.transform.position, Vector2.up, 8);
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
        for (int i = 0; i < parts.Length; i++) {
            double ang = _rng.Range(0, System.Math.PI * 2);
            var dir = new Vec2(System.Math.Cos(ang), System.Math.Sin(ang));
            var pos = new Vec2(at.x, at.y) + dir * 0.3;
            SpawnCreature(new SpawnSpec { SpeciesId = "spore", Pos = pos, Mass = parts[i] }, 1000 + _creatures.Count);
            var last = _creatures[_creatures.Count - 1];
            last.View.Rb.linearVelocity = new Vector2((float)dir.X, (float)dir.Y) * 3.5f;
        }
    }

    // ---------- Цикл ----------
    void FixedUpdate() {
        if (Run == null || _ended || _ui.ModalOpen) return;
        _time += Time.fixedDeltaTime;
        var pPos = _player.Rb.position;
        float pr = PlayerRadius;
        float pull = (1.6f + 0.45f * Mathf.Sqrt((float)Run.Mass)) * (float)Run.Mutations.PullRadiusMult;
        for (int i = _creatures.Count - 1; i >= 0; i--) {
            var c = _creatures[i];
            if (c == null) { _creatures.RemoveAt(i); continue; }
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
            // крупное, но уже меньше нас после роста — съедаем при касании без удара
            if (dist <= pr + c.Radius + 0.06f && Run.Mass >= c.Mass * Run.Mutations.AbsorbRatio) { Absorb(c); continue; }
            var o = c.Brain.Think(new BrainInput {
                SelfPos = new Vec2(rb.position.x, rb.position.y), SelfMass = c.Mass, Behavior = c.Species.Behavior,
                PlayerPos = new Vec2(pPos.x, pPos.y), PlayerMass = Run.Mass, Time = _time
            });
            if (o.Impulse)
                rb.linearVelocity += new Vector2((float)o.Direction.X, (float)o.Direction.Y) * (CreatureSpeed * (float)o.Strength01);
        }
        // ядовитый след: всё, что близко позади, медленно теряет массу
        double tox = Run.Mutations.TrailDamagePerSec;
        if (tox > 0 && _player.Rb.linearVelocity.magnitude > 1.5f) {
            Vector2 back = pPos - _player.Rb.linearVelocity.normalized * (pr + 0.6f);
            for (int i = _creatures.Count - 1; i >= 0; i--) {
                var c = _creatures[i];
                if (c.IsFood || Vector2.Distance(back, c.View.Rb.position) > c.Radius + 0.7f) continue;
                c.Mass -= tox * Time.fixedDeltaTime;
                if (c.Mass < 0.35) KillCreature(c); else c.ApplySize();
            }
        }
        // выход
        if (Run.ExitOpen && Vector2.Distance(pPos, new Vector2((float)Run.Room.ExitPos.X, (float)Run.Room.ExitPos.Y)) <= ExitRadius + pr) {
            if (Run.TryEnterExit()) {
                ProceduralSfx.Instance?.Play("unlock");
                if (Run.IsVictory) { EndRun(); return; }
                BuildRoom();
            }
        }
    }

    void Update() {
        if (Run == null || _ended) return;
        if (!_ui.ModalOpen) Run.Tick(Time.deltaTime);
        ApplyPlayerSize();
        _dark.Set((float)Run.Room.Darkness01, (float)Run.Mutations.LightRadius);
        UpdateExitVisual();
        if (Run.IsDead) { EndRun(); return; }
        if (Run.PendingOffer != null && !_offerShown) {
            _offerShown = true;
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
                Vector2 e = new Vector2((float)Run.Room.ExitPos.X, (float)Run.Room.ExitPos.Y);
                Vector2 dir = (e - p).normalized;
                _exitPointer.transform.position = p + dir * (PlayerRadius + 0.55f);
            }
        }
    }

    // ---------- Сервис ----------
    private static Vector3 ToV3(Vec2 v) => new Vector3((float)v.X, (float)v.Y, 0f);

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
