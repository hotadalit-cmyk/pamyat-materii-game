using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PM.Domain.Run;
using PM.Systems.Bootstrap;
using PM.Systems.Gameplay;
using PM.Systems.UI;

namespace PM.Systems.Run {
/// <summary>
/// Интерфейс забега (v0.4). Строится кодом в рантайме, не запекается в сцену —
/// слушатели кнопок живут, пока живёт объект. Сетка 1920×1080 по ширине + SafeArea.
/// </summary>
public sealed class RunUi : MonoBehaviour {
    private static readonly Color Panel = new Color(0.016f, 0.05f, 0.09f, 0.66f);
    private static readonly Color Modal = new Color(0.02f, 0.06f, 0.11f, 0.97f);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.82f);
    private static readonly Color TextColor = new Color(0.92f, 0.96f, 1f);

    private RunGame _game;
    private Transform _root;
    private Text _mass, _depth, _goal, _banner, _flash;
    private Image _goalFill;
    private readonly List<Image> _dashDots = new List<Image>();
    private float _bannerTimer, _flashTimer;
    private GameObject _offerModal, _imprintModal;
    private Text _imprintBody;
    private readonly Text[] _upgradeLabels = new Text[3];
    private Text _essenceText;
    private Text _combo, _mutations, _biomeTitle, _biomeSub, _bossLabel;
    private RectTransform _bossFill;
    private GameObject _bossBar;
    private float _biomeTimer;
    private RawImage _biomeArt;
    private CanvasGroup _biomeArtGroup;          // иллюстрация области (ИИ Z.ai) за карточкой новой глубины
    private GameObject _titleModal, _infoModal;
    private Text _titleSoul, _infoTitle, _infoBody;
    private RunMinimap _minimap;
    private RunEvolutionUi _evo;
    private RunSettingsUi _settings;
    private RunCodexUi _codex;
    public RunCodexUi Codex => _codex;
    private RectTransform _splitRt, _gatherRt;
    private Image _splitBtn, _gatherBtn;
    private Text _splitLabel, _gatherLabel;
    private RectTransform _dashRt, _soulRt;
    private Text _dashLabel;
    private Image _dashFill, _dashImg;
    private GameObject _pauseModal;
    private RawImage _vignette;
    private float _hurtT;

    public bool ModalOpen => (_offerModal != null && _offerModal.activeSelf) ||
                             (_imprintModal != null && _imprintModal.activeSelf) ||
                             (_titleModal != null && _titleModal.activeSelf) ||
                             (_infoModal != null && _infoModal.activeSelf) ||
                             (_minimap != null && _minimap.IsBig) ||
                             (_evo != null && _evo.ModalOpen) ||
                             (_settings != null && _settings.IsOpen) ||
                             (_codex != null && _codex.IsOpen) ||
                             (_pauseModal != null && _pauseModal.activeSelf);

