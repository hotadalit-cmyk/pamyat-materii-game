using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunRoomTests {

    public static void Generate_DeterministicBySeed() {
        var reg = MakeRegistry();
        var a = RoomGenerator.Generate(2, 1, 777UL, reg);
        var b = RoomGenerator.Generate(2, 1, 777UL, reg);
        Eq(a.Depth, b.Depth, "глубина совпадает");
        Eq(a.RoomIndex, b.RoomIndex, "индекс комнаты совпадает");
        Eq(a.HalfWidth, b.HalfWidth, "ширина совпадает");
        Eq(a.HalfHeight, b.HalfHeight, "высота совпадает");
        Eq(a.Darkness01, b.Darkness01, "темнота совпадает");
        Eq(a.GoalMass, b.GoalMass, "цель совпадает");
        Eq(a.Spawns.Count, b.Spawns.Count, "число спавнов совпадает");
        True(a.PlayerStart.Approx(b.PlayerStart), "старт совпадает");
        True(a.ExitPos.Approx(b.ExitPos), "выход совпадает");
        bool allSame = true;
        for (int i = 0; i < a.Spawns.Count; i++) {
            if (a.Spawns[i].SpeciesId != b.Spawns[i].SpeciesId) allSame = false;
            if (!a.Spawns[i].Pos.Approx(b.Spawns[i].Pos)) allSame = false;
            if (System.Math.Abs(a.Spawns[i].Mass - b.Spawns[i].Mass) > 1e-9) allSame = false;
        }
        True(allSame, "все спавны идентичны по позициям и массам");
    }

    public static void Generate_SoulSlimeAtMostOneAndNotInFirstRoom() {
        for (ulong seed = 1; seed <= 300; seed++) {
            for (int depth = 1; depth <= RunState.MaxDepth; depth++) {
                for (int room = 0; room < RunState.RoomsPerDepth; room++) {
                    var plan = RoomGenerator.Generate(depth, room, seed, null);
                    int souls = 0;
                    foreach (var s in plan.Spawns) if (s.SpeciesId == "soul_slime" && !s.IsNemesis) souls++;
                    True(souls <= 1, "at most one soul slime per room");
                    if (depth == 1 && room == 0) Eq(0, souls, "no soul slime in the very first room");
                }
            }
        }
    }

    public static void Generate_DifferentSeedsDiffer() {
        var a = RoomGenerator.Generate(3, 2, 100UL, null);
        var b = RoomGenerator.Generate(3, 2, 200UL, null);
        bool identical = a.Spawns.Count == b.Spawns.Count;
        if (identical)
            for (int i = 0; i < a.Spawns.Count; i++)
                if (!a.Spawns[i].Pos.Approx(b.Spawns[i].Pos, 1e-4)) { identical = false; break; }
        True(!identical, "разные seed → разные планы");
    }

    public static void Generate_SpawnsKeepDistanceFromPlayerStart() {
        for (ulong seed = 1; seed <= 12; seed++) {
            var plan = RoomGenerator.Generate(3, 1, seed * 31UL, null);
            for (int i = 0; i < plan.Spawns.Count; i++) {
                double dist = (plan.Spawns[i].Pos - plan.PlayerStart).Length;
                True(dist >= 2.0 - 1e-6, "спавн " + i + " дальше 2.0 от старта (" + dist.ToString("0.##") + ")");
            }
        }
    }

    public static void Generate_SpawnsKeepPairwiseDistance() {
        for (ulong seed = 1; seed <= 12; seed++) {
            var plan = RoomGenerator.Generate(4, 0, seed * 97UL, null);
            var s = plan.Spawns;
            for (int i = 0; i < s.Count; i++)
                for (int j = i + 1; j < s.Count; j++) {
                    double minDist = SizeRule.RadiusForMass(s[i].Mass) + SizeRule.RadiusForMass(s[j].Mass) + 0.3;
                    double dist = (s[i].Pos - s[j].Pos).Length;
                    True(dist >= minDist - 1e-6,
                        "пара " + i + "/" + j + " дальше r1+r2+0.3 (" + dist.ToString("0.##") + " >= " + minDist.ToString("0.##") + ")");
                }
        }
    }

    public static void Generate_SpawnsInsideArenaWithMargin() {
        for (ulong seed = 1; seed <= 12; seed++) {
            var plan = RoomGenerator.Generate(5, 2, seed * 7UL, null);
            for (int i = 0; i < plan.Spawns.Count; i++) {
                double r = SizeRule.RadiusForMass(plan.Spawns[i].Mass);
                double x = plan.Spawns[i].Pos.X, y = plan.Spawns[i].Pos.Y;
                True(x <= plan.HalfWidth - r - 0.2, "спавн " + i + " внутри по X");
                True(x >= -(plan.HalfWidth - r - 0.2), "спавн " + i + " внутри по X (минус)");
                True(y <= plan.HalfHeight - r - 0.2, "спавн " + i + " внутри по Y");
                True(y >= -(plan.HalfHeight - r - 0.2), "спавн " + i + " внутри по Y (минус)");
            }
        }
    }

    public static void Generate_GoalAtLeastTwo() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 8; seed++) {
                var plan = RoomGenerator.Generate(depth, (int)(seed % 3), seed * 13UL, null);
                True(plan.GoalMass >= 2.0, "цель комнаты >= 2 (глубина " + depth + ")");
            }
    }

    public static void Generate_CreatureMassGrowsWithDepth() {
        for (ulong seed = 1; seed <= 10; seed++) {
            double d1 = CreatureMass(RoomGenerator.Generate(1, 0, seed * 3UL, null));
            double d5 = CreatureMass(RoomGenerator.Generate(5, 0, seed * 3UL, null));
            True(d5 > d1, "масса существ на глубине 5 больше, чем на 1 (seed " + seed + ")");
        }
    }

    public static void Generate_GoalGrowsWithDepthOnAverage() {
        double sum1 = 0, sum5 = 0;
        for (ulong seed = 1; seed <= 30; seed++) {
            sum1 += RoomGenerator.Generate(1, (int)(seed % 3), seed * 11UL, null).GoalMass;
            sum5 += RoomGenerator.Generate(5, (int)(seed % 3), seed * 11UL, null).GoalMass;
        }
        True(sum5 / 30.0 > sum1 / 30.0 * 2.0, "средняя цель на глубине 5 заметно больше");
    }

    public static void Generate_ArenaAndDarknessByDepth() {
        var d1 = RoomGenerator.Generate(1, 0, 1UL, null);
        var d5 = RoomGenerator.Generate(5, 0, 1UL, null);
        Eq(4.5, d1.HalfWidth, "арена глубины 1: полуширина 4.5");
        Eq(2.5, d1.HalfHeight, "арена глубины 1: полувысота 2.5");
        Eq(6.0, d5.HalfWidth, "арена глубины 5: полуширина 6.0");
        Eq(3.5, d5.HalfHeight, "арена глубины 5: полувысота 3.5");
        Eq(0.55, d1.Darkness01, "темнота глубины 1");
        Eq(0.9, d5.Darkness01, "темнота глубины 5");
        True(d1.ExitPos.X > 0 && d1.PlayerStart.X < 0, "выход напротив старта");
        True(d5.HalfWidth > d1.HalfWidth && d5.HalfHeight > d1.HalfHeight, "арена растёт");
        for (int depth = 1; depth <= 5; depth++) {
            var p = RoomGenerator.Generate(depth, 0, 5UL, null);
            True(p.Darkness01 >= 0.55 - 1e-9 && p.Darkness01 <= 0.9 + 1e-9, "темнота в диапазоне");
        }
    }

    public static void Generate_SpawnCountsInRange() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 10; seed++) {
                var plan = RoomGenerator.Generate(depth, (int)(seed % 3), seed * 17UL, null);
                int spores = 0, creatures = 0;
                for (int i = 0; i < plan.Spawns.Count; i++) {
                    if (plan.Spawns[i].IsNemesis) continue;
                    if (plan.Spawns[i].SpeciesId == "spore") spores++;
                    else creatures++;
                }
                True(spores >= 6 && spores <= 12, "спор 6..12, есть " + spores);
                True(creatures >= 3 && creatures <= 6, "существ 3..6, есть " + creatures);
            }
    }

    public static void Generate_NemesisNeverInFirstRoom() {
        var reg = MakeRegistry();
        for (ulong seed = 1; seed <= 30; seed++) {
            var plan = RoomGenerator.Generate(1, 0, seed * 41UL, reg);
            for (int i = 0; i < plan.Spawns.Count; i++)
                True(!plan.Spawns[i].IsNemesis, "в комнате 0 глубины 1 немезиды нет (seed " + seed + ")");
        }
    }

    public static void Generate_NemesisAppearsLaterWithCorrectMass() {
        var reg = MakeRegistry();
        int found = 0;
        for (ulong seed = 1; seed <= 60; seed++) {
            var plan = RoomGenerator.Generate(3, 1, seed * 53UL, reg);
            for (int i = 0; i < plan.Spawns.Count; i++) {
                if (!plan.Spawns[i].IsNemesis) continue;
                found++;
                True(!string.IsNullOrEmpty(plan.Spawns[i].SoulName), "у немезиды есть имя души");
                var rec = FindRecord(reg, plan.Spawns[i].SoulName);
                True(rec != null, "имя немезиды из реестра");
                Eq(NemesisRegistry.NemesisMass(rec, RoomGenerator.EstimatePlayerMass(3)),
                    plan.Spawns[i].Mass, "масса немезиды по формуле");
            }
        }
        True(found >= 5, "за 60 seed немезида встретилась (" + found + " раз, шанс 35%)");
    }

    public static void Generate_CreatureSpeciesRespectDepth() {
        for (int depth = 1; depth <= 5; depth++)
            for (ulong seed = 1; seed <= 8; seed++) {
                var plan = RoomGenerator.Generate(depth, 0, seed * 19UL, null);
                for (int i = 0; i < plan.Spawns.Count; i++) {
                    var sp = SpeciesCatalog.Get(plan.Spawns[i].SpeciesId);
                    True(sp != null, "вид из каталога: " + plan.Spawns[i].SpeciesId);
                    if (sp.Behavior == CreatureBehavior.Food) continue; // споры везде
                    True(sp.MinDepth <= depth && depth <= sp.MaxDepth,
                        "вид " + sp.Id + " уважает диапазон глубин на глубине " + depth);
                }
            }
    }

    // ---------- helpers ----------
    private static NemesisRegistry MakeRegistry() {
        var reg = new NemesisRegistry();
        reg.Records.Add(new NemesisRecord { SoulName = "Искра", Level = 2, KillsOfPlayer = 3 });
        reg.Records.Add(new NemesisRecord { SoulName = "Тлен", Level = 1, KillsOfPlayer = 1 });
        return reg;
    }

    private static NemesisRecord FindRecord(NemesisRegistry reg, string name) {
        for (int i = 0; i < reg.Records.Count; i++)
            if (reg.Records[i].SoulName == name) return reg.Records[i];
        return null;
    }

    private static double CreatureMass(RoomPlan plan) {
        double sum = 0;
        for (int i = 0; i < plan.Spawns.Count; i++)
            if (!plan.Spawns[i].IsNemesis && plan.Spawns[i].SpeciesId != "spore") sum += plan.Spawns[i].Mass;
        return sum;
    }
}
}
