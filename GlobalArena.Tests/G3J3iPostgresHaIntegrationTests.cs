using GlobalArena.Simulation;
using Npgsql;

namespace GlobalArena.Tests;

/// <summary>
/// Executed twice by the dedicated two-node PostgreSQL HA job: once against
/// a streaming standby and once after that SAME node is promoted. The job
/// stops the former primary before promotion. No in-memory simulation.
/// </summary>
public sealed class G3J3iPostgresHaIntegrationTests
{
    [Fact]
    public void J3iTwoNodeStandbyIsNotAuthorityButPromotedNodeRetainsAck()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("GA_TEST_HA_CONNECTION");
        var phase = Environment.GetEnvironmentVariable("GA_TEST_HA_PHASE");
        if (string.IsNullOrWhiteSpace(connectionString) ||
            string.IsNullOrWhiteSpace(phase))
            return; // Dedicated HA job supplies both. Not a standalone gate.

        using var source = NpgsqlDataSource.Create(connectionString);
        var store = new EconomicV9PostgresTrustedAnchorStore(source, "ga-ha-game");
        using (var connection = source.OpenConnection())
        {
            using var role = new NpgsqlCommand(
                "SELECT pg_is_in_recovery()", connection);
            Assert.Equal(phase == "standby", role.ExecuteScalar());
        }

        if (phase == "standby")
        {
            Assert.Throws<InvalidOperationException>(() =>
                store.ReadSnapshot());
            return;
        }
        Assert.Equal("promoted", phase);
        var ack = store.ReadSnapshot();
        Assert.Null(ack.Prepared);
        Assert.Equal(1UL, ack.Confirmed.Sequence);
        Assert.Equal(1UL, ack.Confirmed.Turn);
        Assert.Equal(new string('B', 64),
            ack.Confirmed.CheckpointSha256Hex);
        Assert.Equal(new string('C', 64),
            ack.Confirmed.JournalSha256Hex);

        // This exact acknowledged transition is replayed on the promoted
        // member, not re-invented by creating a fresh game.
        using var promoted = source.OpenConnection();
        using var count = new NpgsqlCommand(
            """
            SELECT revision, (
                SELECT count(*) FROM public.ga_trusted_v9_anchor_audit
                 WHERE game_id='ga-ha-game')
              FROM public.ga_trusted_v9_anchor
             WHERE game_id='ga-ha-game'
            """, promoted);
        using var reader = count.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(2L, reader.GetInt64(1));
    }
}
