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

    void Awake() {
        Instance = this;
        if (Panel != null) Panel.SetActive(false);
    }
    void Start() { _game = GameBootstrap.Instance; }
    public void Toggle() {
        if (Panel == null) return;
        bool next = !Panel.activeSelf;
        Panel.SetActive(next);
        if (next) Rebuild();
    }
    public void Close() { if (Panel != null) Panel.SetActive(false); }

    public void Rebuild() {
        if (_game == null || _game.Sim == null || ListRoot == null) return;
        var sim = _game.Sim;
        // очистка
        for (int i = ListRoot.childCount - 1; i >= 0; i--)
            Destroy(ListRoot.GetChild(i).gameObject);
        var catalog = sim.GeneCatalogRef;
        for (int i = 0; i < catalog.Length; i++) {
            var node = catalog[i];
            var go = new GameObject("node_" + node.Id);
            go.transform.SetParent(ListRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(860, 84);
            var img = go.AddComponent<Image>();
            string reason = GeneNetworkService.CanUnlock(node, sim.Unlocked, sim.Genome, sim.Stock);
            Color c;
            if (sim.Unlocked.Has(node.Id)) c = GameBootstrap.CFood;
            else if (reason == "prerequisite_missing") c = new Color(0.25f, 0.28f, 0.32f);
            else if (reason == "gene_missing") c = GameBootstrap.CSoul * 0.7f;
            else c = GameBootstrap.CSlime * 0.8f;
            img.color = c;
            var txt = UiUtil.AddText(go.transform, node.Title + "  |  " + DescribeCost(node.Cost),
                30, TextAnchor.MiddleLeft);
            txt.rectTransform.sizeDelta = new Vector2(840, 80);
            var btn = go.AddComponent<Button>();
            string id = node.Id;
            btn.onClick.AddListener(() => {
                string err = _game.Sim.TryUnlockNode(id);
                if (StatusText != null)
                    StatusText.text = err == null ? "Узел открыт" : "Нельзя открыть: " + err;
                Rebuild();
            });
        }
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
