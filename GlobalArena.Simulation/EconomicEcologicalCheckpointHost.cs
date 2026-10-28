using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Small in-memory acceptance boundary for a trusted game host. The host
/// serializes next-turn proposals under one lock and retains the accepted
/// digest. Exported sidecar bytes can be persisted by an external save layer.
/// This does not implement disk writes, game commands, construction, or
/// the authoritative World's turn scheduler.
/// </summary>
public sealed class EconomicEcologicalCheckpointHost
{
    private readonly object _gate = new();
    private readonly WorldGenerationResult _generatedWorld;
    private readonly EconomicEcologicalTurnCommitResolver _turns = new();
    private EconomicEcologicalCheckpoint _accepted;

    public EconomicEcologicalCheckpointHost(
        WorldGenerationResult generatedWorld,
        EconomicEcologicalCheckpoint trustedInitial)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(trustedInitial);
        if (trustedInitial.World.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException("Host and initial checkpoint use different worlds.");

        _generatedWorld = generatedWorld;
        _accepted = trustedInitial;
    }

    /// <summary>
    /// Resume requires the last accepted commitment from a separately
    /// trustworthy source. The saved sidecar cannot attest to itself.
    /// </summary>
    public static EconomicEcologicalCheckpointHost Restore(
        WorldGenerationResult generatedWorld, WorldState world,
        byte[] sidecar, string trustedAcceptedDigest)
    {
        var checkpoint = EconomicEcologicalSidecarCodec.Decode(
            generatedWorld, world, sidecar);
        checkpoint.RequireExpectedDigest(trustedAcceptedDigest);
        return new EconomicEcologicalCheckpointHost(generatedWorld, checkpoint);
    }

    public string AcceptedSha256Hex
    {
        get { lock (_gate) return _accepted.Sha256Hex; }
    }

    public ulong LastAppliedTurn
    {
        get { lock (_gate) return _accepted.NaturalCapital.LastAppliedTurn; }
    }

    public byte[] CaptureSidecar()
    {
        lock (_gate)
            return EconomicEcologicalSidecarCodec.Encode(_accepted);
    }

    public EconomicEcologicalTurnCommitResult CommitNext(
        string expectedAcceptedSha256Hex, ulong turnOrdinal,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        lock (_gate)
        {
            var result = _turns.Propose(
                _generatedWorld, _accepted, expectedAcceptedSha256Hex,
                turnOrdinal, workerDemand, originHolds);
            // No accepted state is replaced unless all ecological and
            // material validations completed successfully.
            _accepted = result.ProposedNext;
            return result;
        }
    }
}
