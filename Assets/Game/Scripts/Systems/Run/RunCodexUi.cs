using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PM.Domain.Run;
using PM.Systems.Bootstrap;
using PM.Systems.Gameplay;
using PM.Systems.UI;

namespace PM.Systems.Run {
/// <summary>
/// Энциклопедия мира (v0.9): карточка «Новая запись» при первой встрече с существом и большой экран
/// с вкладками «Существа / Области / Ресурсы / О мире». Портреты и картины областей — AiArt (Z.ai).
/// Данные и правила открытия — PM.Domain.Run.Codex.
/// </summary>
public sealed class RunCodexUi : MonoBehaviour {
    private static readonly Color Modal = new Color(0.02f, 0.05f, 0.1f, 0.98f);
    private static readonly Color Panel = new Color(0.04f, 0.08f, 0.13f, 0.95f);
    private static readonly Color TextC = new Color(0.92f, 0.96f, 1f);
    private static readonly Color Dim = new Color(0.65f, 0.75f, 0.85f);
    private static readonly Color Accent = new Color(0.55f, 0.9f, 1f);

    private RunGame _game;
    private Transform _root;
    private GameObject _card, _book;
    private RawImage _cardArt;
    private Text _cardName, _cardEpithet, _cardBody;
    private readonly Queue<CodexEntry> _queue = new Queue<CodexEntry>();
    private CodexEntry _cardEntry;
    private bool _pausedByMe;

    private CodexKind _tab = CodexKind.Species;
    private bool _worldTab;
    private string _selected;
    private Transform _grid;
    private RawImage _detArt;
    private Image _detIcon;
    private Text _detName, _detEpithet, _detBody, _counter;
    private readonly Image[] _tabImgs = new Image[4];

    public bool IsOpen => (_card != null && _card.activeSelf) || (_book != null && _book.activeSelf);

    public static RunCodexUi Build(Transform canvas, RunGame game) {
        var go = new GameObject("codex_ui");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var ui = go.AddComponent<RunCodexUi>();
        ui._game = game;
        ui._root = go.transform;
        ui.BuildCard();
        ui.BuildBook();
        return ui;
    }

    // ================= Карточка новой записи =================
    private void BuildCard() {
        _card = FullScreen("codex_card", new Color(0f, 0f, 0f, 0.7f));
        var box = Box(_card.transform, "card", Vector2.zero, new Vector2(1500, 760), Modal, new Vector2(0.5f, 0.5f));
        var frame = Box(box, "art_frame", new Vector2(40, -40), new Vector2(560, 560), Panel, new Vector2(0, 1));
        _cardArt = Raw(frame, new Vector2(540, 540));
        var head = Label(box, "НОВАЯ ЗАПИСЬ В ЭНЦИКЛОПЕДИИ", 24, TextAnchor.MiddleLeft, new Vector2(640, -40), new Vector2(820, 36), new Vector2(0, 1));
        head.color = new Color(1f, 0.85f, 0.4f);
        _cardName = Label(box, "", 58, TextAnchor.MiddleLeft, new Vector2(640, -82), new Vector2(820, 76), new Vector2(0, 1));
        _cardName.color = Accent;
        _cardEpithet = Label(box, "", 28, TextAnchor.MiddleLeft, new Vector2(640, -160), new Vector2(820, 40), new Vector2(0, 1));
        _cardEpithet.color = Dim;
        _cardBody = Label(box, "", 26, TextAnchor.UpperLeft, new Vector2(640, -215), new Vector2(820, 420), new Vector2(0, 1));
        var cont = Btn(box, "Продолжить", new Vector2(-40, 40), new Vector2(380, 100), GameBootstrap.CFood, new Vector2(1, 0), 34);
        cont.onClick.AddListener(NextCard);
        var open = Btn(box, "Энциклопедия", new Vector2(-440, 40), new Vector2(360, 100), GameBootstrap.CSoul, new Vector2(1, 0), 30);
        open.onClick.AddListener(() => { var e = _cardEntry; CloseCard(); Open(e); });
        _card.SetActive(false);
    }

    /// <summary>Показать карточку (или поставить в очередь, если одна уже открыта).</summary>

