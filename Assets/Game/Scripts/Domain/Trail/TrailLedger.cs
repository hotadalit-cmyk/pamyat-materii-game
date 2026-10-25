using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.Trail {
/// <summary>Запись следа (MATTER_PHYSICS «Влажный след»): сегмент с массой и свежестью.</summary>
public struct TrailRecord {
    public int Id;
    public Vec2 Pos;
    public double Mass;      // кг воды в сегменте
    public double Freshness; // 1 — свежий, 0 — испаряется
}
/// <summary>
/// Реестр следа: сегменты по фиксированному расстоянию (не по FPS), испарение, обратный сбор.
/// Инвариант (QA-2): повторный подбор расходует пул однократно — масса не дублируется.
/// </summary>
public sealed class TrailLedger {
    public const double SegmentDistance = 0.5; // длина сегмента в world units
    public const double EpsilonMass = 1e-4;
    public readonly int MaxRecords;
    public double EvaporationFractionPerSecond = 0.02; // экспоненциальное высыхание
    public double FreshnessDecayPerSecond = 0.05;
    private readonly List<TrailRecord> _records = new List<TrailRecord>();
    private Vec2 _pendingAnchor;
    private double _pendingMass;
    private bool _pendingActive;
    private int _nextId = 1;
    private double _now;
    public IReadOnlyList<TrailRecord> Records => _records;
    public int NextIdForSave => _nextId;

    public TrailLedger(int maxRecords = 256) { MaxRecords = maxRecords; }

    public void SetTime(double now) { _now = now; }

    /// <summary>Добавляет потерянную воду по пути. Сегменты формируются по пройденному расстоянию.</summary>
    public void Feed(Vec2 position, double waterMass) {
        if (waterMass <= 0) return;
        if (!_pendingActive) { _pendingAnchor = position; _pendingActive = true; }
        _pendingMass += waterMass;
        if ((position - _pendingAnchor).Length >= SegmentDistance) {
            Emit(position);
        }
    }
    private void Emit(Vec2 at) {
        if (_pendingMass < EpsilonMass) { _pendingMass = 0; return; }
        if (_records.Count >= MaxRecords) {
            // укрупнение: сливаем половину старейшей свежести в новую запись (производительность)
            _records.RemoveAt(0);
        }
        _records.Add(new TrailRecord { Id = _nextId++, Pos = at, Mass = _pendingMass, Freshness = 1.0 });
        _pendingMass = 0;
        _pendingAnchor = at;
    }
    /// <summary>Испарение/впитывание по времени игры (не по кадрам). Экспоненциально:
    /// малые сегменты живут долго, крупные исчезают раньше по абсолютной массе.</summary>
    public void Evaporate(double dt) {
        for (int i = _records.Count - 1; i >= 0; i--) {
            var r = _records[i];
            r.Mass -= r.Mass * EvaporationFractionPerSecond * dt;
            r.Freshness = MathUtil.Clamp01(r.Freshness - FreshnessDecayPerSecond * dt);
            if (r.Mass < EpsilonMass || r.Freshness <= 0) _records.RemoveAt(i);
            else _records[i] = r;
        }
    }
    // ---------- Импорт из сохранения ----------
    public void ClearForImport() { _records.Clear(); _pendingMass = 0; _pendingActive = false; }
    public void AddRecordForImport(int id, Vec2 pos, double mass, double freshness) {
        _records.Add(new TrailRecord { Id = id, Pos = pos, Mass = mass, Freshness = freshness });
        if (id >= _nextId) _nextId = id + 1;
    }
    /// <summary>
    /// Обратный сбор: забирает массу из ближайших записей в радиусе, НЕ дублируя:
    /// каждая запись списывается однократно и удаляется при опустошении.
    /// maxFreshness — порог свежести (сбор только «полежавшего» следа).
    /// </summary>
    public double Reabsorb(Vec2 consumerPos, double radius, double capacity, double maxFreshness = 1.0) {
        double taken = 0;
        for (int i = _records.Count - 1; i >= 0 && taken < capacity - 1e-12; i--) {
            var r = _records[i];
            if (r.Freshness > maxFreshness) continue;
            double d = (r.Pos - consumerPos).Length;
            if (d > radius) continue;
            double want = MathUtil.Min(r.Mass, capacity - taken);
            if (want <= 0) continue;
            // свежесть снижает эффективность возврата, но не дублирует массу
            double got = want * MathUtil.Lerp(0.6, 1.0, r.Freshness);
            r.Mass -= want;
            taken += got;
            if (r.Mass < EpsilonMass) _records.RemoveAt(i);
            else _records[i] = r;
        }
        return taken;
    }
}
}
