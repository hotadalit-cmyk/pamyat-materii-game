using System.Collections.Generic;
using UnityEngine;
using PM.Domain.Core;
using PM.Domain.Run;
using PM.Systems.Bootstrap;
using PM.Systems.CameraRig;
using PM.Systems.Gameplay;
using PM.Systems.Input;

namespace PM.Systems.Run {
/// <summary>
/// Тело из фрагментов (v0.7). Ведущий фрагмент = _player (за ним камера, свет, прицел);
/// остальные тянутся к нему, рывок получают все. «Собраться» — сильное притяжение и слияние при касании.
/// Масса и правила — в RunState.Fragments; здесь только тела и их связь с id фрагментов.
/// </summary>
public sealed partial class RunGame {
    private sealed class FragView {
        public int Id;
        public SimEntityView2D View;
        public SpriteRenderer Glow;
        public SpriteRenderer Thread;  // v0.13: золотая метка автопилота «Кукловода»
        public float BaseRadius;
        public Vector2 WanderDir;      // куда бредёт клон, когда вокруг нет дела
        public float WanderT;
        public float Weak;             // 0..1 — слабость вдали от души
    }

    private readonly Dictionary<int, FragView> _frags = new Dictionary<int, FragView>();
    private readonly List<FragView> _fragList = new List<FragView>();
    private bool _gathering;
    private float _gatherTimer;
    private readonly HashSet<int> _crackHinted = new HashSet<int>();
    private float _twoFingerT = -1f;

    public bool Gathering => _gathering;
    /// <summary>Сколько ресурсов несут двойники (не сдано в хранилище).</summary>
    public double TwinCarry {
        get {
            if (Run == null) return 0;
            double t = 0;
            for (int i = 0; i < Run.Fragments.Count; i++) t += Run.CarryTotal(Run.Fragments[i].Id);
            return t;
        }
    }
    public int FragmentCount => Run != null ? Run.Fragments.Count : 1;
    public bool CanSplit => Run != null && Run.Fragments.CanSplit && InRun;

    private SimEntityView2D CreateFragmentView(int id, Vector2 pos) {
        float baseR = (float)SizeRule.RadiusForMass(4.0);
        var v = SimEntityView2D.Create(transform, "slime_" + id, SimEntityKind.Player, id, baseR, GameBootstrap.CSlime);
        v.Body.sortingOrder = 8;
        v.Rb.linearDamping = PlayerBaseDamping;
        v.GetComponent<CircleCollider2D>().sharedMaterial = _playerMat;
        VisualFactory.DecorateSlime(v);
        var slime = v.gameObject.AddComponent<SlimeVisual>();
        slime.BodyRoot = v.Body.transform;
        slime.Membrane = v.Membrane;
        slime.Core = v.Core;
        var relay = v.gameObject.AddComponent<RunContactRelay>();
        relay.Game = this;
        relay.FragmentId = id;
        v.transform.position = new Vector3(pos.x, pos.y, 0);
        v.Rb.position = pos;
        // v0.10: капля — объёмная модель как на заставке; плоское тело остаётся мягким ореолом
        var hero = ThirdPartyArt.AttachLook(v.transform, "hero", baseR * 2f, default, 1.7f);
        if (hero != null) {
            var bc = v.Body.color; bc.a = 0.28f; v.Body.color = bc;
            if (v.Core != null) v.Core.enabled = false;
        }
        var fv = new FragView { Id = id, View = v, BaseRadius = baseR };
        // у каждого фрагмента свой огонёк — видно его и в темноте
        fv.Glow = SpriteUtil.AddSprite(v.transform, SpriteUtil.GlowSprite, baseR * 4.5f, baseR * 4.5f,
            new Color(0.4f, 0.85f, 1f, 0.35f), DarknessMask.Order + 3);
        _frags[id] = fv;
        _fragList.Add(fv);
        return v;
    }

    /// <summary>Новая жизнь: все старые тела — прочь, один ведущий фрагмент на старте.</summary>
    private void ResetFragments(Vector2 at) {
        for (int i = 0; i < _fragList.Count; i++) if (_fragList[i].View != null) Destroy(_fragList[i].View.gameObject);
        _frags.Clear();
        _fragList.Clear();
        _gathering = false;
        _crackHinted.Clear();
        ClearLoot();
        CreateFragmentView(Run.Fragments.LeadId, at);
        BindLead();
    }

