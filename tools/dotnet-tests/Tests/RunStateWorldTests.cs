using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunStateWorldTests {

    // ---------- Layout / биомы ----------

    public static void Layout_InitialRoomIsNormalFungal() {
        var st = new RunState(new SoulMeta(), 123UL);
        True(st.Layout != null, "раскладка сгенерирована");
        Eq(RoomKind.Normal, st.Layout.Kind, "первая комната — обычная");
        Eq(BiomeId.Fungal, st.Layout.Biome.Id, "биом глубины 1 — грибной");
        True(ReferenceEquals(st.Layout.Biome, BiomeCatalog.ForDepth(1)), "биом — тот же объект каталога");
        True(st.Layout.Obstacles.Count >= 3 && st.Layout.Obstacles.Count <= 7, "3–7 препятствий, есть " + st.Layout.Obstacles.Count);
        Eq("Грибной коридор", st.Layout.Title, "заголовок — имя биома");
        True(st.Layout.AltarPos.Approx(Vec2.Zero), "алтаря нет");
        True(st.Boss == null, "босса нет");
        True(st.Combo != null, "трекер цепочки создан");
        True(!st.AltarUsed, "алтарь не тронут");
        Eq(0, st.Combo.Chain, "цепочка пуста");
    }

    public static void Layout_BiomeMatchesDepthAcrossRun() {
        var st = new RunState(new SoulMeta(), 7UL);
        for (int i = 0; i < 14; i++) {
            Eq(st.Depth, st.Layout.Biome.Depth, "биом соответствует глубине (комната " + i + ")");
            True(ReferenceEquals(st.Layout.Biome, BiomeCatalog.ForDepth(st.Depth)), "биом из каталога");
            st.OnAbsorb("spore", st.Room.GoalMass);
            True(st.TryEnterExit(), "переход " + i);
        }
        Eq(BiomeId.Heart, st.Layout.Biome.Id, "последняя глубина — сердце пещеры");
    }

    // ---------- Combo ----------

    public static void Combo_WiredIntoAbsorbAndDash() {
        var st = new RunState(new SoulMeta(), 11UL);
        Eq(0.0, st.OnAbsorbEx("beetle", 2.0), "первое поглощение — без бонуса");
        Eq(1, st.Combo.Chain, "цепочка 1");
        Eq(0.5, st.OnAbsorbEx("beetle", 2.0), "второе — бонус 0.5");
        Eq(8.5, st.Mass, "4 + 2 + 2 + 0.5 бонуса");
        Eq(2, st.Combo.Best, "рекорд 2");
        True(st.TryDash(), "рывок");
        Eq(0, st.Combo.Chain, "рывок сбросил цепочку");
        Eq(2, st.Combo.Best, "рекорд жив");
        Eq(0.0, st.OnAbsorbEx("mite", 1.0), "новая цепочка начинается без бонуса");
        Eq(1, st.Combo.Chain, "цепочка 1");
    }

    public static void Combo_TickResetsChainInRun() {
        var st = new RunState(new SoulMeta(), 13UL);
        st.OnAbsorbEx("beetle", 2.0);
        st.OnAbsorbEx("mite", 0.8);
        Eq(2, st.Combo.Chain, "цепочка 2");
        st.Tick(2.5);
        Eq(0, st.Combo.Chain, "2.5 с без поглощений — цепочка погасла");
        Eq(2, st.Finish().BestCombo, "рекорд цепочки попал в итог жизни");
    }

    // ---------- испарение ----------

    public static void Evaporation_WarmBiomeDrainsMass() {
        var st = WalkToDepth(3UL, 2);
        Eq(BiomeId.Warm, st.Layout.Biome.Id, "глубина 2 — тёплые пустоты");
        double m0 = st.Mass;
        st.Tick(10.0);
        Eq(m0 * 0.96, st.Mass, "10 с × 0.004/сек = −4% массы");
        True(!st.IsDead, "испарение не убивает");
    }

    public static void Evaporation_HeatproofReduces() {
        var st = WalkToDepth(21UL, 2);
        double m0 = st.Mass;
        st.Mutations.Add("m_heatproof");
        st.Tick(10.0);
        Eq(m0 * (1.0 - 0.004 * 0.4 * 10.0), st.Mass, "жаростойкость: испарение ×0.4");
    }

    public static void Evaporation_NeverKillsAndFloorsAt105() {
        var st = WalkToDepth(33UL, 2);
        st.OnMassLost(st.Mass - 1.1, "vent");
        Eq(1.1, st.Mass, "масса 1.1");
        st.Tick(100.0);
        Eq(1.05, st.Mass, "испарение не опускает ниже 1.05");
        True(!st.IsDead, "жив несмотря на испарение");
    }

    // ---------- родник и святилище ----------

    public static void Spring_HealsOncePerRoom() {
        var st = FindKindAtSecondRoom(RoomKind.Spring);
        True(st != null, "родник найден среди seed");
        Eq(RoomKind.Spring, st.Layout.Kind, "это родник");
        True(!st.AltarUsed, "алтарь ещё не тронут");
        double m0 = st.Mass;
        st.OnMassLost(m0 * 0.5, "thorns");
        Eq(m0 * 0.5, st.Mass, "масса урезана для теста");
        st.UseAltar();
        True(st.AltarUsed, "родник использован");
        double expected = m0 * 0.5 * 1.35; // 35% от текущей; пик и так выше
        Eq(expected, st.Mass, "+35% массы от родника");
        double after = st.Mass;
        st.UseAltar();
        Eq(after, st.Mass, "второй раз родник не действует");
        True(st.AltarUsed, "флаг использования держится");
        True((st.Layout.AltarPos - st.Room.PlayerStart).Length >= 1.6 - 1e-6, "алтарь дальше 1.6 от старта");
        True((st.Layout.AltarPos - st.Room.ExitPos).Length >= 1.6 - 1e-6, "алтарь дальше 1.6 от выхода");
    }

    public static void Spring_CappedAtPeakMass() {
        var st = FindKindAtSecondRoom(RoomKind.Spring);
        True(st != null, "родник найден");
        double peak = st.PeakMass;
        st.OnMassLost(st.Mass - peak, "thorns"); // масса = пик
        Eq(peak, st.Mass, "масса равна пику");
        st.UseAltar();
        Eq(peak, st.Mass, "родник не поднимает массу выше пика");
        True(st.AltarUsed, "но считается использованным");
    }

    public static void Shrine_OfferOutsideThresholds() {
        var st = FindKindAtSecondRoom(RoomKind.Shrine);
        True(st != null, "святилище найдено среди seed");
        Eq(RoomKind.Shrine, st.Layout.Kind, "это святилище");
        while (st.PendingOffer != null && st.PendingOffer.Options.Length > 0)
            st.ChooseMutation(st.PendingOffer.Options[0]);
        True(st.PendingOffer == null || st.PendingOffer.Options.Length == 0, "пороговые предложения исчерпаны");
        st.UseAltar();
        True(st.AltarUsed, "алтарь использован");
        True(st.PendingOffer != null && st.PendingOffer.Options.Length >= 1, "дар алтаря предложен вне порогов");
        int n = st.PendingOffer.Options.Length;
        True(n <= 3, "не больше 3 опций, есть " + n);
        string first = st.PendingOffer.Options[0];
        int stacksBefore = st.Mutations.Stacks(first);
        st.UseAltar(); // повторно — предложения не дублируются
        Eq(n, st.PendingOffer.Options.Length, "опции не поменялись");
        st.ChooseMutation(first);
        Eq(stacksBefore + 1, st.Mutations.Stacks(first), "дар алтаря применён");
    }

    public static void UseAltar_DoesNothingInNormalRoom() {
        var st = new RunState(new SoulMeta(), 3UL);
        st.UseAltar();
        True(!st.AltarUsed, "в обычной комнате алтаря нет");
        Eq(4.0, st.Mass, "масса не изменилась");
        True(st.PendingOffer == null, "предложения не возникло");
    }

    // ---------- элита ----------

    public static void Elite_MassAndGoalMultiplied() {
        var st = FindKindAtSecondRoom(RoomKind.Elite);
        True(st != null, "элитная комната найдена среди seed");
        Eq(RoomKind.Elite, st.Layout.Kind, "это элита");
        Eq("Элитный зал", st.Layout.Title, "заголовок элитной комнаты");
        double massMult = 1.0 + 0.25 * (st.Depth - 1);
        for (int i = 0; i < st.Room.Spawns.Count; i++) {
            var s = st.Room.Spawns[i];
            var sp = SpeciesCatalog.Get(s.SpeciesId);
            if (s.SpeciesId == "spore") {
                Eq(0.3, s.Mass, "масса спор не меняется");
                continue;
            }
            if (s.SpeciesId == "soul_slime" && !s.IsNemesis) {
                double est = RoomGenerator.EstimatePlayerMass(st.Depth);
                True(s.Mass >= est * 0.9 * 1.6 - 1e-6 && s.Mass <= est * 1.1 * 1.6 + 1e-6,
                    "душеслизень ×1.6: " + s.Mass.ToString("0.##"));
                continue;
            }
            double lo = sp.BaseMass * massMult * 0.85 * 1.6;
            double hi = sp.BaseMass * massMult * 1.15 * 1.6;
            True(s.Mass >= lo - 1e-6 && s.Mass <= hi + 1e-6,
                "масса существа " + s.SpeciesId + " ×1.6: " + s.Mass.ToString("0.##") + " ∈ [" + lo.ToString("0.##") + ", " + hi.ToString("0.##") + "]");
        }
        True(st.Room.GoalMass >= 2.0 * 1.3 - 1e-6, "цель элитной комнаты ×1.3 (не ниже 2.6)");
    }

    public static void Elite_ExitGivesEssenceAndGuaranteedOffer() {
        // два одинаковых забега: A гибнет в элитной комнате, B выходит из неё и гибнет следом
        ulong seed = FindEliteSeedAtSecondRoom();
        True(seed > 0, "найден seed с элитной комнатой №2 (не последняя в глубине)");
        var a = new RunState(new SoulMeta(), seed);
        var b = new RunState(new SoulMeta(), seed);
        foreach (var st in new RunState[] { a, b }) {
            st.OnAbsorb("spore", st.Room.GoalMass);
            True(st.TryEnterExit(), "переход в элитную комнату");
            Eq(RoomKind.Elite, st.Layout.Kind, "комната 1 — элитная");
            st.OnAbsorb("spore", st.Room.GoalMass);
            True(st.ExitOpen, "цель набрана");
        }
        a.OnMassLost(1000.0, "spikeback");
        True(b.TryEnterExit(), "выход из элиты");
        True(b.PendingOffer != null, "гарантированное предложение после элиты");
        b.OnMassLost(1000.0, "spikeback");
        double ea = a.Finish().EssenceEarned;
        double eb = b.Finish().EssenceEarned;
        Eq(7.0, eb - ea, "разница: +3 комнаты и +4 эссенции за элитный выход");
    }

    // ---------- босс ----------

    public static void Boss_RoomSetupAndAbsorption() {
        var st = new RunState(new SoulMeta(), 47UL);
        int eliteExits = 0;
        for (int i = 0; i < 14; i++) {
            st.OnAbsorb("spore", st.Room.GoalMass);
            if (st.Layout.Kind == RoomKind.Elite) eliteExits++;
            True(st.TryEnterExit(), "выход " + i);
        }
        Eq(5, st.Depth, "глубина 5");
        Eq(2, st.RoomIndex, "комната 2 — логово босса");
        True(st.Boss != null, "страж создан");
        Eq(30.0, st.Boss.Mass, "масса стража 30");
        Eq(30.0, st.Boss.StartMass, "стартовая масса стража 30");
        Eq(6, st.Room.Spawns.Count, "в комнате босса только 6 спор");
        for (int i = 0; i < st.Room.Spawns.Count; i++)
            Eq("spore", st.Room.Spawns[i].SpeciesId, "спавн " + i + " — спора");
        Eq(RoomKind.Boss, st.Layout.Kind, "раскладка — боссовая");
        Eq(4, st.Layout.Obstacles.Count, "4 колонны по углам");
        True(!st.ExitOpen, "выход закрыт, пока страж жив");
        st.OnAbsorb("spore", 5.0);
        True(!st.ExitOpen, "споры не открывают выход в комнате босса");
        st.OnBossAbsorbed();
        True(st.ExitOpen, "страж поглощён — выход открыт");
        True(st.Finish().WardenSlain, "победа над стражем записана");
        var imprint = st.Finish();
        Eq(127.0 + 4.0 * eliteExits, imprint.EssenceEarned, "40 глубина + 42 комнаты + 5 вид + 40 страж + 4×элита");
    }

    public static void Boss_SecondCallIgnored() {
        var st = new RunState(new SoulMeta(), 47UL);
        for (int i = 0; i < 14; i++) { st.OnAbsorb("spore", st.Room.GoalMass); st.TryEnterExit(); }
        st.OnBossAbsorbed();
        double mass = st.Mass;
        double peak = st.PeakMass;
        st.OnBossAbsorbed(); // повторно — ничего
        Eq(mass, st.Mass, "масса не удвоилась");
        Eq(peak, st.PeakMass, "пик не изменился");
    }

    // ---------- вампиризм и Pool ----------

    public static void OnLifesteal_RequiresVampire() {
        var st = new RunState(new SoulMeta(), 25UL);
        st.OnLifesteal(2.0);
        Eq(4.0, st.Mass, "без вампиризма возврата нет");
        st.Mutations.Add("m_vampire");
        st.OnLifesteal(2.0);
        Eq(4.7, st.Mass, "вампиризм: +35% нанесённого урона");
        Eq(4.7, st.PeakMass, "пик обновлён");
    }

    public static void Heal_AddsMassWithoutRoomProgress() {
        var st = new RunState(new SoulMeta(), 29UL);
        st.OnAbsorb("spore", 2.0); // немного прогресса в комнате
        double absorbed = st.RoomAbsorbed;
        st.Heal(1.0);
        Eq(7.0, st.Mass, "4 + 2 (спора) + 1 (лечение)");
        Eq(absorbed, st.RoomAbsorbed, "лечение не двигает цель комнаты");
        Eq(7.0, st.PeakMass, "пик обновлён");
        var imprint = st.Finish();
        Eq(0, imprint.Absorbed, "лечение — не поглощение (спора тоже не в счёте)");
        st.Heal(-1.0);
        Eq(7.0, st.Mass, "отрицательное лечение игнорируется");
    }

    // ---------- итог жизни ----------

    public static void LifeImprint_NewFields() {
        var st = new RunState(new SoulMeta(), 61UL);
        st.OnAbsorbEx("beetle", 2.5);
        st.OnAbsorbEx("mite", 0.8);
        st.OnMassLost(100.0, "vent");
        var imprint = st.Finish();
        Eq("Грибной коридор", imprint.BiomeReached, "достигнут грибной биом");
        Eq(2, imprint.BestCombo, "лучшая цепочка — 2");
        True(!imprint.WardenSlain, "страж не повержен");
        Eq(2, imprint.Absorbed, "оба поглощения в счёте");
    }

    // ---------- helpers ----------

    /// <summary>Идёт к глубине depth (первая комната глубины). Спорами набирает цель.</summary>
    private static RunState WalkToDepth(ulong seed, int depth) {
        var st = new RunState(new SoulMeta(), seed);
        while (st.Depth < depth) {
            st.OnAbsorb("spore", st.Room.GoalMass);
            True(st.TryEnterExit(), "переход на пути к глубине " + depth);
        }
        return st;
    }

    /// <summary>Ищет seed, у которого вторая комната забега (глубина 1, комната 1) имеет заданный тип.</summary>
    private static RunState FindKindAtSecondRoom(RoomKind kind) {
        for (ulong seed = 1; seed <= 300; seed++) {
            var st = new RunState(new SoulMeta(), seed);
            st.OnAbsorb("spore", st.Room.GoalMass);
            if (!st.TryEnterExit()) continue;
            if (st.Layout.Kind == kind) return st;
        }
        return null;
    }

    private static ulong FindEliteSeedAtSecondRoom() {
        for (ulong seed = 1; seed <= 300; seed++) {
            var st = new RunState(new SoulMeta(), seed);
            st.OnAbsorb("spore", st.Room.GoalMass);
            if (!st.TryEnterExit()) continue;
            if (st.Layout.Kind == RoomKind.Elite) return seed;
        }
        return 0;
    }
}
}
