using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit scale of operational service relative to a freight-price benefit.
/// This is a research-only conversion, NOT an imposed trade throughput limit.
/// A separate fixed scale for LAND and PORT avoids implicit equivalence
/// between port operations and overland transport.
/// </summary>
public sealed record FoundationOperationalFreightShadowPolicy(
    ulong LandReferenceServiceMicro,
    ulong PortReferenceServiceMicro)
{
    public void RequireValid()
    {
        if (LandReferenceServiceMicro == 0UL ||
            PortReferenceServiceMicro == 0UL)
            throw new ArgumentOutOfRangeException(nameof(LandReferenceServiceMicro),
                "Land and port reference service must be explicitly positive.");
    }
}

public sealed record FoundationOperationalFreightShadowEdge(
    PhysicalTacticalEdgeId Edge,
    ulong OriginalPhysicalCostMicroPerWholeCargoUnit,
    ulong ShadowPhysicalCostMicroPerWholeCargoUnit,
    ulong OperationalServiceMicro,
    FoundationOperationalWorkKind? WorkKind);

/// <summary>
/// Proposed fixed prices for ONE comparison, NOT a settlement quote.
/// Rates never depend on cargo, sales, prices or the chosen route.
/// </summary>
public sealed class FoundationOperationalFreightShadowResult
{
    public IReadOnlyList<FoundationPhysicalRoutingEdgeCost> CounterfactualRoads { get; }
    public IReadOnlyList<FoundationOperationalFreightShadowEdge> EdgeReadings { get; }

    internal FoundationOperationalFreightShadowResult(
        FoundationPhysicalRoutingEdgeCost[] roads,
        FoundationOperationalFreightShadowEdge[] readings)
    {
        CounterfactualRoads = Array.AsReadOnly(roads);
        EdgeReadings = Array.AsReadOnly(readings);
    }
}

/// <summary>
/// R153 read-only bridge from actual conserved operating WORK (R145-R152)
/// to a fixed physical freight quote. Shadow cost:
///
///     C_shadow = max(1, floor(C_authoritative * R / (R + S)))
///
/// S is existing physical operational service, R is an explicit class
/// calibration, C_authoritative already contains any committed capital effect.
/// Service zero MUST NOT produce any new freight discount; this also avoids
/// converting investment into a second discount without real operators.
/// No change to owner tariffs, titles, sea freight, cargo, market or state.
/// This formula is deliberately not plugged into economic settlement yet.
/// </summary>
public static class FoundationOperationalFreightShadowProjector
{
    public static FoundationOperationalFreightShadowResult Project(
        IReadOnlyList<FoundationPhysicalRoutingEdgeCost> authoritativeRoads,
        FoundationOperationalServiceResult service,
        FoundationOperationalFreightShadowPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(authoritativeRoads);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(policy);
        policy.RequireValid();

        var serviceByEdge = new Dictionary<PhysicalTacticalEdgeId,
            FoundationOperationalEdgeService>();
        foreach (var item in service.Edges)
            if (item is null || !item.Edge.IsValid ||
                (item.Kind != FoundationOperationalWorkKind.Land &&
                 item.Kind != FoundationOperationalWorkKind.Port) ||
                item.EffortMicro == 0UL ||
                !serviceByEdge.TryAdd(item.Edge, item))
                throw new ArgumentException(
                    "Shadow quotes require unique, physically worked operational service rows.",
                    nameof(service));

        var seen = new HashSet<PhysicalTacticalEdgeId>();
        var roads = new List<FoundationPhysicalRoutingEdgeCost>();
        var readings = new List<FoundationOperationalFreightShadowEdge>();
        foreach (var road in authoritativeRoads
            .OrderBy(x => x?.Edge.FineStrategicEdgeId.Value ?? 0UL))
        {
            if (road is null || !road.Edge.IsValid ||
                road.PhysicalCostMicroPerWholeCargoUnit == 0UL ||
                !seen.Add(road.Edge))
                throw new ArgumentException(
                    "One valid and positive fixed freight quote per crossing is required.",
                    nameof(authoritativeRoads));
            serviceByEdge.TryGetValue(road.Edge, out var operational);
            ulong shadow = road.PhysicalCostMicroPerWholeCargoUnit;
            if (operational is not null)
            {
                ulong reference = operational.Kind ==
                    FoundationOperationalWorkKind.Land
                    ? policy.LandReferenceServiceMicro
                    : policy.PortReferenceServiceMicro;
                BigInteger quote =
                    (BigInteger)shadow * reference /
                    ((BigInteger)reference + operational.ServiceMicro);
                shadow = quote < BigInteger.One
                    ? 1UL : checked((ulong)quote);
            }

            roads.Add(road with { PhysicalCostMicroPerWholeCargoUnit = shadow });
            readings.Add(new FoundationOperationalFreightShadowEdge(
                road.Edge, road.PhysicalCostMicroPerWholeCargoUnit, shadow,
                operational?.ServiceMicro ?? 0UL, operational?.Kind));
        }
        if (serviceByEdge.Keys.Any(id => !seen.Contains(id)))
            throw new ArgumentException(
                "Operational service contains a crossing missing from the fixed world roads.",
                nameof(service));

        return new FoundationOperationalFreightShadowResult(
            roads.ToArray(), readings.ToArray());
    }
}
