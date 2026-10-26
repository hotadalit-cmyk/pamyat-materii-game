using System;
using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Предложение мутаций: ровно 3 разных id (или меньше, если пул мал).</summary>
public sealed class MutationOffer {
    public string[] Options;
}

/// <summary>Итог жизни: всё, что душа уносит из забега.</summary>
public sealed class LifeImprint {
    public int DepthReached, RoomsCleared, Absorbed, NewSpecies;
    public double PeakMass, BestHit, EssenceEarned;
    public string KilledBy;          // speciesId или имя души; null, если вышел/победил
    public string[] Mutations;
    public string BiomeReached;      // имя биома самой глубокой достигнутой глубины
    public int BestCombo;            // самая длинная цепочка поглощений
    public bool WardenSlain;         // страж глубины поглощён
}

/// <summary>
/// Состояние одного забега. Чистый C# без UnityEngine; всё детерминировано от seed.
/// Для поглощения немезиды Unity-слой передаёт её SoulName как speciesId в OnAbsorb.
/// </summary>
public sealed class RunState {
    public const int RoomsPerDepth = 3;
    public const int MaxDepth = 5;
    public const double StartMass = 4.0;
    public const double CrystalMassBonusMult = 1.25;
    public const double VictoryEssence = 25.0;
    public const double NemesisEssence = 15.0;
    public const double ChargeRestoreSeconds = 1.2;
    public const double BossStartMass = 30.0;    // страж глубины в комнате босса
    public const double BossEssence = 40.0;      // награда за стража
    public const double EliteMassMult = 1.6;     // массы существ в элитной комнате
    public const double EliteGoalMult = 1.3;     // цель элитной комнаты
    public const double EliteExitEssence = 4.0;  // бонус за выход из элитной комнаты
    public const double SpringMassBonus = 0.35;  // родник: +35% массы (не выше пика)
    public const double EvapFloorMass = 1.05;    // ниже этой массы испарение не опускает

    private static readonly double[] GrowthThresholds = BuildThresholds(); // 6,9,13,18,24,31,40,(×~1.3)

    private readonly SoulMeta _meta;
    private readonly PmRandom _rng;
    private readonly HashSet<string> _speciesThisRun = new HashSet<string>();
    private readonly List<string> _offerBasePool = new List<string> { "m_dash2", "m_magnet", "m_light", "m_regrow" };

    private LifeImprint _finished;
    private string _killedBy;
    private double _bestHit;
    private double _chargeTimer;
    private int _absorbedCount;        // поглощения не-спор
    private int _nemesisAbsorbedCount; // поглощённые немезиды (+15 эссенции каждая)
    private int _roomsCleared;
    private int _nextThreshold;
    private int _offersDue;
    private double _bonusEssence;   // элита +4, страж +40
    private bool _bossAbsorbed;
    private bool _wardenSlain;

    public int Depth { get; private set; }
    public int RoomIndex { get; private set; }
    public double Mass { get; private set; }
    public double PeakMass { get; private set; }
    public MutationSet Mutations { get; }
    public RoomPlan Room { get; private set; }
    public double RoomAbsorbed { get; private set; }
    /// <summary>В комнате босса выход открыт только после поглощения стража.</summary>
    public bool ExitOpen => Room != null
        && (Boss != null ? _bossAbsorbed : RoomAbsorbed >= Room.GoalMass);
    public bool IsDead { get; private set; }         // Mass < SizeRule.MinViableMass
    public bool IsVictory { get; private set; }      // прошёл последнюю комнату MaxDepth
    public MutationOffer PendingOffer { get; private set; }
    public int DashCharges { get; private set; }
    public int MaxDashCharges { get; private set; }
    public RoomLayout Layout { get; private set; }   // раскладка текущей комнаты
    public BossState Boss { get; private set; }      // не null только в комнате босса
    public ComboTracker Combo { get; private set; }  // цепочки поглощений за рывок
    public bool AltarUsed { get; private set; }      // алтарь использован в этой комнате

    public RunState(SoulMeta meta, ulong seed) {
        _meta = meta ?? new SoulMeta();
        _rng = new PmRandom(seed);
        Mutations = new MutationSet();
        Depth = 1;
        RoomIndex = 0;
        Mass = StartMass + _meta.StartMassBonus;
        PeakMass = Mass;
        Combo = new ComboTracker();
        SetupRoom();
        MaxDashCharges = Mutations.DashCharges + _meta.BonusDashCharges;
        DashCharges = MaxDashCharges;
        SyncOffers();
    }

