using UnityEngine;
using UnityEngine.UI;
using PM.Domain.Run;
using PM.Systems.Bootstrap;
using PM.Systems.Gameplay;
using PM.Systems.UI;

namespace PM.Systems.Run {
/// <summary>
/// Обучение первой жизни (v0.10): панель шага внизу по центру и прыгающая стрелка — на спору,
/// существо или нужную кнопку. Правила шагов — PM.Domain.Run.TutorialFlow; здесь только события и вид.
/// Пройдено (или пропущено) — запоминается в PlayerPrefs; «Обучение заново» — в настройках.
/// </summary>
public sealed class RunTutorial : MonoBehaviour {
    public const string DoneKey = "pm_tut_done";
    public static RunTutorial Instance { get; private set; }
    public TutorialFlow Flow { get; private set; } = new TutorialFlow(true);
    public bool Active => Flow.Active;

    private RunGame _game;
    private RectTransform _root, _panel, _arrow;
    private Text _text, _counter;
    private Image _arrowImg;
    private Vector2 _lastPos;
    private bool _hasPos;
    private int _lastFrags = 1;
    private TutorialStep _shown = TutorialStep.Done;

    public static RunTutorial Build(Transform canvas, RunGame game) {
        var go = new GameObject("tutorial");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<RunTutorial>();
        t._game = game;
        t._root = rt;
        t.BuildUi();
        Instance = t;
        return t;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Начать обучение, если игрок его ещё не прошёл (вызывается при старте жизни).</summary>
    public void BeginIfNeeded() {
        Flow = new TutorialFlow(PlayerPrefs.GetInt(DoneKey, 0) == 1);
        _hasPos = false;
        _lastFrags = 1;
        _shown = TutorialStep.Done;
    }

    public static void ResetProgress() { PlayerPrefs.DeleteKey(DoneKey); PlayerPrefs.Save(); }

    private void BuildUi() {
        _panel = Panel(_root, new Vector2(0, 30), new Vector2(1120, 116));
        _counter = Txt(_panel, "", 22, TextAnchor.UpperLeft, new Vector2(24, -10), new Vector2(300, 30), new Vector2(0, 1));
        _counter.color = UiSkin.Gold;
        _text = Txt(_panel, "", 28, TextAnchor.MiddleLeft, new Vector2(24, -36), new Vector2(860, 74), new Vector2(0, 1));
        var skip = new GameObject("skip");
        skip.transform.SetParent(_panel, false);
        var srt = skip.AddComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1, 0.5f);
        srt.anchoredPosition = new Vector2(-18, 0);
        srt.sizeDelta = new Vector2(190, 70);
        var simg = skip.AddComponent<Image>();
        UiSkin.AsPill(simg, new Color(0.45f, 0.5f, 0.6f, 0.9f));
        var b = skip.AddComponent<Button>();
        b.targetGraphic = simg;
        b.onClick.AddListener(() => { ProceduralSfx.Instance?.Play("click"); Finish(); });
        var st = UiUtil.AddText(srt, "Пропустить", 24, TextAnchor.MiddleCenter, Color.white);
        st.rectTransform.sizeDelta = new Vector2(180, 60);
        st.raycastTarget = false;

        var ag = new GameObject("arrow");
        ag.transform.SetParent(_root, false);
        _arrow = ag.AddComponent<RectTransform>();
        _arrow.sizeDelta = new Vector2(84, 84);
        _arrowImg = ag.AddComponent<Image>();
        _arrowImg.sprite = ArrowSprite();
        _arrowImg.color = UiSkin.Gold;
        _arrowImg.raycastTarget = false;
        _panel.gameObject.SetActive(false);
        _arrow.gameObject.SetActive(false);
    }

