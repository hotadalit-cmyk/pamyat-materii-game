using UnityEngine;
using PM.Systems.Sim;
using PM.Systems.Input;

namespace PM.Systems.Gameplay {
/// <summary>
/// Живой вид героя: сжатие при заряде, растяжение на рывке, «дыхание» в покое,
/// пульс мембраны, ядро-маятник по скорости, брызги при рывке (VISUAL_UI).
/// </summary>
public sealed class SlimeVisual : MonoBehaviour {
    public Transform BodyRoot;
    public SpriteRenderer Membrane;
    public SpriteRenderer Core;
    public float SplashSpeedJump = 3.5f; // порог скачка скорости → брызги

    private Vector3 _baseScale = Vector3.one;
    private bool _baseCaptured;
    private float _deformTimer;
    private float _time;
    private Rigidbody2D _rb;
    private float _prevSpeed;
    private bool _prevSpeedValid;

    void Awake() { }

    void LateUpdate() {
        if (BodyRoot == null) return;
        if (!_baseCaptured) { _baseScale = BodyRoot.localScale; _baseCaptured = true; }
        _time += Time.deltaTime;
        var runner = SimRunner.Instance;
        float charge = 0;
        var input = PullGestureInput.Instance;
        if (input != null && input.Active) charge = (float)input.Charge01;

        // сжатие при натяжении (против направления) — сплющивание по оси жеста
        float squash = 1f - 0.18f * charge;
        float speedStretch = 0f;
        float speed = 0f;
        Vector2 velDir = Vector2.right;
        if (runner != null && runner.Sim != null) {
            speed = (float)runner.Sim.Player.Vel.Length;
            var vel = runner.Sim.Player.Vel;
            if (vel.Length > 0.01) velDir = new Vector2((float)(vel.X / vel.Length), (float)(vel.Y / vel.Length));
            speedStretch = Mathf.Min(0.15f, speed * 0.012f);
        } else {
            // v0.4 (режим забега): скорость берём прямо из своего тела
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb != null) {
                var vel = _rb.linearVelocity;
                speed = vel.magnitude;
                if (speed > 0.01f) velDir = vel / speed;
                speedStretch = Mathf.Min(0.15f, speed * 0.012f);
            }
        }
        float stretch = 1f + charge * 0.10f + speedStretch;
        // «дыхание» в покое: ±3.5% медленный пульс
        float breath = (1f - charge) * 0.035f * Mathf.Sin(_time * 2.6f);
        // деформация при ударе
        float deform = _deformTimer > 0 ? 1f - _deformTimer * 0.25f : 1f;
        if (_deformTimer > 0) _deformTimer -= Time.deltaTime;
        float sx = _baseScale.x * (squash + breath) * deform;
        float sy = _baseScale.y * (stretch + breath) * deform;
        BodyRoot.localScale = new Vector3(sx, sy, _baseScale.z);

        // мембрана дышит чуть с отставанием — живой контур
        if (Membrane != null) {
            float mp = 1.12f + 0.04f * Mathf.Sin(_time * 2.6f + 0.9f);
            Membrane.transform.localScale = new Vector3(mp, mp, 1f);
        }
        // ядро-маятник: слегка уходит против скорости (инерция содержимого)
        if (Core != null) {
            var target = new Vector3(-velDir.x, -velDir.y, 0f) * Mathf.Min(0.10f, speed * 0.02f);
            Core.transform.localPosition = Vector3.Lerp(Core.transform.localPosition, target, 6f * Time.deltaTime);
        }

        // брызги на рывке: резкий скачок скорости
        if (_prevSpeedValid && speed - _prevSpeed > SplashSpeedJump && SplashBurst2D.Instance != null)
            SplashBurst2D.Instance.Burst(transform.position, velDir);
        _prevSpeed = speed;
        _prevSpeedValid = true;
    }

    public void OnImpact() { _deformTimer = 0.35f; }
}
}
