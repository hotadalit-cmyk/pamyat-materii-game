using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PM.Domain.Run;
using PM.Systems.Bootstrap;
using PM.Systems.Gameplay;
using PM.Systems.UI;

namespace PM.Systems.Run {
/// <summary>
/// Эволюция и показатели (v0.8): полоса хранилища в HUD, экран дерева (5 ветвей, тиры, развилки, цены,
/// причины блокировки, жетоны) и интерактивные показатели (касание строки — разбор источников).
/// Правила — в Domain.Run (EvolutionTree/EvolutionState/ResourceStore/StatSheet).
/// </summary>
public sealed class RunEvolutionUi : MonoBehaviour {
    private static readonly Color Panel = new Color(0.016f, 0.05f, 0.09f, 0.7f);
    private static readonly Color Modal = new Color(0.02f, 0.05f, 0.1f, 0.98f);
    private static readonly Color TextC = new Color(0.92f, 0.96f, 1f);

    private RunGame _game;
    private Transform _root;
    // HUD
    private readonly Text[] _resText = new Text[ResourceInfo.Count];
    private Text _evoBadge;
    private Image _evoBtnImg;
    // экран эволюции
    private GameObject _evoModal;
    private EvoBranch _branch = EvoBranch.Body;
    private readonly Image[] _tabImgs = new Image[5];
    private readonly Text[] _tabTexts = new Text[5];
    private Transform _nodeLayer;
    private Text _storeText, _tokenText;
    private Text _detTitle, _detBody, _detCost, _detBlock;
    private Button _detBuy, _detFree;
    private Text _detBuyLabel;
    private string _selected;
    // показатели
    private GameObject _statsModal;
    private Transform _statsList;
    private string _expanded;
    private float _hudTimer;

    public bool ModalOpen => (_evoModal != null && _evoModal.activeSelf) || (_statsModal != null && _statsModal.activeSelf);

    public static RunEvolutionUi Build(Transform canvas, RunGame game) {
        var go = new GameObject("evolution_ui");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var ui = go.AddComponent<RunEvolutionUi>();
        ui._game = game;
        ui._root = go.transform;
        ui.BuildHud();
        ui.BuildEvolution();
        ui.BuildStats();
        return ui;
    }

    // ================= HUD: хранилище + кнопки =================
    private void BuildHud() {
        var bar = Box(_root, "resources", new Vector2(28, -232), new Vector2(560, 116), Panel, new Vector2(0, 1));
        for (int i = 0; i < ResourceInfo.Count; i++) {
            var k = (ResourceKind)i;
            int col = i % 3, row = i / 3;
            var dot = Box(bar, "dot" + i, new Vector2(18 + col * 180, -12 - row * 48), new Vector2(36, 36), Hex(ResourceInfo.Color(k)), new Vector2(0, 1));
            var ic = ThirdPartyArt.Icon(ResourceIcon(k));
            dot.GetComponent<Image>().sprite = ic != null ? ic : SpriteUtil.UiCircleSprite;
            dot.GetComponent<Image>().type = Image.Type.Simple;
            _resText[i] = Label(bar, "0", 26, TextAnchor.MiddleLeft, new Vector2(56 + col * 180, -12 - row * 48), new Vector2(124, 38), new Vector2(0, 1));
        }
        // нажатие на хранилище — открыть эволюцию
        var barBtn = bar.gameObject.AddComponent<Button>();
        barBtn.targetGraphic = bar.GetComponent<Image>();
        barBtn.onClick.AddListener(OpenEvolution);

        var evo = MakeButton(_root, "evo_btn", new Vector2(28, -362), new Vector2(270, 96), GameBootstrap.CSoul, 0.85f, "Эволюция", 30, new Vector2(0, 1));
        evo.GetComponent<Button>().onClick.AddListener(OpenEvolution);
        _evoBtnImg = evo.GetComponent<Image>();
        _evoBadge = Label(evo.transform, "", 24, TextAnchor.MiddleCenter, new Vector2(-6, -6), new Vector2(60, 40), new Vector2(1, 1));
        _evoBadge.color = new Color(1f, 0.9f, 0.4f);
        var stats = MakeButton(_root, "stats_btn", new Vector2(318, -362), new Vector2(270, 96), GameBootstrap.CSlime, 0.75f, "Показатели", 30, new Vector2(0, 1));
        stats.GetComponent<Button>().onClick.AddListener(OpenStats);
    }

