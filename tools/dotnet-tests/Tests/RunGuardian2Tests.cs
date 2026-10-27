using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

/// <summary>
/// v0.12, этап 2: свои механики Горнила (Г2), Хора Бездны (Г3) и Гранильщика (Г4).
/// Фазы по порогам, телеграф перед ударом, деление/слияние капель, кипящее кольцо,
/// ток/оглушение/окно ядра, блок осколком, осколочный шторм, зеркало-контрудар, детерминизм.
/// </summary>
public static class RunGuardian2Tests {

    private static GuardianInput Inp(double dt, double px = 3.0, double py = 0.0,
            bool dashed = false, double dx = 0.0, double dy = 0.0) {
        return new GuardianInput {
            SelfPos = Vec2.Zero, PlayerPos = new Vec2(px, py), PlayerMass = 10.0, Dt = dt,
            PlayerDashed = dashed, PlayerDashDir = new Vec2(dx, dy),
        };
    }

    /// <summary>Сбить стража до заданного HP мелкими ударами (Think не зовётся).</summary>
    private static void DamageToHp(GuardianState g, double targetHp) {
        int n = 0;
        while (g.Hp > targetHp && n++ < 200000) g.ApplyHit(9.0, 2.0, null, 0);
        True(g.Hp <= targetHp + 1e-9, "страж сбит до " + targetHp.ToString("0.##"));
    }

    private static int AliveParts(GuardianOutput o) {
        if (o.Parts == null) return 0;
        int n = 0;
        for (int i = 0; i < o.Parts.Length; i++) if (o.Parts[i].Hp > 0.0) n++;
        return n;
    }

    private static double PartsSum(GuardianOutput o) {
        double s = 0.0;
        for (int i = 0; i < o.Parts.Length; i++) s += o.Parts[i].Hp;
        return s;
    }

    // ---------- каталог: виды и пороги ----------

    public static void Catalog_NewKindsAndTunables() {
        Eq(GuardianKind.Forge, GuardianCatalog.ForDepth(2).Kind, "Г2 — Горнило");
        Eq(GuardianKind.AbyssChoir, GuardianCatalog.ForDepth(3).Kind, "Г3 — Хор Бездны");
        Eq(GuardianKind.Grinder, GuardianCatalog.ForDepth(4).Kind, "Г4 — Гранильщик");
        // телеграфы в окне 0.6–1.0 с (правило честности)
        Eq(1.0, GuardianCatalog.ForDepth(2).GeyserTelegraph, "Гейзеры: телеграф 1.0 с");
        Eq(0.6, GuardianCatalog.ForDepth(3).PulseTelegraph, "Импульс Хора: телеграф 0.6 с");
        Eq(0.9, GuardianCatalog.ForDepth(4).BeamTelegraph, "Луч Гранильщика: телеграф 0.9 с");
        Eq(0.6, GuardianCatalog.ForDepth(4).CounterDelay, "Контрудар через 0.6 с");
        // пороги фаз 66/33
        Eq(0.66, GuardianCatalog.ForDepth(2).PhaseThreshold2, "Горнило: фаза 2 с 66%");
        Eq(0.33, GuardianCatalog.ForDepth(2).PhaseThreshold3, "Горнило: фаза 3 с 33%");
        Eq(0.66, GuardianCatalog.ForDepth(3).PhaseThreshold2, "Хор: фаза 2 с 66%");
        Eq(0.33, GuardianCatalog.ForDepth(3).PhaseThreshold3, "Хор: фаза 3 с 33%");
        Eq(0.66, GuardianCatalog.ForDepth(4).PhaseThreshold2, "Гранильщик: фаза 2 с 66%");
        Eq(0.33, GuardianCatalog.ForDepth(4).PhaseThreshold3, "Гранильщик: фаза 3 с 33%");
        // ключевые числа
        Eq(3.0, GuardianCatalog.ForDepth(2).PartMergeEvery, "слияние после 3 с близости");
        Eq(0.30, GuardianCatalog.ForDepth(2).PartMergeBonus, "+30% от меньшей капли");
        Eq(0.40, GuardianCatalog.ForDepth(2).RingMinFrac, "кольцо сжимается до 40%");
        Eq(3.0, GuardianCatalog.ForDepth(3).DashLockSec, "оглушение рывка 3 с");
        Eq(4.0, GuardianCatalog.ForDepth(3).CoreOpenSec, "ядро открыто 4 с");
        Eq(0.05, GuardianCatalog.ForDepth(4).BlockMassFrac, "блок осколком: −5% массы герою");
        Eq("shard_crab", GuardianCatalog.ForDepth(4).MinionSpecies, "Ф2: миньоны — осколочные крабы");
        Eq(4, GuardianCatalog.ForDepth(4).MinionsPerWave, "Ф2: четыре осколочных краба");
    }

