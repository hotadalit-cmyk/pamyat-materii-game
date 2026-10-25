using UnityEngine;
using UnityEngine.UI;
using PM.Domain.World;
using PM.Systems.Bootstrap;
namespace PM.Systems.UI {
/// <summary>
/// Кнопка «ПОМОЧЬ» (T16, соцразвилка «помочь»): видна, когда ИИ-союзник рядом
/// и у игрока есть запас воды. Нажатие — PlayerAssistAi: вода переходит союзнику,
/// событие попадает в его память (Trust+20), союзник позже приходит на помощь.
/// </summary>
public sealed class HelpAssistButton : MonoBehaviour {
    private GameBootstrap _game;
    private Text _label;
    private bool _wired;

    private GameBootstrap Game {
        get { if (_game == null) _game = GameBootstrap.Instance; return _game; }
    }

    public void Wire() {
        if (_wired) return;
        _wired = true;
        var b = GetComponent<Button>();
        if (b != null) b.onClick.AddListener(OnClick);
    }

    void Update() {
        if (Game == null || Game.Sim == null || Game.Sim.Player == null) {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }
        var sim = Game.Sim;
        bool canSee = false;
        if (sim.Player.Alive && sim.Player.Body.Water > sim.Player.Body.MinimumViableWater + 0.5) {
            for (int i = 0; i < sim.AiActors.Count; i++) {
                var a = sim.AiActors[i];
                var prof = sim.ProfileOf(a.Id);
                if (prof == null || !prof.Ally || !a.Alive) continue;
                if ((a.Pos - sim.Player.Pos).Length <= GameSim.AssistRadius + 1.0) { canSee = true; break; }
            }
        }
        if (gameObject.activeSelf != canSee) gameObject.SetActive(canSee);
        if (canSee && _label == null) _label = GetComponentInChildren<Text>();
    }

    private void OnClick() {
        if (Game == null || Game.Sim == null) return;
        var sim = Game.Sim;
        for (int i = 0; i < sim.AiActors.Count; i++) {
            var a = sim.AiActors[i];
            var prof = sim.ProfileOf(a.Id);
            if (prof == null || !prof.Ally || !a.Alive) continue;
            if (sim.PlayerAssistAi(a.Id) == null) return; // событие покажет хаб
        }
        if (HudPresenter.Instance != null)
            HudPresenter.Instance.ShowHint("Не вышло: подойдите ближе или накопите воду", 3f);
    }
}
}
