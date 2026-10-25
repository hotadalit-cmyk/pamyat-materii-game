using UnityEngine;
using UnityEngine.UI;
using PM.Domain.Core;
using PM.Domain.Trail;
using PM.Persistence;
using PM.Systems.Bootstrap;
using PM.Systems.Gameplay;

namespace PM.Systems.UI {
/// <summary>
/// Мобильный HUD (VISUAL_UI): строится кодом, без ассетов.
/// Сетка под большой палец: safe-area уже применён на канве (SafeAreaFitter),
/// статусы — верхний левый угол, кнопки — верхний правый (тач-зона ≥ 100px при 1920×1080),
/// удержание — над нижней кромкой, подсказка — тянется на всю ширину.
/// CanvasScaler 1920×1080 по ширине + SafeAreaFitter (T01).
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class HudBuilder : MonoBehaviour {
    private static readonly Color PanelColor = new Color(0.016f, 0.05f, 0.09f, 0.62f);
    private static readonly Color ScreenColor = new Color(0.02f, 0.06f, 0.11f, 0.96f);

    public static void BuildIfMissing(Transform sceneRoot) {
        if (Object.FindFirstObjectByType<HudPresenter>() != null) return;
        var canvasGo = new GameObject("hud_canvas");
        canvasGo.transform.SetParent(sceneRoot, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f; // по ширине (landscape)
        canvasGo.AddComponent<SafeAreaFitter>();

        // --- верх слева: статусы (скруглённая панель + цветные маркеры) ---
        var top = MakePanelRounded(canvasGo.transform, "top_left", new Vector2(28, -28),
            new Vector2(620, 244), PanelColor, new Vector2(0, 1));
        var integrity = AddRow(top, "Целостность: 100%", 30, GameBootstrap.CFood, -34);
        var water = AddRow(top, "Вода: 5.00 кг", 30, GameBootstrap.CSlime, -90);
        var energy = AddRow(top, "Духовная энергия: 10/10", 30, GameBootstrap.CSoul, -146);
        var digestion = AddRow(top, "Пищеварение...", 26, GameBootstrap.CFood, -196);
        digestion.gameObject.SetActive(false);

        // --- верх справа: пауза + гены (крупные тач-зоны) ---
        var pauseBtn = MakeButtonRounded(canvasGo.transform, "pause_btn", "II",
            new Vector2(-28, -28), new Vector2(116, 116), GameBootstrap.COutline,
            new Vector2(1, 1), 36, Color.white, 0.28f);
        var genesBtn = MakeButtonRounded(canvasGo.transform, "genes_btn", "ГЕНЫ",
            new Vector2(-162, -28), new Vector2(260, 116), GameBootstrap.CSoul,
            new Vector2(1, 1), 30, Color.white, 0.88f);

        // --- низ по центру: удержание (над нижней кромкой, вне зоны большого пальца) ---
        var captureBar = MakePanelRounded(canvasGo.transform, "capture_bar", new Vector2(0, 232),
            new Vector2(920, 80), PanelColor, new Vector2(0.5f, 0));
        captureBar.gameObject.SetActive(false);
        var captureText = UiUtil.AddText(captureBar, "Удержание...", 26, TextAnchor.MiddleLeft);
        captureText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        captureText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        captureText.rectTransform.anchoredPosition = new Vector2(-140, 0);
        captureText.rectTransform.sizeDelta = new Vector2(620, 44);
        var captureFillGo = new GameObject("capture_fill");
        captureFillGo.transform.SetParent(captureBar, false);
        var fillImg = captureFillGo.AddComponent<Image>();
        fillImg.color = GameBootstrap.CFood;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        var fillRt = fillImg.rectTransform;
        fillRt.anchorMin = new Vector2(0.5f, 0.5f);
        fillRt.anchorMax = new Vector2(0.5f, 0.5f);
        fillRt.anchoredPosition = new Vector2(285, 0);
        fillRt.sizeDelta = new Vector2(300, 28);

        // --- подсказка: тянется на всю ширину, не обрезается ---
        var hint = UiUtil.AddText(canvasGo.transform,
            "Оттяните палец ПРОТИВ направления движения и отпустите", 27, TextAnchor.MiddleCenter,
            new Color(0.92f, 0.96f, 1f, 0.88f));
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0, 96);
        hintRt.sizeDelta = new Vector2(-96, 48);

        // --- presenter ---
        var presenterGo = new GameObject("hud_presenter");
        presenterGo.transform.SetParent(canvasGo.transform, false);
        var hud = presenterGo.AddComponent<HudPresenter>();
        hud.IntegrityText = integrity;
        hud.WaterText = water;
        hud.EnergyText = energy;
        hud.DigestionText = digestion;
        hud.CaptureText = captureText;
        hud.CaptureFill = fillImg;
        hud.HintText = hint;
        presenterGo.AddComponent<TrailDebugNote>();

        // --- экран смерти ---
        var deathGo = MakePanelRounded(canvasGo.transform, "death_screen", Vector2.zero,
            new Vector2(1180, 680), ScreenColor, new Vector2(0.5f, 0.5f)).gameObject;
        var death = deathGo.AddComponent<DeathScreenUI>();
        var summary = UiUtil.AddText(deathGo.transform, "Отпечаток жизни", 32, TextAnchor.UpperCenter);
        summary.rectTransform.sizeDelta = new Vector2(1080, 420);
        summary.rectTransform.anchoredPosition = new Vector2(0, 96);
        death.SummaryText = summary;
        death.Panel = deathGo;
        var reGo = MakeButtonRounded(deathGo.transform, "reincarnate_btn", "РЕИНКАРНИРОВАТЬСЯ",
            new Vector2(0, -256), new Vector2(640, 104), GameBootstrap.CFood,
            new Vector2(0.5f, 0.5f), 32, HudTextColor(), 0.92f);
        death.ReincarnateButton = reGo.GetComponent<Button>();
        reGo.GetComponent<Button>().onClick.AddListener(death.OnReincarnate);
        deathGo.SetActive(false);

        // --- пауза-панель ---
        var panelGo = MakePanelRounded(canvasGo.transform, "pause_panel", Vector2.zero,
            new Vector2(960, 760), ScreenColor, new Vector2(0.5f, 0.5f)).gameObject;
        var pause = panelGo.AddComponent<PauseMenuUI>();
        pause.Panel = panelGo;
        pause.PauseButton = pauseBtn.GetComponent<Button>();
        var resume = MakeButtonRounded(panelGo.transform, "resume_btn", "ПРОДОЛЖИТЬ",
            new Vector2(0, 262), new Vector2(660, 100), GameBootstrap.CFood,
            new Vector2(0.5f, 0.5f), 30, HudTextColor(), 0.92f);
        pause.ResumeButton = resume.GetComponent<Button>();
        resume.GetComponent<Button>().onClick.AddListener(() => pause.SetPaused(false));
        pause.SaveSlot0 = MakeButtonRounded(panelGo.transform, "save0", "Сохранить слот 1",
            new Vector2(-240, 100), new Vector2(436, 88), GameBootstrap.CSlime,
            new Vector2(0.5f, 0.5f), 26, HudTextColor(), 0.8f).GetComponent<Button>();
        pause.SaveSlot1 = MakeButtonRounded(panelGo.transform, "save1", "Сохранить слот 2",
            new Vector2(240, 100), new Vector2(436, 88), GameBootstrap.CSlime,
            new Vector2(0.5f, 0.5f), 26, HudTextColor(), 0.8f).GetComponent<Button>();
        pause.LoadSlot0 = MakeButtonRounded(panelGo.transform, "load0", "Загрузить слот 1",
            new Vector2(-240, -8), new Vector2(436, 88), GameBootstrap.CSlime,
            new Vector2(0.5f, 0.5f), 26, HudTextColor(), 0.8f).GetComponent<Button>();
        pause.LoadSlot1 = MakeButtonRounded(panelGo.transform, "load1", "Загрузить слот 2",
            new Vector2(240, -8), new Vector2(436, 88), GameBootstrap.CSlime,
            new Vector2(0.5f, 0.5f), 26, HudTextColor(), 0.8f).GetComponent<Button>();
        var status = UiUtil.AddText(panelGo.transform, "", 26, TextAnchor.MiddleCenter);
        status.rectTransform.anchoredPosition = new Vector2(0, -156);
        status.rectTransform.sizeDelta = new Vector2(860, 44);
        pause.StatusText = status;
        panelGo.SetActive(false); // Awake не успел скрыть (Panel присваивается после AddComponent)

        // --- экран генов ---
        var geneGo = MakePanelRounded(canvasGo.transform, "gene_screen", Vector2.zero,
            new Vector2(1080, 840), ScreenColor, new Vector2(0.5f, 0.5f)).gameObject;
        var gene = geneGo.AddComponent<GeneScreenUI>();
        gene.Panel = geneGo;
        gene.OpenButton = genesBtn.GetComponent<Button>();
        genesBtn.GetComponent<Button>().onClick.AddListener(gene.Toggle);
        var listGo = new GameObject("node_list");
        listGo.transform.SetParent(geneGo.transform, false);
        var listRt = listGo.AddComponent<RectTransform>();
        listRt.sizeDelta = new Vector2(960, 600);
        listRt.anchoredPosition = new Vector2(0, 66);
        listGo.AddComponent<VerticalLayoutGroup>();
        gene.ListRoot = (RectTransform)listGo.transform;
        var geneStatus = UiUtil.AddText(geneGo.transform, "Генетическая сеть", 26, TextAnchor.MiddleCenter);
        geneStatus.rectTransform.anchoredPosition = new Vector2(0, -356);
        geneStatus.rectTransform.sizeDelta = new Vector2(960, 44);
        gene.StatusText = geneStatus;
        geneGo.SetActive(false);

        // биндинг выполнится в GameBootstrap.Awake → HudPresenter.Bind
        var boot = Object.FindFirstObjectByType<GameBootstrap>();
        if (boot != null) hud.Bind(boot);
    }

    private static Color HudTextColor() {
        return new Color(0.92f, 0.96f, 1f);
    }

    /// <summary>Строка статуса: цветной маркер + текст. Возвращает Text для биндинга.</summary>
    private static Text AddRow(Transform parent, string content, int size, Color dotColor, float y) {
        var dotGo = new GameObject("dot");
        dotGo.transform.SetParent(parent, false);
        var dotRt = dotGo.AddComponent<RectTransform>();
        dotRt.anchorMin = new Vector2(0, 1);
        dotRt.anchorMax = new Vector2(0, 1);
        dotRt.pivot = new Vector2(0, 1);
        dotRt.anchoredPosition = new Vector2(34, y - 3);
        dotRt.sizeDelta = new Vector2(26, 26);
        var dotImg = dotGo.AddComponent<Image>();
        dotImg.sprite = SpriteUtil.UiCircleSprite;
        dotImg.color = dotColor;
        var txt = UiUtil.AddText(parent, content, size, TextAnchor.MiddleLeft);
        txt.rectTransform.anchorMin = new Vector2(0, 1);
        txt.rectTransform.anchorMax = new Vector2(0, 1);
        txt.rectTransform.pivot = new Vector2(0, 1);
        txt.rectTransform.anchoredPosition = new Vector2(76, y);
        txt.rectTransform.sizeDelta = new Vector2(510, 36);
        return txt;
    }

    private static RectTransform MakePanelRounded(Transform parent, string name, Vector2 pos,
                                                  Vector2 size, Color color, Vector2 anchor) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.sprite = SpriteUtil.UiRoundedSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        return rt;
    }

    private static GameObject MakeButtonRounded(Transform parent, string name, string label,
                                                Vector2 pos, Vector2 size, Color color,
                                                Vector2 anchor, int labelSize,
                                                Color? labelColor = null, float bgAlpha = 0.35f) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.sprite = SpriteUtil.UiRoundedSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(color.r, color.g, color.b, bgAlpha);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var txt = UiUtil.AddText(go.transform, label, labelSize, TextAnchor.MiddleCenter,
            labelColor ?? new Color(0.92f, 0.96f, 1f));
        txt.rectTransform.sizeDelta = size;
        return go;
    }
}
/// <summary>Заглушка-заметка: удерживает ссылку на след для инспектора (без логики).</summary>
public sealed class TrailDebugNote : MonoBehaviour {
    public TrailLedger LastSeen;
    void Update() {
        var boot = FindFirstObjectByType<GameBootstrap>();
        if (boot != null && boot.Sim != null) LastSeen = boot.Sim.Trail;
    }
}
}
