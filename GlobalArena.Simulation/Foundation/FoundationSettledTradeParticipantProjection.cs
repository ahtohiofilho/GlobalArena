using System.Collections.ObjectModel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// SERVER-ONLY participant projection of a freshly computed, fully audited
/// economic sale-and-consumption cycle. A player sees crossing volumes only
/// from transactions in which that civilization was a REAL buyer or seller.
/// In particular it never receives the global edge totals, unrelated
/// counterparties, or the full freight vector used by the next-cycle kernel.
/// </summary>
internal static class FoundationSettledTradeParticipantProjection
{
    internal static IReadOnlyDictionary<CivilizationId,
        IReadOnlyList<FoundationPhysicalTradeEdgeLoad>> FromAuditedPlan(
        WorldState source,
        PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy cargoPolicy,
        FoundationPhysicalLaggedFreightCyclePlan plan)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(cargoPolicy);
        ArgumentNullException.ThrowIfNull(plan);
        var full = plan.Distribution.AllocationAudit.Freight;
        if (full.CargoUnitCode != cargoPolicy.CargoUnitCode ||
            plan.Distribution.AllocationAudit.TotalAvailableGoodsMicro !=
                plan.Distribution.AllocationAudit.TotalAllocatedGoodsMicro)
            throw new InvalidOperationException(
                "Only a fully audited physical market may project historical trade.");

        var (_, ownership) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, plan.FixedCycleEdges);
        return FromDistribution(source, incidence, cargoPolicy, plan.Distribution, ownership, requireCompleteOwnership: true);
    }

    // Temporal settlement already has a complete audited distribution. Its
    // historical ownership is from the freight-stage world, not necessarily
    // the post-construction or post-birth committed snapshot.
    internal static IReadOnlyDictionary<CivilizationId,
        IReadOnlyList<FoundationPhysicalTradeEdgeLoad>> FromTemporalDistribution(
        WorldState freightSource, PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy cargoPolicy,
        FoundationPhysicalGlobalMarginalCoordinationPlan distribution)
    {
        var ownership = distribution.AllocationAudit.Freight.UsedEdges
            .Select(e => new FoundationTransportEdgeOwnershipEvidence(
                e.Edge, e.OwnershipKind, e.Owner)).ToArray();
        return FromDistribution(freightSource, incidence, cargoPolicy,
            distribution, ownership, requireCompleteOwnership: false);
    }

    private static IReadOnlyDictionary<CivilizationId,
        IReadOnlyList<FoundationPhysicalTradeEdgeLoad>> FromDistribution(
        WorldState source, PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy cargoPolicy,
        FoundationPhysicalGlobalMarginalCoordinationPlan distribution,
        IReadOnlyList<FoundationTransportEdgeOwnershipEvidence> ownership,
        bool requireCompleteOwnership)
    {
        var full = distribution.AllocationAudit.Freight;
        if (full.CargoUnitCode != cargoPolicy.CargoUnitCode ||
            distribution.AllocationAudit.TotalAvailableGoodsMicro !=
                distribution.AllocationAudit.TotalAllocatedGoodsMicro)
            throw new InvalidOperationException("Trade projection requires a fully audited market.");
        var realOwners = source.Transport.Edges.ToDictionary(r => r.Edge);
        if ((requireCompleteOwnership && ownership.Count != realOwners.Count) ||
            ownership.Any(row => !realOwners.ContainsKey(row.Edge)))
            throw new InvalidOperationException(
                "Route ownership must originate in the actual transport world.");
        var fullVolumes = full.UsedEdges.ToDictionary(
            r => r.Edge, r => r.TotalCargoMicro);
        var result = new Dictionary<CivilizationId,
            IReadOnlyList<FoundationPhysicalTradeEdgeLoad>>();
        foreach (var civ in source.Civilizations.Civilizations
            .OrderBy(id => id.Value))
        {
            // Buyer's and seller's real transactions are observable to
            // the involved civilization. A delivery is NEVER counted twice
            // when both parties belong to the same civilization.
            var deliveries = distribution.Candidates
                .Where(c => c.Buyer == civ || c.Seller == civ)
                .OrderBy(c => c.DeliveryOrdinal)
                .Select(c => new FoundationFreightDeliveryRouteEvidence(
                    c.DeliveryOrdinal, c.Commodity,
                    cargoPolicy.CargoMicro(c.Commodity, c.GoodsMicro),
                    cargoPolicy.CargoUnitCode, c.RouteTiles))
                .ToArray();
            var owned = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                incidence, cargoPolicy.CargoUnitCode, deliveries, ownership);
            foreach (var crossing in owned.UsedEdges)
            {
                if (!fullVolumes.TryGetValue(crossing.Edge, out ulong global) ||
                    crossing.TotalCargoMicro == 0UL ||
                    crossing.TotalCargoMicro > global ||
                    !realOwners.TryGetValue(crossing.Edge, out var actual))
                    throw new InvalidOperationException(
                        "A visible participant crossing must be a subset " +
                        "of the same audited global cargo and real topology.");
                var realKind = actual.OwnershipKind switch
                {
                    PhysicalTransportOwnershipKind.NoInfrastructure =>
                        FoundationInfrastructureOwnershipKind.NoInfrastructure,
                    PhysicalTransportOwnershipKind.UnownedInfrastructure =>
                        FoundationInfrastructureOwnershipKind.UnownedInfrastructure,
                    PhysicalTransportOwnershipKind.Owned =>
                        FoundationInfrastructureOwnershipKind.Owned,
                    _ => throw new InvalidOperationException(
                        "Unknown actual infrastructure ownership.")
                };
                if (crossing.OwnershipKind != realKind ||
                    crossing.Owner != actual.InfrastructureOwner)
                    throw new InvalidOperationException(
                        "A participant overlay cannot invent ownership colors.");
            }
            result.Add(civ, Array.AsReadOnly(owned.UsedEdges.ToArray()));
        }
        return new ReadOnlyDictionary<CivilizationId,
            IReadOnlyList<FoundationPhysicalTradeEdgeLoad>>(result);
    }

    /// <summary>
    /// The new audit property is not part of the WorldState money hash,
    /// because it does not change the economic result. Nevertheless
    /// independent EVENT replay verifies every private participant volume
    /// against a fresh solver before any UI publication is allowed.
    /// </summary>
    internal static bool Identical(
        IReadOnlyDictionary<CivilizationId,
            IReadOnlyList<FoundationPhysicalTradeEdgeLoad>>? received,
        IReadOnlyDictionary<CivilizationId,
            IReadOnlyList<FoundationPhysicalTradeEdgeLoad>>? derived)
    {
        if (received is null || derived is null ||
            received.Count != derived.Count) return false;
        foreach (var (civilization, actual) in derived)
        {
            if (!received.TryGetValue(civilization, out var declared) ||
                declared.Count != actual.Count) return false;
            for (int i = 0; i < actual.Count; i++)
            {
                var a = actual[i];
                var b = declared[i];
                if (a.Edge != b.Edge ||
                    a.FirstTile != b.FirstTile ||
                    a.SecondTile != b.SecondTile ||
                    a.TotalCargoMicro != b.TotalCargoMicro ||
                    a.OwnershipKind != b.OwnershipKind ||
                    a.Owner != b.Owner ||
                    a.ByCommodity.Count != b.ByCommodity.Count ||
                    !a.ByCommodity.SequenceEqual(b.ByCommodity))
                    return false;
            }
        }
        return true;
    }
}
