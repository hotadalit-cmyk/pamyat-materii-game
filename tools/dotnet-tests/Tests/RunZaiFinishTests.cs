using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.10 (доводка идей Z.ai): мерцание в открытом мире, алхимия от алтарей, прогресс видов области.</summary>
public static class RunZaiFinishTests {
    public static void World_HasShimmeringButNotSporesOrNemeses() {
        int shimmer = 0, total = 0;
        for (ulong seed = 1; seed <= 6; seed++) {
            var m = WorldGenerator.Generate(1, seed, new NemesisRegistry());
            foreach (var s in m.Spawns) {
                if (s.SpeciesId == "spore" || s.IsNemesis) { True(s.Variant == SpawnVariant.None, "spores/souls never shimmer"); continue; }
                total++;
                if (s.Variant == SpawnVariant.Shimmering) shimmer++;
            }
        }
        True(total > 0 && shimmer > 0, "some creatures shimmer: " + shimmer + "/" + total);
        True(shimmer < total * 0.4, "but not most of them");
    }

    public static void World_ShimmerDoesNotChangeLayout() {
        var a = WorldGenerator.Generate(2, 77UL, new NemesisRegistry());
        var b = WorldGenerator.Generate(2, 77UL, new NemesisRegistry());
        Eq(a.Spawns.Count, b.Spawns.Count, "deterministic count");
        for (int i = 0; i < a.Spawns.Count; i++) {
            Eq(a.Spawns[i].Mass, b.Spawns[i].Mass, "same mass " + i);
            True(a.Spawns[i].Variant == b.Spawns[i].Variant, "same variant " + i);
        }
    }

    public static void OpenWorld_AlchemyNeedsAltarCharges() {
        var rs = new RunState(new SoulMeta(), 91UL, true);
        rs.Resources.Add(ResourceKind.Chitin, 30);
        Eq(0, rs.TransmutesLeft, "no charges at start");
        True(!rs.TryTransmute(ResourceKind.Chitin, ResourceKind.Psyche), "locked without an altar");
        int shrine = -1;
        for (int i = 0; i < rs.World.Altars.Count; i++) if (rs.World.Altars[i].Kind != AltarKind.Spring) { shrine = i; break; }
        True(shrine >= 0, "world has a shrine or relic");
        rs.UseAltarAt(shrine);
        Eq(RunState.MaxTransmutesPerShrine, rs.TransmutesLeft, "altar gives charges");
        double psyche = rs.Resources.Get(ResourceKind.Psyche);
        True(rs.TryTransmute(ResourceKind.Chitin, ResourceKind.Psyche), "transmute works anywhere");
        Eq(psyche + 1, rs.Resources.Get(ResourceKind.Psyche), "+1 psyche");
        Eq(RunState.MaxTransmutesPerShrine - 1, rs.TransmutesLeft, "charge spent");
    }

    public static void BiomeSpeciesProgress() {
        var rs = new RunState(new SoulMeta(), 92UL, true);
        var fungal = BiomeCatalog.ForDepth(1);
        Eq(0, rs.SpeciesEatenFrom(fungal), "nothing yet");
        rs.OnAbsorb("spore", 0.3);
        rs.OnAbsorb("mite", 0.8);
        Eq(2, rs.SpeciesEatenFrom(fungal), "two species of the area");
        True(!rs.IsBiomeCompleted(fungal.Id), "not complete");
        foreach (var id in fungal.SpeciesPool) rs.OnAbsorb(id, 0.5);
        True(rs.IsBiomeCompleted(fungal.Id), "complete after all species");
        Eq(fungal.Name, rs.LastBiomeCompleted, "name for the banner");
    }
}
}
