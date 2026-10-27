using System.Collections.Generic;
namespace PM.Domain.Run {

/// <summary>Поведение существа при контакте с игроком (RUN_SPEC).</summary>
public enum CreatureBehavior { Drift = 0, Flee = 1, Chase = 2, Charger = 3, Food = 4 }

/// <summary>
/// Описание вида существа. Масса на спавне = MassAtDepth(depth).
/// v0.14 (Docs/BALANCE_v14.md §2.3, §3): Attack — доля массы героя за удар при полной скорости,
/// Toughness — сколько «единиц урона» стоит единица массы (HP существа = масса × Toughness),
/// GrowthRate — темп роста массы по глубинам.
/// </summary>
public sealed class SpeciesDef {
    public string Id;
    public string Name;            // русское имя для UI
    public double BaseMass;        // типичная масса на глубине 1
    public CreatureBehavior Behavior;
    public string GeneMutationId;  // мутация, которую даёт вид (может быть null)
    public int MinDepth, MaxDepth; // глубины появления
    public int ColorIndex;         // 0 еда, 1 добыча, 2 опасность, 3 душа, 4 кристалл
    public double Attack;          // v0.14: базовый урон, доля массы героя за удар (0 — пища)
    public double Toughness;       // v0.14: жёсткость (1.0 по умолчанию)
    public double GrowthRate;      // v0.14: рост массы по глубине: BaseMass × (1 + rate×(d−1))

    /// <summary>Масса вида на глубине depth.</summary>
    public double MassAtDepth(int depth) {
        if (depth < 1) depth = 1;
        return BaseMass * (1.0 + GrowthRate * (depth - 1));
    }
}

/// <summary>Каталог видов забега. Детерминирован, LINQ не используется.</summary>
public static class SpeciesCatalog {
    private static readonly SpeciesDef[] AllDefs = new SpeciesDef[] {
        new SpeciesDef { Id = "spore",         Name = "Спора",      BaseMass = 0.3, Behavior = CreatureBehavior.Food,    GeneMutationId = null,        MinDepth = 1, MaxDepth = 5, ColorIndex = 0 },
        new SpeciesDef { Id = "mite",          Name = "Клещ",       BaseMass = 0.8, Behavior = CreatureBehavior.Flee,    GeneMutationId = "m_light",   MinDepth = 1, MaxDepth = 3, ColorIndex = 1, Attack = 0.05, Toughness = 0.8, GrowthRate = 0.15 },
        new SpeciesDef { Id = "beetle",        Name = "Жук",        BaseMass = 2.5, Behavior = CreatureBehavior.Drift,   GeneMutationId = "m_shell",   MinDepth = 1, MaxDepth = 4, ColorIndex = 1, Attack = 0.08, Toughness = 1.4, GrowthRate = 0.25 },
        new SpeciesDef { Id = "glowworm",      Name = "Светлячок",  BaseMass = 1.5, Behavior = CreatureBehavior.Flee,    GeneMutationId = "m_eyes",    MinDepth = 2, MaxDepth = 5, ColorIndex = 1, Attack = 0.07, Toughness = 0.8, GrowthRate = 0.20 },
        new SpeciesDef { Id = "spikeback",     Name = "Шипастик",   BaseMass = 4.0, Behavior = CreatureBehavior.Charger, GeneMutationId = "m_spikes",  MinDepth = 2, MaxDepth = 5, ColorIndex = 2, Attack = 0.16, Toughness = 1.3, GrowthRate = 0.45 },
        new SpeciesDef { Id = "jelly",         Name = "Медуза",     BaseMass = 3.0, Behavior = CreatureBehavior.Drift,   GeneMutationId = "m_sticky",  MinDepth = 3, MaxDepth = 5, ColorIndex = 1, Attack = 0.10, Toughness = 0.8, GrowthRate = 0.35 },
        new SpeciesDef { Id = "crystal_mite",  Name = "Кристоклещ", BaseMass = 6.0, Behavior = CreatureBehavior.Chase,   GeneMutationId = "m_crystal", MinDepth = 4, MaxDepth = 5, ColorIndex = 4, Attack = 0.14, Toughness = 1.6, GrowthRate = 0.55 },
        new SpeciesDef { Id = "soul_slime",    Name = "Душеслизень",BaseMass = 4.0, Behavior = CreatureBehavior.Chase,   GeneMutationId = "m_hunger",  MinDepth = 1, MaxDepth = 5, ColorIndex = 3, Attack = 0.12, Toughness = 1.0, GrowthRate = 0.75 },
        // v0.5: виды биомов (MinDepth/MaxDepth = глубина биома)
        new SpeciesDef { Id = "ember_beetle",  Name = "Углежук",        BaseMass = 3.5,  Behavior = CreatureBehavior.Charger, GeneMutationId = "m_heatproof", MinDepth = 2, MaxDepth = 2, ColorIndex = 2, Attack = 0.15, Toughness = 1.2, GrowthRate = 0.50 },
        new SpeciesDef { Id = "salamander",    Name = "Саламандра",     BaseMass = 3.0,  Behavior = CreatureBehavior.Chase,   GeneMutationId = "m_vampire",  MinDepth = 2, MaxDepth = 2, ColorIndex = 2, Attack = 0.14, Toughness = 1.1, GrowthRate = 0.45 },
        new SpeciesDef { Id = "leech",         Name = "Пиявка",         BaseMass = 1.2,  Behavior = CreatureBehavior.Chase,   GeneMutationId = null,         MinDepth = 3, MaxDepth = 3, ColorIndex = 2, Attack = 0.10, Toughness = 0.9, GrowthRate = 0.35 },
        new SpeciesDef { Id = "bubble_jelly",  Name = "Пузыремедуза",   BaseMass = 3.5,  Behavior = CreatureBehavior.Drift,   GeneMutationId = "m_echo",     MinDepth = 3, MaxDepth = 3, ColorIndex = 1, Attack = 0.09, Toughness = 0.8, GrowthRate = 0.40 },
        new SpeciesDef { Id = "shard_crab",    Name = "Осколочный краб",BaseMass = 5.0,  Behavior = CreatureBehavior.Drift,   GeneMutationId = "m_ricochet", MinDepth = 4, MaxDepth = 4, ColorIndex = 4, Attack = 0.13, Toughness = 1.8, GrowthRate = 0.50 },
        new SpeciesDef { Id = "wisp",          Name = "Огонёк",         BaseMass = 2.0,  Behavior = CreatureBehavior.Flee,    GeneMutationId = "m_echo",     MinDepth = 5, MaxDepth = 5, ColorIndex = 1, Attack = 0.06, Toughness = 0.8, GrowthRate = 0.30 },
        new SpeciesDef { Id = "warden",        Name = "Страж глубины",  BaseMass = 30.0, Behavior = CreatureBehavior.Charger, GeneMutationId = null,         MinDepth = 5, MaxDepth = 5, ColorIndex = 2, Attack = 0.11, Toughness = 1.2, GrowthRate = 0.0 },
    };

    /// <summary>Все виды. Возвращаемое значение считать read-only.</summary>
    public static SpeciesDef[] All() => AllDefs;

    /// <summary>Вид по id; null, если нет.</summary>
    public static SpeciesDef Get(string id) {
        if (id == null) return null;
        for (int i = 0; i < AllDefs.Length; i++)
            if (AllDefs[i].Id == id) return AllDefs[i];
        return null;
    }
}
}
