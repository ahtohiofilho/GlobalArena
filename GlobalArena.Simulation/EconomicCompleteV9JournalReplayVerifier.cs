using System.Security.Cryptography;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Replays every journal transition from the accepted full-v9 head back to
/// its genesis (or independently authenticated migrated genesis) bundle.
/// This verifies model determinism and correspondence to committed snapshots.
/// It is NOT proof of command authorization or market delivery, and relies
/// on an externally trusted accepted digest to prevent whole-chain rollback.
/// </summary>
public sealed class EconomicCompleteV9JournalReplayVerifier
{
    private readonly EconomicEcologicalTurnCommitResolver _resolver = new();

    public void VerifyChain(
        WorldGenerationResult generatedWorld,
        EconomicCompleteV9SaveBundleArchive bundles,
        EconomicEcologicalTransitionJournalArchive journal,
        string acceptedCheckpointSha256Hex, ulong commitSequence)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(bundles);
        ArgumentNullException.ThrowIfNull(journal);
        var digest = EconomicEcologicalSidecarArchive.NormalizeDigest(
            acceptedCheckpointSha256Hex);
        var current = bundles.Load(generatedWorld, digest);

        for (var remaining = commitSequence; remaining > 0UL; remaining--)
        {
            var step = journal.Load(current.Sha256Hex);
            if (step.ResultCheckpointSha256Hex != current.Sha256Hex ||
                step.TurnOrdinal != current.NaturalCapital.LastAppliedTurn)
                throw new InvalidDataException(
                    "Committed journal turn disagrees with the complete save.");

            var previous = bundles.Load(
                generatedWorld, step.PreviousCheckpointSha256Hex);
            if (previous.NaturalCapital.LastAppliedTurn == ulong.MaxValue ||
                previous.NaturalCapital.LastAppliedTurn + 1UL != step.TurnOrdinal ||
                previous.WorldHash != current.WorldHash)
                throw new InvalidDataException(
                    "Journal predecessor is from another world or turn.");

            var result = _resolver.Propose(
                generatedWorld, previous, previous.Sha256Hex,
                step.TurnOrdinal, step.WorkerDemand, step.OriginHolds);

            if (result.ProposedNext.Sha256Hex != current.Sha256Hex ||
                !CryptographicOperations.FixedTimeEquals(
                    EconomicCompleteV9SaveBundleCodec.Encode(result.ProposedNext),
                    EconomicCompleteV9SaveBundleCodec.Encode(current)))
                throw new InvalidOperationException(
                    "Ecological journal replay diverged from the committed full save.");

            current = previous;
        }

        // The final predecessor is the sequence-zero checkpoint, not
        // necessarily turn zero after an authenticated v8 migration.
        // No journal is required for that initial import.
    }
}
