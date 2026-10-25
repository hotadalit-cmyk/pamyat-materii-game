using UnityEngine;
using PM.Systems.Sim;
using PM.Systems.UI;
namespace PM.Systems.Gameplay {
/// <summary>
/// Мост событий мира → подсказки HUD и звук (T18/T19). Сканирует новые WorldEvent
/// по монотонному тику, поэтому события не теряются и не дублируются при
/// переполнении буфера (64 записи). Первый кадр только «заряжается» без показа.
/// </summary>
public sealed class SimEventHub : MonoBehaviour {
    private long _lastTick;
    private bool _primed;

    void LateUpdate() {
        var runner = SimRunner.Instance;
        if (runner == null || runner.Sim == null) return;
        var events = runner.Sim.Events;
        if (!_primed) {
            long max = 0;
            for (int i = 0; i < events.Count; i++) if (events[i].Tick > max) max = events[i].Tick;
            _lastTick = max;
            _primed = true;
            return;
        }
        for (int i = 0; i < events.Count; i++) {
            var e = events[i];
            if (e.Tick <= _lastTick) continue;
            _lastTick = e.Tick;
            Dispatch(e.Kind, e.Detail);
        }
    }

    private static void Dispatch(string kind, string detail) {
        var hud = HudPresenter.Instance;
        var sfx = ProceduralSfx.Instance;
        switch (kind) {
            case "pellet_consumed":
                if (sfx != null) sfx.Play("eat");
                if (hud != null) hud.ShowHint("Пища усвоена. Ослабьте жука точными столкновениями", 5f);
                break;
            case "beetle_weakened":
                if (sfx != null) sfx.Play("weaken");
                if (hud != null) hud.ShowHint("Жук ослаблен — держитесь рядом, чтобы захватить", 5f);
                break;
            case "beetle_internalized":
                if (sfx != null) sfx.Play("capture");
                if (hud != null) hud.ShowHint("Добыча внутри — идёт пищеварение", 4f);
                break;
            case "gene_extracted":
                if (sfx != null) sfx.Play("unlock");
                if (hud != null) hud.ShowHint("Ген извлечён! Откройте ГЕНЫ", 5f);
                break;
            case "node_unlocked":
                if (sfx != null) sfx.Play("unlock");
                if (detail == "n_chem_sense" && hud != null)
                    hud.ShowHint("Химическое чутьё активно — следите за зелёной стрелкой", 5f);
                else if (detail == "n_beetle_form" && hud != null)
                    hud.ShowHint("Открыта форма жука — экран ГЕНЫ, раздел «Формы»", 6f);
                break;
            case "metamorphosed":
                if (sfx != null) sfx.Play("capture");
                if (hud != null) hud.ShowHint("Форма применена — органы перестроены", 4f);
                break;
            case "assist_water":
                if (sfx != null) sfx.Play("assist");
                if (hud != null) hud.ShowHint("Союзник (ИИ) поделился с вами водой", 5f);
                break;
            case "player_assisted":
                if (sfx != null) sfx.Play("assist");
                if (hud != null) hud.ShowHint("Союзник запомнил помощь", 4f);
                break;
            case "water_stolen":
                if (sfx != null) sfx.Play("hit");
                if (hud != null) hud.ShowHint("Соперник украл воду — конфликт запомнен", 5f);
                break;
            case "player_death":
                if (sfx != null) sfx.Play("death");
                break;
            case "reincarnated":
                if (sfx != null) sfx.Play("assist");
                break;
        }
    }
}
}
