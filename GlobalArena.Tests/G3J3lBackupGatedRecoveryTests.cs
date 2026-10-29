using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3dMixedDurableV9CommitTests
{
    private sealed class J3lEpochWitness : IEconomicV9AcknowledgmentWitness
    {
        public EconomicV9WitnessEpochState State { get; set; }
        public J3lEpochWitness(EconomicV9ExternalAnchor anchor) =>
            State = new EconomicV9WitnessEpochState(3L, anchor);

        public EconomicV9ExternalAnchor ReadLastAcknowledged(string gameId) =>
            ReadState(gameId).LastAcknowledged;

        public EconomicV9WitnessEpochState ReadState(string gameId)
        {
            Assert.Equal("j3l-restore", gameId);
            return State;
        }

        public bool TryAcknowledge(string gameId, long writerEpoch,
            EconomicV9ExternalAnchor before, EconomicV9ExternalAnchor after) =>
            throw new NotSupportedException();
    }

    private sealed class J3lRecoveryBackupGate :
        IEconomicV9ImmutableBackupRecoveryGate
    {
        public bool Accepted { get; set; }
        public int Calls { get; private set; }
        public bool ChangeWitnessDuringVerification { get; set; }
        public J3lEpochWitness? Witness { get; set; }

        public bool VerifyBackup(string gameId, long writerEpoch,
            EconomicV9ExternalAnchor independentlyAcknowledged)
        {
            Calls++;
            Assert.Equal("j3l-restore", gameId);
            Assert.Equal(3L, writerEpoch);
            Assert.NotNull(independentlyAcknowledged);
            if (ChangeWitnessDuringVerification && Witness is not null)
                Witness.State = new EconomicV9WitnessEpochState(
                    4L, independentlyAcknowledged);
            return Accepted;
        }
    }

    [Fact]
    public void J3lSignedBackupGatePrecedesLocalReplayAndBlocksBadEvidence()
    {
        var world = Generate();
        var genesis = Initial(world);
        var anchor = new EconomicV9ExternalAnchor(
            genesis.Sha256Hex, new string('0', 64), 0UL, 0UL);
        var primary = new J3iStaticPrimary(
            new EconomicV9ExternalAnchorSnapshot(anchor));
        var witness = new J3lEpochWitness(anchor);
        var gate = new EconomicV9FailoverAdmissionGate(
            "j3l-restore", primary, witness);
        var backup = new J3lRecoveryBackupGate { Accepted = false };
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3l-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                folder, world, genesis)) { }
            Assert.Throws<InvalidOperationException>(() =>
                gate.RestoreAfterPromotionWithVerifiedBackup(
                    folder, world, backup));
            Assert.Equal(1, backup.Calls);

            backup.Accepted = true;
            backup.ChangeWitnessDuringVerification = true;
            backup.Witness = witness;
            Assert.Throws<InvalidOperationException>(() =>
                gate.RestoreAfterPromotionWithVerifiedBackup(
                    folder, world, backup));
            Assert.Equal(2, backup.Calls);

            witness.State = new EconomicV9WitnessEpochState(3L, anchor);
            backup.ChangeWitnessDuringVerification = false;
            using var admitted = gate.RestoreAfterPromotionWithVerifiedBackup(
                folder, world, backup);
            Assert.Equal(genesis.Sha256Hex, admitted.AcceptedSha256Hex);
            Assert.Equal(3, backup.Calls);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void J3lBackupGateCannotTrustNonVersionedWitness()
    {
        var world = Generate();
        var genesis = Initial(world);
        var anchor = new EconomicV9ExternalAnchor(
            genesis.Sha256Hex, new string('0', 64), 0UL, 0UL);
        var primary = new J3iStaticPrimary(
            new EconomicV9ExternalAnchorSnapshot(anchor));
        var nonVersionedWitness = new J3iIndependentWitness(
            "j3l-restore", anchor);
        var backup = new J3lRecoveryBackupGate { Accepted = true };
        var gate = new EconomicV9FailoverAdmissionGate(
            "j3l-restore", primary, nonVersionedWitness);
        Assert.Throws<InvalidOperationException>(() =>
            gate.RestoreAfterPromotionWithVerifiedBackup(
                "nonexistent", world, backup));
        Assert.Equal(0, backup.Calls);
    }
}
