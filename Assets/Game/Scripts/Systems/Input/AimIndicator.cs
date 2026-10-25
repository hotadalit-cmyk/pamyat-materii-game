using UnityEngine;
using PM.Domain.Core;
using PM.Systems.Gameplay;

namespace PM.Systems.Input {
/// <summary>
/// Визуальный вектор прицеливания: направление — движение, длина — сила (VISUAL_UI).
/// Градиентная линия + точка-наконечник, читаемая на солнце (мобильный приоритет).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public sealed class AimIndicator : MonoBehaviour {
    public static AimIndicator Instance { get; private set; }
    private LineRenderer _line;
    private Transform _tip;
    public Color AimColor = new Color(0.62f, 0.92f, 1f, 0.9f); // #9FEAFF
    public float LineWidth = 0.09f;

    void Awake() {
        Instance = this;
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.widthMultiplier = LineWidth;
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) _line.material = new Material(shader);
        _line.enabled = false;
        var dot = SpriteUtil.AddSprite(transform, SpriteUtil.CircleSprite, 0.26f, 0.26f, AimColor, 11);
        dot.name = "aim_tip";
        dot.gameObject.SetActive(false);
        _tip = dot.transform;
    }

    public void Show(bool valid, Vec2 dir, double charge) {
        if (!valid) { Hide(); return; }
        var origin = transform.parent != null ? transform.parent.position : transform.position;
        double len = 0.8 + charge * 2.2;
        var end = origin + new Vector3((float)dir.X * (float)len, (float)dir.Y * (float)len, 0);
        _line.SetPosition(0, origin);
        _line.SetPosition(1, end);
        float a = Mathf.Lerp(0.35f, 0.95f, (float)charge);
        var cStart = AimColor; cStart.a = a;
        var cEnd = AimColor; cEnd.a = a * 0.35f;
        _line.startColor = cStart; _line.endColor = cEnd;
        _line.enabled = true;
        if (_tip != null) {
            _tip.position = end;
            float s = 0.20f + 0.12f * (float)charge;
            _tip.localScale = new Vector3(s, s, 1f);
            var tc = AimColor; tc.a = Mathf.Min(1f, a + 0.05f);
            _tip.GetComponent<SpriteRenderer>().color = tc;
            _tip.gameObject.SetActive(true);
        }
    }

    public void Hide() {
        if (_line != null) _line.enabled = false;
        if (_tip != null) _tip.gameObject.SetActive(false);
    }
}
}
