using System.Text.Json;
using GlobalArena.World;
using Npgsql;
using NpgsqlTypes;

namespace GlobalArena.Simulation;

public sealed record EconomicV9WitnessEpochState(
    long WriterEpoch, EconomicV9ExternalAnchor LastAcknowledged);

/// <summary>
/// The durable witness is located OUTSIDE the local save and every PostgreSQL
/// member of the primary/standby authority. It enforces monotonic CAS per game.
/// </summary>
public interface IEconomicV9AcknowledgmentWitness :
    IEconomicV9IndependentFailoverWitness
{
    EconomicV9WitnessEpochState ReadState(string gameId);
    bool TryAcknowledge(string gameId, long writerEpoch,
        EconomicV9ExternalAnchor before, EconomicV9ExternalAnchor after);
}

/// <summary>
/// Separate PostgreSQL connection and administration domain. The witness
/// database is never populated from a promoted primary automatically.
/// </summary>
public sealed class EconomicV9PostgresAcknowledgmentWitness :
    IEconomicV9AcknowledgmentWitness
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _gameId;

    public EconomicV9PostgresAcknowledgmentWitness(
        NpgsqlDataSource separatelyProtectedDataSource, string gameId)
    {
        _dataSource = separatelyProtectedDataSource ??
            throw new ArgumentNullException(nameof(separatelyProtectedDataSource));
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 128 ||
            gameId.Any(c => !char.IsAsciiLetterOrDigit(c) &&
                c is not ('-' or '_' or '.' or ':')))
            throw new ArgumentException("Invalid witness game namespace.",
                nameof(gameId));
        _gameId = gameId;
    }

    public EconomicV9ExternalAnchor ReadLastAcknowledged(string gameId) =>
        ReadState(gameId).LastAcknowledged;

    public EconomicV9WitnessEpochState ReadState(string gameId)
    {
        RequireGame(gameId);
        using var connection = _dataSource.OpenConnection();
        using var command = new NpgsqlCommand(
            "SELECT writer_epoch, acknowledged FROM public.ga_v9_witness_read(@game)",
            connection);
        command.Parameters.Add(new NpgsqlParameter("game", NpgsqlDbType.Text)
        {
            Value = _gameId
        });
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException(
                "Independent witness game is not provisioned.");
        var epoch = reader.GetInt64(0);
        var payload = reader.GetString(1);
        if (epoch <= 0 || payload.Length > 1024)
            throw new InvalidDataException("Invalid independent witness epoch or anchor.");
        EconomicV9ExternalAnchor? anchor;
        try
        {
            anchor = JsonSerializer.Deserialize<EconomicV9ExternalAnchor>(payload);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Invalid independent witness anchor.", ex);
        }
        if (anchor is null || JsonSerializer.Serialize(anchor) != payload)
            throw new InvalidDataException("Noncanonical independent witness anchor.");
        if (reader.Read())
            throw new InvalidDataException("Multiple independent witness records.");
        return new EconomicV9WitnessEpochState(epoch, anchor);
    }

    public bool TryAcknowledge(string gameId, long writerEpoch,
        EconomicV9ExternalAnchor before, EconomicV9ExternalAnchor after)
    {
        RequireGame(gameId);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (writerEpoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(writerEpoch));
        _ = new EconomicV9PreparedExternalAdvance(before, after);
        using var connection = _dataSource.OpenConnection();
        using var transaction =
            connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        using (var sync = new NpgsqlCommand(
            "SET LOCAL synchronous_commit = 'on'", connection, transaction))
            sync.ExecuteNonQuery();
        using var command = new NpgsqlCommand(
            "SELECT public.ga_v9_witness_ack(@game, @epoch, @before, @after)",
            connection, transaction);
        command.Parameters.Add(new NpgsqlParameter("game", NpgsqlDbType.Text)
        {
            Value = _gameId
        });
        command.Parameters.Add(new NpgsqlParameter("epoch", NpgsqlDbType.Bigint)
        {
            Value = writerEpoch
        });
        command.Parameters.Add(new NpgsqlParameter("before", NpgsqlDbType.Text)
        {
            Value = JsonSerializer.Serialize(before)
        });
        command.Parameters.Add(new NpgsqlParameter("after", NpgsqlDbType.Text)
        {
            Value = JsonSerializer.Serialize(after)
        });
        var succeeded = command.ExecuteScalar() is true;
        if (!succeeded)
        {
            transaction.Rollback();
            return false;
        }
        transaction.Commit();
        return true;
    }

    private void RequireGame(string gameId)
    {
        if (gameId != _gameId)
            throw new InvalidOperationException(
                "Witness handle is bound to a different game.");
    }
}

