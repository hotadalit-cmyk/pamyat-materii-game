using System.Collections.Generic;
namespace PM.Domain.Genetics {
/// <summary>Узел генетической сети (ABSORPTION_GENETICS «Генетическая сеть»).</summary>
public sealed class GeneNode {
    public string Id;
    public string Title;
    public string[] Prerequisites;         // id узлов
    public int[] RequiredGeneFragments;    // id фрагментов (расходуются)
    public ComponentCost Cost;
    public string EffectId;                // идентификатор для применяющей стороны
    public double EffectValue;
}
/// <summary>
/// Каталог первой демо-ветки: 8 узлов (DEMO_SCOPE: сеть 8–12 узлов).
/// Добыча: грибной жук. Компоненты: вода, органика, хитин, минералы.
/// </summary>
public static class GeneCatalog {
    public const int GeneChitin = 101;   // фрагмент хитинового покрова жука
    public const int GeneMandible = 102; // фрагмент мандибул жука
    public const int GeneAntenna = 103;  // фрагмент антенн (хеморецепция)

    public static GeneNode[] Demo() => new[] {
        new GeneNode { Id="n_chitin_plate", Title="Хитиновая пластина",
            Prerequisites=new string[0], RequiredGeneFragments=new[]{ GeneChitin },
            Cost=ComponentCost.Of(0,2,4,0), EffectId="armor_resist", EffectValue=0.15 },
        new GeneNode { Id="n_chitin_upgrade", Title="Улучшенный покров",
            Prerequisites=new[]{"n_chitin_plate"}, RequiredGeneFragments=new[]{ GeneChitin },
            Cost=ComponentCost.Of(0,2,6,2), EffectId="armor_resist", EffectValue=0.15 },
        new GeneNode { Id="n_chem_sense", Title="Химическое чутьё",
            Prerequisites=new string[0], RequiredGeneFragments=new[]{ GeneAntenna },
            Cost=ComponentCost.Of(1,3,0,0), EffectId="sense_chem", EffectValue=1 },
        new GeneNode { Id="n_viscous_coat", Title="Вязкий покров",
            Prerequisites=new string[0], RequiredGeneFragments=new int[0],
            Cost=ComponentCost.Of(2,4,0,0), EffectId="water_retention", EffectValue=0.15 },
        new GeneNode { Id="n_dense_core", Title="Плотное ядро",
            Prerequisites=new string[0], RequiredGeneFragments=new int[0],
            Cost=ComponentCost.Of(0,0,0,5), EffectId="structure_cap", EffectValue=2.0 },
        new GeneNode { Id="n_energy_sac", Title="Энергетический мешок",
            Prerequisites=new string[0], RequiredGeneFragments=new int[0],
            Cost=ComponentCost.Of(0,5,0,1), EffectId="conductivity", EffectValue=0.2 },
        new GeneNode { Id="n_adhesive_gel", Title="Клейкий гель",
            Prerequisites=new string[0], RequiredGeneFragments=new int[0],
            Cost=ComponentCost.Of(1,4,0,2), EffectId="capture_strength", EffectValue=0.25 },
        new GeneNode { Id="n_beetle_form", Title="Форма жука",
            Prerequisites=new[]{"n_chitin_plate"},
            RequiredGeneFragments=new[]{ GeneChitin, GeneMandible },
            Cost=ComponentCost.Of(2,8,10,4), EffectId="form_beetle", EffectValue=1 }
    };
}
/// <summary>Состояние открытых узлов игрока (сохраняется).</summary>
public sealed class UnlockedNodes {
    private readonly HashSet<string> _unlocked = new HashSet<string>();
    public IReadOnlyCollection<string> Ids => _unlocked;
    public bool Has(string nodeId) => _unlocked.Contains(nodeId);
    public bool Add(string nodeId) => _unlocked.Add(nodeId);
    /// <summary>Суммарное значение эффекта по открытым узлам (для аддитивных эффектов).</summary>
    public double SumEffect(GeneNode[] catalog, string effectId) {
        double s = 0;
        for (int i = 0; i < catalog.Length; i++) {
            var n = catalog[i];
            if (_unlocked.Contains(n.Id) && n.EffectId == effectId) s += n.EffectValue;
        }
        return s;
    }
    /// <summary>Возвращает true, если хотя бы один открытый узел даёт этот эффект (для флагов).</summary>
    public bool AnyEffect(GeneNode[] catalog, string effectId) {
        for (int i = 0; i < catalog.Length; i++)
            if (_unlocked.Contains(catalog[i].Id) && catalog[i].EffectId == effectId) return true;
        return false;
    }
}
/// <summary>
/// Разблокировка узлов (QA-6): зависимости, гены, компоненты; откат недопустимого выбора
/// невозможен — TryUnlock атомарен: проверяет ВСЕ условия до списания.
/// </summary>
public static class GeneNetworkService {
    public static string CanUnlock(GeneNode node, UnlockedNodes unlocked, Genome genome,
                                   ComponentsStock stock) {
        if (node == null) return "null_node";
        if (unlocked.Has(node.Id)) return "already_unlocked";
        for (int i = 0; i < node.Prerequisites.Length; i++)
            if (!unlocked.Has(node.Prerequisites[i])) return "prerequisite_missing";
        for (int i = 0; i < node.RequiredGeneFragments.Length; i++)
            if (!genome.Has(node.RequiredGeneFragments[i])) return "gene_missing";
        if (!stock.CanAfford(node.Cost)) return "components_missing";
        return null; // null = можно
    }
    /// <summary>Разблокировка атомарна: проверяет ВСЕ условия до списания.
    /// Фрагменты НЕ расходуются — они остаются знанием (один источник → одно извлечение,
    /// QA-6); повторная разблокировка того же узла запрещена через already_unlocked.</summary>
    public static bool TryUnlock(GeneNode node, UnlockedNodes unlocked, Genome genome,
                                 ComponentsStock stock) {
        if (CanUnlock(node, unlocked, genome, stock) != null) return false;
        if (!stock.TrySpend(node.Cost)) return false;
        return unlocked.Add(node.Id);
    }
}
}
