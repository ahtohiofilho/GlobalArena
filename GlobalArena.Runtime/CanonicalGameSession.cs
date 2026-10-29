using GlobalArena.World;

namespace GlobalArena.Runtime;

// R38: a canonical generated world and its WorldState have one session owner.
// The UI consumes snapshots; no civilization, territorial controller, or order is synthesized.
public sealed class CanonicalGameSession
{
    private WorldState _snapshot;

    public WorldGenerationResult GeneratedWorld { get; }
    public WorldState Snapshot => _snapshot;

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

    // Only an external rules/simulation owner may create a replacement snapshot.
    public void AcceptSnapshot(WorldState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!next.IsWorldBound || next.WorldBinding != _snapshot.WorldBinding)
            throw new InvalidOperationException("Session snapshot belongs to a different world or is unbound.");
        if (next.Revision < _snapshot.Revision)
            throw new InvalidOperationException("Session snapshot cannot regress its revision.");
        // A live physical economy cannot disappear or be silently
        // overwritten at the same revision by another snapshot author.
        if (_snapshot.PhysicalEconomy is not null)
        {
            if (next.PhysicalEconomy is null)
                throw new InvalidOperationException(
                    "Canonical physical economy cannot be silently removed.");
            if (next.Revision == _snapshot.Revision && !ReferenceEquals(next, _snapshot))
                throw new InvalidOperationException(
                    "Canonical physical state requires a new revision for every mutation.");
        }
        _snapshot = next;
    }
}
