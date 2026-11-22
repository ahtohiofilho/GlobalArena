using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// The two INPUT types of a marginal employment decision are different
/// economic experiments. A transfer keeps the worker census fixed; a new
/// worker changes both labor SUPPLY and consumption DEMAND.
/// </summary>
public enum FoundationMarginalLaborChangeKind : uint
{
    ExistingWorkerTransfer = 1,
    NewWorkerAddition = 2
}

/// <summary>
/// One independently evaluated PRECOMMIT marginal economic candidate.
/// The source must be a fully specified baseline world/scenario; all
/// comparisons within one report MUST share the same baseline identity.
/// CashDeltaScaled is an observed counterfactual delta, not a local
/// production proxy or an assumed profit. No evaluation is done here.
/// </summary>
public sealed record FoundationMarginalLaborObservation(
    string BaselineIdentity,
    FoundationMarginalLaborChangeKind ChangeKind,
    CivilizationId Owner,
    PhysicalTacticalTileId DestinationTile,
    PhysicalWorkerOccupation DestinationOccupation,
    BigInteger CashDeltaScaled,
    PhysicalWorkerId? ExistingWorkerId = null,
    PhysicalTacticalTileId? SourceTile = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaselineIdentity) ||
            !Owner.IsValid || !DestinationTile.IsValid ||
            DestinationOccupation == PhysicalWorkerOccupation.Unassigned ||
            !Enum.IsDefined(DestinationOccupation))
            throw new ArgumentException("Marginal evidence must identify a valid physical destination and occupation.");

        if (ChangeKind == FoundationMarginalLaborChangeKind.ExistingWorkerTransfer)
        {
            if (ExistingWorkerId is not { IsValid: true } ||
                SourceTile is not { IsValid: true })
                throw new ArgumentException(
                    "Realocation requires an existing real worker and their current physical source.");
        }
        else if (ChangeKind == FoundationMarginalLaborChangeKind.NewWorkerAddition)
        {
            if (ExistingWorkerId.HasValue || SourceTile.HasValue)
                throw new ArgumentException(
                    "New-worker growth must not reuse an existing worker or withdraw one from a source.");
        }
        else
            throw new ArgumentOutOfRangeException(nameof(ChangeKind));
    }
}

/// <summary>
/// A CHEAP index over already computed economic deltas, not an optimizer.
/// Ranking is deterministic; no Cartesian products of workers x cities,
/// no transport travel assumptions, no stock changes, no automatic orders.
/// </summary>
public sealed class FoundationMarginalLaborDiagnostic
{
    public string BaselineIdentity { get; }
    public FoundationMarginalLaborChangeKind ChangeKind { get; }
    public IReadOnlyList<FoundationMarginalLaborObservation> LowestReturns { get; }
    public IReadOnlyList<FoundationMarginalLaborObservation> HighestOpportunities { get; }
    public int EvaluatedCandidates { get; }
    public bool IsCommitted => false;

    internal FoundationMarginalLaborDiagnostic(string baseline,
        FoundationMarginalLaborChangeKind kind,
        FoundationMarginalLaborObservation[] low,
        FoundationMarginalLaborObservation[] high,
        int count)
    {
        BaselineIdentity = baseline;
        ChangeKind = kind;
        LowestReturns = Array.AsReadOnly(low);
        HighestOpportunities = Array.AsReadOnly(high);
        EvaluatedCandidates = count;
    }
}

public static class FoundationMarginalLaborDiagnosticIndexer
{
    /// <summary>
    /// Sorts O(N log N) *existing* counterfactual measurements; O(N) storage.
    /// topCount is presentation-only. Does NOT calculate N x N moves.
    /// Observations for new workforce MUST originate from a distinct
    /// legitimate growth experiment, not a transfer with a renamed label.
    /// </summary>
    public static FoundationMarginalLaborDiagnostic Rank(
        IReadOnlyList<FoundationMarginalLaborObservation> evidence,
        int topCount)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (topCount < 1)
            throw new ArgumentOutOfRangeException(nameof(topCount));
        if (evidence.Count == 0)
            throw new ArgumentException("No economically evaluated marginal candidates.", nameof(evidence));

        var first = evidence[0] ??
            throw new ArgumentException("Null marginal evidence.", nameof(evidence));
        first.Validate();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<FoundationMarginalLaborObservation>(evidence.Count);
        foreach (var item in evidence)
        {
            if (item is null)
                throw new ArgumentException("Null marginal evidence.", nameof(evidence));
            item.Validate();
            if (!StringComparer.Ordinal.Equals(item.BaselineIdentity,
                    first.BaselineIdentity) ||
                item.ChangeKind != first.ChangeKind)
                throw new ArgumentException(
                    "Incomparable economic baselines or worker-entry and transfer scenarios.",
                    nameof(evidence));

            string key = string.Join(":",
                item.Owner.Value, item.DestinationTile.FineGoldbergParameters.M,
                item.DestinationTile.FineGoldbergParameters.N,
                item.DestinationTile.FineStrategicCellId.Value,
                (uint)item.DestinationOccupation,
                item.ExistingWorkerId?.Value.ToString() ?? "NEW");
            if (!identities.Add(key))
                throw new ArgumentException(
                    "Repeated marginal candidate within the same physical baseline.",
                    nameof(evidence));
            entries.Add(item);
        }

        static IOrderedEnumerable<FoundationMarginalLaborObservation> Order(
            IEnumerable<FoundationMarginalLaborObservation> items, bool descending)
        {
            var byGain = descending
                ? items.OrderByDescending(x => x.CashDeltaScaled)
                : items.OrderBy(x => x.CashDeltaScaled);
            return byGain.ThenBy(x => x.Owner.Value)
                .ThenBy(x => x.DestinationTile.FineGoldbergParameters.M)
                .ThenBy(x => x.DestinationTile.FineGoldbergParameters.N)
                .ThenBy(x => x.DestinationTile.FineStrategicCellId.Value)
                .ThenBy(x => (uint)x.DestinationOccupation)
                .ThenBy(x => x.ExistingWorkerId?.Value ?? 0UL);
        }

        int take = Math.Min(topCount, entries.Count);
        return new FoundationMarginalLaborDiagnostic(first.BaselineIdentity,
            first.ChangeKind,
            Order(entries, descending: false).Take(take).ToArray(),
            Order(entries, descending: true).Take(take).ToArray(),
            entries.Count);
    }
}
