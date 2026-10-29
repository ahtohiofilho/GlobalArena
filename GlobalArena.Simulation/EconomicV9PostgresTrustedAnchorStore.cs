using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace GlobalArena.Simulation;

/// <summary>
/// A PostgreSQL-backed, per-game, durable compare-and-swap authority.
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
            "SELECT snapshot FROM ga_trusted_v9_anchor WHERE game_id = @game_id",
            connection);
        command.Parameters.Add(new NpgsqlParameter("game_id", NpgsqlDbType.Text)
        {
            Value = _gameId
        });
        var value = command.ExecuteScalar();
        if (value is not string payload)
            throw new InvalidOperationException(
                "Independently trusted v9 game anchor is not provisioned.");
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
        // The authoritative update is a SINGLE conditional PostgreSQL UPDATE.
        // No read-then-write window; concurrent writers cannot both win.
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
            """
            UPDATE ga_trusted_v9_anchor
               SET snapshot = @next_snapshot, revision = revision + 1
             WHERE game_id = @game_id AND snapshot = @previous_snapshot
            """,
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
        var affected = command.ExecuteNonQuery();
        if (affected != 1)
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
