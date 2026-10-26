using System.Collections.Generic;
namespace PM.Domain.Run {

/// <summary>Поведение существа при контакте с игроком (RUN_SPEC).</summary>
public enum CreatureBehavior { Drift = 0, Flee = 1, Chase = 2, Charger = 3, Food = 4 }

/// <summary>Описание вида существа. Масса на спавне = BaseMass × глубинный множитель.</summary>
public sealed class SpeciesDef {
    public string Id;
    public string Name;            // русское имя для UI
    public double BaseMass;        // типичная масса
    public CreatureBehavior Behavior;
    public string GeneMutationId;  // мутация, которую даёт вид (может быть null)
    public int MinDepth, MaxDepth; // глубины появления
    public int ColorIndex;         // 0 еда, 1 добыча, 2 опасность, 3 душа, 4 кристалл
}

/// <summary>Каталог видов забега. Детерминирован, LINQ не используется.</summary>
public static class SpeciesCatalog {
    private static readonly SpeciesDef[] AllDefs = new SpeciesDef[] {
        new SpeciesDef { Id = "spore",         Name = "Спора",      BaseMass = 0.3, Behavior = CreatureBehavior.Food,    GeneMutationId = null,        MinDepth = 1, MaxDepth = 5, ColorIndex = 0 },
        new SpeciesDef { Id = "mite",          Name = "Клещ",       BaseMass = 0.8, Behavior = CreatureBehavior.Flee,    GeneMutationId = "m_light",   MinDepth = 1, MaxDepth = 3, ColorIndex = 1 },
        new SpeciesDef { Id = "beetle",        Name = "Жук",        BaseMass = 2.5, Behavior = CreatureBehavior.Drift,   GeneMutationId = "m_shell",   MinDepth = 1, MaxDepth = 4, ColorIndex = 1 },
        new SpeciesDef { Id = "glowworm",      Name = "Светлячок",  BaseMass = 1.5, Behavior = CreatureBehavior.Flee,    GeneMutationId = "m_eyes",    MinDepth = 2, MaxDepth = 5, ColorIndex = 1 },
        new SpeciesDef { Id = "spikeback",     Name = "Шипастик",   BaseMass = 4.0, Behavior = CreatureBehavior.Charger, GeneMutationId = "m_spikes",  MinDepth = 2, MaxDepth = 5, ColorIndex = 2 },
        new SpeciesDef { Id = "jelly",         Name = "Медуза",     BaseMass = 3.0, Behavior = CreatureBehavior.Drift,   GeneMutationId = "m_sticky",  MinDepth = 3, MaxDepth = 5, ColorIndex = 1 },
        new SpeciesDef { Id = "crystal_mite",  Name = "Кристоклещ", BaseMass = 6.0, Behavior = CreatureBehavior.Chase,   GeneMutationId = "m_crystal", MinDepth = 4, MaxDepth = 5, ColorIndex = 4 },
        new SpeciesDef { Id = "soul_slime",    Name = "Душеслизень",BaseMass = 4.0, Behavior = CreatureBehavior.Chase,   GeneMutationId = "m_hunger",  MinDepth = 1, MaxDepth = 5, ColorIndex = 3 },
        // v0.5: виды биомов (MinDepth/MaxDepth = глубина биома)
        new SpeciesDef { Id = "ember_beetle",  Name = "Углежук",        BaseMass = 3.5,  Behavior = CreatureBehavior.Charger, GeneMutationId = "m_heatproof", MinDepth = 2, MaxDepth = 2, ColorIndex = 2 },
        new SpeciesDef { Id = "salamander",    Name = "Саламандра",     BaseMass = 3.0,  Behavior = CreatureBehavior.Chase,   GeneMutationId = "m_vampire",  MinDepth = 2, MaxDepth = 2, ColorIndex = 2 },
        new SpeciesDef { Id = "leech",         Name = "Пиявка",         BaseMass = 1.2,  Behavior = CreatureBehavior.Chase,   GeneMutationId = null,         MinDepth = 3, MaxDepth = 3, ColorIndex = 2 },
        new SpeciesDef { Id = "bubble_jelly",  Name = "Пузыремедуза",   BaseMass = 3.5,  Behavior = CreatureBehavior.Drift,   GeneMutationId = "m_echo",     MinDepth = 3, MaxDepth = 3, ColorIndex = 1 },
        new SpeciesDef { Id = "shard_crab",    Name = "Осколочный краб",BaseMass = 5.0,  Behavior = CreatureBehavior.Drift,   GeneMutationId = "m_ricochet", MinDepth = 4, MaxDepth = 4, ColorIndex = 4 },
        new SpeciesDef { Id = "wisp",          Name = "Огонёк",         BaseMass = 2.0,  Behavior = CreatureBehavior.Flee,    GeneMutationId = "m_echo",     MinDepth = 5, MaxDepth = 5, ColorIndex = 1 },
        new SpeciesDef { Id = "warden",        Name = "Страж глубины",  BaseMass = 30.0, Behavior = CreatureBehavior.Charger, GeneMutationId = null,         MinDepth = 5, MaxDepth = 5, ColorIndex = 2 },
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
