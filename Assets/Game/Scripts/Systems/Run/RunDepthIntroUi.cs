using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PM.Domain.Run;
using PM.Systems.Bootstrap;
using PM.Systems.Gameplay;
using PM.Systems.UI;

namespace PM.Systems.Run {
/// <summary>
/// v0.16: окно знакомства с новой глубиной. Слева — картина области (медленный наезд камеры),
/// шкала глубин и эпиграф; справа одна за другой проявляются особенности уровня, обитатели,
/// страж и совет. Игра на паузе, пока игрок не нажмёт «Погрузиться». Данные — PM.Domain.Run.DepthIntro.
/// </summary>
public sealed class RunDepthIntroUi : MonoBehaviour {
    private const float BoxW = 1720f, BoxH = 800f, ArtW = 900f, ArtH = 514f, ColX = 960f, ColW = 730f;
    private static readonly Color Modal = new Color(0.02f, 0.04f, 0.08f, 0.98f);
    private static readonly Color TextC = new Color(0.92f, 0.96f, 1f);
    private static readonly Color Dim = new Color(0.66f, 0.74f, 0.86f);

    private RunGame _game;
    private GameObject _root;
    private RectTransform _box, _artImg;
    private CanvasGroup _group;
    private Outline _frame;
    private RawImage _art;
    private Text _kicker, _name, _sub, _epigraph, _head, _guardName, _guardLine, _tip, _newTag, _inhabLabel;
    private Image _newBadge, _tipBox, _goImg;
    private Button _go;
    private RawImage _guardArt;
    private readonly Image[] _pips = new Image[RunState.MaxDepth];
    private readonly Image[] _featIcons = new Image[3];
    private readonly Image[] _featBacks = new Image[3];
    private readonly Text[] _featTitles = new Text[3], _featTexts = new Text[3];
    private Transform _inhabRow;
    private readonly List<CanvasGroup> _reveal = new List<CanvasGroup>();
    private float _t;
    private Color _accent;
    private int _depth;
    private System.Action _onClose;

    public bool IsOpen => _root != null && _root.activeSelf;

    public static RunDepthIntroUi Build(Transform canvas, RunGame game) {
        var go = new GameObject("depth_intro_ui");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var ui = go.AddComponent<RunDepthIntroUi>();
        ui._game = game;
        ui.BuildWindow(go.transform);
        return ui;
    }

