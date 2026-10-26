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
        public float BaseRadius;
    }

    private readonly Dictionary<int, FragView> _frags = new Dictionary<int, FragView>();
    private readonly List<FragView> _fragList = new List<FragView>();
    private bool _gathering;
    private float _gatherTimer;
    private readonly HashSet<int> _crackHinted = new HashSet<int>();
    private float _twoFingerT = -1f;

    public bool Gathering => _gathering;
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
        CreateFragmentView(Run.Fragments.LeadId, at);
        BindLead();
    }

    /// <summary>Привести тела в соответствие с RunState.Fragments (после деления, слияния, растворения).</summary>
    private void SyncFragments() {
        var ids = Run.Fragments.Ids();
        for (int i = _fragList.Count - 1; i >= 0; i--) {
            var fv = _fragList[i];
            if (System.Array.IndexOf(ids, fv.Id) >= 0) continue;
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
    public void RequestSplit() {
        if (!InRun || _ui.ModalOpen) return;
        var pairs = Run.TrySplit();
        if (pairs.Count == 0) {
            _ui.Flash(Run.Fragments.Count >= FragmentSet.MaxFragments ? "Больше делиться нельзя" : "Слишком мало массы, чтобы разделиться");
            ProceduralSfx.Instance?.Play("hit");
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
        ProceduralSfx.Instance?.Play("assist");
        GameFeel.Shake(0.15f);
        SplashBurst2D.Instance?.Burst(_player.transform.position, Vector2.up, 8);
        _ui.Flash(string.Format("Разделились: {0} фрагмента(ов)", Run.Fragments.Count), 1.6f);
    }

    public void RequestGather() {
        if (!InRun || _ui.ModalOpen || Run.Fragments.Count <= 1) return;
        _gathering = !_gathering;
        _gatherTimer = 0f;
        _ui.Flash(_gathering ? "Собираемся в одно целое…" : "Сбор отменён", 1.4f);
        ProceduralSfx.Instance?.Play(_gathering ? "capture" : "hit");
    }

    // ---------- Физика стаи ----------
    private void FragmentsStep(float dt) {
        if (_fragList.Count <= 1) { _gathering = false; return; }
        Vector2 lead = _player.Rb.position;
        float leadR = PlayerRadius;
        if (_gathering) _gatherTimer += dt;
        for (int i = 0; i < _fragList.Count; i++) {
            var fv = _fragList[i];
            if (fv.View == _player) continue;
            var rb = fv.View.Rb;
            Vector2 d = lead - rb.position;
            float dist = d.magnitude, r = FragRadius(fv);
            float gap = dist - leadR - r;
            if (_gathering && gap < 0.12f) {
                // касание в режиме сбора — слияние в ведущего
                int keep = Run.Fragments.LeadId;
                if (Run.MergeFragments(keep, fv.Id)) {
                    ProceduralSfx.Instance?.Play("eat");
                    GameFeel.Text(lead, "Слияние", new Color(0.6f, 0.9f, 1f), 0.8f);
                    SyncFragments();
                    if (Run.Fragments.Count == 1) { _gathering = false; _ui.Flash("Снова единое целое", 1.4f); }
                    return;
                }
            }
            if (gap <= 0.4f || dist < 0.001f) continue;
            // мягкая стая: тянутся к ведущему; в сборе — сильно (и сильнее со временем: застрявшие догоняют)
            float k = _gathering ? 14f + 10f * Mathf.Min(_gatherTimer, 3f) : (gap > 6f ? 7f : 2.5f);
            rb.AddForce(d / dist * (k * rb.mass), ForceMode2D.Force);
        }
        // фрагменты не слипаются случайно вне сбора — лёгкое расталкивание между не-ведущими
        for (int i = 0; i < _fragList.Count; i++)
            for (int j = i + 1; j < _fragList.Count; j++) {
                var a = _fragList[i].View.Rb; var b = _fragList[j].View.Rb;
                Vector2 d = b.position - a.position;
                float min = FragRadius(_fragList[i]) + FragRadius(_fragList[j]) + 0.15f;
                float dd = d.magnitude;
                if (_gathering || dd >= min || dd < 0.001f) continue;
                Vector2 push = d / dd * (min - dd) * 6f;
                a.AddForce(-push * a.mass, ForceMode2D.Force);
                b.AddForce(push * b.mass, ForceMode2D.Force);
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
                _ui.Flash("Узкая трещина — разделитесь, чтобы пролезть", 3f);
        }
    }

    /// <summary>Жест: касание двумя пальцами — разделиться (кнопки дублируют).</summary>
    private void TwoFingerStep() {
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
