using System.Collections.Generic;
using UnityEngine;
using PM.Domain.Core;
using PM.Systems.Sim;

namespace PM.Systems.Gameplay {
/// <summary>
/// Влажный след (T06): пул спрайтов по записям TrailLedger; укрупнение и переиспользование —
/// без новых объектов в каждом кадре (ARCHITECTURE «Object pooling»).
/// </summary>
public sealed class TrailEmitter2D : MonoBehaviour {
    private readonly Dictionary<int, SpriteRenderer> _views = new Dictionary<int, SpriteRenderer>();
    private readonly Queue<SpriteRenderer> _pool = new Queue<SpriteRenderer>();
    public int MaxViews = 128;
    public Color TrailColor = new Color(0.24f, 0.73f, 0.89f, 0.38f);

    void LateUpdate() {
        var runner = SimRunner.Instance;
        if (runner == null || runner.Sim == null) return;
        var records = runner.Sim.Trail.Records;
        var alive = new HashSet<int>();
        for (int i = 0; i < records.Count && _views.Count + alive.Count < MaxViews; i++) {
            var r = records[i];
            alive.Add(r.Id);
            SpriteRenderer sr;
            if (!_views.TryGetValue(r.Id, out sr)) {
                sr = TakeFromPool();
                _views[r.Id] = sr;
            }
            if (sr == null) continue;
            sr.gameObject.SetActive(true);
            sr.transform.position = new Vector3((float)r.Pos.X, (float)r.Pos.Y, 0.1f);
            float s = Mathf.Clamp((float)r.Mass * 3f + 0.25f, 0.3f, 1.0f);
            sr.transform.localScale = new Vector3(s, s, 1f);
            var c = TrailColor; c.a = Mathf.Lerp(0.10f, 0.38f, (float)r.Freshness);
            sr.color = c;
        }
        // скрыть записи, которых больше нет
        var dead = new List<int>();
        foreach (var kv in _views) if (!alive.Contains(kv.Key)) dead.Add(kv.Key);
        for (int i = 0; i < dead.Count; i++) {
            var sr = _views[dead[i]];
            if (sr != null) sr.gameObject.SetActive(false);
            _pool.Enqueue(sr);
            _views.Remove(dead[i]);
        }
    }

    private SpriteRenderer TakeFromPool() {
        while (_pool.Count > 0) {
            var sr = _pool.Dequeue();
            if (sr != null) return sr;
        }
        var go = new GameObject("trail_segment");
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = SpriteUtil.CircleSprite;
        s.sortingOrder = 2;
        return s;
    }
}
}
