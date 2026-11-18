using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Converts ACTUALLY CLOSED and structurally audited cargo into the frozen
/// NEXT-cycle physical freight quote. It does not run water-filling, solve
/// routes, infer prices, publish economic history or settle any payments.
/// One independent congestion target calculation per declared crossing.
/// Only the trusted settlement read boundary may mark its result as closed.
/// </summary>
internal static class FoundationPhysicalSettledFreightQuoteUpdater
{
    internal static FoundationPhysicalLaggedFreightQuoteState FromClosedAudit(
        FoundationPhysicalTradeEdgeLoadAudit audit,
        GoldbergParameters fine,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowRoads,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        string comparableCargoUnitCode,
        ulong economicCycleOrdinal,
        FoundationPhysicalLaggedFreightQuoteState? previousSettled,
        uint smoothingDenominator)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(freeFlowRoads);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (!fine.IsValid || economicCycleOrdinal == 0UL ||
            smoothingDenominator == 0U)
            throw new ArgumentOutOfRangeException(nameof(economicCycleOrdinal));
        if (string.IsNullOrWhiteSpace(comparableCargoUnitCode) ||
            audit.CargoUnitCode != comparableCargoUnitCode)
            throw new InvalidOperationException(
                "Closed freight and carrying scales require the same explicitly " +
                "declared, comparable cargo micro-unit.");
        if (previousSettled is not null &&
            (!previousSettled.HasSettledProvenance ||
             previousSettled.AuditedPreviewCycles != economicCycleOrdinal - 1UL))
            throw new InvalidOperationException(
                "Next freight prices must derive from the immediately " +
                "preceding SETTLED economic cycle, never a preview or skipped cycle.");
        if (previousSettled is null && economicCycleOrdinal != 1UL)
            throw new InvalidOperationException(
                "Missing prior settled quote state; cannot synthesize " +
                "congestion history for a later economic cycle.");

        var roads = freeFlowRoads.OrderBy(
            road => road?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        if (roads.Length == 0)
            throw new ArgumentException(
                "Closed-cycle freight policy must declare real traversable crossings.",
                nameof(freeFlowRoads));

        var byRoad = new Dictionary<PhysicalTacticalEdgeId,
            FoundationPhysicalRoutingEdgeCost>(roads.Length);
        foreach (var road in roads)
        {
            if (road is null || !road.Edge.IsValid ||
                road.Edge.FineGoldbergParameters != fine ||
                road.PhysicalCostMicroPerWholeCargoUnit == 0UL ||
                !Enum.IsDefined(road.OwnershipKind) ||
                (road.OwnershipKind == FoundationInfrastructureOwnershipKind.Owned
                    ? road.InfrastructureOwner is not { IsValid: true }
                    : road.InfrastructureOwner.HasValue ||
                      road.ExternalTariffMicroPerWholeCargoUnit != 0UL) ||
                !byRoad.TryAdd(road.Edge, road))
                throw new ArgumentException(
                    "Each physical road needs one real fine edge, positive " +
                    "infrastructure cost and valid owner/tariff policy.",
                    nameof(freeFlowRoads));
        }

        var scales = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        foreach (var scale in carryingScales)
        {
            if (scale is null || !scale.Edge.IsValid ||
                scale.Edge.FineGoldbergParameters != fine ||
                scale.CargoMicroAtReferenceLoad == 0UL ||
                !scales.TryAdd(scale.Edge, scale.CargoMicroAtReferenceLoad))
                throw new ArgumentException(
                    "Every crossing requires one positive physical carrying scale.",
                    nameof(carryingScales));
        }
        if (scales.Count != roads.Length ||
            roads.Any(road => !scales.ContainsKey(road.Edge)))
            throw new ArgumentException(
                "Physical carrying scales must cover exactly the declared roads.",
                nameof(carryingScales));

        var loads = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        BigInteger total = BigInteger.Zero;
        foreach (var used in audit.UsedEdges)
        {
            if (used is null || !used.Edge.IsValid ||
                used.Edge.FineGoldbergParameters != fine ||
                used.TotalCargoMicro == 0UL ||
                !byRoad.TryGetValue(used.Edge, out var road) ||
                used.OwnershipKind != road.OwnershipKind ||
                used.Owner != road.InfrastructureOwner ||
                !loads.TryAdd(used.Edge, used.TotalCargoMicro))
                throw new InvalidOperationException(
                    "The SETTLED freight audit contains an undeclared crossing " +
                    "or conflicting historical infrastructure ownership.");
            total += used.TotalCargoMicro;
        }
        if (total != audit.TotalEdgeTraversalCargoMicro)
            throw new InvalidOperationException(
                "The settled freight crossings do not conserve audited cargo.");

        var old = previousSettled?.Edges.ToDictionary(entry => entry.Edge)
            ?? new Dictionary<PhysicalTacticalEdgeId,
                FoundationPhysicalLaggedFreightEdgeState>();
        var next = new FoundationPhysicalLaggedFreightEdgeState[roads.Length];
        for (int i = 0; i < roads.Length; i++)
        {
            var road = roads[i];
            ulong baseCost = road.PhysicalCostMicroPerWholeCargoUnit;
            ulong reference = scales[road.Edge];
            ulong previousSurcharge = 0UL;
            if (old.TryGetValue(road.Edge, out var prior) &&
                prior.FreePhysicalCostMicroPerWholeCargoUnit == baseCost &&
                prior.ReferenceCargoMicro == reference)
                previousSurcharge =
                    prior.QuotedCongestionSurchargeMicroPerWholeCargoUnit;

            // The entire current cycle has ALREADY closed. The surcharge
            // computed here must not change that cycle's realized trades.
            ulong targetRate =
                FoundationPhysicalEndogenousCongestionFeedback
                    .CalculateCongestedRate(
                        baseCost, loads.GetValueOrDefault(road.Edge), reference);
            ulong targetSurcharge = checked(targetRate - baseCost);
            ulong nextSurcharge =
                FoundationPhysicalEndogenousCongestionFeedback
                    .RelaxCostTowards(previousSurcharge, targetSurcharge,
                        smoothingDenominator);
            _ = checked(baseCost + nextSurcharge);
            next[i] = new FoundationPhysicalLaggedFreightEdgeState(
                road.Edge, baseCost, reference, nextSurcharge);
        }

        return new FoundationPhysicalLaggedFreightQuoteState(
            economicCycleOrdinal, next, hasSettledProvenance: true);
    }
}