    /// <summary>Привести тела в соответствие с RunState.Fragments (после деления, слияния, растворения).</summary>
    private void SyncFragments() {
        var ids = Run.Fragments.Ids();
        for (int i = _fragList.Count - 1; i >= 0; i--) {
            var fv = _fragList[i];
            if (System.Array.IndexOf(ids, fv.Id) >= 0) continue;
            _puppetMarked.Remove(fv.Id);   // v0.13: нить лопнувшего/влитого клона исчезает
            if (fv.View != null) {
                SplashBurst2D.Instance?.Burst(fv.View.transform.position, Vector2.up, 6);
                Destroy(fv.View.gameObject);
            }
            _frags.Remove(fv.Id);
            _fragList.RemoveAt(i);
        }
        for (int i = 0; i < ids.Length; i++)
            if (!_frags.ContainsKey(ids[i])) CreateFragmentView(ids[i], _player != null ? (Vector2)_player.transform.position : Vector2.zero);
        BindLead();
        ApplyPlayerSize();
    }

    /// <summary>Новая глубина: вся стая — у старта, ведущий в центре, остальные кольцом.</summary>
    private void PlaceFragmentsAt(Vector2 start) {
        ClearLoot();
        for (int i = 0; i < _fragList.Count; i++) {
            var fv = _fragList[i];
            Vector2 p = start;
            if (fv.View != _player) {
                float a = i * 2.4f;
                p += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.9f;
            }
            fv.View.transform.position = new Vector3(p.x, p.y, 0);
            fv.View.Rb.position = p;
            fv.View.Rb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>Камера, свет и прицел — на ведущем фрагменте.</summary>
    private void BindLead() {
        if (!_frags.TryGetValue(Run.Fragments.LeadId, out var lead)) return;
        _player = lead.View;
        _dark.Follow = _player.transform;
        var cam = Camera.main;
        if (cam != null) {
            var follow = cam.GetComponent<FollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<FollowCamera>();
            follow.Target = _player.transform;
        }
        if (_aim == null) {
            _aim = new GameObject("aim_indicator");
            _aim.AddComponent<LineRenderer>();
            _aim.AddComponent<AimIndicator>();
        }
        _aim.transform.SetParent(_player.transform, false);
        _aim.transform.localPosition = Vector3.zero;
        for (int i = 0; i < _fragList.Count; i++)
            if (_fragList[i].Glow != null) _fragList[i].Glow.enabled = _fragList[i].View != _player; // у ведущего — большой свет
    }
    private GameObject _aim;

    private void SizeFragments() {
        for (int i = 0; i < _fragList.Count; i++) {
            var fv = _fragList[i];
            double m = Run.FragmentMass(fv.Id);
            float r = (float)SizeRule.RadiusForMass(m);
            float s = r / fv.BaseRadius;
            fv.View.transform.localScale = new Vector3(s, s, 1f);
            fv.View.Rb.mass = Mathf.Max(0.1f, (float)m);
        }
    }

    private float FragRadius(FragView fv) => (float)SizeRule.RadiusForMass(Run.FragmentMass(fv.Id));

    /// <summary>Ближайший к точке фрагмент — его ест, бьёт и преследует существо.</summary>
    private FragView NearestFragment(Vector2 p, out float dist) {
        FragView best = null; dist = float.MaxValue;
        for (int i = 0; i < _fragList.Count; i++) {
            var fv = _fragList[i];
            if (fv.View == null) continue;
            float d = Vector2.Distance(p, fv.View.Rb.position) - FragRadius(fv);
            if (d < dist) { dist = d; best = fv; }
        }
        return best;
    }

    // ---------- Команды игрока ----------
    public bool CanSoulTransfer => Run != null && InRun && Run.HasAbility("soul_transfer") && Run.Fragments.Count > 1;
    public bool SplitUnlocked => Run != null && Run.HasAbility("split");

    /// <summary>Перенос души: по кругу в следующего клона — он становится вами (камера, свет, управление).</summary>
    public void RequestSoulTransfer() {
        if (!InRun || _ui.ModalOpen || !CanSoulTransfer) return;
        var ids = Run.Fragments.Ids();
        int target = ids[1];                       // ведущий всегда первый — следующий за ним клон
        Vector2 from = _player.Rb.position;
        if (!Run.TransferSoul(target)) { ProceduralSfx.Instance?.Play("deny"); return; }
        BindLead();
        ApplyPlayerSize();
        ProceduralSfx.Instance?.Play("altar");
        SplashBurst2D.Instance?.Burst(from, Vector2.up, 6);
        GameFeel.Text(_player.transform.position, "Душа здесь", new Color(0.75f, 0.6f, 1f), 1f);
        ShowDeposit(_player.Rb.position);
        _ui.Flash("Душа перенесена — теперь вы этот клон", 1.6f);
    }

    public void RequestSplit() {
        if (!InRun || _ui.ModalOpen) return;
        if (!Run.HasAbility("split")) {
            _ui.Flash("Деление откроет эволюция: ветвь «Деление»");
            ProceduralSfx.Instance?.Play("deny");
            return;
        }
        var pairs = Run.TrySplit();
        if (pairs.Count == 0) {
            _ui.Flash(Run.Fragments.Count >= FragmentSet.MaxFragments ? "Больше делиться нельзя" : "Слишком мало массы, чтобы разделиться");
            ProceduralSfx.Instance?.Play("deny");
            return;
        }
        _gathering = false;
        for (int i = 0; i < pairs.Count; i++) {
            int parent = pairs[i][0], child = pairs[i][1];
            Vector2 at = _frags.TryGetValue(parent, out var pv) ? pv.View.Rb.position : (Vector2)_player.transform.position;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float r = (float)SizeRule.RadiusForMass(Run.FragmentMass(child));
            var v = CreateFragmentView(child, at + dir * r * 1.2f);
            v.Rb.linearVelocity = (pv != null ? pv.View.Rb.linearVelocity : Vector2.zero) + dir * 4f;
            if (pv != null) pv.View.Rb.linearVelocity -= dir * 2f;
        }
        BindLead();
        ApplyPlayerSize();
        ProceduralSfx.Instance?.Play("split");
        GameFeel.Shake(0.15f);
        SplashBurst2D.Instance?.Burst(_player.transform.position, Vector2.up, 8);
        _ui.Flash(string.Format("Разделились: {0} фрагмента(ов)", Run.Fragments.Count), 1.6f);
    }

    public void RequestGather() {
        if (!InRun || _ui.ModalOpen || Run.Fragments.Count <= 1) return;
        _gathering = !_gathering;
        _gatherTimer = 0f;
        _ui.Flash(_gathering ? "Собираемся в одно целое…" : "Сбор отменён", 1.4f);
        ProceduralSfx.Instance?.Play(_gathering ? "merge" : "deny");
    }

    // ---------- Физика клонов ----------
    /// <summary>
    /// v0.9: клоны не управляются — охотятся, бродят и сдают ношу сами. Вдали от души слабеют и
    /// теряют массу, за пределом связи лопаются (RunState.DriftAt). «Собраться» — общий сбор и слияние.
    /// </summary>
    private void FragmentsStep(float dt) {
        if (_fragList.Count <= 1) { _gathering = false; return; }
        Vector2 lead = _player.Rb.position;
        float leadR = PlayerRadius;
        if (_gathering) _gatherTimer += dt;
        bool homing = Run.HasAbility("clone_return");
        for (int i = 0; i < _fragList.Count; i++) {
            var fv = _fragList[i];
            if (fv.View == _player) continue;
            var rb = fv.View.Rb;
            Vector2 d = lead - rb.position;
            float dist = d.magnitude, r = FragRadius(fv);
            float gap = dist - leadR - r;
            bool returning = _gathering || (homing && Run.CarryTotal(fv.Id) >= RunState.CloneReturnCarry);
            if (returning && gap < 0.12f) {
                // касание при сборе (или клон с полной ношей вернулся) — слияние в ведущего
                if (Run.MergeFragments(Run.Fragments.LeadId, fv.Id)) {
                    ProceduralSfx.Instance?.Play("merge");
                    GameFeel.Text(lead, "Слияние", new Color(0.6f, 0.9f, 1f), 0.8f);
                    ShowDeposit(lead);
                    SyncFragments();
                    if (Run.Fragments.Count == 1) { _gathering = false; _ui.Flash("Снова единое целое", 1.4f); }
                    return;
                }
            }
            if (!_gathering) {
                var drift = Run.DriftAt(fv.Id, dist, dt);
                fv.Weak = (float)Run.CloneWeakness(dist);
                if (drift == RunState.Drift.Popped) { OnClonePopped(rb.position); return; }
                if (drift == RunState.Drift.Weak && !_driftHinted) {
                    _driftHinted = true;
                    _ui.Hint("Клон слишком далеко — он слабеет и может лопнуть", 3f);
                }
            } else fv.Weak = 0f;
            if (returning) {
                if (dist < 0.001f) continue;
                float kr = _gathering ? 14f + 10f * Mathf.Min(_gatherTimer, 3f) : 12f;
                rb.AddForce(d / dist * (kr * rb.mass), ForceMode2D.Force);
                continue;
            }
            if (_puppetMarked.Contains(fv.Id)) {
                // v0.13: клон под нитью «Кукловода» играет сам по PuppetBrain
                ThreadStep(fv);
                PuppetCloneStep(fv, rb, lead, dist, dt);
                continue;
            }
            if (TwinForage(fv, rb, lead, dist)) continue;
            Wander(fv, rb, lead, dist, dt);
        }
        // клоны не слипаются случайно — лёгкое расталкивание
        for (int i = 0; i < _fragList.Count; i++)
            for (int j = i + 1; j < _fragList.Count; j++) {
                var a = _fragList[i].View.Rb; var b = _fragList[j].View.Rb;
                Vector2 dd = b.position - a.position;
                float min = FragRadius(_fragList[i]) + FragRadius(_fragList[j]) + 0.15f;
                float m = dd.magnitude;
                if (_gathering || m >= min || m < 0.001f) continue;
                Vector2 push = dd / m * (min - m) * 6f;
                a.AddForce(-push * a.mass, ForceMode2D.Force);
                b.AddForce(push * b.mass, ForceMode2D.Force);
            }
    }

    private bool _driftHinted;

    /// <summary>Скорость клона: прокачка «Проворство», слабость вдали.</summary>
    private float CloneMaxSpeed(FragView fv) => TwinMaxSpeed * (float)Run.CloneSpeedMult * (1f - 0.6f * fv.Weak);

    /// <summary>Нечего делать — клон бродит сам; ближе к краю связи забирает к душе.</summary>
    private void Wander(FragView fv, Rigidbody2D rb, Vector2 lead, float dist, float dt) {
        fv.WanderT -= dt;
        if (fv.WanderT <= 0f || fv.WanderDir == Vector2.zero) {
            fv.WanderT = 1.5f + Random.value * 2.5f;
            float ang = Random.value * Mathf.PI * 2f;
            fv.WanderDir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }
        float soft = (float)Run.CloneLeash;
        Vector2 home = dist > 0.001f ? (lead - rb.position) / dist : Vector2.zero;
        float pull = Mathf.InverseLerp(soft * 0.45f, soft * 0.9f, dist);   // у края связи — домой
        Vector2 dir = Vector2.Lerp(fv.WanderDir, home, pull).normalized;
        float cap = CloneMaxSpeed(fv) * (0.45f + 0.55f * pull);
        if (Vector2.Dot(rb.linearVelocity, dir) < cap) rb.AddForce(dir * (6f * rb.mass), ForceMode2D.Force);
    }

    /// <summary>Клон лопнул: ноша и масса рассыпаются там, где он был.</summary>
    private void OnClonePopped(Vector2 at) {
        DropCarry(at);
        double m = Run.LastPoppedMass;
        if (m > 0.05) SpawnDroplets(at, m * 0.7);
        SplashBurst2D.Instance?.Burst(at, Vector2.up, 14);
        ProceduralSfx.Instance?.Play("dissolve");
        GameFeel.Shake(0.2f);
        _ui.Flash("Клон ушёл слишком далеко и лопнул", 2f);
        SyncFragments();
    }

    // ---------- Двойники-добытчики (v0.9) ----------
        private const float TwinSight = 7f;       // радиус, в котором двойник замечает еду
    private const float TwinFear = 4.5f;      // радиус, в котором двойник бежит от крупных
    private const float TwinMaxSpeed = 6.5f;
    private bool _carryHinted;

    /// <summary>
    /// Самостоятельный двойник: бежит от тех, кто его съест, охотится на тех, кого съест сам, подбирает
    /// выпавшую ношу. Уходит не дальше поводка от ведущего. true — сила уже приложена, «стая» не нужна.
    /// </summary>
    private bool TwinForage(FragView fv, Rigidbody2D rb, Vector2 lead, float distToLead) {
        float soft = (float)Run.CloneLeash;
        Vector2 me = rb.position;
        double m = Run.FragmentMass(fv.Id);
        double ratio = Run.CloneAbsorbRatio;
        float r = FragRadius(fv);
        float sight = (TwinSight + (float)Run.CloneSightBonus) * (1f - 0.5f * fv.Weak);
        Vector2 flee = Vector2.zero;
        Vector2 target = Vector2.zero; float best = sight; bool has = false;
        for (int i = 0; i < _creatures.Count; i++) {
            var c = _creatures[i];
            if (c == null || c.IsBoss || !c.gameObject.activeSelf) continue;
            Vector2 cp = c.View.Rb.position;
            float d = Vector2.Distance(me, cp) - c.Radius - r;
            bool edible = c.IsFood || m >= c.Mass * ratio;
            if (!edible) {
                if (c.Mass > m * 0.9 && d < TwinFear) flee += (me - cp).normalized * (1f - Mathf.Max(0f, d) / TwinFear);
                continue;
            }
            // за добычей — только в пределах связи: дальше клон слабеет
            if (d < best && Vector2.Distance(lead, cp) < soft) { best = d; target = cp; has = true; }
        }
        for (int i = 0; i < _loot.Count; i++) {
            float d = Vector2.Distance(me, _loot[i].Pos);
            if (d < best) { best = d; target = _loot[i].Pos; has = true; }
        }
        bool fleeing = flee.sqrMagnitude > 0.01f;
        if (!fleeing && !has) return false;
        Vector2 dir = fleeing ? flee.normalized : (target - me).normalized;
        float k = fleeing ? 18f : 11f;
        if (Vector2.Dot(rb.linearVelocity, dir) < CloneMaxSpeed(fv)) rb.AddForce(dir * (k * rb.mass), ForceMode2D.Force);
        return true;
    }

    /// <summary>Первая добыча двойника — одноразовое объяснение правила ноши.</summary>
    private void OnTwinLoot() {
        if (_carryHinted) return;
        _carryHinted = true;
        _ui.Hint("Двойник несёт добычу. Соберитесь, чтобы сдать её в хранилище: погибнет — она выпадет", 4.5f);
    }

    /// <summary>Что сдано в хранилище при слиянии — цветными цифрами над ведущим.</summary>
    private void ShowDeposit(Vector2 at) {
        var dep = Run.LastDeposit;
        if (dep == null || dep.Length == 0) return;
        double total = 0;
        for (int i = 0; i < dep.Length; i++) {
            total += dep[i].Amount;
            GameFeel.Text(at + new Vector2(0.7f, -0.3f - 0.45f * i), "+" + dep[i].Amount.ToString("0.#") + " " + ResourceInfo.Name(dep[i].Kind),
                RunEvolutionUi.Hex(ResourceInfo.Color(dep[i].Kind)), 0.8f);
        }
        _ui.Flash("Ноша сдана в хранилище: +" + total.ToString("0"), 1.6f);
        ProceduralSfx.Instance?.Play("deposit");
    }

    // ---------- Выпавшая ноша ----------
    private sealed class Loot {
        public Vector2 Pos;
        public ResourceKind Kind;
        public double Amount;
        public GameObject Go;
        public SpriteRenderer Sr;
        public float Phase;
    }
    private readonly List<Loot> _loot = new List<Loot>();

    /// <summary>Двойник растворился: его ноша рассыпается светящимися сгустками.</summary>
    private void DropCarry(Vector2 at) {
        var drop = Run.LastDropped;
        if (drop == null || drop.Length == 0) return;
        for (int i = 0; i < drop.Length; i++) {
            float a = i * 2.1f + Random.value;
            var p = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.7f;
            var go = new GameObject("loot_" + drop[i].Kind);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(p.x, p.y, 0);
            var col = RunEvolutionUi.Hex(ResourceInfo.Color(drop[i].Kind));
            var sr = SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, 1.3f, 1.3f, new Color(col.r, col.g, col.b, 0.9f), DarknessMask.Order + 2);
            SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 0.32f, 0.32f, Color.Lerp(col, Color.white, 0.4f), DarknessMask.Order + 3);
            _loot.Add(new Loot { Pos = p, Kind = drop[i].Kind, Amount = drop[i].Amount, Go = go, Sr = sr, Phase = Random.value * 6f });
        }
        _ui.Flash("Двойник погиб — его ноша выпала. Подберите её", 2.2f);
    }

