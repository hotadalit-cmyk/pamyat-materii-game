using UnityEngine;
using UnityEngine.UI;
using PM.Systems.Bootstrap;

namespace PM.Systems.UI {
/// <summary>Пауза и сохранения (QA-10): два слота, загрузка, без молча новой кампании.
/// Слушатели вешаются в Wire() (идемпотентно): в запечённой сцене code-added listeners
/// не выживают пересохранение, а Awake/Start неактивной панели не запускаются.</summary>
public sealed class PauseMenuUI : MonoBehaviour {
    public static PauseMenuUI Instance { get; private set; }
    public GameObject Panel;
    public Button PauseButton;
    public Button ResumeButton;
    public Button SaveSlot0, SaveSlot1, LoadSlot0, LoadSlot1;
    public Button GenesButton;
    public Text StatusText;
    private GameBootstrap _game;
    private bool _wired;

    void Awake() { Instance = this; }

    private GameBootstrap Game {
        get { if (_game == null) _game = GameBootstrap.Instance; return _game; }
    }

    /// <summary>Повторный вызов безопасен: навешивает слушатели кнопок ровно один раз.</summary>
    public void Wire() {
        Instance = this;
        if (_wired) return;
        _wired = true;
        Assign(PauseButton, () => SetPaused(true));
        Assign(ResumeButton, () => SetPaused(false));
        Assign(SaveSlot0, () => DoSave(0));
        Assign(SaveSlot1, () => DoSave(1));
        Assign(LoadSlot0, () => DoLoad(0));
        Assign(LoadSlot1, () => DoLoad(1));
        Assign(GenesButton, () => { if (GeneScreenUI.Instance != null) GeneScreenUI.Instance.Toggle(); });
    }
    private static void Assign(Button b, System.Action a) {
        if (b != null) b.onClick.AddListener(() => a());
    }

    public void SetPaused(bool paused) {
        Time.timeScale = paused ? 0f : 1f;
        if (Panel != null) Panel.SetActive(paused);
    }

    private void DoSave(int slot) {
        bool ok = Game != null && Game.SaveToSlot(slot);
        if (StatusText != null) StatusText.text = ok ? "Сохранено в слот " + (slot + 1) : "Ошибка сохранения";
    }
    private void DoLoad(int slot) {
        bool ok = Game != null && Game.LoadFromSlot(slot);
        if (StatusText != null)
            StatusText.text = ok ? "Загружено из слот " + (slot + 1)
                : "Слот пуст или повреждён (новая кампания НЕ начата молча)";
    }
}
}