    /// <summary>Тратит заряд и SizeRule.DashCost массы; false, если нет заряда.</summary>
    public bool TryDash() {
        if (IsDead || IsVictory) return false;
        if (DashCharges <= 0) return false;
        DashCharges--;
        Combo.OnDash();
        Mass -= SizeRule.DashCost(Mass, Mutations, _meta.DashCostMult);
        if (Mass < SizeRule.MinViableMass) { // теоретически достижимо на границе 1.0
            IsDead = true;
            _killedBy = "dash";
        }
        return true;
    }

    /// <summary>+масса, учёт видов/генов, счётчики, проверка порога роста.</summary>
    public void OnAbsorb(string speciesId, double mass) => OnAbsorbEx(speciesId, mass);

    /// <summary>Как OnAbsorb, но добавляет и возвращает бонус цепочки поглощений.</summary>
    public double OnAbsorbEx(string speciesId, double mass) {
        if (mass <= 0.0 || IsDead) return 0.0;
        double bonus = Combo.OnAbsorb(speciesId == "spore", mass);
        Mass += mass + bonus;
        RoomAbsorbed += mass + bonus;
        if (Mass > PeakMass) PeakMass = Mass;
        var sp = SpeciesCatalog.Get(speciesId);
        if (sp != null) {
            _speciesThisRun.Add(sp.Id);
            if (sp.Behavior != CreatureBehavior.Food) _absorbedCount++;
        } else if (IsNemesisName(speciesId)) {
            _absorbedCount++;
            _nemesisAbsorbedCount++;
            if (_meta.Nemeses != null) _meta.Nemeses.OnSoulAbsorbed(speciesId);
        }
        SyncOffers();
        return bonus;
    }

    /// <summary>Страж глубины поглощён: +масса, +40 эссенции, WardenSlain, выход открыт.</summary>
    public void OnBossAbsorbed() {
        if (Boss == null || _bossAbsorbed) return;
        _bossAbsorbed = true;
        _wardenSlain = true;
        double m = Boss.Mass;
        if (m > 0.0) {
            Mass += m;
            RoomAbsorbed += m;
            if (Mass > PeakMass) PeakMass = Mass;
        }
        _bonusEssence += BossEssence;
        SyncOffers();
    }

    /// <summary>Алтарь (один раз на комнату): Shrine — дар вне порогов, Spring — +35% массы.</summary>
    public void UseAltar() {
        if (AltarUsed || Layout == null || IsDead || IsVictory) return;
        if (Layout.Kind == RoomKind.Shrine) {
            if (PendingOffer == null) PendingOffer = MakeOffer();
            AltarUsed = true;
        } else if (Layout.Kind == RoomKind.Spring) {
            double target = Mass * (1.0 + SpringMassBonus);
            if (target > PeakMass) target = PeakMass;
            if (target > Mass) Mass = target;
            AltarUsed = true;
        }
    }

    /// <summary>Вампиризм: +dealt × LifestealFrac массы.</summary>
    public void OnLifesteal(double dealt) {
        if (dealt <= 0.0 || IsDead) return;
        double gain = dealt * Mutations.LifestealFrac;
        if (gain <= 0.0) return;
        Mass += gain;
        if (Mass > PeakMass) PeakMass = Mass;
        SyncOffers();
    }

    /// <summary>Зона Pool: +массы; не поглощение, цель комнаты не двигается.</summary>
    public void Heal(double amount) {
        if (amount <= 0.0 || IsDead) return;
        Mass += amount;
        if (Mass > PeakMass) {
            PeakMass = Mass;
            SyncOffers();
        }
    }

    /// <summary>−масса; при смерти запоминает KilledBy.</summary>
    public void OnMassLost(double amount, string sourceId) {
        if (amount <= 0.0) return;
        Mass -= amount;
        if (!IsDead && Mass < SizeRule.MinViableMass) {
            IsDead = true;
            _killedBy = sourceId;
        }
    }

    /// <summary>Учитывает лучший удар игрока (для BestHit в LifeImprint).</summary>
    public void OnHit(double dealtMassLoss) {
        if (dealtMassLoss > _bestHit) _bestHit = dealtMassLoss;
    }