    private void LootStep() {
        for (int i = _loot.Count - 1; i >= 0; i--) {
            var l = _loot[i];
            if (l.Sr != null) {
                float s = 1f + 0.18f * Mathf.Sin(Time.time * 4f + l.Phase);
                l.Sr.transform.localScale = new Vector3(s * 1.3f, s * 1.3f, 1f);
            }
            var fv = NearestFragment(l.Pos, out float d);
            if (fv == null || d > 0.35f) continue;
            Run.PickupAt(fv.Id, l.Kind, l.Amount);
            bool lead = fv.Id == Run.Fragments.LeadId;
            GameFeel.Text(l.Pos, "+" + l.Amount.ToString("0.#") + " " + ResourceInfo.Name(l.Kind) + (lead ? "" : " (в ноше)"),
                RunEvolutionUi.Hex(ResourceInfo.Color(l.Kind)), 0.8f);
            ProceduralSfx.Instance?.Play("pickup");
            if (l.Go != null) Destroy(l.Go);
            _loot.RemoveAt(i);
        }
    }

    private void ClearLoot() {
        for (int i = 0; i < _loot.Count; i++) if (_loot[i].Go != null) Destroy(_loot[i].Go);
        _loot.Clear();
    }

    /// <summary>Огонёк двойника золотится, когда он несёт добычу.</summary>
    private void TwinGlowStep() {
        for (int i = 0; i < _fragList.Count; i++) {
            var fv = _fragList[i];
            if (fv.Glow == null || !fv.Glow.enabled) continue;
            double carry = Run.CarryTotal(fv.Id);
            var c = new Color(0.4f, 0.85f, 1f, 0.35f);
            if (carry > 0) {
                float k = 0.35f + 0.65f * Mathf.Clamp01((float)carry / 20f);
                c = Color.Lerp(c, new Color(1f, 0.82f, 0.3f, 0.6f), k);
                c.a *= 0.8f + 0.2f * Mathf.Sin(Time.time * 5f + fv.Id);
            }
            if (fv.Weak > 0f) c = Color.Lerp(c, new Color(1f, 0.25f, 0.2f, 0.7f), fv.Weak * (0.6f + 0.4f * Mathf.Sin(Time.time * 12f)));
            fv.Glow.color = c;
        }
    }

