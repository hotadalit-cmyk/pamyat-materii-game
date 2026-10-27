using System;
using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Run;

namespace BalanceSim {

/// <summary>
/// v0.14: харнесс баланса — измеряет сгенерированные миры (массы, цели, плотность)
/// и считает по формулам из Docs/BALANCE_v14.md кривые прогресса, TTK и урона.
/// Запуск: cd tools/balance-sim && dotnet run [-- current|plan|verify]
/// </summary>
public static class Program {

    private const double SporeMass = 0.3;

    // Целевая таблица цели глубины (Docs/BALANCE_v14.md, §2)
    private static readonly double[] GoalTable = { 15.0, 32.0, 65.0, 115.0, 190.0 };
    private static readonly double[] HpMult = { 1.3, 1.35, 1.4, 1.45, 1.5 };
    // Множитель урона врагов по глубинам (GDD: ×1.0 … ×2.1)
    private static readonly double[] DepthDamage = { 1.0, 1.2, 1.45, 1.75, 2.1 };

    private static int _mode = 1; // 0 current, 1 plan, 2 verify

    public static void Main(string[] args) {
        if (args.Length > 0 && args[0] == "current") _mode = 0;
        if (args.Length > 0 && args[0] == "verify") _mode = 2;
        if (_mode == 0) CurrentWorlds();
        else { PlanTables(); Worlds(); }
    }

    // ---------- 1. Текущее состояние (для таблицы «как было») ----------

    private static void CurrentWorlds() {
        Console.WriteLine("=== Текущая модель: замер сгенерированных миров (5 сидов на глубину) ===");
        Console.WriteLine("depth | goal(=25% maps) | creatures | spores | elite% | maxCreature | medianCreature");
        for (int depth = 1; depth <= 5; depth++) {
            double goalSum = 0, eliteSum = 0, maxC = 0, medC = 0;
            int creSum = 0, sporeSum = 0;
            var med = new List<double>();
            for (uint seed = 1; seed <= 5; seed++) {
                var w = WorldGenerator.Generate(depth, seed * 7919UL + 13, null);
                goalSum += w.GoalMass;
                int cre = 0, spore = 0, elite = 0;
                for (int i = 0; i < w.Spawns.Count; i++) {
                    var s = w.Spawns[i];
                    if (s.SpeciesId == "spore") { spore++; continue; }
                    cre++;
                    if (s.Mass > maxC) maxC = s.Mass;
                    med.Add(s.Mass);
                    var reg = RegionOf(w, s.Pos);
                    if (reg >= 0 && w.Regions[reg].Kind == RegionKind.Elite) elite++;
                }
                creSum += cre; sporeSum += spore;
                eliteSum += cre > 0 ? 100.0 * elite / cre : 0.0;
            }
            med.Sort();
            if (med.Count > 0) medC = med[med.Count / 2];
            int n = 5;
            Console.WriteLine(string.Format("Г{0}   | {1,8:0.0}        | {2,7:0.0}   | {3,6:0.0} | {4,4:0.0}%  | {5,10:0.0}  | {6,6:0.00}",
                depth, goalSum / n, creSum / (double)n, sporeSum / (double)n, eliteSum / n, maxC, medC));
        }
    }

    private static int RegionOf(WorldMap w, Vec2 p) {
        for (int r = 0; r < w.Regions.Count; r++) {
            var reg = w.Regions[r];
            double dx = p.X - reg.Center.X, dy = p.Y - reg.Center.Y;
            if (dx * dx + dy * dy <= reg.Radius * reg.Radius) return r;
        }
        return -1;
    }

    // ---------- 2. План: целевые кривые ----------

