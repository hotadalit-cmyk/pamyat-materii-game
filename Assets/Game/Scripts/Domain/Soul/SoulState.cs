using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.Soul {
/// <summary>
/// Состояние души (SOUL_WORLD_SOCIAL): качества переносятся между телами,
/// карма отделена от силы (GAME_VISION «Карма ≠ сила души»).
/// </summary>
public sealed class SoulState {
    public double EnergyPotential;      // энергетический потенциал (макс. духовной энергии)
    public double Endurance;            // духовная выносливость
    public double MagicReserve;         // магический резерв
    public double Karma;                // -100..100, только характер/отношения
    public int LivesLived;
    public readonly HashSet<string> KnowledgeGenes = new HashSet<string>(); // гены как знания
    public readonly Dictionary<string, int> GrowthCounts = new Dictionary<string, int>();
    public double CurrentEnergy;        // текущий запас духовной энергии тела

    public SoulState() {
        EnergyPotential = 10;
        Endurance = 5;
        MagicReserve = 2;
        CurrentEnergy = EnergyPotential;
    }
    public void LearnGene(string sourceKey) {
        if (!string.IsNullOrEmpty(sourceKey)) KnowledgeGenes.Add(sourceKey);
    }
}
/// <summary>
/// Рост души (D-19): только значимые действия, убывающая отдача, смерть ничего не даёт.
/// API «опыта за смерть» не существует — запрещено конструкцией.
/// </summary>
public static class SoulGrowth {
    public const string ActFirstDiscover = "first_discover";
    public const string ActComplexStreak = "complex_streak";
    public const string ActCapture = "capture_prey";
    public const string ActMetamorphose = "metamorphose";
    public const string ActAssist = "assist";
    public const string ActSurviveDanger = "survive_danger";
    public const double BaseReward = 2.0;
    public const double FloorFactor = 0.1; // отдача не падает ниже 10% базы
    /// <summary>Награда с убывающей отдачей: base / (1 + 0.5*priorCount), пол 10% базы.</summary>
    public static double RewardFor(int priorCount, double baseReward = BaseReward) {
        if (priorCount < 0) priorCount = 0;
        double r = baseReward / (1 + 0.5 * priorCount);
        return MathUtil.Max(baseReward * FloorFactor, r);
    }
    /// <summary>Применение: 60% в потенциал энергии, 25% в выносливость, 15% в магию.</summary>
    public static void Apply(SoulState soul, string actionType, double reward) {
        if (soul == null || reward <= 0) return;
        soul.GrowthCounts.TryGetValue(actionType, out int cnt);
        soul.GrowthCounts[actionType] = cnt + 1;
        soul.EnergyPotential += reward * 0.60;
        soul.Endurance += reward * 0.25;
        soul.MagicReserve += reward * 0.15;
    }
    /// <summary>Угасание без подкрепления (D-19): медленное снижение к 90% от пика.
    /// idleSeconds — время без значимых действий.</summary>
    public static void DecayIfIdle(SoulState soul, double idleSeconds) {
        if (idleSeconds < 600) return; // первые 10 «минут» мира — без угасания
        double k = (idleSeconds - 600) / 600.0 * 0.001;
        soul.EnergyPotential = MathUtil.Max(soul.EnergyPotential * (1 - k), soul.EnergyPotential * 0.9);
        soul.Endurance = MathUtil.Max(soul.Endurance * (1 - k), soul.Endurance * 0.9);
        soul.MagicReserve = MathUtil.Max(soul.MagicReserve * (1 - k), soul.MagicReserve * 0.9);
    }
}
}
