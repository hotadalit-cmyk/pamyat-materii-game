using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PM.Systems.Gameplay;
using PM.Systems.Input;

namespace PM.Systems.Run {
/// <summary>
/// Плавающий джойстик (v0.9, режим управления «Джойстик»): палец в любом месте своей половины экрана —
/// там появляется круг, смещение пальца = направление и сила ползания. Рывок — отдельной кнопкой.
/// Половина — противоположная кнопкам деления (по умолчанию левая).
/// </summary>
public sealed class RunJoystick : MonoBehaviour {
    public static RunJoystick Instance { get; private set; }
    /// <summary>Направление ползания, длина 0..1.</summary>
    public Vector2 Dir { get; private set; }

    private const float Radius = 120f;           // ход ручки в единицах канваса
    private RectTransform _root, _base, _knob;
    private int _finger = -2;                    // -2 — нет; -1 — мышь
    private Vector2 _origin;
    private CanvasGroup _group;

    /// <summary>Где джойстик ждёт палец (как в MOBA — всегда виден в своём углу).</summary>
    private Vector2 Home {
        get {
            var r = _root.rect;
            bool left = !GameSettings.LeftHanded;
            return new Vector2(left ? r.xMin + 330f : r.xMax - 330f, r.yMin + 290f);
        }
    }

    public static RunJoystick Build(Transform canvas) {
        var go = new GameObject("joystick");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var j = go.AddComponent<RunJoystick>();
        j._root = rt;
        j._base = Circle(rt, "base", Radius * 2.2f, new Color(0.6f, 0.85f, 1f, 0.16f));
        j._knob = Circle(j._base, "knob", Radius * 0.95f, new Color(0.6f, 0.9f, 1f, 0.55f));
        j._group = j._base.gameObject.AddComponent<CanvasGroup>();
        j._group.blocksRaycasts = false;
        j._base.gameObject.SetActive(false);
        go.transform.SetAsFirstSibling(); // под всеми кнопками и окнами
        Instance = j;
        return j;
    }

    private static RectTransform Circle(Transform parent, string name, float size, Color c) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = SpriteUtil.UiCircleSprite;
        img.color = c;
        img.raycastTarget = false;
        return rt;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update() {
        bool on = GameSettings.Joystick && !PullGestureInput.Blocked && RunGame.Instance != null && RunGame.Instance.InRun;
        if (!on) { Release(); if (_base.gameObject.activeSelf) _base.gameObject.SetActive(false); return; }
        if (_finger == -2) {                                     // ждёт палец: тусклый, на своём месте
            if (!_base.gameObject.activeSelf) _base.gameObject.SetActive(true);
            _base.anchoredPosition = Home;
            _knob.anchoredPosition = Vector2.zero;
            _group.alpha = 0.45f;
        }
        if (Application.isMobilePlatform) Touches(); else Mouse();
    }

    private void Touches() {
        for (int i = 0; i < UnityEngine.Input.touchCount; i++) {
            var t = UnityEngine.Input.GetTouch(i);
            if (_finger == -2) {
                if (t.phase == TouchPhase.Began && InZone(t.position) && !OverUi(t.fingerId)) Begin(t.fingerId, t.position);
                continue;
            }
            if (t.fingerId != _finger) continue;
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { Release(); return; }
            Move(t.position);
        }
        // палец пропал без Ended (сворачивание приложения) — отпустить
        if (_finger >= 0) {
            bool found = false;
            for (int i = 0; i < UnityEngine.Input.touchCount; i++) if (UnityEngine.Input.GetTouch(i).fingerId == _finger) found = true;
            if (!found) Release();
        }
    }

    private void Mouse() {
        Vector2 p = UnityEngine.Input.mousePosition;
        if (_finger == -2 && UnityEngine.Input.GetMouseButtonDown(0) && InZone(p) && !OverUi(-1)) Begin(-1, p);
        else if (_finger == -1 && UnityEngine.Input.GetMouseButton(0)) Move(p);
        else if (_finger == -1) Release();
    }

    /// <summary>Своя половина экрана, ниже верхней панели.</summary>
    private static bool InZone(Vector2 screen) {
        if (screen.y > Screen.height * 0.78f) return false;
        bool left = !GameSettings.LeftHanded; // кнопки справа → джойстик слева
        return left ? screen.x < Screen.width * 0.5f : screen.x > Screen.width * 0.5f;
    }

    private static bool OverUi(int fingerId) =>
        EventSystem.current != null && (fingerId >= 0 ? EventSystem.current.IsPointerOverGameObject(fingerId) : EventSystem.current.IsPointerOverGameObject());

    private void Begin(int finger, Vector2 screen) {
        _finger = finger;
        _origin = ToLocal(screen);
        _base.anchoredPosition = _origin;
        _knob.anchoredPosition = Vector2.zero;
        _base.gameObject.SetActive(true);
        _group.alpha = 1f;
        Dir = Vector2.zero;
    }

    private void Move(Vector2 screen) {
        Vector2 d = ToLocal(screen) - _origin;
        if (d.magnitude > Radius) {
            // плавающая база: тянется за пальцем, если тот ушёл дальше хода ручки
            _origin += d.normalized * (d.magnitude - Radius);
            _base.anchoredPosition = _origin;
            d = d.normalized * Radius;
        }
        _knob.anchoredPosition = d;
        float m = d.magnitude / Radius;
        Dir = m < 0.15f ? Vector2.zero : d.normalized * m;  // мёртвая зона у центра
    }

    private void Release() {
        _finger = -2;
        Dir = Vector2.zero;
        // база возвращается «домой» в Update
    }

    private Vector2 ToLocal(Vector2 screen) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
        return local;
    }
}

/// <summary>Кнопка, срабатывающая в момент касания (рывок не должен ждать отпускания пальца).</summary>
public sealed class PressButton : MonoBehaviour, IPointerDownHandler {
    public System.Action OnPress;
    public void OnPointerDown(PointerEventData e) { OnPress?.Invoke(); }
}
}
