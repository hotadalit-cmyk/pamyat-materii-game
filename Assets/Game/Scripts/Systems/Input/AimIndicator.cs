using UnityEngine;
using PM.Domain.Core;

namespace PM.Systems.Input {
/// <summary>Визуальный вектор прицеливания: направление — движение, длина — сила (VISUAL_UI).</summary>
[RequireComponent(typeof(LineRenderer))]
public sealed class AimIndicator : MonoBehaviour {
    public static AimIndicator Instance { get; private set; }
    private LineRenderer _line;
    private Transform _head;
    public Color AimColor = new Color(0.62f, 0.92f, 1f, 0.9f); // #9FEAFF

    void Awake() { Instance = this; _line = GetComponent<LineRenderer>(); _line.positionCount = 2; _line.widthMultiplier = 0.06f; _line.enabled = false; }

    public void Show(bool valid, Vec2 dir, double charge) {
        if (!valid) { Hide(); return; }
        var origin = transform.parent != null ? transform.parent.position : transform.position;
        double len = 0.8 + charge * 2.2;
        var end = origin + new Vector3((float)dir.X * (float)len, (float)dir.Y * (float)len, 0);
        _line.SetPosition(0, origin);
        _line.SetPosition(1, end);
        var c = AimColor; c.a = Mathf.Lerp(0.35f, 0.95f, (float)charge);
        _line.startColor = c; _line.endColor = c;
        _line.enabled = true;
    }

    public void Hide() { if (_line != null) _line.enabled = false; }
}
}
