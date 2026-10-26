using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunStateTests {

    public static void Initial_FreshRunState() {
        var st = new RunState(new SoulMeta(), 123UL);
        Eq(1, st.Depth, "старт с глубины 1");
        Eq(0, st.RoomIndex, "старт с комнаты 0");
        Eq(4.0, st.Mass, "стартовая масса 4.0");
        Eq(4.0, st.PeakMass, "пик = старту");
        Eq(0.0, st.RoomAbsorbed, "в комнате ничего не поглощено");
        True(!st.ExitOpen, "выход закрыт (цель >= 2)");
        True(!st.IsDead, "жив");
        True(!st.IsVictory, "не победа");
        True(st.PendingOffer == null, "предложения нет (4 < 6)");
        Eq(1, st.DashCharges, "базовый заряд один");
        Eq(1, st.MaxDashCharges, "максимум один");
        True(st.Room != null, "комната сгенерирована");
        True(st.Mutations != null, "мутации созданы");
        Eq(0, st.Mutations.ToArray().Length, "мутаций нет");
    }

    public static void Start_VitalityGivesMassBonus() {
        var meta = new SoulMeta { Essence = 1000 };
        True(meta.TryUpgrade(SoulStat.Vitality), "апгрейд за эссенцию");
        meta.TryUpgrade(SoulStat.Vitality); meta.TryUpgrade(SoulStat.Vitality);
        Eq(3, meta.Level(SoulStat.Vitality), "уровень живучести 3");
        Eq(1.8, meta.StartMassBonus, "бонус массы 0.6×3");
        var st = new RunState(meta, 5UL);
        Eq(5.8, st.Mass, "стартовая масса 4.0 + 1.8");
        Eq(5.8, st.PeakMass, "пик = старту");
    }

    public static void TryDash_SpendsChargeAndMass() {
        var st = new RunState(new SoulMeta(), 7UL);
        True(st.TryDash(), "рывок удался");
        Eq(0, st.DashCharges, "заряд потрачен");
        Eq(3.9, st.Mass, "масса 4.0 − 0.1 за рывок");
        True(!st.TryDash(), "второй рывок без заряда невозможен");
        Eq(3.9, st.Mass, "масса не изменилась");
    }

    public static void DashCharges_RegenOnePer12Seconds() {
        var st = new RunState(new SoulMeta(), 7UL);
        st.TryDash();
        st.Tick(0.6);
        Eq(0, st.DashCharges, "0.6 c — заряда ещё нет");
        st.Tick(0.6);
        Eq(1, st.DashCharges, "1.2 c — заряд восстановлен");
        st.TryDash();
        st.Tick(1.19);
        Eq(0, st.DashCharges, "1.19 c — чуть-чуть не хватает");
    }

    public static void MaxDash_SoulBonusAndMutationStack() {
        var meta = new SoulMeta { Essence = 1000 };
        meta.TryUpgrade(SoulStat.Energy); meta.TryUpgrade(SoulStat.Energy);
        Eq(2, meta.Level(SoulStat.Energy), "энергия 2");
        Eq(1, meta.BonusDashCharges, "+1 заряд от души");
        var st = new RunState(meta, 11UL);
        Eq(2, st.MaxDashCharges, "максимум зарядов 2");
        Eq(2, st.DashCharges, "старт с полными");
        st.Mutations.Add("m_magnet"); st.Mutations.Add("m_magnet");
        st.Mutations.Add("m_light"); st.Mutations.Add("m_light");
        st.Mutations.Add("m_regrow"); st.Mutations.Add("m_regrow");
        st.OnAbsorb("beetle", 2.5); // 6.5 → порог 6 → предложение
        True(st.PendingOffer != null, "предложение появилось");
        True(Contains(st.PendingOffer.Options, "m_dash2"), "m_dash2 доступен (база не закрыта)");
        Eq(2, st.PendingOffer.Options.Length, "пул мал: только dash2 и ген жука");
        st.ChooseMutation("m_dash2");
        Eq(3, st.MaxDashCharges, "второй рывок: максимум 3 (2+1 души)");
        Eq(2, st.DashCharges, "текущие заряды не выросли мгновенно");
        st.Tick(0.6); st.Tick(0.6);
        Eq(3, st.DashCharges, "заряд восстановился до нового максимума");
    }

    public static void Death_BelowMinMassRemembersKiller() {
        var st = new RunState(new SoulMeta(), 21UL);
        st.OnMassLost(2.0, "spikeback");
        True(!st.IsDead, "2.0 — ещё жив");
        st.OnMassLost(1.05, "spikeback");
        True(st.IsDead, "0.95 < 1.0 — мёртв");
        var imprint = st.Finish();
        Eq("spikeback", imprint.KilledBy, "убийца запомнен");
        Eq(1, imprint.DepthReached, "достигнута глубина 1");
        Eq(0.0, imprint.EssenceEarned, "смерть сама ничего не даёт (D-19)");
    }

    public static void DeathByDash_AtMassBoundary() {
        var st = new RunState(new SoulMeta(), 33UL);
        st.OnMassLost(3.0, "mite"); // ровно 1.0 — ещё жив
        True(!st.IsDead, "1.0 — порог жизни");
        True(st.TryDash(), "рывок возможен");
        True(st.IsDead, "0.96 < 1.0 — смерть от рывка");
        Eq("dash", st.Finish().KilledBy, "маркер смерти от рывка");
    }

    public static void OnHit_TracksBestHit() {
        var st = new RunState(new SoulMeta(), 9UL);
        st.OnHit(0.3);
        st.OnHit(0.5);
        st.OnHit(0.2);
        st.OnHit(-1.0);
        Eq(0.5, st.Finish().BestHit, "лучший удар 0.5");
    }

    public static void GrowthOffer_ThreeDistinctAtThreshold() {
        var st = new RunState(new SoulMeta(), 13UL);
        True(st.PendingOffer == null, "до порога предложения нет");
        st.OnAbsorb("beetle", 2.5); // 6.5 >= 6
        True(st.PendingOffer != null, "порог 6 пересечён → предложение");
        var options = st.PendingOffer.Options;
        Eq(3, options.Length, "ровно 3 опции");
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < options.Length; i++) {
            True(seen.Add(options[i]), "опции различны: " + options[i]);
            True(st.Mutations.CanAdd(options[i]), "опция доступна: " + options[i]);
            True(OptionAllowed(options[i]), "опция из допустимого пула: " + options[i]);
        }
    }

    public static void GrowthOffer_QueuesAcrossTwoThresholds() {
        var st = new RunState(new SoulMeta(), 17UL);
        st.OnAbsorb("spore", 6.0); // 10.0: пороги 6 и 9 сразу
        True(st.PendingOffer != null, "первое предложение");
        var first = st.PendingOffer.Options;
        st.ChooseMutation(first[0]);
        True(st.PendingOffer != null, "второй порог ждал своей очереди");
        Eq(3, st.PendingOffer.Options.Length, "второе предложение тоже из 3 опций");
    }

    public static void GrowthOffer_PoolExcludesMaxedAndIncludesGenes() {
        var meta = new SoulMeta();
        meta.KnownGenes.Add("m_spikes");
        var st = new RunState(meta, 23UL);
        st.Mutations.Add("m_magnet"); st.Mutations.Add("m_magnet");
        st.Mutations.Add("m_light"); st.Mutations.Add("m_light");
        st.Mutations.Add("m_regrow"); st.Mutations.Add("m_regrow");
        st.OnAbsorb("spore", 2.5); // 6.5 → порог
        var options = st.PendingOffer.Options;
        Eq(2, options.Length, "доступны только dash2 и известный ген");
        True(Contains(options, "m_spikes"), "известный ген в предложении");
        True(Contains(options, "m_dash2"), "незакрытая база в предложении");
        True(!Contains(options, "m_magnet"), "максимум стаков исключён");
        True(!Contains(options, "m_light"), "максимум стаков исключён (лёгкость)");
        True(!Contains(options, "m_regrow"), "максимум стаков исключён (регенерация)");
    }

    public static void Choose_AppliesMutationAndClearsOffer() {
        var st = new RunState(new SoulMeta(), 29UL);
        st.OnAbsorb("beetle", 2.5);
        string id = st.PendingOffer.Options[0];
        st.ChooseMutation(id);
        Eq(1, st.Mutations.Stacks(id), "мутация применена");
        True(st.PendingOffer == null, "предложение снято");
        st.ChooseMutation("m_eyes"); // вне предложения
        Eq(0, st.Mutations.Stacks("m_eyes"), "выбор вне опций игнорируется");
    }

    public static void Choose_CrystalGives25PercentMass() {
        var meta = new SoulMeta();
        meta.KnownGenes.Add("m_crystal");
        var st = new RunState(meta, 31UL);
        st.Mutations.Add("m_magnet"); st.Mutations.Add("m_magnet");
        st.Mutations.Add("m_light"); st.Mutations.Add("m_light");
        st.Mutations.Add("m_regrow"); st.Mutations.Add("m_regrow");
        st.OnAbsorb("spore", 3.0); // 7.0
        True(Contains(st.PendingOffer.Options, "m_crystal"), "кристалл в предложении");
        st.ChooseMutation("m_crystal");
        Eq(8.75, st.Mass, "7.0 × 1.25 мгновенно");
        Eq(8.75, st.PeakMass, "пик обновлён");
        Eq(0.85, st.Mutations.DashSpeedMult, "скорость рывка ×0.85");
        True(st.PendingOffer == null, "8.75 < 9 — второго предложения нет");
    }

    public static void Exit_OpensOnlyAfterGoalMass() {
        var st = new RunState(new SoulMeta(), 37UL);
        True(!st.ExitOpen, "выход закрыт");
        True(!st.TryEnterExit(), "войти в выход нельзя");
        st.OnAbsorb("spore", 8.0); // больше любой цели глубины 1
        True(st.ExitOpen, "цель набрана — выход открыт");
    }

    public static void EnterExit_AdvancesRoomAndResetsProgress() {
        var st = new RunState(new SoulMeta(), 41UL);
        st.OnAbsorb("spore", 8.0);
        True(st.TryEnterExit(), "переход в следующую комнату");
        Eq(1, st.RoomIndex, "комната 1");
        Eq(0.0, st.RoomAbsorbed, "прогресс комнаты сброшен");
        Eq(1, st.Room.Depth, "вс ещё глубина 1");
        Eq(1, st.Room.RoomIndex, "план комнаты знает свой индекс");
        True(!st.ExitOpen, "новая комната — выход закрыт");
        True(!st.TryEnterExit(), "без цели не перейти");
    }

    public static void EnterExit_DepthAdvancesAfterThreeRooms() {
        var st = new RunState(new SoulMeta(), 43UL);
        for (int room = 0; room < 3; room++) {
            st.OnAbsorb("spore", st.Room.GoalMass);
            True(st.TryEnterExit(), "выход из комнаты " + room);
        }
        Eq(2, st.Depth, "после 3 комнат — глубина 2");
        Eq(0, st.RoomIndex, "снова комната 0");
        Eq(2, st.Room.Depth, "план комнаты глубины 2");
        Eq(3, st.Finish().RoomsCleared, "3 комнаты зачищено");
    }

    public static void Victory_AfterLastRoomOfMaxDepth() {
        var st = new RunState(new SoulMeta(), 47UL);
        int eliteExits = 0;
        for (int i = 0; i < 15; i++) {
            if (st.Boss != null) { // v0.5: глубина 5, комната 2 — босс
                Eq(14, i, "босс ждёт только в последней комнате");
                True(!st.ExitOpen, "выход закрыт, пока страж жив");
                st.OnBossAbsorbed();
                True(st.ExitOpen, "страж поглощён — выход открыт");
            } else {
                st.OnAbsorb("spore", st.Room.GoalMass);
                True(st.ExitOpen, "цель комнаты " + i + " набрана");
            }
            if (st.Layout.Kind == RoomKind.Elite) eliteExits++;
            True(st.TryEnterExit(), "выход " + i);
        }
        True(st.IsVictory, "15-я комната (глубина 5) пройдена");
        Eq(5, st.Depth, "глубина не растёт после 5");
        True(!st.TryEnterExit(), "после победы выходов нет");
        var imprint = st.Finish();
        Eq(5, imprint.DepthReached, "достигнута глубина 5");
        Eq(15, imprint.RoomsCleared, "15 комнат зачищено");
        Eq(0, imprint.Absorbed, "споры и страж не считаются поглощениями");
        Eq(1, imprint.NewSpecies, "спора — новый вид");
        Eq(155.0 + 4.0 * eliteExits, imprint.EssenceEarned, "40+45+5+25+40(страж)+4×элита");
        True(imprint.KilledBy == null, "победа — без убийцы");
        True(imprint.PeakMass >= 34.0, "масса выросла за забег");
        Eq(0, imprint.Mutations.Length, "мутации не выбирались");
        True(imprint.WardenSlain, "страж глубины повержен");
        Eq("Сердце пещеры", imprint.BiomeReached, "самый глубокий биом");
    }

    public static void Finish_IdempotentSameObject() {
        var st = new RunState(new SoulMeta(), 53UL);
        st.OnAbsorb("beetle", 2.5);
        var a = st.Finish();
        var b = st.Finish();
        True(System.Object.ReferenceEquals(a, b), "повторный Finish — тот же объект");
        Eq(a.EssenceEarned, b.EssenceEarned, "эссенция стабильна");
    }

    public static void Essence_ExactFormulaWithNemesis() {
        var meta = new SoulMeta();
        meta.Nemeses.Records.Add(new NemesisRecord { SoulName = "Искра", Level = 1 });
        var st = new RunState(meta, 59UL);
        // комната 1
        st.OnAbsorb("beetle", 2.5);
        st.OnAbsorb("beetle", 2.5);
        st.OnAbsorb("spore", 0.3);
        st.OnAbsorb("spore", 3.2); // итого 8.5 в комнате
        True(st.TryEnterExit(), "первая комната пройдена");
        // комната 2
        st.OnAbsorb("Искра", 3.0);   // немезида!
        st.OnAbsorb("mite", 0.9);
        st.OnAbsorb("spore", 3.0);   // 6.9 в комнате
        True(st.ExitOpen, "цель второй комнаты набрана");
        st.OnMassLost(25.0, "spikeback"); // смерть (v0.5: масса выше из-за бонуса цепочки)
        var imprint = st.Finish();
        Eq(1, imprint.DepthReached, "глубина 1");
        Eq(1, imprint.RoomsCleared, "одна комната");
        Eq(4, imprint.Absorbed, "2 жука + немезида + клещ (споры не в счёт)");
        Eq(3, imprint.NewSpecies, "жук, спора, клещ — новые");
        // v0.5: +2.8 бонуса цепочки поглощений (0.625 + 1.5 + 0.675)
        Eq(22.2, imprint.PeakMass, "пик: 4 + 8.5 + 6.9 + 2.8 бонуса цепочки");
        Eq(35.0, imprint.EssenceEarned, "3 + 2 + 15 + 15 = 35");
        Eq("spikeback", imprint.KilledBy, "убит шипастиком");
        True(meta.Nemeses.Records[0].Defeated, "немезида Искра повержена");
    }

    public static void NemesisAbsorb_GivesBonusEssence() {
        var meta = new SoulMeta();
        meta.Nemeses.Records.Add(new NemesisRecord { SoulName = "Тлен", Level = 3 });
        var st = new RunState(meta, 61UL);
        st.OnAbsorb("Тлен", 3.0);
        Eq(7.0, st.Mass, "масса немезиды усвоена");
        True(meta.Nemeses.Records[0].Defeated, "реестр отмечает поражение души");
        var imprint = st.Finish();
        Eq(1, imprint.Absorbed, "немезида — поглощение");
        Eq(15.5, imprint.EssenceEarned, "0.5 за поглощение + 15 за душу");
    }

    public static void Run_DeterministicBySeed() {
        var st1 = new RunState(new SoulMeta(), 2024UL);
        var st2 = new RunState(new SoulMeta(), 2024UL);
        st1.OnAbsorb("beetle", 2.5);
        st2.OnAbsorb("beetle", 2.5);
        var o1 = st1.PendingOffer.Options;
        var o2 = st2.PendingOffer.Options;
        Eq(o1.Length, o2.Length, "одинаковое число опций");
        bool same = true;
        for (int i = 0; i < o1.Length; i++) if (o1[i] != o2[i]) same = false;
        True(same, "опции предложения совпадают для одинакового seed");
        st1.OnAbsorb("spore", 8.0); st2.OnAbsorb("spore", 8.0);
        st1.TryEnterExit(); st2.TryEnterExit();
        True(st1.Room.Spawns.Count == st2.Room.Spawns.Count, "одинаковое число спавнов");
        True(st1.Room.Spawns[0].Pos.Approx(st2.Room.Spawns[0].Pos), "позиции совпадают");
        Eq(st1.Room.GoalMass, st2.Room.GoalMass, "цели совпадают");
    }

    public static void Tick_RegrowRestoresMass() {
        var st = new RunState(new SoulMeta(), 67UL);
        st.Mutations.Add("m_regrow"); st.Mutations.Add("m_regrow");
        st.Tick(5.0);
        Eq(4.2, st.Mass, "0.04/сек × 5 сек = +0.2");
        st.OnMassLost(1.0, "mite");
        st.Tick(10.0);
        Eq(3.6, st.Mass, "3.2 + 0.4 регенерации");
        Eq(1, st.DashCharges, "заряды не тратятся от Tick");
    }

    // ---------- v0.10: Пороги цепочки поглощений ----------

    public static void ComboMilestone3_RestoresDashCharge() {
        var st = new RunState(new SoulMeta(), 91UL);
        True(st.TryDash(), "рывок потрачен");
        Eq(0, st.DashCharges, "зарядов 0");
        Eq(0, st.LastComboMilestone, "порогов ещё не было");
        st.OnAbsorb("mite", 0.4);  // chain 1
        Eq(0, st.LastComboMilestone, "chain 1 — без порога");
        st.OnAbsorb("mite", 0.4);  // chain 2
        Eq(0, st.LastComboMilestone, "chain 2 — без порога");
        st.OnAbsorb("mite", 0.4);  // chain 3 → milestone
        Eq(3, st.LastComboMilestone, "chain 3 → порог 3");
        Eq(1, st.DashCharges, "заряд восстановлен через порог 3");
    }

    public static void ComboMilestone3_NoEffectWhenDashFull() {
        var st = new RunState(new SoulMeta(), 93UL);
        // Без TryDash: DashCharges = MaxDashCharges = 1 (базовый)
        Eq(1, st.DashCharges, "старт: 1 заряд");
        Eq(1, st.MaxDashCharges, "максимум: 1");
        st.OnAbsorb("mite", 0.4);  // chain 1
        st.OnAbsorb("mite", 0.4);  // chain 2
        st.OnAbsorb("mite", 0.4);  // chain 3 → milestone 3
        Eq(3, st.LastComboMilestone, "порог 3 сработал");
        Eq(1, st.DashCharges, "заряд уже полон — прироста нет");
    }

    // ---------- helpers ----------
    private static bool Contains(string[] arr, string id) {
        for (int i = 0; i < arr.Length; i++) if (arr[i] == id) return true;
        return false;
    }

    private static bool OptionAllowed(string id) {
        return id == "m_dash2" || id == "m_magnet" || id == "m_light" || id == "m_regrow" || id == "m_shell";
    }
}
}
