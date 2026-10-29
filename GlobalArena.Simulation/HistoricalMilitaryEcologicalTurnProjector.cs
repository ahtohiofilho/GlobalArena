using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A fully serialized, replayable combined candidate. Neither its existence
/// nor serialization constitutes an accepted gameplay turn. Only an eventual
/// single-owner transaction may publish the candidate and its evidence.
/// </summary>
public sealed class HistoricalMilitaryEcologicalTurnCandidate
{
    private readonly byte[] _completeV9Bundle;
    private readonly byte[] _authorizationCertificate;

    public EconomicEcologicalCheckpoint AcceptedPrevious { get; }
    public AuthorizedMilitaryMoveAuditProjection MilitaryAudit { get; }
    public EconomicEcologicalTurnCommitResult EconomicProjection { get; }
    public string PreviousCheckpointSha256Hex => AcceptedPrevious.Sha256Hex;
    public string ProposedCheckpointSha256Hex =>
        EconomicProjection.ProposedNext.Sha256Hex;
    public byte[] CompleteV9Bundle => (byte[])_completeV9Bundle.Clone();
    public byte[] HistoricalAuthorizationCertificate =>
        (byte[])_authorizationCertificate.Clone();

    internal HistoricalMilitaryEcologicalTurnCandidate(
        EconomicEcologicalCheckpoint accepted,
        AuthorizedMilitaryMoveAuditProjection military,
        EconomicEcologicalTurnCommitResult economic, byte[] bundle,
        byte[] certificate)
    {
        AcceptedPrevious = accepted;
        MilitaryAudit = military;
        EconomicProjection = economic;
        _completeV9Bundle = (byte[])bundle.Clone();
        _authorizationCertificate = (byte[])certificate.Clone();
    }
}

/// <summary>
/// G3-J3c first mixed world/ecology PRE-PUBLICATION transaction: verify
/// historical host authorization and kernel movement trace, stage resulting
/// warfare changes, then project actual natural resource production/reserves
/// using the changed WorldState in ONE version-9 save candidate.
///
/// Does NOT publish an active CanonicalGameSession, durable head or journal.
/// The old ecological journal currently assumes stable WorldState hashes;
/// this combined candidate cannot be committed with its old protocol.
/// </summary>
public sealed class HistoricalMilitaryEcologicalTurnProjector
{
    private readonly WorldGenerationResult _generated;
    private readonly HistoricalMilitaryMoveAuthorizationSeal _seal;
    private readonly EconomicEcologicalTurnCommitResolver _ecology = new();

    public HistoricalMilitaryEcologicalTurnProjector(
        WorldGenerationResult generatedWorld,
        HistoricalMilitaryMoveAuthorizationSeal certificateVerifier)
    {
        _generated = generatedWorld ??
            throw new ArgumentNullException(nameof(generatedWorld));
        _seal = certificateVerifier ??
            throw new ArgumentNullException(nameof(certificateVerifier));
    }

    public HistoricalMilitaryEcologicalTurnCandidate Propose(
        EconomicEcologicalCheckpoint accepted,
        string independentlyTrustedPreviousDigest,
        SimulationContext independentlyTrustedContext,
        byte[] signedHistoricalAuthorization,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(signedHistoricalAuthorization);
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        accepted.RequireExpectedDigest(independentlyTrustedPreviousDigest);
        if (accepted.NaturalCapital.LastAppliedTurn == ulong.MaxValue ||
            accepted.NaturalCapital.LastAppliedTurn + 1UL !=
                independentlyTrustedContext.Turn.Value)
            throw new InvalidOperationException(
                "Mixed military/ecological candidate must advance one sequential turn.");

        var demand = workerDemand.ToArray();
        var holds = originHolds.ToArray();
        if (demand.Any(x => x is null) || holds.Any(x => x is null))
            throw new ArgumentException("Mixed turn cannot contain null economic orders.");

        var military = _seal.Verify(
            accepted, independentlyTrustedPreviousDigest,
            independentlyTrustedContext, signedHistoricalAuthorization);
        var stagedWorld = military.Resolution.ResultingWorldState;
        if (stagedWorld.WorldBinding != accepted.World.WorldBinding ||
            new CanonicalWorldStateHasher().Compute(stagedWorld).FormatVersion != 9U)
            throw new InvalidOperationException(
                "Authenticated military transition produced invalid staged v9 world.");

        // Only military unit movement and revision are supported by this
        // certificate. All non-warfare domains MUST be reference-identical:
        // no fabricated territory, new facility, worker, or trade settlement.
        if (!ReferenceEquals(stagedWorld.Civilizations, accepted.World.Civilizations) ||
            !ReferenceEquals(stagedWorld.Territory, accepted.World.Territory) ||
            !ReferenceEquals(stagedWorld.Diplomacy, accepted.World.Diplomacy) ||
            !ReferenceEquals(stagedWorld.Economy, accepted.World.Economy) ||
            !ReferenceEquals(stagedWorld.Population, accepted.World.Population) ||
            !ReferenceEquals(stagedWorld.TransportFacilities,
                accepted.World.TransportFacilities))
            throw new InvalidOperationException(
                "Military-only candidate cannot change other authoritative domains.");

        // Bind unchanged ecological physical books to the new world first.
        // The next ecological turn is calculated on this actual staged world,
        // not the stale pre-movement world hash.
        var staged = new EconomicEcologicalCheckpoint(
            _generated, stagedWorld, accepted.NaturalCapital,
            accepted.MaterialReserves, accepted.Policy, accepted.Commodities);
        var next = _ecology.Propose(
            _generated, staged, staged.Sha256Hex,
            independentlyTrustedContext.Turn.Value, demand, holds);

        if (next.ProposedNext.WorldHash !=
                new CanonicalWorldStateHasher().Compute(stagedWorld) ||
            next.ProposedNext.NaturalCapital.LastAppliedTurn !=
                independentlyTrustedContext.Turn.Value)
            throw new InvalidOperationException(
                "Combined candidate did not bind ecological books to the actual military world.");

        var bundle = EconomicCompleteV9SaveBundleCodec.Encode(next.ProposedNext);
        var reloaded = EconomicCompleteV9SaveBundleCodec.Decode(
            _generated, bundle);
        if (reloaded.Sha256Hex != next.ProposedNext.Sha256Hex ||
            reloaded.WorldHash != next.ProposedNext.WorldHash)
            throw new InvalidOperationException(
                "Combined v9 save failed its canonical round trip.");

        return new HistoricalMilitaryEcologicalTurnCandidate(
            accepted, military, next, bundle, signedHistoricalAuthorization);
    }
}
