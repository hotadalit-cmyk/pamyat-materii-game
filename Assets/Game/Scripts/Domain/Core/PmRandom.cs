namespace PM.Domain.Core {
/// <summary>Детерминированный xorshift64*-генератор для воспроизводимой симуляции и тестов.</summary>
public sealed class PmRandom {
    private ulong _state;
    public PmRandom(ulong seed) { _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
    public ulong NextUInt() {
        ulong x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return x * 0x2545F4914F6CDD1DUL;
    }
    /// <summary>[0..1) детерминированно.</summary>
    public double NextDouble01() => (NextUInt() >> 11) * (1.0 / 9007199254740992.0);
    public double Range(double minInclusive, double maxExclusive) =>
        minInclusive + (maxExclusive - minInclusive) * NextDouble01();
    public int NextInt(int maxExclusive) => (int)(NextDouble01() * maxExclusive);
}
}
