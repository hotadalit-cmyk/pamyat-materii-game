using UnityEngine;
using PM.Domain.Core;
using PM.Systems.Sim;
using PM.Systems.Input;

namespace PM.Systems.CameraRig {
/// <summary>Топ-даун камера: ортографическое взятие по аспекту (T01) + смещение в прицеливании.</summary>
[RequireComponent(typeof(Camera))]
public sealed class FollowCamera : MonoBehaviour {
    public Transform Target;
    public float AimBias = 1.6f;
    public const float BackDistance = -10f; // мир живёт на z=0, камера обязана отойти

    private Camera _cam;

    void Awake() {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
        var p = transform.position;
        if (p.z > BackDistance + 0.3f) transform.position = new Vector3(p.x, p.y, BackDistance);
    }

    void LateUpdate() {
        if (_cam == null) return;
        float aspect = (float)Screen.width / (float)System.Math.Max(1, Screen.height);
        _cam.orthographicSize = (float)PM.Domain.Core.OrthoFraming.ComputeOrthoSize(aspect);
        if (Target == null) return;
        var bias = Vector3.zero;
        var input = PullGestureInput.Instance;
        if (input != null && input.Active && input.Charge01 > 0) {
            // смещаем фокус в сторону предполагаемой траектории, не закрывая героя
            bias = new Vector3((float)input.AimDir.X, (float)input.AimDir.Y, 0) *
                   AimBias * (float)input.Charge01;
        }
        var p = Target.position + bias;
        transform.position = new Vector3(p.x, p.y, BackDistance);
    }
}
}
