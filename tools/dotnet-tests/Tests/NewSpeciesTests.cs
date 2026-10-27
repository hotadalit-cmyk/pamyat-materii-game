using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.15: новые виды — пещерная крыса, пиранья-стая, жаба-бомба, элитная акула.</summary>
public static class NewSpeciesTests {
    private static WorldMap Map(int depth, ulong seed) =>
        WorldGenerator.Generate(depth, seed, new NemesisRegistry());

    public static void Catalog_Entries_Consistent() {
        var rat = SpeciesCatalog.Get("cave_rat");
        var fish = SpeciesCatalog.Get("piranha");
        var toad = SpeciesCatalog.Get("bomb_toad");
        var shark = SpeciesCatalog.Get("deep_shark");
        True(rat != null && rat.MinDepth == 1 && rat.MaxDepth == 1, "rat lives on Г1");
        True(fish != null && toad != null && fish.MinDepth == 3, "piranha and toad live on Г3");
        True(shark != null && shark.EliteOnly, "shark is elite-only");
        // баланс по BALANCE_v14: добыча Г1 — меньше 3 (герой 6→20 съедает поштучно)
        True(rat.MassAtDepth(1) < 3.0, "rat mass fits Г1 prey scale");
        // Г3: герой 45→90; пиранья/жаба — добыча, акула — элита
        True(fish.MassAtDepth(3) < 6.0, "piranha mass Г3 small");
        True(toad.MassAtDepth(3) < 9.0, "toad mass Г3 moderate");
        True(shark.MassAtDepth(3) > toad.MassAtDepth(3), "shark heavier than toad");
        True(shark.Attack > fish.Attack, "shark hits harder than piranha");
        // у всех новая атака в пределах границ урона (3–25% массы героя за удар задаёт SizeRule)
        foreach (var s in new[] { rat, fish, toad, shark })
            True(s.Attack > 0.03 && s.Attack < 0.25, s.Id + ": attack fraction sane");
    }

    public static void Rat_SpawnsOnlyOnDepth1() {
        for (int s = 0; s < 3; s++) {
            var m = Map(1, 500u + (ulong)s);
            True(m.Spawns.Exists(sp => sp.SpeciesId == "cave_rat"), "d1 seed " + s + ": rats present");
        }
        var d3 = Map(3, 510u);
        True(!d3.Spawns.Exists(sp => sp.SpeciesId == "cave_rat"), "no rats on Г3");
    }

    public static void Piranha_MovesInPack() {
        for (int s = 0; s < 5; s++) {
            var m = Map(3, 520u + (ulong)s);
            int fish = 0;
            foreach (var sp in m.Spawns) if (sp.SpeciesId == "piranha") fish++;
            True(fish >= 3, "d3 seed " + s + ": piranha pack present (" + fish + ")");
            // стайность: есть хотя бы одна пара ближе 3.5 ед.
            var pts = new List<Vec2>();
            foreach (var sp in m.Spawns) if (sp.SpeciesId == "piranha") pts.Add(sp.Pos);
            bool pair = false;
            for (int i = 0; i < pts.Count && !pair; i++)
                for (int j = i + 1; j < pts.Count; j++)
                    if ((pts[i] - pts[j]).SqrLength < 12.25) { pair = true; break; }
            True(pair, "d3 seed " + s + ": piranhas cluster");
        }
    }

    public static void Shark_OnlyInEliteNests() {
        for (int s = 0; s < 5; s++) {
            var m = Map(3, 530u + (ulong)s);
            foreach (var sp in m.Spawns) {
                if (sp.SpeciesId != "deep_shark") continue;
                m.CellOf(sp.Pos, out int x, out int y);
                int reg = m.RegionOf[y * m.Width + x];
                True(reg >= 0 && m.Regions[reg].Kind == RegionKind.Elite, "shark inside elite nest");
                True(sp.Mass >= 7.0 * 0.9, "shark mass carries elite mult");
            }
        }
        // хотя бы на одном из сидов акулы вообще появляются (элитные гнёзда есть на каждой карте Г3)
        bool any = false;
        for (int s = 0; s < 8 && !any; s++) {
            var m = Map(3, 540u + (ulong)s);
            any = m.Spawns.Exists(sp => sp.SpeciesId == "deep_shark");
        }
        True(any, "elite nests host sharks on some seeds");
    }

    public static void Codex_And_Bestiary_Cover_NewSpecies() {
        foreach (var id in new[] { "cave_rat", "piranha", "bomb_toad", "deep_shark" }) {
            var e = Codex.ForSpecies(id);
            Eq(id, e.Id, id + ": codex id");
            True(e.Facts.Length >= 3, id + ": codex facts");
            True(Bestiary.Describe(id) != null, id + ": bestiary tip");
            var y = YieldTable.For(id, 1.0);
            True(y.Length >= 1, id + ": yield table");
        }
    }

    public static void Shark_HasArmor() {
        True(SpeciesTraitsCatalog.Get("deep_shark").ArmorSpeed > 0, "shark armor speed");
        True(SpeciesTraitsCatalog.Get("cave_rat").ArmorSpeed == 0, "rat has no armor");
    }
}
}
