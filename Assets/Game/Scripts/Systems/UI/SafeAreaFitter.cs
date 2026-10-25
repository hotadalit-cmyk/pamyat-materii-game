using UnityEngine;

namespace PM.Systems.UI {
/// <summary>
/// Safe area для landscape-телефонов (T01): чистая математика 0..1, защита от NaN
/// (некоторые устройства сообщают нули до первого кадра).
/// </summary>
public sealed class SafeAreaFitter : MonoBehaviour {
    private Rect _applied;

    void OnRectTransformDimensionsChange() => Apply();
    void Start() => Apply();

    private void Apply() {
        var rt = transform as RectTransform;
        if (rt == null) return;
        var sa = Screen.safeArea;
        if (sa.width <= 0 || sa.height <= 0 || Screen.width <= 0 || Screen.height <= 0) return;
        var anchors = new Vector4(
            sa.x / Screen.width,
            sa.y / Screen.height,
            (sa.x + sa.width) / Screen.width,
            (sa.y + sa.height) / Screen.height);
        if (_applied == new Rect(anchors.x, anchors.y, anchors.z, anchors.w)) return;
        _applied = new Rect(anchors.x, anchors.y, anchors.z, anchors.w);
        rt.anchorMin = new Vector2(anchors.x, anchors.y);
        rt.anchorMax = new Vector2(anchors.z, anchors.w);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
}
