using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R78: read-only attachment of PROSPECTIVE R75/R76 tolls to seller-specific
/// traversals in the actual, fully audited physical market plan. A buyer's
/// visibility projection is NOT a seller-specific toll receipt. This adapter
/// is not a settled event and cannot authorize a treasury transaction.
/// </summary>
public static class FoundationPhysicalAuditedSellerTollBinding
{
    public sealed record Evidence(
        string CargoUnitCode,
        ulong TotalAuditedGoodsMicro,
        ulong TotalAuditedEdgeTraversalCargoMicro,
        IReadOnlyList<FoundationPhysicalCandidateTariffTransferReconciliation
            .CandidateDebit> SellerEdgeCandidates);

    public static Evidence Inspect(
        WorldState source,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demand,
        FoundationCommodityCargoConversionPolicy cargo,
        FoundationPhysicalLaggedFreightCyclePlan plan,
        ulong planningEconomicCycleOrdinal,
        ulong tariffReferenceCargoMicro,
        uint ownerBenefitShareNumerator,
        uint ownerBenefitShareDenominator)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demand);
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(plan);

        var original = plan.Distribution.AllocationAudit;
        if (original.TotalAvailableGoodsMicro !=
                original.TotalAllocatedGoodsMicro ||
            original.Freight.CargoUnitCode != cargo.CargoUnitCode ||
            original.DeliveryCount != plan.Distribution.Candidates.Count ||
            plan.RepricedWithinCycle ||
            plan.NextCycleQuotes.HasSettledProvenance)
            throw new InvalidOperationException(
                "Only a complete audited, fixed-current-cycle market " +
                "candidate can bind seller-specific toll traversals.");

        var (_, ownership) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, plan.FixedCycleEdges);
        var independentlyAudited =
            FoundationPhysicalDeliveryAllocationAuditor.Audit(
                source.Workers, incidence, demand, cargo,
                plan.Distribution.Candidates, ownership);
        if (independentlyAudited.TotalAllocatedGoodsMicro !=
                original.TotalAllocatedGoodsMicro ||
            independentlyAudited.TotalAvailableGoodsMicro !=
                original.TotalAvailableGoodsMicro ||
            independentlyAudited.Freight.TotalEdgeTraversalCargoMicro !=
                original.Freight.TotalEdgeTraversalCargoMicro ||
            independentlyAudited.Freight.DeliveryCount !=
                original.Freight.DeliveryCount ||
            independentlyAudited.Freight.UsedEdges.Count !=
                original.Freight.UsedEdges.Count)
            throw new InvalidOperationException(
                "Replayed cargo audit disagrees with the supplied market.");

        var realEdges = source.Transport.Edges.ToDictionary(e => e.Edge);
        var auditedVolumes = original.Freight.UsedEdges.ToDictionary(
            e => e.Edge, e => e.TotalCargoMicro);
        var certifiedVolumes = independentlyAudited.Freight.UsedEdges
            .ToDictionary(e => e.Edge, e => e.TotalCargoMicro);
        if (auditedVolumes.Count != certifiedVolumes.Count ||
            auditedVolumes.Any(row =>
                !certifiedVolumes.TryGetValue(row.Key, out var load) ||
                row.Value != load))
            throw new InvalidOperationException(
                "Every physical edge must have identical independently " +
                "replayed cargo; no inferred crossing or duplicated passage.");

        var byEndpoints = new Dictionary<
            (ulong First, ulong Second), PhysicalTacticalEdgeId>();
        foreach (var edge in incidence.FineTopology.Edges)
        {
            ulong a = edge.IncidentCellIds[0].Value;
            ulong b = edge.IncidentCellIds[1].Value;
            var key = (Math.Min(a, b), Math.Max(a, b));
            if (!byEndpoints.TryAdd(key, new PhysicalTacticalEdgeId(
                    incidence.Refinement.FineParameters, edge.Id)))
                throw new InvalidOperationException(
                    "Duplicate physical crossing in signed fine topology.");
        }
        var sellerCrossings = new Dictionary<
            (PhysicalTacticalEdgeId Edge, CivilizationId Seller,
                CivilizationId Owner), ulong>();
        foreach (var delivery in plan.Distribution.Candidates)
        {
            ulong mass = cargo.CargoMicro(delivery.Commodity,
                delivery.GoodsMicro);
            for (int i = 1; i < delivery.RouteTiles.Count; i++)
            {
                ulong a = delivery.RouteTiles[i - 1]
                    .FineStrategicCellId.Value;
                ulong b = delivery.RouteTiles[i]
                    .FineStrategicCellId.Value;
                if (!byEndpoints.TryGetValue(
                        (Math.Min(a, b), Math.Max(a, b)), out var edgeId) ||
                    !auditedVolumes.ContainsKey(edgeId) ||
                    !realEdges.TryGetValue(edgeId, out var edge))
                    throw new InvalidOperationException(
                        "Delivery cannot charge an absent or unaudited edge.");
                if (edge.OwnershipKind !=
                        PhysicalTransportOwnershipKind.Owned ||
                    edge.InfrastructureOwner is not CivilizationId owner ||
                    owner == delivery.Seller)
                    continue;

                var key = (edgeId, delivery.Seller, owner);
                sellerCrossings[key] = checked(
                    sellerCrossings.GetValueOrDefault(key) + mass);
            }
        }
        var result = new List<FoundationPhysicalCandidateTariffTransferReconciliation
            .CandidateDebit>();
        foreach (var (key, cargoMicro) in sellerCrossings
            .OrderBy(pair => pair.Key.Edge.FineStrategicEdgeId.Value)
            .ThenBy(pair => pair.Key.Seller.Value)
            .ThenBy(pair => pair.Key.Owner.Value))
        {
            var quote = FoundationPhysicalFrozenTariffAdmission.Inspect(
                source, incidence, cargo, key.Edge, key.Seller,
                planningEconomicCycleOrdinal, tariffReferenceCargoMicro,
                ownerBenefitShareNumerator, ownerBenefitShareDenominator);
            if (quote.SellerPayableTariffMicro == 0UL)
                continue;
            result.Add(new FoundationPhysicalCandidateTariffTransferReconciliation
                .CandidateDebit(key.Edge, key.Seller, key.Owner,
                    cargoMicro, quote.SellerPayableTariffMicro));
        }

        // Every nonzero candidate represents REAL cargo in the independent
        // global freight audit, never a buyer-visible but unrelated route.
        return new Evidence(cargo.CargoUnitCode,
            original.TotalAllocatedGoodsMicro,
            original.Freight.TotalEdgeTraversalCargoMicro,
            Array.AsReadOnly(result.ToArray()));
    }
}
