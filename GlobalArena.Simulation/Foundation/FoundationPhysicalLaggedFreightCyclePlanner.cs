using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// One edge's carry-over physical surcharge after an AUDITED candidate
/// cycle. Kept distinct from immutable infrastructure cost and external
/// owner tariff. A production integration MUST promote this state only
/// after an authoritative economic cycle closes, never after a preview.
/// </summary>
public sealed record FoundationPhysicalLaggedFreightEdgeState(
    PhysicalTacticalEdgeId Edge,
    ulong FreePhysicalCostMicroPerWholeCargoUnit,
    ulong ReferenceCargoMicro,
    ulong QuotedCongestionSurchargeMicroPerWholeCargoUnit);

/// <summary>
/// Immutable quote memory for the NEXT cycle. Not a price, ledger entry,
/// closed-cycle receipt or cumulative inventory.
/// </summary>
public sealed class FoundationPhysicalLaggedFreightQuoteState
{
    public ulong AuditedPreviewCycles { get; }
    public IReadOnlyList<FoundationPhysicalLaggedFreightEdgeState> Edges { get; }

    internal FoundationPhysicalLaggedFreightQuoteState(
        ulong cycles, FoundationPhysicalLaggedFreightEdgeState[] edges)
    {
        AuditedPreviewCycles = cycles;
        Edges = Array.AsReadOnly(edges);
    }
}

/// <summary>
/// One immutable physical quote for the CURRENT solve, its observed cargo,
/// and the damped price quotation available only to the NEXT cycle.
/// </summary>
public sealed record FoundationPhysicalLaggedFreightEdgeReading(
    PhysicalTacticalEdgeId Edge,
    ulong FreePhysicalCostMicroPerWholeCargoUnit,
    ulong CurrentPhysicalQuoteMicroPerWholeCargoUnit,
    ulong CurrentCargoMicro,
    ulong CargoMicroAtReferenceLoad,
    ulong TargetNextPhysicalQuoteMicroPerWholeCargoUnit,
    ulong DampedNextPhysicalQuoteMicroPerWholeCargoUnit,
    FoundationInfrastructureOwnershipKind OwnershipKind,
    CivilizationId? InfrastructureOwner,
    ulong ExternalTariffMicroPerWholeCargoUnit);

/// <summary>
/// One and only one fixed-freight global marginal market solve.
/// Unlike same-turn Picard or joint market/route optimization, this does
/// not reprice or resolve the CURRENT turn after seeing its freight loads.
/// This is uncommitted research evidence, never an economic settlement.
/// </summary>
public sealed class FoundationPhysicalLaggedFreightCyclePlan
{
    public FoundationPhysicalGlobalMarginalCoordinationPlan Distribution { get; }
    public IReadOnlyList<FoundationPhysicalRoutingEdgeCost> FixedCycleEdges { get; }
    public IReadOnlyList<FoundationPhysicalLaggedFreightEdgeReading> EdgeReadings { get; }
    public FoundationPhysicalLaggedFreightQuoteState NextCycleQuotes { get; }
    public uint SmoothingDenominator { get; }
    public bool RepricedWithinCycle => false;

    internal FoundationPhysicalLaggedFreightCyclePlan(
        FoundationPhysicalGlobalMarginalCoordinationPlan distribution,
        FoundationPhysicalRoutingEdgeCost[] fixedEdges,
        FoundationPhysicalLaggedFreightEdgeReading[] readings,
        FoundationPhysicalLaggedFreightQuoteState next, uint denominator)
    {
        Distribution = distribution;
        FixedCycleEdges = Array.AsReadOnly(fixedEdges);
        EdgeReadings = Array.AsReadOnly(readings);
        NextCycleQuotes = next;
        SmoothingDenominator = denominator;
    }
}

/// <summary>
/// DELIBERATE architecture pivot: congestion is evaluated only AFTER an
/// independently audited fixed-cost marginal distribution. The resulting
/// smoothed surcharge is offered to the NEXT cycle, not reapplied here.
/// This eliminates the endogenous same-turn congestion fixed point.
/// The visible market marginal signal is still P0*sqrt(D/Q); fixed freight
/// and owner tariffs determine seller-specific destination thresholds.
/// </summary>
public static class FoundationPhysicalLaggedFreightCyclePlanner
{
    // One quarter of the difference per cycle. Deterministic under-relaxation
    // limits a one-turn change; it does NOT mathematically guarantee absence
    // of oscillation under all possible discrete demand/route responses.
    public const uint DefaultSmoothingDenominator = 4U;

