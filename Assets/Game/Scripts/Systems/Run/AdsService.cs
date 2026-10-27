using UnityEngine;
using UnityEngine.UI;
namespace PM.Systems.Run {
/// <summary>
/// v0.13: настройка монетизации. Полный выключатель рекламы с вознаграждением.
/// Рекламных SDK, сети и платёжных библиотек в проекте нет и не будет — только этот контракт;
/// реальную реализацию (Unity Ads / AdMob / Яндекс) владелец подставит в AdsService.Current сам.
/// </summary>
public static class AdsConfig {
    public static bool RewardedEnabled = true;
}

/// <summary>Контекст «реклама за вознаграждение»: доступность и показ с одним обратным вызовом.</summary>
public interface IRewardedAds {
    bool IsAvailable { get; }
    /// <summary>Показать ролик: onDone(true) — просмотр засчитан, onDone(false) — отмена или ошибка.</summary>
    void Show(System.Action<bool> onDone);
}

/// <summary>Точка доступа к рекламе. По умолчанию — заглушка без сети.</summary>
public static class AdsService {
    public static IRewardedAds Current { get; set; } = new StubRewardedAds();
}

/// <summary>
/// Заглушка: полноэкранная панель «Здесь будет реклама» с таймером 5 с; после таймера кнопка
/// «Закрыть» → onDone(true). «Отмена» → onDone(false). Без сети, без SDK, поверх всего интерфейса.
/// </summary>
public sealed class StubRewardedAds : IRewardedAds {
    public bool IsAvailable => AdsConfig.RewardedEnabled;

    public void Show(System.Action<bool> onDone) {
        if (!IsAvailable) { onDone?.Invoke(false); return; }
        var go = new GameObject("stub_rewarded_ad");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<StubAdPanel>().Open(onDone);
    }
}

/// <summary>Панель заглушки: строится кодом, живёт на своём объекте, закрывается сама.</summary>
public sealed class StubAdPanel : MonoBehaviour {
    private const float Duration = 5f;
    private static readonly Color Dim = new Color(0f, 0f, 0.02f, 0.9f);
    private static readonly Color PanelC = new Color(0.02f, 0.06f, 0.11f, 0.98f);
    private static readonly Color TextC = new Color(0.92f, 0.96f, 1f);
    private static readonly Color Accent = new Color(0.35f, 0.75f, 1f, 0.95f);

    private System.Action<bool> _done;
    private Text _timer;
    private GameObject _closeBtn;
    private float _t;
    private bool _finished;

    public void Open(System.Action<bool> onDone) {
        _done = onDone;
        var canvasGo = new GameObject("canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;                       // поверх интерфейса забега
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var dim = Img(canvasGo.transform, "dim", Dim);
        FillParent(dim.rectTransform);
        var box = Img(canvasGo.transform, "box", PanelC);
        var rt = box.rectTransform;
        rt.sizeDelta = new Vector2(1100, 640);
        Text(box.transform, "Здесь будет реклама", 52, new Vector2(0, 180), new Vector2(1000, 80)).color = Accent;
        Text(box.transform,
            "Заглушка rewarded-рекламы: SDK подключит владелец игры.\nПросмотр засчитывается через 5 секунд.", 28,
            new Vector2(0, 60), new Vector2(1000, 120));
        _timer = Text(box.transform, "Закрыть через " + ((int)Duration) + "…", 34,
            new Vector2(0, -80), new Vector2(1000, 60));
        _closeBtn = Btn(box.transform, "Закрыть", new Vector2(0, -200), new Vector2(360, 96),
            new Color(0.4f, 1f, 0.55f, 0.95f), () => Finish(true));
        _closeBtn.SetActive(false);
        Btn(box.transform, "Отмена", new Vector2(0, -310), new Vector2(280, 84),
            new Color(0.75f, 0.35f, 0.3f, 0.9f), () => Finish(false));
    }

    private void Update() {
        if (_finished) return;
        _t += Time.unscaledDeltaTime;
        int left = Mathf.CeilToInt(Duration - _t);
        if (left > 0) {
            _timer.text = "Закрыть через " + left + "…";
        } else {
            _timer.text = "Спасибо за просмотр!";
            if (_closeBtn != null) _closeBtn.SetActive(true);
        }
    }

    private void Finish(bool ok) {
        if (_finished) return;
        _finished = true;
        var cb = _done;
        _done = null;
        Destroy(gameObject);
        cb?.Invoke(ok);
    }

    private void OnDestroy() {
        if (_finished) return;
        _finished = true;
        var cb = _done;
        _done = null;
        cb?.Invoke(false);   // панель разрушена извне (сцена/выход) — считаем отменой
    }

    // ---------- мини-строители (RunUi свои не отдаёт) ----------
    private static Image Img(Transform parent, string name, Color c) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    private static void FillParent(RectTransform rt) {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-200, -200); rt.offsetMax = new Vector2(200, 200);
    }

    private static Text Text(Transform parent, string s, int size, Vector2 pos, Vector2 box) {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = s;
        t.fontSize = size;
        t.color = TextC;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        return t;
    }

    private static GameObject Btn(Transform parent, string label, Vector2 pos, Vector2 size, Color c, System.Action onPress) {
        var go = new GameObject("btn_" + label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = c;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() => onPress?.Invoke());
        var t = Text(go.transform, label, 30, Vector2.zero, size - new Vector2(16, 8));
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        return go;
    }
}
}