    private void BuildWindow(Transform parent) {
        _root = new GameObject("depth_intro");
        _root.transform.SetParent(parent, false);
        var rrt = _root.AddComponent<RectTransform>();
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
        rrt.offsetMin = new Vector2(-200, -200); rrt.offsetMax = new Vector2(200, 200);
        _root.AddComponent<Image>().color = new Color(0f, 0f, 0.01f, 0.9f);

        _box = Box(_root.transform, "intro_box", Vector2.zero, new Vector2(BoxW, BoxH), Modal, new Vector2(0.5f, 0.5f));
        UiFit.Apply(_box);
        _group = _box.gameObject.AddComponent<CanvasGroup>();
        _frame = _box.gameObject.AddComponent<Outline>();
        _frame.effectDistance = new Vector2(3, -3);

        // ---- картина области: маска + медленный наезд ----
        var artFrame = Box(_box, "art_frame", new Vector2(30, -30), new Vector2(ArtW, ArtH), new Color(0f, 0f, 0f, 1f), new Vector2(0, 1));
        artFrame.gameObject.AddComponent<RectMask2D>();
        _art = new GameObject("art").AddComponent<RawImage>();
        _art.transform.SetParent(artFrame, false);
        _art.raycastTarget = false;
        _artImg = _art.rectTransform;
        _artImg.anchorMin = _artImg.anchorMax = _artImg.pivot = new Vector2(0.5f, 0.5f);
        _artImg.sizeDelta = new Vector2(ArtW, ArtH);
        // нижняя полоса на картине: «ГЛУБИНА 2 из 5» и шкала глубин
        var band = Plain(artFrame, "band", new Vector2(0, 0), new Vector2(ArtW, 74), new Color(0f, 0f, 0.02f, 0.72f), new Vector2(0, 0));
        _kicker = Label(band, "", 28, TextAnchor.MiddleLeft, new Vector2(24, 0), new Vector2(500, 74), new Vector2(0, 0.5f));
        for (int i = 0; i < _pips.Length; i++) {
            _pips[i] = Plain(band, "pip" + i, new Vector2(-24 - (_pips.Length - 1 - i) * 46, 0), new Vector2(30, 30), Color.white, new Vector2(1, 0.5f)).GetComponent<Image>();
            _pips[i].sprite = SpriteUtil.UiRoundedSprite; _pips[i].type = Image.Type.Sliced;
        }
        // лента «новая область» в углу картины
        _newBadge = Plain(artFrame, "new", new Vector2(18, -18), new Vector2(250, 48), new Color(1f, 0.8f, 0.35f, 0.95f), new Vector2(0, 1)).GetComponent<Image>();
        _newBadge.sprite = SpriteUtil.UiRoundedSprite; _newBadge.type = Image.Type.Sliced;
        _newTag = Label(_newBadge.transform, "НОВАЯ ОБЛАСТЬ", 24, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(250, 48), new Vector2(0.5f, 0.5f));
        _newTag.color = new Color(0.18f, 0.1f, 0.02f);

        _name = Label(_box, "", 62, TextAnchor.MiddleLeft, new Vector2(30, -ArtH - 44), new Vector2(ArtW, 80), new Vector2(0, 1));
        _sub = Label(_box, "", 30, TextAnchor.MiddleLeft, new Vector2(30, -ArtH - 118), new Vector2(ArtW, 44), new Vector2(0, 1));
        _sub.fontStyle = FontStyle.Italic;
        _epigraph = Label(_box, "", 25, TextAnchor.UpperLeft, new Vector2(30, -ArtH - 170), new Vector2(ArtW, 90), new Vector2(0, 1));
        _epigraph.color = Dim;
        Fit(_epigraph, 18);

        // ---- правая колонка: что ждёт внизу ----
        _head = Label(_box, "ЧТО ЖДЁТ ВНИЗУ", 26, TextAnchor.MiddleLeft, new Vector2(ColX, -34), new Vector2(ColW, 40), new Vector2(0, 1));
        for (int i = 0; i < 3; i++) {
            var row = Group(_box, "feature" + i, new Vector2(ColX, -82 - i * 100), new Vector2(ColW, 92));
            _featBacks[i] = Plain(row, "icon_back", new Vector2(0, 0), new Vector2(78, 78), Color.white, new Vector2(0, 1)).GetComponent<Image>();
            _featBacks[i].sprite = SpriteUtil.UiRoundedSprite; _featBacks[i].type = Image.Type.Sliced;
            _featIcons[i] = Plain(_featBacks[i].transform, "icon", Vector2.zero, new Vector2(54, 54), Color.white, new Vector2(0.5f, 0.5f)).GetComponent<Image>();
            _featIcons[i].preserveAspect = true;
            _featTitles[i] = Label(row, "", 28, TextAnchor.UpperLeft, new Vector2(96, 0), new Vector2(ColW - 96, 36), new Vector2(0, 1));
            _featTitles[i].fontStyle = FontStyle.Bold;
            _featTexts[i] = Label(row, "", 22, TextAnchor.UpperLeft, new Vector2(96, -36), new Vector2(ColW - 96, 56), new Vector2(0, 1));
            _featTexts[i].color = Dim;
            Fit(_featTexts[i], 16);
        }
        var inhab = Group(_box, "inhabitants", new Vector2(ColX, -384), new Vector2(ColW, 104));
        _inhabLabel = Label(inhab, "Обитатели", 22, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(ColW, 30), new Vector2(0, 1));
        _inhabRow = new GameObject("row").AddComponent<RectTransform>();
        _inhabRow.SetParent(inhab, false);
        var irt = (RectTransform)_inhabRow;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0, 1);
        irt.anchoredPosition = new Vector2(0, -36); irt.sizeDelta = new Vector2(ColW, 66);