    void Update() {
        var run = _game != null ? _game.Run : null;
        if (run == null || run.Resources == null) return;
        _hudTimer -= Time.unscaledDeltaTime;
        if (_hudTimer > 0f) return;
        _hudTimer = 0.2f;
        for (int i = 0; i < ResourceInfo.Count; i++) {
            double v = run.Resources.Get((ResourceKind)i);
            _resText[i].text = v >= run.Resources.Capacity - 0.01 ? Mathf.FloorToInt((float)v) + " макс" : Mathf.FloorToInt((float)v).ToString();
        }
        int avail = CountAvailable(run);
        _evoBadge.text = avail > 0 ? avail.ToString() : (run.Evolution.FreeTokens > 0 ? "!" : "");
        float pulse = avail > 0 || run.Evolution.FreeTokens > 0 ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f) : 0.55f;
        _evoBtnImg.color = new Color(GameBootstrap.CSoul.r, GameBootstrap.CSoul.g, GameBootstrap.CSoul.b, pulse);
        if (_evoModal.activeSelf) RefreshStore();
    }

    private static int CountAvailable(RunState run) {
        int n = 0;
        for (int b = 0; b < 5; b++) n += CountAvailable(run, (EvoBranch)b);
        return n;
    }

    /// <summary>Узлы ветви, доступные сейчас: за ресурсы или жетоном.</summary>
    private static int CountAvailable(RunState run, EvoBranch branch) {
        int n = 0;
        var nodes = EvolutionTree.Branch(branch);
        for (int i = 0; i < nodes.Length; i++) if (Availability(run, nodes[i]) != 0) n++;
        return n;
    }

    /// <summary>0 — нельзя; 1 — можно за ресурсы; 2 — только жетоном.</summary>
    private static int Availability(RunState run, EvoNode n) {
        if (run.Evolution.CanUnlock(n.Id, run.Resources, run.KnownSpecies) == UnlockBlock.None) return 1;
        if (run.Evolution.FreeTokens > 0 && run.Evolution.CanUnlockFree(n.Id, run.KnownSpecies) == UnlockBlock.None) return 2;
        return 0;
    }

