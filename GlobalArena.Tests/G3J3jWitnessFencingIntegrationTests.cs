using System.Reflection;
using GlobalArena.Simulation;
using Npgsql;
using NpgsqlTypes;

namespace GlobalArena.Tests;

/// <summary>
/// G3-J3j real two-independent-PostgreSQL connection test. Each service has
/// its OWN server/volume in CI (5432 for authority; 55434 for witness).
/// </summary>
[Collection("G3J3PostgresIntegration")]
public sealed class G3J3jWitnessFencingIntegrationTests
{
    private static EconomicV9ExternalAnchor Anchor(char checkpoint,
        char journal, ulong sequence, ulong turn) =>
        new(new string(checkpoint, 64), new string(journal, 64),
            sequence, turn);

    private static void Apply(NpgsqlDataSource source, string suffix)
    {
        var assembly = typeof(G3J3jWitnessFencingIntegrationTests).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(x =>
            x.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        using var connection = source.OpenConnection();
        using var sql = new NpgsqlCommand(reader.ReadToEnd(), connection);
        sql.ExecuteNonQuery();
    }

    [Fact]
    public void J3jTwoIndependentDatabasesRejectOldEpochAndRollback()
    {
        var mainConnection = Environment.GetEnvironmentVariable(
            "GA_TEST_PG_CONNECTION");
        var witnessConnection = Environment.GetEnvironmentVariable(
            "GA_TEST_WITNESS_CONNECTION");
        if (string.IsNullOrWhiteSpace(mainConnection) ||
            string.IsNullOrWhiteSpace(witnessConnection))
            return; // Dedicated CI job supplies TWO independent servers.

        using var primary = NpgsqlDataSource.Create(mainConnection);
        using var witnessDb = NpgsqlDataSource.Create(witnessConnection);
        Apply(primary, "GA-G3J3g-trusted-anchor-postgres.sql");
        Apply(witnessDb, "GA-G3J3j-independent-witness.sql");

        var game = "j3j-" + Guid.NewGuid().ToString("N");
        var a0 = Anchor('A', '0', 0UL, 0UL);
        var a1 = Anchor('B', 'C', 1UL, 1UL);
        var a2 = Anchor('D', 'E', 2UL, 2UL);
        var initialSnapshot =
            EconomicV9PostgresTrustedAnchorStore.SerializeSnapshot(
                new EconomicV9ExternalAnchorSnapshot(a0));

        using (var conn = primary.OpenConnection())
        using (var command = new NpgsqlCommand(
            """
            INSERT INTO public.ga_trusted_v9_anchor(
                game_id, snapshot, writer_epoch)
            VALUES (@game, @snap, 1);
            INSERT INTO public.ga_trusted_v9_game_principal(game_id, login_role)
            VALUES (@game, session_user);
            """, conn))
        {
            command.Parameters.AddWithValue("game", game);
            command.Parameters.AddWithValue("snap", initialSnapshot);
            command.ExecuteNonQuery();
        }
        using (var conn = witnessDb.OpenConnection())
        using (var command = new NpgsqlCommand(
            """
            INSERT INTO public.ga_v9_witness(
                game_id, writer_epoch, acknowledged)
            VALUES (@game, 1, @anchor);
            INSERT INTO public.ga_v9_witness_acl(game_id, login_role)
            VALUES (@game, session_user);
            """, conn))
        {
            command.Parameters.AddWithValue("game", game);
            command.Parameters.AddWithValue(
                "anchor", System.Text.Json.JsonSerializer.Serialize(a0));
            command.ExecuteNonQuery();
        }

        var active = new EconomicV9PostgresTrustedAnchorStore(
            primary, game, writerEpoch: 1);
        var witness = new EconomicV9PostgresAcknowledgmentWitness(
            witnessDb, game);
        var guarded = new EconomicV9WitnessedTrustedAnchorStore(
            active, witness, game, writerEpoch: 1);
        var a0Snapshot = guarded.ReadSnapshot();
        var step1 = new EconomicV9PreparedExternalAdvance(a0, a1);

        Assert.True(guarded.TryPrepare(a0Snapshot, step1));
        Assert.Equal(step1, guarded.ReadSnapshot().Prepared);
        Assert.True(guarded.TryFinalize(step1));
        Assert.Null(guarded.ReadSnapshot().Prepared);
        Assert.Equal(a1, witness.ReadLastAcknowledged(game));
        Assert.Equal(1L, witness.ReadState(game).WriterEpoch);

        // Old legacy clients MUST NOT bypass fencing once epoch is nonzero.
        var legacy = new EconomicV9PostgresTrustedAnchorStore(primary, game);
        Assert.False(legacy.TryPrepare(
            new EconomicV9ExternalAnchorSnapshot(a1),
            new EconomicV9PreparedExternalAdvance(a1, a2)));

        // Admin first isolates old writers, then rotates BOTH epoch stores.
        // The old adapter's read and CAS must fail closed after rotation.
        using (var conn = primary.OpenConnection())
        using (var rotate = new NpgsqlCommand(
            """
            UPDATE public.ga_trusted_v9_anchor
               SET writer_epoch=2
             WHERE game_id=@game AND writer_epoch=1
            """, conn))
        {
            rotate.Parameters.AddWithValue("game", game);
            Assert.Equal(1, rotate.ExecuteNonQuery());
        }
        using (var conn = witnessDb.OpenConnection())
        using (var rotate = new NpgsqlCommand(
            "SELECT public.ga_v9_witness_rotate(@game, 1)", conn))
        {
            rotate.Parameters.AddWithValue("game", game);
            Assert.Equal(true, rotate.ExecuteScalar());
        }
        Assert.Equal(2L, witness.ReadState(game).WriterEpoch);
        Assert.Equal(a1, witness.ReadLastAcknowledged(game));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            Assert.Throws<PostgresException>(() => active.ReadSnapshot()).SqlState);
        Assert.False(active.TryPrepare(
            new EconomicV9ExternalAnchorSnapshot(a1),
            new EconomicV9PreparedExternalAdvance(a1, a2)));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            Assert.Throws<PostgresException>(() => guarded.ReadSnapshot()).SqlState);

