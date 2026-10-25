using UnityEngine;
using PM.Domain.Core;
using PM.Domain.World;
using PM.Domain.Absorption;
using PM.Domain.Body;
using PM.Domain.AI;
using PM.Domain.Soul;
using PM.Persistence;
using PM.Systems.Sim;
using PM.Systems.Gameplay;
using PM.Systems.Input;
using PM.Systems.CameraRig;
using PM.Systems.UI;

namespace PM.Systems.Bootstrap {
/// <summary>
/// Сборка игры без ручной настройки сцены: арена, игрок, еда, жуки, ИИ-души, стены.
/// Запускается из Boot-сцены, созданной PM/Setup/01.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class GameBootstrap : MonoBehaviour {
    public static GameBootstrap Instance { get; private set; }
    public GameSim Sim { get; private set; }

    private SimEntityView2D _playerView;
    private readonly System.Collections.Generic.List<SimEntityView2D> _aiViews =
        new System.Collections.Generic.List<SimEntityView2D>();
    private readonly System.Collections.Generic.List<SimEntityView2D> _beetleViews =
        new System.Collections.Generic.List<SimEntityView2D>();
    private readonly System.Collections.Generic.List<SimEntityView2D> _pelletViews =
        new System.Collections.Generic.List<SimEntityView2D>();

    // Палитра VISUAL_UI
    public static readonly Color CBackground = ColorFromHex(0x0B1723);
    public static readonly Color CStone = ColorFromHex(0x283B46);
    public static readonly Color CSlime = ColorFromHex(0x3EBBE4);
    public static readonly Color COutline = ColorFromHex(0x9FEAFF);
    public static readonly Color CFood = ColorFromHex(0x5FCE95);
    public static readonly Color CSoul = ColorFromHex(0xAE86E7);
    public static readonly Color CDanger = ColorFromHex(0xE67C5F);
    public static readonly Color CGlow = ColorFromHex(0x14314A);  // фоновое свечение
    public static readonly Color CCore = ColorFromHex(0x1C7FA8);  // ядро слайма