    // ================= Экран эволюции =================
    private void BuildEvolution() {
        _evoModal = FullScreen("evolution_modal", new Color(0f, 0f, 0f, 0.85f));
        var box = Box(_evoModal.transform, "evo_box", Vector2.zero, new Vector2(1860, 1020), Modal, new Vector2(0.5f, 0.5f));
        Label(box, "ЭВОЛЮЦИЯ", 46, TextAnchor.MiddleLeft, new Vector2(40, -20), new Vector2(500, 60), new Vector2(0, 1)).color = GameBootstrap.COutline;
        // вкладки ветвей
        for (int b = 0; b < 5; b++) {
            var br = (EvoBranch)b;
            var tab = MakeButton(box, "tab" + b, new Vector2(470 + b * 240, -18), new Vector2(226, 74), BranchColor(br), 0.35f,
                EvolutionTree.BranchName(br), 28, new Vector2(0, 1));
            tab.GetComponent<Button>().onClick.AddListener(() => { _branch = br; _selected = null; RefreshEvolution(); });
            _tabImgs[b] = tab.GetComponent<Image>();
            _tabTexts[b] = tab.GetComponentInChildren<Text>();
        }
        var close = MakeButton(box, "close", new Vector2(-22, -18), new Vector2(170, 74), GameBootstrap.CSlime, 0.6f, "Закрыть", 28, new Vector2(1, 1));
        close.GetComponent<Button>().onClick.AddListener(CloseEvolution);
        // хранилище слева
        var store = Box(box, "store", new Vector2(30, -120), new Vector2(380, 640), Panel, new Vector2(0, 1));
        Label(store, "Хранилище", 32, TextAnchor.MiddleLeft, new Vector2(24, -14), new Vector2(330, 46), new Vector2(0, 1)).color = GameBootstrap.COutline;
        _storeText = Label(store, "", 26, TextAnchor.UpperLeft, new Vector2(24, -70), new Vector2(340, 420), new Vector2(0, 1));
        _tokenText = Label(store, "", 26, TextAnchor.UpperLeft, new Vector2(24, -500), new Vector2(340, 120), new Vector2(0, 1));
        _tokenText.color = new Color(1f, 0.9f, 0.4f);
        // поле узлов
        var field = new GameObject("nodes");
        field.transform.SetParent(box, false);
        var frt = field.AddComponent<RectTransform>();
        frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0, 1);
        frt.anchoredPosition = new Vector2(440, -120);
        frt.sizeDelta = new Vector2(900, 880);
        _nodeLayer = field.transform;
        // детали справа
        var det = Box(box, "detail", new Vector2(-30, -120), new Vector2(470, 880), Panel, new Vector2(1, 1));
        _detTitle = Label(det, "", 34, TextAnchor.UpperLeft, new Vector2(24, -18), new Vector2(420, 100), new Vector2(0, 1));
        _detTitle.color = GameBootstrap.COutline;
        _detBody = Label(det, "", 26, TextAnchor.UpperLeft, new Vector2(24, -130), new Vector2(420, 260), new Vector2(0, 1));
        _detCost = Label(det, "", 26, TextAnchor.UpperLeft, new Vector2(24, -400), new Vector2(420, 120), new Vector2(0, 1));
        _detBlock = Label(det, "", 26, TextAnchor.UpperLeft, new Vector2(24, -520), new Vector2(420, 100), new Vector2(0, 1));
        _detBlock.color = new Color(1f, 0.6f, 0.5f);
        var buy = MakeButton(det, "buy", new Vector2(0, 150), new Vector2(420, 100), GameBootstrap.CFood, 0.9f, "Развить", 32, new Vector2(0.5f, 0));
        _detBuy = buy.GetComponent<Button>();
        _detBuyLabel = buy.GetComponentInChildren<Text>();
        _detBuy.onClick.AddListener(() => TryBuy(false));
        var free = MakeButton(det, "free", new Vector2(0, 36), new Vector2(420, 96), new Color(1f, 0.85f, 0.35f), 0.85f, "Жетон: бесплатно", 28, new Vector2(0.5f, 0));
        _detFree = free.GetComponent<Button>();
        _detFree.onClick.AddListener(() => TryBuy(true));
        _evoModal.SetActive(false);
    }

    private static Color BranchColor(EvoBranch b) {
        switch (b) {
            case EvoBranch.Body: return new Color(0.4f, 0.8f, 0.5f);
            case EvoBranch.Motion: return new Color(0.35f, 0.75f, 1f);
            case EvoBranch.Hunt: return new Color(1f, 0.5f, 0.4f);
            case EvoBranch.Senses: return new Color(0.75f, 0.55f, 1f);
            default: return new Color(1f, 0.8f, 0.4f);
        }
    }

    public void OpenEvolution() {
        if (_game == null || !_game.InRun || _game.UiModalOpen) return;
        _evoModal.SetActive(true);
        _root.SetAsLastSibling();                // поверх миникарты и HUD
        _evoModal.transform.SetAsLastSibling();
        _game.SetPaused(true);
        RefreshEvolution();
    }

    private void CloseEvolution() {
        _evoModal.SetActive(false);
        _game.SetPaused(false);
    }

    private void RefreshStore() {
        var run = _game.Run;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < ResourceInfo.Count; i++) {
            var k = (ResourceKind)i;
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Hex(ResourceInfo.Color(k)))).Append(">●</color> ")
              .Append(ResourceInfo.Name(k)).Append(": ").Append(Mathf.FloorToInt((float)run.Resources.Get(k)))
              .Append(" / ").Append(Mathf.FloorToInt((float)run.Resources.Capacity)).Append('\n');
        }
        _storeText.text = sb.ToString();
        _tokenText.text = run.Evolution.FreeTokens > 0
            ? "Жетоны эволюции: " + run.Evolution.FreeTokens + "\n(любой доступный узел бесплатно)"
            : "Жетоны дают рост массы\nи святилища.";
    }

    private void RefreshEvolution() {
        var run = _game.Run;
        for (int b = 0; b < 5; b++) {
            var c = BranchColor((EvoBranch)b);
            _tabImgs[b].color = new Color(c.r, c.g, c.b, (EvoBranch)b == _branch ? 0.95f : 0.3f);
            // число узлов, которые можно развить прямо сейчас, — видно, в какую ветвь заглянуть
            int avail = CountAvailable(run, (EvoBranch)b);
            _tabTexts[b].text = EvolutionTree.BranchName((EvoBranch)b) + (avail > 0 ? "  •" + avail : "");
            _tabTexts[b].color = avail > 0 ? new Color(1f, 0.95f, 0.6f) : TextC;
        }
        RefreshStore();
        for (int i = _nodeLayer.childCount - 1; i >= 0; i--) Destroy(_nodeLayer.GetChild(i).gameObject);
        var nodes = EvolutionTree.Branch(_branch);
        var pos = new Dictionary<string, Vector2>();
        const float cw = 290f, ch = 210f;
        for (int i = 0; i < nodes.Length; i++)
            pos[nodes[i].Id] = new Vector2(nodes[i].Column * cw + 140f, -(nodes[i].Tier - 1) * ch - 90f);
        // связи — тонкие линии (под карточками)
        for (int i = 0; i < nodes.Length; i++) {
            var n = nodes[i];
            if (n.Requires == null) continue;
            for (int r = 0; r < n.Requires.Length; r++) {
                if (!pos.TryGetValue(n.Requires[r], out var a)) continue;
                bool lit = run.Evolution.Unlocked.Contains(n.Requires[r]);
                Line(_nodeLayer, a, pos[n.Id], lit ? new Color(0.6f, 0.95f, 1f, 0.8f) : new Color(1f, 1f, 1f, 0.15f));
            }
        }
        for (int i = 0; i < nodes.Length; i++) NodeCard(nodes[i], pos[nodes[i].Id], run);
        if (_selected == null && nodes.Length > 0) _selected = nodes[0].Id;
        ShowDetail(_selected);
    }

    private void NodeCard(EvoNode n, Vector2 p, RunState run) {
        var block = run.Evolution.CanUnlock(n.Id, run.Resources, run.KnownSpecies);
        bool owned = run.Evolution.Unlocked.Contains(n.Id);
        bool gene = !string.IsNullOrEmpty(n.RequiredGene) && (run.KnownSpecies == null || !run.KnownSpecies.Contains(n.RequiredGene));
        Color bc = BranchColor(n.Branch);
        int avail = owned ? 0 : Availability(run, n);
        Color hl = avail == 2 ? new Color(1f, 0.82f, 0.3f) : new Color(0.45f, 1f, 0.55f);
        // доступные — подсветка: пульсирующее свечение за карточкой (за ресурсы — зелёное, жетоном — золотое)
        if (avail != 0) {
            var glow = Box(_nodeLayer, "glow_" + n.Id, p, new Vector2(300, 200), new Color(hl.r, hl.g, hl.b, 0.5f), new Vector2(0, 1));
            glow.pivot = new Vector2(0.5f, 0.5f);
            glow.GetComponent<Image>().raycastTarget = false;
            glow.gameObject.AddComponent<UiPulse>().Base = new Color(hl.r, hl.g, hl.b, 0.55f);
        }
        Color bg = owned ? new Color(bc.r * 0.6f, bc.g * 0.6f, bc.b * 0.6f, 0.95f)
                 : avail != 0 ? new Color(bc.r * 0.42f, bc.g * 0.42f, bc.b * 0.42f, 0.98f)
                 : new Color(0.07f, 0.08f, 0.1f, 0.92f);                  // недоступные — приглушены
        var card = MakeButton(_nodeLayer, "node_" + n.Id, p, new Vector2(270, 170), bg, 1f, "", 24, new Vector2(0, 1), center: true);
        var outline = card.AddComponent<Outline>();
        outline.effectColor = n.Id == _selected ? new Color(1f, 1f, 1f, 0.95f) : avail != 0 ? hl : new Color(0, 0, 0, 0);
        outline.effectDistance = avail != 0 || n.Id == _selected ? new Vector2(4, -4) : new Vector2(3, -3);
        var nodeIcon = ThirdPartyArt.Icon(NodeIcon(n));
        if (nodeIcon != null) {
            var ig = new GameObject("icon");
            ig.transform.SetParent(card.transform, false);
            var irt = ig.AddComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(0, 2);
            irt.sizeDelta = new Vector2(54, 54);
            var iimg = ig.AddComponent<Image>();
            iimg.sprite = nodeIcon;
            iimg.raycastTarget = false;
            iimg.color = owned ? Color.white : avail != 0 ? new Color(bc.r * 0.6f + 0.4f, bc.g * 0.6f + 0.4f, bc.b * 0.6f + 0.4f) : new Color(1, 1, 1, 0.3f);
        }
        if (avail != 0) {
            var tag = Label(card.transform, avail == 2 ? "ЖЕТОН" : "МОЖНО", 18, TextAnchor.UpperLeft, new Vector2(10, -4), new Vector2(110, 26), new Vector2(0, 1));
            tag.color = hl;
        }
        string title = gene ? "??? (" + GeneName(n.RequiredGene) + ")" : n.Name;
        var tt = Label(card.transform, title, 25, TextAnchor.UpperCenter, new Vector2(0, -22), new Vector2(250, 66), new Vector2(0.5f, 1));
        if (!owned && avail == 0) tt.color = new Color(0.62f, 0.66f, 0.72f);
        string sub = owned ? "развито" : CostShort(n.Cost);
        var st = Label(card.transform, sub, 21, TextAnchor.LowerCenter, new Vector2(0, 10), new Vector2(256, 84), new Vector2(0.5f, 0));
        st.supportRichText = true;
        if (!string.IsNullOrEmpty(n.ExclusiveGroup))
            Label(card.transform, "развилка", 18, TextAnchor.UpperRight, new Vector2(-8, -4), new Vector2(110, 26), new Vector2(1, 1)).color = new Color(1f, 0.8f, 0.4f);
        string id = n.Id;
        card.GetComponent<Button>().onClick.AddListener(() => { _selected = id; RefreshEvolution(); });
    }

    private void ShowDetail(string id) {
        var run = _game.Run;
        var n = id != null ? EvolutionTree.Get(id) : null;
        if (n == null) { _detTitle.text = ""; _detBody.text = ""; _detCost.text = ""; _detBlock.text = ""; _detBuy.gameObject.SetActive(false); _detFree.gameObject.SetActive(false); return; }
        bool owned = run.Evolution.Unlocked.Contains(n.Id);
        _detTitle.text = n.Name + "\n<size=22>" + EvolutionTree.BranchName(n.Branch) + " · тир " + n.Tier + "</size>";
        _detTitle.supportRichText = true;
        var sb = new System.Text.StringBuilder(n.Description);
        if (!string.IsNullOrEmpty(n.ExclusiveGroup)) sb.Append("\n\nРазвилка: в этой группе можно выбрать только один путь.");
        if (!string.IsNullOrEmpty(n.RequiredGene)) sb.Append("\nГен: ").Append(GeneName(n.RequiredGene));
        _detBody.text = sb.ToString();
        _detCost.supportRichText = true;
        _detCost.text = owned ? "Уже развито" : "Цена:\n" + CostLong(n.Cost, run.Resources);
        var block = run.Evolution.CanUnlock(n.Id, run.Resources, run.KnownSpecies);
        _detBlock.text = owned || block == UnlockBlock.None ? "" : EvolutionState.BlockText(block);
        _detBuy.gameObject.SetActive(!owned);
        _detBuy.interactable = block == UnlockBlock.None;
        _detBuyLabel.text = block == UnlockBlock.None ? "Развить" : "Недоступно";
        bool freeOk = !owned && run.Evolution.FreeTokens > 0 && run.Evolution.CanUnlockFree(n.Id, run.KnownSpecies) == UnlockBlock.None;
        _detFree.gameObject.SetActive(freeOk);
    }

    private void TryBuy(bool free) {
        if (_selected == null) return;
        if (_game.TryEvolve(_selected, free)) {
            ProceduralSfx.Instance?.Play("unlock");
            RefreshEvolution();
        } else ProceduralSfx.Instance?.Play("hit");
    }

    /// <summary>Иконка ресурса (game-icons.net, CC BY 3.0 — Lorc).</summary>
    public static string ResourceIcon(ResourceKind k) {
        switch (k) {
            case ResourceKind.Organic: return "lorc_leaf-swirl";
            case ResourceKind.Chitin: return "lorc_beetle-shell";
            case ResourceKind.Slime: return "lorc_drop";
            case ResourceKind.Crystal: return "lorc_crystal-cluster";
            case ResourceKind.Ember: return "lorc_fire-ray";
            default: return "lorc_brain";
        }
    }

    /// <summary>Иконка узла: по мутации, иначе по виду эффекта, иначе по ветви.</summary>
    private static string NodeIcon(EvoNode n) {
        if (n.Effects != null && n.Effects.Length > 0) {
            var e = n.Effects[0];
            if (e.Kind == EvoEffectKind.Mutation) {
                switch (e.MutationId) {
                    case "m_spikes": return "lorc_spiked-shell";
                    case "m_shell": return "lorc_checked-shield";
                    case "m_sticky": return "lorc_gooey-molecule";
                    case "m_dash2": return "lorc_sprint";
                    case "m_magnet": return "lorc_magnet";
                    case "m_eyes": return "lorc_semi-closed-eye";
                    case "m_toxic": return "lorc_biohazard";
                    case "m_light": return "lorc_fluffy-wing";
                    case "m_regrow": return "lorc_heart-organ";
                    case "m_hunger": return "lorc_meat";
                    case "m_crystal": return "lorc_crystal-growth";
                    case "m_heatproof": return "lorc_heat-haze";
                    case "m_vampire": return "lorc_claw-slashes";
                    case "m_ricochet": return "lorc_divert";
                    case "m_echo": return "lorc_bubbles";
                }
            }
            switch (e.Kind) {
                case EvoEffectKind.MaxDash: case EvoEffectKind.DashSpeedMult: return "lorc_sprint";
                case EvoEffectKind.MaxFragments: case EvoEffectKind.SplitMinMass: return "lorc_split-cross";
                case EvoEffectKind.StorageCap: return "lorc_scroll-unfurled";
                case EvoEffectKind.LightFlat: return "lorc_light-bulb";
                case EvoEffectKind.MassGainMult: return "delapouite_slime";
                case EvoEffectKind.YieldMult: return "lorc_emerald";
                case EvoEffectKind.RegenFlat: return "lorc_heart-organ";
                case EvoEffectKind.DamageTakenMult: return "lorc_checked-shield";
                case EvoEffectKind.PullFlat: return "lorc_magnet";
            }
        }
        return "lorc_dna1";
    }

    private static string GeneName(string speciesId) {
        var sp = SpeciesCatalog.Get(speciesId);
        return "поглотите: " + (sp != null ? sp.Name : speciesId);
    }

    private static string CostShort(Cost c) {
        if (c == null || c.Items == null) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < c.Items.Length; i++) {
            if (i > 0) sb.Append("  ");
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Hex(ResourceInfo.Color(c.Items[i].Kind)))).Append(">●</color>")
              .Append(Mathf.CeilToInt((float)c.Items[i].Amount));
        }
        return sb.ToString();
    }

    private static string CostLong(Cost c, ResourceStore store) {
        if (c == null || c.Items == null) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < c.Items.Length; i++) {
            var it = c.Items[i];
            bool enough = store.Get(it.Kind) >= it.Amount - 1e-9;
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Hex(ResourceInfo.Color(it.Kind)))).Append(">●</color> ")
              .Append(ResourceInfo.Name(it.Kind)).Append(": ")
              .Append(enough ? "" : "<color=#FF8877>").Append(Mathf.FloorToInt((float)store.Get(it.Kind))).Append(" / ")
              .Append(Mathf.CeilToInt((float)it.Amount)).Append(enough ? "" : "</color>").Append('\n');
        }
        return sb.ToString();
    }

    // ================= Показатели =================
    private void BuildStats() {
        _statsModal = FullScreen("stats_modal", new Color(0f, 0f, 0f, 0.8f));
        var box = Box(_statsModal.transform, "stats_box", Vector2.zero, new Vector2(1400, 1000), Modal, new Vector2(0.5f, 0.5f));
        Label(box, "ПОКАЗАТЕЛИ", 46, TextAnchor.MiddleLeft, new Vector2(40, -20), new Vector2(600, 60), new Vector2(0, 1)).color = GameBootstrap.COutline;
        Label(box, "Коснитесь строки — увидите, откуда взялось значение", 24, TextAnchor.MiddleLeft, new Vector2(40, -80), new Vector2(900, 40), new Vector2(0, 1)).color = new Color(0.7f, 0.8f, 0.9f);
        var close = MakeButton(box, "close", new Vector2(-22, -18), new Vector2(170, 74), GameBootstrap.CSlime, 0.6f, "Закрыть", 28, new Vector2(1, 1));
        close.GetComponent<Button>().onClick.AddListener(() => { _statsModal.SetActive(false); _game.SetPaused(false); });
        var list = new GameObject("list");
        list.transform.SetParent(box, false);
        var lrt = list.AddComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0, 1);
        lrt.anchoredPosition = new Vector2(40, -130);
        lrt.sizeDelta = new Vector2(1320, 850);
        _statsList = list.transform;
        _statsModal.SetActive(false);
    }

    public void OpenStats() {
        if (_game == null || !_game.InRun || _game.UiModalOpen) return;
        _statsModal.SetActive(true);
        _root.SetAsLastSibling();
        _statsModal.transform.SetAsLastSibling();
        _game.SetPaused(true);
        RefreshStats();
    }

    private void RefreshStats() {
        for (int i = _statsList.childCount - 1; i >= 0; i--) Destroy(_statsList.GetChild(i).gameObject);
        var lines = _game.Run.Stats();
        float y = 0f;
        // две колонки строк; раскрытая строка занимает всю ширину под собой
        for (int i = 0; i < lines.Count; i++) {
            var l = lines[i];
            bool open = l.Key == _expanded;
            float h = open ? 64f + 30f * (1 + (l.Sources != null ? l.Sources.Length : 0)) : 58f;
            var row = MakeButton(_statsList, "stat_" + l.Key, new Vector2(0, y), new Vector2(1320, h - 6),
                open ? new Color(0.15f, 0.3f, 0.4f) : new Color(0.08f, 0.13f, 0.18f), 0.95f, "", 24, new Vector2(0, 1));
            Label(row.transform, l.Name, 28, TextAnchor.MiddleLeft, new Vector2(24, -6), new Vector2(600, 46), new Vector2(0, 1));
            var v = Label(row.transform, l.Value, 30, TextAnchor.MiddleRight, new Vector2(-24, -6), new Vector2(400, 46), new Vector2(1, 1));
            v.color = GameBootstrap.COutline;
            if (open) {
                var sb = new System.Text.StringBuilder();
                sb.Append("<i>").Append(l.Tooltip).Append("</i>");
                if (l.Sources != null) for (int s = 0; s < l.Sources.Length; s++) sb.Append("\n  • ").Append(l.Sources[s]);
                var det = Label(row.transform, sb.ToString(), 24, TextAnchor.UpperLeft, new Vector2(40, -54), new Vector2(1240, h - 60), new Vector2(0, 1));
                det.supportRichText = true;
                det.color = new Color(0.8f, 0.88f, 0.95f);
            }
            string key = l.Key;
            row.GetComponent<Button>().onClick.AddListener(() => { _expanded = _expanded == key ? null : key; RefreshStats(); });
            y -= h;
            if (y < -840f) break;
        }
    }

    // ================= Строители =================
    public static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

    private GameObject FullScreen(string name, Color color) {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-200, -200); rt.offsetMax = new Vector2(200, 200);
        go.AddComponent<Image>().color = color;
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

    private static GameObject MakeButton(Transform parent, string name, Vector2 pos, Vector2 size, Color color, float alpha,
                                         string label, int labelSize, Vector2 anchor, bool center = false) {
        var rt = Box(parent, name, pos, size, new Color(color.r, color.g, color.b, alpha), anchor);
        if (center) rt.pivot = new Vector2(0.5f, 0.5f);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = rt.GetComponent<Image>();
        if (ThirdPartyArt.Button != null && !string.IsNullOrEmpty(label)) rt.GetComponent<Image>().sprite = ThirdPartyArt.Button;
        btn.onClick.AddListener(() => ProceduralSfx.Instance?.Play("click"));
        if (!string.IsNullOrEmpty(label)) {
            var t = UiUtil.AddText(rt, label, labelSize, TextAnchor.MiddleCenter, TextC);
            t.rectTransform.sizeDelta = size - new Vector2(16, 8);
            t.raycastTarget = false;
        }
        return rt.gameObject;
    }

    private static Text Label(Transform parent, string content, int size, TextAnchor align, Vector2 pos, Vector2 box, Vector2 anchor) {
        var t = UiUtil.AddText(parent, content, size, align, TextC);
        var rt = t.rectTransform;
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        t.raycastTarget = false;
        t.supportRichText = true;
        return t;
    }

    private static void Line(Transform parent, Vector2 a, Vector2 b, Color c) {
        var go = new GameObject("link");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 d = b - a;
        rt.sizeDelta = new Vector2(d.magnitude, 6f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
    }
}

/// <summary>Пульс подсветки (на unscaled-времени — работает и на паузе).</summary>
public sealed class UiPulse : MonoBehaviour {
    public Color Base = Color.white;
    private Image _img;
    void Awake() { _img = GetComponent<Image>(); }
    void Update() {
        if (_img == null) return;
        float k = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
        _img.color = new Color(Base.r, Base.g, Base.b, Base.a * k);
    }
}
}
