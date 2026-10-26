using System.Collections.Generic;

namespace PM.Domain.Run {
/// <summary>Один фрагмент тела: устойчивый id (для связи с видом в Unity) и масса.</summary>
public struct Fragment {
    public int Id;
    public double Mass;
}

/// <summary>
/// Тело из фрагментов (v0.7): слизень делится пополам, чтобы пролезать в узкие трещины, и сливается
/// обратно. Ведущий фрагмент — первый в списке (за ним камера, свет и цели). Сумма масс — масса тела.
/// Правила: делится только фрагмент ≥ 2×MinFragmentMass; фрагмент ниже DissolveMass растворяется;
/// фрагментов не больше MaxFragments. Чистая логика без UnityEngine.
/// </summary>
public sealed class FragmentSet {
    public const double MinFragmentMass = 1.0;   // меньше — нельзя отделить
    public const double DissolveMass = 0.5;      // ниже — фрагмент растворяется
    public const int MaxFragments = 8;

    /// <summary>Текущий лимит фрагментов (v0.8: 2 + ветвь «Деление»; не выше MaxFragments).</summary>
    public int MaxCount = MaxFragments;
    /// <summary>Минимальная масса половинки при делении (ветвь «Деление» снижает).</summary>
    public double MinMass = MinFragmentMass;

    private readonly List<Fragment> _items = new List<Fragment>();
    private int _nextId = 1;

    public FragmentSet(double mass) {
        _items.Add(new Fragment { Id = _nextId++, Mass = mass });
    }

    public int Count => _items.Count;
    public Fragment this[int index] => _items[index];
    public int LeadId => _items.Count > 0 ? _items[0].Id : -1;

    public double Total {
        get { double s = 0; for (int i = 0; i < _items.Count; i++) s += _items[i].Mass; return s; }
    }

    public int IndexOf(int id) {
        for (int i = 0; i < _items.Count; i++) if (_items[i].Id == id) return i;
        return -1;
    }

    public double MassOf(int id) { int i = IndexOf(id); return i < 0 ? 0 : _items[i].Mass; }

    public bool CanSplit => _items.Count < System.Math.Min(MaxCount, MaxFragments) && LargestSplittable() >= 0;

    private int LargestSplittable() {
        int best = -1;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Mass >= 2 * MinMass && (best < 0 || _items[i].Mass > _items[best].Mass)) best = i;
        return best;
    }

    /// <summary>
    /// Делит каждый фрагмент, который можно, пополам (один «такт» деления), пока не упрёмся в лимит.
    /// Возвращает пары (родитель, новый) — Unity создаёт новых рядом с родителями.
    /// </summary>
    public List<int[]> SplitAll() {
        var created = new List<int[]>();
        int n = _items.Count;
        int cap = System.Math.Min(MaxCount, MaxFragments);
        for (int i = 0; i < n && _items.Count < cap; i++) {
            var f = _items[i];
            if (f.Mass < 2 * MinMass) continue;
            double half = f.Mass * 0.5;
            f.Mass = half;
            _items[i] = f;
            var nf = new Fragment { Id = _nextId++, Mass = half };
            _items.Add(nf);
            created.Add(new[] { f.Id, nf.Id });
        }
        return created;
    }

    /// <summary>keep поглощает other. Если other — ведущий, ведущим становится keep.</summary>
    public bool Merge(int keepId, int otherId) {
        if (keepId == otherId) return false;
        int k = IndexOf(keepId), o = IndexOf(otherId);
        if (k < 0 || o < 0) return false;
        var keep = _items[k];
        keep.Mass += _items[o].Mass;
        _items[k] = keep;
        _items.RemoveAt(o);
        if (o == 0) { // ведущий слился в другого — тот становится ведущим
            int nk = IndexOf(keepId);
            var lead = _items[nk];
            _items.RemoveAt(nk);
            _items.Insert(0, lead);
        }
        return true;
    }

    /// <summary>Добавить массу конкретному фрагменту (поглощение этим фрагментом).</summary>
    public void Add(int id, double amount) {
        int i = IndexOf(id);
        if (i < 0) i = 0;
        if (_items.Count == 0) return;
        var f = _items[i]; f.Mass += amount; _items[i] = f;
    }

    /// <summary>Отнять массу у фрагмента. Возвращает true, если фрагмент растворился.</summary>
    public bool Remove(int id, double amount) {
        int i = IndexOf(id);
        if (i < 0) return false;
        var f = _items[i]; f.Mass -= amount; _items[i] = f;
        if (f.Mass < DissolveMass && _items.Count > 1) { _items.RemoveAt(i); return true; }
        return false;
    }

    /// <summary>Изменение массы «тела вообще»: прибавка — ведущему, убыль — пропорционально всем.</summary>
    public void ApplyDelta(double delta) {
        if (_items.Count == 0 || delta == 0) return;
        if (delta > 0) { Add(_items[0].Id, delta); return; }
        double total = Total;
        if (total <= 0) return;
        for (int i = 0; i < _items.Count; i++) {
            var f = _items[i];
            f.Mass += delta * (f.Mass / total);
            _items[i] = f;
        }
    }

    /// <summary>Сделать фрагмент ведущим (игрок переключился на него).</summary>
    public void MakeLead(int id) {
        int i = IndexOf(id);
        if (i <= 0) return;
        var f = _items[i];
        _items.RemoveAt(i);
        _items.Insert(0, f);
    }

    public int[] Ids() {
        var ids = new int[_items.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = _items[i].Id;
        return ids;
    }
}
}