    public static RunUi Build(RunGame game) {
        var canvasGo = new GameObject("run_ui");
        canvasGo.transform.SetParent(game.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<SafeAreaFitter>();
        var ui = canvasGo.AddComponent<RunUi>();
        ui._game = game;
        ui._root = canvasGo.transform;
        ui.BuildHud();
        ui.BuildOffer();
        ui.BuildImprint();
        ui._evo = RunEvolutionUi.Build(ui._root, game);
        ui._settings = RunSettingsUi.Build(ui._root, game);
        // «Меню» — одна кнопка слева от миникарты: пауза, энциклопедия, настройки (v0.9)
        var menu = Button(ui._root, "menu_btn", Vector2.zero, new Vector2(180, 80), GameBootstrap.COutline, 0.45f, "Меню", 28);
        var menuRt = (RectTransform)menu.transform;
        menuRt.anchorMin = menuRt.anchorMax = menuRt.pivot = new Vector2(1, 1);
        menuRt.anchoredPosition = new Vector2(-480, -28);
        menu.GetComponent<Button>().onClick.AddListener(() => { if (!ui.ModalOpen) ui.OpenPause(); });
        ui._codex = RunCodexUi.Build(ui._root, game);
        ui.BuildPause();
        ui.BuildVignette();
        GameSettings.Changed += ui.ApplyHandedness;
        ui.BuildTitle();
        ui.BuildInfo();
        return ui;
    }

    // ---------- HUD ----------
    private void BuildHud() {
        var top = Box(_root, "status", new Vector2(28, -28), new Vector2(560, 196), Panel, new Vector2(0, 1));
        _mass = Label(top, "Масса 4.0", 40, TextAnchor.MiddleLeft, new Vector2(30, -22), new Vector2(500, 52), new Vector2(0, 1));
        for (int i = 0; i < 4; i++) {
            var dot = Box(top, "dash" + i, new Vector2(34 + i * 46, -92), new Vector2(34, 34), GameBootstrap.CSlime, new Vector2(0, 1));
            var img = dot.GetComponent<Image>();
            img.sprite = SpriteUtil.UiCircleSprite;
            img.type = Image.Type.Simple;
            _dashDots.Add(img);
        }
        _depth = Label(top, "Глубина 1 · 1/3", 28, TextAnchor.MiddleLeft, new Vector2(30, -140), new Vector2(500, 40), new Vector2(0, 1));
        _combo = Label(top, "", 34, TextAnchor.MiddleRight, new Vector2(-30, -22), new Vector2(260, 52), new Vector2(1, 1));
        _combo.color = new Color(1f, 0.9f, 0.4f);
        _mutations = Label(_root, "", 22, TextAnchor.UpperLeft, new Vector2(40, -470), new Vector2(700, 120), new Vector2(0, 1));
        _mutations.color = new Color(0.75f, 0.65f, 1f, 0.9f);

        _bossBar = Box(_root, "boss_bar", new Vector2(0, 40), new Vector2(1000, 84), Panel, new Vector2(0.5f, 0)).gameObject;
        var bossBg = Box(_bossBar.transform, "boss_bg", new Vector2(0, 14), new Vector2(940, 18), new Color(1, 1, 1, 0.08f), new Vector2(0.5f, 0));
        _bossFill = Box(bossBg, "boss_fill", Vector2.zero, new Vector2(940, 18), GameBootstrap.CDanger, new Vector2(0, 0.5f));
        _bossLabel = Label(_bossBar.transform, "Страж глубины", 28, TextAnchor.MiddleCenter, new Vector2(0, -6), new Vector2(940, 40), new Vector2(0.5f, 1));
        _bossBar.SetActive(false);

        // деление/слияние — крупные кнопки под правый большой палец
        var split = Button(_root, "split_btn", Vector2.zero, new Vector2(250, 120), GameBootstrap.CSlime, 0.8f, "Разделиться", 30);
        var srt = (RectTransform)split.transform;
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1, 0);
        srt.anchoredPosition = new Vector2(-28, 168);
        split.GetComponent<Button>().onClick.AddListener(() => _game.RequestSplit());
        _splitBtn = split.GetComponent<Image>();
        _splitRt = srt;
        _splitLabel = split.GetComponentInChildren<Text>();
        var gather = Button(_root, "gather_btn", Vector2.zero, new Vector2(250, 120), GameBootstrap.CSoul, 0.8f, "Собраться", 30);
        var grt = (RectTransform)gather.transform;
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(1, 0);
        grt.anchoredPosition = new Vector2(-28, 28);
        gather.GetComponent<Button>().onClick.AddListener(() => _game.RequestGather());
        _gatherBtn = gather.GetComponent<Image>();
        _gatherRt = grt;
        _gatherLabel = gather.GetComponentInChildren<Text>();
        var soul = Button(_root, "soul_btn", Vector2.zero, new Vector2(250, 120), new Color(0.75f, 0.55f, 1f), 0.8f, "Душа ⇄", 30);
        soul.GetComponent<Button>().onClick.AddListener(() => _game.RequestSoulTransfer());
        _soulRt = (RectTransform)soul.transform;
        // режим джойстика: круглая кнопка рывка рядом с кнопками деления, срабатывает при касании
        var dash = Box(_root, "dash_btn", Vector2.zero, new Vector2(230, 230), new Color(GameBootstrap.CSlime.r, GameBootstrap.CSlime.g, GameBootstrap.CSlime.b, 0.55f), new Vector2(1, 0));
        dash.GetComponent<Image>().sprite = SpriteUtil.UiCircleSprite;
        dash.GetComponent<Image>().type = Image.Type.Simple;
        _dashImg = dash.GetComponent<Image>();
        var chargeGo = new GameObject("charge_fill");
        chargeGo.transform.SetParent(dash, false);
        _dashFill = chargeGo.AddComponent<Image>();
        _dashFill.sprite = SpriteUtil.UiCircleSprite;
        _dashFill.type = Image.Type.Filled;
        _dashFill.fillMethod = Image.FillMethod.Radial360;
        _dashFill.fillOrigin = (int)Image.Origin360.Top;
        _dashFill.color = new Color(1f, 1f, 1f, 0.22f);
        _dashFill.raycastTarget = false;
        _dashFill.rectTransform.sizeDelta = new Vector2(230, 230);
        var dl = UiUtil.AddText(dash, "РЫВОК", 34, TextAnchor.MiddleCenter, TextColor);
        dl.rectTransform.sizeDelta = new Vector2(220, 120);
        dl.raycastTarget = false;
        _dashLabel = dl;
        dash.gameObject.AddComponent<PressButton>().OnPress = () => _game.JoystickDash();
        _dashRt = dash;
        RunJoystick.Build(_root);
        ApplyHandedness();

        var artGo = new GameObject("biome_art");
        artGo.transform.SetParent(_root, false);
        _biomeArt = artGo.AddComponent<RawImage>();
        _biomeArt.raycastTarget = false;
        var art = _biomeArt.rectTransform;
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0.5f);
        art.anchoredPosition = new Vector2(0, 90);
        art.sizeDelta = new Vector2(1100, 1100f * 768f / 1344f);
        _biomeArtGroup = artGo.AddComponent<CanvasGroup>();
        _biomeArtGroup.blocksRaycasts = false;
        // тёмная полоса под заголовком — текст читается на любой картинке
        var band = new GameObject("biome_band");
        band.transform.SetParent(artGo.transform, false);
        var bimg = band.AddComponent<Image>();
        bimg.color = new Color(0f, 0f, 0.02f, 0.82f);
        bimg.raycastTarget = false;
        var brt = bimg.rectTransform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(1100f, 190f);
        brt.anchoredPosition = new Vector2(0f, -10f);
        _biomeArt.gameObject.SetActive(false);
        _biomeTitle = Label(_root, "", 72, TextAnchor.MiddleCenter, new Vector2(0, 120), new Vector2(1600, 100), new Vector2(0.5f, 0.5f));
        _biomeSub = Label(_root, "", 32, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(1600, 60), new Vector2(0.5f, 0.5f));

