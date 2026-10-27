// PM.Domain.Run — «Наследие прошлой жизни» (v0.13). C# 9, без LINQ.
namespace PM.Domain.Run {

/// <summary>
/// Что переносится из прошлой жизни: 30% ресурсов на момент смерти (по каждому виду),
/// один открытый в прошлой жизни узел эволюции на выбор (узел «Деление» не переносится)
/// и стартовая масса +15% от пика прошлой жизни (не больше +3).
/// Снимок делается при смерти (RunState.MakeLegacy) и живёт в SoulMeta до следующей жизни;
/// сгорает, если следующая жизнь начата без применения.
/// </summary>
public sealed class LegacyPack {
    public const double ResourceFrac = 0.30;
    public const double MassFrac = 0.15;
    public const double MassCap = 3.0;
    public const string ExcludedNode = "div_split_basics";   // узел «Деление» — не наследуется

    /// <summary>30% каждого ресурса на момент смерти (индекс = ResourceKind).</summary>
    public double[] Resources = new double[ResourceInfo.Count];
    /// <summary>Открытые узлы прошлой жизни (в порядке каталога) — из них игрок выбирает один.</summary>
    public string[] Nodes = new string[0];
    /// <summary>+стартовая масса: min(пик прошлой жизни × 15%, 3).</summary>
    public double StartMassBonus;
}
}
