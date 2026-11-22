using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit candidate for one new individual at the real location and with
/// the REAL output/consumption profile of an existing producing archetype.
/// This limited first step does not guess unimplemented resource production.
/// </summary>
public sealed record FoundationNewWorkerProductiveOpportunity(
    PhysicalWorkerId ArchetypeWorker);

public sealed record FoundationNewWorkerProductiveReading(
    PhysicalWorkerId ArchetypeWorker,
    CivilizationId Owner,
    PhysicalTacticalTileId Tile,
    PhysicalWorkerOccupation Occupation,
    CommodityId Commodity,
    ulong AddedProductionMicro,
    ulong AddedDemandMicro,
    BigInteger CollectiveCashGainScaled);

public sealed class FoundationNewWorkerProductiveAssessment
{
    public string BaselineIdentity { get; }
    public FoundationMarginalLaborDiagnostic Ranking { get; }
    public IReadOnlyList<FoundationNewWorkerProductiveReading> Evaluated { get; }
    public int CompleteMarketSolutions { get; }
    public bool IsCommitted => false;

    internal FoundationNewWorkerProductiveAssessment(
        string identity, FoundationMarginalLaborDiagnostic ranking,
        FoundationNewWorkerProductiveReading[] readings)
    {
        BaselineIdentity = identity;
        Ranking = ranking;
        Evaluated = Array.AsReadOnly(readings);
        CompleteMarketSolutions = checked(1 + readings.Length);
    }
}

/// <summary>
/// R161: ONE new real worker per comparison, at the existing productive
/// archetype's node, with the same explicit production and demand profile.
/// All alternatives use exactly the same baseline and the same fresh ID.
/// Each addition causes both an additional producer and an additional buyer.
/// </summary>
public static class FoundationNewWorkerProductiveMarginalProjector
{
    public static FoundationNewWorkerProductiveAssessment Evaluate(
        WorldState baseline,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy marketPolicy,
        IReadOnlyList<FoundationNewWorkerProductiveOpportunity> candidates,
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
                "New-worker marginal evaluation requires an explicit bounded candidate set.");
        if (!baseline.Workers.HasAuthoritativeOccupations ||
            !baseline.Workers.HasAuthoritativeTacticalLocations ||
            !baseline.Transport.IsInitialized || baseline.WorldBinding is null ||
            geography.FineParameters != incidence.Refinement.FineParameters ||
            geography.FineElevationRaw.Count != incidence.FineTopology.Cells.Count)
            throw new ArgumentException(
                "New-worker proposals require a matching bound physical world.");

        var existing = baseline.Workers.Workers.ToDictionary(w => w.Id);
        if (existing.Count == 0 || existing.Keys.Max(x => x.Value) == ulong.MaxValue)
            throw new ArgumentException(
                "An existing real worker and an unused new physical identity are required.");
        var sorted = candidates.OrderBy(c => c?.ArchetypeWorker.Value ?? 0UL).ToArray();
        if (sorted.Any(c => c is null) ||
            sorted.Select(c => c.ArchetypeWorker).Distinct().Count() != sorted.Length)
            throw new ArgumentException("Every new-worker archetype must be unique.", nameof(candidates));

        foreach (var candidate in sorted)
            if (!existing.TryGetValue(candidate.ArchetypeWorker, out var person) ||
                person.Activity != PhysicalWorkerActivity.Producing ||
                person.TacticalTileId is not { IsValid: true } ||
                person.Occupation is not (PhysicalWorkerOccupation.Agriculture or
                    PhysicalWorkerOccupation.Forestry or
                    PhysicalWorkerOccupation.Mining))
                throw new ArgumentException(
                    "Only existing productive physical workers can provide an explicit first-stage production/demand archetype.",
                    nameof(candidates));