    internal static ulong NextSurcharge(
        ulong freeCost, ulong previousSurcharge,
        ulong observedCargo, ulong referenceCargo, uint smoothingDenominator)
    {
        if (smoothingDenominator == 0U)
            throw new ArgumentOutOfRangeException(nameof(smoothingDenominator));
        ulong raw = FoundationPhysicalEndogenousCongestionFeedback
            .CalculateCongestedRate(freeCost, observedCargo, referenceCargo);
        ulong targetSurcharge = checked(raw - freeCost);
        return FoundationPhysicalEndogenousCongestionFeedback.RelaxCostTowards(
            previousSurcharge, targetSurcharge, smoothingDenominator);
    }

    public static FoundationPhysicalLaggedFreightCyclePlan CoordinateCandidate(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> marketQuotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdges,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        FoundationPhysicalLaggedFreightQuoteState? previousClosedCycleQuotes = null,
        int maxProducerPasses = 8,
        uint smoothingDenominator = DefaultSmoothingDenominator)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(marketQuotes);
        ArgumentNullException.ThrowIfNull(freeFlowEdges);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (maxProducerPasses < 1 || smoothingDenominator == 0U)
            throw new ArgumentOutOfRangeException(nameof(maxProducerPasses),
                "Positive fixed-cost producer budget and smoothing denominator required.");

        // Freeze the actual worker quotes and real physical graph exactly
        // once at the start of the cycle. The costs never change during it.
        var quotes = marketQuotes.ToArray();

        // Product policy: a universal marginal price function, never
        // an arbitrary civilization/market-specific starting valuation.
        // D (real weighted workers) and Q (real delivered goods) are the
        // ONLY market-specific inputs to P0*sqrt(D/Q). Reject inconsistent
        // quote vectors until the legacy per-market quote input can be
        // removed from the public planner contract.
        if (quotes.Length == 0 || quotes[0] is null ||
            quotes[0].QuoteMicroPerWholeCommodityUnit == 0UL)
            throw new ArgumentException(
                "Universal marginal baseline must be positive and defined.",
                nameof(marketQuotes));
        ulong universalQuote = quotes[0].QuoteMicroPerWholeCommodityUnit;
        if (quotes.Any(q => q is null ||
            q.QuoteMicroPerWholeCommodityUnit != universalQuote))
            throw new ArgumentException(
                "Market-specific reference prices contradict the " +
                "universal commodity/market marginal price function; " +
                "only real demand and goods quantities may shift prices.",
                nameof(marketQuotes));

        var roads = freeFlowEdges
            .OrderBy(r => r?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
            incidence, roads);

        var scales = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        foreach (var carrying in carryingScales)
        {
            if (carrying is null || !carrying.Edge.IsValid ||
                carrying.Edge.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                carrying.CargoMicroAtReferenceLoad == 0UL ||
                !scales.TryAdd(carrying.Edge,
                    carrying.CargoMicroAtReferenceLoad))
                throw new ArgumentException(
                    "Require one positive physical carrying scale per real crossing.",
                    nameof(carryingScales));
        }
        if (scales.Count != roads.Length ||
            roads.Any(road => !scales.ContainsKey(road.Edge)))
            throw new ArgumentException(
                "Carrying scales must cover all and only declared roads.",
                nameof(carryingScales));

        var old = new Dictionary<PhysicalTacticalEdgeId,
            FoundationPhysicalLaggedFreightEdgeState>();
        if (previousClosedCycleQuotes is not null)
        {
            foreach (var entry in previousClosedCycleQuotes.Edges)
                if (entry is null || !entry.Edge.IsValid ||
                    !old.TryAdd(entry.Edge, entry))
                    throw new ArgumentException(
                        "Previous quote state must have unique real crossings.",
                        nameof(previousClosedCycleQuotes));
            if (old.Count != roads.Length ||
                roads.Any(road => !old.ContainsKey(road.Edge)))
                throw new ArgumentException(
                    "Previous quote state must cover exactly the same physical crossings.",
                    nameof(previousClosedCycleQuotes));
        }

