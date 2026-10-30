using GlobalArena.World;

namespace GlobalArena.Runtime;

// R38: a canonical generated world and its WorldState have one session owner.
// The UI consumes snapshots; no civilization, territorial controller, or order is synthesized.
public sealed class CanonicalGameSession
{
    private WorldState _snapshot;
    private readonly object _snapshotSync = new();

    public WorldGenerationResult GeneratedWorld { get; }
    public WorldState Snapshot
    {
        get
        {
            lock (_snapshotSync)
                return _snapshot;
        }
    }

    public CanonicalGameSession(WorldGenerationResult generatedWorld)
    {
        GeneratedWorld = generatedWorld ?? throw new ArgumentNullException(nameof(generatedWorld));
        _snapshot = WorldState.CreateBound(GeneratedWorld);
    }

    public static CanonicalGameSession Start(WorldGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CanonicalGameSession(new DeterministicWorldGenerator().Generate(request));
    }

    // The general snapshot ingress remains available to the established kernel.
    // Physical command resolvers use AcceptSnapshotIfCurrent to prevent a
    // stale command from overwriting another accepted canonical revision.
    public void AcceptSnapshot(WorldState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        lock (_snapshotSync)
            AcceptValidated(next);
    }

    /// <summary>
    /// Atomic compare-and-commit for one completed command. The expected
    /// snapshot must still be the exact canonical object that was read when
    /// the order began. The result must advance exactly one revision.
    /// </summary>
    public void AcceptSnapshotIfCurrent(WorldState expected, WorldState next)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(next);
        lock (_snapshotSync)
        {
            if (!ReferenceEquals(_snapshot, expected))
                throw new InvalidOperationException(
                    "Canonical physical command was built against a stale snapshot.");
            if (next.Revision != checked(expected.Revision + 1UL))
                throw new InvalidOperationException(
                    "Canonical command must publish exactly one new revision.");
            AcceptValidated(next);
        }
    }

    private void AcceptValidated(WorldState next)
    {
        if (!next.IsWorldBound || next.WorldBinding != _snapshot.WorldBinding)
            throw new InvalidOperationException(
                "Session snapshot belongs to a different world or is unbound.");
        if (next.Revision < _snapshot.Revision)
            throw new InvalidOperationException(
                "Session snapshot cannot regress its revision.");
        if (_snapshot.PhysicalEconomy is not null)
        {
            if (next.PhysicalEconomy is null)
                throw new InvalidOperationException(
                    "Canonical physical economy cannot be silently removed.");
            if (next.Revision == _snapshot.Revision &&
                !ReferenceEquals(next, _snapshot))
                throw new InvalidOperationException(
                    "Canonical physical state requires a new revision for every mutation.");
        }
        _snapshot = next;
    }
}
