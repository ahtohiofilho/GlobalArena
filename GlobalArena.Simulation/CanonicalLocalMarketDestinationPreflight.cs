using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Candidate final destination for an existing physical good. Purpose is an
/// intent, not proof of performed consumption, purchase, sale or processing.
/// </summary>
public enum CanonicalLocalMarketUsePurpose : byte
{
    ResidentFoodUse = 1,
    WorkforceMaterialUse = 2
}

public sealed record CanonicalLocalMarketDestinationCandidate(
    ulong SourceFineTile,
    ulong DestinationFineTile,
    uint Commodity,
    ulong RawQuantity,
    CanonicalLocalMarketUsePurpose Purpose,
    EconomicActivityKind? BeneficiaryActivity = null);

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01B: fail-closed, same-tile physical destination
/// candidate preflight. No hypothetical inter-tile carriage is authorized.
/// Every candidate must name a real resident or productive workforce group,
/// draw exclusively from the eligible local pool, and not double-allocate
/// a physical unit to more than one destination.
/// </summary>
public sealed class CanonicalLocalMarketDestinationPreflight
{
    private readonly CanonicalPhysicalTacticalSession _source;
    private readonly CanonicalDefaultMarketPoolPreflight _pool;
    private readonly string _sourceHash;
    private readonly int _sourceEvents;
    private readonly CanonicalLocalMarketDestinationCandidate[] _candidates;

    public IReadOnlyList<CanonicalLocalMarketDestinationCandidate> Candidates { get; }
    public BigInteger CandidateRawTotal { get; }

    public bool AllCandidatesHaveRealLocalRecipients => true;
    public bool PermitsUnprovenRemoteTransport => false;
    public bool ProvesExecutedFinalConsumption => false;
    public bool ProvesExecutedSale => false;
    public bool AuthorizesPaymentOrRevenue => false;
    public bool ChangesWorldStateOrSave => false;

    private CanonicalLocalMarketDestinationPreflight(
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalLocalMarketDestinationCandidate> candidates)
    {
        _source = session;
        _sourceHash = session.CurrentHash.HexDigest;
        _sourceEvents = session.Events.Count;
        _pool = CanonicalDefaultMarketPoolPreflight.FromCurrent(session);
        _candidates = candidates
            .OrderBy(x => x.SourceFineTile)
            .ThenBy(x => x.Commodity)
            .ThenBy(x => x.DestinationFineTile)
            .ThenBy(x => (byte)x.Purpose)
            .ThenBy(x => x.BeneficiaryActivity.HasValue
                ? (int)x.BeneficiaryActivity.Value : -1)
            .ThenBy(x => x.RawQuantity)
            .ToArray();
        if (_candidates.Length == 0)
            throw new ArgumentException(
                "At least one evidenced local destination candidate is required.",
                nameof(candidates));

        var keys = new HashSet<(ulong Source, ulong Destination, uint Commodity,
            CanonicalLocalMarketUsePurpose Purpose, EconomicActivityKind? Beneficiary)>();
        var workforce = session.State.Allocations;

        foreach (var candidate in _candidates)
        {
            if (candidate is null ||
                candidate.RawQuantity == 0UL ||
                candidate.Commodity is not (1U or 2U or 3U) ||
                !keys.Add((candidate.SourceFineTile,
                    candidate.DestinationFineTile, candidate.Commodity,
                    candidate.Purpose, candidate.BeneficiaryActivity)))
                throw new ArgumentException(
                    "Invalid or repeated local market destination candidate.",
                    nameof(candidates));

            if (candidate.SourceFineTile != candidate.DestinationFineTile)
                throw new InvalidOperationException(
                    "Remote physical delivery requires a certified transport service.");

            var destinationPeople = workforce.Any(a =>
                a.TileId.FineStrategicCellId.Value == candidate.DestinationFineTile &&
                a.Workers > 0UL);
            if (!destinationPeople)
                throw new InvalidOperationException(
                    "The proposed destination lacks real residents.");

            switch (candidate.Purpose)
            {
                case CanonicalLocalMarketUsePurpose.ResidentFoodUse
                    when candidate.Commodity == 1U &&
                         !candidate.BeneficiaryActivity.HasValue:
                    break;
                case CanonicalLocalMarketUsePurpose.WorkforceMaterialUse
                    when candidate.Commodity is 2U or 3U &&
                         candidate.BeneficiaryActivity.HasValue &&
                         workforce.Any(a =>
                             a.TileId.FineStrategicCellId.Value ==
                                candidate.DestinationFineTile &&
                             a.Activity == candidate.BeneficiaryActivity.Value &&
                             a.Workers > 0UL):
                    break;
                default:
                    throw new InvalidOperationException(
                        "Food and material destination types cannot be conflated.");
            }
        }

        foreach (var group in _candidates.GroupBy(
            x => (x.SourceFineTile, x.Commodity)))
        {
            var rawTotal = group.Aggregate(BigInteger.Zero,
                (total, x) => total + x.RawQuantity);
            var eligible = _pool.ForTileCommodity(
                group.Key.SourceFineTile, group.Key.Commodity)
                .EligibleByDefaultRaw;
            if (rawTotal > eligible)
                throw new InvalidOperationException(
                    "Physical goods were assigned to multiple destinations " +
                    "or include a deliberate retention.");
        }

        CandidateRawTotal = _candidates.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.RawQuantity);
        Candidates = Array.AsReadOnly(_candidates);
        VerifyCurrent();
    }

    public static CanonicalLocalMarketDestinationPreflight FromCurrent(
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalLocalMarketDestinationCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(candidates);
        return new(session, candidates);
    }

    public void VerifyCurrent()
    {
        if (_source.CurrentHash.HexDigest != _sourceHash ||
            _source.Events.Count != _sourceEvents)
            throw new InvalidOperationException(
                "Market destination candidates are stale after a canonical action.");
        _pool.VerifyCurrent();
        if (_candidates.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.RawQuantity) != CandidateRawTotal)
            throw new InvalidOperationException(
                "The destination proposal sum changed without authorization.");
    }
}
