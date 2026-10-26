using System.Collections.Generic;
using UnityEngine;
using PM.Systems.CameraRig;
using PM.Systems.Gameplay;

namespace PM.Systems.Run {
/// <summary>
/// «Сочность» (v0.5): тряска камеры, стоп-кадр на сильных ударах, всплывающие надписи в мире.
/// Всё на unscaled-времени — работает и во время стоп-кадра. Пул TextMesh без аллокаций в бою.
/// </summary>
public sealed class GameFeel : MonoBehaviour {
    public static GameFeel Instance { get; private set; }
    private const int PoolSize = 24;

    private float _trauma;          // 0..1, тряска = trauma²
    private float _hitStop;         // сколько ещё держать паузу (unscaled)
    private float _savedTimeScale = 1f;
    private bool _stopping;
    private float _seed;

    private sealed class Popup { public TextMesh Tm; public float Life, MaxLife; public Vector3 Vel; public Color Color; }
    private readonly List<Popup> _pool = new List<Popup>();

    public static GameFeel Create(Transform parent) {
        var go = new GameObject("game_feel");
        go.transform.SetParent(parent, false);
        var f = go.AddComponent<GameFeel>();
        Instance = f;
        f._seed = Random.value * 100f;
        var font = ThirdPartyArt.Title != null ? ThirdPartyArt.Title : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < PoolSize; i++) {
            var p = new GameObject("popup");
            p.transform.SetParent(go.transform, false);
            var tm = p.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            var mr = p.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = DarknessMask.Order + 20;
            p.SetActive(false);
            f._pool.Add(new Popup { Tm = tm });
        }
        return f;
    }

    /// <summary>Тряска: 0.15 — лёгкий толчок, 0.5 — сильный удар, 1 — взрыв.</summary>
    public static void Shake(float amount) {
        if (Instance == null) return;
        Instance._trauma = Mathf.Clamp01(Instance._trauma + amount * GameSettings.Shake);
#if UNITY_ANDROID || UNITY_IOS
        // сильный удар — короткая вибрация (если включена в настройках)
        if (amount >= 0.45f && GameSettings.Vibration && Time.unscaledTime - Instance._lastVibro > 0.6f) {
            Instance._lastVibro = Time.unscaledTime;
            Handheld.Vibrate();
        }
#endif
    }
    private float _lastVibro = -10f;

    /// <summary>Стоп-кадр: мир замирает на долю секунды — удар «ощущается».</summary>
    public static void HitStop(float seconds) {
        var f = Instance;
        if (f == null || seconds <= 0f) return;
        if (!f._stopping) {
            if (Time.timeScale <= 0f) return; // модалка уже держит паузу — не вмешиваемся
            f._savedTimeScale = Time.timeScale;
            Time.timeScale = 0.02f;
            f._stopping = true;
        }
        f._hitStop = Mathf.Max(f._hitStop, seconds);
    }

    /// <summary>Всплывающая надпись в мире (числа массы, «Цепочка ×3», «Броня!»).</summary>
    public static void Text(Vector3 worldPos, string text, Color color, float scale = 1f) {
        var f = Instance;
        if (f == null) return;
        Popup p = null;
        for (int i = 0; i < f._pool.Count; i++) if (!f._pool[i].Tm.gameObject.activeSelf) { p = f._pool[i]; break; }
        if (p == null) p = f._pool[0]; // переиспользуем старейшую
        p.Tm.text = text;
        p.Color = color;
        p.Tm.color = color;
        p.Tm.characterSize = 0.06f * scale;
        p.Tm.transform.position = new Vector3(worldPos.x, worldPos.y + 0.3f, 0f);
        p.Vel = new Vector3(Random.Range(-0.3f, 0.3f), 1.6f, 0f);
        p.MaxLife = p.Life = 0.9f;
        p.Tm.gameObject.SetActive(true);
    }

    /// <summary>Не оставлять паузу «висеть», если модалка открылась во время стоп-кадра.</summary>
    public static void CancelHitStop() {
        var f = Instance;
        if (f == null || !f._stopping) return;
        f._stopping = false;
        f._hitStop = 0f;
    }

    void Update() {
        float dt = Time.unscaledDeltaTime;
        if (_stopping) {
            _hitStop -= dt;
            if (_hitStop <= 0f) {
                _stopping = false;
                if (Time.timeScale > 0f) Time.timeScale = _savedTimeScale;
            }
        }
        // тряска: перлин-шум, затухание траумы
        _trauma = Mathf.Max(0f, _trauma - dt * 1.6f);
        float s = _trauma * _trauma;
        float t = Time.unscaledTime * 22f;
        FollowCamera.ShakeOffset = new Vector2(
            (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f * s * 0.6f,
            (Mathf.PerlinNoise(_seed + 7f, t) - 0.5f) * 2f * s * 0.6f);
        for (int i = 0; i < _pool.Count; i++) {
            var p = _pool[i];
            if (!p.Tm.gameObject.activeSelf) continue;
            p.Life -= dt;
            if (p.Life <= 0f) { p.Tm.gameObject.SetActive(false); continue; }
            p.Tm.transform.position += p.Vel * dt;
            p.Vel *= 1f - 2.5f * dt;
            var c = p.Color; c.a = Mathf.Clamp01(p.Life / (p.MaxLife * 0.5f)); p.Tm.color = c;
        }
    }

    void OnDestroy() {
        FollowCamera.ShakeOffset = Vector2.zero;
        if (_stopping && Time.timeScale > 0f) Time.timeScale = _savedTimeScale;
    }
}
}
