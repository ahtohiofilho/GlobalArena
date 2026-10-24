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
        _snapshot = next;
    }
}
