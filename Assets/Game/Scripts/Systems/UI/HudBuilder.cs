using UnityEngine;
using UnityEngine.UI;
using PM.Domain.Core;
using PM.Domain.Trail;
using PM.Persistence;
using PM.Systems.Bootstrap;

namespace PM.Systems.UI {
/// <summary>
/// Построитель HUD-канвы кодом (создаётся в Boot-сцене редакторским меню PM/Setup/01,
/// но также умеет достроить себя при отсутствии — устойчивость к ручным правкам).
/// CanvasScaler 1920×1080 по ширине + SafeAreaFitter (T01).
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class HudBuilder : MonoBehaviour {
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

        // --- верх слева: статусы ---
        var top = MakePanel(canvasGo.transform, "top_left", new Vector2(24, -24),
            new Vector2(560, 220), new Color(0, 0, 0, 0.25f));
        var integrity = UiUtil.AddText(top, "Целостность: 100%", 30, TextAnchor.UpperLeft);
        integrity.rectTransform.anchoredPosition = new Vector2(0, -32);
        var water = UiUtil.AddText(top, "Вода: 5.00 кг", 30, TextAnchor.UpperLeft);
        water.rectTransform.anchoredPosition = new Vector2(0, -74);
        var energy = UiUtil.AddText(top, "Духовная энергия: 10/10", 30, TextAnchor.UpperLeft);
        energy.rectTransform.anchoredPosition = new Vector2(0, -116);
        var digestion = UiUtil.AddText(top, "Пищеварение...", 26, TextAnchor.UpperLeft);
        digestion.rectTransform.anchoredPosition = new Vector2(0, -158);
        digestion.gameObject.SetActive(false);

        // --- верх справа: пауза + гены ---
        var pauseBtn = MakeButton(canvasGo.transform, "pause_btn", "II",
            new Vector2(-70, -40), new Vector2(90, 70), GameBootstrap.COutline);
        var genesBtn = MakeButton(canvasGo.transform, "genes_btn", "ГЕНЫ",
            new Vector2(-200, -40), new Vector2(180, 70), GameBootstrap.CSoul);

        // --- низ: удержание + подсказка ---
        var captureBar = MakePanel(canvasGo.transform, "capture_bar", new Vector2(0, 70),
            new Vector2(760, 64), new Color(0, 0, 0, 0.35f));
        captureBar.gameObject.SetActive(false);
        var captureText = UiUtil.AddText(captureBar, "Удержание...", 26, TextAnchor.MiddleLeft);
        captureText.rectTransform.anchoredPosition = new Vector2(-360, 0);
        captureText.alignment = TextAnchor.MiddleLeft;
        var captureFillGo = new GameObject("capture_fill");
        captureFillGo.transform.SetParent(captureBar, false);
        var fillImg = captureFillGo.AddComponent<Image>();
        fillImg.color = GameBootstrap.CFood;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        var fillRt = fillImg.rectTransform;
        fillRt.anchorMin = new Vector2(0.5f, 0.5f);
        fillRt.anchorMax = new Vector2(0.5f, 0.5f);
        fillRt.anchoredPosition = new Vector2(120, 0);
        fillRt.sizeDelta = new Vector2(440, 30);

        var hint = UiUtil.AddText(canvasGo.transform,
            "Оттяните палец ПРОТИВ направления движения и отпустите", 28, TextAnchor.LowerCenter);
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0.5f, 0);
        hintRt.anchorMax = new Vector2(0.5f, 0);
        hintRt.anchoredPosition = new Vector2(0, 140);

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
        var deathGo = new GameObject("death_screen");
        deathGo.transform.SetParent(canvasGo.transform, false);
        var deathRt = deathGo.AddComponent<RectTransform>();
        deathRt.sizeDelta = new Vector2(1100, 640);
        var deathImg = deathGo.AddComponent<Image>();
        deathImg.color = new Color(0.04f, 0.09f, 0.14f, 0.95f);
        var death = deathGo.AddComponent<DeathScreenUI>();
        var summary = UiUtil.AddText(deathGo.transform, "Отпечаток жизни", 34, TextAnchor.UpperCenter);
        summary.rectTransform.sizeDelta = new Vector2(1040, 380);
        summary.rectTransform.anchoredPosition = new Vector2(0, 80);
        death.SummaryText = summary;
        death.Panel = deathGo;
        var reGo = MakeButton(deathGo.transform, "reincarnate_btn", "РЕИНКАРНИРОВАТЬСЯ",
            new Vector2(0, -220), new Vector2(560, 90), GameBootstrap.CFood);
        death.ReincarnateButton = reGo.GetComponent<Button>();
        reGo.GetComponent<Button>().onClick.AddListener(death.OnReincarnate);
        deathGo.SetActive(false);

