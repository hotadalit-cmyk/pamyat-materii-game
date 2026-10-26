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
    public int RegionsDiscovered;    // v0.6: сколько областей подземелья открыто
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
    public const double BiomeCompletionEssence = 5.0;  // v0.10: за полное исследование биома в забеге
    // v0.11: стражи глубин
    public const double GuardianRetreatPenalty = 0.25; // отступление в фазе 1: −25% текущей массы героя
    public const double GuardianPityMult = 0.85;       // после 2 поражений от стража его HP ×0.85
    public const int GuardianPityAfterDefeats = 2;
    private static readonly double[] GuardianEssenceByDepth = { 8.0, 14.0, 22.0, 32.0, 40.0 };

    private static readonly double[] GrowthThresholds = BuildThresholds(); // 6,9,13,18,24,31,40,(×~1.3)

    private readonly SoulMeta _meta;
    private readonly PmRandom _rng;
    private readonly HashSet<string> _speciesThisRun = new HashSet<string>();
    private readonly List<string> _offerBasePool = new List<string> { "m_dash2", "m_magnet", "m_light", "m_regrow" };

    private LifeImprint _finished;
    private string _killedBy;
    private double _bestHit;
    private double _chargeTimer;
    /// <summary>Сколько накопилось до следующего заряда рывка (0..1; 1 — все заряды полны).</summary>
    public double ChargeProgress01 => DashCharges >= MaxDashCharges ? 1.0 : _chargeTimer / ChargeRestoreSeconds;
    private int _absorbedCount;        // поглощения не-спор
    private int _nemesisAbsorbedCount; // поглощённые немезиды (+15 эссенции каждая)
    private int _roomsCleared;
    private int _nextThreshold;
    private int _offersDue;
    private double _bonusEssence;   // элита +4, страж +40
    private bool _bossAbsorbed;
    private bool _wardenSlain;
    // v0.10: завершённые в этом забеге биомы — за полное исследование их пула видов даётся +5 эссенции.
    private readonly HashSet<BiomeId> _completedBiomes = new HashSet<BiomeId>();
    // v0.6 открытый мир
    private bool[] _altarUsed = new bool[0];
    private readonly HashSet<int> _regionsSeen = new HashSet<int>();
    private double _relicEssence;
    private int _regionsTotal;

    public int Depth { get; private set; }
    public int RoomIndex { get; private set; }
    /// <summary>Масса тела = сумма фрагментов (v0.7). Прибавка идёт ведущему (или фрагменту-поглотителю),
    /// убыль — пропорционально всем фрагментам.</summary>
    public double Mass {
        get => Fragments != null ? Fragments.Total : 0.0;
        private set {
            if (Fragments == null) return;
            double delta = value - Fragments.Total;
            if (delta > 0 && _creditId >= 0) Fragments.Add(_creditId, delta);
            else Fragments.ApplyDelta(delta);
        }
    }
    public FragmentSet Fragments { get; private set; }

    // ---------- v0.8: ресурсы, хранилище, дерево эволюции ----------
    public ResourceStore Resources { get; private set; }
    public EvolutionState Evolution { get; private set; }
    /// <summary>Прокачка показателей за ресурсы (v0.9).</summary>
    public StatTraining Training { get; private set; }
    private readonly HashSet<string> _known = new HashSet<string>();
    /// <summary>Виды, чьи гены известны (поглощены в этой жизни или раньше) — открывают генные узлы.</summary>
    public ICollection<string> KnownSpecies => _known;
    /// <summary>Что дало последнее поглощение (для всплывающих «+2 Хитин»).</summary>
    public CostItem[] LastYield { get; private set; } = new CostItem[0];
    /// <summary>Мутация, возвращённая из немезиды при мести (для всплывающего «Возвращено: Шипы»); null, если не было.</summary>
    public string LastReclaimedMutation { get; private set; }
    public double LightRadius => Mutations.LightRadius + (Evolution != null ? Evolution.Sum(EvoEffectKind.LightFlat) : 0);
    public double PullBonus => Evolution != null ? Evolution.Sum(EvoEffectKind.PullFlat) : 0;
    public double DashSpeedMult => Mutations.DashSpeedMult * (1.0 + (Evolution != null ? Evolution.Sum(EvoEffectKind.DashSpeedMult) : 0));
    private int _creditId = -1;   // какой фрагмент получает прибавку (поглощение конкретным фрагментом)
    public double PeakMass { get; private set; }
    public MutationSet Mutations { get; }
    public RoomPlan Room { get; private set; }
    public double RoomAbsorbed { get; private set; }
    /// <summary>
    /// v0.11: в открытом мире выход открыт, только когда набрана цель глубины И страж поглощён;
    /// в комнатном режиме — как раньше (босс-комната требует стража, обычная — цель).
    /// </summary>
    public bool ExitOpen => Room != null
        && (OpenWorld ? _guardianAbsorbed && RoomAbsorbed >= Room.GoalMass
                      : (Boss != null ? _bossAbsorbed : RoomAbsorbed >= Room.GoalMass));
    public bool IsDead { get; private set; }         // Mass < SizeRule.MinViableMass
    public bool IsVictory { get; private set; }      // прошёл последнюю комнату MaxDepth
    public MutationOffer PendingOffer { get; private set; }
    public int DashCharges { get; private set; }
    public int MaxDashCharges { get; private set; }
    public RoomLayout Layout { get; private set; }   // раскладка текущей комнаты
    public BossState Boss { get; private set; }      // v0.11: только в комнатном режиме; в открытом мире — Guardian
    // v0.11: страж каждой глубины открытого мира; спит, пока не набрана цель глубины
    public GuardianState Guardian { get; private set; }
    public bool GuardianAwake { get; private set; }
    public bool GuardianAbsorbed => _guardianAbsorbed;
    private bool _guardianAbsorbed;
    private readonly Dictionary<int, int> _guardianDefeats = new Dictionary<int, int>();
    /// <summary>HP стража по формуле max(8, GoalMass × HpMult) — без поправки на жалость.</summary>
    public double GuardianFormulaHp { get; private set; }
    /// <summary>Текущий максимум HP стража (формула × жалость после 2 поражений).</summary>
    public double GuardianMaxHp { get; private set; }

    /// <summary>Эссенция за стража глубины: Г1 8, Г2 14, Г3 22, Г4 32, Г5 40.</summary>
    public static double GuardianEssenceFor(int depth) {
        if (depth < 1) depth = 1;
        if (depth > GuardianEssenceByDepth.Length) depth = GuardianEssenceByDepth.Length;
        return GuardianEssenceByDepth[depth - 1];
    }

    /// <summary>Сколько раз герой погибал/отступал в бою со стражем этой глубины в этом забеге.</summary>
    public int GuardianDefeats(int depth) => _guardianDefeats.TryGetValue(depth, out int n) ? n : 0;
    public ComboTracker Combo { get; private set; }  // цепочки поглощений за рывок
    public bool AltarUsed { get; private set; }      // алтарь использован в этой комнате
    public bool OpenWorld { get; }                   // v0.6: каждая глубина — одно огромное подземелье
    public WorldMap World { get; private set; }
    public int RegionsDiscovered => _regionsTotal;
    public const double RelicEssence = 15.0;

    public RunState(SoulMeta meta, ulong seed) : this(meta, seed, false) { }

    public RunState(SoulMeta meta, ulong seed, bool openWorld) {
        OpenWorld = openWorld;
        _meta = meta ?? new SoulMeta();
        _rng = new PmRandom(seed);
        Mutations = new MutationSet();
        Depth = 1;
        RoomIndex = 0;
        Fragments = new FragmentSet(StartMass + _meta.StartMassBonus);
        Resources = new ResourceStore { Capacity = ResourceStore.BaseCapacity };
        Evolution = new EvolutionState();
        Training = new StatTraining();
        Evolution.Training = Training;
        if (_meta.SeenSpecies != null) foreach (var sp in _meta.SeenSpecies) _known.Add(sp);
        if (OpenWorld) Fragments.MaxCount = 1; // v0.9: делиться можно только после узла «Деление»
        PeakMass = Mass;
        Combo = new ComboTracker();
        if (OpenWorld) SetupWorld(); else SetupRoom();
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

    /// <summary>
    /// v0.10: Поглощение мерцающей особи — даёт ×1.5 ресурсов (масса не меняется).
    /// Unity вызывает это вместо OnAbsorb, когда подобрана особь с SpawnVariant.Shimmering.
    /// </summary>
    public double OnAbsorbVariant(string speciesId, double mass, SpawnVariant variant) {
        double saved = _shimmeringYieldMult;
        if (variant == SpawnVariant.Shimmering) _shimmeringYieldMult = 1.5;
        LastAbsorbVariant = variant;
        try { return OnAbsorbEx(speciesId, mass); }
        finally { _shimmeringYieldMult = saved; }
    }
    private double _shimmeringYieldMult = 1.0;
    /// <summary>Вариант последнего поглощённого существа (None/Shimmering) — для UI-всплеска «Мерцание!».</summary>
    public SpawnVariant LastAbsorbVariant { get; private set; }

    /// <summary>Как OnAbsorb, но добавляет и возвращает бонус цепочки поглощений.</summary>
    public double OnAbsorbEx(string speciesId, double mass) {
        if (mass <= 0.0 || IsDead) return 0.0;
        double yieldMass = mass;
        if (Evolution != null) mass *= 1.0 + Evolution.Sum(EvoEffectKind.MassGainMult);
        double bonus = Combo.OnAbsorb(speciesId == "spore", mass);
        // v0.10: пороги цепочки 3/5/8 — мгновенная награда за агрессивную игру.
        int milestone = Combo.PopPendingMilestone();
        if (milestone > 0) ApplyComboMilestone(milestone);
        if (Resources != null) {
            var items = YieldTable.For(speciesId, yieldMass);
            double ym = 1.0 + Evolution.Sum(EvoEffectKind.YieldMult);
            ym *= _shimmeringYieldMult; // v0.10: мерцание ×1.5 к выпадаемым ресурсам
            var got = new List<CostItem>(items.Length);
            if (_creditId >= 0 && _creditId != Fragments.LeadId) ym *= 1.0 + Evolution.Sum(EvoEffectKind.CloneYieldMult);
            for (int i = 0; i < items.Length; i++) {
                double added = AddLoot(_creditId, items[i].Kind, items[i].Amount * ym);
                if (added > 0) got.Add(new CostItem { Kind = items[i].Kind, Amount = added });
            }
            LastYield = got.ToArray();
            _known.Add(speciesId);
        }
        Mass += mass + bonus;
        RoomAbsorbed += mass + bonus;
        if (Mass > PeakMass) PeakMass = Mass;
        CheckGuardianWake(); // v0.11: цель глубины набрана — страж пробуждается у шахты
        var sp = SpeciesCatalog.Get(speciesId);
        if (sp != null) {
            _speciesThisRun.Add(sp.Id);
            if (sp.Behavior != CreatureBehavior.Food) _absorbedCount++;
            CheckBiomeCompletions();  // v0.10: новый вид может завершить пул биома
        } else if (IsNemesisName(speciesId)) {
            _absorbedCount++;
            _nemesisAbsorbedCount++;
            LastReclaimedMutation = null;
            if (_meta.Nemeses != null) {
                // v0.10: Месть — за поглощение души, убивавшей вас, вы получаете бонус-эссенцию
                // и возвращаете украденную мутацию (один стак на забег, если место есть).
                var rec = _meta.Nemeses.FindRecord(speciesId);
                double vengeance = NemesisRegistry.VengeanceEssence(rec);
                if (vengeance > 0.0) {
                    _bonusEssence += vengeance;
                    string reclaim = NemesisRegistry.ReclaimableMutation(rec);
                    if (!string.IsNullOrEmpty(reclaim) && Mutations.CanAdd(reclaim)) {
                        Mutations.Add(reclaim);
                        LastReclaimedMutation = reclaim;
                    }
                }
                _meta.Nemeses.OnSoulAbsorbed(speciesId);
            }
        }
        SyncOffers();
        return bonus;
    }

    /// <summary>
    /// Страж поглощён касанием (только обнажённый). Открытый мир: награда эссенцией по глубине
    /// и жетон эволюции, выход открыт. Комнатный режим: как раньше — +40 эссенции.
    /// </summary>
    public void OnBossAbsorbed() {
        if (OpenWorld) {
            if (Guardian == null || _guardianAbsorbed || !Guardian.IsExposed) return;
            _guardianAbsorbed = true;
            _wardenSlain = true;
            double m = Guardian.Hp;
            if (m > 0.0) {
                Mass += m;
                RoomAbsorbed += m;
                if (Mass > PeakMass) PeakMass = Mass;
            }
            _bonusEssence += GuardianEssenceFor(Depth);
            if (Evolution != null) Evolution.FreeTokens++; // жетон эволюции за стража
            SyncOffers();
            return;
        }
        if (Boss == null || _bossAbsorbed) return;
        _bossAbsorbed = true;
        _wardenSlain = true;
        double mb = Boss.Mass;
        if (mb > 0.0) {
            Mass += mb;
            RoomAbsorbed += mb;
            if (Mass > PeakMass) PeakMass = Mass;
        }
        _bonusEssence += BossEssence;
        SyncOffers();
    }

    /// <summary>
    /// v0.11: игрок отступил от стража в фазе 1 (выход из арены). Счёт поражений +1,
    /// штраф −25% текущей массы героя; после 2 поражений страж этой глубины оживает с HP ×0.85.
    /// </summary>
    public void OnGuardianFightReset() {
        if (!OpenWorld || Guardian == null || !GuardianAwake || _guardianAbsorbed || IsDead) return;
        _guardianDefeats[Depth] = GuardianDefeats(Depth) + 1;
        double penalty = Mass * GuardianRetreatPenalty;
        if (penalty > 0.0) {
            Mass -= penalty;
            if (!IsDead && Mass < SizeRule.MinViableMass) { IsDead = true; _killedBy = "guardian_retreat"; }
        }
        GuardianMaxHp = GuardianFormulaHp *
            (GuardianDefeats(Depth) >= GuardianPityAfterDefeats ? GuardianPityMult : 1.0);
        Guardian.Restore(GuardianMaxHp);
    }

    /// <summary>Цель глубины набрана — страж пробуждается у шахты (World.ExitPos).</summary>
    private void CheckGuardianWake() {
        if (OpenWorld && Guardian != null && !GuardianAwake && !_guardianAbsorbed
            && Room != null && RoomAbsorbed >= Room.GoalMass)
            GuardianAwake = true;
    }

    /// <summary>Смерть от стража этой глубины тоже считается поражением (для жалости ×0.85).</summary>
    private void CountGuardianDefeat(string sourceId) {
        if (OpenWorld && GuardianAwake && !_guardianAbsorbed && Guardian != null
            && sourceId == Guardian.Def.SpeciesId)
            _guardianDefeats[Depth] = GuardianDefeats(Depth) + 1;
    }

    /// <summary>Алтарь (один раз на комнату): Shrine — дар вне порогов, Spring — +35% массы.</summary>
    public void UseAltar() {
        if (AltarUsed || Layout == null || IsDead || IsVictory) return;
        if (Layout.Kind == RoomKind.Shrine) {
            if (PendingOffer == null) {
                PendingOffer = MakeOffer();
                if (PendingOffer.Options.Length == 0) PendingOffer = null;
            }
            AltarUsed = true;
        } else if (Layout.Kind == RoomKind.Spring) {
            double target = Mass * (1.0 + SpringMassBonus);
            if (target > PeakMass) target = PeakMass;
            if (target > Mass) Mass = target;
            AltarUsed = true;
        }
    }

    // ---------- v0.10: Алхимия ресурсов у святилища ----------
    // Игрок может конвертировать 3 единицы одного ресурса в 1 единицу другого.
    // Доступно только в комнатах-святилищах (Layout.Kind == Shrine), до 3 трансмутаций за комнату.
    // Не зависит от UseAltar — игрок может взять дар И потратить трансмутации (или только одно).
    public const int MaxTransmutesPerShrine = 3;

    /// <summary>Сколько трансмутаций осталось в текущей комнате-святилище.</summary>
    public int TransmutesLeft => OpenWorld ? _alchemyCharges
        : (Layout != null && Layout.Kind == RoomKind.Shrine) ? MaxTransmutesPerShrine - _transmutesThisRoom : 0;

    /// <summary>Открытый мир: обмены, накопленные у святилищ и реликвий (тратятся где угодно).</summary>
    private int _alchemyCharges;

    /// <summary>Сколько трансмутаций уже сделано в этой комнате — для тестов и UI.</summary>
    public int TransmutesUsedThisRoom => _transmutesThisRoom;

    private int _transmutesThisRoom;

    /// <summary>Последняя трансмутация — для UI-всплеска «3 Хитин → 1 Психея».</summary>
    public TransmuteResult LastTransmute { get; private set; }

    // ---------- v0.10: Завершение биомов ----------
    /// <summary>Сколько биомов полностью исследовано в этом забеге.</summary>
    public int CompletedBiomesCount => _completedBiomes.Count;
    /// <summary>Имя последнего завершённого биома — для UI-флеша «Грибной коридор исследован!».</summary>
    public string LastBiomeCompleted { get; private set; }

    /// <summary>Сколько видов области уже поглощено в этой жизни (для подсказки «Виды 2/4»).</summary>
    public int SpeciesEatenFrom(BiomeDef b) {
        if (b == null || b.SpeciesPool == null) return 0;
        int n = 0;
        for (int i = 0; i < b.SpeciesPool.Length; i++) if (_speciesThisRun.Contains(b.SpeciesPool[i])) n++;
        return n;
    }

    public bool IsBiomeCompleted(BiomeId id) => _completedBiomes.Contains(id);

    /// <summary>Проверяет все биомы: если их пул видов целиком поглощён в этом забеге — даёт +5 эссенции.</summary>
    private void CheckBiomeCompletions() {
        var all = BiomeCatalog.All();
        for (int i = 0; i < all.Length; i++) {
            var b = all[i];
            if (_completedBiomes.Contains(b.Id)) continue;
            if (b.SpeciesPool == null || b.SpeciesPool.Length == 0) continue;
            bool complete = true;
            for (int j = 0; j < b.SpeciesPool.Length; j++) {
                if (!_speciesThisRun.Contains(b.SpeciesPool[j])) { complete = false; break; }
            }
            if (complete) {
                _completedBiomes.Add(b.Id);
                _bonusEssence += BiomeCompletionEssence;
                LastBiomeCompleted = b.Name;
            }
        }
    }

    /// <summary>
    /// Конвертировать 3 единицы ресурса <paramref name="from"/> в 1 единицу <paramref name="to"/>.
    /// Только в Shrine-комнате, не больше MaxTransmutesPerShrine раз за заход.
    /// Возвращает false (без изменений состояния), если условие не выполнено.
    /// </summary>
    public bool TryTransmute(ResourceKind from, ResourceKind to) {
        if (IsDead || IsVictory) return false;
        if (from == to) return false;
        if (OpenWorld) { if (_alchemyCharges <= 0) return false; }
        else {
            if (Layout == null || Layout.Kind != RoomKind.Shrine) return false;
            if (_transmutesThisRoom >= MaxTransmutesPerShrine) return false;
        }
        if (Resources == null) return false;
        if (Resources.Get(from) + 1e-9 < 3.0) return false;
        if (Resources.Get(to) + 1.0 > Resources.Capacity + 1e-9) return false;
        // Обе проверки пройдены — транзакция атомарна.
        Resources.Pay(Cost.Of(new CostItem(from, 3.0)));
        Resources.Add(to, 1.0);
        if (OpenWorld) _alchemyCharges--; else _transmutesThisRoom++;
        LastTransmute = new TransmuteResult { From = from, To = to };
        return true;
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

    /// <summary>
    /// v0.10: Награда за порог цепочки поглощений — мгновенный эффект, не меняющий массу/пик.
    /// 3 — +1 заряд рывка (до максимума); 5 — +1 жетон эволюции (в режиме открытого мира);
    /// 8 — бесплатное предложение мутации (в комнатном режиме) или ещё +1 жетон (открытый мир).
    /// </summary>
    private void ApplyComboMilestone(int milestone) {
        if (IsDead || IsVictory) return;
        if (milestone == ComboTracker.MilestoneChain3) {
            if (DashCharges < MaxDashCharges) DashCharges++;
            LastComboMilestone = 3;
        } else if (milestone == ComboTracker.MilestoneChain5) {
            if (OpenWorld && Evolution != null) Evolution.FreeTokens++;
            LastComboMilestone = 5;
        } else if (milestone == ComboTracker.MilestoneChain8) {
            if (OpenWorld) {
                if (Evolution != null) Evolution.FreeTokens++;
            } else if (PendingOffer == null) {
                PendingOffer = MakeOffer();
                if (PendingOffer.Options.Length == 0) PendingOffer = null;
            }
            LastComboMilestone = 8;
        }
    }

    /// <summary>Последний сработавший порог цепочки (3/5/8); 0, если ещё не было. Для UI-флеша «Цепочка 3!».</summary>
    public int LastComboMilestone { get; private set; }

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
        amount *= DamageTakenFactor;
        Mass -= amount;
        if (!IsDead && Mass < SizeRule.MinViableMass) {
            IsDead = true;
            _killedBy = sourceId;
            CountGuardianDefeat(sourceId); // v0.11
        }
    }

    // ---------- Фрагменты (v0.7) ----------
    /// <summary>Разделиться: каждый достаточно крупный фрагмент — пополам. Пары (родитель, новый) или пусто.</summary>
    public List<int[]> TrySplit() {
        if (IsDead || IsVictory || !Fragments.CanSplit) return new List<int[]>();
        if (OpenWorld && !HasAbility("split")) return new List<int[]>();
        return Fragments.SplitAll();
    }

    /// <summary>Слияние двух фрагментов (касание в режиме сбора). Ноша объединяется; влилась в ведущего — сдана в хранилище.</summary>
    public bool MergeFragments(int keepId, int otherId) {
        LastDeposit = new CostItem[0];
        if (IsDead || !Fragments.Merge(keepId, otherId)) return false;
        if (_carry.TryGetValue(otherId, out var o)) {
            _carry.Remove(otherId);
            if (!_carry.TryGetValue(keepId, out var k)) { k = new double[ResourceInfo.Count]; _carry[keepId] = k; }
            for (int i = 0; i < k.Length; i++) k[i] += o[i];
        }
        LastDeposit = DepositLead();
        return true;
    }

    // ---------- Способности и клоны (v0.9) ----------
    public const double BaseCloneLeash = 10.0;     // дальше — клон слабеет
    public const double CloneHardLeashMult = 1.8;  // дальше soft×1.8 — лопается
    public const double CloneDriftLossPerSec = 0.18; // доля массы в секунду на самом краю
    public const double CloneReturnCarry = 12.0;   // «Зов улья»: с такой ношей клон идёт сдавать

    public bool HasAbility(string key) => Evolution != null && Evolution.HasAbility(key);

    /// <summary>Мягкая граница связи с клонами (мир. ед.).</summary>
    public double CloneLeash => BaseCloneLeash + Evolution.Sum(EvoEffectKind.CloneLeash);
    public double CloneSightBonus => Evolution.Sum(EvoEffectKind.CloneSight);
    public double CloneSpeedMult => 1.0 + Evolution.Sum(EvoEffectKind.CloneSpeedMult);
    /// <summary>Во сколько раз клон должен быть тяжелее жертвы, чтобы съесть её.</summary>
    public double CloneAbsorbRatio => HasAbility("clone_hunter") ? 1.0 : Mutations.AbsorbRatio;
    private double CloneDamageFactor { get { double k = 1.0 + Evolution.Sum(EvoEffectKind.CloneDamageTakenMult); return k < 0.2 ? 0.2 : k; } }

    /// <summary>Слабость клона на расстоянии dist: 0 — в пределах связи, 1 — на грани разрыва.</summary>
    public double CloneWeakness(double dist) {
        double soft = CloneLeash, hard = soft * CloneHardLeashMult;
        if (dist <= soft) return 0;
        return dist >= hard ? 1 : (dist - soft) / (hard - soft);
    }

    public enum Drift { Ok = 0, Weak = 1, Popped = 2 }

    /// <summary>
    /// Клон вдали от души: слабеет и теряет массу; за пределом связи лопается — ноша и масса
    /// рассыпаются рядом (LastDropped, LastPoppedMass). Ведущего не касается.
    /// </summary>
    public Drift DriftAt(int fragmentId, double dist, double dt) {
        if (IsDead || fragmentId == Fragments.LeadId || Fragments.IndexOf(fragmentId) < 0) return Drift.Ok;
        double w = CloneWeakness(dist);
        if (w <= 0) return Drift.Ok;
        if (w >= 1) { PopFragment(fragmentId); return Drift.Popped; }
        double loss = Fragments.MassOf(fragmentId) * CloneDriftLossPerSec * w * dt;
        if (Fragments.Remove(fragmentId, loss)) { DropCarryOf(fragmentId); LastPoppedMass = 0; return Drift.Popped; }
        return Drift.Weak;
    }

    /// <summary>Масса лопнувшего клона — Unity рассыпает её каплями.</summary>
    public double LastPoppedMass { get; private set; }

    /// <summary>Клон лопается: масса и ноша выпадают рядом с ним.</summary>
    public bool PopFragment(int fragmentId) {
        if (fragmentId == Fragments.LeadId || Fragments.IndexOf(fragmentId) < 0) return false;
        double m = Fragments.MassOf(fragmentId);
        if (!Fragments.Remove(fragmentId, m + 1.0)) return false;
        LastPoppedMass = m;
        DropCarryOf(fragmentId);
        return true;
    }

    private void DropCarryOf(int fragmentId) {
        LastDropped = new CostItem[0];
        if (!_carry.TryGetValue(fragmentId, out var lost)) return;
        _carry.Remove(fragmentId);
        var drop = new List<CostItem>();
        for (int i = 0; i < lost.Length; i++) if (lost[i] > 0) drop.Add(new CostItem((ResourceKind)i, lost[i]));
        LastDropped = drop.ToArray();
    }

    /// <summary>Перенос души: клон становится ведущим (вами), его ноша сразу уходит в хранилище.</summary>
    public bool TransferSoul(int targetId) {
        LastDeposit = new CostItem[0];
        if (IsDead || !HasAbility("soul_transfer")) return false;
        if (targetId == Fragments.LeadId || Fragments.IndexOf(targetId) < 0) return false;
        Fragments.MakeLead(targetId);
        LastDeposit = DepositLead();
        return true;
    }

    // ---------- Ноша двойников (v0.9) ----------
    // Добыча не-ведущего фрагмента не идёт в хранилище сразу: он несёт её, пока не сольётся с ведущим.
    // Растворился по дороге — ноша выпадает в мир (LastDropped) и её можно подобрать снова.
    private readonly Dictionary<int, double[]> _carry = new Dictionary<int, double[]>();

    /// <summary>Что сдано в хранилище при последнем слиянии.</summary>
    public CostItem[] LastDeposit { get; private set; } = new CostItem[0];
    /// <summary>Ноша растворившегося фрагмента — Unity рассыпает её по полу.</summary>
    public CostItem[] LastDropped { get; private set; } = new CostItem[0];

    /// <summary>Сколько всего ресурсов несёт фрагмент (0 — у ведущего: его добыча сразу в хранилище).</summary>
    public double CarryTotal(int fragmentId) {
        if (!_carry.TryGetValue(fragmentId, out var c)) return 0;
        double t = 0; for (int i = 0; i < c.Length; i++) t += c[i];
        return t;
    }

    public double CarryOf(int fragmentId, ResourceKind kind) => _carry.TryGetValue(fragmentId, out var c) ? c[(int)kind] : 0;

    /// <summary>Подобрать выпавшую ношу (или любую добычу) фрагментом.</summary>
    public void PickupAt(int fragmentId, ResourceKind kind, double amount) {
        if (IsDead || amount <= 0) return;
        AddLoot(Fragments.IndexOf(fragmentId) >= 0 ? fragmentId : -1, kind, amount);
    }

    private double AddLoot(int fragmentId, ResourceKind kind, double amount) {
        if (amount <= 0) return 0;
        if (fragmentId < 0 || fragmentId == Fragments.LeadId) return Resources.Add(kind, amount);
        if (!_carry.TryGetValue(fragmentId, out var c)) { c = new double[ResourceInfo.Count]; _carry[fragmentId] = c; }
        c[(int)kind] += amount;
        return amount;
    }

    /// <summary>Ведущий никогда ничего не «несёт» — всё, что у него оказалось, уходит в хранилище.</summary>
    private CostItem[] DepositLead() {
        int lead = Fragments.LeadId;
        if (!_carry.TryGetValue(lead, out var c)) return new CostItem[0];
        _carry.Remove(lead);
        var got = new List<CostItem>();
        for (int i = 0; i < c.Length; i++) {
            if (c[i] <= 0) continue;
            double added = Resources.Add((ResourceKind)i, c[i]);
            if (added > 0) got.Add(new CostItem((ResourceKind)i, added));
        }
        return got.ToArray();
    }

    /// <summary>Поглощение конкретным фрагментом: масса и бонус цепочки достаются ему.</summary>
    public double OnAbsorbAt(int fragmentId, string speciesId, double mass) =>
        OnAbsorbAt(fragmentId, speciesId, mass, SpawnVariant.None);

    /// <summary>Поглощение конкретным фрагментом с вариантом (мерцание даёт ×1.5 ресурсов).</summary>
    public double OnAbsorbAt(int fragmentId, string speciesId, double mass, SpawnVariant variant) {
        _creditId = Fragments.IndexOf(fragmentId) >= 0 ? fragmentId : -1;
        double saved = _shimmeringYieldMult;
        if (variant == SpawnVariant.Shimmering) _shimmeringYieldMult = 1.5;
        LastAbsorbVariant = variant;
        try { return OnAbsorbEx(speciesId, mass); }
        finally { _shimmeringYieldMult = saved; _creditId = -1; }
    }

    /// <summary>Удар по конкретному фрагменту. Возвращает true, если фрагмент растворился.</summary>
    public bool OnMassLostAt(int fragmentId, double amount, string sourceId) {
        if (amount <= 0.0) return false;
        amount *= DamageTakenFactor;
        if (fragmentId != Fragments.LeadId) amount *= CloneDamageFactor;
        bool dissolved = Fragments.Remove(fragmentId, amount);
        LastDropped = new CostItem[0];
        if (dissolved && _carry.TryGetValue(fragmentId, out var lost)) {
            _carry.Remove(fragmentId);
            var drop = new List<CostItem>();
            for (int i = 0; i < lost.Length; i++) if (lost[i] > 0) drop.Add(new CostItem((ResourceKind)i, lost[i]));
            LastDropped = drop.ToArray();
        }
        if (dissolved) DepositLead(); // растворился ведущий — новый ведущий сдаёт свою ношу
        if (!IsDead && Mass < SizeRule.MinViableMass) {
            IsDead = true;
            _killedBy = sourceId;
            CountGuardianDefeat(sourceId); // v0.11
        }
        return dissolved;
    }

    public double FragmentMass(int id) => Fragments.MassOf(id);

    private double DamageTakenFactor {
        get { double k = 1.0 + (Evolution != null ? Evolution.Sum(EvoEffectKind.DamageTakenMult) : 0); return k < 0.2 ? 0.2 : k; }
    }

    /// <summary>Развить узел дерева: за ресурсы или жетоном. Пересчитывает лимиты (заряды, фрагменты, хранилище).</summary>
    public bool TryEvolve(string nodeId, bool free) {
        if (IsDead || IsVictory || Evolution == null) return false;
        bool hadCrystal = Mutations.Stacks("m_crystal") > 0;
        bool ok = free ? Evolution.TryUnlockFree(nodeId, _known, Mutations)
                       : Evolution.TryUnlock(nodeId, Resources, _known, Mutations);
        if (!ok) return false;
        if (!hadCrystal && Mutations.Stacks("m_crystal") > 0) Mass *= CrystalMassBonusMult;
        if (Mass > PeakMass) PeakMass = Mass;
        RecomputeLimits();
        return true;
    }

    /// <summary>Поднять показатель на уровень за ресурсы из хранилища.</summary>
    public bool TryTrain(string key) {
        if (IsDead || IsVictory || Training == null) return false;
        if (!Training.TryTrain(key, Resources, Evolution)) return false;
        RecomputeLimits();
        return true;
    }

    public TrainBlock CanTrain(string key) => Training == null ? TrainBlock.Unknown : Training.CanTrain(key, Resources, Evolution);

    private void RecomputeLimits() {
        Mutations.ExtraDamageMult = 1.0 + Evolution.Sum(EvoEffectKind.DamageDealtMult);
        int maxDash = Mutations.DashCharges + _meta.BonusDashCharges + (int)System.Math.Round(Evolution.Sum(EvoEffectKind.MaxDash));
        if (maxDash > MaxDashCharges) DashCharges += maxDash - MaxDashCharges;
        MaxDashCharges = maxDash;
        if (DashCharges > MaxDashCharges) DashCharges = MaxDashCharges;
        Resources.Capacity = ResourceStore.BaseCapacity + Evolution.Sum(EvoEffectKind.StorageCap);
        if (OpenWorld) Fragments.MaxCount = HasAbility("split") ? 2 + (int)System.Math.Round(Evolution.Sum(EvoEffectKind.MaxFragments)) : 1;
        double minMass = FragmentSet.MinFragmentMass + Evolution.Sum(EvoEffectKind.SplitMinMass);
        Fragments.MinMass = minMass < 0.6 ? 0.6 : minMass;
    }

    /// <summary>Показатели героя с разбором источников (для экрана «Показатели»).</summary>
    public List<StatLine> Stats() => StatSheet.Build(Mass, Fragments.Count, DashCharges, Mutations, Evolution, _meta);

    /// <summary>Учитывает лучший удар игрока (для BestHit в LifeImprint).</summary>
    public void OnHit(double dealtMassLoss) {
        if (dealtMassLoss > _bestHit) _bestHit = dealtMassLoss;
    }

    /// <summary>Регенерация массы и восстановление зарядов (1 за 1.2 с).</summary>
    public void Tick(double dt) {
        if (dt <= 0.0 || IsDead) return;
        Combo.Tick(dt);
        double regen = (Mutations.RegenPerSec + (Evolution != null ? Evolution.Sum(EvoEffectKind.RegenFlat) : 0)) * dt;
        double cloneRegen = Evolution != null ? Evolution.Sum(EvoEffectKind.CloneRegen) * dt : 0;
        if (cloneRegen > 0) for (int i = 1; i < Fragments.Count; i++) Fragments.Add(Fragments[i].Id, cloneRegen);
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
        if (OpenWorld) {
            _roomsCleared++;
            if (Depth >= MaxDepth) { IsVictory = true; return true; }
            Depth++;
            SetupWorld();
            RoomAbsorbed = 0.0;
            return true;
        }
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

    /// <summary>Открытый мир: огромная карта глубины; Room/Layout строятся из неё для совместимости.</summary>
    private void SetupWorld() {
        World = WorldGenerator.Generate(Depth, _rng.NextUInt(), _meta.Nemeses);
        var biome = BiomeCatalog.ForDepth(Depth);
        Room = new RoomPlan {
            Depth = Depth, RoomIndex = 0,
            HalfWidth = World.Width * 0.5, HalfHeight = World.Height * 0.5,
            Spawns = World.Spawns, GoalMass = World.GoalMass,
            ExitPos = World.ExitPos, PlayerStart = World.PlayerStart,
            Darkness01 = biome.Darkness01
        };
        bool boss = Depth >= MaxDepth;
        Layout = new RoomLayout {
            Kind = boss ? RoomKind.Boss : RoomKind.Normal, Biome = biome,
            Obstacles = World.Obstacles, Title = biome.Name, Hint = biome.Subtitle, AltarPos = Vec2.Zero
        };
        Boss = null; // v0.11: в открытом мире стражей всех глубин ведёт GuardianState
        var gdef = GuardianCatalog.ForDepth(Depth);
        GuardianFormulaHp = System.Math.Max(8.0, World.GoalMass * gdef.HpMult);
        GuardianMaxHp = GuardianFormulaHp *
            (GuardianDefeats(Depth) >= GuardianPityAfterDefeats ? GuardianPityMult : 1.0);
        Guardian = new GuardianState(gdef, GuardianMaxHp, _rng.NextUInt());
        GuardianAwake = false;
        _guardianAbsorbed = false;
        _altarUsed = new bool[World.Altars.Count];
        _regionsSeen.Clear();
        AltarUsed = false;
        _bossAbsorbed = false;
    }

    public bool IsAltarUsed(int index) => index < 0 || index >= _altarUsed.Length || _altarUsed[index];

    /// <summary>Алтарь открытого мира: Shrine — дар, Spring — +35% массы, Relic — дар + 15 эссенции.</summary>
    public void UseAltarAt(int index) {
        if (World == null || IsAltarUsed(index) || IsDead || IsVictory) return;
        var a = World.Altars[index];
        if (a.Kind == AltarKind.Spring) {
            double target = Mass * (1.0 + SpringMassBonus);
            if (target > PeakMass) target = PeakMass;
            if (target > Mass) Mass = target;
        } else {
            Evolution.FreeTokens++;                         // святилище и реликвия — бесплатный узел
            _alchemyCharges += MaxTransmutesPerShrine;      // v0.10: и три обмена «Алхимии»
            if (a.Kind == AltarKind.Relic) {
                _relicEssence += RelicEssence;
                Resources.Add(ResourceKind.Psyche, 10);
                Resources.Add(ResourceKind.Crystal, 6);
            } else Resources.Add(ResourceKind.Organic, 8);
        }
        _altarUsed[index] = true;
    }

    /// <summary>Первое посещение региона; исследование вознаграждается эссенцией в итоге жизни.</summary>
    public void OnRegionEntered(int regionId) {
        if (regionId < 0 || !_regionsSeen.Add(regionId)) return;
        _regionsTotal++;
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
        // v0.10: алхимия ресурсов — сброс на каждой новой комнате.
        _transmutesThisRoom = 0;
        LastTransmute = null;
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
        double essence = (OpenWorld ? 20.0 : 10.0) * (Depth - 1)
                       + 3.0 * _roomsCleared
                       + 0.5 * _absorbedCount
                       + 5.0 * newSpecies;
        if (IsVictory) essence += VictoryEssence;
        essence += NemesisEssence * _nemesisAbsorbedCount;
        essence += _bonusEssence;
        essence += _relicEssence + _regionsTotal / 3;
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
            RegionsDiscovered = _regionsTotal,
        };
        return _finished;
    }

    private void SyncOffers() {
        while (_nextThreshold < GrowthThresholds.Length && PeakMass >= GrowthThresholds[_nextThreshold]) {
            _nextThreshold++;
            _offersDue++;
        }
        if (OpenWorld && Evolution != null) {
            // v0.8: рост даёт жетон эволюции — игрок сам выбирает узел в дереве
            if (_offersDue > 0) { Evolution.FreeTokens += _offersDue; _offersDue = 0; }
            return;
        }
        if (PendingOffer == null && _offersDue > 0) {
            _offersDue--;
            PendingOffer = MakeOffer();
            // все мутации уже взяты до предела — пустое «выберите 1 из 0» не показываем
            if (PendingOffer.Options.Length == 0) { PendingOffer = null; _offersDue = 0; }
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
