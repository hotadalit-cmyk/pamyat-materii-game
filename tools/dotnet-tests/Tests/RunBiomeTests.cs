using System.Collections.Generic;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunBiomeTests {

    public static void All_FiveBiomesOrderedByDepth() {
        var all = BiomeCatalog.All();
        Eq(5, all.Length, "ровно пять биомов");
        for (int i = 0; i < 5; i++) {
            Eq(i + 1, all[i].Depth, "биом " + i + " на глубине " + (i + 1));
            Eq((BiomeId)(i + 1), all[i].Id, "Id биома соответствует глубине");
        }
    }

    public static void ForDepth_MappingAndClamps() {
        Eq(BiomeId.Fungal, BiomeCatalog.ForDepth(1).Id, "глубина 1 — грибной биом");
        Eq(BiomeId.Warm, BiomeCatalog.ForDepth(2).Id, "глубина 2 — тёплый биом");
        Eq(BiomeId.Ponds, BiomeCatalog.ForDepth(3).Id, "глубина 3 — затопленный биом");
        Eq(BiomeId.Crystal, BiomeCatalog.ForDepth(4).Id, "глубина 4 — кристальный биом");
        Eq(BiomeId.Heart, BiomeCatalog.ForDepth(5).Id, "глубина 5 — сердце пещеры");
        Eq(BiomeId.Fungal, BiomeCatalog.ForDepth(0).Id, "глубина 0 сжимается к 1");
        Eq(BiomeId.Fungal, BiomeCatalog.ForDepth(-7).Id, "отрицательная глубина сжимается к 1");
        Eq(BiomeId.Heart, BiomeCatalog.ForDepth(6).Id, "глубина 6 сжимается к 5");
        Eq(BiomeId.Heart, BiomeCatalog.ForDepth(100).Id, "глубина 100 сжимается к 5");
    }

    public static void Fungal_NameAndSubtitle() {
        var b = BiomeCatalog.ForDepth(1);
        Eq("Грибной коридор", b.Name, "имя грибного биома");
        Eq("Здесь всё начинается…", b.Subtitle, "подзаголовок грибного биома");
    }

    public static void Darkness_MatchesRoomGeneratorByDepth() {
        Eq(0.55, BiomeCatalog.ForDepth(1).Darkness01, "тьма на глубине 1");
        Eq(0.9, BiomeCatalog.ForDepth(5).Darkness01, "тьма на глубине 5");
        for (int d = 1; d <= 5; d++) {
            var plan = RoomGenerator.Generate(d, 0, 5UL, null);
            Eq(plan.Darkness01, BiomeCatalog.ForDepth(d).Darkness01, "тьма биома = тьме комнаты (глубина " + d + ")");
            True(BiomeCatalog.ForDepth(d).Darkness01 >= BiomeCatalog.ForDepth(d - 1).Darkness01 - 1e-9,
                "тьма не убывает с глубиной (" + d + ")");
        }
    }

    public static void Evaporation_PerBiome() {
        Eq(0.0, BiomeCatalog.ForDepth(1).EvaporationPerSec, "грибы не испаряют");
        Eq(0.004, BiomeCatalog.ForDepth(2).EvaporationPerSec, "тёплые пустоты: 0.004/сек");
        Eq(0.0, BiomeCatalog.ForDepth(3).EvaporationPerSec, "затопленные залы не испаряют");
        Eq(0.002, BiomeCatalog.ForDepth(4).EvaporationPerSec, "кристаллы: 0.002/сек");
        Eq(0.003, BiomeCatalog.ForDepth(5).EvaporationPerSec, "сердце: 0.003/сек");
    }

    public static void SpeciesPool_ContainsSporeAndMatchesDepth() {
        var all = BiomeCatalog.All();
        for (int b = 0; b < all.Length; b++) {
            var biome = all[b];
            True(Contains(biome.SpeciesPool, "spore"), "spore всегда в пуле биома " + biome.Name);
            for (int i = 0; i < biome.SpeciesPool.Length; i++) {
                var sp = SpeciesCatalog.Get(biome.SpeciesPool[i]);
                True(sp != null, "вид пула есть в каталоге: " + biome.SpeciesPool[i]);
                if (biome.SpeciesPool[i] == "spore") continue;
                True(sp.MinDepth <= biome.Depth && biome.Depth <= sp.MaxDepth,
                    "вид " + sp.Id + " в пуле биома глубины " + biome.Depth + " уважает свои глубины");
            }
            // обратное: каждый не-_food вид, подходящий по глубине, есть в пуле
            // (v0.15: EliteOnly-виды — исключение, они живут только в элитных гнёздах)
            var catalog = SpeciesCatalog.All();
            for (int i = 0; i < catalog.Length; i++) {
                if (catalog[i].Behavior == CreatureBehavior.Food) continue;
                if (catalog[i].EliteOnly) continue;
                if (catalog[i].MinDepth <= biome.Depth && biome.Depth <= catalog[i].MaxDepth)
                    True(Contains(biome.SpeciesPool, catalog[i].Id),
                        "вид " + catalog[i].Id + " подходит биому " + biome.Name + " и должен быть в пуле");
            }
        }
    }

    public static void ObstaclePool_PerSpec() {
        var all = BiomeCatalog.All();
        for (int i = 0; i < all.Length; i++)
            True(all[i].ObstaclePool.Length >= 2, "в пуле биома " + all[i].Name + " хотя бы 2 препятствия");
        Eq(2, BiomeCatalog.ForDepth(1).ObstaclePool.Length, "грибы: pillar + mushroom");
        True(Contains(BiomeCatalog.ForDepth(1).ObstaclePool, ObstacleKind.Pillar), "грибы: колонна");
        True(Contains(BiomeCatalog.ForDepth(1).ObstaclePool, ObstacleKind.Mushroom), "грибы: гриб");
        True(Contains(BiomeCatalog.ForDepth(2).ObstaclePool, ObstacleKind.Vent), "тепло: жерло");
        True(Contains(BiomeCatalog.ForDepth(2).ObstaclePool, ObstacleKind.Thorns), "тепло: шипы");
        True(Contains(BiomeCatalog.ForDepth(3).ObstaclePool, ObstacleKind.Pool), "вода: лужа");
        True(Contains(BiomeCatalog.ForDepth(3).ObstaclePool, ObstacleKind.Sticky), "вода: липкость");
        True(Contains(BiomeCatalog.ForDepth(4).ObstaclePool, ObstacleKind.Crystal), "кристаллы: кристалл");
        True(Contains(BiomeCatalog.ForDepth(5).ObstaclePool, ObstacleKind.Vent), "сердце: жерло");
        True(Contains(BiomeCatalog.ForDepth(5).ObstaclePool, ObstacleKind.Pool), "сердце: лужа");
    }

    public static void Colors_DarkWallsBrightAccent() {
        var all = BiomeCatalog.All();
        for (int i = 0; i < all.Length; i++) {
            var b = all[i];
            True(b.WallColor < b.FloorColor, "стены темнее пола: " + b.Name);
            True(b.AccentColor != 0, "акцент задан: " + b.Name);
            True(b.GlowColor != 0, "свечение задано: " + b.Name);
            True(b.WallColor != 0, "цвет стен задан: " + b.Name);
        }
    }

    public static void All_NamesAndSubtitlesNotEmpty() {
        var all = BiomeCatalog.All();
        for (int i = 0; i < all.Length; i++) {
            True(!string.IsNullOrEmpty(all[i].Name), "имя биома " + i + " задано");
            True(!string.IsNullOrEmpty(all[i].Subtitle), "подзаголовок биома " + i + " задан");
        }
    }

    private static bool Contains(string[] arr, string id) {
        for (int i = 0; i < arr.Length; i++) if (arr[i] == id) return true;
        return false;
    }

    private static bool Contains(ObstacleKind[] arr, ObstacleKind kind) {
        for (int i = 0; i < arr.Length; i++) if (arr[i] == kind) return true;
        return false;
    }
}
}
