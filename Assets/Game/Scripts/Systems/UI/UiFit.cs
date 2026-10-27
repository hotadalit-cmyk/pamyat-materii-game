using UnityEngine;

namespace PM.Systems.UI {
/// <summary>
/// v0.15: масштаб крупных модальных окон под видимую высоту канвы.
/// CanvasScaler «1920×1080 по ширине» (match=1): на экране 2400×1080 видимая высота
/// канвы всего 864 ед. — окна 930–1060 обрезались краями экрана и за safe area.
/// UiFit.Apply равномерно сжимает бокс (children масштабируются вместе — налезаний нет).
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
}
}
