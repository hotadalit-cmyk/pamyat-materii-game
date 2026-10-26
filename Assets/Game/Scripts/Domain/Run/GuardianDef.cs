namespace PM.Domain.Run {

/// <summary>
/// Тип поведения стража (v0.11, этап 1): Спороносица и Пульсар — полноценные механики;
/// Г2–Г4 временно используют поведение Пульсара со своими именем/цветом/HP.
/// </summary>
public enum GuardianKind { SporeMother = 0, Pulsar = 1 }

/// <summary>Описание стража глубины: имя, HP-множитель, арена, пороги фаз, параметры атак.</summary>
public sealed class GuardianDef {
    public string Id;
    public string Name;
    public int Depth;
    public string SpeciesId;       // вид тела для Unity/энциклопедии (этап 1 — "warden" у всех)
    public uint Tint;              // 0xRRGGBB — цвет свечения/подписи в Unity
    public GuardianKind Kind;
    public double HpMult;          // HP стража = max(8, GoalMass × HpMult)
    public double ArenaRadius;     // радиус арены боя у шахты
    public double WakeRadius;      // дальше — спит (таймеры стоят)
    public double PhaseThreshold2; // доля HP, ниже которой — фаза 2 (Спороносица: 0.66)
    public double PhaseThreshold3; // доля HP, ниже которой — фаза 3 (Спороносица: 0.33)
    // зоны угрозы (Спороносица)
    public int ZoneCount;
    public double ZoneRadius, ZoneDamagePerSec, ZoneSlow, ZoneShuffleEvery, ZoneDriftSpeed;
    // миньоны
    public string MinionSpecies;
    public double MinionMass;
    public int MinionsPerWave;
    public double MinionEvery1;   // интервал волн в фазах 1–2 (Спороносица: 12 с)
    public double MinionEvery3;   // интервал в фазе 3 — вдвое чаще (Спороносица: 6 с)
    // удар корней по площади (Спороносица, фаза 2+)
    public double RootEvery2, RootEvery3, RootTelegraph, RootDamageFrac, RootRadius;
    // окно уязвимости (Спороносица, фаза 2+)
    public double WeakClosedSec, WeakOpenSec, WeakDamageMult, ClosedCapMult;
}

/// <summary>Каталог пяти стражей — по одному на глубину. Детерминирован, без LINQ.</summary>
public static class GuardianCatalog {
    private static readonly GuardianDef[] AllDefs = new GuardianDef[] {
        new GuardianDef {
            Id = "spore_mother", Name = "Спороносица", Depth = 1, SpeciesId = "warden", Tint = 0x7CE07C,
            Kind = GuardianKind.SporeMother, HpMult = 1.3, ArenaRadius = 9.0, WakeRadius = 7.0,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            ZoneCount = 4, ZoneRadius = 2.5, ZoneDamagePerSec = 0.4, ZoneSlow = 0.30,
            ZoneShuffleEvery = 8.0, ZoneDriftSpeed = 1.0,
            MinionSpecies = "mite", MinionMass = 1.5, MinionsPerWave = 2, MinionEvery1 = 12.0, MinionEvery3 = 6.0,
            RootEvery2 = 4.0, RootEvery3 = 2.5, RootTelegraph = 0.8, RootDamageFrac = 0.12, RootRadius = 2.2,
            WeakClosedSec = 6.0, WeakOpenSec = 2.0, WeakDamageMult = 1.5, ClosedCapMult = 0.5,
        },
        // TODO(этап 2): уникальное поведение Горнила — шлаковые лужи, жар, шатуны. Пока — Пульсар.
        new GuardianDef {
            Id = "forge", Name = "Горнило", Depth = 2, SpeciesId = "warden", Tint = 0xE08A4A,
            Kind = GuardianKind.Pulsar, HpMult = 1.35, ArenaRadius = 10.0, WakeRadius = 7.0,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            MinionSpecies = "mite", MinionMass = 1.1, MinionsPerWave = 3, MinionEvery1 = 7.0, MinionEvery3 = 7.0,
        },
        // TODO(этап 2): уникальное поведение Хора Бездны — эхо-волны, поющие зоны. Пока — Пульсар.
        new GuardianDef {
            Id = "abyss_choir", Name = "Хор Бездны", Depth = 3, SpeciesId = "warden", Tint = 0x7A8CE0,
            Kind = GuardianKind.Pulsar, HpMult = 1.4, ArenaRadius = 11.0, WakeRadius = 7.0,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            MinionSpecies = "mite", MinionMass = 1.1, MinionsPerWave = 3, MinionEvery1 = 7.0, MinionEvery3 = 7.0,
        },
        // TODO(этап 2): уникальное поведение Гранильщика — искры, оснастка, круговая пила. Пока — Пульсар.
        new GuardianDef {
            Id = "grinder", Name = "Гранильщик", Depth = 4, SpeciesId = "warden", Tint = 0xC0C8D8,
            Kind = GuardianKind.Pulsar, HpMult = 1.45, ArenaRadius = 12.0, WakeRadius = 7.0,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            MinionSpecies = "mite", MinionMass = 1.1, MinionsPerWave = 3, MinionEvery1 = 7.0, MinionEvery3 = 7.0,
        },
        // Пульсар: поведение старого BossState 1:1 (пороги 0.60/0.45 — RunBossTests зелёные).
        new GuardianDef {
            Id = "pulsar", Name = "Пульсар", Depth = 5, SpeciesId = "warden", Tint = 0xFF6A5A,
            Kind = GuardianKind.Pulsar, HpMult = 1.5, ArenaRadius = 12.0, WakeRadius = 7.0,
            PhaseThreshold2 = 0.60, PhaseThreshold3 = 0.45,
            MinionSpecies = "mite", MinionMass = 1.1, MinionsPerWave = 3, MinionEvery1 = 7.0, MinionEvery3 = 7.0,
        },
    };

    /// <summary>Все стражи. Возвращаемое значение считать read-only.</summary>
    public static GuardianDef[] All() => AllDefs;

    /// <summary>Страж глубины; глубина зажимается в диапазон 1..5.</summary>
    public static GuardianDef ForDepth(int depth) {
        if (depth < 1) depth = 1;
        if (depth > AllDefs.Length) depth = AllDefs.Length;
        return AllDefs[depth - 1];
    }
}
}
