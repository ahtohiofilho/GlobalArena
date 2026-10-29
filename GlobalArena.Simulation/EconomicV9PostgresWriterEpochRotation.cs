using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace GlobalArena.Simulation;

/// <summary>
/// A production implementation must independently verify that every former
/// primary and writer session is unable to commit. It MUST NOT infer this
/// from database reachability or from a caller-supplied boolean.
/// </summary>
public interface IEconomicV9FormerWriterIsolationVerifier
{
    bool IsIsolated(string gameId, long formerWriterEpoch);
}

public enum EconomicV9WriterRotationHealth
{
    Aligned,
    PrimaryRotatedWitnessPending,
    PreparedTransition,
    AnchorDivergence,
    EpochDivergence
}

public sealed record EconomicV9WriterRotationObservation(
    long PrimaryEpoch,
    long WitnessEpoch,
    EconomicV9ExternalAnchorSnapshot PrimarySnapshot,
    EconomicV9ExternalAnchor WitnessAcknowledged,
    EconomicV9WriterRotationHealth Health);

public sealed record EconomicV9WriterRotationReceipt(
    string GameId, long PreviousEpoch, long NewEpoch,
    EconomicV9ExternalAnchor AcknowledgedAnchor);

/// <summary>
/// The two DB administrative credentials MUST be separate from the game's
/// application credentials. This is a fail-closed operator protocol, not an
/// automatic cluster manager or an atomic cross-database transaction.
/// </summary>
public sealed class EconomicV9PostgresWriterEpochRotation
{
    private readonly NpgsqlDataSource _primaryAdmin;
    private readonly NpgsqlDataSource _witnessAdmin;
    private readonly IEconomicV9FormerWriterIsolationVerifier _isolation;
    private readonly string _gameId;

