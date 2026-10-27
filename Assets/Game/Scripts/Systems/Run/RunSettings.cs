using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PM.Systems.Bootstrap;
using PM.Systems.Gameplay;
using PM.Systems.Input;
using PM.Systems.UI;

namespace PM.Systems.Run {
/// <summary>
/// Настройки игрока (v0.8): хранятся в PlayerPrefs, применяются сразу.
/// Звук, вибрация, тряска, чувствительность рывка, отдаление камеры, частота кадров, подсказки, рука.
/// </summary>
public static class GameSettings {
    private const string P = "pm_set_";
    public static event System.Action Changed;

    /// <summary>Громкость музыки и тревоги (0..1), отдельно от общей.</summary>
    public static float MusicVolume { get => PlayerPrefs.GetFloat(P + "music", 0.7f); set { PlayerPrefs.SetFloat(P + "music", Mathf.Clamp01(value)); Apply(); } }
    public static float Volume { get => ProceduralSfx.CurrentVolume(); set { PlayerPrefs.SetFloat(ProceduralSfx.PrefKey, Mathf.Clamp01(value)); Apply(); } }
    public static bool Vibration { get => PlayerPrefs.GetInt(P + "vibro", 1) == 1; set { PlayerPrefs.SetInt(P + "vibro", value ? 1 : 0); Apply(); } }
    public static float Shake { get => PlayerPrefs.GetFloat(P + "shake", 1f); set { PlayerPrefs.SetFloat(P + "shake", value); Apply(); } }
    /// <summary>Длина оттягивания до полного рывка (мир. ед.): меньше — чувствительнее.</summary>
    public static float DragLength { get => PlayerPrefs.GetFloat(P + "drag", 3.2f); set { PlayerPrefs.SetFloat(P + "drag", value); Apply(); } }
    public static float CameraZoom { get => PlayerPrefs.GetFloat(P + "zoom", 1f); set { PlayerPrefs.SetFloat(P + "zoom", value); Apply(); } }
    public static int Fps { get => PlayerPrefs.GetInt(P + "fps", 60); set { PlayerPrefs.SetInt(P + "fps", value); Apply(); } }
    public static bool Hints { get => PlayerPrefs.GetInt(P + "hints", 1) == 1; set { PlayerPrefs.SetInt(P + "hints", value ? 1 : 0); Apply(); } }
    public static bool LeftHanded { get => PlayerPrefs.GetInt(P + "left", 0) == 1; set { PlayerPrefs.SetInt(P + "left", value ? 1 : 0); Apply(); } }
    /// <summary>Управление: false — оттянуть и отпустить (рывок жестом), true — джойстик + кнопка рывка.</summary>
    public static bool Joystick { get => PlayerPrefs.GetInt(P + "joy", 0) == 1; set { PlayerPrefs.SetInt(P + "joy", value ? 1 : 0); Apply(); } }

    public static void Apply() {
        PlayerPrefs.Save();
        AudioListener.volume = Volume;
        Application.targetFrameRate = Fps;
        if (PullGestureInput.Instance != null) {
            PullGestureInput.Instance.DragMaxWorld = DragLength;
            PullGestureInput.Instance.enabled = !Joystick; // в режиме джойстика жест рывка выключен
        }
        PM.Systems.CameraRig.FollowCamera.UserZoom = CameraZoom;
        Changed?.Invoke();
    }

    public static void ResetDefaults() {
        foreach (var k in new[] { "vibro", "shake", "drag", "zoom", "fps", "hints", "left", "joy", "music" }) PlayerPrefs.DeleteKey(P + k);
        PlayerPrefs.SetFloat(ProceduralSfx.PrefKey, 0.8f);
        Apply();
    }
}

/// <summary>Окно настроек: строки с крупными сегментными переключателями (удобно пальцем).</summary>
public sealed class RunSettingsUi : MonoBehaviour {
    private static readonly Color Modal = new Color(0.02f, 0.05f, 0.1f, 0.98f);
    private static readonly Color TextC = new Color(0.92f, 0.96f, 1f);
    private static readonly Color On = new Color(0.35f, 0.75f, 1f, 0.95f);
    private static readonly Color Off = new Color(0.12f, 0.18f, 0.24f, 0.95f);

