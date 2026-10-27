using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

/// <summary>
/// v0.11: фреймворк стражей — каталог, HP по формуле, пробуждение у шахты,
/// зоны/телеграф/окно уязвимости Спороносицы, отступление и жалость ×0.85.
/// </summary>
public static class RunGuardianTests {

    private static GuardianInput NearInput(double dt, double px = 3.0) {
        return new GuardianInput { SelfPos = Vec2.Zero, PlayerPos = new Vec2(px, 0), PlayerMass = 10.0, Dt = dt };
    }

    private static GuardianInput FarInput(double dt, double px = 20.0) {
        return new GuardianInput { SelfPos = Vec2.Zero, PlayerPos = new Vec2(px, 0), PlayerMass = 10.0, Dt = dt };
    }

    /// <summary>Сбить стража до заданного HP мелкими ударами (без Think — окно уязвимости не открывается).</summary>
    private static void DamageToHp(GuardianState g, double targetHp) {
        int n = 0;
        while (g.Hp > targetHp && n++ < 200000) g.ApplyHit(9.0, 2.0, null, 0);
        True(g.Hp <= targetHp + 1e-9, "страж сбит до " + targetHp.ToString("0.##"));
    }

    // ---------- каталог ----------

    public static void Catalog_FiveGuardians() {
        Eq(5, GuardianCatalog.All().Length, "пять стражей");
        string[] ids = { "spore_mother", "forge", "abyss_choir", "grinder", "pulsar" };
        string[] names = { "Спороносица", "Горнило", "Хор Бездны", "Гранильщик", "Пульсар" };
        double[] hpMults = { 1.3, 1.35, 1.4, 1.45, 1.5 };
        for (int d = 1; d <= 5; d++) {
            var def = GuardianCatalog.ForDepth(d);
            Eq(ids[d - 1], def.Id, "id глубины " + d);
            Eq(names[d - 1], def.Name, "имя глубины " + d);
            Eq(d, def.Depth, "глубина записи " + d);
            Eq(hpMults[d - 1], def.HpMult, "HpMult глубины " + d);
            True(def.ArenaMult > 0.0, "множитель арены задан");
            True(!string.IsNullOrEmpty(def.SpeciesId), "вид тела задан");
            True(def.WakeMult > 0.0, "множитель радиуса сна задан");
        }
        Eq("spore_mother", GuardianCatalog.ForDepth(0).Id, "ниже 1 — первый страж");
        Eq("pulsar", GuardianCatalog.ForDepth(9).Id, "выше 5 — последний страж");
        Eq(0.66, GuardianCatalog.ForDepth(1).PhaseThreshold2, "Спороносица: фаза 2 с 66%");
        Eq(0.33, GuardianCatalog.ForDepth(1).PhaseThreshold3, "Спороносица: фаза 3 с 33%");
        Eq(GuardianKind.SporeMother, GuardianCatalog.ForDepth(1).Kind, "Г1 — Спороносица");
        Eq(GuardianKind.Pulsar, GuardianCatalog.ForDepth(5).Kind, "Г5 — Пульсар");
    }

    // ---------- HP по формуле ----------

    public static void Formula_HpAtEveryDepth() {
        double[] hpMults = { 1.3, 1.35, 1.4, 1.45, 1.5 };
        for (int d = 1; d <= 5; d++) {
            var rs = RunAtDepth(d);
            double expected = System.Math.Max(8.0, rs.World.GoalMass * hpMults[d - 1]);
            Eq(expected, rs.GuardianFormulaHp, "формула HP, глубина " + d);
            Eq(expected, rs.Guardian.MaxHp, "страж создан с формульным HP, глубина " + d);
            Eq(expected, rs.Guardian.Hp, "HP стража = его масса, глубина " + d);
            True(!rs.GuardianAwake, "страж спит до цели глубины " + d);
            True(!rs.ExitOpen, "выход закрыт до стража, глубина " + d);
            True(rs.Guardian.Def.Depth == d, "деф стража соответствует глубине");
        }
    }