        // --- пауза-панель ---
        var panelGo = new GameObject("pause_panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(900, 700);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.04f, 0.09f, 0.14f, 0.95f);
        var pause = panelGo.AddComponent<PauseMenuUI>();
        pause.Panel = panelGo;
        pause.PauseButton = pauseBtn.GetComponent<Button>();
        var resume = MakeButton(panelGo.transform, "resume_btn", "ПРОДОЛЖИТЬ",
            new Vector2(0, 240), new Vector2(600, 84), GameBootstrap.CFood);
        pause.ResumeButton = resume.GetComponent<Button>();
        resume.GetComponent<Button>().onClick.AddListener(() => pause.SetPaused(false));
        pause.SaveSlot0 = MakeButton(panelGo.transform, "save0", "Сохранить слот 1",
            new Vector2(-190, 80), new Vector2(380, 74), GameBootstrap.CSlime).GetComponent<Button>();
        pause.SaveSlot1 = MakeButton(panelGo.transform, "save1", "Сохранить слот 2",
            new Vector2(190, 80), new Vector2(380, 74), GameBootstrap.CSlime).GetComponent<Button>();
        pause.LoadSlot0 = MakeButton(panelGo.transform, "load0", "Загрузить слот 1",
            new Vector2(-190, -10), new Vector2(380, 74), GameBootstrap.CSlime).GetComponent<Button>();
        pause.LoadSlot1 = MakeButton(panelGo.transform, "load1", "Загрузить слот 2",
            new Vector2(190, -10), new Vector2(380, 74), GameBootstrap.CSlime).GetComponent<Button>();
        var status = UiUtil.AddText(panelGo.transform, "", 24, TextAnchor.MiddleCenter);
        status.rectTransform.anchoredPosition = new Vector2(0, -120);
        pause.StatusText = status;

        // --- экран генов ---
        var geneGo = new GameObject("gene_screen");
        geneGo.transform.SetParent(canvasGo.transform, false);
        var geneRt = geneGo.AddComponent<RectTransform>();
        geneRt.sizeDelta = new Vector2(1000, 800);
        var geneImg = geneGo.AddComponent<Image>();
        geneImg.color = new Color(0.04f, 0.09f, 0.14f, 0.96f);
        var gene = geneGo.AddComponent<GeneScreenUI>();
        gene.Panel = geneGo;
        gene.OpenButton = genesBtn.GetComponent<Button>();
        genesBtn.GetComponent<Button>().onClick.AddListener(gene.Toggle);
        var listGo = new GameObject("node_list");
        listGo.transform.SetParent(geneGo.transform, false);
        var listRt = listGo.AddComponent<RectTransform>();
        listRt.sizeDelta = new Vector2(900, 560);
        listRt.anchoredPosition = new Vector2(0, 40);
        listGo.AddComponent<VerticalLayoutGroup>();
        gene.ListRoot = (RectTransform)listGo.transform;
        var geneStatus = UiUtil.AddText(geneGo.transform, "Генетическая сеть", 26, TextAnchor.MiddleCenter);
        geneStatus.rectTransform.anchoredPosition = new Vector2(0, -330);
        gene.StatusText = geneStatus;
        geneGo.SetActive(false);

        // биндинг выполнится в GameBootstrap.Awake → HudPresenter.Bind
        var boot = Object.FindFirstObjectByType<GameBootstrap>();
        if (boot != null) hud.Bind(boot);
    }

    private static RectTransform MakePanel(Transform parent, string name, Vector2 pos,
                                          Vector2 size, Color color) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = color;
        return rt;
    }

    private static GameObject MakeButton(Transform parent, string name, string label,
                                         Vector2 pos, Vector2 size, Color color) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = new Color(color.r, color.g, color.b, 0.35f);
        var btn = go.AddComponent<Button>();
        var txt = UiUtil.AddText(go.transform, label, 28, TextAnchor.MiddleCenter);
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
