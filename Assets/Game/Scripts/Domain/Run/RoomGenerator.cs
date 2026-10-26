using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Один спавн в комнате.</summary>
public struct SpawnSpec {
    public string SpeciesId;
    public Vec2 Pos;
    public double Mass;
    public bool IsNemesis;
    public string SoulName;   // только для немезид
}

/// <summary>План комнаты: арена, спавны, цель, выход. Чистые данные, без UnityEngine.</summary>
public sealed class RoomPlan {
    public int Depth, RoomIndex;
    public double HalfWidth, HalfHeight;       // арена 9×5 (глубина 1) → 12×7 (глубина 5)
    public List<SpawnSpec> Spawns;
    public double GoalMass;                    // сколько массы надо поглотить, чтобы открыть выход
    public Vec2 ExitPos;                       // у края арены, напротив старта
    public Vec2 PlayerStart;
    public double Darkness01;                  // 0.55 на глубине 1 → 0.9 на глубине 5
}

/// <summary>Детерминированная генерация комнат от seed. Без LINQ.</summary>
public static class RoomGenerator {
    private const double BaseHalfWidth = 4.5, BaseHalfHeight = 2.5;
    private const double MaxHalfWidth = 6.0, MaxHalfHeight = 3.5;
    private const double BaseDarkness = 0.55, MaxDarkness = 0.9;
    private const double EdgeMargin = 1.2;     // отступ старта/выхода от края
    private const double MinDistToPlayer = 2.0;
    private const double PairGap = 0.3;
    private const double WallPadding = 0.25;
    private const double DepthMassGrowth = 0.25; // ×(1 + 0.25×(depth−1))
    private const double GoalShare = 0.4;
    private const double MinGoalMass = 2.0;

    public static RoomPlan Generate(int depth, int roomIndex, ulong seed, NemesisRegistry nemeses) {
        if (depth < 1) depth = 1;
        if (depth > RunState.MaxDepth) depth = RunState.MaxDepth;
        var rng = new PmRandom(seed);
        double t = (depth - 1) / (double)(RunState.MaxDepth - 1);

        var plan = new RoomPlan {
            Depth = depth,
            RoomIndex = roomIndex,
            HalfWidth = BaseHalfWidth + (MaxHalfWidth - BaseHalfWidth) * t,
            HalfHeight = BaseHalfHeight + (MaxHalfHeight - BaseHalfHeight) * t,
            Darkness01 = BaseDarkness + (MaxDarkness - BaseDarkness) * t,
            PlayerStart = new Vec2(-(BaseHalfWidth + (MaxHalfWidth - BaseHalfWidth) * t - EdgeMargin), 0.0),
            Spawns = new List<SpawnSpec>(20),
        };
        plan.ExitPos = new Vec2(-plan.PlayerStart.X, 0.0);

        // --- содержимое комнаты (порядок вызовов rng фиксирован — детерминизм) ---
        int sporeCount = 6 + rng.NextInt(7);                       // 6..12
        for (int i = 0; i < sporeCount; i++)
            plan.Spawns.Add(new SpawnSpec { SpeciesId = "spore", Mass = 0.3 });

        int creatureCount = 3 + (depth - 1) / 2 + rng.NextInt(2);  // 3..6, растёт с глубиной
        if (creatureCount > 6) creatureCount = 6;
        var eligible = EligibleSpecies(depth);
        double massMult = 1.0 + DepthMassGrowth * (depth - 1);
        // душеслизень — охотник с массой игрока: не больше одного на комнату и ни одного
        // в самой первой комнате (первые секунды — на освоение рывка, а не на бегство)
        bool soulAllowed = !(depth == 1 && roomIndex == 0);
        for (int i = 0; i < creatureCount && eligible.Count > 0; i++) {
            var sp = eligible[rng.NextInt(eligible.Count)];
            if (sp.Id == "soul_slime") {
                if (!soulAllowed) {
                    if (eligible.Count == 1) break;
                    i--; continue; // перевыбрать другой вид
                }
                soulAllowed = false;
            }
            double mass = sp.Id == "soul_slime"
                ? EstimatePlayerMass(depth) * rng.Range(0.9, 1.1)
                : sp.BaseMass * massMult * rng.Range(0.85, 1.15);
            plan.Spawns.Add(new SpawnSpec { SpeciesId = sp.Id, Mass = mass });
        }

        // --- немезида (не в комнате 0 глубины 1) ---
        if (!(depth == 1 && roomIndex == 0) && nemeses != null) {
            var rec = nemeses.PickForRoom(depth, rng);
            if (rec != null) {
                plan.Spawns.Add(new SpawnSpec {
                    SpeciesId = "soul_slime",
                    Mass = NemesisRegistry.NemesisMass(rec, EstimatePlayerMass(depth)),
                    IsNemesis = true,
                    SoulName = rec.SoulName,
                });
            }
        }

        // --- позиции: крупные размещаем первыми (проще упаковка), отступ от стен,
        // ≥2.0 от старта игрока, ≥(r1+r2+0.3) друг от друга ---
        PlaceAllSpawns(rng, plan);

        // --- цель комнаты: ~40% массы поглощаемого содержимого, не меньше 2 ---
        double absorbable = 0.0;
        for (int i = 0; i < plan.Spawns.Count; i++) {
            SpawnSpec s = plan.Spawns[i];
            if (s.IsNemesis || s.SpeciesId == "soul_slime") continue;
            absorbable += s.Mass;
        }
        plan.GoalMass = absorbable * GoalShare;
        if (plan.GoalMass < MinGoalMass) plan.GoalMass = MinGoalMass;
        return plan;
    }

