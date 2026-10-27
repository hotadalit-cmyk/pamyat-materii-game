using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.9: энциклопедия — записи из каталогов, тексты из JSON, открытие и сохранение.</summary>
public static class CodexTests {
    public static void Entries_CoverCatalogs() {
        // v0.15: вкладка «Существа» = виды каталога + записи пяти стражей
        Eq(SpeciesCatalog.All().Length + GuardianCatalog.All().Length, Codex.All(CodexKind.Species).Count, "every species + guardians");
        Eq(BiomeCatalog.All().Length, Codex.All(CodexKind.Biome).Count, "every biome");
        Eq(ResourceInfo.Count, Codex.All(CodexKind.Resource).Count, "every resource");
        var mite = Codex.ForSpecies("mite");
        Eq("Клещ", mite.Name, "name from catalog");
        True(mite.Facts.Length >= 3, "facts: habitat, behaviour, yield");
        True(mite.Tip.Length > 0, "tactics from bestiary");
        Eq("portrait_mite", mite.ArtKey, "portrait key");
    }

    public static void GuardianEntries_HavePortraitAndTip() {
        foreach (var g in GuardianCatalog.All()) {
            var e = Codex.ForGuardian(g);
            Eq("gd:" + g.Id, e.Key, g.Id + ": key");
            Eq("Страж глубины " + g.Depth, e.Epithet, g.Id + ": epithet");
            True(!string.IsNullOrEmpty(e.ArtKey) && e.ArtKey.StartsWith("guardian_"), g.Id + ": portrait key");
            True(e.Facts.Length >= 3, g.Id + ": facts");
            True(e.Tip.Length > 0, g.Id + ": tip");
            True(Codex.GuardianKey(g.Id).StartsWith("gd:"), g.Id + ": key prefix");
        }
    }

    public static void GuardianUnlock_Persists() {
        var meta = new SoulMeta();
        var g = GuardianCatalog.ForDepth(2);
        True(Codex.Unlock(meta, Codex.GuardianKey(g.Id)), "first victory unlocks");
        True(!Codex.Unlock(meta, Codex.GuardianKey(g.Id)), "second victory — not new");
        var back = SoulMeta.FromJson(meta.ToJson());
        True(back.Codex.Contains(Codex.GuardianKey(g.Id)), "guardian key survives save/load");
    }

    public static void Lore_LoadsAndSurvivesGarbage() {
        int n = Codex.LoadLore("{\"world\":\"Мир\",\"species\":{\"mite\":{\"title\":\"Пугливый\",\"lore\":\"Текст\"}},\"biomes\":{\"fungal\":{\"lore\":\"Грибы\"}},\"resources\":{\"Slime\":\"Слизь\"}}");
        Eq(4, n, "four texts");
        Eq("Мир", Codex.World, "world text");
        Eq("Пугливый", Codex.ForSpecies("mite").Epithet, "epithet");
        Eq("Текст", Codex.ForSpecies("mite").Lore, "lore");
        Eq("Слизь", Codex.ForResource(ResourceKind.Slime).Lore, "resource lore");
        Eq(0, Codex.LoadLore("{not json"), "garbage ignored");
        Eq("", Codex.ForSpecies("mite").Lore, "garbage clears to defaults");
    }

    public static void Unlock_FirstTimeOnly_AndPersists() {
        var meta = new SoulMeta();
        var e = Codex.ForSpecies("beetle");
        True(!Codex.IsUnlocked(e, meta), "locked");
        True(Codex.Unlock(meta, e.Key), "first unlock");
        True(!Codex.Unlock(meta, e.Key), "second time — not new");
        var back = SoulMeta.FromJson(meta.ToJson());
        True(Codex.IsUnlocked(e, back), "saved and loaded");
        Eq(1, Codex.CountUnlocked(CodexKind.Species, back), "one species open");
    }

    public static void OldSaves_SeenSpeciesCountAsOpen() {
        var meta = new SoulMeta();
        meta.SeenSpecies.Add("mite");
        True(Codex.IsUnlocked(Codex.ForSpecies("mite"), meta), "absorbed in an old life — open");
    }
}
}
