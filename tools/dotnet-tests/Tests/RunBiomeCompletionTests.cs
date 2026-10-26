using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunBiomeCompletionTests {

    // Пулы видов биомов (из BiomeCatalog):
    //   Fungal (глубина 1): spore, mite, beetle, soul_slime
    //   Warm (глубина 2):   spore, mite, beetle, glowworm, spikeback, soul_slime, ember_beetle, salamander
    //   Ponds (глубина 3):  spore, mite, beetle, glowworm, spikeback, jelly, soul_slime, leech, bubble_jelly
    //   Crystal (глубина 4):spore, beetle, glowworm, spikeback, jelly, crystal_mite, soul_slime, shard_crab
    //   Heart (глубина 5):  spore, glowworm, spikeback, jelly, crystal_mite, soul_slime, wisp, warden

    public static void Fresh_NoBiomesCompleted() {
        var st = new RunState(new SoulMeta(), 7UL);
        Eq(0, st.CompletedBiomesCount, "никакой биом не завершён");
        True(st.LastBiomeCompleted == null, "LastBiomeCompleted пусто");
    }

    public static void Fungal_CompleteGrantsBonusEssence() {
        var st = new RunState(new SoulMeta(), 11UL);
        st.OnAbsorb("spore", 0.3);      // 1
        Eq(0, st.CompletedBiomesCount, "не хватает mite, beetle, soul_slime");
        st.OnAbsorb("mite", 0.4);        // 2
        st.OnAbsorb("beetle", 0.4);     // 3
        Eq(0, st.CompletedBiomesCount, "не хватает soul_slime");
        st.OnAbsorb("soul_slime", 0.4); // 4 → Fungal complete
        Eq(1, st.CompletedBiomesCount, "Fungal завершён");
        Eq("Грибной коридор", st.LastBiomeCompleted, "имя завершённого биома");
        var imprint = st.Finish();
        // Essence = 0 (глубина 1) + 0 (комнат) + 1.5 (3 поглощения не-спор) + 20 (4 новых вида) + 5 (Fungal)
        Eq(26.5, imprint.EssenceEarned, "1.5 + 20 + 5 = 26.5");
    }

    public static void DuplicateAbsorb_DoesntReGrant() {
        var st = new RunState(new SoulMeta(), 13UL);
        st.OnAbsorb("spore", 0.3);
        st.OnAbsorb("mite", 0.4);
        st.OnAbsorb("beetle", 0.4);
        st.OnAbsorb("soul_slime", 0.4);
        Eq(1, st.CompletedBiomesCount, "Fungal завершён");
        // Повторное поглощение тех же видов — Fungal уже в _completedBiomes.
        st.OnAbsorb("spore", 0.3);
        st.OnAbsorb("mite", 0.4);
        st.OnAbsorb("beetle", 0.4);
        st.OnAbsorb("soul_slime", 0.4);
        Eq(1, st.CompletedBiomesCount, "повторно бонус не даётся");
        // Эссенция за завершение учтена один раз.
        // Базовая часть: 0.5×6 (3+3 поглощения не-спор) + 5×4 (новых вида) + 5 (Fungal) = 3 + 20 + 5 = 28
        Eq(28.0, st.Finish().EssenceEarned, "3 (поглощения) + 20 (4 вида) + 5 (Fungal)");
    }

    public static void PoolPartial_NoBonus() {
        var st = new RunState(new SoulMeta(), 17UL);
        st.OnAbsorb("spore", 0.3);
        st.OnAbsorb("mite", 0.4);
        st.OnAbsorb("beetle", 0.4);
        // soul_slime не поглощён — Fungal не завершён
        Eq(0, st.CompletedBiomesCount, "Fungal не завершён");
        True(st.LastBiomeCompleted == null, "LastBiomeCompleted пусто");
        var imprint = st.Finish();
        // spore — Food (не в _absorbedCount); mite, beetle — 2 поглощения.
        // 3 новых вида: 5×3 = 15. Поглощения: 0.5×2 = 1. Бонуса нет. Итого 16.
        Eq(16.0, imprint.EssenceEarned, "1 (поглощения) + 15 (3 вида) = 16, бонуса нет");
    }

    public static void MultipleBiomes_CompleteInOneAbsorb() {
        // soul_slime есть в пулах ВСЕХ биомов. Но каждый биом требует полного покрытия.
        // Сложим пул Heart, минус warden — для теста полного Heart (без warden нет завершения).
        var st = new RunState(new SoulMeta(), 19UL);
        // Absorbs covering all Heart species EXCEPT warden:
        foreach (var id in new[] { "spore", "glowworm", "spikeback", "jelly", "crystal_mite", "soul_slime", "wisp" }) {
            st.OnAbsorb(id, 0.3);
            True(st.CompletedBiomesCount == 0, "Heart не завершён без warden (после " + id + ")");
        }
        Eq(0, st.CompletedBiomesCount, "Heart требует warden");
        // Поглощаем warden — Heart завершён.
        st.OnBossAbsorbed(); // это не сработает: Boss == null
        True(st.CompletedBiomesCount == 0, "OnBossAbsorbed не добавляет warden в _speciesThisRun");
        // Прямое поглощение warden как вида:
        st.OnAbsorb("warden", 0.3);
        // Heart имеет warden, spore, glowworm, spikeback, jelly, crystal_mite, soul_slime, wisp — всё покрыто.
        True(st.CompletedBiomesCount >= 1, "Heart завершён после warden: " + st.CompletedBiomesCount);
        Eq("Сердце пещеры", st.LastBiomeCompleted, "последний завершённый — Heart");
    }

    public static void LastBiomeCompleted_TracksMostRecent() {
        var st = new RunState(new SoulMeta(), 23UL);
        // Завершим Fungal (4 вида).
        st.OnAbsorb("spore", 0.3);
        st.OnAbsorb("mite", 0.4);
        st.OnAbsorb("beetle", 0.4);
        st.OnAbsorb("soul_slime", 0.4);
        Eq("Грибной коридор", st.LastBiomeCompleted, "Fungal завершён");
        // Завершим Heart: нужны glowworm, spikeback, jelly, crystal_mite, wisp, warden (soul_slime уже поглощён).
        st.OnAbsorb("glowworm", 0.3);
        Eq("Грибной коридор", st.LastBiomeCompleted, "Heart ещё не завершён");
        st.OnAbsorb("spikeback", 0.3);
        st.OnAbsorb("jelly", 0.3);
        st.OnAbsorb("crystal_mite", 0.3);
        st.OnAbsorb("wisp", 0.3);
        st.OnAbsorb("warden", 0.3);
        Eq(2, st.CompletedBiomesCount, "Fungal + Heart завершены");
        Eq("Сердце пещеры", st.LastBiomeCompleted, "последний — Heart");
    }

    public static void BiomeCompletionEssence_ConstantValue() {
        Eq(5.0, RunState.BiomeCompletionEssence, "константа = 5.0 эссенции за биом");
    }
}
}
