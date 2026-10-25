using UnityEngine;
using UnityEngine.UI;
using PM.Systems.Bootstrap;

namespace PM.Systems.UI {
/// <summary>Главный HUD (VISUAL_UI): целостность, вода/масса, энергия, удержание, пищеварение.</summary>
public sealed class HudPresenter : MonoBehaviour {
    public static HudPresenter Instance { get; private set; }
    public Text IntegrityText;
    public Text WaterText;
    public Text EnergyText;
    public Text CaptureText;
    public Image CaptureFill;
    public Text DigestionText;
    public Text HintText;
    private GameBootstrap _game;
    private float _hintTimer;

    void Awake() { Instance = this; }

    public void Bind(GameBootstrap game) { _game = game; }

    void Update() {
        if (_game == null || _game.Sim == null) return;
        var sim = _game.Sim;
        var p = sim.Player;
        IntegrityText.text = string.Format("Целостность: {0:0}%", p.Vitals.Integrity * 100f);
        WaterText.text = string.Format("Вода: {0:0.00} кг | Всего: {1:0.00} кг", p.Body.Water, p.Body.Total);
        EnergyText.text = string.Format("Духовная энергия: {0:0.0}/{1:0.0}", sim.Soul.CurrentEnergy, sim.Soul.EnergyPotential);
        // удержание
        var target = sim.CaptureTarget;
        if (sim.Capture.Active && target != null) {
            CaptureText.gameObject.SetActive(true);
            CaptureFill.gameObject.SetActive(true);
            CaptureFill.fillAmount = (float)sim.Capture.Progress;
            CaptureText.text = string.Format("Удержание добычи: {0:0}%", sim.Capture.Progress * 100);
        } else if (target != null) {
            CaptureText.gameObject.SetActive(true);
            CaptureFill.gameObject.SetActive(false);
            CaptureText.text = "Жук ослаблен — держитесь рядом, чтобы захватить";
        } else {
            CaptureText.gameObject.SetActive(false);
            CaptureFill.gameObject.SetActive(false);
        }
        // пищеварение
        if (sim.Chamber.Items.Count > 0) {
            var it = sim.Chamber.Items[0];
            DigestionText.gameObject.SetActive(true);
            DigestionText.text = string.Format("Пищеварение: {0:0.0} с", it.RemainingSec);
        } else {
            DigestionText.gameObject.SetActive(false);
        }
        // контекстные подсказки
        if (_hintTimer > 0) _hintTimer -= Time.deltaTime;
        else if (HintText != null) HintText.gameObject.SetActive(false);
    }

    public void ShowHint(string message, float seconds = 4f) {
        if (HintText == null) return;
        HintText.gameObject.SetActive(true);
        HintText.text = message;
        _hintTimer = seconds;
    }
    public void ShowDeath() {
        if (DeathScreenUI.Instance != null) DeathScreenUI.Instance.Show();
    }
    public void HideDeath() {
        if (DeathScreenUI.Instance != null) DeathScreenUI.Instance.Hide();
    }
}
}
