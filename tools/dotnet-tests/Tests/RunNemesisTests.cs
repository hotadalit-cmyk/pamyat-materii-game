using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunNemesisTests {

    public static void PickSoulName_FromCanonicalSet() {
        var reg = new NemesisRegistry();
        var canonical = new System.Collections.Generic.HashSet<string>
            { "Искра", "Осколок", "Эхо", "Тлен", "Капля", "Шёпот" };
        for (ulong seed = 1; seed <= 30; seed++) {
            string name = reg.PickSoulName(new PmRandom(seed * 131UL));
            True(canonical.Contains(name), "имя из канона: " + name);
        }
    }

    public static void PickSoulName_AvoidsTakenNames() {
        var reg = new NemesisRegistry();
        reg.Records.Add(new NemesisRecord { SoulName = "Искра", Level = 1 });
        for (ulong seed = 1; seed <= 30; seed++) {
            string name = reg.PickSoulName(new PmRandom(seed * 137UL));
            True(name != "Искра", "занятое имя не выдаётся: " + name);
        }
        reg.Records.Add(new NemesisRecord { SoulName = "Осколок" });
        reg.Records.Add(new NemesisRecord { SoulName = "Эхо" });
        reg.Records.Add(new NemesisRecord { SoulName = "Тлен" });
        reg.Records.Add(new NemesisRecord { SoulName = "Капля" });
        reg.Records.Add(new NemesisRecord { SoulName = "Шёпот" });
        for (ulong seed = 1; seed <= 10; seed++) {
            string name = reg.PickSoulName(new PmRandom(seed * 139UL));
            True(name == "Искра" || name == "Осколок" || name == "Эхо" || name == "Тлен" || name == "Капля" || name == "Шёпот",
                "все заняты → любое из канона: " + name);
        }
    }

    public static void OnPlayerKilledBy_CreatesAndSteals() {
        var reg = new NemesisRegistry();
        var rng = new PmRandom(999UL);
        reg.OnPlayerKilledBy("Тлен", new string[] { "m_spikes", "m_shell" }, rng);
        Eq(1, reg.Records.Count, "запись создана");
        var rec = reg.Records[0];
        Eq("Тлен", rec.SoulName, "имя души");
        Eq(1, rec.Level, "уровень 1 при первой смерти игрока");
        Eq(1, rec.KillsOfPlayer, "одино убийства");
        True(rec.StolenMutation == "m_spikes" || rec.StolenMutation == "m_shell",
            "украдена мутация игрока: " + rec.StolenMutation);
        True(!rec.Defeated, "немезида жива");
    }

    public static void OnPlayerKilledBy_IncrementsExisting() {
        var reg = new NemesisRegistry();
        var rng = new PmRandom(1001UL);
        reg.Records.Add(new NemesisRecord { SoulName = "Эхо", Level = 2, KillsOfPlayer = 1 });
        reg.OnPlayerKilledBy("Эхо", new string[] { "m_eyes" }, rng);
        Eq(1, reg.Records.Count, "новая запись не создаётся");
        Eq(3, reg.Records[0].Level, "уровень вырос до 3");
        Eq(2, reg.Records[0].KillsOfPlayer, "двойное убийство");
        Eq("m_eyes", reg.Records[0].StolenMutation, "новая кража");
        reg.OnPlayerKilledBy("", new string[] { "m_eyes" }, rng); // пустое имя игнорируется
        Eq(1, reg.Records.Count, "пустое имя не создаёт запись");
    }

    public static void OnPlayerKilledBy_EmptyMutationsStealNothing() {
        var reg = new NemesisRegistry();
        reg.OnPlayerKilledBy("Капля", new string[0], new PmRandom(7UL));
        True(reg.Records[0].StolenMutation == null, "нечего красть");
        reg.OnPlayerKilledBy("Капля", null, new PmRandom(8UL));
        True(reg.Records[0].StolenMutation == null, "null-мутации безопасны");
        Eq(1, reg.Records.Count, "запись одна и та же");
    }

    public static void OnSoulAbsorbed_MarksDefeated() {
        var reg = new NemesisRegistry();
        reg.Records.Add(new NemesisRecord { SoulName = "Шёпот", Level = 1 });
        reg.OnSoulAbsorbed("Шёпот");
        True(reg.Records[0].Defeated, "душа повержена");
        reg.OnSoulAbsorbed("никто"); // неизвестное имя — без исключений
        Eq(1, reg.Records.Count, "записей не прибавилось");
    }

    public static void PickForRoom_NullWhenNoAliveNemeses() {
        var empty = new NemesisRegistry();
        for (ulong seed = 1; seed <= 20; seed++)
            True(empty.PickForRoom(3, new PmRandom(seed * 3UL)) == null, "пустой реестр → null");
        var allDead = new NemesisRegistry();
        allDead.Records.Add(new NemesisRecord { SoulName = "Тлен", Level = 1, Defeated = true });
        allDead.Records.Add(new NemesisRecord { SoulName = "Эхо", Level = 2, Defeated = true });
        for (ulong seed = 1; seed <= 20; seed++)
            True(allDead.PickForRoom(3, new PmRandom(seed * 5UL)) == null, "поверженные не спавнятся");
    }

    public static void PickForRoom_ChanceAbout35Percent() {
        var reg = new NemesisRegistry();
        reg.Records.Add(new NemesisRecord { SoulName = "Тлен", Level = 1 });
        int spawned = 0;
        for (ulong seed = 1; seed <= 2000; seed++)
            if (reg.PickForRoom(4, new PmRandom(seed * 11UL)) != null) spawned++;
        True(spawned >= 500 && spawned <= 900, "шанс ~35%: " + spawned + "/2000");
    }

    public static void PickForRoom_ReturnsAliveRecord() {
        var reg = new NemesisRegistry();
        reg.Records.Add(new NemesisRecord { SoulName = "Тлен", Level = 1, Defeated = true });
        reg.Records.Add(new NemesisRecord { SoulName = "Эхо", Level = 2 });
        bool sawEcho = false;
        for (ulong seed = 1; seed <= 200; seed++) {
            var rec = reg.PickForRoom(2, new PmRandom(seed * 13UL));
            if (rec == null) continue;
            Eq("Эхо", rec.SoulName, "выбирается только живая");
            sawEcho = true;
        }
        True(sawEcho, "живая немезида выпадает");
    }

    public static void NemesisMass_FormulaByLevel() {
        var r0 = new NemesisRecord { Level = 0 };
        Eq(4.4, NemesisRegistry.NemesisMass(r0, 4.0), "lvl 0: 4.0 × 1.1");
        var r2 = new NemesisRecord { Level = 2 };
        Eq(5.6, NemesisRegistry.NemesisMass(r2, 4.0), "lvl 2: 4.0 × 1.4");
        var r5 = new NemesisRecord { Level = 5 };
        Eq(9.25, NemesisRegistry.NemesisMass(r5, 5.0), "lvl 5: 5.0 × 1.85");
        Eq(4.4, NemesisRegistry.NemesisMass(null, 4.0), "null-запись безопасна");
        var stolen = new NemesisRecord { Level = 3, StolenMutation = "m_spikes" };
        Eq(6.2, NemesisRegistry.NemesisMass(stolen, 4.0), "lvl 3: 4.0 × 1.55 (кража не влияет)");
    }
}
}