        // The newly fenced writer is admitted for exactly the NEW epoch.
        using var restartedWitnessDb = NpgsqlDataSource.Create(
            witnessConnection);
        var restartedWitness = new EconomicV9PostgresAcknowledgmentWitness(
            restartedWitnessDb, game);
        var promoted = new EconomicV9PostgresTrustedAnchorStore(
            primary, game, writerEpoch: 2);
        var nextGuarded = new EconomicV9WitnessedTrustedAnchorStore(
            promoted, restartedWitness, game, writerEpoch: 2);
        var step2 = new EconomicV9PreparedExternalAdvance(a1, a2);
        Assert.True(nextGuarded.TryPrepare(nextGuarded.ReadSnapshot(), step2));
        Assert.True(nextGuarded.TryFinalize(step2));
        Assert.Equal(a2, restartedWitness.ReadLastAcknowledged(game));
        Assert.Equal(a2, nextGuarded.ReadSnapshot().Confirmed);
        Assert.False(witness.TryAcknowledge(game, 1, a1, a2));
        Assert.False(witness.TryAcknowledge(game, 2, a0, a1));

        using var auditConnection = witnessDb.OpenConnection();
        using var check = new NpgsqlCommand(
            """
            SELECT revision, (SELECT count(*) FROM public.ga_v9_witness_audit
                               WHERE game_id=@game)
              FROM public.ga_v9_witness WHERE game_id=@game
            """, auditConnection);
        check.Parameters.AddWithValue("game", game);
        using var audit = check.ExecuteReader();
        Assert.True(audit.Read());
        Assert.Equal(3L, audit.GetInt64(0));
        Assert.Equal(3L, audit.GetInt64(1));
    }
}
