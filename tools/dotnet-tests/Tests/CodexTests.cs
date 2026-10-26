using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.9: энциклопедия — записи из каталогов, тексты из JSON, открытие и сохранение.</summary>
public static class CodexTests {
    public static void Entries_CoverCatalogs() {
        Eq(SpeciesCatalog.All().Length, Codex.All(CodexKind.Species).Count, "every species");
        Eq(BiomeCatalog.All().Length, Codex.All(CodexKind.Biome).Count, "every biome");
        Eq(ResourceInfo.Count, Codex.All(CodexKind.Resource).Count, "every resource");
        var mite = Codex.ForSpecies("mite");
        Eq("Клещ", mite.Name, "name from catalog");
        True(mite.Facts.Length >= 3, "facts: habitat, behaviour, yield");
        True(mite.Tip.Length > 0, "tactics from bestiary");
        Eq("portrait_mite", mite.ArtKey, "portrait key");
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