        var guard = Group(_box, "guardian", new Vector2(ColX, -498), new Vector2(ColW, 90));
        var gFrame = Plain(guard, "portrait", Vector2.zero, new Vector2(88, 88), Color.white, new Vector2(0, 1));
        var gImg = gFrame.GetComponent<Image>(); gImg.sprite = SpriteUtil.UiRoundedSprite; gImg.type = Image.Type.Sliced;
        gFrame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        _guardArt = new GameObject("art").AddComponent<RawImage>();
        _guardArt.transform.SetParent(gFrame, false);
        _guardArt.raycastTarget = false;
        var gart = _guardArt.rectTransform; gart.anchorMin = gart.anchorMax = gart.pivot = new Vector2(0.5f, 0.5f); gart.sizeDelta = new Vector2(84, 84);
        _guardName = Label(guard, "", 28, TextAnchor.UpperLeft, new Vector2(106, -4), new Vector2(ColW - 106, 38), new Vector2(0, 1));
        _guardLine = Label(guard, "", 22, TextAnchor.UpperLeft, new Vector2(106, -42), new Vector2(ColW - 106, 48), new Vector2(0, 1));
        _guardLine.color = Dim;
        Fit(_guardLine, 16);

        var tip = Group(_box, "tip", new Vector2(ColX, -604), new Vector2(ColW, 74));
        _tipBox = Plain(tip, "back", Vector2.zero, new Vector2(ColW, 74), Color.white, new Vector2(0, 1)).GetComponent<Image>();
        _tipBox.sprite = SpriteUtil.UiRoundedSprite; _tipBox.type = Image.Type.Sliced;
        _tip = Label(tip, "", 23, TextAnchor.MiddleLeft, new Vector2(20, 0), new Vector2(ColW - 40, 74), new Vector2(0, 1));
        _tip.supportRichText = true;
        Fit(_tip, 16);

        var goGroup = Group(_box, "go", new Vector2(ColX, -694), new Vector2(ColW, 84));
        var goRt = Box(goGroup, "go_btn", Vector2.zero, new Vector2(ColW, 84), GameBootstrap.CFood, new Vector2(0, 1));
        _goImg = goRt.GetComponent<Image>();
        _goImg.raycastTarget = true;
        UiSkin.AsPill(_goImg, new Color(GameBootstrap.CFood.r, GameBootstrap.CFood.g, GameBootstrap.CFood.b, 0.95f));
        _go = goRt.gameObject.AddComponent<Button>();
        _go.targetGraphic = _goImg;
        _go.onClick.AddListener(Close);
        var goText = UiUtil.AddText(goRt, "Погрузиться", 40, TextAnchor.MiddleCenter, TextC);
        goText.rectTransform.sizeDelta = new Vector2(ColW - 20, 80);
        goText.raycastTarget = false;
        _root.SetActive(false);
    }