    public static void Phases_ByThresholds_NewGuardians() {
        // Горнило
        var f = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 41UL);
        f.Think(Inp(0.0));
        Eq(GuardianPhase.Phase1, f.Phase, "Горнило: 100% — фаза 1");
        DamageToHp(f, 30.0 * 0.60);
        f.Think(Inp(0.0));
        Eq(GuardianPhase.Phase2, f.Phase, "Горнило: 60% — фаза 2 (деление)");
        True(f.Think(Inp(0.0)).Parts != null, "в фазе 2 капли на выходе");
        DamageToHp(f, 30.0 * 0.32);
        f.Think(Inp(0.0));
        Eq(GuardianPhase.Phase3, f.Phase, "Горнило: 32% — фаза 3 (кипение)");
        // Хор
        var c = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 43UL);
        c.Think(Inp(0.0));
        Eq(GuardianPhase.Phase1, c.Phase, "Хор: 100% — фаза 1 (ток)");
        DamageToHp(c, 30.0 * 0.60);
        c.Think(Inp(0.0));
        Eq(GuardianPhase.Phase2, c.Phase, "Хор: 60% — фаза 2 (оглушение)");
        DamageToHp(c, 30.0 * 0.32);
        c.Think(Inp(0.0));
        Eq(GuardianPhase.Phase3, c.Phase, "Хор: 32% — фаза 3 (воронка)");
        // Гранильщик
        var g = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 45UL);
        g.Think(Inp(0.0));
        Eq(GuardianPhase.Phase1, g.Phase, "Гранильщик: 100% — фаза 1 (грани)");
        DamageToHp(g, 30.0 * 0.60);
        g.Think(Inp(0.0));
        Eq(GuardianPhase.Phase2, g.Phase, "Гранильщик: 60% — фаза 2 (шторм)");
        DamageToHp(g, 30.0 * 0.32);
        g.Think(Inp(0.0));
        Eq(GuardianPhase.Phase3, g.Phase, "Гранильщик: 32% — фаза 3 (зеркало)");
    }

    // ---------- телеграф предшествует удару ----------

    public static void Telegraph_PrecedesStrike_EveryNewGuardian() {
        // Горнило: гейзеры — 3 круга, телеграф 1.0 с
        var f = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 51UL);
        var of = f.Think(Inp(0.0));
        True(of.Telegraphs != null && of.Telegraphs.Length == 3, "три круга гейзеров");
        Eq(f.GeyserRadius, of.Telegraphs[0].Radius, "радиус круга гейзера = BodyR×1.1 (v0.14)");
        Eq(1.0, of.Telegraphs[0].Seconds, "окно уклонения 1.0 с");
        True(of.Telegraphs[0].Pos.Approx(new Vec2(3, 0), 1e-9), "первый круг — у героя");
        for (int i = 1; i < 3; i++)
            True((of.Telegraphs[i].Pos - new Vec2(3, 0)).Length <= 5.0 + 1e-9, "круг " + i + " вокруг героя");
        True(!of.Strike, "телеграф только предупреждение");
        var of2 = f.Think(Inp(1.0)); // окно истекло, герой на месте
        True(of2.Strike, "после телеграфа — удар");
        Eq(0.08, of2.StrikeDamageFrac, "гейзер: 8% массы героя");
        Eq(8.0, of2.Knockback.Length, "отброс 8 единиц скорости");
        // Хор: импульс — телеграф 0.6 с
        var c = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 53UL);
        var oc = c.Think(Inp(0.0));
        Eq(0.6, oc.Telegraph.Seconds, "Хор: телеграф 0.6 с");
        Eq(c.PulseRadius, oc.Telegraph.Radius, "Хор: кольцо импульса = BodyR×1.9 (v0.14: больше тела)");
        Eq(2.0, oc.Current.Length, "течение 2.0 ед/с²");
        True(!oc.Strike, "Хор: телеграф без удара");
        var oc2 = c.Think(Inp(0.6)); // герой в 3 — внутри кольца
        True(oc2.Strike, "Хор: удар по герою внутри кольца");
        Eq(0.08, oc2.StrikeDamageFrac, "Хор: 8% массы внутри");
        // Гранильщик: луч — телеграф 0.9 с
        var g = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 55UL);
        var og = g.Think(Inp(0.0, 4.0));
        Eq(0.9, og.Beam.Seconds, "Гранильщик: телеграф луча 0.9 с");
        Eq(g.BeamWidth, og.Beam.Width, "ширина луча = BodyR×0.13 (v0.14)");
        True(og.Beam.From.Approx(Vec2.Zero, 1e-9), "луч от стража");
        True(og.Beam.To.Approx(new Vec2(g.ArenaRadius * 1.3, 0), 1e-6), "луч в сторону героя (длина = арена×1.3)");
        True(!og.Strike && !og.BeamFlash, "Гранильщик: телеграф без удара");
        var og2 = g.Think(Inp(0.9, 4.0)); // герой на линии
        True(og2.BeamFlash, "вспышка удара лучом");
        True(og2.Strike, "луч бьёт героя на линии");
        Eq(0.10, og2.StrikeDamageFrac, "луч: 10% массы героя");
    }

    // ---------- Горнило: деление и слияние ----------

    public static void Forge_DivideIntoThreeParts() {
        var f = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 61UL);
        f.Think(Inp(0.0));
        DamageToHp(f, 30.0 * 0.60); // 60% — ниже 66%
        var o = f.Think(Inp(0.0));  // такт входа в фазу 2 — распад
        Eq(3, AliveParts(o), "распад на три капли");
        double sum = PartsSum(o);
        Eq(f.Hp, sum, "сумма капель = HP стража");
        True(o.Center.Length < 1e-6, "центроид капель в центре арены");
        True(o.Parts[0].Id != o.Parts[1].Id && o.Parts[1].Id != o.Parts[2].Id, "у капель разные id");
        // урон по конкретной капле (v0.14: формула от массы героя и √доли)
        double partHp = o.Parts[2].Hp;
        double exp = 0.025 * 10.0 * 8.0 * System.Math.Sqrt(10.0 / (10.0 + partHp));
        double loss = f.ApplyHit(8.0, 10.0, null, 0, o.Parts[2].Id);
        Eq(exp, loss, "удар по капле — формула по HP капли");
        var o2 = f.Think(Inp(0.0));
        Eq(partHp - exp, o2.Parts[2].Hp, "HP капли уменьшился");
        Eq(f.Hp, PartsSum(o2), "инвариант: сумма капель = HP стража");
        // старый ApplyHit без partId — бьёт первую живую каплю
        double hpBefore = f.Hp;
        f.ApplyHit(8.0, 10.0, null, 0);
        var o3 = f.Think(Inp(0.0));
        True(f.Hp < hpBefore, "старый ApplyHit тоже наносит урон в делении");
        Eq(f.Hp, PartsSum(o3), "инвариант сохранён");
    }

    public static void Forge_MergeAfter3sBonus30pct() {
        var f = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 63UL);
        f.Think(Inp(0.0));
        DamageToHp(f, 30.0 * 0.60);
        var o0 = f.Think(Inp(0.0, 0.5)); // распад; герой рядом — капли ползут к нему
        Eq(3, AliveParts(o0), "три капли после распада");
        double lastSum = PartsSum(o0);
        int lastCount = AliveParts(o0);
        double share = lastSum / 3.0; // капли равны — без урона в этом тесте
        int merges = 0;
        double bonus1 = 0.0, bonus2 = 0.0;
        var t = Inp(0.5, 0.5);
        for (int k = 0; k < 120 && merges < 2; k++) {
            var cur = f.Think(t);
            int count = AliveParts(cur);
            double sum = PartsSum(cur);
            if (count < lastCount) {
                double delta = sum - lastSum; // сумма растёт только от слияния
                True(delta > 0.0, "слияние добавляет HP (шаг " + k + ")");
                if (merges == 0) bonus1 = delta; else bonus2 = delta;
                merges++;
            }
            lastCount = count;
            lastSum = sum;
        }
        Eq(2, merges, "два слияния рядом с героем");
        Eq(0.30 * share, bonus1, "первое слияние: +30% меньшей (капли равны)");
        Eq(0.30 * share, bonus2, "второе слияние: +30% одиночной капли");
        var fin = f.Think(t);
        Eq(1, AliveParts(fin), "осталась одна капля");
        Eq(GuardianPhase.Phase3, f.Phase, "одна капля — она и есть страж, фаза 3");
        Eq(f.Hp, PartsSum(fin), "инвариант суммы после слияний");
        Eq(f.Hp, fin.Parts[0].Hp + fin.Parts[1].Hp + fin.Parts[2].Hp, "HP стража = HP оставшейся капли");
    }

    public static void Forge_RingShrinksTo40AndBurns() {
        var f = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 65UL);
        f.Think(Inp(0.0, 3.0));
        True(f.Think(Inp(0.0, 3.0)).ArenaRadiusNow == 0.0, "в фазах 1–2 кольца нет");
        DamageToHp(f, 30.0 * 0.32); // фаза 3: 32%
        var o0 = f.Think(Inp(0.0, 3.0));
        Eq(f.ArenaRadius, o0.ArenaRadiusNow, "кольцо на полную аренту (BodyR×4.5+2, v0.14)");
        True(!o0.RingBurn, "герой в 3 — внутри кольца, жара нет");
        // капли сливаются (герой рядом) — ждём одну каплю, затем проверяем сжатие
        var t = Inp(0.5, 3.0);
        int guardTicks = 0;
        while (AliveParts(f.Think(t)) > 1 && guardTicks++ < 120) { }
        True(guardTicks < 120, "капли слились в одну");
        Eq(GuardianPhase.Phase3, f.Phase, "одна капля — фаза 3, кольцо сжимается");
        double full = f.ArenaRadius;
        double prev = f.Think(t).ArenaRadiusNow;
        True(prev <= full + 1e-9 && prev > full * 0.4 + 1e-9, "кольцо между 40% и 100%");
        bool reachedFloor = false;
        for (int k = 0; k < 14; k++) {
            var o = f.Think(Inp(6.0, 3.0));
            double now = o.ArenaRadiusNow;
            True(now <= prev + 1e-9, "кольцо не расширяется (шаг " + k + ")");
            True(prev - now <= full * 0.1 + 1e-9, "шаг сжатия не больше 10% радиуса (шаг " + k + ")");
            if (now <= full * 0.4 + 1e-9) { reachedFloor = true; Eq(full * 0.4, now, "пол — 40% арены"); }
            prev = now;
        }
        True(reachedFloor, "кольцо дошло до 40% за 84 с");
        Eq(full * 0.4, f.Think(Inp(6.0, 3.0)).ArenaRadiusNow, "дальше не сжимается");
        // урон вне кольца: капля у героя (3,0), герой на противоположной стороне (−4,0) —
        // до кольца 4.0 далеко, а от стража всего 4 (не дальше радиуса сна 7)
        True(f.Think(Inp(0.1, -4.0, 0.0)).RingBurn, "вне кольца герой горит (1.0 массы/с — применит Unity)");
        True(!f.Think(Inp(0.1, 3.0, 0.0)).RingBurn, "внутри кольца жара нет");
    }

    public static void Forge_GeysersDodgeable() {
        var f = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 67UL);
        f.Think(Inp(0.0, 3.0));                    // круг у героя и вокруг
        var dodge = f.Think(Inp(1.0, -4.0, 0.0));  // ушёл за 7 от старой точки: до любого круга ≥ 2
        True(!dodge.Strike, "ушёл из кругов за 1 с — удара нет (кольца не лгут)");
        // герой остался в первом круге (чуть сместился) — удар с отбросом ОТ центра круга
        var f2 = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 69UL);
        f2.Think(Inp(0.0, 3.5));
        var hit = f2.Think(Inp(1.0, 3.0)); // внутри круга (0.5 < r гейзера), сбоку от центра
        True(hit.Strike, "герой в круге — удар");
        True(hit.Knockback.X < 0.0 && System.Math.Abs(hit.Knockback.Y) < 1e-9,
            "отброс от центра круга");
    }

    // ---------- Хор Бездны: ток, оглушение, воронка ----------

    public static void Choir_CurrentChangesEvery6s() {
        var a = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 71UL);
        var dirs = new Vec2[12];
        for (int k = 0; k < 12; k++) {
            var o = a.Think(Inp(1.0));
            dirs[k] = o.Current.Normalized();
            Eq(2.0, o.Current.Length, "сила течения 2.0 (такт " + k + ")");
        }
        for (int k = 1; k < 6; k++)
            True(dirs[k].Approx(dirs[0], 1e-9), "течение держит направление (такт " + k + ")");
        True(!dirs[6].Approx(dirs[5], 1e-9), "на 6 с направление сменилось");
        for (int k = 7; k < 12; k++)
            True(dirs[k].Approx(dirs[6], 1e-9), "новое направление держится (такт " + k + ")");
    }

    public static void Choir_CurrentDeterministicBySeed() {
        var a = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 73UL);
        var b = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 73UL);
        for (int k = 0; k < 20; k++) {
            var oa = a.Think(Inp(1.0));
            var ob = b.Think(Inp(1.0));
            True(oa.Current.Approx(ob.Current, 1e-9), "одинаковый сид — одинаковое течение (такт " + k + ")");
            Eq(oa.Telegraph.Seconds, ob.Telegraph.Seconds, "одинаковые импульсы (такт " + k + ")");
            True(oa.Telegraph.Pos.Approx(ob.Telegraph.Pos, 1e-9), "одинаковые центры импульсов (такт " + k + ")");
        }
    }

    public static void Choir_DashLockOnlyInPhase2Plus_CoreWindow4s() {
        // фаза 1: импульс есть, оглушения и окна нет
        var c = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 75UL);
        c.Think(Inp(0.0, 3.0));
        var strike1 = c.Think(Inp(0.6, 3.0));
        True(strike1.Strike, "фаза 1: импульс бьёт героя внутри кольца");
        Eq(0.0, strike1.DashLockSeconds, "фаза 1: оглушения нет после импульса");
        True(!strike1.WeakPointOpen, "фаза 1: ядро не открывается");
        // фаза 1: полный урон без шляпки (v0.14: 0.025×8×10×√(10/40))
        var fresh = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 77UL);
        fresh.Think(Inp(0.0));
        Eq(1.0, fresh.ApplyHit(8.0, 10.0, null, 0), "фаза 1: урон без модификаторов");
        // фаза 2: после импульса — оглушение 3 с и окно ядра 4 с
        DamageToHp(c, 30.0 * 0.55); // 55% — фаза 2
        c.Think(Inp(0.0, 3.0));
        var o = c.Think(Inp(0.5, 3.0));
        int waited = 0;
        while (o.Telegraph.Seconds <= 0.0 && waited++ < 40) o = c.Think(Inp(0.5, 3.0));
        True(o.Telegraph.Seconds > 0.0, "импульс в фазе 2 телеграфируется");
        var hit = c.Think(Inp(0.6, 3.0));
        True(hit.Strike, "импульс дотянулся");
        Eq(3.0, hit.DashLockSeconds, "оглушение: рывок невозможен 3 с");
        True(hit.WeakPointOpen, "ядро открыто после импульса");
        double expOpen = 0.025 * 8.0 * 10.0 * System.Math.Sqrt(10.0 / (10.0 + c.Hp)) * 1.5;
        Eq(expOpen, c.ApplyHit(8.0, 10.0, null, 0), "в окне ядра урон ×1.5");
        // оглушение тает за 3 с, окно ещё открыто на третьей секунде
        for (int k = 1; k <= 3; k++) {
            var t = c.Think(Inp(1.0, 3.0));
            Eq(3.0 - k, t.DashLockSeconds, "оглушение тает (с " + k + ")");
            True(t.WeakPointOpen, "ядро открыто на " + k + "-й секунде окна");
        }
        var closed = c.Think(Inp(1.0, 3.0)); // 4-я секунда — окно закрылось
        True(!closed.WeakPointOpen, "окно ядра закрылось после 4 с");
        Eq(0.0, closed.DashLockSeconds, "оглушение прошло");
        double expClosed = 0.025 * 8.0 * 10.0 * System.Math.Sqrt(10.0 / (10.0 + c.Hp)) * 0.5;
        Eq(expClosed, c.ApplyHit(8.0, 10.0, null, 0), "вне окна урон ×0.5");
    }

    public static void Choir_Phase3_FunnelAndFasterPulses() {
        var c = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 79UL);
        c.Think(Inp(0.0, 3.0));
        Eq(0.0, c.Think(Inp(0.1, 3.0)).Pull, "фаза 1: притяжения нет");
        DamageToHp(c, 30.0 * 0.32); // фаза 3
        var o = c.Think(Inp(0.1, 3.0));
        Eq(2.5, o.Pull, "воронка: притяжение 2.5 ед/с² к стражу");
        // доживаем импульс, созданный ещё в фазе 1 (кулдаун 5.0)
        var old = c.Think(Inp(0.6, 3.0));
        True(old.Strike, "старый импульс дошёл");
        // ждём новый телеграф — создан уже в фазе 3, кулдаун 3.5 с
        o = c.Think(Inp(0.5, 3.0));
        int waited = 0;
        while (o.Telegraph.Seconds <= 0.0 && waited++ < 40) o = c.Think(Inp(0.5, 3.0));
        True(o.Telegraph.Seconds > 0.0, "импульс фазы 3 телеграфируется");
        var strike = c.Think(Inp(0.6, 3.0));
        True(strike.Strike, "импульс фазы 3 дошёл");
        // интервал 3.5 с: новый телеграф через 2.9 с после удара (3.5 − 0.6)
        var early = c.Think(Inp(2.5, 3.0));
        Eq(0.0, early.Telegraph.Seconds, "2.5 с после удара — нового телеграфа ещё нет");
        var late = c.Think(Inp(0.5, 3.0));
        True(late.Telegraph.Seconds > 0.0, "через 3.0 с — новый импульс (интервал 3.5 с)");
        Eq(2.5, late.Pull, "воронка не отпускает");
    }

    // ---------- Гранильщик: осколки, блок, шторм, зеркало ----------

    public static void Grinder_ShardBlockBounceAndPenalty() {
        var g = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 81UL);
        var o = g.Think(Inp(0.0, 5.0)); // осколки на орбите: v0.14 — BodyR×2.2 ≈ 4.22
        Eq(4, o.Shards.Length, "четыре осколка на орбите");
        double orbit = g.ShardOrbitRadius;
        for (int i = 0; i < 4; i++)
            Eq(orbit, (o.Shards[i] - Vec2.Zero).Length, "осколок " + i + " на орбите BodyR×2.2");
        // герой в (5,0): линия к стражу проходит через осколок на оси X — блок
        double loss = g.ApplyHit(8.0, 10.0, null, 0, new Vec2(5, 0));
        Eq(0.0, loss, "осколок блокирует удар: урон 0");
        True(g.LastBlock, "флаг блока для Unity (отскок и −5% массы)");
        True(g.LastBlockBounce.Approx(new Vec2(1, 0), 1e-9), "отскок от осколка");
        // герой сбоку: линия не задевает осколок — обычный урон (v0.14: 0.025×8×10×√(10/40))
        double loss2 = g.ApplyHit(8.0, 10.0, null, 0, new Vec2(5, 3));
        Eq(1.0, loss2, "мимо осколка — формула удара без изменений");
        True(!g.LastBlock, "без блока флага нет");
        // орбита вращается: через 3 с — пол-оборота (1 оборот за 6 с)
        var o2 = g.Think(Inp(3.0, 5.0));
        True(o2.Shards[0].X < 0.0, "осколок ушёл на противоположную сторону орбиты");
    }

    public static void Grinder_Phase2_ShardsStorm() {
        var g = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 83UL);
        g.Think(Inp(0.0, 5.0));
        True(g.Think(Inp(0.0, 5.0)).Shards != null, "в фазе 1 осколки на орбите");
        DamageToHp(g, 30.0 * 0.60); // фаза 2
        var o = g.Think(Inp(0.0, 5.0));
        Eq(4, o.SpawnMinions, "осколочный шторм: 4 миньона");
        Eq("shard_crab", o.MinionSpecies, "вид миньона — осколочный краб");
        Eq(g.MinionMass, o.MinionMass, "масса краба = BodyR²×0.65 (v0.14: от радиуса тела)");
        True(o.Shards == null, "осколков на орбите больше нет");
        var o2 = g.Think(Inp(0.5, 5.0));
        Eq(0, o2.SpawnMinions, "шторм однократный");
        // блок больше не работает — осколков нет
        g.Think(Inp(0.0, 5.0));
        double loss = g.ApplyHit(8.0, 10.0, null, 0, new Vec2(5, 0));
        True(loss > 0.0, "без осколков удары не блокируются");
        True(!g.LastBlock, "флага блока нет");
    }

    public static void Grinder_BeamLockedAtTelegraph() {
        var g = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 85UL);
        g.Think(Inp(0.0, 4.0));                  // луч вдоль оси X на героя
        var miss = g.Think(Inp(0.9, 4.0, 2.0));  // герой сошёл с линии за 0.9 с
        True(!miss.Strike, "сошёл с линии луча — не попал (кольца не лгут)");
        True(miss.BeamFlash, "вспышка луча была");
    }

    public static void Grinder_Phase3_MirrorCounterDash() {
        var g = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 87UL);
        g.Think(Inp(0.0, 5.0));
        DamageToHp(g, 30.0 * 0.32); // фаза 3
        g.Think(Inp(0.0, 5.0));
        // рывок героя вдоль X — страж запоминает и через 0.6 с бьёт тем же направлением
        var arm = g.Think(Inp(0.2, 5.0, 0.0, true, 1.0, 0.0));
        True(!arm.Impulse, "контрудар не мгновенный");
        var mid = g.Think(Inp(0.3, 5.0));
        True(!mid.Impulse, "через 0.5 с после рывка — ещё ждёт");
        var hit = g.Think(Inp(0.3, 5.0));
        True(hit.Impulse, "через 0.6 с после рывка — контрудар");
        True(hit.Direction.Approx(new Vec2(1, 0), 1e-9), "контрудар тем же направлением, что рывок");
        Eq(1.0, hit.Strength01, "контрудар в полную силу");
        // новый рывок перезаряжает зеркало
        g.Think(Inp(0.1, 5.0, 0.0, true, 0.0, -1.0));
        var hit2 = g.Think(Inp(0.6, 5.0));
        True(hit2.Impulse, "новый рывок — новый контрудар");
        True(hit2.Direction.Approx(new Vec2(0, -1), 1e-9), "направление последнего рывка");
        // в фазе 1 зеркала нет
        var g2 = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 89UL);
        g2.Think(Inp(0.0, 5.0));
        var noCounter = g2.Think(Inp(0.7, 5.0, 0.0, true, 1.0, 0.0));
        True(!noCounter.Impulse, "фаза 1: рывок героя не вызывает контрудара");
        var later = g2.Think(Inp(0.6, 5.0));
        True(!later.Impulse, "фаза 1: контрудара нет вовсе");
    }

    // ---------- детерминизм ----------

    public static void Determinism_BySeed_AllThree() {
        // Горнило: гейзеры и капли
        var f1 = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 91UL);
        var f2 = new GuardianState(GuardianCatalog.ForDepth(2), 30.0, 91UL);
        DamageToHp(f1, 19.0);
        DamageToHp(f2, 19.0);
        for (int k = 0; k < 30; k++) {
            var o1 = f1.Think(Inp(0.5, 0.5));
            var o2 = f2.Think(Inp(0.5, 0.5));
            for (int i = 0; i < o1.Telegraphs.Length; i++) {
                True(o1.Telegraphs[i].Pos.Approx(o2.Telegraphs[i].Pos, 1e-9),
                    "гейзеры одинаковы (такт " + k + ", круг " + i + ")");
                Eq(o1.Telegraphs[i].Seconds, o2.Telegraphs[i].Seconds, "таймеры гейзеров (такт " + k + ")");
            }
            if (o1.Parts != null) {
                for (int i = 0; i < o1.Parts.Length; i++) {
                    Eq(o1.Parts[i].Id, o2.Parts[i].Id, "id капель (такт " + k + ")");
                    True(o1.Parts[i].Pos.Approx(o2.Parts[i].Pos, 1e-9), "позиции капель (такт " + k + ")");
                    Eq(o1.Parts[i].Hp, o2.Parts[i].Hp, "HP капель (такт " + k + ")");
                }
            }
        }
        // Гранильщик: осколки и луч
        var g1 = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 93UL);
        var g2 = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 93UL);
        for (int k = 0; k < 30; k++) {
            var o1 = g1.Think(Inp(0.5, 4.0));
            var o2 = g2.Think(Inp(0.5, 4.0));
            if (o1.Shards != null)
                for (int i = 0; i < o1.Shards.Length; i++)
                    True(o1.Shards[i].Approx(o2.Shards[i], 1e-9), "осколки одинаковы (такт " + k + ")");
            True(o1.Beam.From.Approx(o2.Beam.From, 1e-9), "луч: начало (такт " + k + ")");
            True(o1.Beam.To.Approx(o2.Beam.To, 1e-9), "луч: конец (такт " + k + ")");
            Eq(o1.Beam.Seconds, o2.Beam.Seconds, "луч: таймер (такт " + k + ")");
        }
    }

    // ---------- сон и сброс не сломаны ----------

    public static void Dormant_AndRetreat_StillWorkForNewGuardians() {
        var c = new GuardianState(GuardianCatalog.ForDepth(3), 30.0, 95UL);
        var far = c.Think(Inp(1.0, 20.0)); // 20 > 7 — спит
        True(far.Telegraph.Seconds == 0.0 && far.Current.Length == 0.0, "во сне Хор не поёт и не течёт");
        Eq(GuardianPhase.Dormant, c.Phase, "вдали — дремлет");
        // отступление в фазе 1: один прыжок из арены за границу ×1.5 (v0.14: от поверхностей тел)
        var g = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 97UL);
        g.Think(Inp(0.0, 5.0));
        DamageToHp(g, 27.0); // 90% — фаза 1
        // арена BodyR×4.5+2 ≈ 10.63, граница ×1.5 ≈ 15.9; герой в 20: surf = 20 − 1.82 − 1.11 = 17.1
        var reset = g.Think(Inp(0.1, 20.0));
        True(reset.ResetFight, "выход из арены в фазе 1 — сброс боя");
        Eq(30.0, g.Hp, "HP восстановлен");
        Eq(GuardianPhase.Phase1, g.Phase, "страж снова в фазе 1");
        // внутри зоны сброса (14 → surf ≈ 11.0 < 15.9) бой не сбрасывается
        var g2 = new GuardianState(GuardianCatalog.ForDepth(4), 30.0, 99UL);
        g2.Think(Inp(0.0, 5.0));
        var inside = g2.Think(Inp(0.1, 14.0));
        True(!inside.ResetFight, "внутри зоны сброса бой продолжается");
        // после сброса осколки выставляются заново
        var back = g.Think(Inp(0.0, 5.0));
        True(back.Shards != null && back.Shards.Length == 4, "осколки после сброса на месте");
    }
}
}
