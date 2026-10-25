using UnityEngine;
using UnityEngine.UI;
using PM.Domain.Genetics;
using PM.Systems.Bootstrap;

namespace PM.Systems.UI {
/// <summary>Экран генетической сети (T11): узлы с требованиями и стоимостью; разблокировка кнопкой.</summary>
public sealed class GeneScreenUI : MonoBehaviour {
    public static GeneScreenUI Instance { get; private set; }
    public GameObject Panel;
    public RectTransform ListRoot;
    public Text StatusText;
    public Button OpenButton;
    public Button CloseButton;
    private GameBootstrap _game;
    private string _lastResult = "";
    private bool _wired;

    void Awake() { Instance = this; }

    private GameBootstrap Game {
        get { if (_game == null) _game = GameBootstrap.Instance; return _game; }
    }

    /// <summary>Идемпотентная навеска слушателей: в запечённой сцене code-added
    /// listeners не выживают пересохранение, Awake неактивной панели не запускается.</summary>
    public void Wire() {
        Instance = this;
        if (_wired) return;
        _wired = true;
        if (OpenButton != null) OpenButton.onClick.AddListener(Toggle);
    }

    public void Toggle() {
        if (Panel == null) return;
        bool next = !Panel.activeSelf;
        Panel.SetActive(next);
        if (!next) return;
        EnsureFormsRow();
        Rebuild();
    }
    public void Close() { if (Panel != null) Panel.SetActive(false); }

    /// <summary>Раздел «Формы» (T12): достраивается в запечённой сцене, идемпотентно.</summary>
    private void EnsureFormsRow() {
        if (Panel == null || Panel.transform.Find("forms_row") != null) return;
        var rt = Panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1000, 940); // было 1000×800 — место под формы
        var header = UiUtil.AddText(Panel.transform, "ФОРМЫ:", 26, TextAnchor.MiddleLeft);
        header.rectTransform.anchoredPosition = new Vector2(-330, -252);
        header.rectTransform.sizeDelta = new Vector2(300, 36);
        var row = new GameObject("forms_row");
        row.transform.SetParent(Panel.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(900, 84);
        rowRt.anchoredPosition = new Vector2(0, -330);
        AddFormButton("form_chitin_slime", "Хитиновая модиф.\n(орг 2 · хитин 5)");
        AddFormButton("form_beetle", "Форма жука\n(орг 8 · хитин 10 · мин 4)");
        AddFormButton("form_slime", "Вернуться в слизь\n(возврат 80%)");
        if (StatusText != null)
            StatusText.rectTransform.anchoredPosition = new Vector2(0, -420);
    }

    private void AddFormButton(string formId, string label) {
        var row = Panel.transform.Find("forms_row");
        const int width = 290, gap = 15;
        int index = row.childCount;
        var go = new GameObject("form_" + formId);
        go.transform.SetParent(row, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 84);
        rt.anchoredPosition = new Vector2((index - 1) * (width + gap), 0);
        var img = go.AddComponent<Image>();
        img.color = new Color(GameBootstrap.CSoul.r, GameBootstrap.CSoul.g, GameBootstrap.CSoul.b, 0.3f);
        var btn = go.AddComponent<Button>();
        var txt = UiUtil.AddText(go.transform, label, 22, TextAnchor.MiddleCenter);
        txt.rectTransform.sizeDelta = new Vector2(width, 84);
        string id = formId;
        btn.onClick.AddListener(() => ApplyForm(id));
    }

    private void ApplyForm(string formId) {
        if (Game == null || Game.Sim == null) return;
        string err;
        if (formId == "form_slime") {
            var r = Game.Sim.RevertForm();
            err = r.Ok ? null : r.FailReason;
        } else {
            var r = Game.Sim.MetamorphoseTo(formId);
            err = r.Ok ? null : r.FailReason;
        }
        _lastResult = err == null ? "Форма применена: " + Game.Sim.CurrentFormId
                                  : "Нельзя: " + RuReason(err);
        Rebuild();
    }

    private static string RuReason(string e) {
        switch (e) {
            case "form_node_not_unlocked": return "не открыт узел формы";
            case "structure_too_low": return "мало структуры тела";
            case "components_missing": return "не хватает компонентов";
            case "already_slime": return "уже чистая слизь";
            default: return e;
        }
    }

    public void Rebuild() {
        if (Game == null || Game.Sim == null || ListRoot == null) return;
        var sim = Game.Sim;
        // очистка
        for (int i = ListRoot.childCount - 1; i >= 0; i--)
            Destroy(ListRoot.GetChild(i).gameObject);
        var catalog = sim.GeneCatalogRef;
        for (int i = 0; i < catalog.Length; i++) {
            var node = catalog[i];
            var go = new GameObject("node_" + node.Id);
            go.transform.SetParent(ListRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(860, 68); // 8 узлов × 68 = 544 — входит в список без наложений
            var img = go.AddComponent<Image>();
            string reason = GeneNetworkService.CanUnlock(node, sim.Unlocked, sim.Genome, sim.Stock);
            Color c;
            if (sim.Unlocked.Has(node.Id)) c = GameBootstrap.CFood;
            else if (reason == "prerequisite_missing") c = new Color(0.25f, 0.28f, 0.32f);
            else if (reason == "gene_missing") c = GameBootstrap.CSoul * 0.7f;
            else c = GameBootstrap.CSlime * 0.8f;
            img.color = c;
            var txt = UiUtil.AddText(go.transform, node.Title + "  |  " + DescribeCost(node.Cost),
                28, TextAnchor.MiddleLeft);
            txt.rectTransform.sizeDelta = new Vector2(840, 62);
            var btn = go.AddComponent<Button>();
            string id = node.Id;
            btn.onClick.AddListener(() => {
                string err = Game.Sim.TryUnlockNode(id);
                _lastResult = err == null ? "Узел открыт" : "Нельзя открыть: " + err;
                Rebuild();
            });
        }
        if (StatusText != null)
            StatusText.text = string.Format(
                "Компоненты — вода {0:0.#} · орг {1:0.#} · хитин {2:0.#} · мин {3:0.#}" +
                (string.IsNullOrEmpty(_lastResult) ? "" : " | " + _lastResult),
                sim.Stock.Water, sim.Stock.Organic, sim.Stock.Chitin, sim.Stock.Mineral);
    }

    private static string DescribeCost(ComponentCost c) {
        var sb = new System.Text.StringBuilder();
        if (c.Water > 0) sb.Append("вода ").Append(c.Water.ToString("0.#")).Append(" ");
        if (c.Organic > 0) sb.Append("орг. ").Append(c.Organic.ToString("0.#")).Append(" ");
        if (c.Chitin > 0) sb.Append("хитин ").Append(c.Chitin.ToString("0.#")).Append(" ");
        if (c.Mineral > 0) sb.Append("мин. ").Append(c.Mineral.ToString("0.#"));
        return sb.Length == 0 ? "без затрат" : sb.ToString();
    }
}
/// <summary>Помощник создания uGUI-элементов кодом.</summary>
public static class UiUtil {
    public static Text AddText(Transform parent, string content, int size, TextAnchor anchor) {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = anchor;
        t.color = new Color(0.92f, 0.96f, 1f);
        t.text = content;
        t.rectTransform.sizeDelta = new Vector2(400, 40);
        return t;
    }
}
}