    /// <summary>Ожидаемая масса игрока на глубине (для массы soul_slime и немезид).</summary>
    public static double EstimatePlayerMass(int depth) => 4.0 * (1.0 + 0.4 * (depth - 1));

    private static List<SpeciesDef> EligibleSpecies(int depth) {
        var list = new List<SpeciesDef>(8);
        var all = SpeciesCatalog.All();
        for (int i = 0; i < all.Length; i++) {
            if (all[i].Behavior == CreatureBehavior.Food) continue; // еда спавнится отдельным слоем спор
            if (all[i].MinDepth <= depth && depth <= all[i].MaxDepth) list.Add(all[i]);
        }
        return list;
    }

    /// <summary>Размещение спавнов в порядке убывания массы: крупным достаётся пустая арена.</summary>
    private static void PlaceAllSpawns(PmRandom rng, RoomPlan plan) {
        int n = plan.Spawns.Count;
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        for (int i = 0; i < n; i++) { // простая сортировка выбором без LINQ
            int best = i;
            for (int j = i + 1; j < n; j++)
                if (plan.Spawns[order[j]].Mass > plan.Spawns[order[best]].Mass) best = j;
            int tmp = order[i]; order[i] = order[best]; order[best] = tmp;
        }
        var positioned = new bool[n];
        for (int k = 0; k < n; k++) {
            int i = order[k];
            SpawnSpec s = plan.Spawns[i];
            s.Pos = FindPosition(rng, plan, SizeRule.RadiusForMass(s.Mass), plan.Spawns, positioned);
            plan.Spawns[i] = s;
            positioned[i] = true;
        }
    }

    private static Vec2 FindPosition(PmRandom rng, RoomPlan plan, double radius, List<SpawnSpec> placed, bool[] positioned) {
        double maxX = plan.HalfWidth - (radius + WallPadding);
        double maxY = plan.HalfHeight - (radius + WallPadding);
        if (maxX < 0.1) maxX = 0.1;
        if (maxY < 0.1) maxY = 0.1;

        for (int attempt = 0; attempt < 150; attempt++) {
            Vec2 p = RandomInside(rng, maxX, maxY);
            if ((p - plan.PlayerStart).Length < MinDistToPlayer) continue;
            if (!PairwiseOk(p, radius, placed, positioned, 1.0)) continue;
            return p;
        }
        for (int attempt = 0; attempt < 100; attempt++) { // тесная арена: половинный зазор
            Vec2 p = RandomInside(rng, maxX, maxY);
            if ((p - plan.PlayerStart).Length < MinDistToPlayer) continue;
            if (!PairwiseOk(p, radius, placed, positioned, 0.5)) continue;
            return p;
        }
        for (int attempt = 0; attempt < 60; attempt++) { // запасной вариант: без попарной дистанции
            Vec2 p = RandomInside(rng, maxX, maxY);
            if ((p - plan.PlayerStart).Length < MinDistToPlayer) continue;
            return p;
        }
        return RandomInside(rng, maxX, maxY); // крайний случай: хотя бы внутри арены
    }

    private static bool PairwiseOk(Vec2 p, double radius, List<SpawnSpec> placed, bool[] positioned, double gapScale) {
        for (int i = 0; i < positioned.Length; i++) {
            if (!positioned[i]) continue;
            double minDist = radius + SizeRule.RadiusForMass(placed[i].Mass) + PairGap * gapScale;
            if ((p - placed[i].Pos).Length < minDist) return false;
        }
        return true;
    }

    private static Vec2 RandomInside(PmRandom rng, double maxX, double maxY) {
        double x = (rng.NextDouble01() * 2.0 - 1.0) * maxX;
        double y = (rng.NextDouble01() * 2.0 - 1.0) * maxY;
        return new Vec2(x, y);
    }
}
}