        var fixedRoads = new FoundationPhysicalRoutingEdgeCost[roads.Length];
        var carriedSurcharge = new ulong[roads.Length];
        for (int i = 0; i < roads.Length; i++)
        {
            var road = roads[i];
            ulong scale = scales[road.Edge];
            if (old.TryGetValue(road.Edge, out var prior) &&
                prior.FreePhysicalCostMicroPerWholeCargoUnit ==
                    road.PhysicalCostMicroPerWholeCargoUnit &&
                prior.ReferenceCargoMicro == scale)
                carriedSurcharge[i] =
                    prior.QuotedCongestionSurchargeMicroPerWholeCargoUnit;
            // A road upgrade or capacity change takes effect immediately.
            // Reset only this edge's prior congestion memory when its
            // physical baseline or reference capacity changes.
            fixedRoads[i] = road with
            {
                PhysicalCostMicroPerWholeCargoUnit = checked(
                    road.PhysicalCostMicroPerWholeCargoUnit +
                    carriedSurcharge[i])
            };
        }

        // Exactly ONE global producer/water-filling solve at the quoted,
        // fixed per-edge rates. It may have its own bounded producer passes,
        // but NEVER recomputes congestion nor changes these edge quotes.
        var distribution = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            census, incidence, demandPolicy, conversionPolicy, quotes,
            fixedRoads, maxProducerPasses);
        var freight = distribution.AllocationAudit.Freight;
        var loads = freight.UsedEdges.ToDictionary(
            edge => edge.Edge, edge => edge.TotalCargoMicro);

        var readings = new FoundationPhysicalLaggedFreightEdgeReading[roads.Length];
        var nextEdges = new FoundationPhysicalLaggedFreightEdgeState[roads.Length];
        BigInteger totalCrossingCargo = BigInteger.Zero;
        for (int i = 0; i < roads.Length; i++)
        {
            var baseRoad = roads[i];
            ulong load = loads.GetValueOrDefault(baseRoad.Edge);
            totalCrossingCargo += load;
            // Compute the congestion target ONCE per crossing AFTER
            // the fixed-cost water-filling solve. It must never feed
            // back into this same turn's allocation or route costs.
            ulong targetQuote =
                FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                    baseRoad.PhysicalCostMicroPerWholeCargoUnit, load,
                    scales[baseRoad.Edge]);
            ulong targetSurcharge = checked(targetQuote -
                baseRoad.PhysicalCostMicroPerWholeCargoUnit);
            ulong nextSurcharge =
                FoundationPhysicalEndogenousCongestionFeedback.RelaxCostTowards(
                    carriedSurcharge[i], targetSurcharge,
                    smoothingDenominator);
            ulong nextQuote = checked(
                baseRoad.PhysicalCostMicroPerWholeCargoUnit + nextSurcharge);
            readings[i] = new FoundationPhysicalLaggedFreightEdgeReading(
                baseRoad.Edge,
                baseRoad.PhysicalCostMicroPerWholeCargoUnit,
                fixedRoads[i].PhysicalCostMicroPerWholeCargoUnit,
                load, scales[baseRoad.Edge], targetQuote, nextQuote,
                baseRoad.OwnershipKind, baseRoad.InfrastructureOwner,
                baseRoad.ExternalTariffMicroPerWholeCargoUnit);
            nextEdges[i] = new FoundationPhysicalLaggedFreightEdgeState(
                baseRoad.Edge,
                baseRoad.PhysicalCostMicroPerWholeCargoUnit,
                scales[baseRoad.Edge], nextSurcharge);
        }
        if (totalCrossingCargo != freight.TotalEdgeTraversalCargoMicro)
            throw new InvalidOperationException(
                "Observed cycle crossing cargo disagrees with the physical freight audit.");
        if (distribution.AllocationAudit.TotalAvailableGoodsMicro !=
            distribution.AllocationAudit.TotalAllocatedGoodsMicro)
            throw new InvalidOperationException(
                "A cycle cannot carry forward quotes for unallocated real production.");

        return new FoundationPhysicalLaggedFreightCyclePlan(
            distribution, fixedRoads, readings,
            new FoundationPhysicalLaggedFreightQuoteState(
                checked((previousClosedCycleQuotes?.AuditedPreviewCycles ?? 0UL)
                    + 1UL), nextEdges), smoothingDenominator);
    }
}
