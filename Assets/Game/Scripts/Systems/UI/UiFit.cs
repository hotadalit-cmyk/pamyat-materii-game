using UnityEngine;
using UnityEngine.UI;

namespace PM.Systems.UI {
/// <summary>
/// v0.15: масштаб крупных модальных окон под видимую высоту канвы.
/// CanvasScaler «1920×1080 по ширине» (match=1): на экране 2400×1080 видимая высота
/// канвы всего 864 ед. — окна 930–1060 обрезались краями экрана и за safe area.
/// UiFit.Apply равномерно сжимает бокс (children масштабируются вместе — налезаний нет).
/// v0.16: Scroll — списки, растущие с контентом (энциклопедия, показатели), прокручиваются внутри окна.
/// </summary>
public static class UiFit {
    public const float RefWidth = 1920f, RefHeight = 1080f;

    /// <summary>Видимая высота канвы в единицах референса (с учётом safe area).</summary>
    public static float CanvasHeight() {
        if (Screen.width <= 0 || Screen.height <= 0) return RefHeight;
        float sf = Screen.width / RefWidth;
        float h = Screen.height / sf;
        float safeH = Screen.safeArea.height / sf;
        return Mathf.Min(h, safeH);
    }

    /// <summary>Равномерно ужать бокс, если он выше видимой высоты минус поля.</summary>
    public static void Apply(RectTransform box, float margin = 40f) {
        if (box == null) return;
        float maxH = CanvasHeight() - margin;
        if (maxH <= 0f || box.rect.height <= maxH) return;
        float k = maxH / box.rect.height;
        box.localScale = new Vector3(k, k, 1f);
    }

    /// <summary>
    /// Вертикальная прокрутка: окно size в точке pos (угол — левый верх parent), справа тонкая полоса.
    /// Возвращает content (левый верх в 0,0; дети кладутся вниз с отрицательным y).
    /// После заполнения вызвать SetContentHeight.
    /// </summary>
    public static RectTransform Scroll(Transform parent, Vector2 pos, Vector2 size) {
        var vp = new GameObject("scroll");
        vp.transform.SetParent(parent, false);
        var vrt = vp.AddComponent<RectTransform>();
        vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0, 1);
        vrt.anchoredPosition = pos; vrt.sizeDelta = size;
        vp.AddComponent<RectMask2D>();
        vp.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // ловит перетаскивание между плитками
        var content = new GameObject("content");
        content.transform.SetParent(vp.transform, false);
        var crt = content.AddComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0, 1);
        crt.anchoredPosition = Vector2.zero; crt.sizeDelta = size;
        var sr = vp.AddComponent<ScrollRect>();
        sr.content = crt; sr.viewport = vrt;
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.scrollSensitivity = 40f;
        // полоса прокрутки — снаружи окна, чтобы не налезать на плитки
        var bar = new GameObject("scrollbar");
        bar.transform.SetParent(parent, false);
        var brt = bar.AddComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0, 1);
        brt.anchoredPosition = pos + new Vector2(size.x + 8f, 0f); brt.sizeDelta = new Vector2(8f, size.y);
        bar.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        var handle = new GameObject("handle");
        handle.transform.SetParent(bar.transform, false);
        var hrt = handle.AddComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = hrt.offsetMax = Vector2.zero;
        var himg = handle.AddComponent<Image>();
        himg.color = new Color(0.55f, 0.9f, 1f, 0.65f);
        var sb = bar.AddComponent<Scrollbar>();
        sb.handleRect = hrt; sb.targetGraphic = himg; sb.direction = Scrollbar.Direction.BottomToTop;
        sr.verticalScrollbar = sb;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return crt;
    }

    /// <summary>Высота содержимого после заполнения; прокрутка остаётся в допустимых пределах.</summary>
    public static void SetContentHeight(RectTransform content, float height) {
        var vp = (RectTransform)content.parent;
        float view = vp.sizeDelta.y;
        float h = Mathf.Max(height, view);
        content.sizeDelta = new Vector2(content.sizeDelta.x, h);
        var p = content.anchoredPosition;
        content.anchoredPosition = new Vector2(0f, Mathf.Clamp(p.y, 0f, h - view));
    }

    /// <summary>Прокрутить к началу (смена вкладки).</summary>
    public static void ScrollTop(RectTransform content) => content.anchoredPosition = Vector2.zero;

    /// <summary>Показать полосу [top, top+h] (в единицах контента, вниз от верха) целиком.</summary>
    public static void Reveal(RectTransform content, float top, float h) {
        float view = ((RectTransform)content.parent).sizeDelta.y;
        float y = content.anchoredPosition.y;
        if (top < y) y = top;
        else if (top + h > y + view) y = top + h - view;
        content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, content.sizeDelta.y - view)));
    }

    /// <summary>Картина из AiArt: корень или подпапка Guardians (портреты стражей v0.15 лежат там).</summary>
    public static Texture2D LoadArt(string key) {
        if (string.IsNullOrEmpty(key)) return null;
        var tex = Resources.Load<Texture2D>("AiArt/" + key);
        if (tex == null) tex = Resources.Load<Texture2D>("AiArt/Guardians/" + key);
        return tex;
    }
}
}
