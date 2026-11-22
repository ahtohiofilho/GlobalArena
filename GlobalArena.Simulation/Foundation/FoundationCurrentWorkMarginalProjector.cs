using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// The loss of total collective economic cash if ONE existing productive
/// worker were temporarily inactive, while retaining their location, identity,
/// consumption and all other workers. This is NOT a migration, birth,
/// logistics assignment or recommendation for dismissal.
/// </summary>
public sealed record FoundationCurrentWorkMarginalReading(
    PhysicalWorkerId Worker,
    CivilizationId Owner,
    PhysicalTacticalTileId Tile,
    PhysicalWorkerOccupation CurrentOccupation,
    CommodityId Commodity,
    BigInteger ContributionScaled,
    ulong LostProductionMicro);

public sealed class FoundationCurrentWorkMarginalReport
{
    public string BaselineIdentity { get; }
    public IReadOnlyList<FoundationCurrentWorkMarginalReading> AllReadings { get; }
    public IReadOnlyList<FoundationCurrentWorkMarginalReading> LowestContributions { get; }
    public IReadOnlyList<FoundationCurrentWorkMarginalReading> HighestContributions { get; }
    public int CompleteMarketSolutions { get; }
    public bool IsCommitted => false;

    internal FoundationCurrentWorkMarginalReport(string identity,
        FoundationCurrentWorkMarginalReading[] all,
        FoundationCurrentWorkMarginalReading[] lowest,
        FoundationCurrentWorkMarginalReading[] highest)
    {
        BaselineIdentity = identity;
        AllReadings = Array.AsReadOnly(all);
        LowestContributions = Array.AsReadOnly(lowest);
        HighestContributions = Array.AsReadOnly(highest);
        CompleteMarketSolutions = checked(1 + all.Length);
    }
}

/// <summary>
/// R160: direct PRECOMMIT counterfactuals of existing jobs. Market baseline
/// solved ONCE; one candidate solves the market with precisely one worker
/// inactive. No service shadow or invented labor price. The worker remains
/// in the population, generates the same demand, and is NOT removed.
/// </summary>
public static class FoundationCurrentWorkMarginalProjector
{
    public static FoundationCurrentWorkMarginalReport Evaluate(
        WorldState baseline,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy marketPolicy,
        IReadOnlyList<PhysicalWorkerId> candidates,
        int evaluationBudget, int topCount)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(marketPolicy);
        ArgumentNullException.ThrowIfNull(candidates);
        if (evaluationBudget < 1 || topCount < 1 ||
            candidates.Count == 0 || candidates.Count > evaluationBudget)
            throw new ArgumentException(
                "Provide a nonempty explicit worker sample within a positive evaluation budget.");
        if (!baseline.Workers.HasAuthoritativeOccupations ||
            !baseline.Workers.HasAuthoritativeTacticalLocations ||
            !baseline.Transport.IsInitialized || baseline.WorldBinding is null ||
            geography.FineParameters != incidence.Refinement.FineParameters ||
            geography.FineElevationRaw.Count != incidence.FineTopology.Cells.Count)
            throw new ArgumentException("Marginal work diagnosis requires a matching bound physical world.");

        var workers = baseline.Workers.Workers.ToDictionary(w => w.Id);
        var ids = candidates.OrderBy(id => id.Value).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Duplicate marginal worker candidate.", nameof(candidates));
        foreach (var id in ids)
            if (!workers.TryGetValue(id, out var w) ||
                w.Activity != PhysicalWorkerActivity.Producing ||
                w.TacticalTileId is not { IsValid: true } ||
                w.Occupation is not (PhysicalWorkerOccupation.Agriculture or
                    PhysicalWorkerOccupation.Forestry or
                    PhysicalWorkerOccupation.Mining))
                throw new ArgumentException(
                    "Each candidate must be a real, currently productive, physically placed worker.",
                    nameof(candidates));

