using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Caller-supplied productivity for one verified physical crossing, in
/// SERVICE micro-units per whole unit of operational effort. The R150
/// prototype does not invent a final cargo capacity or market calibration.
/// </summary>
public sealed record FoundationOperationalEdgeProductivity(
    PhysicalTacticalEdgeId Edge,
    FoundationOperationalWorkKind Kind,
    ulong ServiceMicroPerWholeEffort);

/// <summary>
/// Read-only amount of work and corresponding service of one real crossing.
/// Effort is first summed across its real terminals, THEN converted to service
/// once, so fractional work at two terminals is not rounded away twice.
/// </summary>
public sealed record FoundationOperationalEdgeService(
    PhysicalTacticalEdgeId Edge,
    FoundationOperationalWorkKind Kind,
    ulong EffortMicro,
    ulong ServiceMicro,
    int ContributingTerminals);

public sealed class FoundationOperationalServiceResult
{
    public IReadOnlyList<FoundationOperationalEdgeService> Edges { get; }
    public ulong TotalAllocatedEffortMicro { get; }
    public ulong TotalIdleEffortMicro { get; }
    public BigInteger TotalServiceMicro { get; }

    internal FoundationOperationalServiceResult(
        FoundationOperationalEdgeService[] edges,
        ulong allocated, ulong idle, BigInteger service)
    {
        Edges = Array.AsReadOnly(edges);
        TotalAllocatedEffortMicro = allocated;
        TotalIdleEffortMicro = idle;
        TotalServiceMicro = service;
    }
}

/// <summary>
/// R150: work is an operational INPUT; service is its OUTPUT. Each physical
/// worker's effort was conserved in R145 and R146 before this transformation.
/// Capital already controls distribution of work; DO NOT multiply by capital
/// again here. No cargo volume, demand, price, congestion, or freight feedback.
/// </summary>
public static class FoundationOperationalServiceProjector
{
    public static FoundationOperationalServiceResult ProjectCommitted(
        WorldState world,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        IReadOnlyList<FoundationOperationalEdgeProductivity> productivity)
    {
        var nodes = FoundationPhysicalOperationalWorkProjector.ProjectCommitted(
            world, incidence, geography);
        return Project(nodes, productivity);
    }

    public static FoundationOperationalServiceResult Project(
        IReadOnlyList<FoundationPhysicalOperationalNodeAllocation> nodes,
        IReadOnlyList<FoundationOperationalEdgeProductivity> productivity)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(productivity);
        var factors = new Dictionary<PhysicalTacticalEdgeId,
            FoundationOperationalEdgeProductivity>();
        foreach (var factor in productivity)
        {
            if (factor is null || !factor.Edge.IsValid ||
                (factor.Kind != FoundationOperationalWorkKind.Land &&
                 factor.Kind != FoundationOperationalWorkKind.Port) ||
                factor.ServiceMicroPerWholeEffort == 0UL ||
                !factors.TryAdd(factor.Edge, factor))
                throw new ArgumentException(
                    "Every productivity factor must be positive, valid and unique.",
                    nameof(productivity));
        }

        var work = new Dictionary<PhysicalTacticalEdgeId,
            (FoundationOperationalWorkKind Kind, ulong Effort, int Terminals)>();
        var occupiedNodes = new HashSet<
            (PhysicalTacticalTileId Tile, CivilizationId Owner)>();
        ulong available = 0UL;
        ulong idle = 0UL;
        foreach (var node in nodes)
        {
            if (node is null || !node.Tile.IsValid || !node.Owner.IsValid ||
                node.Allocation is null ||
                !occupiedNodes.Add((node.Tile, node.Owner)))
                throw new ArgumentException(
                    "Each work node must be an independent real physical owner/node allocation.",
                    nameof(nodes));

            var allocation = node.Allocation;
            available = checked(available +
                checked(allocation.LandEffortMicro + allocation.PortEffortMicro));
            idle = checked(idle + checked(
                allocation.UnallocatedLandEffortMicro +
                allocation.UnallocatedPortEffortMicro));
            ulong land = 0UL;
            ulong port = 0UL;
            var seen = new HashSet<PhysicalTacticalEdgeId>();
            foreach (var share in allocation.Shares)
            {
                if (share is null || !share.Edge.IsValid ||
                    !seen.Add(share.Edge) ||
                    (share.Kind != FoundationOperationalWorkKind.Land &&
                     share.Kind != FoundationOperationalWorkKind.Port))
                    throw new ArgumentException(
                        "Invalid or repeated crossing within a physical node.",
                        nameof(nodes));

                if (share.Kind == FoundationOperationalWorkKind.Land)
                    land = checked(land + share.EffortMicro);
                else
                    port = checked(port + share.EffortMicro);
                if (share.EffortMicro == 0UL)
                    continue;
                if (!factors.TryGetValue(share.Edge, out var factor) ||
                    factor.Kind != share.Kind)
                    throw new ArgumentException(
                        "Positive work requires matching explicit crossing productivity.",
                        nameof(productivity));
                work.TryGetValue(share.Edge, out var prior);
                if (prior.Terminals > 0 && prior.Kind != share.Kind)
                    throw new ArgumentException(
                        "Physical crossing has conflicting operational categories.",
                        nameof(nodes));
                work[share.Edge] = (share.Kind,
                    checked(prior.Effort + share.EffortMicro),
                    checked(prior.Terminals + 1));
            }
            if (checked(land + allocation.UnallocatedLandEffortMicro) !=
                    allocation.LandEffortMicro ||
                checked(port + allocation.UnallocatedPortEffortMicro) !=
                    allocation.PortEffortMicro)
                throw new ArgumentException(
                    "Operational node must conserve each independent workforce pool.",
                    nameof(nodes));
        }

        var rows = new List<FoundationOperationalEdgeService>();
        BigInteger totalService = BigInteger.Zero;
        ulong used = 0UL;
        foreach (var pair in work.OrderBy(p => p.Key.FineStrategicEdgeId.Value))
        {
            var factor = factors[pair.Key];
            BigInteger service = (BigInteger)pair.Value.Effort *
                factor.ServiceMicroPerWholeEffort / 1_000_000UL;
            if (service > ulong.MaxValue)
                throw new OverflowException(
                    "Physical service exceeds representable micro-units.");
            ulong output = checked((ulong)service);
            used = checked(used + pair.Value.Effort);
            totalService += output;
            rows.Add(new FoundationOperationalEdgeService(
                pair.Key, pair.Value.Kind,
                pair.Value.Effort, output, pair.Value.Terminals));
        }
        if (checked(used + idle) != available)
            throw new InvalidOperationException(
                "Service conversion has lost or duplicated physical work.");
        return new FoundationOperationalServiceResult(
            rows.ToArray(), used, idle, totalService);
    }
}