    /// <summary>Подсказка у входа в трещину: целым телом не пролезть.</summary>
    private void CrackHintStep() {
        if (Run.World == null || Run.World.CrackMouths == null) return;
        Vector2 p = _player.Rb.position;
        var mouths = Run.World.CrackMouths;
        for (int i = 0; i < mouths.Count; i++) {
            if (_crackHinted.Contains(i)) continue;
            if (Vector2.Distance(p, new Vector2((float)mouths[i].X, (float)mouths[i].Y)) > 3.5f) continue;
            _crackHinted.Add(i);
            if (PlayerRadius * 2f > 0.95f)
                _ui.Hint("Узкая трещина — разделитесь, чтобы пролезть", 3f);
        }
    }

    /// <summary>Жест: касание двумя пальцами — разделиться (кнопки дублируют).</summary>
    private void TwoFingerStep() {
        if (GameSettings.Joystick || !SplitUnlocked) return; // джойстик + тап по кнопке — не жест; деление ещё не открыто
        if (UnityEngine.Input.touchCount == 2) {
            var a = UnityEngine.Input.GetTouch(0); var b = UnityEngine.Input.GetTouch(1);
            if (b.phase == TouchPhase.Began) _twoFingerT = Time.unscaledTime;
            if ((a.phase == TouchPhase.Ended || b.phase == TouchPhase.Ended) && _twoFingerT > 0 &&
                Time.unscaledTime - _twoFingerT < 0.35f &&
                a.deltaPosition.magnitude < 20f && b.deltaPosition.magnitude < 20f) {
                _twoFingerT = -1f;
                RequestSplit();
            }
        }
    }

    /// <summary>Разброс стаи: камера отъезжает, чтобы видеть все фрагменты.</summary>
    private float FragmentSpread() {
        float max = 0f;
        Vector2 lead = _player.transform.position;
        for (int i = 0; i < _fragList.Count; i++) {
            float d = Vector2.Distance(lead, _fragList[i].View.transform.position);
            if (d > max) max = d;
        }
        return max;
    }
}
}
