using UnityEngine;

namespace PM.Systems.Gameplay {
/// <summary>
/// Живые детали существ (v0.7): пузырьки внутри слизня кружат и «дышат»,
/// лапки жуков семенят тем быстрее, чем быстрее жук движется. Всё процедурно, без ассетов.
/// </summary>
public sealed class SlimeBubbles : MonoBehaviour {
    private Transform[] _b;
    private float[] _phase, _speed, _rad;

    public static void Attach(Transform body, int count) {
        var host = body.gameObject.AddComponent<SlimeBubbles>();
        host._b = new Transform[count];
        host._phase = new float[count];
        host._speed = new float[count];
        host._rad = new float[count];
        for (int i = 0; i < count; i++) {
            float s = Random.Range(0.06f, 0.13f);
            var sr = SpriteUtil.AddSprite(body, SpriteUtil.CircleSprite, s, s, new Color(0.85f, 0.97f, 1f, 0.45f), 10);
            host._b[i] = sr.transform;
            host._phase[i] = Random.Range(0f, Mathf.PI * 2f);
            host._speed[i] = Random.Range(0.4f, 1.1f) * (Random.value < 0.5f ? -1f : 1f);
            host._rad[i] = Random.Range(0.12f, 0.3f);
        }
    }

    void Update() {
        float t = Time.time;
        for (int i = 0; i < _b.Length; i++) {
            float a = _phase[i] + t * _speed[i];
            float r = _rad[i] * (0.85f + 0.15f * Mathf.Sin(t * 1.7f + i));
            _b[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 1.3f) * r, 0f);
        }
    }
}

public sealed class LegWiggle : MonoBehaviour {
    private Transform[] _legs;
    private float[] _base;
    private Rigidbody2D _rb;
    private float _t;

    public static void Attach(Transform body, Rigidbody2D rb, Color color) {
        var host = body.gameObject.AddComponent<LegWiggle>();
        host._rb = rb;
        host._legs = new Transform[6];
        host._base = new float[6];
        for (int i = 0; i < 6; i++) {
            int side = i < 3 ? -1 : 1;
            int row = i % 3;
            var pivot = new GameObject("leg");
            pivot.transform.SetParent(body, false);
            pivot.transform.localPosition = new Vector3(side * 0.34f, 0.18f - row * 0.2f, 0f);
            var leg = SpriteUtil.AddSprite(pivot.transform, SpriteUtil.CircleSprite, 0.32f, 0.07f, color, 5);
            leg.transform.localPosition = new Vector3(side * 0.14f, 0f, 0f);
            float baseAng = side * (row - 1) * -22f;
            pivot.transform.localRotation = Quaternion.Euler(0, 0, baseAng);
            host._legs[i] = pivot.transform;
            host._base[i] = baseAng;
        }
    }

    void Update() {
        float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
        _t += Time.deltaTime * (2f + speed * 5f);
        float amp = Mathf.Clamp(6f + speed * 8f, 6f, 28f);
        for (int i = 0; i < _legs.Length; i++) {
            float ph = (i % 2 == 0 ? 0f : Mathf.PI) + (i < 3 ? 0f : Mathf.PI * 0.5f);
            _legs[i].localRotation = Quaternion.Euler(0, 0, _base[i] + Mathf.Sin(_t + ph) * amp);
        }
    }
}
}
