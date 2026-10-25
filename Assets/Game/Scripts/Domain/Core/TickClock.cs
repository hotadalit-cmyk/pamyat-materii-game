using System;
using System.Collections.Generic;
namespace PM.Domain.Core {
/// <summary>
/// Фиксированный игровой тик для редких систем (пищеварение, мир, ИИ-каденс).
/// Ввод/рендер остаются покадровыми; этой часовой шаг не зависит от FPS (QA-4).
/// </summary>
public sealed class TickClock {
    public readonly double TickDuration;
    private double _accumulator;
    public long TickIndex { get; private set; }
    public double WorldTime { get; private set; }
    private readonly List<Action<long>> _listeners = new List<Action<long>>();

    public TickClock(double tickDuration = 0.2) {
        if (tickDuration <= 0) throw new ArgumentOutOfRangeException(nameof(tickDuration));
        TickDuration = tickDuration;
    }
    public void AddListener(Action<long> onTick) { if (onTick != null) _listeners.Add(onTick); }

    /// <summary>Принимает произвольный кадр; возвращает число сработавших тиков (0..N).</summary>
    public int Advance(double frameDelta) {
        if (frameDelta < 0) frameDelta = 0;
        _accumulator += frameDelta;
        WorldTime += frameDelta;
        int fired = 0;
        while (_accumulator >= TickDuration) {
            _accumulator -= TickDuration;
            TickIndex++;
            fired++;
            for (int i = 0; i < _listeners.Count; i++) _listeners[i](TickIndex);
        }
        return fired;
    }
}
}