    public void ShowCard(CodexEntry e) {
        if (e == null) return;
        if (_card.activeSelf || _book.activeSelf) { _queue.Enqueue(e); return; }
        _cardEntry = e;
        SetArt(_cardArt, e.ArtKey, null);
        _cardName.text = e.Name;
        _cardEpithet.text = e.Epithet;
        _cardBody.text = Body(e, true);
        _card.SetActive(true);
        transform.SetAsLastSibling();
        PauseIfRunning();
        ProceduralSfx.Instance?.Play("evolve");
    }

    /// <summary>Кнопка «Назад»: карточка → следующая, книга → закрыть.</summary>
    public bool CloseTop() {
        if (_card.activeSelf) { NextCard(); return true; }
        if (_book.activeSelf) { Close(); return true; }
        return false;
    }

    private void NextCard() {
        CloseCard();
        if (_queue.Count > 0) ShowCard(_queue.Dequeue());
    }

    private void CloseCard() {
        _card.SetActive(false);
        if (!_book.activeSelf) ResumeIfPaused();
    }

    // ================= Книга =================
    private void BuildBook() {
        _book = FullScreen("codex_book", new Color(0f, 0f, 0f, 0.85f));
        var box = Box(_book.transform, "book", Vector2.zero, new Vector2(1860, 1020), Modal, new Vector2(0.5f, 0.5f));
        UiFit.Apply(box); // v0.15: не выше видимой высоты канвы
        Label(box, "ЭНЦИКЛОПЕДИЯ", 44, TextAnchor.MiddleLeft, new Vector2(40, -22), new Vector2(460, 60), new Vector2(0, 1)).color = GameBootstrap.COutline;
        string[] tabs = { "Существа", "Области", "Ресурсы", "О мире" };
        for (int i = 0; i < 4; i++) {
            int idx = i;
            var b = Btn(box, tabs[i], new Vector2(500 + i * 232, -18), new Vector2(220, 74), GameBootstrap.CSlime, new Vector2(0, 1), 28);
            b.onClick.AddListener(() => { _worldTab = idx == 3; if (idx < 3) _tab = (CodexKind)idx; _selected = null; Refresh(); });
            _tabImgs[i] = b.GetComponent<Image>();
        }
        var close = Btn(box, "Закрыть", new Vector2(-22, -18), new Vector2(170, 74), GameBootstrap.CSlime, new Vector2(1, 1), 28);
        close.onClick.AddListener(Close);
        _counter = Label(box, "", 24, TextAnchor.MiddleLeft, new Vector2(40, -96), new Vector2(700, 36), new Vector2(0, 1));
        _counter.color = Dim;
        var grid = new GameObject("grid");
        grid.transform.SetParent(box, false);
        var grt = grid.AddComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0, 1);
        grt.anchoredPosition = new Vector2(40, -140);
        grt.sizeDelta = new Vector2(900, 850);
        _grid = grid.transform;
        var det = Box(box, "detail", new Vector2(-30, -120), new Vector2(860, 870), Panel, new Vector2(1, 1));
        _detArt = Raw(det, new Vector2(380, 380));
        var art = _detArt.rectTransform;
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
        art.anchoredPosition = new Vector2(24, -24);
        var icon = new GameObject("icon");
        icon.transform.SetParent(det, false);
        _detIcon = icon.AddComponent<Image>();
        _detIcon.raycastTarget = false;
        var irt = _detIcon.rectTransform;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0, 1);
        irt.anchoredPosition = new Vector2(84, -84); irt.sizeDelta = new Vector2(260, 260);
        _detName = Label(det, "", 44, TextAnchor.UpperLeft, new Vector2(430, -30), new Vector2(410, 120), new Vector2(0, 1));
        _detName.color = Accent;
        _detEpithet = Label(det, "", 26, TextAnchor.UpperLeft, new Vector2(430, -160), new Vector2(410, 220), new Vector2(0, 1));
        _detEpithet.color = Dim;
        _detBody = Label(det, "", 25, TextAnchor.UpperLeft, new Vector2(24, -425), new Vector2(810, 430), new Vector2(0, 1));
        _book.SetActive(false);
    }

    /// <summary>Открыть книгу (на записи e, если задана).</summary>
    public void Open(CodexEntry e = null) {
        if (e != null) { _tab = e.Kind; _worldTab = false; _selected = e.Key; }
        _book.SetActive(true);
        transform.SetAsLastSibling();
        PauseIfRunning();
        Refresh();
    }

    private void Close() {
        _book.SetActive(false);
        ResumeIfPaused();
        if (_queue.Count > 0) ShowCard(_queue.Dequeue());
    }

    private void Refresh() {
        for (int i = 0; i < 4; i++)
            _tabImgs[i].color = (_worldTab ? i == 3 : i == (int)_tab) ? new Color(0.35f, 0.75f, 1f, 0.95f) : new Color(0.12f, 0.18f, 0.24f, 0.95f);
        for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
        var meta = _game.Meta;
        if (_worldTab) {
            _counter.text = "Что известно о пещере";
            ShowText("Память Материи", "Мир", Codex.World + "\n\n<b>Капля</b>\n" + Codex.Hero, "title");
            return;
        }
        var list = Codex.All(_tab);
        int open = 0;
        foreach (var e in list) if (Codex.IsUnlocked(e, meta)) open++;
        _counter.text = "Открыто: " + open + " из " + list.Count;
        int cols = _tab == CodexKind.Species ? 5 : 3;
        float size = _tab == CodexKind.Species ? 168f : 280f, gap = 12f;
        for (int i = 0; i < list.Count; i++) {
            var e = list[i];
            bool unlocked = Codex.IsUnlocked(e, meta);
            if (_selected == null && unlocked) _selected = e.Key;
            var pos = new Vector2((i % cols) * (size + gap), -(i / cols) * (size + gap + 34f));
            var tile = Box(_grid, "tile_" + e.Key, pos, new Vector2(size, size), e.Key == _selected ? new Color(0.25f, 0.5f, 0.65f, 1f) : Panel, new Vector2(0, 1));
            var b = tile.gameObject.AddComponent<Button>();
            b.targetGraphic = tile.GetComponent<Image>();
            string key = e.Key;
            b.onClick.AddListener(() => { _selected = key; ProceduralSfx.Instance?.Play("click"); Refresh(); });
            var r = Raw(tile, new Vector2(size - 12, size - 12));
            if (e.Kind == CodexKind.Resource) {
                r.gameObject.SetActive(false);
                var ic = new GameObject("icon"); ic.transform.SetParent(tile, false);
                var img = ic.AddComponent<Image>(); img.raycastTarget = false;
                img.sprite = ThirdPartyArt.Icon(RunEvolutionUi.ResourceIcon((ResourceKind)System.Enum.Parse(typeof(ResourceKind), e.Id)));
                img.color = unlocked ? RunEvolutionUi.Hex(ResourceInfo.Color((ResourceKind)System.Enum.Parse(typeof(ResourceKind), e.Id))) : new Color(0.2f, 0.22f, 0.26f);
                img.rectTransform.sizeDelta = new Vector2(size * 0.55f, size * 0.55f);
            } else {
                SetArt(r, e.ArtKey, null);
                r.color = unlocked ? Color.white : new Color(0.04f, 0.05f, 0.07f, 1f);   // неизвестное — силуэт
            }
            var name = Label(_grid, unlocked ? e.Name : "???", 22, TextAnchor.UpperCenter, pos + new Vector2(0, -size - 2), new Vector2(size, 32), new Vector2(0, 1));
            name.color = unlocked ? TextC : Dim;
        }
        CodexEntry sel = null;
        foreach (var e in list) if (e.Key == _selected) sel = e;
        if (sel == null) { ShowText("", "", "Встречайте новых существ и исследуйте пещеру — записи откроются сами.", null); return; }
        if (!Codex.IsUnlocked(sel, meta)) { ShowText("???", "Запись не открыта", Hint(sel), sel.Kind == CodexKind.Resource ? null : sel.ArtKey, true); return; }
        if (sel.Kind == CodexKind.Resource) {
            var k = (ResourceKind)System.Enum.Parse(typeof(ResourceKind), sel.Id);
            ShowText(sel.Name, sel.Epithet, Body(sel, false), null);
            _detIcon.gameObject.SetActive(true);
            _detIcon.sprite = ThirdPartyArt.Icon(RunEvolutionUi.ResourceIcon(k));
            _detIcon.color = RunEvolutionUi.Hex(ResourceInfo.Color(k));
            return;
        }
        ShowText(sel.Name, sel.Epithet, Body(sel, false), sel.ArtKey);
    }

    private static string Hint(CodexEntry e) {
        switch (e.Kind) {
            case CodexKind.Species: return "Поглотите это существо, чтобы узнать о нём.";
            case CodexKind.Biome: return "Доберитесь до этой области пещеры.";
            default: return "Добудьте этот ресурс.";
        }
    }

    private void ShowText(string name, string epithet, string body, string art, bool silhouette = false) {
        _detName.text = name;
        _detEpithet.text = epithet;
        _detBody.text = body;
        _detIcon.gameObject.SetActive(false);
        SetArt(_detArt, art, null);
        _detArt.color = silhouette ? new Color(0.04f, 0.05f, 0.07f, 1f) : Color.white;
    }

    /// <summary>Текст записи: художественный абзац, факты из каталогов, совет.</summary>
    private static string Body(CodexEntry e, bool compact) {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(e.Lore)) sb.Append("<i>").Append(e.Lore).Append("</i>\n\n");
        if (e.Facts != null) foreach (var f in e.Facts) sb.Append("• ").Append(f).Append('\n');
        if (!string.IsNullOrEmpty(e.Tip)) sb.Append(compact ? "\n" : "\n<b>Как справиться</b>\n").Append(compact ? "Совет: " : "").Append(e.Tip);
        return sb.ToString();
    }

    private void PauseIfRunning() {
        if (_pausedByMe || _game == null || !_game.InRun || Time.timeScale <= 0f) return;
        _pausedByMe = true;
        _game.SetPaused(true);
    }

    private void ResumeIfPaused() {
        if (!_pausedByMe) return;
        _pausedByMe = false;
        _game.SetPaused(false);
    }

    // ================= Строители =================
    private static void SetArt(RawImage r, string key, Texture fallback) {
        var tex = string.IsNullOrEmpty(key) ? null : Resources.Load<Texture2D>("AiArt/" + key);
        r.texture = tex != null ? tex : fallback;
        r.enabled = r.texture != null;
        if (tex != null) {
            // вписать с сохранением пропорций (картины областей — широкие)
            var rt = r.rectTransform;
            float box = Mathf.Max(rt.sizeDelta.x, rt.sizeDelta.y);
            float aspect = (float)tex.width / tex.height;
            rt.sizeDelta = aspect >= 1f ? new Vector2(box, box / aspect) : new Vector2(box * aspect, box);
        }
    }

    private GameObject FullScreen(string name, Color c) {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-200, -200); rt.offsetMax = new Vector2(200, 200);
        go.AddComponent<Image>().color = c;
        return go;
    }

    private static RawImage Raw(Transform parent, Vector2 size) {
        var go = new GameObject("art");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RawImage>();
        r.raycastTarget = false;
        var rt = r.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return r;
    }

    private static RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size, Color c, Vector2 anchor) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        UiSkin.Box(img, c, size);
        return rt;
    }

    private static Button Btn(Transform parent, string label, Vector2 pos, Vector2 size, Color c, Vector2 anchor, int font) {
        var rt = Box(parent, "btn", pos, size, new Color(c.r, c.g, c.b, 0.9f), anchor);
        UiSkin.AsPill(rt.GetComponent<Image>(), new Color(c.r, c.g, c.b, 0.95f));
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = rt.GetComponent<Image>();
        b.onClick.AddListener(() => ProceduralSfx.Instance?.Play("click"));
        var t = UiUtil.AddText(rt, label, font, TextAnchor.MiddleCenter, TextC);
        t.rectTransform.sizeDelta = size - new Vector2(16, 8);
        t.raycastTarget = false;
        return b;
    }

    private static Text Label(Transform parent, string s, int size, TextAnchor a, Vector2 pos, Vector2 box, Vector2 anchor) {
        var t = UiUtil.AddText(parent, s, size, a, TextC);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }
}
}