        var (roads, carrying) =
            FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
                baseline, incidence);
        var blocked = new HashSet<PhysicalTacticalEdgeId>();
        foreach (var e in incidence.FineTopology.Edges)
        {
            int a = checked((int)e.IncidentCellIds[0].Value - 1);
            int b = checked((int)e.IncidentCellIds[1].Value - 1);
            if (geography.CrossingAtIndices(a, b) ==
                FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
                blocked.Add(new PhysicalTacticalEdgeId(
                    incidence.Refinement.FineParameters, e.Id));
        }

        (BigInteger Cash, ulong Goods) Solve(PhysicalWorkerRuntimeState workforce)
        {
            var quotes = FoundationPhysicalUniversalMarketPricing
                .QuotesFromRealWorkers(workforce, marketPolicy.Demand);
            var plan = FoundationPhysicalLaggedFreightCyclePlanner
                .CoordinateCandidate(workforce, incidence,
                    marketPolicy.Demand, marketPolicy.Cargo, quotes,
                    roads, carrying,
                    maxProducerPasses: marketPolicy.MaxProducerPasses,
                    blockedEdges: blocked, fixedUnitFreight: true);
            var audit = plan.Distribution.AllocationAudit;
            if (plan.RepricedWithinCycle ||
                audit.TotalAllocatedGoodsMicro != audit.TotalAvailableGoodsMicro)
                throw new InvalidOperationException(
                    "R160 requires fully allocated goods under unchanged fixed freight.");
            var accounts = FoundationPhysicalEconomicAccountingReconciler
                .EvaluateCandidate(workforce, incidence,
                    marketPolicy.Demand, marketPolicy.Cargo, plan,
                    marketPolicy.TaxRates);
            return (accounts.GrossSalesScaled - accounts.PhysicalFreightScaled,
                audit.TotalAllocatedGoodsMicro);
        }

        var original = Solve(baseline.Workers);
        var results = new List<FoundationCurrentWorkMarginalReading>(ids.Length);
        foreach (var id in ids)
        {
            var individual = workers[id];
            var variant = baseline.Workers.Workers.Select(w =>
                w.Id == id
                    ? w.WithTacticalAssignment(w.TacticalTileId!.Value, w.CellId,
                        PhysicalWorkerActivity.Other,
                        PhysicalWorkerOccupation.Unassigned)
                    : w).ToArray();
            // Explicitly no workers removed: consumption and geography fixed.
            var temporarilyInactive = new PhysicalWorkerRuntimeState(
                variant, tacticalLocationsAuthoritative: true,
                occupationsAuthoritative: true);
            var absentOutput = Solve(temporarilyInactive);
            if (original.Goods < absentOutput.Goods ||
                original.Goods - absentOutput.Goods != individual.OutputMicroPerTurn)
                throw new InvalidOperationException(
                    "Withdrawing one producer must remove exactly its output, not its demand.");
            results.Add(new FoundationCurrentWorkMarginalReading(
                id, individual.Owner, individual.TacticalTileId!.Value,
                individual.Occupation!.Value, individual.Commodity,
                original.Cash - absentOutput.Cash,
                original.Goods - absentOutput.Goods));
        }

        static IOrderedEnumerable<FoundationCurrentWorkMarginalReading> Order(
            IEnumerable<FoundationCurrentWorkMarginalReading> items,
            bool descending)
        {
            var initial = descending
                ? items.OrderByDescending(x => x.ContributionScaled)
                : items.OrderBy(x => x.ContributionScaled);
            return initial.ThenBy(x => x.Owner.Value)
                .ThenBy(x => x.Tile.FineStrategicCellId.Value)
                .ThenBy(x => x.Worker.Value);
        }

        var list = results.ToArray();
        int take = Math.Min(topCount, list.Length);
        var identity = new CanonicalWorldStateHasher().Compute(baseline)
            .ToString() ?? throw new InvalidOperationException("Missing canonical state identity.");
        return new FoundationCurrentWorkMarginalReport(identity, list,
            Order(list, descending: false).Take(take).ToArray(),
            Order(list, descending: true).Take(take).ToArray());
    }
}
