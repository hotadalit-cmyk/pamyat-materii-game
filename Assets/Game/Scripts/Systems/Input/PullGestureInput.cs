using UnityEngine;
using UnityEngine.EventSystems;
using PM.Domain.Core;
using PM.Domain.Movement;
using PM.Domain.AI;
using PM.Systems.Sim;

namespace PM.Systems.Input {
/// <summary>
/// Оттягивание → вектор импульса (T02). Guard: жесты поверх UI не запускают рывок,
/// отмена при выходе пальца за экран / открытом меню (QA-1, релиз-критерий 1).
/// </summary>
public sealed class PullGestureInput : MonoBehaviour {
    public static PullGestureInput Instance { get; private set; }
    /// <summary>v0.4: режим забега принимает рывок сам (направление, заряд 0..1) — без GameSim.</summary>
    public static System.Action<Vec2, double> ReleaseHandler;
    /// <summary>Модальные экраны забега блокируют жест (выбор мутации, отпечаток жизни).</summary>
    public static bool Blocked;
    public float DragMaxWorld = 3.2f;
    public float MinDragWorld = 0.35f;

    public bool Active { get; private set; }      // жест идёт (для сжатия героя и смещения камеры)
    public Vec2 AimDir { get; private set; }
    public double Charge01 { get; private set; }

    private Camera _cam;
    private bool _dragging;
    private Vector2 _originWorld;
    private int _fingerId = -1;

    void Awake() { Instance = this; _cam = Camera.main; }

    void Update() {
        bool hasSim = SimRunner.Instance != null && SimRunner.Instance.Sim != null;
        if (!hasSim && ReleaseHandler == null) return;
        if (Blocked) { if (_dragging) Cancel("blocked"); return; }
        if (_cam == null) _cam = Camera.main;
        bool blockedByUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (Application.isMobilePlatform) TouchUpdate(blockedByUi);
        else MouseUpdate(blockedByUi);
    }

    private void TouchUpdate(bool blockedByUi) {
        if (UnityEngine.Input.touchCount != 1) { if (_dragging) Cancel("multitouch"); return; }
        var t = UnityEngine.Input.GetTouch(0);
        if (!_dragging) {
            if (t.phase == TouchPhase.Began && !blockedByUi) Begin(t.position, t.fingerId);
            return;
        }
        switch (t.phase) {
            case TouchPhase.Moved:
            case TouchPhase.Stationary: Drag(t.position, blockedByUi); break;
            case TouchPhase.Ended: Release(blockedByUi); break;
            case TouchPhase.Canceled: Cancel("touch_canceled"); break;
        }
    }

    private void MouseUpdate(bool blockedByUi) {
        if (UnityEngine.Input.GetMouseButtonDown(0) && !_dragging && !blockedByUi)
            Begin(UnityEngine.Input.mousePosition, -1);
        else if (_dragging && UnityEngine.Input.GetMouseButton(0))
            Drag(UnityEngine.Input.mousePosition, blockedByUi);
        else if (_dragging && UnityEngine.Input.GetMouseButtonUp(0))
            Release(blockedByUi);
        else if (_dragging && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            Cancel("esc");
    }

    private void Begin(Vector2 screenPos, int fingerId) {
        _dragging = true;
        _fingerId = fingerId;
        _originWorld = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        Active = true;
    }

    private void Drag(Vector2 screenPos, bool blockedByUi) {
        if (blockedByUi) { UpdateCharge(Vec2.Zero, 0, false); return; } // прицеливание поверх UI не бьёт
        var pointer = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        var r = PullCharge.Compute(ToVec(_originWorld), SimRunner.ToVec(pointer), DragMaxWorld, MinDragWorld);
        UpdateCharge(r.Valid ? r.Direction : Vec2.Zero, r.Charge01, r.Valid);
    }

    private void Release(bool blockedByUi) {
        if (!blockedByUi && Active && Charge01 > 0 && ReleaseHandler != null) {
            ReleaseHandler(AimDir, Charge01);
        } else if (!blockedByUi && Active && Charge01 > 0) {
            SimRunner.Instance.Sim.EnqueueCommand(1, new ActorCommand {
                Kind = CommandKind.Jet,
                Direction = AimDir,
                Charge01 = Charge01
            });
        }
        ResetGesture();
    }

    private void Cancel(string reason) {
        ResetGesture(); // отмена без рывка
    }

    private void UpdateCharge(Vec2 dir, double charge, bool valid) {
        AimDir = dir;
        Charge01 = valid ? charge : 0;
        if (AimIndicator.Instance != null)
            AimIndicator.Instance.Show(valid, AimDir, Charge01);
    }

    private void ResetGesture() {
        _dragging = false;
        Active = false;
        AimDir = Vec2.Zero;
        Charge01 = 0;
        if (AimIndicator.Instance != null) AimIndicator.Instance.Hide();
    }

    private static Vec2 ToVec(Vector2 v) => new Vec2(v.x, v.y);
}
}
