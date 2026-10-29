using System.Text.Json;
using GlobalArena.Simulation;
using Npgsql;

namespace GlobalArena.Tests;

/// <summary>
/// Uses the two independent PostgreSQL service containers of the CAS CI gate.
/// The database roles in this fixture are ADMINISTRATORS; game logins are
/// intentionally never given permission to call the rotation functions.
/// </summary>
[Collection("G3J3PostgresIntegration")]
public sealed class G3J3kWriterRotationIntegrationTests
{
    private static EconomicV9ExternalAnchor Genesis() =>
        new(new string('A', 64), new string('0', 64), 0UL, 0UL);

    private sealed class ControlledIsolation : IEconomicV9FormerWriterIsolationVerifier
    {
        public bool IsFenced { get; set; } = true;
        public int RefuseOnCall { get; set; }
        public int Calls { get; private set; }
        public bool IsIsolated(string game, long previousEpoch)
        {
            Assert.StartsWith("j3k-", game);
            Assert.Equal(1L, previousEpoch);
            Calls++;
            return IsFenced && Calls != RefuseOnCall;
        }
    }

    private static (NpgsqlDataSource Primary, NpgsqlDataSource Witness,
        string Game)? Provision()
    {
        var primary = Environment.GetEnvironmentVariable("GA_TEST_PG_CONNECTION");
        var witness = Environment.GetEnvironmentVariable(
            "GA_TEST_WITNESS_CONNECTION");
        if (string.IsNullOrWhiteSpace(primary) ||
            string.IsNullOrWhiteSpace(witness))
            return null;
        var primaryDb = NpgsqlDataSource.Create(primary);
        var witnessDb = NpgsqlDataSource.Create(witness);
        try
        {
            var assembly = typeof(G3J3kWriterRotationIntegrationTests).Assembly;
            void Apply(NpgsqlDataSource source, string suffix)
            {
                var name = assembly.GetManifestResourceNames().Single(x =>
                    x.EndsWith(suffix, StringComparison.Ordinal));
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                using var connection = source.OpenConnection();
                using var migration = new NpgsqlCommand(
                    reader.ReadToEnd(), connection);
                migration.ExecuteNonQuery();
            }
            Apply(primaryDb, "GA-G3J3g-trusted-anchor-postgres.sql");
            Apply(witnessDb, "GA-G3J3j-independent-witness.sql");
            var game = "j3k-" + Guid.NewGuid().ToString("N");
            var genesis = Genesis();
            using (var conn = primaryDb.OpenConnection())
            using (var cmd = new NpgsqlCommand(
                """
                INSERT INTO public.ga_trusted_v9_anchor(
                    game_id, snapshot, writer_epoch)
                VALUES (@game, @snapshot, 1);
                INSERT INTO public.ga_trusted_v9_game_principal(game_id, login_role)
                VALUES (@game, session_user);
                """, conn))
            {
                cmd.Parameters.AddWithValue("game", game);
                cmd.Parameters.AddWithValue("snapshot",
                    EconomicV9PostgresTrustedAnchorStore.SerializeSnapshot(
                        new EconomicV9ExternalAnchorSnapshot(genesis)));
                cmd.ExecuteNonQuery();
            }
            using (var conn = witnessDb.OpenConnection())
            using (var cmd = new NpgsqlCommand(
                """
                INSERT INTO public.ga_v9_witness(
                    game_id, acknowledged, writer_epoch)
                VALUES (@game, @anchor, 1);
                INSERT INTO public.ga_v9_witness_acl(game_id, login_role)
                VALUES (@game, session_user);
                """, conn))
            {
                cmd.Parameters.AddWithValue("game", game);
                cmd.Parameters.AddWithValue("anchor",
                    JsonSerializer.Serialize(genesis));
                cmd.ExecuteNonQuery();
            }
            return (primaryDb, witnessDb, game);
        }
        catch
        {
            primaryDb.Dispose();
            witnessDb.Dispose();
            throw;
        }
    }

    [Fact]
    public void J3kInterruptedRotationFencesOldWriterAndResumesWithoutRollback()
    {
        var setup = Provision();
        if (setup is null) return;
        using var primaryDb = setup.Value.Primary;
        using var witnessDb = setup.Value.Witness;
        var game = setup.Value.Game;
        var isolation = new ControlledIsolation { RefuseOnCall = 3 };
        var rotation = new EconomicV9PostgresWriterEpochRotation(
            primaryDb, witnessDb, isolation, game);
        var oldPrimary = new EconomicV9PostgresTrustedAnchorStore(
            primaryDb, game, writerEpoch: 1);
        var oldWitness = new EconomicV9PostgresAcknowledgmentWitness(
            witnessDb, game);
        Assert.Equal(EconomicV9WriterRotationHealth.Aligned,
            rotation.Observe().Health);
        Assert.Throws<InvalidOperationException>(() => rotation.Rotate(1));
        // Primary CAS was durable, but loss of independent fencing
        // acknowledgment halted before witness CAS.
        var pending = rotation.Observe();
        Assert.Equal(EconomicV9WriterRotationHealth.PrimaryRotatedWitnessPending,
            pending.Health);
        Assert.Equal(2L, pending.PrimaryEpoch);
        Assert.Equal(1L, pending.WitnessEpoch);
        Assert.Equal(Genesis(), oldWitness.ReadLastAcknowledged(game));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            Assert.Throws<PostgresException>(
                () => oldPrimary.ReadSnapshot()).SqlState);