    private RunGame _game;
    private GameObject _modal;
    private Transform _rows;
    private bool _pausedByMe;
    private int _resetStage;
    private Text _resetLabel;

    public bool IsOpen => _modal != null && _modal.activeSelf;

    public static RunSettingsUi Build(Transform canvas, RunGame game) {
        var go = new GameObject("settings_ui");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var ui = go.AddComponent<RunSettingsUi>();
        ui._game = game;
        ui.BuildModal(go.transform);
        return ui;
    }

    private void BuildModal(Transform root) {
        _modal = new GameObject("settings_modal");
        _modal.transform.SetParent(root, false);
        var mrt = _modal.AddComponent<RectTransform>();
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(-200, -200); mrt.offsetMax = new Vector2(200, 200);
        _modal.AddComponent<Image>().color = new Color(0, 0, 0, 0.82f);
        var box = Box(_modal.transform, new Vector2(1500, 1060), Modal);
        Text(box, "НАСТРОЙКИ", 46, TextAnchor.MiddleLeft, new Vector2(40, -20), new Vector2(600, 60)).color = GameBootstrap.COutline;
        var close = Btn(box, "Закрыть", new Vector2(-24, -18), new Vector2(190, 74), GameBootstrap.CSlime, new Vector2(1, 1));
        close.onClick.AddListener(Close);
        var rows = new GameObject("rows");
        rows.transform.SetParent(box, false);
        var rrt = rows.AddComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0, 1);
        rrt.anchoredPosition = new Vector2(40, -110);
        rrt.sizeDelta = new Vector2(1420, 880);
        _rows = rows.transform;
        _modal.SetActive(false);
    }

    public void Open() {
        _modal.SetActive(true);
        transform.SetAsLastSibling();
        _resetStage = 0;
        _pausedByMe = _game != null && _game.InRun && Time.timeScale > 0f;
        if (_pausedByMe) _game.SetPaused(true);
        Rebuild();
    }

    /// <summary>Кнопка «Назад»: закрыть, если открыто.</summary>
    public bool CloseIfOpen() { if (!IsOpen) return false; Close(); return true; }

    private void Close() {
        _modal.SetActive(false);
        if (_pausedByMe) _game.SetPaused(false);
        _pausedByMe = false;
    }

    private void Rebuild() {
        for (int i = _rows.childCount - 1; i >= 0; i--) Destroy(_rows.GetChild(i).gameObject);
        float y = 0;
        Row(ref y, "Громкость", new[] { "0", "25", "50", "75", "100" }, Mathf.RoundToInt(GameSettings.Volume * 4f),
            i => GameSettings.Volume = i / 4f);
        Row(ref y, "Музыка", new[] { "0", "25", "50", "75", "100" }, Mathf.RoundToInt(GameSettings.MusicVolume * 4f),
            i => GameSettings.MusicVolume = i / 4f);
        Row(ref y, "Управление", new[] { "Оттянуть и отпустить", "Джойстик + рывок" }, GameSettings.Joystick ? 1 : 0,
            i => GameSettings.Joystick = i == 1);
        Row(ref y, "Вибрация", new[] { "Выкл", "Вкл" }, GameSettings.Vibration ? 1 : 0, i => GameSettings.Vibration = i == 1);
        Row(ref y, "Тряска экрана", new[] { "Нет", "Мягко", "Сильно" }, IndexOf(GameSettings.Shake, 0f, 0.5f, 1f),
            i => GameSettings.Shake = new[] { 0f, 0.5f, 1f }[i]);
        Row(ref y, "Чувствительность рывка", new[] { "Низкая", "Средняя", "Высокая" }, IndexOf(GameSettings.DragLength, 4.2f, 3.2f, 2.3f),
            i => GameSettings.DragLength = new[] { 4.2f, 3.2f, 2.3f }[i]);
        Row(ref y, "Камера", new[] { "Ближе", "Обычно", "Дальше" }, IndexOf(GameSettings.CameraZoom, 0.8f, 1f, 1.3f),
            i => GameSettings.CameraZoom = new[] { 0.8f, 1f, 1.3f }[i]);
        Row(ref y, "Частота кадров", new[] { "30 (экономия)", "60 (плавно)" }, GameSettings.Fps >= 60 ? 1 : 0,
            i => GameSettings.Fps = i == 1 ? 60 : 30);
        Row(ref y, "Подсказки", new[] { "Выкл", "Вкл" }, GameSettings.Hints ? 1 : 0, i => GameSettings.Hints = i == 1);
        Row(ref y, "Кнопки деления", new[] { "Справа", "Слева" }, GameSettings.LeftHanded ? 1 : 0, i => GameSettings.LeftHanded = i == 1);
        // служебное
        var defaults = Btn(_rows, "Сбросить настройки", new Vector2(0, y - 20), new Vector2(440, 84), GameBootstrap.CSlime, new Vector2(0, 1));
        defaults.onClick.AddListener(() => { GameSettings.ResetDefaults(); Rebuild(); });
        var tut = Btn(_rows, "Обучение заново", new Vector2(1080, y - 20), new Vector2(340, 84), GameBootstrap.CSoul, new Vector2(0, 1));
        tut.onClick.AddListener(() => {
            RunTutorial.ResetProgress();
            tut.GetComponentInChildren<Text>().text = "Со следующей жизни";
        });
        var reset = Btn(_rows, "Сбросить прогресс души", new Vector2(480, y - 20), new Vector2(560, 84), GameBootstrap.CDanger, new Vector2(0, 1));
        _resetLabel = reset.GetComponentInChildren<Text>();
        reset.onClick.AddListener(() => {
            _resetStage++;
            if (_resetStage == 1) _resetLabel.text = "Точно? Нажмите ещё раз";
            else { _game.ResetSoul(); _resetLabel.text = "Душа очищена"; _resetStage = 0; }
        });
        y -= 100f;
        if (DevTools.Enabled) BuildDevRow(ref y); // v0.13: секция «Для тестов» — только при DevTools.Enabled
    }

