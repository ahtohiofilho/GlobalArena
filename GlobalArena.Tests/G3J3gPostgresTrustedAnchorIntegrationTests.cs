using System.Reflection;
using GlobalArena.Simulation;
using Npgsql;
using NpgsqlTypes;

namespace GlobalArena.Tests;

/// <summary>
/// Runs against an ACTUAL PostgreSQL primary when GA_TEST_PG_CONNECTION is set.
/// The dedicated GitHub Actions PostgreSQL job sets it; ordinary kernel suites
/// may run without a database. Tests never treat an in-memory store as durable.
/// </summary>
public sealed class G3J3gPostgresTrustedAnchorIntegrationTests
{
    private static readonly string Zero = new('0', 64);

    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("GA_TEST_PG_CONNECTION");

    private static EconomicV9ExternalAnchor Anchor(char checkpoint, char journal,
        ulong seq, ulong turn) =>
        new(new string(checkpoint, 64),
            journal == '0' ? Zero : new string(journal, 64), seq, turn);

    private static NpgsqlDataSource? Provision()
    {
        var value = ConnectionString;
        if (string.IsNullOrWhiteSpace(value))
            return null; // Run a real PostgreSQL backend via the dedicated CI job.
        var source = NpgsqlDataSource.Create(value);
        try
        {
            var resource = typeof(G3J3gPostgresTrustedAnchorIntegrationTests)
                .Assembly.GetManifestResourceNames()
                .Single(x => x.EndsWith(
                    "GA-G3J3g-trusted-anchor-postgres.sql",
                    StringComparison.Ordinal));
            using var stream =
                typeof(G3J3gPostgresTrustedAnchorIntegrationTests)
                    .Assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            using var connection = source.OpenConnection();
            using var command = new NpgsqlCommand(reader.ReadToEnd(), connection);
            command.ExecuteNonQuery();
            return source;
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    private static void Bootstrap(NpgsqlDataSource source, string game,
        EconomicV9ExternalAnchor genesis)
    {
        using var connection = source.OpenConnection();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO public.ga_trusted_v9_anchor(game_id, snapshot)
            VALUES (@game_id, @snapshot)
            """,
            connection);
        command.Parameters.Add(
            new NpgsqlParameter("game_id", NpgsqlDbType.Text) { Value = game });
        command.Parameters.Add(
            new NpgsqlParameter("snapshot", NpgsqlDbType.Text)
            {
                Value = EconomicV9PostgresTrustedAnchorStore.SerializeSnapshot(
                    new EconomicV9ExternalAnchorSnapshot(genesis))
            });
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    [Fact]
    public void J3gPersistentCasSurvivesReconnectionAndRejectsStaleWriters()
    {
        using var source = Provision();
        if (source is null) return;
        var game = "j3g-" + Guid.NewGuid().ToString("N");
        var a0 = Anchor('A', '0', 0, 0);
        var a1 = Anchor('B', 'C', 1, 1);
        var a2 = Anchor('D', 'E', 2, 2);
        Bootstrap(source, game, a0);

        var first = new EconomicV9PostgresTrustedAnchorStore(source, game);
        var before = first.ReadSnapshot();
        var firstStep = new EconomicV9PreparedExternalAdvance(a0, a1);
        Assert.True(first.TryPrepare(before, firstStep));
        Assert.Equal(firstStep, first.ReadSnapshot().Prepared);
        Assert.False(first.TryPrepare(before, firstStep));
        Assert.False(first.TryFinalize(
            new EconomicV9PreparedExternalAdvance(a1, a2)));
        Assert.True(first.TryFinalize(firstStep));
        Assert.Null(first.ReadSnapshot().Prepared);
        Assert.Equal(a1, first.ReadSnapshot().Confirmed);
        Assert.False(first.TryFinalize(firstStep));

        // Simulate a distinct process: independent datasource/connection pool.
        using var restartedSource = NpgsqlDataSource.Create(ConnectionString!);
        var second = new EconomicV9PostgresTrustedAnchorStore(
            restartedSource, game);
        Assert.Equal(a1, second.ReadSnapshot().Confirmed);
        Assert.False(second.TryPrepare(
            new EconomicV9ExternalAnchorSnapshot(a0), firstStep));
        var secondStep = new EconomicV9PreparedExternalAdvance(a1, a2);
        Assert.True(second.TryPrepare(second.ReadSnapshot(), secondStep));
        Assert.True(second.TryFinalize(secondStep));
        Assert.Equal(a2, second.ReadSnapshot().Confirmed);
        Assert.Null(second.ReadSnapshot().Prepared);

        using var connection = source.OpenConnection();
        using var cmd = new NpgsqlCommand(
            """
            SELECT revision,
                   (SELECT count(*) FROM public.ga_trusted_v9_anchor_audit
                     WHERE game_id = @id)
              FROM public.ga_trusted_v9_anchor
             WHERE game_id = @id
            """, connection);
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Text)
        {
            Value = game
        });
        using var records = cmd.ExecuteReader();
        Assert.True(records.Read());
        Assert.Equal(4L, records.GetInt64(0));
        Assert.Equal(4L, records.GetInt64(1));
    }

    [Fact]
    public async Task J3gOnlyOneConcurrentPrepareWinsAndGamesAreIsolated()
    {
        using var source = Provision();
        if (source is null) return;
        var firstGame = "j3g-" + Guid.NewGuid().ToString("N");
        var secondGame = "j3g-" + Guid.NewGuid().ToString("N");
        var before = Anchor('A', '0', 0, 0);
        Bootstrap(source, firstGame, before);
        Bootstrap(source, secondGame, before);

        var contenderA = new EconomicV9PostgresTrustedAnchorStore(source, firstGame);
        var contenderB = new EconomicV9PostgresTrustedAnchorStore(source, firstGame);
        var otherGame = new EconomicV9PostgresTrustedAnchorStore(source, secondGame);
        var targetA = new EconomicV9PreparedExternalAdvance(
            before, Anchor('B', 'C', 1, 1));
        var targetB = new EconomicV9PreparedExternalAdvance(
            before, Anchor('D', 'E', 1, 1));
        var original = new EconomicV9ExternalAnchorSnapshot(before);
        using var start = new ManualResetEventSlim(false);
        var first = Task.Run(() =>
        {
            start.Wait();
            return contenderA.TryPrepare(original, targetA);
        });
        var second = Task.Run(() =>
        {
            start.Wait();
            return contenderB.TryPrepare(original, targetB);
        });
        start.Set();
        var results = await Task.WhenAll(first, second);
        Assert.NotEqual(results[0], results[1]);
        var winner = results[0] ? targetA : targetB;
        Assert.Equal(winner, contenderA.ReadSnapshot().Prepared);
        Assert.Equal(original, otherGame.ReadSnapshot());
        Assert.False(otherGame.TryFinalize(winner));
        Assert.True(contenderB.TryFinalize(winner));
        Assert.Equal(winner.After, contenderA.ReadSnapshot().Confirmed);
        Assert.Equal(original, otherGame.ReadSnapshot());
    }

    [Fact]
    public void J3gUnprovisionedOrNoncanonicalAnchorFailsClosed()
    {
        using var source = Provision();
        if (source is null) return;
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicV9PostgresTrustedAnchorStore(
                source, "unprovisioned-" + Guid.NewGuid().ToString("N"))
                .ReadSnapshot());
        Assert.Throws<ArgumentException>(() =>
            new EconomicV9PostgresTrustedAnchorStore(source, "../escape"));
        var initial = new EconomicV9ExternalAnchorSnapshot(
            Anchor('A', '0', 0, 0));
        var canonical = EconomicV9PostgresTrustedAnchorStore
            .SerializeSnapshot(initial);
        Assert.Equal(initial, EconomicV9PostgresTrustedAnchorStore
            .ParseSnapshot(canonical));
        Assert.Throws<InvalidDataException>(() =>
            EconomicV9PostgresTrustedAnchorStore.ParseSnapshot(
                canonical + " "));
        Assert.Throws<InvalidDataException>(() =>
            EconomicV9PostgresTrustedAnchorStore.ParseSnapshot("{}"));
    }
}
