using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.AI {
public enum MemoryEventKind { HelpedBy = 0, AttackedBy = 1, StolenBy = 2, Observed = 3 }
public struct MemoryEvent {
    public MemoryEventKind Kind;
    public int ActorId;
    public long Tick;
}
/// <summary>Память ИИ-души: ключевые события + доверие (SOUL_WORLD «ИИ-души в демо»).</summary>
public sealed class AiMemory {
    public const int MaxEvents = 32;
    public double Trust; // -100..100
    private readonly List<MemoryEvent> _events = new List<MemoryEvent>();
    public IReadOnlyList<MemoryEvent> Events => _events;
    public void Record(MemoryEventKind kind, int actorId, long tick) {
        _events.Add(new MemoryEvent { Kind = kind, ActorId = actorId, Tick = tick });
        if (_events.Count > MaxEvents) _events.RemoveAt(0);
        switch (kind) {
            case MemoryEventKind.HelpedBy: Trust = MathUtil.Clamp(Trust + 20, -100, 100); break;
            case MemoryEventKind.AttackedBy: Trust = MathUtil.Clamp(Trust - 35, -100, 100); break;
            case MemoryEventKind.StolenBy: Trust = MathUtil.Clamp(Trust - 25, -100, 100); break;
        }
    }
    public bool Recently(MemoryEventKind kind, int actorId, long currentTick, int windowTicks) {
        for (int i = _events.Count - 1; i >= 0; i--) {
            var e = _events[i];
            if (e.Kind == kind && e.ActorId == actorId && currentTick - e.Tick <= windowTicks)
                return true;
        }
        return false;
    }
}
}
