using System.Security.Cryptography;
using GlobalArena.World;
using GlobalArena.Kernel;

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
        string acceptedCheckpointSha256Hex, ulong commitSequence,
        string expectedLatestJournalSha256Hex,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null,
        string? independentlyTrustedJournalRoot = null)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(bundles);
        ArgumentNullException.ThrowIfNull(journal);
        var digest = EconomicEcologicalSidecarArchive.NormalizeDigest(
            acceptedCheckpointSha256Hex);
        var current = bundles.Load(generatedWorld, digest);
        var expectedJournal = EconomicEcologicalSidecarArchive.NormalizeDigest(
            expectedLatestJournalSha256Hex);
        var zero = new string('0', 64);
        if (independentlyTrustedJournalRoot is not null &&
            EconomicEcologicalSidecarArchive.NormalizeDigest(
                independentlyTrustedJournalRoot) != expectedJournal)
            throw new InvalidOperationException(
                "Durable journal root differs from independently trusted root.");
        var mixedArchive = new EconomicMixedV9TransitionArchive(
            bundles.DirectoryPath);
        if ((commitSequence == 0UL) != (expectedJournal == zero))
            throw new InvalidDataException("Journal head and sequence disagree.");

        for (var remaining = commitSequence; remaining > 0UL; remaining--)
        {
            var hasEcology = File.Exists(journal.RecordPath(current.Sha256Hex));
            var hasMixed = File.Exists(mixedArchive.RecordPath(current.Sha256Hex));
            if (hasEcology == hasMixed)
                throw new InvalidDataException(
                    "Committed transition requires exactly one journal type.");
            if (hasMixed)
            {
                // A mixed entry changes the WorldState hash. Verify its
                // signed military actions and ecological books together.
                if (historicalVerifier is null ||
                    independentlyTrustedJournalRoot is null)
                    throw new InvalidOperationException(
                        "Mixed replay requires historical host key and independently trusted journal root.");
                var step = mixedArchive.Load(current.Sha256Hex);
                var actual = Convert.ToHexString(
                    SHA256.HashData(EconomicMixedV9TransitionCodec.Encode(step)));
                if (actual != expectedJournal ||
                    step.TurnOrdinal != current.NaturalCapital.LastAppliedTurn)
                    throw new InvalidDataException(
                        "Mixed journal differs from accepted chain or turn.");
                expectedJournal = step.PreviousJournalSha256Hex;
                var previous = bundles.Load(
                    generatedWorld, step.PreviousCheckpointSha256Hex);
                if (previous.NaturalCapital.LastAppliedTurn == ulong.MaxValue ||
                    previous.NaturalCapital.LastAppliedTurn + 1UL != step.TurnOrdinal)
                    throw new InvalidDataException(
                        "Mixed journal predecessor is from another turn.");
                var projector = new HistoricalMilitaryEcologicalTurnProjector(
                    generatedWorld, historicalVerifier);
                var replay = projector.Propose(
                    previous, previous.Sha256Hex,
                    new SimulationContext(
                        new TurnNumber(step.TurnOrdinal),
                        new SimulationSeed(step.Seed)),
                    step.MilitaryAuthorizationCertificate,
                    step.WorkerDemand, step.OriginHolds);
                if (replay.ProposedCheckpointSha256Hex != current.Sha256Hex ||
                    !CryptographicOperations.FixedTimeEquals(
                        replay.CompleteV9Bundle,
                        EconomicCompleteV9SaveBundleCodec.Encode(current)))
                    throw new InvalidOperationException(
                        "Mixed military/ecological transition failed deterministic replay.");
                current = previous;
                continue;
            }

            var ecological = journal.Load(current.Sha256Hex);
            var actualJournal = Convert.ToHexString(
                SHA256.HashData(EconomicEcologicalTransitionJournalCodec.Encode(
                    ecological)));
            if (actualJournal != expectedJournal)
                throw new InvalidDataException(
                    "Ecological input journal differs from the head-anchored hash chain.");
            expectedJournal = ecological.PreviousJournalSha256Hex;
            if (ecological.ResultCheckpointSha256Hex != current.Sha256Hex ||
                ecological.TurnOrdinal != current.NaturalCapital.LastAppliedTurn)
                throw new InvalidDataException(
                    "Committed journal turn disagrees with the complete save.");

            var prior = bundles.Load(
                generatedWorld, ecological.PreviousCheckpointSha256Hex);
            if (prior.NaturalCapital.LastAppliedTurn == ulong.MaxValue ||
                prior.NaturalCapital.LastAppliedTurn + 1UL != ecological.TurnOrdinal ||
                prior.WorldHash != current.WorldHash)
                throw new InvalidDataException(
                    "Journal predecessor is from another world or turn.");

            var result = _resolver.Propose(
                generatedWorld, prior, prior.Sha256Hex,
                ecological.TurnOrdinal, ecological.WorkerDemand,
                ecological.OriginHolds);

            if (result.ProposedNext.Sha256Hex != current.Sha256Hex ||
                !CryptographicOperations.FixedTimeEquals(
                    EconomicCompleteV9SaveBundleCodec.Encode(result.ProposedNext),
                    EconomicCompleteV9SaveBundleCodec.Encode(current)))
                throw new InvalidOperationException(
                    "Ecological journal replay diverged from the committed full save.");

            current = prior;
        }

        if (expectedJournal != zero)
            throw new InvalidDataException(
                "Ecological journal chain did not reach its genesis anchor.");

        // The final predecessor is the sequence-zero checkpoint, not
        // necessarily turn zero after an authenticated v8 migration.
        // No journal is required for that initial import.
    }
}
