using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3dMixedDurableV9CommitTests
{
    private sealed class J3iStaticPrimary : IEconomicV9TrustedAnchorStore
    {
        public EconomicV9ExternalAnchorSnapshot Snapshot { get; set; }
        public J3iStaticPrimary(EconomicV9ExternalAnchorSnapshot snapshot) =>
            Snapshot = snapshot;
        public EconomicV9ExternalAnchorSnapshot ReadSnapshot() => Snapshot;
        public bool TryPrepare(EconomicV9ExternalAnchorSnapshot expected,
            EconomicV9PreparedExternalAdvance requested) =>
            throw new NotSupportedException();
        public bool TryFinalize(EconomicV9PreparedExternalAdvance expected) =>
            throw new NotSupportedException();
    }

    private sealed class J3iIndependentWitness :
        IEconomicV9IndependentFailoverWitness
    {
        public EconomicV9ExternalAnchor Acknowledged { get; set; }
        public EconomicV9ExternalAnchor? ChangedOnSecondRead { get; set; }
        public int Reads { get; private set; }
        public string ExpectedGameId { get; }

        public J3iIndependentWitness(
            string gameId, EconomicV9ExternalAnchor acknowledged)
        {
            ExpectedGameId = gameId;
            Acknowledged = acknowledged;
        }

        public EconomicV9ExternalAnchor ReadLastAcknowledged(string gameId)
        {
            Assert.Equal(ExpectedGameId, gameId);
            Reads++;
            return Reads >= 2 && ChangedOnSecondRead is not null
                ? ChangedOnSecondRead : Acknowledged;
        }
    }

    [Fact]
    public void J3iMatchingExternalWitnessAllowsCompleteReplayAfterPromotion()
    {
        var world = Generate();
        var initial = Initial(world);
        const string game = "test-failover-genesis";
        var anchor = new EconomicV9ExternalAnchor(
            initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
        var primary = new J3iStaticPrimary(
            new EconomicV9ExternalAnchorSnapshot(anchor));
        var witness = new J3iIndependentWitness(game, anchor);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3i-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                folder, world, initial)) { }

            using var recovered = new EconomicV9FailoverAdmissionGate(
                game, primary, witness).RestoreAfterPromotion(folder, world);
            Assert.Equal(initial.Sha256Hex, recovered.AcceptedSha256Hex);
            Assert.Equal(0UL, recovered.CommitSequence);
            Assert.Equal(2, witness.Reads);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void J3iLostAcknowledgedTurnAndUnresolvedPrepareBothFailClosed()
    {
        var world = Generate();
        var initial = Initial(world);
        const string game = "test-failover-lost-commit";
        var genesis = new EconomicV9ExternalAnchor(
            initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3i-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            EconomicV9ExternalAnchor next;
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, world, initial))
            {
                var result = host.CommitNext(initial.Sha256Hex, 1UL,
                    Demand(), Holds());
                next = new EconomicV9ExternalAnchor(
                    result.ProposedNext.Sha256Hex,
                    host.AcceptedJournalSha256Hex, 1UL, 1UL);
            }

            var primary = new J3iStaticPrimary(
                new EconomicV9ExternalAnchorSnapshot(genesis));
            var witness = new J3iIndependentWitness(game, next);
            var gate = new EconomicV9FailoverAdmissionGate(
                game, primary, witness);
            // Promoted replica lost the previously acknowledged turn.
            Assert.Throws<InvalidOperationException>(() =>
                gate.RestoreAfterPromotion(folder, world));
            // A pending prepare is not allowed even if the confirmed
            // checkpoint matches the independent witness.
            primary.Snapshot = new EconomicV9ExternalAnchorSnapshot(
                genesis, new EconomicV9PreparedExternalAdvance(genesis, next));
            witness.Acknowledged = genesis;
            Assert.Throws<InvalidOperationException>(() =>
                gate.RestoreAfterPromotion(folder, world));
            Assert.Throws<IOException>(() =>
                {
                    // This is never an automatic rollback/overwrite of local
                    // head merely because the promoted primary is older.
                    using var restored = EconomicCompleteV9DurableSaveHost.Create(
                        folder, world, initial);
                });
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void J3iWitnessRacingDuringReplayCannotAdmitLocalSave()
    {
        var world = Generate();
        var initial = Initial(world);
        const string game = "test-failover-race";
        var anchor = new EconomicV9ExternalAnchor(
            initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
        var changed = new EconomicV9ExternalAnchor(
            new string('F', 64), new string('A', 64), 1UL, 1UL);
        var primary = new J3iStaticPrimary(
            new EconomicV9ExternalAnchorSnapshot(anchor));
        var witness = new J3iIndependentWitness(game, anchor)
        {
            ChangedOnSecondRead = changed
        };
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3i-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                folder, world, initial)) { }
            Assert.Throws<InvalidOperationException>(() =>
                new EconomicV9FailoverAdmissionGate(game, primary, witness)
                    .RestoreAfterPromotion(folder, world));
            // An unsuccessful gate releases the durable writer lease.
            using var valid = EconomicCompleteV9DurableSaveHost.Restore(
                folder, world, initial.Sha256Hex);
            Assert.Equal(initial.Sha256Hex, valid.AcceptedSha256Hex);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
