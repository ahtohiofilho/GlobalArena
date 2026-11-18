using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// The ONLY transport input adapter for authoritative-world candidate planning.
/// This reads the immutable, canonically hashed WorldState transport inventory;
/// it does not invent roads, prices, ownership, capacities or tariff policy.
/// External quote overloads remain available for explicitly labeled research.
/// </summary>
public static class FoundationPhysicalAuthoritativeTransportProjector
{
    public static (
        IReadOnlyList<FoundationPhysicalRoutingEdgeCost> Roads,
        IReadOnlyList<FoundationPhysicalEdgeCarryingScale> Scales)
        FromWorld(WorldState snapshot, PhysicalTacticalIncidenceMap incidence)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(incidence);
        if (!snapshot.Transport.IsInitialized ||
            snapshot.WorldBinding is null ||
            !snapshot.Workers.HasAuthoritativeOccupations ||
            incidence.Refinement.CoarseParameters.StrategicCellCount !=
                snapshot.WorldBinding.StrategicCellCount)
            throw new InvalidOperationException(
                "A candidate cannot claim authoritative freight without a " +
                "bound, initialized, hashed physical transport inventory.");

        var fine = incidence.Refinement.FineParameters;
        var known = incidence.FineTopology.Edges
            .Select(e => new PhysicalTacticalEdgeId(fine, e.Id))
            .ToHashSet();
        var roads = new List<FoundationPhysicalRoutingEdgeCost>(
            snapshot.Transport.Edges.Count);
        var scales = new List<FoundationPhysicalEdgeCarryingScale>(
            snapshot.Transport.Edges.Count);
        foreach (var edge in snapshot.Transport.Edges)
        {
            if (!known.Remove(edge.Edge))
                throw new InvalidOperationException(
                    "The world inventory references a foreign/duplicated fine crossing.");
            var ownership = edge.OwnershipKind switch
            {
                PhysicalTransportOwnershipKind.NoInfrastructure =>
                    FoundationInfrastructureOwnershipKind.NoInfrastructure,
                PhysicalTransportOwnershipKind.UnownedInfrastructure =>
                    FoundationInfrastructureOwnershipKind.UnownedInfrastructure,
                PhysicalTransportOwnershipKind.Owned =>
                    FoundationInfrastructureOwnershipKind.Owned,
                _ => throw new InvalidOperationException(
                    "Unknown authoritative infrastructure ownership kind.")
            };
            roads.Add(new FoundationPhysicalRoutingEdgeCost(
                edge.Edge, ownership, edge.InfrastructureOwner,
                edge.FreePhysicalCostMicroPerWholeCargoUnit,
                edge.ExternalTariffMicroPerWholeCargoUnit));
            scales.Add(new FoundationPhysicalEdgeCarryingScale(
                edge.Edge, edge.CargoMicroAtReferenceLoad));
        }
        if (known.Count != 0)
            throw new InvalidOperationException(
                "Authoritative physical transport inventory omits real crossings.");
        return (Array.AsReadOnly(roads.ToArray()),
            Array.AsReadOnly(scales.ToArray()));
    }
}
