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

    /// <summary>
    /// R86 explicit one-time pre-turn fine-first adoption. Replaces only
    /// the physical world identity at the immutable session boundary, after
    /// cryptographic re-verification of the exact G18 fields. This method
    /// is NOT a generic world-binding swap or migration for live games.
    /// </summary>
    public void AcceptFineFirstOpening(
        WorldState opening,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fine,
        FoundationFineFirstWorldgenIdentity signature)
    {
        ArgumentNullException.ThrowIfNull(opening);
        if (_snapshot.WorldBinding !=
                RuntimeWorldBinding.FromGeneratedWorld(GeneratedWorld) ||
            _snapshot.Transport.IsInitialized ||
            _snapshot.Treasury.IsInitialized ||
            opening.WorldBinding != RuntimeWorldBinding.FromFineFirst(
                GeneratedWorld, incidence, fine, signature) ||
            opening.Revision != _snapshot.Revision ||
            !ReferenceEquals(opening.Workers, _snapshot.Workers) ||
            !ReferenceEquals(opening.Civilizations, _snapshot.Civilizations) ||
            !opening.Workers.HasAuthoritativeOccupations ||
            !opening.Transport.HasMonetaryConstructionCosts ||
            !opening.Treasury.IsInitialized)
            throw new InvalidOperationException(
                "Session can adopt signed fine-world geography only once " +
                "before economic turns, retaining the same worker/civilization " +
                "census and fully initialized G18 freight and money.");
        _snapshot = opening;
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