    /// <summary>Показать знакомство с глубиной. firstVisit — лента «новая область». onClose — после «Погрузиться».</summary>
    public void Show(DepthIntroInfo info, Color accent, bool firstVisit, SoulMeta meta, System.Action onClose) {
        _onClose = onClose;
        _depth = info.Depth;
        _accent = accent;
        var b = info.Biome;
        _art.texture = UiFit.LoadArt("biome_" + b.Id.ToString().ToLowerInvariant());
        _art.enabled = _art.texture != null;
        if (_art.texture != null) {   // заполнить рамку, сохранив пропорции картины
            float aspect = (float)_art.texture.width / _art.texture.height;
            _artImg.sizeDelta = aspect >= ArtW / ArtH ? new Vector2(ArtH * aspect, ArtH) : new Vector2(ArtW, ArtW / aspect);
        }
        _frame.effectColor = new Color(accent.r, accent.g, accent.b, 0.85f);
        _kicker.text = "ГЛУБИНА " + info.Depth + " из " + RunState.MaxDepth;
        _kicker.color = accent;
        for (int i = 0; i < _pips.Length; i++)
            _pips[i].color = i < info.Depth ? new Color(accent.r, accent.g, accent.b, i == info.Depth - 1 ? 1f : 0.55f) : new Color(1f, 1f, 1f, 0.16f);
        _newBadge.gameObject.SetActive(firstVisit);
        _name.text = b.Name;
        _name.color = accent;
        _sub.text = b.Subtitle;
        _sub.color = TextC;
        _epigraph.text = "<i>«" + info.Epigraph + "»</i>";
        _epigraph.supportRichText = true;
        _head.color = accent;
        for (int i = 0; i < 3; i++) {
            var f = info.Features[i];
            _featBacks[i].color = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.95f);
            _featIcons[i].sprite = ThirdPartyArt.Icon(f.Icon);
            _featIcons[i].enabled = _featIcons[i].sprite != null;
            _featIcons[i].color = Color.Lerp(accent, Color.white, 0.35f);
            _featTitles[i].text = f.Title;
            _featTitles[i].color = TextC;
            _featTexts[i].text = f.Text;
        }
        BuildInhabitants(info, meta);
        var g = info.Guardian;
        bool beaten = meta != null && Codex.IsUnlocked(Codex.ForGuardian(g), meta);
        _guardArt.texture = UiFit.LoadArt(g.Portrait);
        _guardArt.enabled = _guardArt.texture != null;
        _guardArt.color = beaten ? Color.white : new Color(0.55f, 0.5f, 0.55f, 1f);   // ещё не побеждён — в полутени
        _guardName.text = "Страж: " + g.Name;
        _guardName.color = new Color(1f, 0.82f, 0.45f);
        _guardLine.text = beaten
            ? "Ты уже побеждал его. Он помнит — и стал злее."
            : "Дремлет у шахты вниз. Проснётся, когда ты наберёшь силу этой глубины.";
        _tipBox.color = new Color(accent.r * 0.22f, accent.g * 0.22f, accent.b * 0.22f, 0.95f);
        _tip.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(accent, Color.white, 0.3f)) + "><b>Совет:</b></color> " + info.Tip;
        _tip.color = TextC;

        _reveal.Clear();
        foreach (Transform c in _box) {
            var cg = c.GetComponent<CanvasGroup>();
            if (cg != null) { _reveal.Add(cg); cg.alpha = 0f; }
        }
        _t = 0f;
        _group.alpha = 0f;
        _box.localScale = Vector3.one * BaseScale * 0.94f;
        _go.interactable = false;
        _root.SetActive(true);
        transform.SetAsLastSibling();
        _game.SetPaused(true);
        ProceduralSfx.Instance?.Play("evolve");
    }

    private float _baseScale = -1f;
    private float BaseScale { get { if (_baseScale < 0f) _baseScale = _box.localScale.x; return _baseScale; } }

    private void BuildInhabitants(DepthIntroInfo info, SoulMeta meta) {
        for (int i = _inhabRow.childCount - 1; i >= 0; i--) Destroy(_inhabRow.GetChild(i).gameObject);
        int known = 0;
        int n = Mathf.Min(info.Inhabitants.Length, 10);
        float size = 64f, gap = Mathf.Min(10f, (ColW - n * size) / Mathf.Max(1, n - 1));
        for (int i = 0; i < n; i++) {
            string id = info.Inhabitants[i];
            bool seen = Codex.IsUnlocked(Codex.ForSpecies(id), meta);   // как в энциклопедии
            if (seen) known++;
            var cell = Plain(_inhabRow, "sp_" + id, new Vector2(i * (size + gap), 0), new Vector2(size, size),
                seen ? new Color(_accent.r * 0.5f, _accent.g * 0.5f, _accent.b * 0.5f, 1f) : new Color(0.12f, 0.14f, 0.18f, 1f), new Vector2(0, 1));
            var img = cell.GetComponent<Image>(); img.sprite = SpriteUtil.UiRoundedSprite; img.type = Image.Type.Sliced;
            cell.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var r = new GameObject("art").AddComponent<RawImage>();
            r.transform.SetParent(cell, false);
            r.raycastTarget = false;
            r.rectTransform.sizeDelta = new Vector2(size - 6, size - 6);
            r.texture = UiFit.LoadArt(Codex.ForSpecies(id).ArtKey);
            r.enabled = r.texture != null;
            r.color = seen ? Color.white : new Color(0.06f, 0.07f, 0.1f, 1f);       // незнакомые — силуэты
            if (!seen) {
                var q = Label(cell, "?", 30, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(size, size), new Vector2(0.5f, 0.5f));
                q.color = new Color(_accent.r, _accent.g, _accent.b, 0.8f);
            }
        }
        _inhabLabel.text = "Обитатели · знакомы " + known + " из " + n;
        _inhabLabel.color = TextC;
    }

    void Update() {
        if (!IsOpen) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        _t += dt;
        // окно: проявление и лёгкое «раскрытие»
        float k = Ease(Mathf.Clamp01(_t / 0.35f));
        _group.alpha = k;
        _box.localScale = Vector3.one * BaseScale * (0.94f + 0.06f * k);
        // картина: медленный наезд и дрейф (как в документальном кино)
        float z = 1.04f + 0.08f * (0.5f - 0.5f * Mathf.Cos(_t * 0.25f));
        _artImg.localScale = new Vector3(z, z, 1f);
        _artImg.anchoredPosition = new Vector2(Mathf.Sin(_t * 0.17f) * 18f, Mathf.Sin(_t * 0.11f) * 10f);
        // текущая глубина на шкале — пульсирует
        var p = _pips[Mathf.Clamp(_depth - 1, 0, _pips.Length - 1)];
        float s = 1f + 0.18f * (0.5f + 0.5f * Mathf.Sin(_t * 4f));
        p.rectTransform.localScale = new Vector3(s, s, 1f);
        // блоки справа — по очереди, со сдвигом слева направо
        for (int i = 0; i < _reveal.Count; i++) {
            float a = Ease(Mathf.Clamp01((_t - 0.3f - i * 0.14f) / 0.35f));
            _reveal[i].alpha = a;
            var rt = (RectTransform)_reveal[i].transform;
            rt.anchoredPosition = new Vector2(ColX + 40f * (1f - a), rt.anchoredPosition.y);
        }
        // кнопка: не ловит случайный тап в момент появления, потом мягко «дышит»
        if (!_go.interactable && _t > 0.3f + _reveal.Count * 0.14f) _go.interactable = true;
        if (_go.interactable) {
            float b = 0.8f + 0.2f * (0.5f + 0.5f * Mathf.Sin(_t * 3f));
            var c = GameBootstrap.CFood;
            _goImg.color = new Color(c.r * b + 0.1f * (1 - b), c.g * b, c.b * b, 0.95f);
        }
    }

    /// <summary>«Назад» / «Погрузиться».</summary>
    public bool CloseIfOpen() {
        if (!IsOpen) return false;
        Close();
        return true;
    }

    private void Close() {
        if (!IsOpen) return;
        _root.SetActive(false);
        _game.SetPaused(false);
        ProceduralSfx.Instance?.Play("click");
        var cb = _onClose; _onClose = null;
        cb?.Invoke();
    }

    private static float Ease(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

    // ================= Строители =================
    private static RectTransform Group(Transform parent, string name, Vector2 pos, Vector2 size) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        return rt;
    }

    private static RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size, Color c, Vector2 anchor) {
        var rt = Plain(parent, name, pos, size, c, anchor);
        UiSkin.Box(rt.GetComponent<Image>(), c, size);
        return rt;
    }

    private static RectTransform Plain(Transform parent, string name, Vector2 pos, Vector2 size, Color c, Vector2 anchor) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return rt;
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

    private static void Fit(Text t, int min) {
        t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = t.fontSize;
    }
}
}