    private static void PlanTables() {
        Console.WriteLine("=== План v0.14: целевые кривые ===");
        Console.WriteLine();
        Console.WriteLine("--- 2.1 Масса героя и цель глубины ---");
        Console.WriteLine("depth | goal  | devHero(×1.1) | guardianHp | heroR(dev) | corridor    | entry→fight(сим.)");
        double entry = 4.0;
        for (int d = 1; d <= 5; d++) {
            double goal = GoalTable[d - 1];
            double dev = goal * 1.1;
            double hp = Math.Max(8.0, goal * HpMult[d - 1]);
            double r = 0.35 * Math.Sqrt(dev);
            // симуляция жизни: вход с массой entry, потери на пути ~9% от съеденного,
            // бой со стражем: 3 удара закрытой фазы + 2 открытых, поглощение стража
            double eatLoss = goal * 0.09;
            double fight = entry + goal - eatLoss;
            double dmgTaken = fight * (0.03 * 3 + 0.21 * 2) * 0.55; // промахи учтены коэффициентом
            double fightMass = fight - dmgTaken;
            double exitMass = fightMass + hp * (1.0 - 0.12); // съел стража, часть растеряла по дороге
            Console.WriteLine(string.Format(
                "Г{0}    | {1,5:0.0} | {2,6:0.0}       | {3,7:0.0}    | {4,4:0.00}    | {5:0}-> {6:0}  | {7:0.0} → {8:0.0}",
                d, goal, dev, hp, r, CorridorLo(d), CorridorHi(d), entry, fightMass));
            entry = exitMass;
        }
        Console.WriteLine();
        Console.WriteLine("--- 2.2 TTK (удары до смерти цели; полная скорость rel=8) ---");
        Console.WriteLine("цель                     | формула                      | ударов | сек (~1.5 с/удар)");
        TtkRow("равный (T=1.0)", m => 1.0 / (0.025 * 8.0 * Math.Sqrt(0.5)));
        TtkRow("чуть больший ×1.3 (T=1.0)", m => 1.3 / (0.025 * 8.0 * Math.Sqrt(1.0 / 2.3)));
        TtkRow("равный бронированный (T=1.6)", m => 1.6 / (0.025 * 8.0 * Math.Sqrt(0.5)));
        TtkRow("мельче ×0.8 (T=0.8)", m => 0.8 / (0.025 * 8.0 * Math.Sqrt(1.0 / 1.8) / 0.8));
        TtkRow("страж Г1 (закрытая шляпка ×0.5)", m => 1.0 / (0.025 * 8.0 * Math.Sqrt(15.4 / (15.4 + 19.5)) * 0.5) * 1.0);
        Console.WriteLine();
        Console.WriteLine("--- 2.3 Урон врага герою, % массы героя за удар (полная скорость) ---");
        Console.WriteLine("вид           | Attack | Г1    | Г2    | Г3    | Г4    | Г5    | (границы 3–25%)");
        string[] ids = { "mite", "beetle", "glowworm", "spikeback", "jelly", "crystal_mite", "soul_slime", "shard_crab", "warden(страж)" };
        double[] atk  = { 0.05,   0.08,    0.07,      0.16,       0.10,   0.14,          0.12,        0.13,        0.11 };
        for (int i = 0; i < ids.Length; i++) {
            Console.Write(string.Format("{0,-13} | {1:0.00}   |", ids[i], atk[i]));
            for (int d = 1; d <= 5; d++) {
                double f = ClampFrac(atk[i] * DepthDamage[d - 1]);
                Console.Write(string.Format(" {0,4:0.0}%", f * 100));
            }
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("--- 2.4 Масштаб стражей (BodyR = 0.35×√HP) ---");
        Console.WriteLine("страж | HP     | BodyR | Arena | Wake  | зона/корни | импульс | миньон | осколок(орбита) | луч(ширина)");
        for (int d = 1; d <= 5; d++) {
            double hp = Math.Max(8.0, GoalTable[d - 1] * HpMult[d - 1]);
            double b = 0.35 * Math.Sqrt(hp);
            double arena = b * 4.5 + 2.0, wake = b * 3.6 + 1.0;
            double zone = b * 1.7, root = b * 1.5, pulse = b * 1.9;
            double minion = b * b * 0.65, orbit = b * 2.2, beam = b * 0.13;
            Console.WriteLine(string.Format(
                "Г{0}    | {1,6:0.0} | {2,5:0.00} | {3,5:0.0} | {4,5:0.0} | {5:0.00}/{6:0.00}  | {7,7:0.00} | {8,6:0.00} | {9:0.00} (r {10:0.00})    | {11:0.00}",
                d, hp, b, arena, wake, zone, root, pulse, minion, orbit, b * 0.11, beam));
        }
    }

    private static double ClampFrac(double f) => f < 0.03 ? 0.03 : (f > 0.25 ? 0.25 : f);
    private static int CorridorLo(int d) { int[] lo = { 6, 20, 45, 90, 160 }; return lo[d - 1]; }
    private static int CorridorHi(int d) { int[] hi = { 20, 45, 90, 160, 250 }; return hi[d - 1]; }

    private static void TtkRow(string label, Func<double, double> hits) {
        double h = hits(0);
        Console.WriteLine(string.Format("{0,-24} | {1,28} | {2,6:0.0}  | {3,5:0.0}", label, "по формуле §3.2", h, h * 1.5));
    }

    // ---------- 3. Проверка после реализации ----------

    private static void Worlds() {
        Console.WriteLine();
        Console.WriteLine("=== Проверка миров после реализации (5 сидов на глубину) ===");
        Console.WriteLine("depth | goal  | цель  | карта(25%) | creatures | spores | elite% | maxCreature");
        for (int depth = 1; depth <= 5; depth++) {
            double goalSum = 0, eliteSum = 0, maxC = 0, mapSum = 0;
            int creSum = 0, sporeSum = 0;
            for (uint seed = 1; seed <= 5; seed++) {
                var w = WorldGenerator.Generate(depth, seed * 7919UL + 13, null);
                goalSum += w.GoalMass;
                int cre = 0, spore = 0, elite = 0;
                double total = 0;
                for (int i = 0; i < w.Spawns.Count; i++) {
                    var s = w.Spawns[i];
                    total += s.Mass;
                    if (s.SpeciesId == "spore") { spore++; continue; }
                    cre++;
                    if (s.Mass > maxC) maxC = s.Mass;
                    var reg = RegionOf(w, s.Pos);
                    if (reg >= 0 && w.Regions[reg].Kind == RegionKind.Elite) elite++;
                }
                mapSum += total * 0.25;
                creSum += cre; sporeSum += spore;
                eliteSum += cre > 0 ? 100.0 * elite / cre : 0.0;
            }
            int n = 5;
            double goal = goalSum / n, target = GoalTable[depth - 1];
            Console.WriteLine(string.Format("Г{0}    | {1,5:0.0} | {2,5:0.0} | {3,8:0.0}  | {4,7:0.0}   | {5,6:0.0} | {6,4:0.0}%  | {7,10:0.00}",
                depth, goal, target, mapSum / n, creSum / (double)n, sporeSum / (double)n, eliteSum / n, maxC));
        }
    }
}
}
