using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.Absorption {
/// <summary>
/// Пищеварительная камера (T10): ограниченная вместимость груза, обработка по игровому времени,
/// выдача компонентов/генов один раз. Пауза останавливает процесс (тик не тикает — гарантировано
/// вызовом Tick только из игрового шага).
/// </summary>
public sealed class DigestionChamber {
    public double CargoCapacity;
    public int MaxParallel = 1; // демо: последовательная обработка
    public double BaseSpeed = 1.0; // множитель скорости переваривания
    private readonly List<DigestionItem> _items = new List<DigestionItem>();
    public IReadOnlyList<DigestionItem> Items => _items;
    public double OccupiedCargo {
        get {
            double s = 0;
            for (int i = 0; i < _items.Count; i++) s += _items[i].CargoMass;
            return s;
        }
    }
    public DigestionChamber(double cargoCapacity = 3.0) { CargoCapacity = cargoCapacity; }

    /// <summary>Добавление добычи. Отказ: дубликат, переполнение груза, не-интернализированная фаза.</summary>
    public bool TryAdd(int preyId, MaterialClass cls, double cargoMass, double seconds, DigestionYield yield,
                       double? startRemainingSec = null) {
        if (cargoMass <= 0) return false;
        if (OccupiedCargo + cargoMass > CargoCapacity + 1e-9) return false;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].PreyId == preyId) return false; // двойное поглощение невозможно
        _items.Add(new DigestionItem {
            PreyId = preyId, Class = cls, CargoMass = cargoMass,
            RemainingSec = MathUtil.Clamp(startRemainingSec ?? MathUtil.Max(0.01, seconds), 0, MathUtil.Max(0.01, seconds)),
            TotalSec = seconds, Yield = yield,
            Yielded = false
        });
        return true;
    }
    /// <summary>Тик по игровому времени. Обрабатывается до MaxParallel элементов.</summary>
    public void Tick(double dt) {
        int processed = 0;
        for (int i = 0; i < _items.Count && processed < MaxParallel; i++) {
            var it = _items[i];
            if (it.Yielded) continue;
            it.RemainingSec = MathUtil.Max(0, it.RemainingSec - dt * BaseSpeed);
            _items[i] = it;
            processed++;
        }
    }
    /// <summary>Забрать готовые записи. Каждый результат — ровно один раз (Yielded=true навсегда).</summary>
    public List<DigestionItem> HarvestReadyItems() {
        var result = new List<DigestionItem>();
        for (int i = _items.Count - 1; i >= 0; i--) {
            var it = _items[i];
            if (it.RemainingSec > 0 || it.Yielded) continue;
            it.Yielded = true;
            result.Add(it);
            _items.RemoveAt(i); // Consumed: запись удаляется после выдачи
        }
        return result;
    }
}
}