    /// <summary>v0.14: кнопки «Страж 1…5» в строке заголовка настроек — новая жизнь сразу у стража глубины.</summary>
    private void BuildDevRow(ref float y) {
        var devCol = new Color(0.85f, 0.55f, 0.3f, 0.95f);
        for (int i = 0; i < PM.Domain.Run.RunState.MaxDepth; i++) {
            int depth = i + 1;
            var b = Btn(_rows, "Страж " + depth, new Vector2(330f + i * 140f, 112f), new Vector2(130, 60), devCol, new Vector2(0, 1));
            b.onClick.AddListener(() => {
                _game.StartDevGuardianRun(depth);
                Close();
            });
        }
    }

    private static int IndexOf(float v, params float[] opts) {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < opts.Length; i++) { float d = Mathf.Abs(opts[i] - v); if (d < bd) { bd = d; best = i; } }
        return best;
    }

    private void Row(ref float y, string label, string[] options, int selected, System.Action<int> onPick) {
        Text(_rows, label, 30, TextAnchor.MiddleLeft, new Vector2(0, y - 12), new Vector2(460, 70));
        float x = 480f, w = (1420f - 480f) / options.Length;
        for (int i = 0; i < options.Length; i++) {
            int idx = i;
            var b = Btn(_rows, options[i], new Vector2(x + i * w, y - 6), new Vector2(w - 12, 68), i == selected ? On : Off, new Vector2(0, 1));
            b.onClick.AddListener(() => { onPick(idx); ProceduralSfx.Instance?.Play("click"); Rebuild(); });
        }
        y -= 80f;
    }

    // ---------- строители ----------
    private static RectTransform Box(Transform parent, Vector2 size, Color c) {
        var go = new GameObject("box");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        UiSkin.Box(img, c, size);
        return rt;
    }

    private static Button Btn(Transform parent, string label, Vector2 pos, Vector2 size, Color c, Vector2 anchor) {
        var go = new GameObject("btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        UiSkin.AsPill(img, c);
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() => ProceduralSfx.Instance?.Play("click"));
        var t = UiUtil.AddText(rt, label, 26, TextAnchor.MiddleCenter, TextC);
        t.rectTransform.sizeDelta = size - new Vector2(12, 6);
        t.raycastTarget = false;
        return b;
    }

    private static Text Text(Transform parent, string s, int size, TextAnchor a, Vector2 pos, Vector2 box) {
        var t = UiUtil.AddText(parent, s, size, a, TextC);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        t.raycastTarget = false;
        return t;
    }
}
}
