using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.Genetics {
/// <summary>Стоимость в компонентах (всё или ничего при списании).</summary>
public struct ComponentCost {
    public double Water, Organic, Chitin, Mineral;
    public static ComponentCost Of(double water, double organic, double chitin, double mineral) =>
        new ComponentCost { Water = water, Organic = organic, Chitin = chitin, Mineral = mineral };
}
/// <summary>Запас компонентов с инвариантом неотрицательности.</summary>
public sealed class ComponentsStock {
    public double Water, Organic, Chitin, Mineral;
    public bool CanAfford(ComponentCost c) =>
        Water >= c.Water && Organic >= c.Organic && Chitin >= c.Chitin && Mineral >= c.Mineral;
    public bool TrySpend(ComponentCost c) {
        if (!CanAfford(c)) return false;
        Water -= c.Water; Organic -= c.Organic; Chitin -= c.Chitin; Mineral -= c.Mineral;
        return true;
    }
    public void Add(double water, double organic, double chitin, double mineral) {
        Water += MathUtil.Max(0, water); Organic += MathUtil.Max(0, organic);
        Chitin += MathUtil.Max(0, chitin); Mineral += MathUtil.Max(0, mineral);
    }
}
/// <summary>Генетический фрагмент. Один источник → одно извлечение → одно использование (QA-6).</summary>
public sealed class GeneFragment {
    public int Id;
    public string SourceKey;   // откуда извлечён (вид добычи)
    public bool Extracted;
    public bool ConsumedByNode;
}
/// <summary>Геном игрока: извлечение фрагментов и расходование на узлы.</summary>
public sealed class Genome {
    private readonly Dictionary<int, GeneFragment> _fragments = new Dictionary<int, GeneFragment>();
    private readonly HashSet<string> _extractedSources = new HashSet<string>();
    public IEnumerable<GeneFragment> Fragments => _fragments.Values;

    /// <summary>Извлечение при переваривании: один источник — один фрагмент, без повторов.</summary>
    public bool TryExtract(int fragmentId, string sourceKey) {
        if (string.IsNullOrEmpty(sourceKey)) return false;
        if (!_extractedSources.Add(sourceKey)) return false; // источник уже дал ген
        if (_fragments.ContainsKey(fragmentId)) return false;
        _fragments[fragmentId] = new GeneFragment { Id = fragmentId, SourceKey = sourceKey, Extracted = true };
        return true;
    }
    public bool Has(int fragmentId) =>
        _fragments.TryGetValue(fragmentId, out var f) && f.Extracted;
    /// <summary>Расходование фрагмента на узел — навсегда помечает использованным.</summary>
    public bool TryConsume(int fragmentId) {
        if (!_fragments.TryGetValue(fragmentId, out var f) || !f.Extracted || f.ConsumedByNode)
            return false;
        f.ConsumedByNode = true;
        return true;
    }
    public bool IsConsumed(int fragmentId) =>
        _fragments.TryGetValue(fragmentId, out var f) && f.ConsumedByNode;
}
}