        var (roads, carrying) =
            FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
                baseline, incidence);
        var blocked = new HashSet<PhysicalTacticalEdgeId>();
        foreach (var edge in incidence.FineTopology.Edges)
        {
            int a = checked((int)edge.IncidentCellIds[0].Value - 1);
            int b = checked((int)edge.IncidentCellIds[1].Value - 1);
            if (geography.CrossingAtIndices(a, b) ==
                FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
                blocked.Add(new PhysicalTacticalEdgeId(
                    incidence.Refinement.FineParameters, edge.Id));
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
                    "R161 needs fully reconciled supply under the unchanged fixed-freight market.");
            var accounts = FoundationPhysicalEconomicAccountingReconciler
                .EvaluateCandidate(workforce, incidence,
                    marketPolicy.Demand, marketPolicy.Cargo, plan,
                    marketPolicy.TaxRates);
            return (accounts.GrossSalesScaled - accounts.PhysicalFreightScaled,
                audit.TotalAllocatedGoodsMicro);
        }

        var original = Solve(baseline.Workers);
        var originalInputs = FoundationPhysicalCommodityDemandProjector
            .Project(baseline.Workers, marketPolicy.Demand);
        var newbornId = new PhysicalWorkerId(
            checked(existing.Keys.Max(x => x.Value) + 1UL));
        var fingerprint = new CanonicalWorldStateHasher().Compute(baseline)
            .ToString() ?? throw new InvalidOperationException("Missing baseline hash.");
        var entries = new List<FoundationMarginalLaborObservation>();
        var readings = new List<FoundationNewWorkerProductiveReading>();

        foreach (var candidate in sorted)
        {
            var archetype = existing[candidate.ArchetypeWorker];
            var newborn = new PhysicalWorkerRuntimeRecord(newbornId,
                archetype.Owner, archetype.CellId, archetype.Commodity,
                archetype.OutputMicroPerTurn, PhysicalWorkerActivity.Producing,
                archetype.DemandMultiplierMicro, archetype.TacticalTileId,
                archetype.Occupation);

            // Do not alter an existing person. The new individual also
            // participates in commodity demand through QuotesFromRealWorkers.
            var expanded = new PhysicalWorkerRuntimeState(
                baseline.Workers.Workers.Append(newborn),
                tacticalLocationsAuthoritative: true,
                occupationsAuthoritative: true);
            if (expanded.Workers.Count != baseline.Workers.Workers.Count + 1 ||
                expanded.Workers.Where(w => w.Id != newbornId)
                    .Any(w => !Equals(w, existing[w.Id])))
                throw new InvalidOperationException(
                    "New-worker comparisons must preserve the entire original census.");
            var expandedInputs = FoundationPhysicalCommodityDemandProjector
                .Project(expanded, marketPolicy.Demand);
            var workerProfile = FoundationPhysicalCommodityDemandProjector
                .SelectProfile(newborn);
            ulong exactNewDemand = marketPolicy.Demand.Commodities
                .Aggregate(0UL, (sum, item) =>
                    checked(sum + checked((ulong)(
                        (UInt128)newborn.DemandMultiplierMicro *
                        marketPolicy.Demand.IntensityMicroPerWorker(
                            workerProfile, item) / 1_000_000U))));
            if (expandedInputs.DistinctWorkers !=
                    originalInputs.DistinctWorkers + 1 ||
                expandedInputs.TotalCommodityDemandMicro <
                    originalInputs.TotalCommodityDemandMicro ||
                expandedInputs.TotalCommodityDemandMicro -
                    originalInputs.TotalCommodityDemandMicro != exactNewDemand)
                throw new InvalidOperationException(
                    "New-worker comparisons must add the complete real commodity demand of exactly one worker.");
            var future = Solve(expanded);
            if (future.Goods < original.Goods ||
                future.Goods - original.Goods != newborn.OutputMicroPerTurn)
                throw new InvalidOperationException(
                    "New-worker market supply must increase by exactly the newborn's output.");

            var delta = future.Cash - original.Cash;
            entries.Add(new FoundationMarginalLaborObservation(
                fingerprint, FoundationMarginalLaborChangeKind.NewWorkerAddition,
                newborn.Owner, newborn.TacticalTileId!.Value,
                newborn.Occupation!.Value, delta));
            readings.Add(new FoundationNewWorkerProductiveReading(
                candidate.ArchetypeWorker, newborn.Owner,
                newborn.TacticalTileId.Value, newborn.Occupation.Value,
                newborn.Commodity, newborn.OutputMicroPerTurn,
                exactNewDemand, delta));
        }

        var results = readings.ToArray();
        return new FoundationNewWorkerProductiveAssessment(fingerprint,
            FoundationMarginalLaborDiagnosticIndexer.Rank(
                entries.ToArray(), topCount), results);
    }
}
