using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PM.Systems.Gameplay;
using PM.Systems.UI;

namespace PM.Systems.Run {
/// <summary>
/// Круглая кнопка умения в духе мобильных MOBA: свечение готовности, диск цвета умения, иконка,
/// радиальная тень перезарядки, металлический ободок, подпись и значок-счётчик.
/// Срабатывает в момент касания (PointerDown) — умения не ждут отпускания пальца.
/// </summary>
public sealed class SkillButton : MonoBehaviour, IPointerDownHandler {
    public System.Action OnPress;
    /// <summary>Служебные кнопки (меню) не «дышат» свечением.</summary>
    public bool NoGlow;
    public RectTransform Rt { get; private set; }
    private Image _glow, _disc, _icon, _cool, _ring;
    private Text _label, _badge, _center;
    private Color _color;
    private bool _ready = true, _locked;
    private float _flashT;

    public static SkillButton Create(Transform parent, string name, float size, Color color, string icon, string label) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        var sb = go.AddComponent<SkillButton>();
        sb.Rt = rt;
        sb._color = color;
        sb._glow = Img(rt, "glow", UiSkin.Glow, size * 1.55f, new Color(color.r, color.g, color.b, 0.45f));
        sb._disc = Img(rt, "disc", UiSkin.Disc, size, color);
        sb._disc.raycastTarget = true;                                   // зона касания — весь круг
        var ic = string.IsNullOrEmpty(icon) ? null : ThirdPartyArt.Icon(icon);
        sb._icon = Img(rt, "icon", ic, size * 0.5f, new Color(1f, 1f, 1f, 0.95f));
        sb._icon.enabled = ic != null;
        sb._cool = Img(rt, "cooldown", UiSkin.Disc, size * 0.94f, new Color(0f, 0f, 0.02f, 0.62f));
        sb._cool.type = Image.Type.Filled;
        sb._cool.fillMethod = Image.FillMethod.Radial360;
        sb._cool.fillOrigin = (int)Image.Origin360.Top;
        sb._cool.fillClockwise = false;
        sb._cool.fillAmount = 0f;
        Img(rt, "rim", UiSkin.Rim, size, Color.white);
        sb._ring = Img(rt, "flash", UiSkin.Ring, size * 1.2f, new Color(1f, 0.95f, 0.7f, 0f));
        sb._center = UiUtil.AddText(rt, "", (int)(size * 0.26f), TextAnchor.MiddleCenter, Color.white);
        sb._center.rectTransform.sizeDelta = new Vector2(size, size * 0.6f);
        sb._center.raycastTarget = false;
        sb._label = UiUtil.AddText(rt, label, (int)Mathf.Clamp(size * 0.17f, 20, 30), TextAnchor.MiddleCenter, new Color(0.95f, 0.98f, 1f));
        var lrt = sb._label.rectTransform;
        lrt.anchoredPosition = new Vector2(0, -size * 0.5f - 6f);
        lrt.sizeDelta = new Vector2(size * 1.6f, 34);
        sb._label.raycastTarget = false;
        var outl = sb._label.gameObject.AddComponent<Outline>();
        outl.effectColor = new Color(0, 0, 0, 0.85f); outl.effectDistance = new Vector2(2, -2);
        var badgeBg = Img(rt, "badge_bg", UiSkin.Disc, size * 0.3f, new Color(0.9f, 0.25f, 0.25f));
        badgeBg.rectTransform.anchoredPosition = new Vector2(size * 0.36f, size * 0.36f);
        sb._badge = UiUtil.AddText(badgeBg.rectTransform, "", (int)(size * 0.17f), TextAnchor.MiddleCenter, Color.white);
        sb._badge.rectTransform.sizeDelta = new Vector2(size * 0.3f, size * 0.3f);
        sb._badge.raycastTarget = false;
        badgeBg.gameObject.SetActive(false);
        go.AddComponent<UiPressFx>();
        return sb;
    }

    private static Image Img(RectTransform parent, string name, Sprite s, float size, Color c) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }

    public void OnPointerDown(PointerEventData e) {
        if (_locked) { ProceduralSfx.Instance?.Play("deny"); return; }
        OnPress?.Invoke();
    }

    /// <summary>ready — можно применить; locked — умение ещё не открыто (серое, с пометкой).</summary>
    public void SetState(bool ready, bool locked = false) {
        if (ready && !_ready) Flash();        // только что стало доступно — вспышка
        _ready = ready; _locked = locked;
        var c = locked ? new Color(0.3f, 0.32f, 0.36f) : ready ? _color : Color.Lerp(_color, new Color(0.25f, 0.27f, 0.3f), 0.55f);
        _disc.color = c;
        _icon.color = new Color(1f, 1f, 1f, locked ? 0.35f : ready ? 0.95f : 0.6f);
    }

    /// <summary>Доля перезарядки, которую ещё ждать (0 — готово; 1 — только начали).</summary>
    public void SetCooldown(float remaining01) { _cool.fillAmount = Mathf.Clamp01(remaining01); }

    public void SetBadge(string text) {
        _badge.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        _badge.text = text;
    }

    public void SetCenter(string text) { _center.text = text; }

    /// <summary>Счётчик внизу круга (заряды) — иконка остаётся читаемой.</summary>
    public void SetCounter(string text) {
        var rt = _center.rectTransform;
        float size = Rt.sizeDelta.x;
        rt.anchoredPosition = new Vector2(0, -size * 0.3f);
        rt.sizeDelta = new Vector2(size, size * 0.24f);
        _center.fontSize = (int)(size * 0.15f);
        _center.text = text;
        _icon.rectTransform.anchoredPosition = new Vector2(0, size * 0.06f);
    }
    public void SetLabel(string text) { _label.text = text; }

    public void Flash() { _flashT = 0.45f; }

    void Update() {
        float t = Time.unscaledTime;
        // готовое умение мягко дышит свечением; недоступное — без свечения
        float g = _ready && !_locked && !NoGlow ? 0.35f + 0.15f * Mathf.Sin(t * 3.2f + transform.GetSiblingIndex()) : 0f;
        _glow.color = new Color(_color.r, _color.g, _color.b, g);
        if (_flashT > 0f) {
            _flashT -= Time.unscaledDeltaTime;
            float k = 1f - _flashT / 0.45f;
            _ring.color = new Color(1f, 0.95f, 0.7f, 1f - k);
            float s = 0.85f + 0.45f * k;
            _ring.rectTransform.localScale = new Vector3(s, s, 1f);
        } else if (_ring.color.a > 0f) _ring.color = new Color(1f, 0.95f, 0.7f, 0f);
    }
}
}
