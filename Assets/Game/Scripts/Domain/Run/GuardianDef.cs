namespace PM.Domain.Run {

/// <summary>
/// Тип поведения стража (v0.12, этап 2): у каждого из пяти — свои механики.
/// Пульсар — поведение старого BossState 1:1 (этап 1), Пульсар и Спороносица не меняются.
/// </summary>
public enum GuardianKind { SporeMother = 0, Pulsar = 1, Forge = 2, AbyssChoir = 3, Grinder = 4 }

/// <summary>
/// Описание стража глубины: имя, HP-множитель, пороги фаз, параметры атак.
/// v0.14 (Docs/BALANCE_v14.md §4): все радиусы — ДОЛИ радиуса тела стража
/// (BodyR = SizeRule.RadiusForMass(HP)) + запас; конкретные величины считает GuardianState.
/// </summary>
public sealed class GuardianDef {
    public string Id;
    public string Name;
    public int Depth;
    public string SpeciesId;       // вид тела для Unity/энциклопедии (этап 1 — "warden" у всех)
    public string ModelKey;        // v0.15: ключ модели в ThirdPartyArt (gd_*); null — как у вида
    public string Portrait;        // v0.15: Resources-ключ портрета (AiArt/Guardians/*); null — без портрета
    public uint Tint;              // 0xRRGGBB — цвет свечения/подписи в Unity
    public GuardianKind Kind;
    public double HpMult;          // HP стража = max(8, GoalMass × HpMult)
    public double ArenaMult;       // арена = BodyR × ArenaMult + ArenaPad
    public double WakeMult;        // радиус сна = BodyR × WakeMult + WakePad
    public double PhaseThreshold2; // доля HP, ниже которой — фаза 2 (Спороносица: 0.66)
    public double PhaseThreshold3; // доля HP, ниже которой — фаза 3 (Спороносица: 0.33)
    // запас к радиусам, ед. (не масштабируется)
    public const double ArenaPad = 2.0;
    public const double WakePad = 1.0;
    // зоны угрозы (Спороносица)
    public int ZoneCount;
    public double ZoneMult;                   // радиус зоны = BodyR × ZoneMult
    public double ZoneDriftFrac;              // скорость дрейфа = BodyR × ZoneDriftFrac
    public double ZoneDamagePerSec, ZoneSlow, ZoneShuffleEvery;
    // миньоны
    public string MinionSpecies;
    public double MinionMassFrac;             // масса миньона = BodyR² × MinionMassFrac
    public int MinionsPerWave;
    public double MinionEvery1;   // интервал волн в фазах 1–2 (Спороносица: 12 с)
    public double MinionEvery3;   // интервал в фазе 3 — вдвое чаще (Спороносица: 6 с)
    // удар корней по площади (Спороносица, фаза 2+)
    public double RootEvery2, RootEvery3, RootTelegraph, RootDamageFrac, RootMult;
    // окно уязвимости (Спороносица, фаза 2+; Хор — ядро после импульса)
    public double WeakClosedSec, WeakOpenSec, WeakDamageMult, ClosedCapMult;
    // ---------- Горнило (Г2): гейзеры, деление на капли, кипящее кольцо ----------
    public double GeyserEvery1, GeyserEvery3; // интервал волн гейзеров (5 с / 4 с)
    public int GeyserCount;                   // кругов за волну (3)
    public double GeyserMult;                 // радиус круга = BodyR × GeyserMult
    public double GeyserTelegraph;            // предупреждение (1.0 с)
    public double GeyserDamageFrac;           // доля массы героя при попадании (0.08)
    public double GeyserKnockback;            // скорость отброса (8)
    public double PartCrawlFrac;              // скорость капли к герою = BodyR × PartCrawlFrac
    public double PartMergeEvery;             // непрерывной близости до слияния (3 с)
    public double PartMergeBonus;             // доля меньшей капли сверх суммы (0.30)
    public double RingShrinkEvery;            // кольцо сжимается каждые 6 с
    public double RingShrinkStep;             // на 10% радиуса
    public double RingMinFrac;                // но не ниже 40%
    public double RingDamagePerSec;           // v0.14: вне кольца герой теряет ДОЛЮ массы/с (0.012)
    // ---------- Хор Бездны (Г3): ток, оглушение, воронка ----------
    public double CurrentForce;               // ускорение течения (2.0 ед/с²)
    public double CurrentChangeEvery;         // смена направления каждые 6 с
    public double PulseEvery1, PulseEvery3;   // импульсы: 5 с (Ф1–2) / 3.5 с (Ф3)
    public double PulseMult;                  // кольцо импульса = BodyR × PulseMult
    public double PulseTelegraph;             // предупреждение (0.6 с)
    public double PulseDamageFrac;            // доля массы героя внутри кольца (0.08)
    public double DashLockSec;                // рывок невозможен 3 с после импульса
    public double CoreOpenSec;                // ядро открыто 4 с после импульса
    public double PullForce;                  // воронка Ф3: 2.5 ед/с² к стражу
    // ---------- Гранильщик (Г4): осколки, луч, зеркало ----------
    public int ShardCount;                    // осколков на орбите (4)
    public double ShardOrbitFrac;             // орбита = BodyR × (1 + ShardOrbitFrac)
    public double ShardOrbitPeriod;           // оборот за 6 с
    public double ShardRadiusFrac;            // допуск блока = BodyR × ShardRadiusFrac
    public double BlockMassFrac;              // блок: −5% массы герою (применяет Unity)
    public double BeamEvery;                  // луч каждые 6 с
    public double BeamTelegraph;              // предупреждение (0.9 с)
    public double BeamDamageFrac;             // попадание 10% массы
    public double BeamWidthFrac;              // ширина луча = BodyR × BeamWidthFrac
    public double BeamLengthMult;             // длина = арена × 1.3
    public double CounterDelay;               // контрудар через 0.6 с после рывка
}

/// <summary>Каталог пяти стражей — по одному на глубину. Детерминирован, без LINQ.</summary>
public static class GuardianCatalog {
    private static readonly GuardianDef[] AllDefs = new GuardianDef[] {
        new GuardianDef {
            Id = "spore_mother", Name = "Спороносица", Depth = 1, SpeciesId = "warden", Tint = 0x7CE07C,
            ModelKey = "gd_spore_mother", Portrait = "guardian_sporonosa",
            Kind = GuardianKind.SporeMother, HpMult = 1.3, ArenaMult = 4.5, WakeMult = 3.6,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            ZoneCount = 4, ZoneMult = 1.7, ZoneDriftFrac = 0.68, ZoneDamagePerSec = 0.4, ZoneSlow = 0.30,
            ZoneShuffleEvery = 8.0,
            MinionSpecies = "mite", MinionMassFrac = 0.65, MinionsPerWave = 2, MinionEvery1 = 12.0, MinionEvery3 = 6.0,
            RootEvery2 = 4.0, RootEvery3 = 2.5, RootTelegraph = 0.8, RootDamageFrac = 0.12, RootMult = 1.5,
            WeakClosedSec = 6.0, WeakOpenSec = 2.0, WeakDamageMult = 1.5, ClosedCapMult = 0.5,
        },
        // Горнило (Г2): экономия массы — гейзеры, деление на капли, кипящее кольцо.
        new GuardianDef {
            Id = "forge", Name = "Горнило", Depth = 2, SpeciesId = "warden", Tint = 0xE08A4A,
            ModelKey = "gd_forge", Portrait = "guardian_gornilo",
            Kind = GuardianKind.Forge, HpMult = 1.35, ArenaMult = 4.5, WakeMult = 3.6,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            GeyserEvery1 = 5.0, GeyserEvery3 = 4.0, GeyserCount = 3, GeyserMult = 1.1,
            GeyserTelegraph = 1.0, GeyserDamageFrac = 0.08, GeyserKnockback = 8.0,
            PartCrawlFrac = 0.55, PartMergeEvery = 3.0, PartMergeBonus = 0.30,
            RingShrinkEvery = 6.0, RingShrinkStep = 0.10, RingMinFrac = 0.40, RingDamagePerSec = 0.012,
        },
        // Хор Бездны (Г3): экономия рывков и тайминг — ток, оглушение, воронка.
        new GuardianDef {
            Id = "abyss_choir", Name = "Хор Бездны", Depth = 3, SpeciesId = "warden", Tint = 0x7A8CE0,
            ModelKey = "gd_abyss_choir", Portrait = "guardian_hor_bezdny",
            Kind = GuardianKind.AbyssChoir, HpMult = 1.4, ArenaMult = 4.5, WakeMult = 3.6,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            CurrentForce = 2.0, CurrentChangeEvery = 6.0,
            PulseEvery1 = 5.0, PulseEvery3 = 3.5, PulseMult = 1.9, PulseTelegraph = 0.6, PulseDamageFrac = 0.08,
            DashLockSec = 3.0, CoreOpenSec = 4.0, PullForce = 2.5,
            WeakDamageMult = 1.5, ClosedCapMult = 0.5,
        },
        // Гранильщик (Г4): точность рывков — осколки-блок, лучи, зеркало-контрудар.
        new GuardianDef {
            Id = "grinder", Name = "Гранильщик", Depth = 4, SpeciesId = "warden", Tint = 0xC0C8D8,
            ModelKey = "gd_grinder", Portrait = "guardian_granilshchik",
            Kind = GuardianKind.Grinder, HpMult = 1.45, ArenaMult = 4.5, WakeMult = 3.6,
            PhaseThreshold2 = 0.66, PhaseThreshold3 = 0.33,
            ShardCount = 4, ShardOrbitFrac = 1.2, ShardOrbitPeriod = 6.0, ShardRadiusFrac = 0.11, BlockMassFrac = 0.05,
            BeamEvery = 6.0, BeamTelegraph = 0.9, BeamDamageFrac = 0.10, BeamWidthFrac = 0.13, BeamLengthMult = 1.3,
            CounterDelay = 0.6,
            MinionSpecies = "shard_crab", MinionMassFrac = 0.65, MinionsPerWave = 4,
        },
        // Пульсар: поведение старого BossState 1:1 (пороги 0.60/0.45 — RunBossTests зелёные).
        new GuardianDef {
            Id = "pulsar", Name = "Пульсар", Depth = 5, SpeciesId = "warden", Tint = 0xFF6A5A,
            ModelKey = "gd_pulsar", Portrait = "guardian_pulsar",
            Kind = GuardianKind.Pulsar, HpMult = 1.5, ArenaMult = 4.5, WakeMult = 3.6,
            PhaseThreshold2 = 0.60, PhaseThreshold3 = 0.45,
            MinionSpecies = "mite", MinionMassFrac = 0.65, MinionsPerWave = 3, MinionEvery1 = 7.0, MinionEvery3 = 7.0,
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