    public EconomicV9PostgresWriterEpochRotation(
        NpgsqlDataSource primaryAdministrator,
        NpgsqlDataSource witnessAdministrator,
        IEconomicV9FormerWriterIsolationVerifier formerWriterIsolation,
        string gameId)
    {
        _primaryAdmin = primaryAdministrator ??
            throw new ArgumentNullException(nameof(primaryAdministrator));
        _witnessAdmin = witnessAdministrator ??
            throw new ArgumentNullException(nameof(witnessAdministrator));
        _isolation = formerWriterIsolation ??
            throw new ArgumentNullException(nameof(formerWriterIsolation));
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 128 ||
            gameId.Any(c => !char.IsAsciiLetterOrDigit(c) &&
                c is not ('-' or '_' or '.' or ':')))
            throw new ArgumentException("Invalid rotation game identity.",
                nameof(gameId));
        _gameId = gameId;
    }

    /// <summary>
    /// Read-only diagnosis: does not certify safe admission or fencing.
    /// External monitoring can alert on anything other than Aligned.
    /// </summary>
    public EconomicV9WriterRotationObservation Observe()
    {
        var primary = ReadPrimary();
        var witness = ReadWitness();
        var health =
            primary.Snapshot.Prepared is not null
                ? EconomicV9WriterRotationHealth.PreparedTransition
            : primary.Snapshot.Confirmed != witness.Anchor
                ? EconomicV9WriterRotationHealth.AnchorDivergence
            : primary.Epoch == witness.Epoch
                ? EconomicV9WriterRotationHealth.Aligned
            : primary.Epoch == witness.Epoch + 1
                ? EconomicV9WriterRotationHealth.PrimaryRotatedWitnessPending
            : EconomicV9WriterRotationHealth.EpochDivergence;
        return new EconomicV9WriterRotationObservation(
            primary.Epoch, witness.Epoch, primary.Snapshot,
            witness.Anchor, health);
    }

    /// <summary>
    /// Recoverable sequence: physically fence old writers, CAS primary epoch,
    /// then CAS witness epoch with the exact same independently acknowledged
    /// checkpoint. On any ambiguity or divergence throw and keep gameplay
    /// blocked. Reinvoke with the same oldEpoch after checking physical fencing
    /// to recover from interruption between these two CAS operations.
    /// </summary>
    public EconomicV9WriterRotationReceipt Rotate(long oldEpoch)
    {
        if (oldEpoch <= 0 || oldEpoch == long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(oldEpoch));
        RequirePhysicalIsolation(oldEpoch);

        var oldWitness = ReadWitness();
        var oldPrimary = ReadPrimary();
        if (oldPrimary.Snapshot.Prepared is not null ||
            oldPrimary.Snapshot.Confirmed != oldWitness.Anchor)
            throw new InvalidOperationException(
                "Unsafe writer rotation: pending prepare or anchor mismatch.");

        var newEpoch = checked(oldEpoch + 1);
        // A repeated invocation may observe a completed rotation, but never
        // repair a witness-ahead, primary-behind, or epoch-gap state.
        if (oldWitness.Epoch == newEpoch &&
            oldPrimary.Epoch == newEpoch)
        {
            RequirePhysicalIsolation(oldEpoch);
            return VerifyCompleted(oldEpoch, newEpoch,
                oldWitness.Anchor);
        }
        if (oldWitness.Epoch != oldEpoch ||
            oldPrimary.Epoch is not (var value) ||
            (value != oldEpoch && value != newEpoch))
            throw new InvalidOperationException(
                "Unexpected epoch order; never rotate from a stale authority.");

        var expectedSnapshot =
            EconomicV9PostgresTrustedAnchorStore.SerializeSnapshot(
                new EconomicV9ExternalAnchorSnapshot(oldWitness.Anchor));
        var acknowledgedJson = JsonSerializer.Serialize(oldWitness.Anchor);

        if (oldPrimary.Epoch == oldEpoch)
        {
            RequirePhysicalIsolation(oldEpoch);
            if (!PerformAdministrativeCas(_primaryAdmin,
                    "SELECT public.ga_v9_anchor_rotate_verified(@game, @epoch, @expected)",
                    oldEpoch, expectedSnapshot))
                throw new InvalidOperationException(
                    "Primary rotation CAS refused; inspect external state.");
        }

        RequirePhysicalIsolation(oldEpoch);
        var midPrimary = ReadPrimary();
        var midWitness = ReadWitness();
        if (midPrimary.Epoch != newEpoch ||
            midWitness.Epoch != oldEpoch ||
            midPrimary.Snapshot.Prepared is not null ||
            midPrimary.Snapshot.Confirmed != oldWitness.Anchor ||
            midWitness.Anchor != oldWitness.Anchor)
            throw new InvalidOperationException(
                "Rotation changed during primary fencing; stop before witness CAS.");

        if (!PerformAdministrativeCas(_witnessAdmin,
                "SELECT public.ga_v9_witness_rotate_verified(@game, @epoch, @expected)",
                oldEpoch, acknowledgedJson))
            throw new InvalidOperationException(
                "Witness rotation CAS refused; primary remains fenced.");
        RequirePhysicalIsolation(oldEpoch);
        return VerifyCompleted(oldEpoch, newEpoch, oldWitness.Anchor);
    }

    private EconomicV9WriterRotationReceipt VerifyCompleted(
        long oldEpoch, long newEpoch, EconomicV9ExternalAnchor expected)
    {
        var primary = ReadPrimary();
        var witness = ReadWitness();
        if (primary.Epoch != newEpoch || witness.Epoch != newEpoch ||
            primary.Snapshot.Prepared is not null ||
            primary.Snapshot.Confirmed != expected ||
            witness.Anchor != expected)
            throw new InvalidOperationException(
                "Rotation confirmation changed or lacks independent agreement.");
        return new EconomicV9WriterRotationReceipt(
            _gameId, oldEpoch, newEpoch, expected);
    }

    private void RequirePhysicalIsolation(long oldEpoch)
    {
        if (!_isolation.IsIsolated(_gameId, oldEpoch))
            throw new InvalidOperationException(
                "Former primary/writer is not independently physically isolated.");
    }

    private bool PerformAdministrativeCas(NpgsqlDataSource source,
        string sql, long oldEpoch, string expected)
    {
        using var connection = source.OpenConnection();
        using var transaction =
            connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        using (var sync = new NpgsqlCommand(
            "SET LOCAL synchronous_commit = 'on'", connection, transaction))
            sync.ExecuteNonQuery();
        using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add(new NpgsqlParameter("game", NpgsqlDbType.Text)
        {
            Value = _gameId
        });
        command.Parameters.Add(new NpgsqlParameter("epoch", NpgsqlDbType.Bigint)
        {
            Value = oldEpoch
        });
        command.Parameters.Add(new NpgsqlParameter("expected", NpgsqlDbType.Text)
        {
            Value = expected
        });
        if (command.ExecuteScalar() is not true)
        {
            transaction.Rollback();
            return false;
        }
        transaction.Commit();
        return true;
    }

    private (long Epoch, EconomicV9ExternalAnchorSnapshot Snapshot) ReadPrimary()
    {
        using var connection = _primaryAdmin.OpenConnection();
        using var command = new NpgsqlCommand(
            "SELECT writer_epoch, snapshot FROM public.ga_trusted_v9_anchor WHERE game_id=@game",
            connection);
        command.Parameters.AddWithValue("game", _gameId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("Primary game not provisioned.");
        var epoch = reader.GetInt64(0);
        var snapshot = EconomicV9PostgresTrustedAnchorStore.ParseSnapshot(
            reader.GetString(1));
        if (reader.Read())
            throw new InvalidDataException("Duplicate primary game.");
        return (epoch, snapshot);
    }

    private (long Epoch, EconomicV9ExternalAnchor Anchor) ReadWitness()
    {
        using var connection = _witnessAdmin.OpenConnection();
        using var command = new NpgsqlCommand(
            "SELECT writer_epoch, acknowledged FROM public.ga_v9_witness WHERE game_id=@game",
            connection);
        command.Parameters.AddWithValue("game", _gameId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("Witness game not provisioned.");
        var epoch = reader.GetInt64(0);
        var payload = reader.GetString(1);
        EconomicV9ExternalAnchor? anchor;
        try
        {
            anchor = JsonSerializer.Deserialize<EconomicV9ExternalAnchor>(payload);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Invalid witness anchor.", ex);
        }
        if (anchor is null || JsonSerializer.Serialize(anchor) != payload)
            throw new InvalidDataException("Witness anchor is not canonical.");
        if (reader.Read())
            throw new InvalidDataException("Duplicate witness game.");
        return (epoch, anchor);
    }
}
