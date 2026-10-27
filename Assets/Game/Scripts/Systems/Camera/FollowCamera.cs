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
    /// <summary>v0.5: множитель кадрирования (растёт с массой героя) и смещение тряски — задаёт GameFeel.</summary>
    public static float ZoomMult = 1f;
    /// <summary>Отдаление из настроек игрока («Ближе / Обычно / Дальше»).</summary>
    public static float UserZoom = 1f;
    public static Vector2 ShakeOffset;
    /// <summary>Мгновенно перенести камеру к цели (смена комнаты — без «проезда» через полкарты).</summary>
    public static bool Snap;
    private Vector3 _smoothPos;
    private bool _hasPos;
    public const float BackDistance = -120f; // мир на z=0; 3D-модели крупных стражей наклонены к камере — даём им глубину, чтобы не срезала ближняя плоскость

    private Camera _cam;

    void Awake() {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
        _cam.nearClipPlane = 0.3f;
        _cam.farClipPlane = 260f;   // камера на −120: видно и пол (z=0), и модели, выступающие к камере
        var p = transform.position;
        if (p.z > BackDistance + 0.3f) transform.position = new Vector3(p.x, p.y, BackDistance);
    }

    void LateUpdate() {
        if (_cam == null) return;
        float aspect = (float)Screen.width / (float)System.Math.Max(1, Screen.height);
        float target = (float)PM.Domain.Core.OrthoFraming.ComputeOrthoSize(aspect) * ZoomMult * UserZoom;
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
        if (Target == null) return;
        var bias = Vector3.zero;
        var input = PullGestureInput.Instance;
        if (input != null && input.Active && input.Charge01 > 0) {
            // смещаем фокус в сторону предполагаемой траектории, не закрывая героя
            bias = new Vector3((float)input.AimDir.X, (float)input.AimDir.Y, 0) *
                   AimBias * (float)input.Charge01;
        }
        var p = Target.position + bias;
        // мягкое следование: камера «плывёт» за героем, а не прилипает к нему
        if (!_hasPos || Snap) { _smoothPos = p; _hasPos = true; Snap = false; }
        _smoothPos = Vector3.Lerp(_smoothPos, p, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
        transform.position = new Vector3(_smoothPos.x + ShakeOffset.x, _smoothPos.y + ShakeOffset.y, BackDistance);
    }
}
}
