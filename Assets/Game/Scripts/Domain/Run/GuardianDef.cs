namespace PM.Domain.Run {

/// <summary>
/// Тип поведения стража (v0.12, этап 2): у каждого из пяти — свои механики.
/// Пульсар — поведение старого BossState 1:1 (этап 1), Пульсар и Спороносица не меняются.
/// </summary>
public enum GuardianKind { SporeMother = 0, Pulsar = 1, Forge = 2, AbyssChoir = 3, Grinder = 4 }

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
    // окно уязвимости (Спороносица, фаза 2+; Хор — ядро после импульса)
    public double WeakClosedSec, WeakOpenSec, WeakDamageMult, ClosedCapMult;
    // ---------- Горнило (Г2): гейзеры, деление на капли, кипящее кольцо ----------
    public double GeyserEvery1, GeyserEvery3; // интервал волн гейзеров (5 с / 4 с)
    public int GeyserCount;                   // кругов за волну (3)
    public double GeyserRadius;               // радиус круга (1.6)
    public double GeyserTelegraph;            // предупреждение (1.0 с)
    public double GeyserDamageFrac;           // доля массы героя при попадании (0.08)
    public double GeyserKnockback;            // скорость отброса (8)
    public double PartCrawlSpeed;             // скорость капли к герою (1.2)
    public double PartMergeEvery;             // непрерывной близости до слияния (3 с)
    public double PartMergeBonus;             // доля меньшей капли сверх суммы (0.30)
    public double PartGap;                    // запас к радиусам в условии слияния (0.3)
    public double RingShrinkEvery;            // кольцо сжимается каждые 6 с
    public double RingShrinkStep;             // на 10% радиуса
    public double RingMinFrac;                // но не ниже 40%
    public double RingDamagePerSec;           // вне кольца герой теряет 1.0 массы/с
    // ---------- Хор Бездны (Г3): ток, оглушение, воронка ----------
    public double CurrentForce;               // ускорение течения (2.0 ед/с²)
    public double CurrentChangeEvery;         // смена направления каждые 6 с
    public double PulseEvery1, PulseEvery3;   // импульсы: 5 с (Ф1–2) / 3.5 с (Ф3)
    public double PulseRadius;                // кольцо импульса вокруг стража (4)
    public double PulseTelegraph;             // предупреждение (0.6 с)
    public double PulseDamageFrac;            // доля массы героя внутри кольца (0.08)
    public double DashLockSec;                // рывок невозможен 3 с после импульса
    public double CoreOpenSec;                // ядро открыто 4 с после импульса
    public double PullForce;                  // воронка Ф3: 2.5 ед/с² к стражу
    // ---------- Гранильщик (Г4): осколки, луч, зеркало ----------
    public int ShardCount;                    // осколков на орбите (4)
    public double ShardOrbitExtra;            // орбита = радиус стража + 1.2
    public double ShardOrbitPeriod;           // оборот за 6 с
    public double ShardRadius;                // допуск блока — радиус осколка (0.5)
    public double BlockMassFrac;              // блок: −5% массы герою (применяет Unity)
    public double BeamEvery;                  // луч каждые 6 с
    public double BeamTelegraph;              // предупреждение (0.9 с)
    public double BeamDamageFrac;             // попадание 10% массы
    public double BeamWidth;                  // ширина луча (0.6)
    public double BeamLengthMult;             // длина = арена × 1.3
    public double CounterDelay;               // контрудар через 0.6 с после рывка
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
        // Горнило (Г2): экономия массы — гейзеры, деление на капли, кипящее кольцо.
        new GuardianDef {
            Id = "forge", Name = "Горнило", Depth = 2, SpeciesId = "warden", Tint = 0xE08A4A,
            Kind = GuardianKind.Forge, HpMult = 1.35, ArenaRadius = 10.0, WakeRadius = 7.0,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            GeyserEvery1 = 5.0, GeyserEvery3 = 4.0, GeyserCount = 3, GeyserRadius = 1.6,
            GeyserTelegraph = 1.0, GeyserDamageFrac = 0.08, GeyserKnockback = 8.0,
            PartCrawlSpeed = 1.2, PartMergeEvery = 3.0, PartMergeBonus = 0.30, PartGap = 0.3,
            RingShrinkEvery = 6.0, RingShrinkStep = 0.10, RingMinFrac = 0.40, RingDamagePerSec = 1.0,
        },
        // Хор Бездны (Г3): экономия рывков и тайминг — ток, оглушение, воронка.
        new GuardianDef {
            Id = "abyss_choir", Name = "Хор Бездны", Depth = 3, SpeciesId = "warden", Tint = 0x7A8CE0,
            Kind = GuardianKind.AbyssChoir, HpMult = 1.4, ArenaRadius = 11.0, WakeRadius = 7.0,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            CurrentForce = 2.0, CurrentChangeEvery = 6.0,
            PulseEvery1 = 5.0, PulseEvery3 = 3.5, PulseRadius = 4.0, PulseTelegraph = 0.6, PulseDamageFrac = 0.08,
            DashLockSec = 3.0, CoreOpenSec = 4.0, PullForce = 2.5,
            WeakDamageMult = 1.5, ClosedCapMult = 0.5,
        },
        // Гранильщик (Г4): точность рывков — осколки-блок, лучи, зеркало-контрудар.
        new GuardianDef {
            Id = "grinder", Name = "Гранильщик", Depth = 4, SpeciesId = "warden", Tint = 0xC0C8D8,
            Kind = GuardianKind.Grinder, HpMult = 1.45, ArenaRadius = 12.0, WakeRadius = 7.0,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            ShardCount = 4, ShardOrbitExtra = 1.2, ShardOrbitPeriod = 6.0, ShardRadius = 0.5, BlockMassFrac = 0.05,
            BeamEvery = 6.0, BeamTelegraph = 0.9, BeamDamageFrac = 0.10, BeamWidth = 0.6, BeamLengthMult = 1.3,
            CounterDelay = 0.6,
            MinionSpecies = "shard_crab", MinionMass = 1.5, MinionsPerWave = 4,
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
