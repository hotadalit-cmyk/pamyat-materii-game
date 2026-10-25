using UnityEngine;
using UnityEngine.UI;
using PM.Systems.Bootstrap;

namespace PM.Systems.UI {
/// <summary>Экран смерти без «Game Over»: отпечаток жизни + кнопка реинкарнации (VISUAL_UI).</summary>
public sealed class DeathScreenUI : MonoBehaviour {
    public static DeathScreenUI Instance { get; private set; }
    public GameObject Panel;
    public Text SummaryText;
    public Button ReincarnateButton;
    private GameBootstrap _game;
    private bool _wasDead;

    void Awake() { Instance = this; if (Panel != null) Panel.SetActive(false); }
    void Start() { _game = GameBootstrap.Instance; }

    void Update() {
        if (_game == null || _game.Sim == null) return;
        bool dead = !_game.Sim.Player.Alive;
        if (dead && !_wasDead) Show();
        if (!dead && _wasDead) Hide();
        _wasDead = dead;
    }

    public void Show() {
        if (Panel == null) return;
        var sim = _game.Sim;
        SummaryText.text = string.Format(
            "Отпечаток жизни #{0}\nПоглощено пищи и добычи — см. историю мира.\n" +
            "Качества души сохраняются:\nЭнергия {1:0.0} · Выносливость {2:0.0} · Магия {3:0.0}\n" +
            "Известных генов: {4}",
            sim.Soul.LivesLived + 1,
            sim.Soul.EnergyPotential, sim.Soul.Endurance, sim.Soul.MagicReserve,
            sim.Soul.KnowledgeGenes.Count);
        Panel.SetActive(true);
    }

    public void Hide() { if (Panel != null) Panel.SetActive(false); }

    public void OnReincarnate() {
        if (_game != null) _game.Reincarnate();
        Hide();
    }
}
}
