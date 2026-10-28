using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Externally recorded turn input and its expected committed checkpoint.
/// The host must authenticate and persist this journal independently.
/// </summary>
public sealed class EconomicEcologicalReplayStep
{
    public ulong TurnOrdinal { get; }
    public string ExpectedCheckpointSha256Hex { get; }
    public IReadOnlyList<EconomicDemandProfile> WorkerDemand { get; }
    public IReadOnlyList<EconomicMaterialOriginHoldOrder> OriginHolds { get; }

    public EconomicEcologicalReplayStep(
        ulong turnOrdinal, string expectedCheckpointSha256Hex,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(expectedCheckpointSha256Hex);
        if (expectedCheckpointSha256Hex.Length != 64 ||
            !expectedCheckpointSha256Hex.All(Uri.IsHexDigit))
            throw new ArgumentException(
                "Replay expected commitment must be a SHA-256 digest.",
                nameof(expectedCheckpointSha256Hex));
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        var demand = workerDemand.ToArray();
        var holds = originHolds.ToArray();
        if (demand.Any(x => x is null) || holds.Any(x => x is null))
            throw new ArgumentException("Replay input cannot contain null orders.");

        TurnOrdinal = turnOrdinal;
        ExpectedCheckpointSha256Hex =
            expectedCheckpointSha256Hex.ToUpperInvariant();
        WorkerDemand = Array.AsReadOnly(demand);
        OriginHolds = Array.AsReadOnly(holds);
    }
}

/// <summary>
/// Fail-closed deterministic replay of ecological/production/withholding
/// projections from a decoded sidecar plus an independently supplied
/// WorldState. The input journal is NOT authenticated by this class.
/// Market payments and delivery events remain outside this reference.
/// </summary>
public sealed class EconomicEcologicalSidecarReplayVerifier
{
    private readonly EconomicEcologicalTurnCommitResolver _turns = new();

    public EconomicEcologicalCheckpoint Replay(
        WorldGenerationResult generatedWorld, WorldState world,
        byte[] initialSidecar,
        IEnumerable<EconomicEcologicalReplayStep> recordedSteps)
    {
        ArgumentNullException.ThrowIfNull(recordedSteps);
        var current = EconomicEcologicalSidecarCodec.Decode(
            generatedWorld, world, initialSidecar);

        foreach (var step in recordedSteps)
        {
            if (step is null)
                throw new ArgumentException("Replay steps cannot contain null.", nameof(recordedSteps));
            var proposal = _turns.Propose(
                generatedWorld, current, current.Sha256Hex,
                step.TurnOrdinal, step.WorkerDemand, step.OriginHolds);

            if (!string.Equals(
                proposal.ProposedNext.Sha256Hex,
                step.ExpectedCheckpointSha256Hex,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Replay diverged from the recorded ecological checkpoint.");
            current = proposal.ProposedNext;
        }

        return current;
    }
}
