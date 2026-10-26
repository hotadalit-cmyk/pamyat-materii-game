namespace PM.Domain.Run {

/// <summary>Биом пещеры: по одному на каждую глубину 1..5.</summary>
public enum BiomeId { Fungal = 1, Warm = 2, Ponds = 3, Crystal = 4, Heart = 5 }

/// <summary>
/// Описание биома: палитра (0xRRGGBB, тёмные стены и яркий акцент), тьма,
/// испарение массы в секунду, пулы видов и препятствий.
/// </summary>
public sealed class BiomeDef {
    public BiomeId Id;
    public int Depth;
    public string Name, Subtitle;                              // «Грибной коридор», «Здесь всё начинается…»
    public uint FloorColor, WallColor, AccentColor, GlowColor; // 0xRRGGBB
    public double Darkness01;                                  // 0.55 → 0.9 по глубине
    public double EvaporationPerSec;                           // доля массы, испаряющаяся за секунду
    public string[] SpeciesPool;                               // id видов биома (spore — всегда)
    public ObstacleKind[] ObstaclePool;
}

/// <summary>Каталог биомов по глубинам. Детерминирован, без LINQ.</summary>
public static class BiomeCatalog {
    private static readonly BiomeDef[] AllDefs = new BiomeDef[] {
        new BiomeDef {
            Id = BiomeId.Fungal, Depth = 1,
            Name = "Грибной коридор", Subtitle = "Здесь всё начинается…",
            FloorColor = 0x3E3550, WallColor = 0x241E33, AccentColor = 0xC39BE8, GlowColor = 0xEAD9FF,
            Darkness01 = 0.55, EvaporationPerSec = 0.0,
            SpeciesPool = new string[] { "spore", "mite", "beetle", "soul_slime" },
            ObstaclePool = new ObstacleKind[] { ObstacleKind.Pillar, ObstacleKind.Mushroom },
        },
        new BiomeDef {
            Id = BiomeId.Warm, Depth = 2,
            Name = "Тёплые пустоты", Subtitle = "Тело испаряется — не задерживайся",
            FloorColor = 0x54301C, WallColor = 0x301A0D, AccentColor = 0xFF9A4D, GlowColor = 0xFFD9A8,
            Darkness01 = 0.6375, EvaporationPerSec = 0.004,
            SpeciesPool = new string[] { "spore", "mite", "beetle", "glowworm", "spikeback", "soul_slime", "ember_beetle", "salamander" },
            ObstaclePool = new ObstacleKind[] { ObstacleKind.Pillar, ObstacleKind.Vent, ObstacleKind.Thorns },
        },
        new BiomeDef {
            Id = BiomeId.Ponds, Depth = 3,
            Name = "Затопленные залы", Subtitle = "Вода помнит всех, кто здесь тонул",
            FloorColor = 0x1C4646, WallColor = 0x0E2B2E, AccentColor = 0x5CE0C8, GlowColor = 0xAAF2E3,
            Darkness01 = 0.725, EvaporationPerSec = 0.0,
            SpeciesPool = new string[] { "spore", "mite", "beetle", "glowworm", "spikeback", "jelly", "soul_slime", "leech", "bubble_jelly" },
            ObstaclePool = new ObstacleKind[] { ObstacleKind.Pillar, ObstacleKind.Pool, ObstacleKind.Sticky },
        },
        new BiomeDef {
            Id = BiomeId.Crystal, Depth = 4,
            Name = "Кристальные россыпи", Subtitle = "Осколки поют на частоте боли",
            FloorColor = 0x2B3A5E, WallColor = 0x161F3A, AccentColor = 0x7FB4FF, GlowColor = 0xC9E2FF,
            Darkness01 = 0.8125, EvaporationPerSec = 0.002,
            SpeciesPool = new string[] { "spore", "beetle", "glowworm", "spikeback", "jelly", "crystal_mite", "soul_slime", "shard_crab" },
            ObstaclePool = new ObstacleKind[] { ObstacleKind.Crystal, ObstacleKind.Thorns, ObstacleKind.Pillar },
        },
        new BiomeDef {
            Id = BiomeId.Heart, Depth = 5,
            Name = "Сердце пещеры", Subtitle = "Страж ждёт внизу. Он помнит тебя",
            FloorColor = 0x4A1D2B, WallColor = 0x2A0E17, AccentColor = 0xFF5C7A, GlowColor = 0xFFC2CE,
            Darkness01 = 0.9, EvaporationPerSec = 0.003,
            SpeciesPool = new string[] { "spore", "glowworm", "spikeback", "jelly", "crystal_mite", "soul_slime", "wisp", "warden" },
            ObstaclePool = new ObstacleKind[] { ObstacleKind.Pillar, ObstacleKind.Thorns, ObstacleKind.Vent, ObstacleKind.Pool },
        },
    };

    /// <summary>Все биомы по глубинам 1..5. Возвращаемое значение считать read-only.</summary>
    public static BiomeDef[] All() => AllDefs;

    /// <summary>Биом глубины (1..5; значения вне диапазона сжимаются к краям).</summary>
    public static BiomeDef ForDepth(int depth) {
        if (depth < 1) depth = 1;
        if (depth > AllDefs.Length) depth = AllDefs.Length;
        return AllDefs[depth - 1];
    }
}
}
