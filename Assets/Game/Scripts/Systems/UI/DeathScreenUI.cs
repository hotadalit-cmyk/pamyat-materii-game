using UnityEngine;
using UnityEngine.UI;
using PM.Systems.Bootstrap;

namespace PM.Systems.UI {
/// <summary>Экран смерти без «Game Over»: отпечаток жизни + кнопка реинкарнации (VISUAL_UI).
/// Wire() вместо Start: панель неактивна до смерти, слушатели должны быть готовы заранее.</summary>
public sealed class DeathScreenUI : MonoBehaviour {
    public static DeathScreenUI Instance { get; private set; }
    public GameObject Panel;
    public Text SummaryText;
    public Button ReincarnateButton;
    private GameBootstrap _game;
    private bool _wired;
    private bool _wasDead;

    void Awake() { Instance = this; }

    private GameBootstrap Game {
        get { if (_game == null) _game = GameBootstrap.Instance; return _game; }
    }

    public void Wire() {
        Instance = this;
        if (_wired) return;
        _wired = true;
        if (ReincarnateButton != null)
            ReincarnateButton.onClick.AddListener(OnReincarnate);
    }

    void Update() {
        if (Game == null || Game.Sim == null) return;
        bool dead = !Game.Sim.Player.Alive;
        if (dead && !_wasDead) Show();
        if (!dead && _wasDead) Hide();
        _wasDead = dead;
    }

    public void Show() {
        if (Panel == null) return;
        var sim = Game != null ? Game.Sim : null;
        if (sim == null) return;
        SummaryText.text = string.Format(
            "Отпечаток жизни #{0}\n" +
            "Гранул усвоено: {1} · Добычи поглощено: {2}\n" +
            "Узлов открыто: {3} · Помощи оказано: {4}\n" +
            "Качества души сохраняются:\nЭнергия {5:0.0} · Выносливость {6:0.0} · Магия {7:0.0}\n" +
            "Известных генов: {8}",
            sim.Soul.LivesLived + 1,
            sim.LifePellets, sim.LifeBeetles, sim.LifeNodes, sim.LifeAssists,
            sim.Soul.EnergyPotential, sim.Soul.Endurance, sim.Soul.MagicReserve,
            sim.Soul.KnowledgeGenes.Count);
        Panel.SetActive(true);
    }

    public void Hide() { if (Panel != null) Panel.SetActive(false); }

    public void OnReincarnate() {
        if (Game != null) Game.Reincarnate();
        Hide();
    }
}
}