        var goal = Box(_root, "goal", new Vector2(0, -28), new Vector2(720, 92), Panel, new Vector2(0.5f, 1));
        var fillBg = Box(goal, "goal_bg", new Vector2(0, 18), new Vector2(660, 16), new Color(1, 1, 1, 0.08f), new Vector2(0.5f, 0));
        var fillGo = Box(fillBg, "goal_fill", Vector2.zero, new Vector2(660, 16), GameBootstrap.CFood, new Vector2(0, 0.5f));
        _goalFill = fillGo.GetComponent<Image>(); // ширина = прогресс (Filled без спрайта не режется)
        _goal = Label(goal, "Поглощено 0 / 3", 28, TextAnchor.MiddleCenter, new Vector2(0, -8), new Vector2(700, 40), new Vector2(0.5f, 1));

        _banner = Label(_root, "", 52, TextAnchor.MiddleCenter, new Vector2(0, 160), new Vector2(1500, 140), new Vector2(0.5f, 0.5f));
        _banner.horizontalOverflow = HorizontalWrapMode.Wrap; _banner.resizeTextForBestFit = true; _banner.resizeTextMinSize = 30; _banner.resizeTextMaxSize = 52; // длинный баннер — в две строки, не обрезается
        _flash = Label(_root, "", 30, TextAnchor.MiddleCenter, new Vector2(0, 150), new Vector2(1400, 50), new Vector2(0.5f, 0));
    }

    private static string PhaseName(BossPhase p) {
        switch (p) {
            case BossPhase.Dormant: return "дремлет";
            case BossPhase.Charge: return "таранит";
            case BossPhase.Brood: return "выводок";
            case BossPhase.Frenzy: return "ярость";
            case BossPhase.Exposed: return "обнажён — поглотите!";
            default: return "";
        }
    }

    /// <summary>Миникарта открытого мира (создаётся один раз, привязывается к каждой новой глубине).</summary>
    public void SetMinimap(WorldView view) {
        if (_minimap == null) _minimap = RunMinimap.Build(_root, _game);
        _minimap.Bind(view);
    }

    /// <summary>Карточка новой глубины: «Глубина 2 · Тёплые тоннели» + подзаголовок.</summary>
    public void BiomeCard(int depth, string name, string subtitle, Color accent, string artKey = null) {
        var tex = artKey != null ? Resources.Load<Texture2D>("AiArt/biome_" + artKey) : null;
        _biomeArt.texture = tex;
        _biomeArt.gameObject.SetActive(tex != null);
        _biomeTitle.text = "Глубина " + depth + " · " + name;
        _biomeTitle.color = new Color(accent.r, accent.g, accent.b, 1f);
        _biomeSub.text = subtitle;
        _biomeSub.color = new Color(0.9f, 0.93f, 1f, 1f);
        _biomeTimer = 3.2f;
    }

    // ---------- Пауза и «Назад» ----------
    private void BuildPause() {
        _pauseModal = FullScreen("pause_modal");
        _pauseModal.GetComponent<Image>().color = new Color(0f, 0.01f, 0.03f, 0.8f);
        var box = Box(_pauseModal.transform, "pause_box", Vector2.zero, new Vector2(760, 930), Modal, new Vector2(0.5f, 0.5f));
        var t = Label(box, "ПАУЗА", 56, TextAnchor.MiddleCenter, new Vector2(0, 390), new Vector2(700, 80), new Vector2(0.5f, 0.5f));
        t.color = GameBootstrap.COutline;
        string[] names = { "Продолжить", "Эволюция", "Показатели", "Энциклопедия", "Настройки", "Как играть" };
        Color[] cols = { GameBootstrap.CFood, GameBootstrap.CSoul, GameBootstrap.CSlime, GameBootstrap.CSoul, GameBootstrap.COutline, GameBootstrap.COutline };
        for (int i = 0; i < names.Length; i++) {
            int idx = i;
            var b = Button(box, "p" + i, new Vector2(0, 260 - i * 118), new Vector2(620, 100), cols[i], i == 0 ? 0.95f : 0.75f, names[i], 34);
            b.GetComponent<Button>().onClick.AddListener(() => PauseChoice(idx));
        }
        _pauseModal.SetActive(false);
    }

    public void OpenPause() {
        if (_game == null || !_game.InRun) return;
        _pauseModal.SetActive(true);
        _pauseModal.transform.SetAsLastSibling();
        _game.SetPaused(true);
    }

    private void ClosePause() {
        _pauseModal.SetActive(false);
        _game.SetPaused(false);
    }

    private void PauseChoice(int i) {
        ClosePause();
        switch (i) {
            case 1: _evo.OpenEvolution(); break;
            case 2: _evo.OpenStats(); break;
            case 3: _codex.Open(); break;
            case 4: _settings.Open(); break;
            case 5: ShowInfo("Как играть", HowToPlay()); break;
        }
    }

    /// <summary>«Назад»: закрыть верхнее окно; в игре без окон — пауза.</summary>
    private void OnBack() {
        if (_codex != null && _codex.CloseTop()) return;
        if (_settings != null && _settings.CloseIfOpen()) return;
        if (_evo != null && _evo.CloseTop()) return;
        if (_infoModal != null && _infoModal.activeSelf) { _infoModal.SetActive(false); return; }
        if (_pauseModal != null && _pauseModal.activeSelf) { ClosePause(); return; }
        if (ModalOpen) return;                                   // выбор мутации, итог жизни, титул — решаются кнопками
        if (_game != null && _game.InRun) OpenPause();
    }

    // ---------- Виньетка опасности ----------
    private void BuildVignette() {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.2f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.25f, d));
                px[y * n + x] = new Color32(255, 30, 40, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        var go = new GameObject("danger_vignette");
        go.transform.SetParent(_root, false);
        go.transform.SetAsFirstSibling();                         // под всем интерфейсом, над миром
        _vignette = go.AddComponent<RawImage>();
        _vignette.texture = tex;
        _vignette.raycastTarget = false;
        var rt = _vignette.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(-200, -200); rt.offsetMax = new Vector2(200, 200);
        _vignette.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>Красная вспышка по краям экрана при ударе (сила 0..1).</summary>
    public void HurtFlash(float strength) { _hurtT = Mathf.Max(_hurtT, 0.25f + 0.55f * strength); }

    private void UpdateVignette(RunState run) {
        if (_vignette == null) return;
        _hurtT = Mathf.Max(0f, _hurtT - Time.unscaledDeltaTime * 1.6f);
        float low = 0f;
        if (run != null && _game.InRun && run.Mass < SizeRule.MinViableMass * 1.6)
            low = 0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f);   // мало массы — пульс тревоги
        _vignette.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(low, _hurtT)));
    }

    /// <summary>Обучающая подсказка — показывается, только если подсказки включены в настройках.</summary>
    public void Hint(string text, float seconds = 3f) { if (GameSettings.Hints) Flash(text, seconds); }

    /// <summary>Кнопки деления под большой палец: справа (по умолчанию) или слева.</summary>
    public void ApplyHandedness() {
        if (_splitRt == null || _gatherRt == null) return;
        bool left = GameSettings.LeftHanded;
        var anchor = left ? new Vector2(0, 0) : new Vector2(1, 0);
        float x = left ? 28 : -28;
        _splitRt.anchorMin = _splitRt.anchorMax = _splitRt.pivot = anchor;
        _gatherRt.anchorMin = _gatherRt.anchorMax = _gatherRt.pivot = anchor;
        _splitRt.anchoredPosition = new Vector2(x, 168);
        _gatherRt.anchoredPosition = new Vector2(x, 28);
        if (_soulRt != null) {
            _soulRt.anchorMin = _soulRt.anchorMax = _soulRt.pivot = anchor;
            _soulRt.anchoredPosition = new Vector2(x, 308);
        }
        if (_dashRt != null) {
            _dashRt.anchorMin = _dashRt.anchorMax = _dashRt.pivot = anchor;
            _dashRt.anchoredPosition = new Vector2(left ? 310 : -310, 30);   // между кнопками деления и центром
            _dashRt.gameObject.SetActive(GameSettings.Joystick);
        }
    }

    void OnDestroy() { GameSettings.Changed -= ApplyHandedness; }

    public void Banner(string text, float seconds = 2.2f) { _banner.text = text; _bannerTimer = seconds; }
    public void Flash(string text, float seconds = 1.4f) { _flash.text = text; _flashTimer = seconds; }

    void Update() {
        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) OnBack();   // «Назад» на Android
        var run = _game != null ? _game.Run : null;
        UpdateVignette(run);
        if (run != null && _dashLabel != null) {
            // круглая кнопка рывка: число зарядов и заполнение до следующего
            _dashLabel.text = "РЫВОК\n<size=26>" + run.DashCharges + " / " + run.MaxDashCharges + "</size>";
            _dashLabel.supportRichText = true;
            _dashFill.fillAmount = run.DashCharges >= run.MaxDashCharges ? 0f : (float)run.ChargeProgress01;
            var dc = GameBootstrap.CSlime; dc.a = run.DashCharges > 0 ? 0.6f : 0.25f;
            _dashImg.color = dc;
        }
        if (run != null) {
            _mass.text = string.Format("Масса {0:0.0}", run.Mass);
            _mass.color = run.Mass < SizeRule.MinViableMass * 1.6 ? GameBootstrap.CDanger : TextColor;
            for (int i = 0; i < _dashDots.Count; i++) {
                bool exists = i < run.MaxDashCharges;
                _dashDots[i].gameObject.SetActive(exists);
                var c = GameBootstrap.CSlime;
                c.a = i < run.DashCharges ? 1f : 0.22f;
                _dashDots[i].color = c;
            }
            int frags = _game.FragmentCount;
            bool canSplit = _game.CanSplit;
            _splitBtn.color = new Color(GameBootstrap.CSlime.r, GameBootstrap.CSlime.g, GameBootstrap.CSlime.b, canSplit ? 0.8f : 0.25f);
            _splitLabel.text = frags > 1 ? "Разделиться\n(" + frags + ")" : "Разделиться";
            _gatherBtn.gameObject.SetActive(frags > 1);
            _splitBtn.gameObject.SetActive(_game.SplitUnlocked);     // деление — только после узла эволюции
            _soulRt.gameObject.SetActive(_game.CanSoulTransfer);
            bool g = _game.Gathering;
            _gatherBtn.color = g ? new Color(1f, 0.85f, 0.4f, 0.9f) : new Color(GameBootstrap.CSoul.r, GameBootstrap.CSoul.g, GameBootstrap.CSoul.b, 0.8f);
            double carry = _game.TwinCarry;
            _gatherLabel.text = g ? "Собираемся…" : carry >= 0.5 ? "Собраться\n(ноша " + carry.ToString("0") + ")" : "Собраться";
            _combo.text = run.Combo != null && run.Combo.Chain >= 2 ? "Цепочка ×" + run.Combo.Chain : "";
            var ids = run.Mutations.ToArray();
            if (ids.Length > 0) {
                var sbm = new System.Text.StringBuilder();
                for (int i = 0; i < ids.Length; i++) {
                    var d = MutationCatalog.Get(ids[i]);
                    if (i > 0) sbm.Append(" · ");
                    sbm.Append(d != null ? d.Name : ids[i]);
                }
                _mutations.text = sbm.ToString();
            } else _mutations.text = "";
            var boss = run.Boss;
            _bossBar.SetActive(boss != null && !run.ExitOpen);
            if (boss != null) {
                float f = Mathf.Clamp01((float)(boss.Mass / System.Math.Max(0.01, boss.StartMass)));
                _bossFill.sizeDelta = new Vector2(Mathf.Max(18f, 940f * f), 18f);
                _bossLabel.text = "Страж глубины — " + PhaseName(boss.Phase);
            }
            if (run.Room != null) {
                bool evap = run.Layout != null && run.Layout.Biome != null && run.Layout.Biome.EvaporationPerSec > 0;
                _depth.text = run.OpenWorld && run.Layout != null && run.Layout.Biome != null
                    ? string.Format("Глубина {0} · {1}{2}", run.Depth, run.Layout.Biome.Name, evap ? " · жар" : "")
                    : string.Format("Глубина {0} · комната {1}/{2}{3}", run.Depth,
                        Mathf.Clamp(run.RoomIndex + 1, 1, RunState.RoomsPerDepth), RunState.RoomsPerDepth,
                        evap ? " · жар" : "");
                float goal = (float)System.Math.Max(0.01, run.Room.GoalMass);
                float frac = Mathf.Clamp01((float)run.RoomAbsorbed / goal);
                _goalFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(16f, 660f * frac), 16f);
                _goal.text = run.ExitOpen
                    ? (run.OpenWorld ? "Шахта вниз открыта — следуйте за огоньком" : "Выход открыт — ищите светящуюся трещину")
                    : string.Format(run.OpenWorld ? "Шахта откроется: {0:0.0} / {1:0.0}" : "Поглощено {0:0.0} / {1:0.0}", run.RoomAbsorbed, goal);
                _goal.color = run.ExitOpen ? GameBootstrap.CFood : TextColor;
            }
        }
        Fade(_banner, ref _bannerTimer);
        Fade(_biomeTitle, ref _biomeTimer);
        var bc = _biomeSub.color; bc.a = _biomeTitle.color.a; _biomeSub.color = bc;
        if (_biomeArt.gameObject.activeSelf) {
            float a = _biomeTitle.color.a;
            _biomeArtGroup.alpha = a;
            if (a <= 0f) _biomeArt.gameObject.SetActive(false);
        }
        Fade(_flash, ref _flashTimer);
    }

    private static void Fade(Text t, ref float timer) {
        if (timer > 0) timer -= Time.unscaledDeltaTime;
        var c = t.color; c.a = Mathf.Clamp01(timer / 0.4f); t.color = c;
    }

    // ---------- Выбор мутации ----------
    private Transform _offerCards;
    private void BuildOffer() {
        _offerModal = FullScreen("offer_modal");
        var title = Label(_offerModal.transform, "Тело меняется — выберите мутацию", 44, TextAnchor.MiddleCenter,
            new Vector2(0, 300), new Vector2(1600, 70), new Vector2(0.5f, 0.5f));
        title.color = GameBootstrap.COutline;
        _offerCards = new GameObject("cards").AddComponent<RectTransform>();
        _offerCards.SetParent(_offerModal.transform, false);
        _offerModal.SetActive(false);
    }

    public void ShowOffer(string[] options, System.Action<string> onChoose) {
        for (int i = _offerCards.childCount - 1; i >= 0; i--) Destroy(_offerCards.GetChild(i).gameObject);
        int n = options.Length;
        for (int i = 0; i < n; i++) {
            string id = options[i];
            var def = MutationCatalog.Get(id);
            float x = (i - (n - 1) * 0.5f) * 560f;
            // плотная карточка: мир под модалкой не должен просвечивать сквозь текст
            var card = Button(_offerCards, "card_" + id, new Vector2(x, -40), new Vector2(520, 460),
                Color.Lerp(GameBootstrap.CSoul, Modal, 0.6f), 0.96f, "", 30);
            Label(card.transform, def != null ? def.Name : id, 40, TextAnchor.MiddleCenter,
                new Vector2(0, 150), new Vector2(480, 70), new Vector2(0.5f, 0.5f)).color = GameBootstrap.COutline;
            Label(card.transform, def != null ? def.Description : "", 28, TextAnchor.UpperCenter,
                new Vector2(0, -40), new Vector2(460, 280), new Vector2(0.5f, 0.5f));
            card.GetComponent<Button>().onClick.AddListener(() => {
                _offerModal.SetActive(false);
                onChoose(id);
            });
        }
        _offerModal.SetActive(true);
    }

    // ---------- Отпечаток жизни ----------
    private void BuildImprint() {
        _imprintModal = FullScreen("imprint_modal");
        var box = Box(_imprintModal.transform, "imprint", Vector2.zero, new Vector2(1500, 900), Modal, new Vector2(0.5f, 0.5f));
        Label(box, "Отпечаток жизни", 48, TextAnchor.MiddleCenter, new Vector2(0, 390), new Vector2(1400, 70),
            new Vector2(0.5f, 0.5f)).color = GameBootstrap.COutline;
        _imprintBody = Label(box, "", 30, TextAnchor.UpperLeft, new Vector2(-330, 90), new Vector2(720, 520), new Vector2(0.5f, 0.5f));
        _essenceText = Label(box, "", 34, TextAnchor.MiddleCenter, new Vector2(370, 300), new Vector2(640, 50), new Vector2(0.5f, 0.5f));
        _essenceText.color = GameBootstrap.CSoul;
        var stats = new[] { SoulStat.Energy, SoulStat.Endurance, SoulStat.Vitality };
        for (int i = 0; i < 3; i++) {
            var stat = stats[i];
            var b = Button(box, "up_" + stat, new Vector2(370, 180 - i * 130), new Vector2(640, 112),
                GameBootstrap.CSoul, 0.45f, "", 28);
            _upgradeLabels[i] = b.GetComponentInChildren<Text>();
            b.GetComponent<Button>().onClick.AddListener(() => _game.UpgradeSoul(stat));
        }
        var again = Button(box, "again", new Vector2(0, -360), new Vector2(620, 110), GameBootstrap.CFood, 0.9f,
            "НОВАЯ ЖИЗНЬ", 38);
        again.GetComponent<Button>().onClick.AddListener(() => _game.StartNewRun());
        _imprintModal.SetActive(false);
    }

    public void ShowImprint(LifeImprint im, bool victory, string killerText) {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(victory ? "Вы прошли пещеру до дна." : "Тело распалось. Душа помнит.");
        sb.AppendLine();
        sb.AppendLine(string.Format("Глубина: {0}   Комнат пройдено: {1}", im.DepthReached, im.RoomsCleared));
        sb.AppendLine(string.Format("Поглощено существ: {0}   Новых видов: {1}", im.Absorbed, im.NewSpecies));
        sb.AppendLine(string.Format("Пик массы: {0:0.0}   Лучший удар: {1:0.00}", im.PeakMass, im.BestHit));
        sb.AppendLine(string.Format("Биом: {0}   Лучшая цепочка: ×{1}", im.BiomeReached ?? "—", im.BestCombo));
        if (im.WardenSlain) sb.AppendLine("Страж глубины повержен!");
        if (im.Mutations != null && im.Mutations.Length > 0) {
            var names = new List<string>();
            foreach (var id in im.Mutations) { var d = MutationCatalog.Get(id); names.Add(d != null ? d.Name : id); }
            sb.AppendLine("Мутации: " + string.Join(", ", names));
        }
        if (!string.IsNullOrEmpty(killerText)) { sb.AppendLine(); sb.AppendLine(killerText); }
        sb.AppendLine();
        sb.AppendLine(string.Format("Эссенция за жизнь: +{0:0}", im.EssenceEarned));
        _imprintBody.text = sb.ToString();
        RefreshSoul();
        _imprintModal.SetActive(true);
    }

    public void HideImprint() { _imprintModal.SetActive(false); }

    public void RefreshSoul() {
        var meta = _game.Meta;
        _essenceText.text = string.Format("Эссенция души: {0:0}", meta.Essence);
        string[] names = { "Энергия — заряды рывка", "Выносливость — дешевле рывок", "Живучесть — больше стартовой массы" };
        var stats = new[] { SoulStat.Energy, SoulStat.Endurance, SoulStat.Vitality };
        for (int i = 0; i < 3; i++) {
            int lvl = meta.Level(stats[i]);
            _upgradeLabels[i].text = lvl >= SoulMeta.MaxLevel
                ? string.Format("{0}\nур. {1} (макс.)", names[i], lvl)
                : string.Format("{0}\nур. {1} → {2}   цена {3:0}", names[i], lvl, lvl + 1, SoulMeta.UpgradeCost(lvl));
        }
    }

    // ---------- Титул ----------
    private void BuildTitle() {
        _titleModal = FullScreen("title_modal");
        _titleModal.GetComponent<Image>().color = new Color(0f, 0.01f, 0.03f, 0.72f);
        // заставка (сгенерирована ИИ Z.ai по нашему описанию): слизень в грибной пещере — в центре, кнопки вокруг
        var art = Resources.Load<Texture2D>("AiArt/title");
        if (art != null) {
            _titleModal.GetComponent<Image>().color = new Color(0f, 0.01f, 0.03f, 1f);
            var artGo = new GameObject("title_art");
            artGo.transform.SetParent(_titleModal.transform, false);
            var raw = artGo.AddComponent<RawImage>();
            raw.texture = art;
            raw.raycastTarget = false;
            var art_rt = raw.rectTransform;
            art_rt.anchorMin = art_rt.anchorMax = art_rt.pivot = new Vector2(0.5f, 0.5f);
            // «обложка» на всю ширину; сдвинута вверх, чтобы мордочка слизня была над кнопкой «Начать жизнь»
            art_rt.sizeDelta = new Vector2(2500f, 2500f * art.height / art.width);
            art_rt.anchoredPosition = new Vector2(0f, 150f);
            var shade = new GameObject("title_shade");
            shade.transform.SetParent(_titleModal.transform, false);
            var sh = shade.AddComponent<Image>();
            sh.color = new Color(0f, 0.01f, 0.04f, 0.38f);
            sh.raycastTarget = false;
            var shrt = sh.rectTransform;
            shrt.anchorMin = Vector2.zero; shrt.anchorMax = Vector2.one; shrt.offsetMin = shrt.offsetMax = Vector2.zero;
        }
        var t = Label(_titleModal.transform, "ПАМЯТЬ МАТЕРИИ", 110, TextAnchor.MiddleCenter, new Vector2(0, 370), new Vector2(1800, 140), new Vector2(0.5f, 0.5f));
        t.color = GameBootstrap.COutline;
        Label(_titleModal.transform, "Ты — капля живой материи на дне тёмной пещеры.\nЕшь меньших. Бойся больших. Умри — и вернись сильнее.",
            34, TextAnchor.MiddleCenter, new Vector2(0, 250), new Vector2(1600, 110), new Vector2(0.5f, 0.5f));
        _titleSoul = Label(_titleModal.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(0, 178), new Vector2(1600, 50), new Vector2(0.5f, 0.5f));
        _titleSoul.color = GameBootstrap.CSoul;
        var play = Button(_titleModal.transform, "play", new Vector2(0, -420), new Vector2(620, 130), GameBootstrap.CFood, 0.95f, "НАЧАТЬ ЖИЗНЬ", 44);
        play.GetComponent<Button>().onClick.AddListener(() => {
            _titleModal.SetActive(false);
            PM.Systems.Input.PullGestureInput.Blocked = false;
            _game.OnTitleClosed();
            Hint("Тяните ПРОТИВ движения и отпустите", 4f);
        });
        var how = Button(_titleModal.transform, "how", new Vector2(-660, -60), new Vector2(460, 100), GameBootstrap.CSlime, 0.8f, "Как играть", 34);
        how.GetComponent<Button>().onClick.AddListener(() => ShowInfo("Как играть", HowToPlay()));
        var best = Button(_titleModal.transform, "bestiary", new Vector2(-660, -190), new Vector2(460, 100), GameBootstrap.CSoul, 0.8f, "Энциклопедия", 34);
        best.GetComponent<Button>().onClick.AddListener(() => _codex.Open());
        var settings = Button(_titleModal.transform, "settings", new Vector2(660, -60), new Vector2(460, 100), GameBootstrap.COutline, 0.6f, "Настройки", 32);
        settings.GetComponent<Button>().onClick.AddListener(() => _settings.Open());
        var credits = Button(_titleModal.transform, "credits", new Vector2(660, -190), new Vector2(460, 100), GameBootstrap.COutline, 0.6f, "Титры", 32);
        credits.GetComponent<Button>().onClick.AddListener(() => ShowInfo("Титры", CreditsText()));
        _titleModal.SetActive(false);
    }

    public void ShowTitle() {
        var m = _game.Meta;
        _titleSoul.text = m.Runs > 0
            ? string.Format("Прожито жизней: {0}   Глубочайшая: {1}   Эссенция души: {2:0}", m.Runs, m.BestDepth, m.Essence)
            : "Первая жизнь. Душа ещё ничего не помнит.";
        _titleModal.SetActive(true);
        PM.Systems.Input.PullGestureInput.Blocked = true;
    }

    private void BuildInfo() {
        _infoModal = FullScreen("info_modal");
        var box = Box(_infoModal.transform, "info", Vector2.zero, new Vector2(1500, 900), Modal, new Vector2(0.5f, 0.5f));
        _infoTitle = Label(box, "", 48, TextAnchor.MiddleCenter, new Vector2(0, 390), new Vector2(1400, 70), new Vector2(0.5f, 0.5f));
        _infoTitle.color = GameBootstrap.COutline;
        _infoBody = Label(box, "", 27, TextAnchor.UpperLeft, new Vector2(0, -20), new Vector2(1380, 690), new Vector2(0.5f, 0.5f));
        var back = Button(box, "back", new Vector2(0, -390), new Vector2(420, 90), GameBootstrap.CSlime, 0.7f, "Назад", 34);
        back.GetComponent<Button>().onClick.AddListener(() => _infoModal.SetActive(false));
        _infoModal.SetActive(false);
    }

    private void ShowInfo(string title, string body) {
        _infoTitle.text = title;
        _infoBody.text = body;
        _infoModal.SetActive(true);
        _infoModal.transform.SetAsLastSibling();
    }

    /// <summary>Авторы сторонних ресурсов: CC BY и OFL требуют указать авторство, CC0 — из благодарности.</summary>
    private static string CreditsText() {
        return "Память Материи — игра о капле живой материи.\n\n" +
               "Иконки: Lorc и Delapouite, game-icons.net — CC BY 3.0 (creativecommons.org/licenses/by/3.0)\n" +
               "Шрифты: Russo One (Jovanny Lemonad), Rubik (Hubert & Fischer, Cyreal) — SIL Open Font License 1.1\n" +
               "3D-монстры: Quaternius (quaternius.com) — CC0\n" +
               "Интерфейс и звуки: Kenney (kenney.nl) — CC0\n" +
               "Иллюстрации: созданы ИИ (генератор изображений Z.ai) по описаниям автора игры\n" +
               "Музыка: Paul Wortmann, JaggedStone (OpenGameArt.org) — CC0\n\n" +
               "Спасибо всем, кто делится своим творчеством свободно.";
    }

    private static string HowToPlay() {
        return "• Движение: оттяните палец ПРОТИВ направления и отпустите — рывок. Или включите джойстик в «Настройках».\n" +
               "• Кто меньше вас — поглощается касанием. Кто больше — ранит. Зелёное кольцо — можно съесть, красное — беги.\n" +
               "• Масса — это всё: здоровье, размер, сила. Рывок стоит капли массы, заряды копятся сами.\n" +
               "• Добыча даёт ресурсы. На них — «Эволюция» (новые способности) и «Показатели» (прокачка силы, ловкости…).\n" +
               "• Рост массы дарит жетоны: любой открытый узел эволюции бесплатно.\n" +
               "• «Деление» в эволюции: клоны охотятся сами и несут добычу — соберитесь, чтобы сдать её. Далеко уйдут — лопнут.\n" +
               "• Наберите массу — откроется шахта вниз. 5 глубин, на дне — Страж.\n" +
               "• Рикошет от стен разгоняет; броню пробивает только скорость.\n" +
               "• После смерти эссенция души усиливает следующие жизни. Всё увиденное — в «Энциклопедии».\n" +
               "• «Меню» (или кнопка «Назад») — пауза, энциклопедия, настройки.";
    }

    private string BestiaryText() {
        var seen = _game.Meta.SeenSpecies;
        var sb = new System.Text.StringBuilder();
        int known = 0;
        foreach (var sp in SpeciesCatalog.All()) {
            if (sp.Id == "spore") continue;
            if (seen != null && seen.Contains(sp.Id)) {
                known++;
                var gene = string.IsNullOrEmpty(sp.GeneMutationId) ? null : MutationCatalog.Get(sp.GeneMutationId);
                sb.Append("• ").Append(sp.Name);
                if (gene != null) sb.Append(" (ген: ").Append(gene.Name).Append(")");
                sb.Append(" — ").AppendLine(Bestiary.Describe(sp.Id));
            } else sb.AppendLine("• ??? — ещё не поглощён");
        }
        sb.Insert(0, string.Format("Изучено видов: {0}\n\n", known));
        return sb.ToString();
    }

    // ---------- Строители ----------
    private GameObject FullScreen(string name) {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-200, -200); rt.offsetMax = new Vector2(200, 200); // перекрыть и вне safe area
        var img = go.AddComponent<Image>();
        img.color = Dim;
        return go;
    }

    private static RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Vector2 anchor) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.sprite = SpriteUtil.UiRoundedSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        return rt;
    }

    private static GameObject Button(Transform parent, string name, Vector2 pos, Vector2 size, Color color,
                                     float alpha, string label, int labelSize) {
        var rt = Box(parent, name, pos, size, new Color(color.r, color.g, color.b, alpha), new Vector2(0.5f, 0.5f));
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = rt.GetComponent<Image>();
        if (ThirdPartyArt.Button != null && !string.IsNullOrEmpty(label)) rt.GetComponent<Image>().sprite = ThirdPartyArt.Button;
        btn.onClick.AddListener(() => ProceduralSfx.Instance?.Play("click"));
        var t = UiUtil.AddText(rt, label, labelSize, TextAnchor.MiddleCenter, TextColor);
        t.rectTransform.sizeDelta = size - new Vector2(24, 12);
        return rt.gameObject;
    }

    private static Text Label(Transform parent, string content, int size, TextAnchor align, Vector2 pos,
                              Vector2 box, Vector2 anchor) {
        var t = UiUtil.AddText(parent, content, size, align, TextColor);
        var rt = t.rectTransform;
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        t.raycastTarget = false;
        return t;
    }
}
}
