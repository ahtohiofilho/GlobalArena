using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Read-only presentation of a physical connection incident to ONE selected
/// tactical node. This is not a multi-edge commercial route or an inventory.
/// </summary>
public sealed record FoundationLocalConnection(
    PhysicalTacticalEdgeId Edge,
    PhysicalTacticalTileId Neighbor,
    PhysicalTransportOwnershipKind Ownership,
    CivilizationId? InfrastructureOwner,
    ulong UnitPhysicalFreightMicro,
    ulong ExternalTariffMicro,
    ulong InvestedMoneyMicro);

public static class FoundationLocalConnectionsProjector
{
    public static IReadOnlyList<FoundationLocalConnection> Read(
        WorldState snapshot, PhysicalTacticalIncidenceMap incidence,
        PhysicalTacticalTileId node)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(incidence);
        if (!snapshot.Transport.IsInitialized || snapshot.WorldBinding is null ||
            incidence.Refinement.CoarseParameters.StrategicCellCount !=
                snapshot.WorldBinding.StrategicCellCount ||
            node.FineGoldbergParameters != incidence.Refinement.FineParameters)
            throw new InvalidOperationException(
                "Local links require the matching authoritative physical world.");
        var known = incidence.FineTopology.Cells
            .Any(x => x.Id == node.FineStrategicCellId);
        if (!known)
            throw new InvalidOperationException("Unknown physical node.");
        var physicalEdges = incidence.FineTopology.Edges
            .Where(x => x.IncidentCellIds.Contains(node.FineStrategicCellId))
            .ToArray();
        var transport = snapshot.Transport.Edges.ToDictionary(x => x.Edge);
        var result = new List<FoundationLocalConnection>(physicalEdges.Length);
        foreach (var topology in physicalEdges)
        {
            var edgeId = new PhysicalTacticalEdgeId(
                incidence.Refinement.FineParameters, topology.Id);
            if (!transport.TryGetValue(edgeId, out var entry))
                throw new InvalidOperationException(
                    "A displayed link has no authoritative transport record.");
            var neighborId = topology.IncidentCellIds.Single(
                x => x != node.FineStrategicCellId);
            var neighbor = new PhysicalTacticalTileId(
                incidence.Refinement.FineParameters, neighborId);
            result.Add(new FoundationLocalConnection(
                edgeId, neighbor, entry.OwnershipKind,
                entry.InfrastructureOwner,
                FoundationPhysicalAuthoritativeTransportProjector
                    .EffectiveUnitFreightCost(entry),
                entry.ExternalTariffMicroPerWholeCargoUnit,
                entry.CommittedInvestmentMoneyMicro.GetValueOrDefault()));
        }
        return Array.AsReadOnly(result
            .OrderBy(x => x.Neighbor.FineStrategicCellId.Value)
            .ToArray());
    }
}
