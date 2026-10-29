using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3dMixedDurableV9CommitTests
{
    private sealed class J3jFailureInjectingWitness :
        IEconomicV9AcknowledgmentWitness
    {
        private EconomicV9WitnessEpochState _state;
        public bool RejectOnce { get; set; }
        public int Writes { get; private set; }

        public J3jFailureInjectingWitness(EconomicV9ExternalAnchor genesis)
        {
            _state = new EconomicV9WitnessEpochState(1, genesis);
        }

        public EconomicV9WitnessEpochState ReadState(string gameId)
        {
            Assert.Equal("j3j-game", gameId);
            return _state;
        }

        public EconomicV9ExternalAnchor ReadLastAcknowledged(string gameId) =>
            ReadState(gameId).LastAcknowledged;

        public bool TryAcknowledge(string gameId, long epoch,
            EconomicV9ExternalAnchor before, EconomicV9ExternalAnchor after)
        {
            Assert.Equal("j3j-game", gameId);
            Writes++;
            if (RejectOnce)
            {
                RejectOnce = false;
                return false; // Simulate witness unavailable after PRIMARY commit.
            }
            if (epoch != _state.WriterEpoch ||
                before != _state.LastAcknowledged)
                return false;
            _ = new EconomicV9PreparedExternalAdvance(before, after);
            _state = new EconomicV9WitnessEpochState(epoch, after);
            return true;
        }
    }

    [Fact]
    public void J3jLostWitnessAcknowledgmentIsRecoveredOnlyAfterFullReplay()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var genesis = new EconomicV9ExternalAnchor(
            initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
        var primary = new J3fTestTrustedStore(genesis);
        var witness = new J3jFailureInjectingWitness(genesis)
        {
            RejectOnce = true
        };
        var guarded = new EconomicV9WitnessedTrustedAnchorStore(
            primary, witness, "j3j-game", writerEpoch: 1);
        var coordinator = new EconomicV9ExternalCasTurnCoordinator(
            generated, guarded);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            EconomicV9ExternalAnchor after;
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, initial))
            {
                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.CommitEcological(host, 1UL, Demand(), Holds()));
                Assert.Equal(1UL, host.CommitSequence);
                after = primary.ReadSnapshot().Confirmed;
                Assert.Equal(0UL,
                    witness.ReadLastAcknowledged("j3j-game").Sequence);
                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.CommitEcological(host, 2UL, Demand(), Holds()));
            }
            Assert.Throws<InvalidOperationException>(() => guarded.ReadSnapshot());
            var verified = guarded.ReconcileFinalizedAcknowledgment(
                folder, generated);
            Assert.Equal(after, verified.LastAcknowledged);
            Assert.Equal(2, witness.Writes);
            Assert.Equal(after, guarded.ReadSnapshot().Confirmed);
            using var restored =
                EconomicCompleteV9DurableSaveHost.RestoreExternallyConfirmed(
                    folder, generated, primary.ReadSnapshot());
            Assert.Equal(1UL, restored.CommitSequence);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void J3jCorruptedJournalCannotAdvanceIndependentWitness()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var genesis = new EconomicV9ExternalAnchor(
            initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
        var primary = new J3fTestTrustedStore(genesis);
        var witness = new J3jFailureInjectingWitness(genesis)
        {
            RejectOnce = true
        };
        var guarded = new EconomicV9WitnessedTrustedAnchorStore(
            primary, witness, "j3j-game", writerEpoch: 1);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, initial))
                Assert.Throws<InvalidOperationException>(() =>
                    new EconomicV9ExternalCasTurnCoordinator(
                        generated, guarded).CommitEcological(
                            host, 1UL, Demand(), Holds()));
            var accepted = primary.ReadSnapshot().Confirmed;
            var journal = new EconomicEcologicalTransitionJournalArchive(folder)
                .RecordPath(accepted.CheckpointSha256Hex);
            var bytes = File.ReadAllBytes(journal);
            bytes[bytes.Length / 2] ^= 1;
            File.WriteAllBytes(journal, bytes);
            Assert.Throws<InvalidDataException>(() =>
                guarded.ReconcileFinalizedAcknowledgment(folder, generated));
            Assert.Equal(genesis,
                witness.ReadLastAcknowledged("j3j-game"));
            Assert.Equal(1, witness.Writes);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
