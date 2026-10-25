using PM.Domain.Genetics;
using static PMHarness.Check;
namespace PMHarness {
public static class GeneTests {
    public static void Catalog_HasDemoSizeAndUniqueIds() {
        var nodes = GeneCatalog.Demo();
        True(nodes.Length >= 8 && nodes.Length <= 12, "8-12 nodes per DEMO_SCOPE");
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < nodes.Length; i++) True(seen.Add(nodes[i].Id), "unique node ids");
    }
    public static void Extract_OneSourceOneGene() {
        var g = new Genome();
        True(g.TryExtract(101, "beetle_a"), "extracted from beetle_a");
        True(!g.TryExtract(101, "beetle_a"), "same source cannot give again");
        True(!g.TryExtract(102, "beetle_a"), "source exhausted: no second fragment");
        True(g.TryExtract(102, "beetle_b"), "different source works");
    }
    public static void Unlock_RequiresGene() {
        var nodes = GeneCatalog.Demo();
        var un = new UnlockedNodes();
        var g = new Genome();
        var s = new ComponentsStock { Water = 10, Organic = 10, Chitin = 10, Mineral = 10 };
        var plate = Find(nodes, "n_chitin_plate");
        Eq("gene_missing", GeneNetworkService.CanUnlock(plate, un, g, s), "no gene -> locked");
        g.TryExtract(GeneCatalog.GeneChitin, "beetle");
        Eq(null, GeneNetworkService.CanUnlock(plate, un, g, s), "with gene -> can");
    }
    public static void Unlock_RequiresPrerequisite() {
        var nodes = GeneCatalog.Demo();
        var un = new UnlockedNodes();
        var g = new Genome();
        g.TryExtract(GeneCatalog.GeneChitin, "b1");
        g.TryExtract(GeneCatalog.GeneMandible, "b1_m");
        var s = new ComponentsStock { Water = 99, Organic = 99, Chitin = 99, Mineral = 99 };
        var form = Find(nodes, "n_beetle_form");
        Eq("prerequisite_missing", GeneNetworkService.CanUnlock(form, un, g, s), "needs plate first");
        True(GeneNetworkService.TryUnlock(Find(nodes, "n_chitin_plate"), un, g, s), "plate unlocked");
        True(GeneNetworkService.TryUnlock(form, un, g, s), "form unlocked after prereq");
    }
    public static void Unlock_ConsumesComponentsAndGenes_AllOrNothing() {
        var nodes = GeneCatalog.Demo();
        var un = new UnlockedNodes();
        var g = new Genome();
        g.TryExtract(GeneCatalog.GeneChitin, "b");
        var poor = new ComponentsStock { Water = 0, Organic = 2, Chitin = 3, Mineral = 0 };
        var plate = Find(nodes, "n_chitin_plate");
        True(!GeneNetworkService.TryUnlock(plate, un, g, poor), "cannot afford");
        True(g.Has(GeneCatalog.GeneChitin), "gene NOT consumed on failed unlock (atomic)");
        var rich = new ComponentsStock { Water = 0, Organic = 2, Chitin = 4, Mineral = 0 };
        True(GeneNetworkService.TryUnlock(plate, un, g, rich), "unlocked");
        True(g.Has(GeneCatalog.GeneChitin), "gene persists as knowledge after success");
        True(un.Has("n_chitin_plate"), "node stored");
    }
    public static void Unlock_NoDuplicates() {
        var nodes = GeneCatalog.Demo();
        var un = new UnlockedNodes();
        var g = new Genome();
        g.TryExtract(GeneCatalog.GeneChitin, "b");
        var s = new ComponentsStock { Water = 10, Organic = 10, Chitin = 10, Mineral = 10 };
        True(GeneNetworkService.TryUnlock(Find(nodes, "n_chitin_plate"), un, g, s), "first unlock");
        True(!GeneNetworkService.TryUnlock(Find(nodes, "n_chitin_plate"), un, g, s), "duplicate rejected");
    }
    public static void EffectsSumAcrossNodes() {
        var nodes = GeneCatalog.Demo();
        var un = new UnlockedNodes();
        var g = new Genome();
        g.TryExtract(GeneCatalog.GeneChitin, "b1");
        g.TryExtract(GeneCatalog.GeneChitin, "b2"); // в тестах источник иной — второй фрагмент
        var s = new ComponentsStock { Water = 10, Organic = 10, Chitin = 20, Mineral = 10 };
        GeneNetworkService.TryUnlock(Find(nodes, "n_chitin_plate"), un, g, s);
        GeneNetworkService.TryUnlock(Find(nodes, "n_chitin_upgrade"), un, g, s);
        Eq(0.30, un.SumEffect(nodes, "armor_resist"), "armor sum 0.15+0.15", 1e-9);
    }
    private static GeneNode Find(GeneNode[] arr, string id) {
        for (int i = 0; i < arr.Length; i++) if (arr[i].Id == id) return arr[i];
        return null;
    }
}
}
