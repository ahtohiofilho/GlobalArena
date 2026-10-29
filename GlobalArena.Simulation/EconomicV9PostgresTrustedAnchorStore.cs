using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace GlobalArena.Simulation;

/// <summary>
/// A PostgreSQL-backed, per-game, durable compare-and-swap authority.
 /// App roles must only have EXECUTE on narrowly scoped SECURITY DEFINER
 /// functions, never direct UPDATE/DELETE privileges over anchor tables.
/// PostgreSQL MUST be independent from the GAFULL09 save storage, with a
/// trusted primary, restricted roles, backups, and rollback protections.
/// Not a substitute for protection against DB administrator compromise.
/// </summary>
public sealed class EconomicV9PostgresTrustedAnchorStore :
    IEconomicV9TrustedAnchorStore
{
    private const int MaximumSnapshotBytes = 4096;
    private readonly NpgsqlDataSource _source;
    private readonly string _gameId;

    /// <summary>
    /// The supplied datasource belongs to the host and must remain open
    /// for the lifetime of this adapter. Schema and game genesis are
    /// provisioned separately by a trusted administrator.
    /// </summary>
    public EconomicV9PostgresTrustedAnchorStore(
        NpgsqlDataSource independentlyProtectedDataSource, string gameId)
    {
        _source = independentlyProtectedDataSource ??
            throw new ArgumentNullException(nameof(independentlyProtectedDataSource));
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 128 ||
            gameId.Any(c => !char.IsAsciiLetterOrDigit(c) &&
                c is not ('-' or '_' or '.' or ':')))
            throw new ArgumentException("Invalid trusted game namespace.",
                nameof(gameId));
        _gameId = gameId;
    }

    public EconomicV9ExternalAnchorSnapshot ReadSnapshot()
    {
        using var connection = _source.OpenConnection();
        using var command = new NpgsqlCommand(
            "SELECT public.ga_v9_anchor_read(@game_id) WHERE NOT pg_is_in_recovery()",
            connection);
        command.Parameters.Add(new NpgsqlParameter("game_id", NpgsqlDbType.Text)
        {
            Value = _gameId
        });
        var value = command.ExecuteScalar();
        // A streaming standby may have a stale, yet locally consistent,
        // snapshot. It is NOT an authority until it is promoted. PostgreSQL
        // itself answers pg_is_in_recovery() for this same statement.
        if (value is not string payload)
            throw new InvalidOperationException(
                "Trusted v9 anchor unavailable on standby or unprovisioned primary.");
        return ParseSnapshot(payload);
    }

    public bool TryPrepare(
        EconomicV9ExternalAnchorSnapshot expected,
        EconomicV9PreparedExternalAdvance requested)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(requested);
        if (expected.Prepared is not null || requested.Before != expected.Confirmed)
            return false;
        // The database function executes ONE conditional row update and
        // audits a valid transition in the same transaction. No client-side
        // read-then-write window; concurrent writers cannot both win.
        var after = new EconomicV9ExternalAnchorSnapshot(
            expected.Confirmed, requested);
        return Swap(expected, after);
    }

    public bool TryFinalize(EconomicV9PreparedExternalAdvance expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var before = new EconomicV9ExternalAnchorSnapshot(
            expected.Before, expected);
        var after = new EconomicV9ExternalAnchorSnapshot(expected.After);
        return Swap(before, after);
    }

    private bool Swap(
        EconomicV9ExternalAnchorSnapshot expected,
        EconomicV9ExternalAnchorSnapshot after)
    {
        var from = SerializeSnapshot(expected);
        var to = SerializeSnapshot(after);
        using var connection = _source.OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        // PostgreSQL must acknowledge a locally durable WAL commit. For
        // multi-node HA durability, administrators must ALSO configure
        // synchronous replication and an appropriate failover policy.
        using (var sync = new NpgsqlCommand(
            "SET LOCAL synchronous_commit = 'on'", connection, transaction))
        {
            sync.ExecuteNonQuery();
        }
        using var command = new NpgsqlCommand(
            "SELECT public.ga_v9_anchor_cas(@game_id, @previous_snapshot, @next_snapshot)",
            connection, transaction);
        command.Parameters.Add(new NpgsqlParameter("game_id", NpgsqlDbType.Text)
        {
            Value = _gameId
        });
        command.Parameters.Add(
            new NpgsqlParameter("previous_snapshot", NpgsqlDbType.Text)
            {
                Value = from
            });
        command.Parameters.Add(new NpgsqlParameter("next_snapshot", NpgsqlDbType.Text)
        {
            Value = to
        });
        var succeeded = command.ExecuteScalar();
        if (succeeded is not true)
        {
            transaction.Rollback();
            return false;
        }
        transaction.Commit();
        return true;
    }

    /// <summary>
    /// Canonical snapshot representation, stable across hosts on .NET 10.
    /// Lexically noncanonical or invalid data from the database is rejected
    /// instead of becoming a new local source of authority.
    /// </summary>
    public static string SerializeSnapshot(
        EconomicV9ExternalAnchorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var result = JsonSerializer.Serialize(snapshot);
        if (result.Length > MaximumSnapshotBytes)
            throw new InvalidDataException("External anchor is oversized.");
        return result;
    }

    public static EconomicV9ExternalAnchorSnapshot ParseSnapshot(
        string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) ||
            payload.Length > MaximumSnapshotBytes)
            throw new InvalidDataException("Invalid trusted external anchor payload.");
        try
        {
            var parsed =
                JsonSerializer.Deserialize<EconomicV9ExternalAnchorSnapshot>(payload);
            if (parsed is null || SerializeSnapshot(parsed) != payload)
                throw new InvalidDataException(
                    "External anchor is not a canonical trusted snapshot.");
            return parsed;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException
            or InvalidOperationException)
        {
            throw new InvalidDataException(
                "External anchor cannot be decoded canonically.", ex);
        }
    }
}
