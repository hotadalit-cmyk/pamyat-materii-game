using UnityEngine;
using PM.Domain.Run;
using PM.Systems.Gameplay;

namespace PM.Systems.Run {
/// <summary>
/// Существо комнаты (v0.4): вид, масса, «мозг». Масса задаёт и физическую массу тела, и размер
/// (SizeRule.RadiusForMass) — правило «меньше тебя — съешь, больше — берегись» читается на глаз.
/// </summary>
public sealed class RunCreature : MonoBehaviour {
    public SpeciesDef Species;
    public double Mass;
    public bool IsNemesis;
    public string SoulName;
    public CreatureBrain Brain;
    public readonly MutationSet Mutations = new MutationSet();
    public SimEntityView2D View;
    public SpeciesTraits Traits;      // броня, деление, пиявка, свечение (v0.5)
    public bool IsBoss;               // Страж глубины: масса и фазы — в RunState.Boss
    public SpriteRenderer EchoDot;    // метка эхолокации поверх тьмы (m_echo)
    public MonsterModel Model;        // 3D-модель (Quaternius, CC0), если есть
    public SpriteRenderer Ring;       // кольцо: зелёное — можно съесть, красное — опасен
    public bool IsFood => Species == null || Species.Behavior == CreatureBehavior.Food;
    /// <summary>Источник урона для «Отпечатка жизни»: имя души или id вида.</summary>
    public string SourceId => IsNemesis || !string.IsNullOrEmpty(SoulName) ? SoulName : Species?.Id;

    private float _baseRadius = 1f;

    public void Init(SpeciesDef species, double mass, CreatureBrain brain, SimEntityView2D view,
                     float baseRadius) {
        Species = species;
        Mass = mass;
        Brain = brain;
        View = view;
        _baseRadius = Mathf.Max(0.01f, baseRadius);
        ApplySize();
    }

    /// <summary>Масштаб корня и масса тела — от текущей массы.</summary>
    public void ApplySize() {
        float r = (float)SizeRule.RadiusForMass(Mass);
        float s = r / _baseRadius;
        transform.localScale = new Vector3(s, s, 1f);
        if (View != null && View.Rb != null) View.Rb.mass = Mathf.Max(0.05f, (float)Mass);
    }

    public float Radius => (float)SizeRule.RadiusForMass(Mass);
}
}
