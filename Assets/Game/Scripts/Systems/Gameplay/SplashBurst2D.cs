using System.Collections.Generic;
using UnityEngine;
using PM.Domain.Core;
using PM.Systems.Bootstrap;

namespace PM.Systems.Gameplay {
/// <summary>
/// Брызги при рывке (VISUAL_UI «Состояния героя»): пул кружков-капель,
/// разлетающихся по направлению движения. Только визуал, без физики.
/// </summary>
public sealed class SplashBurst2D : MonoBehaviour {
    public static SplashBurst2D Instance { get; private set; }
    public int PoolSize = 28;

    private struct Drop {
        public SpriteRenderer Sr;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public float BaseAlpha;
        public float Scale;
    }
    private readonly List<Drop> _pool = new List<Drop>();

    void Awake() {
        Instance = this;
        for (int i = 0; i < PoolSize; i++) _pool.Add(MakeDrop());
    }

    private Drop MakeDrop() {
        var go = new GameObject("splash_drop");
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteUtil.CircleSprite;
        sr.sortingOrder = 7;
        return new Drop { Sr = sr, Vel = Vector2.zero, Life = 0f, MaxLife = 1f, BaseAlpha = 0.85f, Scale = 0.14f };
    }

    /// <summary>Выброс капель из точки в конусе вокруг направления dir.</summary>
    public void Burst(Vector3 pos, Vector2 dir, int count = 7) {
        int spawned = 0;
        for (int i = 0; i < _pool.Count && spawned < count; i++) {
            var d = _pool[i];
            if (d.Sr.gameObject.activeSelf) continue;
            float ang = (Random.value - 0.5f) * 1.2f; // ±~35°
            var ca = new Vector2(dir.x * Mathf.Cos(ang) - dir.y * Mathf.Sin(ang),
                                 dir.x * Mathf.Sin(ang) + dir.y * Mathf.Cos(ang));
            d.Vel = ca.normalized * Random.Range(1.6f, 3.6f);
            d.MaxLife = Random.Range(0.28f, 0.5f);
            d.Life = d.MaxLife;
            d.Scale = Random.Range(0.10f, 0.20f);
            d.BaseAlpha = 0.85f;
            d.Sr.transform.position = pos + new Vector3(dir.x, dir.y, 0f) * 0.25f;
            d.Sr.transform.localScale = new Vector3(d.Scale, d.Scale, 1f);
            var c = GameBootstrap.CSlime; c.a = d.BaseAlpha;
            d.Sr.color = c;
            d.Sr.gameObject.SetActive(true);
            _pool[i] = d;
            spawned++;
        }
    }

    void Update() {
        float dt = Time.deltaTime;
        for (int i = 0; i < _pool.Count; i++) {
            var d = _pool[i];
            if (!d.Sr.gameObject.activeSelf) continue;
            d.Life -= dt;
            if (d.Life <= 0f) { d.Sr.gameObject.SetActive(false); _pool[i] = d; continue; }
            d.Vel *= (1f - 3.2f * dt);
            var p = d.Sr.transform.position + new Vector3(d.Vel.x, d.Vel.y, 0f) * dt;
            d.Sr.transform.position = p;
            float k = d.Life / d.MaxLife;
            d.Sr.transform.localScale = new Vector3(d.Scale * k, d.Scale * k, 1f);
            var c = d.Sr.color;
            c.a = d.BaseAlpha * k;
            d.Sr.color = c;
            _pool[i] = d;
        }
    }
}
}