    // ---------- пробуждение и выход ----------

    public static void Wake_AtGoal_ExitOnlyAfterAbsorb() {
        var rs = new RunState(new SoulMeta(), 9UL, true);
        rs.OnAbsorb("spore", rs.Room.GoalMass * 0.5);
        True(!rs.GuardianAwake, "цель не набрана — страж спит");
        True(!rs.ExitOpen, "выход закрыт");
        rs.OnAbsorb("spore", rs.Room.GoalMass);
        True(rs.GuardianAwake, "цель набрана — страж пробуждается у шахты");
        True(!rs.ExitOpen, "страж жив — выхода нет");
        Expose(rs);
        double massBefore = rs.Mass;
        int tokensBefore = rs.Evolution.FreeTokens;
        rs.OnBossAbsorbed();
        True(rs.ExitOpen, "страж поглощён — выход открыт");
        True(rs.Mass > massBefore, "масса стража досталась герою");
        True(rs.Evolution.FreeTokens >= tokensBefore + 1, "жетон эволюции за стража");
        True(rs.Finish().WardenSlain, "победа над стражем записана");
        True(rs.GuardianAbsorbed, "флаг поглощения выставлен");
    }

    public static void EssenceTable_ByDepth() {
        double[] expect = { 8.0, 14.0, 22.0, 32.0, 40.0 };
        for (int d = 1; d <= 5; d++)
            Eq(expect[d - 1], RunState.GuardianEssenceFor(d), "эссенция за стража глубины " + d);
        Eq(8.0, RunState.GuardianEssenceFor(0), "ниже 1 — как Г1");
        Eq(40.0, RunState.GuardianEssenceFor(7), "выше 5 — как Г5");
    }

    public static void Absorb_IgnoredUntilExposed() {
        var rs = new RunState(new SoulMeta(), 9UL, true);
        rs.OnAbsorb("spore", rs.Room.GoalMass);
        True(rs.GuardianAwake, "страж пробуждён");
        double mass = rs.Mass;
        rs.OnBossAbsorbed(); // страж ещё не обнажён — отказ
        True(!rs.ExitOpen, "выход по-прежнему закрыт");
        Eq(mass, rs.Mass, "масса не изменилась");
    }

    // ---------- фазы Спороносицы ----------