    /// <summary>Регенерация массы и восстановление зарядов (1 за 1.2 с).</summary>
    public void Tick(double dt) {
        if (dt <= 0.0 || IsDead) return;
        Combo.Tick(dt);
        double regen = Mutations.RegenPerSec * dt;
        if (regen > 0.0) {
            Mass += regen;
            if (Mass > PeakMass) {
                PeakMass = Mass;
                SyncOffers();
            }
        }
        if (DashCharges < MaxDashCharges) {
            _chargeTimer += dt;
            while (_chargeTimer >= ChargeRestoreSeconds && DashCharges < MaxDashCharges) {
                _chargeTimer -= ChargeRestoreSeconds;
                DashCharges++;
            }
            if (DashCharges >= MaxDashCharges) _chargeTimer = 0.0;
        } else {
            _chargeTimer = 0.0;
        }
        // испарение в тёплых биомах: m_heatproof ослабляет, m_regrow компенсирует;
        // масса не может опуститься ниже EvapFloorMass (испарение не убивает)
        double evapPerSec = Layout != null ? Layout.Biome.EvaporationPerSec : 0.0;
        if (evapPerSec > 0.0 && Mass > EvapFloorMass) {
            Mass -= Mass * evapPerSec * Mutations.HazardMult * dt;
            if (Mass < EvapFloorMass) Mass = EvapFloorMass;
        }
    }

    /// <summary>Применяет выбранную мутацию и снимает PendingOffer (m_crystal даёт +25% массы).</summary>
    public void ChooseMutation(string id) {
        if (PendingOffer == null || id == null) return;
        bool offered = false;
        var options = PendingOffer.Options;
        if (options != null)
            for (int i = 0; i < options.Length; i++)
                if (options[i] == id) { offered = true; break; }
        if (!offered) return;
        Mutations.Add(id);
        if (id == "m_crystal") {
            Mass *= CrystalMassBonusMult;
            if (Mass > PeakMass) PeakMass = Mass;
        }
        if (id == "m_dash2") MaxDashCharges = Mutations.DashCharges + _meta.BonusDashCharges;
        PendingOffer = null;
        SyncOffers(); // возможно, за порогом уже ждёт следующее предложение
    }

    /// <summary>Если ExitOpen: следующая комната/глубина (новый RoomPlan); на последней — победа.</summary>
    public bool TryEnterExit() {
        if (!ExitOpen || IsDead || IsVictory) return false;
        bool wasElite = Layout != null && Layout.Kind == RoomKind.Elite;
        _roomsCleared++;
        if (wasElite) {
            _bonusEssence += EliteExitEssence;
            _offersDue++; // гарантированное предложение при выходе
        }
        if (Depth >= MaxDepth && RoomIndex >= RoomsPerDepth - 1) {
            IsVictory = true;
            return true;
        }
        RoomIndex++;
        if (RoomIndex >= RoomsPerDepth) {
            RoomIndex = 0;
            Depth++;
        }
        SetupRoom();
        RoomAbsorbed = 0.0;
        if (wasElite) SyncOffers();
        return true;
    }

    /// <summary>Комната + раскладка; элита усиливается после генерации, у босса — только споры.</summary>
    private void SetupRoom() {
        ulong roomSeed = _rng.NextUInt();
        Room = RoomGenerator.Generate(Depth, RoomIndex, roomSeed, _meta.Nemeses);
        var kind = LayoutGenerator.KindFor(Depth, RoomIndex, roomSeed);
        if (kind == RoomKind.Boss) {
            var spores = new List<SpawnSpec>(6);
            for (int i = 0; i < Room.Spawns.Count && spores.Count < 6; i++)
                if (Room.Spawns[i].SpeciesId == "spore") spores.Add(Room.Spawns[i]);
            Room.Spawns = spores;
            Boss = new BossState(BossStartMass, _rng.NextUInt());
        } else {
            Boss = null;
            if (kind == RoomKind.Elite) ApplyElite(Room);
        }
        Layout = LayoutGenerator.Generate(Room, kind, _rng.NextUInt());
        AltarUsed = false;
        _bossAbsorbed = false;
    }

