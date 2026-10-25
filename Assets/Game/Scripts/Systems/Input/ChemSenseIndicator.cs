using UnityEngine;
using PM.Domain.Core;
using PM.Systems.Sim;
namespace PM.Systems.Input {
/// <summary>
/// Химическое чутьё (T14, узел n_chem_sense): зелёная линия-стрелка к ближайшей
/// пище в радиусе чутья. Только наблюдаемые факты из Domain (ChemSenseDirection).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public sealed class ChemSenseIndicator : MonoBehaviour {
    public Color SenseColor = new Color(0.37f, 0.81f, 0.58f, 0.85f); // #5FCE95
    private LineRenderer _line;

    void Awake() {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.widthMultiplier = 0.05f;
        _line.enabled = false;
    }

    void Update() {
        var runner = SimRunner.Instance;
        if (runner == null || runner.Sim == null) { if (_line != null) _line.enabled = false; return; }
        double dist; int pelletId;
        var dir = runner.Sim.ChemSenseDirection(out dist, out pelletId);
        if (dir.Approx(Vec2.Zero, 1e-9)) { _line.enabled = false; return; }
        var origin = transform.parent != null ? transform.parent.position : transform.position;
        float len = 1.0f + Mathf.Min(2f, (float)dist * 0.15f);
        var end = origin + new Vector3((float)dir.X, (float)dir.Y, 0) * len;
        _line.SetPosition(0, origin);
        _line.SetPosition(1, end);
        _line.startColor = SenseColor;
        _line.endColor = SenseColor;
        _line.enabled = true;
    }
}
}
