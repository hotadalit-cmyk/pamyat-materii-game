using UnityEngine;
using UnityEngine.UI;
using PM.Systems.Bootstrap;
namespace PM.Systems.UI {
/// <summary>
/// Финальный экран демо (критерий релиза «очевидное завершение»): показывается
/// один раз после первой реинкарнации. Wire() вместо Start: панель неактивна
/// до события, слушатели должны быть готовы заранее.
/// </summary>
public sealed class DemoEndUI : MonoBehaviour {
    public static DemoEndUI Instance { get; private set; }
    public GameObject Panel;
    public Text BodyText;
    public Button ContinueButton;
    private GameBootstrap _game;
    private bool _wired;
    private bool _shownOnce;

    void Awake() { Instance = this; }

    private GameBootstrap Game {
        get { if (_game == null) _game = GameBootstrap.Instance; return _game; }
    }

    public void Wire() {
        Instance = this;
        if (_wired) return;
        _wired = true;
        if (ContinueButton != null) ContinueButton.onClick.AddListener(Hide);
    }

    void Update() {
        if (_shownOnce || Game == null || Game.Sim == null || Panel == null) return;
        if (Game.Sim.Soul.LivesLived >= 1 && Game.Sim.Player.Alive) {
            _shownOnce = true;
            Show();
        }
    }

    public void Show() {
        if (Panel == null) return;
        var sim = Game != null ? Game.Sim : null;
        if (sim == null) return;
        if (BodyText != null) {
            BodyText.text = string.Format(
                "Круг замкнулся: тело умерло, но душа сохранила знания и силу.\n" +
                "Жизней прожито: {0} · Генов в знании: {1}\n\n" +
                "Это конец демо-среза «Память Материи». В полной игре — новые регионы, формы и магия душ.\n\n" +
                "Спасибо за игру! Расскажите о впечатлениях: ссылка на отзыв и поддержку появится на странице релиза.",
                sim.Soul.LivesLived, sim.Soul.KnowledgeGenes.Count);
        }
        Panel.SetActive(true);
    }

    public void Hide() { if (Panel != null) Panel.SetActive(false); }
}
}