    public static Color ColorFromHex(int hex) {
        float r = ((hex >> 16) & 0xFF) / 255f;
        float g = ((hex >> 8) & 0xFF) / 255f;
        float b = (hex & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }

    void Awake() {
        Instance = this;
        Application.targetFrameRate = 60;
        Camera.main.backgroundColor = CBackground;
        // HUD-канва запечена в сцене без GraphicRaycaster (HudBuilder в билде не запускается)
        var hudCanvas = FindFirstObjectByType<Canvas>();
        if (hudCanvas != null && hudCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            hudCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        BuildSim();
        BuildArena();
        BuildEntities();
        BuildSystems();
        HudPresenter.Instance?.Bind(this);
    }

    private void BuildSim() {
        Sim = new GameSim(SimMode.UnityDriven);
        // демо-контент: 12 гранул (DEMO_SCOPE: небольшая зона)
        var rnd = new PmRandom(777);
        for (int i = 0; i < 12; i++) {
            double x = rnd.Range(-7.5, 7.5), y = rnd.Range(-4.0, 4.0);
            if (System.Math.Abs(x) < 1.5 && System.Math.Abs(y) < 1.5) x += 3.0; // не на игроке
            Sim.AddPellet(new Vec2(x, y), 0.35);
        }
        Sim.AddBeetle(new Vec2(5.5, 1.5));  // грибной жук (T08)
        Sim.AddBeetle(new Vec2(-6.0, -2.5));
        Sim.AddAiActor(true, new Vec2(-4.0, 3.0));   // ИИ-союзник
        Sim.AddAiActor(false, new Vec2(6.0, -3.0));  // ИИ-соперник
    }

    private void BuildArena() {
        // фоновое свечение за ареной (глубина, читается и на 16:9, и на 20:9)
        var glow = SpriteUtil.AddSprite(transform, SpriteUtil.GlowSprite, 30f, 20f, CGlow, -1);
        glow.gameObject.name = "bg_glow";
        glow.transform.localPosition = Vector3.zero;
        // пол арены: мягкий скруглённый прямоугольник чуть темнее прежнего круга
        var floor = SpriteUtil.AddSprite(transform, SpriteUtil.SoftRectSprite, 20.1f, 12.1f, CStone, 0);
        floor.gameObject.name = "arena_floor";
        floor.transform.localPosition = Vector3.zero;
        // физические стены (каркасный бокс по краям арены) + световые кромки
        MakeWall("wall_top", new Vector3(0, (float)Sim.Cfg.HalfHeight + 0.25f, 0), new Vector2(19f, 0.5f));
        MakeWall("wall_bottom", new Vector3(0, -(float)Sim.Cfg.HalfHeight - 0.25f, 0), new Vector2(19f, 0.5f));
        MakeWall("wall_left", new Vector3(-(float)Sim.Cfg.HalfWidth - 0.25f, 0, 0), new Vector2(0.5f, 12f));
        MakeWall("wall_right", new Vector3((float)Sim.Cfg.HalfWidth + 0.25f, 0, 0), new Vector2(0.5f, 12f));
        MakeEdge("edge_top", new Vector3(0, (float)Sim.Cfg.HalfHeight - 0.14f, 0), new Vector2(17.9f, 0.10f));
        MakeEdge("edge_bottom", new Vector3(0, -(float)Sim.Cfg.HalfHeight + 0.14f, 0), new Vector2(17.9f, 0.10f));
        MakeEdge("edge_left", new Vector3(-(float)Sim.Cfg.HalfWidth + 0.14f, 0, 0), new Vector2(0.10f, 9.7f));
        MakeEdge("edge_right", new Vector3((float)Sim.Cfg.HalfWidth - 0.14f, 0, 0), new Vector2(0.10f, 9.7f));
    }

    private void MakeWall(string name, Vector3 pos, Vector2 size) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        var sr = SpriteUtil.AddSprite(go.transform, SpriteUtil.SoftRectSprite, size.x, size.y, CStone * 0.72f, 1);
        sr.transform.localPosition = Vector3.zero;
    }

    private void MakeEdge(string name, Vector3 pos, Vector2 size) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var c = COutline; c.a = 0.16f;
        var sr = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, size.x, size.y, c, 1);
        sr.transform.localPosition = Vector3.zero;
    }

    private void BuildEntities() {
        // игрок
        _playerView = SimEntityView2D.Create(transform, "player_slime", SimEntityKind.Player,
            Sim.Player.Id, (float)Sim.Player.Radius, CSlime);
        _playerView.Body.sortingOrder = 8;
        VisualFactory.DecorateSlime(_playerView);
        var slime = _playerView.gameObject.AddComponent<SlimeVisual>();
        slime.BodyRoot = _playerView.Body.transform;
        slime.Membrane = _playerView.Membrane;
        slime.Core = _playerView.Core;
        _playerView.gameObject.AddComponent<ContactDamageRelay2D>();
        // ИИ-души
        for (int i = 0; i < Sim.AiActors.Count; i++) {
            var a = Sim.AiActors[i];
            var prof = Sim.ProfileOf(a.Id);
            var v = SimEntityView2D.Create(transform, prof.Ally ? "ai_ally" : "ai_rival",
                SimEntityKind.Ai, a.Id, (float)a.Radius, prof.Ally ? CFood : CDanger);
            v.Body.sortingOrder = 6;
            VisualFactory.DecorateAi(v);
            v.gameObject.AddComponent<ContactDamageRelay2D>();
            _aiViews.Add(v);
        }
        // жуки
        for (int i = 0; i < Sim.Beetles.Count; i++) {
            var b = Sim.Beetles[i];
            var v = SimEntityView2D.Create(transform, "beetle_" + b.Id, SimEntityKind.Beetle,
                b.Id, (float)b.Radius, CDanger);
            v.Body.sortingOrder = 6;
            VisualFactory.DecorateBeetle(v);
            v.gameObject.AddComponent<ContactDamageRelay2D>();
            _beetleViews.Add(v);
        }
        // гранулы
        for (int i = 0; i < Sim.Food.Count; i++) {
            var p = Sim.Food[i];
            var v = SimEntityView2D.Create(transform, "pellet_" + p.Id, SimEntityKind.Beetle,
                p.Id, 0.22f, CFood);
            v.Body.sortingOrder = 4;
            VisualFactory.DecoratePellet(v);
            _pelletViews.Add(v);
        }
        // след
        var trailGo = new GameObject("trail_emitter");
        trailGo.transform.SetParent(transform, false);
        trailGo.AddComponent<TrailEmitter2D>();
        // пул брызг
        var splashGo = new GameObject("splash_burst");
        splashGo.transform.SetParent(transform, false);
        splashGo.AddComponent<SplashBurst2D>();
    }

    private void BuildSystems() {
        var runnerGo = new GameObject("sim_runner");
        runnerGo.transform.SetParent(transform, false);
        var runner = runnerGo.AddComponent<SimRunner>();
        runner.Construct(Sim);
        runner.PlayerRb = _playerView.Rb;
        for (int i = 0; i < _aiViews.Count; i++) runner.AiRbs.Add(_aiViews[i].Rb);
        for (int i = 0; i < _beetleViews.Count; i++) runner.BeetleRbs.Add(_beetleViews[i].Rb);
        for (int i = 0; i < _pelletViews.Count; i++) runner.PelletRbs.Add(_pelletViews[i].Rb);
        // ввод + индикатор
        var inputGo = new GameObject("pull_input");
        inputGo.transform.SetParent(transform, false);
        inputGo.AddComponent<PullGestureInput>();
        var aimGo = new GameObject("aim_indicator");
        aimGo.transform.SetParent(_playerView.transform, false);
        aimGo.AddComponent<LineRenderer>();
        aimGo.AddComponent<AimIndicator>();
        // камера следит за игроком
        var cam = Camera.main;
        if (cam != null) {
            if (cam.GetComponent<FollowCamera>() == null) cam.gameObject.AddComponent<FollowCamera>();
            cam.GetComponent<FollowCamera>().Target = _playerView.transform;
        }
    }

    void Update() {
        // синхронизация вида ↔ модель
        if (_playerView != null)
            _playerView.transform.position = new Vector3((float)Sim.Player.Pos.X, (float)Sim.Player.Pos.Y, 0);
        for (int i = 0; i < Sim.AiActors.Count && i < _aiViews.Count; i++) {
            var a = Sim.AiActors[i];
            var v = _aiViews[i];
            v.transform.position = new Vector3((float)a.Pos.X, (float)a.Pos.Y, 0);
            if (!a.Alive || a.Vitals.Integrity <= 0) v.Body.color = new Color(0.2f, 0.2f, 0.25f);
        }
        for (int i = 0; i < Sim.Beetles.Count && i < _beetleViews.Count; i++) {
            var b = Sim.Beetles[i];
            var v = _beetleViews[i];
            if (b.Consumed || b.Phase >= PreyPhase.Internalized) {
                v.gameObject.SetActive(false);
                continue;
            }
            v.transform.position = new Vector3((float)b.Pos.X, (float)b.Pos.Y, 0);
            // ослабленный жук темнеет (читаемость без эффектов света)
            v.Body.color = Color.Lerp(CDanger, new Color(0.35f, 0.2f, 0.18f), 1f - (float)b.Health01);
        }
        for (int i = 0; i < Sim.Food.Count && i < _pelletViews.Count; i++) {
            var p = Sim.Food[i];
            var v = _pelletViews[i];
            if (p.Consumed) { if (v.gameObject.activeSelf) v.gameObject.SetActive(false); continue; }
            v.transform.position = new Vector3((float)p.Pos.X, (float)p.Pos.Y, 0);
        }
        // масштаб игрока от массы (структура+вода+запас+груз)
        if (_playerView != null) {
            double m = Sim.Player.Body.Total;
            float s = Mathf.Clamp((float)(0.7 + m * 0.05), 0.7f, 2.2f);
            var root = _playerView.Body.transform.parent;
            root.localScale = new Vector3(s, s, 1f);
        }
    }

    // ---------- Реинкарнация (T17) ----------
    public void Reincarnate() {
        var r = Reincarnation.Reborn(Sim.Soul, baseStructure: 1.0, baseWater: 5.0,
            bodyConductivityBase: Sim.PlayerOrgans.Conductivity, regionRichness: 1.0);
        Sim.Player.Body = r.Body;
        Sim.Player.Vitals = new BodyVitals();
        Sim.Player.Alive = true;
        Sim.Player.Pos = new Vec2(0, 0);
        Sim.Player.Vel = new Vec2(0, 0);
        Sim.CurrentFormId = "form_slime";
        Sim.Player.FormId = "form_slime";
        Sim.ReapplyNodeEffects();
        Sim.RecordEvent("reincarnated", "life #" + Sim.Soul.LivesLived);
        if (HudPresenter.Instance != null) HudPresenter.Instance.HideDeath();
    }

    // ---------- Сохранение (QA-10) ----------
    private SaveService _saves;
    public SaveService Saves {
        get {
            if (_saves == null)
                _saves = new SaveService(new SystemFilePort(Application.persistentDataPath));
            return _saves;
        }
    }
    public bool SaveToSlot(int slot) {
        try {
            Saves.Save(slot == 0 ? SaveService.Slot0 : SaveService.Slot1, GameSimSave.Export(Sim));
            return true;
        } catch (System.Exception e) {
            Debug.LogWarning("save failed: " + e.Message);
            return false;
        }
    }
    public bool LoadFromSlot(int slot) {
        try {
            var data = Saves.Load(slot == 0 ? SaveService.Slot0 : SaveService.Slot1);
            GameSimSave.Import(Sim, data);
            // восстановить виды из модели
            SnapViewsToSim();
            return true;
        } catch (System.Exception e) {
            Debug.LogWarning("load failed: " + e.Message);
            return false;
        }
    }
    public bool HasSave(int slot) =>
        Saves.HasSave(slot == 0 ? SaveService.Slot0 : SaveService.Slot1);

    private void SnapViewsToSim() {
        for (int i = 0; i < Sim.Beetles.Count && i < _beetleViews.Count; i++) {
            var b = Sim.Beetles[i];
            _beetleViews[i].gameObject.SetActive(!(b.Consumed || b.Phase >= PreyPhase.Internalized));
        }
        for (int i = 0; i < Sim.Food.Count && i < _pelletViews.Count; i++)
            _pelletViews[i].gameObject.SetActive(!Sim.Food[i].Consumed);
        if (_playerView != null) {
            _playerView.transform.position =
                new Vector3((float)Sim.Player.Pos.X, (float)Sim.Player.Pos.Y, 0);
            if (_playerView.Rb != null)
                _playerView.Rb.position = new Vector2((float)Sim.Player.Pos.X, (float)Sim.Player.Pos.Y);
        }
    }
}
}