    /// <summary>Элитная комната: массы существ ×1.6 и цель ×1.3 (после генерации).</summary>
    private static void ApplyElite(RoomPlan room) {
        for (int i = 0; i < room.Spawns.Count; i++) {
            var sp = SpeciesCatalog.Get(room.Spawns[i].SpeciesId);
            bool isCreature = room.Spawns[i].IsNemesis || (sp != null && sp.Behavior != CreatureBehavior.Food);
            if (!isCreature) continue;
            SpawnSpec s = room.Spawns[i];
            s.Mass *= EliteMassMult;
            room.Spawns[i] = s;
        }
        room.GoalMass *= EliteGoalMult;
    }

    /// <summary>Итог жизни + эссенция; повторный вызов возвращает тот же объект.</summary>
    public LifeImprint Finish() {
        if (_finished != null) return _finished;
        int newSpecies = 0;
        foreach (string s in _speciesThisRun)
            if (_meta.SeenSpecies == null || !_meta.SeenSpecies.Contains(s)) newSpecies++;
        double essence = 10.0 * (Depth - 1)
                       + 3.0 * _roomsCleared
                       + 0.5 * _absorbedCount
                       + 5.0 * newSpecies;
        if (IsVictory) essence += VictoryEssence;
        essence += NemesisEssence * _nemesisAbsorbedCount;
        essence += _bonusEssence;
        _finished = new LifeImprint {
            DepthReached = Depth,
            RoomsCleared = _roomsCleared,
            Absorbed = _absorbedCount,
            NewSpecies = newSpecies,
            PeakMass = PeakMass,
            BestHit = _bestHit,
            EssenceEarned = essence,
            KilledBy = IsDead ? _killedBy : null,
            Mutations = Mutations.ToArray(),
            BiomeReached = BiomeCatalog.ForDepth(Depth).Name,
            BestCombo = Combo.Best,
            WardenSlain = _wardenSlain,
        };
        return _finished;
    }

    private void SyncOffers() {
        while (_nextThreshold < GrowthThresholds.Length && PeakMass >= GrowthThresholds[_nextThreshold]) {
            _nextThreshold++;
            _offersDue++;
        }
        if (PendingOffer == null && _offersDue > 0) {
            _offersDue--;
            PendingOffer = MakeOffer();
        }
    }

    /// <summary>Пул: базовый ∪ KnownGenes ∪ гены видов, поглощённых в забеге; исключить недоступные.</summary>
    private MutationOffer MakeOffer() {
        var pool = new List<string>(_offerBasePool.Count + 8);
        CopyMissing(pool, _offerBasePool);
        if (_meta.KnownGenes != null) {
            var genes = new List<string>(_meta.KnownGenes);
            CopyMissing(pool, genes);
        }
        foreach (string speciesId in _speciesThisRun) {
            var sp = SpeciesCatalog.Get(speciesId);
            if (sp != null && !string.IsNullOrEmpty(sp.GeneMutationId)) CopyMissing(pool, sp.GeneMutationId);
        }
        var available = new List<string>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
            if (Mutations.CanAdd(pool[i])) available.Add(pool[i]);

        int take = available.Count < 3 ? available.Count : 3;
        var options = new string[take];
        for (int i = 0; i < take; i++) { // частичный Фишер–Йетс, seeded
            int j = i + _rng.NextInt(available.Count - i);
            string tmp = available[i]; available[i] = available[j]; available[j] = tmp;
            options[i] = available[i];
        }
        return new MutationOffer { Options = options };
    }

    private static void CopyMissing(List<string> target, List<string> ids) {
        for (int i = 0; i < ids.Count; i++) CopyMissing(target, ids[i]);
    }

    private static void CopyMissing(List<string> target, string id) {
        for (int i = 0; i < target.Count; i++)
            if (target[i] == id) return;
        target.Add(id);
    }

    private bool IsNemesisName(string name) {
        if (_meta.Nemeses == null || name == null) return false;
        var records = _meta.Nemeses.Records;
        for (int i = 0; i < records.Count; i++)
            if (records[i].SoulName == name) return true;
        return false;
    }

    private static double[] BuildThresholds() {
        var t = new double[16];
        t[0] = 6; t[1] = 9; t[2] = 13; t[3] = 18; t[4] = 24; t[5] = 31; t[6] = 40;
        for (int i = 7; i < t.Length; i++) t[i] = Math.Ceiling(t[i - 1] * 1.3);
        return t;
    }
}
}