    public static void SporeMother_PhasesByHp() {
        var g = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 1UL);
        Eq(GuardianPhase.Dormant, g.Phase, "вдали — дремлет");
        g.Think(NearInput(0.0));
        Eq(GuardianPhase.Phase1, g.Phase, "100% HP — фаза 1");
        DamageToHp(g, 20.0 * 0.60); // 60% — ниже 66%
        g.Think(NearInput(0.0));
        Eq(GuardianPhase.Phase2, g.Phase, "60% — фаза 2");
        DamageToHp(g, 20.0 * 0.32); // 32% — ниже 33%, выше 30%
        g.Think(NearInput(0.0));
        Eq(GuardianPhase.Phase3, g.Phase, "32% — фаза 3");
        True(!g.IsExposed, "32% — ещё не обнажена");
        DamageToHp(g, 20.0 * 0.29); // 29% — ниже 30%
        True(g.IsExposed, "29% — обнажена");
        g.Think(NearInput(0.0));
        Eq(GuardianPhase.Exposed, g.Phase, "ниже 30% — можно поглотить");
    }

    public static void SporeMother_Immobile() {
        var g = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 3UL);
        var o = g.Think(NearInput(0.0));
        True(!o.Impulse, "Спороносица не делает рывков");
        g.Think(NearInput(2.0));
        g.Think(NearInput(2.0));
        DamageToHp(g, 20.0 * 0.5);
        var o2 = g.Think(NearInput(1.0)); // фаза 2: корни, но движения нет
        True(!o2.Impulse, "и в фазе 2 неподвижна");
        True(g.Think(NearInput(1.0)).Impulse == false || g.Phase == GuardianPhase.Exposed,
            "движение только в обнажённой фазе");
    }

    // ---------- зоны ----------

    public static void SporeMother_ZonesShuffleDeterministic() {
        var a = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 77UL);
        var b = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 77UL);
        var oa = a.Think(NearInput(0.0));
        b.Think(NearInput(0.0));
        Eq(4, oa.Zones.Length, "четыре зоны спор");
        for (int i = 0; i < 4; i++) {
            Eq(a.ZoneRadius, oa.Zones[i].Radius, "радиус зоны " + i + " = BodyR×1.7 (v0.14)");
            Eq(0.4, oa.Zones[i].DamagePerSec, "урон зоны " + i);
            Eq(0.3, oa.Zones[i].Slow01, "замедление 30% в зоне " + i);
            True(oa.Zones[i].Pos.Length <= a.ArenaRadius * 0.85 + 1e-9, "зона " + i + " в границах арены");
        }
        var before = new Vec2[4];
        for (int i = 0; i < 4; i++) before[i] = oa.Zones[i].Pos;
        var o1 = a.Think(NearInput(4.0)); // 4 c — ещё те же места
        for (int i = 0; i < 4; i++) True(o1.Zones[i].Pos.Approx(before[i], 1e-9), "до 8 с зона " + i + " на месте");
        var o2 = a.Think(NearInput(4.0)); // 8 c — переставились
        bool moved = false;
        for (int i = 0; i < 4; i++) if (!o2.Zones[i].Pos.Approx(before[i], 1e-6)) moved = true;
        True(moved, "на 8 с зоны переставились");
        var ob2 = b.Think(NearInput(4.0)); // промежуточный такт — та же последовательность, что у a
        ob2 = b.Think(NearInput(4.0));     // 8 c — перестановка тем же генератором
        for (int i = 0; i < 4; i++) True(o2.Zones[i].Pos.Approx(ob2.Zones[i].Pos, 1e-9),
            "одинаковый сид — одинаковые зоны " + i);
        // детерминизм таймера: у обоих стражей зоны снова соберутся одновременно
        Eq(o2.Zones[0].Life, ob2.Zones[0].Life, "одинаковый срок жизни зон");
    }

    public static void SporeMother_ZonesDriftInPhase3() {
        var g = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 79UL);
        g.Think(NearInput(0.0, 5.0));
        DamageToHp(g, 20.0 * 0.32); // фаза 3
        var before = new Vec2[4];
        var o0 = g.Think(NearInput(0.0, 5.0)); // вход в фазу 3 — дрейф ещё не двигал зоны
        for (int i = 0; i < 4; i++) before[i] = o0.Zones[i].Pos;
        var o1 = g.Think(NearInput(1.0, 5.0));
        for (int i = 0; i < 4; i++) {
            double d0 = System.Math.Abs(5.0 - before[i].X);
            double d1 = System.Math.Abs(5.0 - o1.Zones[i].Pos.X);
            True(d1 <= d0 + 1e-9, "зона " + i + " приблизилась к игроку по X");
        }
    }

    // v0.14: зоны строились вокруг (0,0) карты — должны строиться вокруг стража
    public static void SporeZones_AroundGuardianNotMapOrigin() {
        var self = new Vec2(60.0, -35.0);
        var def = GuardianCatalog.ForDepth(1);
        True(self.Length > def.ArenaMult * 3.0, "фикстура: страж далеко от (0,0) карты");
        var g = new GuardianState(def, 20.0, 101UL);
        var o = g.Think(new GuardianInput { SelfPos = self, PlayerPos = self + new Vec2(3, 0), PlayerMass = 10.0, Dt = 0.0 });
        Eq(4, o.Zones.Length, "четыре зоны спор");
        double reach = g.ArenaRadius * 0.85 + g.ZoneRadius;
        for (int i = 0; i < o.Zones.Length; i++) {
            double fromGuardian = (o.Zones[i].Pos - self).Length;
            True(fromGuardian <= g.ArenaRadius + 1e-9,
                "зона " + i + " в пределах арены стража (60,−35): " + fromGuardian.ToString("0.##"));
            True(fromGuardian <= reach, "зона " + i + " внутри досягаемости перемешивания");
            True(fromGuardian > 0.5, "зона " + i + " не прилипла к центру стража");
        }
    }

    // ---------- окно уязвимости ----------

    public static void SporeMother_WeakPointWindow2of8() {
        var g = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 5UL);
        g.Think(NearInput(0.0));
        DamageToHp(g, 20.0 * 0.5); // фаза 2
        g.Think(NearInput(0.0));
        Eq(GuardianPhase.Phase2, g.Phase, "фаза 2");
        var t1 = NearInput(1.0);
        double openSec = 0.0;
        for (int s = 0; s < 16; s++) {
            var o = g.Think(t1);
            if (o.WeakPointOpen) openSec += 1.0;
        }
        Eq(4.0, openSec, "за 16 с окно открыто 4 с (2 из 8)");
    }

    public static void SporeMother_DamageMultCapClosedAndOpen() {
        var def = GuardianCatalog.ForDepth(1);
        double baseLoss = 0.025 * 8.0 * 10.0 * System.Math.Sqrt(10.0 / (10.0 + 20.0)); // v0.14: формула от √доли
        var closed = new GuardianState(def, 20.0, 7UL);
        closed.Think(NearInput(0.0)); // фаза 1 — шляпка закрыта всегда
        Eq(baseLoss * 0.5, closed.ApplyHit(8.0, 10.0, null, 0), "закрытая шляпка — урон ×0.5");
        var opened = new GuardianState(def, 20.0, 7UL);
        opened.Think(NearInput(0.0));
        DamageToHp(opened, 20.0 * 0.5); // фаза 2
        var o = opened.Think(NearInput(6.0)); // через 6 c окно открыто
        True(o.WeakPointOpen, "окно уязвимости открыто");
        double hpBefore = opened.Hp;
        double expOpen = 0.025 * 8.0 * 10.0 * System.Math.Sqrt(10.0 / (10.0 + hpBefore)) * 1.5;
        Eq(expOpen, opened.ApplyHit(8.0, 10.0, null, 0), "открытая шляпка — урон ×1.5");
    }

    // ---------- удар корней ----------

    public static void SporeMother_RootStrikeTelegraph() {
        var g = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 11UL);
        g.Think(NearInput(0.0)); // фаза 1
        var o1 = g.Think(NearInput(1.0));
        True(!o1.Strike, "в фазе 1 корни не бьют");
        True(o1.Telegraph.Radius == 0.0, "в фазе 1 телеграфа нет");
        DamageToHp(g, 20.0 * 0.5); // фаза 2
        g.Think(NearInput(0.0));   // вход в фазу 2: телеграф сразу
        var t = NearInput(1.0);
        var o2 = g.Think(t);       // 0.8 c истекли — удар
        True(o2.Strike, "телеграф дотянулся — удар корней");
        Eq(0.12, o2.StrikeDamageFrac, "урон 12% массы игрока");
        True(!o2.Impulse, "Спороносица при ударе неподвижна");
        var o3 = g.Think(t);
        True(o3.Telegraph.Radius == 0.0, "сразу нового телеграфа нет (кулдаун 4 с)");
        var o4 = g.Think(NearInput(3.0)); // ещё 3 c — суммарно 4 c
        Eq(g.RootRadius, o4.Telegraph.Radius, "новый телеграф через 4 с (радиус = BodyR×1.5, v0.14)");
        Eq(0.8, o4.Telegraph.Seconds, "окно уклонения 0.8 с");
    }

    // ---------- миньоны ----------

    public static void SporeMother_MinionsWave() {
        var g = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 13UL);
        var first = g.Think(NearInput(0.0));
        Eq(2, first.SpawnMinions, "первая волна — два плесеника");
        Eq("mite", first.MinionSpecies, "вид миньона — mite");
        Eq(g.MinionMass, first.MinionMass, "масса плесеника = BodyR²×0.65 (v0.14)");
        var t6 = NearInput(6.0);
        Eq(0, g.Think(t6).SpawnMinions, "6 c — волн нет");
        Eq(2, g.Think(t6).SpawnMinions, "12 c — новая волна");
    }

    public static void SporeMother_MinionsTwiceAsOftenInPhase3() {
        var g = new GuardianState(GuardianCatalog.ForDepth(1), 20.0, 15UL);
        g.Think(NearInput(0.0));
        DamageToHp(g, 20.0 * 0.32); // фаза 3 (>30%, чтобы не обнажилась)
        g.Think(NearInput(0.0));
        // ловим волну шагами по 0.5 c, затем проверяем интервал 6 c
        double waited = 0.0;
        while (waited < 20.0) {
            var o = g.Think(NearInput(0.5));
            waited += 0.5;
            if (o.SpawnMinions > 0) break;
        }
        True(waited < 20.0, "волна в фазе 3 произошла");
        var t5 = NearInput(5.0);
        Eq(0, g.Think(t5).SpawnMinions, "5 c — ещё рано (интервал 6 c)");
        var o2 = g.Think(NearInput(1.0));
        Eq(2, o2.SpawnMinions, "6 c — волна (вдвое чаще, чем 12 с)");
    }

    // ---------- отступление ----------

    public static void Retreat_Phase1Resets_Phase2DoesNot() {
        var def = GuardianCatalog.ForDepth(1); // арена BodyR×4.5+2
        var g = new GuardianState(def, 20.0, 17UL);
        g.Think(NearInput(0.0, 5.0)); // внутри арены — бой начался
        DamageToHp(g, 18.0);          // 90% — фаза 1
        var o = g.Think(FarInput(0.1, 17.0)); // v0.14: surf = 17 − 1.57 − 1.11 = 14.3 > арена×1.5 = 13.6
        True(o.ResetFight, "выход из арены в фазе 1 — сброс боя");
        Eq(20.0, g.Hp, "HP восстановлен");
        Eq(GuardianPhase.Phase1, g.Phase, "страж снова в фазе 1");
        g.Think(NearInput(0.0, 5.0)); // вернулся в арену
        var zonesBack = g.Think(NearInput(0.0, 5.0));
        True(zonesBack.Zones[0].Radius > 0.0, "зоны после сброса выставляются заново");
        DamageToHp(g, 8.0);           // 40% — фаза 2
        var o2 = g.Think(FarInput(0.1, 17.0));
        True(!o2.ResetFight, "в фазе 2 отступить нельзя");
        True(g.Hp < 8.0 + 1e-9, "HP не восстановился");
    }

    public static void Retreat_RunStatePenaltyAndPity() {
        var rs = new RunState(new SoulMeta(), 9UL, true);
        rs.OnAbsorb("spore", rs.Room.GoalMass + 1);
        True(rs.GuardianAwake, "страж пробуждён");
        double m0 = rs.Mass;
        rs.OnGuardianFightReset();
        Eq(m0 * 0.75, rs.Mass, "штраф −25% текущей массы");
        Eq(1, rs.GuardianDefeats(rs.Depth), "отступление — поражение");
        Eq(rs.GuardianFormulaHp, rs.Guardian.MaxHp, "после 1 поражения HP без скидки");
        rs.OnGuardianFightReset();
        Eq(2, rs.GuardianDefeats(rs.Depth), "второе поражение");
        Eq(rs.GuardianFormulaHp * 0.85, rs.Guardian.MaxHp, "после 2 поражений HP ×0.85");
        True(rs.Guardian.Hp <= rs.Guardian.MaxHp + 1e-9, "HP не выше нового максимума");
        True(rs.Guardian.Hp > 0.0, "страж жив и восстановлен");
    }

    public static void Defeat_CountedOnDeathByGuardian() {
        var rs = new RunState(new SoulMeta(), 9UL, true);
        rs.OnAbsorb("spore", rs.Room.GoalMass + 1);
        var def = GuardianCatalog.ForDepth(rs.Depth);
        rs.OnMassLost(rs.Mass, def.SpeciesId); // убил страж
        True(rs.IsDead, "герой мёртв");
        Eq(1, rs.GuardianDefeats(rs.Depth), "смерть от стража — поражение");
    }

    // ---------- Пульсар: совместимость ----------

    public static void Pulsar_GuardianMatchesLegacyBoss() {
        var g = new GuardianState(GuardianCatalog.ForDepth(5), 30.0, 21UL);
        var o = g.Think(NearInput(0.0));
        Eq(GuardianPhase.Phase1, g.Phase, "100% — фаза 1 (таран)");
        True(o.Impulse && o.Direction.Approx(new Vec2(1, 0)), "рывок к игроку");
        DamageToHp(g, 30.0 * 0.55); // 55% — фаза 2 по порогам 0.60/0.45
        var o2 = g.Think(NearInput(0.0));
        Eq(GuardianPhase.Phase2, g.Phase, "55% — фаза 2 (выводок)");
        Eq(3, o2.SpawnMinions, "выводок из трёх");
        Eq("mite", o2.MinionSpecies, "вид миньона — mite");
        Eq(g.MinionMass, o2.MinionMass, "масса миньона = BodyR²×0.65 (v0.14)");
        DamageToHp(g, 30.0 * 0.40); // 40% — фаза 3
        var o3 = g.Think(NearInput(0.0));
        Eq(GuardianPhase.Phase3, g.Phase, "40% — фаза 3 (ярость)");
        True(o3.Shockwave, "ударная волна в фазе 3");
        Eq(0, o3.Zones.Length, "у Пульсара нет зон");
        // формула удара v0.14 — на свежем страже (HP 30)
        var fresh = new GuardianState(GuardianCatalog.ForDepth(5), 30.0, 22UL);
        Eq(1.0, fresh.ApplyHit(8.0, 10.0, null, 0), "v0.14: 0.025×8×10×√(10/40)");
    }

    public static void Pulsar_BossFacadeStillWorks() {
        var boss = new BossState(30.0, 31UL);
        Eq(30.0, boss.Mass, "фасад: масса стража");
        Eq(30.0, boss.StartMass, "фасад: стартовая масса");
        var o = boss.Think(new BossInput { SelfPos = Vec2.Zero, PlayerPos = new Vec2(3, 0), PlayerMass = 10.0, Dt = 0.0 });
        Eq(BossPhase.Charge, boss.Phase, "фасад: Charge при 100%");
        True(o.Impulse, "фасад: рывок");
        // полный GuardiansState Пульсара и фасад ведут себя одинаково
        var g = new GuardianState(GuardianCatalog.ForDepth(5), 30.0, 31UL);
        var og = g.Think(NearInput(0.0));
        Eq(o.Impulse, og.Impulse, "одинаковое решение о рывке");
        True(o.Direction.Approx(og.Direction), "одинаковое направление");
    }

    // ---------- запуск на нужной глубине ----------

    private static RunState RunAtDepth(int depth) {
        var rs = new RunState(new SoulMeta(), 9UL, true);
        for (int d = 1; d < depth; d++) {
            rs.OnAbsorb("spore", rs.Room.GoalMass + 1);
            Expose(rs);
            rs.OnBossAbsorbed();
            True(rs.TryEnterExit(), "спуск на глубину " + (d + 1));
        }
        return rs;
    }

    private static void Expose(RunState rs) {
        int n = 0;
        while (!rs.Guardian.IsExposed && n++ < 200000) rs.Guardian.ApplyHit(9.0, 60.0, null, 0);
        True(rs.Guardian.IsExposed, "страж обнажён");
    }
}
}
