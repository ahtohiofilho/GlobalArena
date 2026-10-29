using GlobalArena.Simulation;
using Npgsql;
using NpgsqlTypes;

namespace GlobalArena.Tests;

/// <summary>
/// Real PostgreSQL privilege boundary. The test administrator provisions two
/// separate LOGIN accounts; each application connection authenticates as its
/// own session_user. SET ROLE cannot impersonate another login identity.
/// </summary>
[Collection("G3J3PostgresIntegration")]
public sealed class G3J3hPostgresLeastPrivilegeIntegrationTests
{
    private static readonly string Zero = new('0', 64);

    private static EconomicV9ExternalAnchor Anchor(char hash, char root,
        ulong seq = 0UL, ulong turn = 0UL) =>
        new(new string(hash, 64),
            root == '0' ? Zero : new string(root, 64), seq, turn);

    private static void Execute(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void Provision(NpgsqlConnection connection)
    {
        var assembly = typeof(G3J3hPostgresLeastPrivilegeIntegrationTests)
            .Assembly;
        var name = assembly.GetManifestResourceNames().Single(x =>
            x.EndsWith("GA-G3J3g-trusted-anchor-postgres.sql",
                StringComparison.Ordinal));
        using var resource = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(resource);
        Execute(connection, reader.ReadToEnd());
    }

    private static void Bootstrap(NpgsqlConnection admin, string game,
        EconomicV9ExternalAnchor initial, string principal)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO public.ga_trusted_v9_anchor(game_id, snapshot)
            VALUES (@id, @state);
            INSERT INTO public.ga_trusted_v9_game_principal(game_id, login_role)
            VALUES (@id, @principal);
            """, admin);
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Text)
        {
            Value = game
        });
        command.Parameters.Add(new NpgsqlParameter("state", NpgsqlDbType.Text)
        {
            Value = EconomicV9PostgresTrustedAnchorStore.SerializeSnapshot(
                new EconomicV9ExternalAnchorSnapshot(initial))
        });
        command.Parameters.Add(new NpgsqlParameter("principal", NpgsqlDbType.Name)
        {
            Value = principal
        });
        command.ExecuteNonQuery();
    }

    private static void RequirePermissionDenied(Action action) =>
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            Assert.Throws<PostgresException>(action).SqlState);

    [Fact]
    public void J3hAuthenticatedLoginsCannotReadOrCommitOtherGamesOrBypassCas()
    {
        var administrator = Environment.GetEnvironmentVariable(
            "GA_TEST_PG_CONNECTION");
        if (string.IsNullOrWhiteSpace(administrator))
            return; // Dedicated PostgreSQL CI job supplies a real database.

        using var adminSource = NpgsqlDataSource.Create(administrator);
        using var admin = adminSource.OpenConnection();
        Provision(admin);
        var suffix = Guid.NewGuid().ToString("N");
        var loginA = "ga_j3h_a_" + suffix;
        var loginB = "ga_j3h_b_" + suffix;
        var secretA = Guid.NewGuid().ToString("N");
        var secretB = Guid.NewGuid().ToString("N");
        var gameA = "j3h-a-" + suffix;
        var gameB = "j3h-b-" + suffix;
        var genesis = Anchor('A', '0');
        // These generated identifiers and secrets contain only [a-z0-9_].
        // Never interpolate untrusted game IDs or arbitrary role names in SQL.
        Execute(admin, $"CREATE ROLE {loginA} LOGIN PASSWORD '{secretA}'");
        Execute(admin, $"CREATE ROLE {loginB} LOGIN PASSWORD '{secretB}'");
        try
        {
            Execute(admin,
                $"GRANT USAGE ON SCHEMA public TO {loginA}, {loginB}");
            Execute(admin,
                $"GRANT EXECUTE ON FUNCTION public.ga_v9_anchor_read(text) TO {loginA}, {loginB}");
            Execute(admin,
                $"GRANT EXECUTE ON FUNCTION public.ga_v9_anchor_cas(text, text, text) TO {loginA}, {loginB}");
            Bootstrap(admin, gameA, genesis, loginA);
            Bootstrap(admin, gameB, genesis, loginB);

            var cfgA = new NpgsqlConnectionStringBuilder(administrator)
            {
                Username = loginA,
                Password = secretA,
                Pooling = false
            };
            var cfgB = new NpgsqlConnectionStringBuilder(administrator)
            {
                Username = loginB,
                Password = secretB,
                Pooling = false
            };

            using (var sourceA = NpgsqlDataSource.Create(cfgA.ConnectionString))
            using (var sourceB = NpgsqlDataSource.Create(cfgB.ConnectionString))
            {
                var own = new EconomicV9PostgresTrustedAnchorStore(sourceA, gameA);
                var other = new EconomicV9PostgresTrustedAnchorStore(sourceB, gameB);
                Assert.Equal(genesis, own.ReadSnapshot().Confirmed);
                Assert.Equal(genesis, other.ReadSnapshot().Confirmed);

                var next = Anchor('B', 'C', 1UL, 1UL);
                var step = new EconomicV9PreparedExternalAdvance(genesis, next);
                RequirePermissionDenied(() =>
                    new EconomicV9PostgresTrustedAnchorStore(sourceA, gameB)
                        .ReadSnapshot());
                RequirePermissionDenied(() =>
                    new EconomicV9PostgresTrustedAnchorStore(sourceA, gameB)
                        .TryPrepare(new EconomicV9ExternalAnchorSnapshot(genesis),
                            step));

                using (var serviceConnection = sourceA.OpenConnection())
                {
                    RequirePermissionDenied(() => Execute(serviceConnection,
                        "SELECT snapshot FROM public.ga_trusted_v9_anchor"));
                    RequirePermissionDenied(() => Execute(serviceConnection,
                        "UPDATE public.ga_trusted_v9_anchor SET revision = revision + 1"));
                    RequirePermissionDenied(() => Execute(serviceConnection,
                        "DELETE FROM public.ga_trusted_v9_anchor_audit"));
                    RequirePermissionDenied(() => Execute(serviceConnection,
                        "SELECT * FROM public.ga_trusted_v9_game_principal"));
                    RequirePermissionDenied(() => Execute(serviceConnection,
                        $"SET ROLE {loginB}"));
                }

                Assert.True(own.TryPrepare(own.ReadSnapshot(), step));
                Assert.Equal(step, own.ReadSnapshot().Prepared);
                Assert.True(own.TryFinalize(step));
                Assert.Equal(next, own.ReadSnapshot().Confirmed);
                Assert.Equal(genesis, other.ReadSnapshot().Confirmed);

                using var separateConnection = sourceA.OpenConnection();
                Assert.Equal(loginA, new NpgsqlCommand(
                    "SELECT session_user", separateConnection)
                    .ExecuteScalar());

                // Revocation must take effect at the DB boundary without
                // relying on an application restart or clearing a cache.
                using (var revoke = new NpgsqlCommand(
                    """
                    DELETE FROM public.ga_trusted_v9_game_principal
                     WHERE game_id = @game AND login_role = @login
                    """, admin))
                {
                    revoke.Parameters.AddWithValue("game", gameA);
                    revoke.Parameters.AddWithValue("login", loginA);
                    Assert.Equal(1, revoke.ExecuteNonQuery());
                }
                RequirePermissionDenied(() => own.ReadSnapshot());
                RequirePermissionDenied(() => own.TryFinalize(step));
            }
        }
        finally
        {
            // Test-only administrative teardown; never use this path in gameplay.
            using var cleanup = new NpgsqlCommand(
                """
                DELETE FROM public.ga_trusted_v9_game_principal
                 WHERE game_id IN (@a, @b);
                DELETE FROM public.ga_trusted_v9_anchor_audit
                 WHERE game_id IN (@a, @b);
                DELETE FROM public.ga_trusted_v9_anchor
                 WHERE game_id IN (@a, @b);
                """, admin);
            cleanup.Parameters.AddWithValue("a", gameA);
            cleanup.Parameters.AddWithValue("b", gameB);
            cleanup.ExecuteNonQuery();
            Execute(admin,
                $"REVOKE EXECUTE ON FUNCTION public.ga_v9_anchor_read(text) FROM {loginA}, {loginB}");
            Execute(admin,
                $"REVOKE EXECUTE ON FUNCTION public.ga_v9_anchor_cas(text, text, text) FROM {loginA}, {loginB}");
            Execute(admin, $"REVOKE USAGE ON SCHEMA public FROM {loginA}, {loginB}");
            Execute(admin, $"DROP ROLE {loginA}");
            Execute(admin, $"DROP ROLE {loginB}");
        }
    }
}
