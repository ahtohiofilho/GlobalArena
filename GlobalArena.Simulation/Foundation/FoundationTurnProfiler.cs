using System.Diagnostics;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Explicit synchronous, thread-local diagnostics only. No time or
/// allocation reading is used by the simulation, state, journal or decisions.
/// Measurements are inclusive; nested phases must not be summed.</summary>
public static class FoundationTurnProfiler
{
    [ThreadStatic] private static Session? _current;

    public readonly record struct Sample(string Phase, long Calls, long ElapsedMicroseconds, long AllocatedBytes);

    public static Session? Begin(bool enabled)
    {
        if (!enabled) return null;
        if (_current is not null) throw new InvalidOperationException("Nested turn profiling session.");
        return _current = new Session();
    }

    public static Scope Measure(string phase) => _current is { } session
        ? new Scope(session, phase) : default;

    public sealed class Session : IDisposable
    {
        private readonly Dictionary<string, (long Calls, long Ticks, long Bytes)> _data = new(StringComparer.Ordinal);
        internal void Add(string phase, long ticks, long bytes)
        {
            var prior = _data.GetValueOrDefault(phase);
            _data[phase] = (prior.Calls + 1, prior.Ticks + ticks, prior.Bytes + bytes);
        }
        public IReadOnlyList<Sample> Read() => _data.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new Sample(p.Key, p.Value.Calls,
                (long)(p.Value.Ticks * (1_000_000d / Stopwatch.Frequency)), p.Value.Bytes)).ToArray();
        public void Dispose()
        {
            if (ReferenceEquals(_current, this)) _current = null;
        }
    }

    public readonly struct Scope : IDisposable
    {
        private readonly Session? _session;
        private readonly string? _phase;
        private readonly long _start;
        private readonly long _allocated;
        internal Scope(Session session, string phase)
        {
            _session = session;
            _phase = phase;
            _allocated = GC.GetAllocatedBytesForCurrentThread();
            _start = Stopwatch.GetTimestamp();
        }
        public void Dispose()
        {
            if (_session is null) return;
            long ticks = Stopwatch.GetTimestamp() - _start;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - _allocated;
            _session.Add(_phase!, ticks, bytes);
        }
    }
}
