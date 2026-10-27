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
    public SpawnVariant Variant;      // v0.10: мерцание — даёт ×1.5 ресурсов при поглощении
    public bool IsShimmering => Variant == SpawnVariant.Shimmering;
    public SpriteRenderer Sparkle;    // сияние мерцающего существа — видно и в темноте
    public bool IsFood => Species == null || Species.Behavior == CreatureBehavior.Food;
    /// <summary>Источник урона для «Отпечатка жизни»: имя души или id вида.</summary>
    public string SourceId => IsNemesis || !string.IsNullOrEmpty(SoulName) ? SoulName : Species?.Id;
    // v0.13: шкала HP — масса при спавне; растёт, если существо ест/растёт (максимум в ApplySize)
    public double MaxMass;
    /// <summary>Дуга-шкала HP снаружи кольца угрозы (создаётся лениво в RunGame).</summary>
    public SpriteRenderer HpArc;
    /// <summary>Радиус тела при спавне — для локальных масштабов дочерних спрайтов.</summary>
    public float BaseRadius => _baseRadius;

    private float _baseRadius = 1f;
    private double _spawnMass;        // v0.14: масса спавна — база потолка роста

    public void Init(SpeciesDef species, double mass, CreatureBrain brain, SimEntityView2D view,
                     float baseRadius) {
        Species = species;
        Mass = mass;
        Brain = brain;
        View = view;
        _baseRadius = Mathf.Max(0.01f, baseRadius);
        if (_spawnMass <= 0.0) _spawnMass = mass; // фиксируем один раз
        MaxMass = System.Math.Max(MaxMass, mass);
        ApplySize();
    }

    /// <summary>
    /// Масштаб корня и масса тела — от текущей массы. Попутно ведёт MaxMass (существо подросло).
    /// v0.14: потолок роста — не больше SizeRule.CreatureGrowthCap (×1.5) от массы спавна:
    /// пиявка и проглоченные фрагменты не раздувают существо бесконечно.
    /// </summary>
    public void ApplySize() {
        double spawnBase = _spawnMass > 0.0 ? _spawnMass : MaxMass;
        double cap = spawnBase * SizeRule.CreatureGrowthCap;
        if (Mass > cap) Mass = cap; // не выше потолка от массы спавна
        if (Mass > MaxMass) MaxMass = Mass;
        float r = (float)SizeRule.RadiusForMass(Mass);
        float s = r / _baseRadius;
        transform.localScale = new Vector3(s, s, 1f);
        if (View != null && View.Rb != null) View.Rb.mass = Mathf.Max(0.05f, (float)Mass);
    }

    public float Radius => (float)SizeRule.RadiusForMass(Mass);
}
}