    void Update() {
        bool show = Flow.Active && _game != null && _game.InRun;
        if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
        if (!show) { if (_arrow.gameObject.activeSelf) _arrow.gameObject.SetActive(false); return; }
        transform.SetAsLastSibling();
        var run = _game.Run;
        bool paused = Time.timeScale <= 0f;
        // события, которые удобно опрашивать: движение, деление и сбор, окно эволюции
        var p = _game.PlayerPosition;
        if (_hasPos && !paused) Flow.OnMoved(Vector2.Distance(_lastPos, p));
        _lastPos = p; _hasPos = true;
        int frags = _game.FragmentCount;
        if (frags > _lastFrags) Flow.OnSplit();
        if (frags == 1 && _lastFrags > 1) Flow.OnMerged();
        _lastFrags = frags;
        if (_game.UiEvolutionOpen) Flow.OnEvolutionOpened();
        bool canEvolve = run.Evolution.FreeTokens > 0
            || run.Evolution.CanUnlock("div_split_basics", run.Resources, run.KnownSpecies) == UnlockBlock.None;
        if (!paused) Flow.Tick(Time.deltaTime, canEvolve);
        if (!Flow.Active) { Finish(); return; }
        if (Flow.Step != _shown) {
            _shown = Flow.Step;
            _text.text = Flow.Step == TutorialStep.Evolve && !canEvolve
                ? "Растите: съедайте всё зелёное — рост массы даст жетон эволюции"
                : TutorialFlow.Text(Flow.Step, GameSettings.Joystick);
            _counter.text = "Обучение · шаг " + (Flow.Index + 1) + " из " + TutorialFlow.Count;
            ProceduralSfx.Instance?.Play("pickup");
        } else if (Flow.Step == TutorialStep.Evolve && canEvolve && _text.text.StartsWith("Растите")) {
            _text.text = TutorialFlow.Text(Flow.Step, GameSettings.Joystick);
        }
        PlaceArrow(paused);
    }

    /// <summary>Стрелка над целью шага: существо в мире или кнопка интерфейса; на паузе — прячется.</summary>
    private void PlaceArrow(bool paused) {
        Vector2? screen = null;
        if (!paused && !_game.UiModalOpen) {
            switch (Flow.Step) {
                case TutorialStep.EatSpores: screen = World(_game.TutorialTarget(true)); break;
                case TutorialStep.Rings: screen = World(_game.TutorialTarget(false)); break;
                case TutorialStep.Dash: screen = Ui(GameSettings.Joystick ? _game.UiDashButton : null); break;
                case TutorialStep.Evolve: screen = Ui(_game.UiEvolutionButton); break;
                case TutorialStep.Split: screen = Ui(_game.SplitUnlocked ? _game.UiSplitButton : _game.UiEvolutionButton); break;
                case TutorialStep.Gather: screen = Ui(_game.UiGatherButton); break;
            }
        }
        bool on = screen.HasValue;
        if (_arrow.gameObject.activeSelf != on) _arrow.gameObject.SetActive(on);
        if (!on) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen.Value, null, out var local);
        float bob = 14f * Mathf.Sin(Time.unscaledTime * 6f);
        _arrow.anchoredPosition = local + new Vector2(0, 92f + bob);
        _arrowImg.color = new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
    }

    private static Vector2? World(Vector3? pos) {
        if (!pos.HasValue || Camera.main == null) return null;
        var sp = Camera.main.WorldToScreenPoint(pos.Value);
        if (sp.z < 0 || sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) return null;
        return new Vector2(sp.x, sp.y);
    }

    private static Vector2? Ui(RectTransform rt) {
        if (rt == null || !rt.gameObject.activeInHierarchy) return null;
        return RectTransformUtility.WorldToScreenPoint(null, rt.position);
    }

    private void Finish() {
        Flow.Skip();
        PlayerPrefs.SetInt(DoneKey, 1);
        PlayerPrefs.Save();
        _panel.gameObject.SetActive(false);
        _arrow.gameObject.SetActive(false);
    }

    // ---------- строители ----------
    private static RectTransform Panel(Transform parent, Vector2 pos, Vector2 size) {
        var go = new GameObject("tut_panel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        UiSkin.AsPanel(img, new Color(1f, 1f, 1f, 0.97f));
        return rt;
    }

    private static Text Txt(Transform parent, string s, int size, TextAnchor a, Vector2 pos, Vector2 box, Vector2 anchor) {
        var t = UiUtil.AddText(parent, s, size, a, new Color(0.95f, 0.98f, 1f));
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }

    /// <summary>Стрелка вниз со светлой кромкой (процедурно, без ассетов).</summary>
    private static Sprite ArrowSprite() {
        const int n = 96;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n;          // v: 0 — низ (остриё), 1 — верх
                float half = 0.42f * v;                                        // треугольник остриём вниз
                float d = Mathf.Max(Mathf.Abs(u) - half, Mathf.Max(0.08f - v, v - 0.92f));
                float a = Mathf.Clamp01(0.5f - d * n);
                float edge = Mathf.Clamp01(1f - Mathf.Abs(d * n + 3f) / 2.5f);
                var c = Color.Lerp(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 0.85f, 1f), edge);
                c.a = a;
                px[y * n + x] = c;
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
}
