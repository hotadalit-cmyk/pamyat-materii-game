using UnityEngine;
using UnityEngine.UI;
using PM.Systems.Bootstrap;

namespace PM.Systems.UI {
/// <summary>Пауза и сохранения (QA-10): два слота, загрузка, без молча новой кампании.</summary>
public sealed class PauseMenuUI : MonoBehaviour {
    public static PauseMenuUI Instance { get; private set; }
    public GameObject Panel;
    public Button PauseButton;
    public Button ResumeButton;
    public Button SaveSlot0, SaveSlot1, LoadSlot0, LoadSlot1;
    public Button GenesButton;
    public Text StatusText;
    private GameBootstrap _game;

    void Awake() {
        Instance = this;
        if (Panel != null) Panel.SetActive(false);
    }
    void Start() {
        _game = GameBootstrap.Instance;
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

    private void SetPaused(bool paused) {
        Time.timeScale = paused ? 0f : 1f;
        if (Panel != null) Panel.SetActive(paused);
    }

    private void DoSave(int slot) {
        bool ok = _game != null && _game.SaveToSlot(slot);
        if (StatusText != null) StatusText.text = ok ? "Сохранено в слот " + (slot + 1) : "Ошибка сохранения";
    }
    private void DoLoad(int slot) {
        bool ok = _game != null && _game.LoadFromSlot(slot);
        if (StatusText != null)
            StatusText.text = ok ? "Загружено из слота " + (slot + 1)
                : "Слот пуст или повреждён (новая кампания НЕ начата молча)";
    }
}
}
