using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunVariantTests {

    // ---------- SpawnSpec и детерминизм роллинга ----------

    public static void SpawnSpec_VariantDefaultNone() {
        var s = new SpawnSpec { SpeciesId = "beetle", Mass = 2.5 };
        Eq(SpawnVariant.None, s.Variant, "по умолчанию Variant = None");
    }

    public static void RoomGen_ShimmeringAbout12PercentOfCreatures() {
        // Считаем мерцающих существ по большому числу seed и комнат.
        // Споры и немезиды исключены — только «обычные» создания.
        int total = 0, shimmering = 0;
        for (ulong seed = 1; seed <= 400; seed++) {
            var room = RoomGenerator.Generate(2, 1, seed, null);
            for (int i = 0; i < room.Spawns.Count; i++) {
                var s = room.Spawns[i];
                if (s.SpeciesId == "spore" || s.IsNemesis) continue;
                total++;
                if (s.Variant == SpawnVariant.Shimmering) shimmering++;
            }
        }
        True(total > 800, "достаточно существ для статистики: " + total);
        double share = (double)shimmering / total;
        // ~12 % — пускаем широкие ворота (статистический разброс на 400 seed).
        True(share > 0.08 && share < 0.17, "доля мерцания ~12 %: " + share.ToString("0.00"));
    }

    public static void RoomGen_ShimmeringNeverOnSporesOrNemeses() {
        for (ulong seed = 1; seed <= 200; seed++) {
            var meta = new SoulMeta();
            meta.Nemeses.Records.Add(new NemesisRecord { SoulName = "Тлен", Level = 2 });
            // И комнатный, и более глубокий срез.
            for (int depth = 1; depth <= 4; depth++)
            for (int room = 0; room < 3; room++) {
                var r = RoomGenerator.Generate(depth, room, seed + (ulong)depth * 7 + (ulong)room, meta.Nemeses);
                for (int i = 0; i < r.Spawns.Count; i++) {
                    var s = r.Spawns[i];
                    if (s.SpeciesId == "spore")
                        True(s.Variant == SpawnVariant.None, "спора не мерцает: seed " + seed);
                    if (s.IsNemesis)
                        True(s.Variant == SpawnVariant.None, "немезида не мерцает: seed " + seed);
                }
            }
        }
    }

    public static void RoomGen_VariantRollDoesntShiftMainRng() {
        // Два забега с одинаковым seed: позиции и массы существ должны совпасть
        // (variant крутится отдельным rng, не двигая основной поток).
        for (int trial = 0; trial < 6; trial++) {
            ulong seed = (ulong)(101 + trial * 17);
            var a = RoomGenerator.Generate(3, 1, seed, null);
            var b = RoomGenerator.Generate(3, 1, seed, null);
            Eq(a.Spawns.Count, b.Spawns.Count, "число спавнов совпадает");
            for (int i = 0; i < a.Spawns.Count; i++) {
                Eq(a.Spawns[i].SpeciesId, b.Spawns[i].SpeciesId, "вид " + i + " совпадает");
                Eq(a.Spawns[i].Mass, b.Spawns[i].Mass, "масса " + i + " совпадает");
                True(a.Spawns[i].Pos.Approx(b.Spawns[i].Pos), "позиция " + i + " совпадает");
                Eq(a.Spawns[i].Variant, b.Spawns[i].Variant, "variant детерминирован");
            }
            Eq(a.GoalMass, b.GoalMass, "цель комнаты совпадает");
        }
    }

    // ---------- RunState.OnAbsorbVariant ----------

    public static void RunState_OnAbsorbVariant_GivesBonusResources() {
        // Обычное поглощение жука массой 2.5: базовый yield Chitin 0.8 × 2.5 = 2.0; Organic 0.4 × 2.5 = 1.0
        // С мерцанием ×1.5: Chitin 3.0; Organic 1.5
        var a = new RunState(new SoulMeta(), 11UL);
        a.OnAbsorb("beetle", 2.5);
        double baseChitin = a.Resources.Get(ResourceKind.Chitin);
        double baseOrganic = a.Resources.Get(ResourceKind.Organic);
        Eq(2.0, baseChitin, "база Chitin: 0.8 × 2.5 = 2.0");
        Eq(1.0, baseOrganic, "база Organic: 0.4 × 2.5 = 1.0");

        var b = new RunState(new SoulMeta(), 13UL);
        b.OnAbsorbVariant("beetle", 2.5, SpawnVariant.Shimmering);
        Eq(3.0, b.Resources.Get(ResourceKind.Chitin), "мерцание ×1.5: Chitin 3.0");
        Eq(1.5, b.Resources.Get(ResourceKind.Organic), "мерцание ×1.5: Organic 1.5");
        Eq(SpawnVariant.Shimmering, b.LastAbsorbVariant, "LastAbsorbVariant запомнен");
    }

    public static void RunState_OnAbsorbVariant_DoesntChangeMass() {
        var a = new RunState(new SoulMeta(), 17UL);
        a.OnAbsorb("beetle", 2.5);
        var b = new RunState(new SoulMeta(), 19UL);
        b.OnAbsorbVariant("beetle", 2.5, SpawnVariant.Shimmering);
        Eq(a.Mass, b.Mass, "масса одинакова — мерцание даёт только ресурсы");
        Eq(a.PeakMass, b.PeakMass, "пик массы одинаков");
        Eq(a.RoomAbsorbed, b.RoomAbsorbed, "прогресс комнаты одинаков");
    }

    public static void RunState_OnAbsorbVariant_NoneIsSameAsOnAbsorb() {
        var a = new RunState(new SoulMeta(), 23UL);
        a.OnAbsorb("beetle", 2.5);
        var b = new RunState(new SoulMeta(), 29UL);
        b.OnAbsorbVariant("beetle", 2.5, SpawnVariant.None);
        Eq(a.Mass, b.Mass, "масса та же");
        Eq(a.Resources.Get(ResourceKind.Chitin), b.Resources.Get(ResourceKind.Chitin), "ресурсы те же");
        Eq(SpawnVariant.None, b.LastAbsorbVariant, "LastAbsorbVariant = None");
    }

    public static void RunState_OnAbsorbVariant_ShimmeringStateResetsAfterCall() {
        // После мерцающего поглощения следующее обычное не должно получить бонус.
        var st = new RunState(new SoulMeta(), 31UL);
        st.OnAbsorbVariant("beetle", 2.5, SpawnVariant.Shimmering);
        double chitinAfterShimmering = st.Resources.Get(ResourceKind.Chitin);
        st.OnAbsorb("beetle", 2.5);  // обычное поглощение
        double chitinDelta = st.Resources.Get(ResourceKind.Chitin) - chitinAfterShimmering;
        Eq(2.0, chitinDelta, "следующее обычное поглощение без бонуса: +2.0 Chitin (база)");
    }
}
}