        isolation.RefuseOnCall = 0;
        var completed = rotation.Rotate(1);
        Assert.Equal(1L, completed.PreviousEpoch);
        Assert.Equal(2L, completed.NewEpoch);
        Assert.Equal(Genesis(), completed.AcknowledgedAnchor);
        Assert.Equal(EconomicV9WriterRotationHealth.Aligned,
            rotation.Observe().Health);
        Assert.Equal(2L, rotation.Observe().WitnessEpoch);

        // Replaying the administrative command is idempotent after success,
        // but it cannot create an epoch 3 or mutate the checkpoint.
        Assert.Equal(completed, rotation.Rotate(1));
        Assert.Equal(2L, rotation.Observe().PrimaryEpoch);
        using var conn = witnessDb.OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT count(*) FROM public.ga_v9_witness_audit
             WHERE game_id = @game AND event_kind = 'rotate'
            """, conn);
        command.Parameters.AddWithValue("game", game);
        Assert.Equal(1L, (long)command.ExecuteScalar()!);
    }

    [Fact]
    public void J3kUnfencedOrPreparedOrDivergentStateIsRejected()
    {
        var setup = Provision();
        if (setup is null) return;
        using var primaryDb = setup.Value.Primary;
        using var witnessDb = setup.Value.Witness;
        var game = setup.Value.Game;
        var isolation = new ControlledIsolation { IsFenced = false };
        var rotation = new EconomicV9PostgresWriterEpochRotation(
            primaryDb, witnessDb, isolation, game);
        Assert.Throws<InvalidOperationException>(() => rotation.Rotate(1));
        Assert.Equal(EconomicV9WriterRotationHealth.Aligned,
            rotation.Observe().Health);

        isolation.IsFenced = true;
        var pending = new EconomicV9ExternalAnchorSnapshot(
            Genesis(), new EconomicV9PreparedExternalAdvance(
                Genesis(), new EconomicV9ExternalAnchor(
                    new string('B', 64), new string('C', 64), 1UL, 1UL)));
        using (var conn = primaryDb.OpenConnection())
        using (var cmd = new NpgsqlCommand(
            """
            UPDATE public.ga_trusted_v9_anchor
               SET snapshot = @snapshot WHERE game_id = @game
            """, conn))
        {
            cmd.Parameters.AddWithValue("snapshot",
                EconomicV9PostgresTrustedAnchorStore.SerializeSnapshot(pending));
            cmd.Parameters.AddWithValue("game", game);
            Assert.Equal(1, cmd.ExecuteNonQuery());
        }
        Assert.Equal(EconomicV9WriterRotationHealth.PreparedTransition,
            rotation.Observe().Health);
        Assert.Throws<InvalidOperationException>(() => rotation.Rotate(1));
        Assert.Equal(1L, rotation.Observe().WitnessEpoch);

        using (var conn = primaryDb.OpenConnection())
        using (var cmd = new NpgsqlCommand(
            """
            UPDATE public.ga_trusted_v9_anchor
               SET snapshot = @snapshot WHERE game_id = @game
            """, conn))
        {
            cmd.Parameters.AddWithValue("snapshot",
                EconomicV9PostgresTrustedAnchorStore.SerializeSnapshot(
                    new EconomicV9ExternalAnchorSnapshot(Genesis())));
            cmd.Parameters.AddWithValue("game", game);
            Assert.Equal(1, cmd.ExecuteNonQuery());
        }
        using (var conn = witnessDb.OpenConnection())
        using (var cmd = new NpgsqlCommand(
            """
            UPDATE public.ga_v9_witness SET acknowledged=@anchor
             WHERE game_id=@game
            """, conn))
        {
            cmd.Parameters.AddWithValue("anchor", JsonSerializer.Serialize(
                new EconomicV9ExternalAnchor(
                    new string('B', 64), new string('C', 64), 1UL, 1UL)));
            cmd.Parameters.AddWithValue("game", game);
            Assert.Equal(1, cmd.ExecuteNonQuery());
        }
        Assert.Equal(EconomicV9WriterRotationHealth.AnchorDivergence,
            rotation.Observe().Health);
        Assert.Throws<InvalidOperationException>(() => rotation.Rotate(1));
        Assert.Equal(1L, rotation.Observe().PrimaryEpoch);
    }
}