/// <summary>
/// Fenced composition of the G3-J3f commit coordinator's trusted store.
/// Prepare requires the witness to agree with the confirmed predecessor.
/// A successful primary finalize is NOT returned as success until the
/// independent witness also acknowledges After with a monotonic CAS.
/// An ambiguous witness write is fail-closed, never rolled back.
/// </summary>
public sealed class EconomicV9WitnessedTrustedAnchorStore :
    IEconomicV9TrustedAnchorStore
{
    private readonly IEconomicV9TrustedAnchorStore _primary;
    private readonly IEconomicV9AcknowledgmentWitness _witness;
    private readonly string _gameId;
    private readonly long _epoch;

    public EconomicV9WitnessedTrustedAnchorStore(
        IEconomicV9TrustedAnchorStore epochFencedPrimary,
        IEconomicV9AcknowledgmentWitness independentWitness,
        string gameId, long writerEpoch)
    {
        _primary = epochFencedPrimary ??
            throw new ArgumentNullException(nameof(epochFencedPrimary));
        _witness = independentWitness ??
            throw new ArgumentNullException(nameof(independentWitness));
        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Game identity required.", nameof(gameId));
        if (writerEpoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(writerEpoch));
        _gameId = gameId;
        _epoch = writerEpoch;
    }

    public EconomicV9ExternalAnchorSnapshot ReadSnapshot()
    {
        var observed = _primary.ReadSnapshot();
        var witnessed = RequireWitnessEpoch();
        if (observed.Confirmed != witnessed.LastAcknowledged)
            throw new InvalidOperationException(
                "Primary confirmation is not independently acknowledged.");
        return observed;
    }

    public bool TryPrepare(EconomicV9ExternalAnchorSnapshot expected,
        EconomicV9PreparedExternalAdvance requested)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(requested);
        if (expected.Prepared is not null ||
            expected.Confirmed != RequireWitnessEpoch().LastAcknowledged)
            return false;
        return _primary.TryPrepare(expected, requested);
    }

    public bool TryFinalize(EconomicV9PreparedExternalAdvance expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var witnessed = RequireWitnessEpoch();
        if (witnessed.LastAcknowledged != expected.Before)
            return false;
        if (!_primary.TryFinalize(expected))
            return false;
        // Verify the primary commit was independently observable BEFORE
        // advancing the witness. A lost acknowledgment creates a recovery
        // obligation, not automatic success.
        var actual = _primary.ReadSnapshot();
        if (actual != new EconomicV9ExternalAnchorSnapshot(expected.After))
            throw new InvalidOperationException(
                "Primary CAS acknowledgment was not independently observable.");
        if (!_witness.TryAcknowledge(_gameId, _epoch,
                expected.Before, expected.After))
            throw new InvalidOperationException(
                "Primary committed but witness CAS failed; recovery required.");
        var confirmed = RequireWitnessEpoch();
        if (confirmed.LastAcknowledged != expected.After)
            throw new InvalidOperationException(
                "Witness confirmation could not be independently observed.");
        return true;
    }

    /// <summary>
    /// Narrow reconciliation of a primary-finalized but witness-unacknowledged
    /// transition. The caller MUST have disposed the active writer. It verifies
    /// the entire GAFULL09 replay and the immediate journal predecessor before
    /// retrying the witness CAS. Never admits a primary ahead by more than one.
    /// </summary>
    public EconomicV9WitnessEpochState ReconcileFinalizedAcknowledgment(
        string directoryPath, WorldGenerationResult generatedWorld,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null,
        WorldStateHash? independentlyTrustedLegacyWorldHash = null,
        string? independentlyTrustedLegacyEcologicalDigest = null)
    {
        var witnessed = RequireWitnessEpoch();
        var primary = _primary.ReadSnapshot();
        if (primary.Prepared is not null)
            throw new InvalidOperationException("Pending prepare cannot be witness-acknowledged.");
        var expected = new EconomicV9PreparedExternalAdvance(
            witnessed.LastAcknowledged, primary.Confirmed);
        using var replayed =
            EconomicCompleteV9DurableSaveHost.RestoreExternallyConfirmed(
                directoryPath, generatedWorld, primary,
                independentlyTrustedLegacyWorldHash,
                independentlyTrustedLegacyEcologicalDigest,
                historicalVerifier);

        var mixed = new EconomicMixedV9TransitionArchive(directoryPath);
        var ecological = new EconomicEcologicalTransitionJournalArchive(directoryPath);
        var checkpoint = primary.Confirmed.CheckpointSha256Hex;
        var hasMixed = File.Exists(mixed.RecordPath(checkpoint));
        var hasEcological = File.Exists(ecological.RecordPath(checkpoint));
        if (hasMixed == hasEcological)
            throw new InvalidDataException("Latest journal type is missing or ambiguous.");
        string priorCheckpoint;
        string priorRoot;
        ulong turn;
        if (hasMixed)
        {
            var record = mixed.Load(checkpoint);
            priorCheckpoint = record.PreviousCheckpointSha256Hex;
            priorRoot = record.PreviousJournalSha256Hex;
            turn = record.TurnOrdinal;
        }
        else
        {
            var record = ecological.Load(checkpoint);
            priorCheckpoint = record.PreviousCheckpointSha256Hex;
            priorRoot = record.PreviousJournalSha256Hex;
            turn = record.TurnOrdinal;
        }
        if (priorCheckpoint != expected.Before.CheckpointSha256Hex ||
            priorRoot != expected.Before.JournalSha256Hex ||
            turn != expected.After.Turn)
            throw new InvalidDataException(
                "Latest committed transition is not a direct witnessed successor.");
        if (_primary.ReadSnapshot() != primary ||
            RequireWitnessEpoch() != witnessed)
            throw new InvalidOperationException(
                "Authority changed during witness reconciliation.");
        if (!_witness.TryAcknowledge(_gameId, _epoch,
                expected.Before, expected.After))
            throw new InvalidOperationException(
                "Independent witness recovery CAS rejected the verified successor.");
        var after = RequireWitnessEpoch();
        if (after.LastAcknowledged != expected.After)
            throw new InvalidOperationException("Witness recovery acknowledgment not durable.");
        return after;
    }

    private EconomicV9WitnessEpochState RequireWitnessEpoch()
    {
        var state = _witness.ReadState(_gameId);
        if (state.WriterEpoch != _epoch)
            throw new InvalidOperationException("Writer has been fenced by the independent witness.");
        return state;
    }
}
