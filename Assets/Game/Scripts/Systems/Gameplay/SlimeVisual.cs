using UnityEngine;
using PM.Systems.Sim;
using PM.Systems.Input;

namespace PM.Systems.Gameplay {
/// <summary>Сжатие/растяжение героя: заряд сжимает, рывок растягивает (VISUAL_UI «Состояния героя»).</summary>
public sealed class SlimeVisual : MonoBehaviour {
    public Transform BodyRoot;
    private Vector3 _baseScale = Vector3.one;
    private float _deformTimer;

    void Awake() { if (BodyRoot != null) _baseScale = BodyRoot.localScale; }

    void LateUpdate() {
        if (BodyRoot == null) return;
        var runner = SimRunner.Instance;
        float charge = 0;
        var input = PullGestureInput.Instance;
        if (input != null && input.Active) charge = (float)input.Charge01;
        // сжатие при натяжении (против направления) — сплющивание по оси жеста
        float squash = 1f - 0.18f * charge;
        float speedStretch = 0f;
        if (runner != null && runner.Sim != null)
            speedStretch = Mathf.Min(0.15f, (float)runner.Sim.Player.Vel.Length * 0.012f);
        float stretch = 1f + charge * 0.10f + speedStretch;
        // деформация при ударе
        float deform = _deformTimer > 0 ? 1f - _deformTimer * 0.25f : 1f;
        if (_deformTimer > 0) _deformTimer -= Time.deltaTime;
        BodyRoot.localScale = new Vector3(
            _baseScale.x * squash * deform,
            _baseScale.y * stretch * deform,
            _baseScale.z);
    }

    public void OnImpact() { _deformTimer = 0.35f; }
}
}
