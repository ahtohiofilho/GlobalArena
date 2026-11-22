using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R145: operational workforce is physically located at a fine node.
/// Land workers are shared by its LAND crossings; port workers by its COAST
/// crossings. The projector has no market, freight or turn-close side effects.
/// Its caller supplies only real, already verified incident crossings.
/// </summary>
public enum FoundationOperationalWorkKind
{
    Land = 1,
    Port = 2
}

/// <summary>
/// Effective operational weight includes a positive natural baseline and
/// additional settled capital. Both use the SAME weight units. No cargo-load
/// dependent congestion and no uncommitted monetary orders.
/// </summary>
public sealed record FoundationOperationalWorkEdge(
    PhysicalTacticalEdgeId Edge,
    FoundationOperationalWorkKind Kind,
    ulong NaturalWeightMicro,
    ulong InvestedWeightMicro);

public sealed record FoundationOperationalWorkShare(
    PhysicalTacticalEdgeId Edge,
    FoundationOperationalWorkKind Kind,
    ulong EffortMicro);

public sealed class FoundationOperationalWorkAllocation
{
    public IReadOnlyList<FoundationOperationalWorkShare> Shares { get; }
    public ulong LandEffortMicro { get; }
    public ulong PortEffortMicro { get; }
    public ulong UnallocatedLandEffortMicro { get; }
    public ulong UnallocatedPortEffortMicro { get; }

    internal FoundationOperationalWorkAllocation(
        FoundationOperationalWorkShare[] shares,
        ulong land, ulong port, ulong unusedLand, ulong unusedPort)
    {
        Shares = Array.AsReadOnly(shares);
        LandEffortMicro = land;
        PortEffortMicro = port;
        UnallocatedLandEffortMicro = unusedLand;
        UnallocatedPortEffortMicro = unusedPort;
    }
}

/// <summary>
/// Deterministic, integer-only proportional work allocation. One group of
/// workers cannot donate its effort to another group. Work never multiplies
/// when a node has multiple incident links. Remainders go to highest
/// fractional claims, then ascending canonical edge ID; this avoids losing
/// effort to rounding, even with very small workforce quantities.
/// </summary>
public static class FoundationOperationalWorkAllocator
{
    public static FoundationOperationalWorkAllocation Allocate(
        ulong landEffortMicro, ulong portEffortMicro,
        IReadOnlyList<FoundationOperationalWorkEdge> incidentEdges)
    {
        ArgumentNullException.ThrowIfNull(incidentEdges);
        var edges = incidentEdges.ToArray();
        if (edges.Any(e => e is null || !e.Edge.IsValid ||
            (e.Kind != FoundationOperationalWorkKind.Land &&
             e.Kind != FoundationOperationalWorkKind.Port) ||
            e.NaturalWeightMicro == 0UL))
            throw new ArgumentException(
                "Every incident crossing must be valid with a positive natural work weight.",
                nameof(incidentEdges));
        if (edges.Select(e => e.Edge).Distinct().Count() != edges.Length)
            throw new ArgumentException(
                "The same physical crossing cannot appear twice.", nameof(incidentEdges));

        var shares = new List<FoundationOperationalWorkShare>(edges.Length);
        foreach (var kind in new[] {
            FoundationOperationalWorkKind.Land,
            FoundationOperationalWorkKind.Port })
        {
            var group = edges.Where(e => e.Kind == kind)
                .OrderBy(e => e.Edge.FineStrategicEdgeId.Value)
                .ToArray();
            ulong available = kind == FoundationOperationalWorkKind.Land
                ? landEffortMicro : portEffortMicro;
            if (group.Length == 0)
                continue;

            BigInteger totalWeight = group.Aggregate(BigInteger.Zero,
                (sum, e) => sum + (BigInteger)e.NaturalWeightMicro +
                    e.InvestedWeightMicro);
            var provisional = group.Select(e =>
            {
                BigInteger numerator = (BigInteger)available *
                    ((BigInteger)e.NaturalWeightMicro + e.InvestedWeightMicro);
                BigInteger floor = BigInteger.DivRem(numerator,
                    totalWeight, out BigInteger remainder);
                return (Edge: e, Floor: checked((ulong)floor),
                    Remainder: remainder);
            }).ToArray();
            ulong allocated = provisional.Aggregate(0UL,
                (sum, p) => checked(sum + p.Floor));
            ulong missing = available - allocated;
            var bonuses = provisional.OrderByDescending(p => p.Remainder)
                .ThenBy(p => p.Edge.Edge.FineStrategicEdgeId.Value)
                .Take(checked((int)missing))
                .Select(p => p.Edge.Edge).ToHashSet();
            foreach (var p in provisional)
                shares.Add(new FoundationOperationalWorkShare(
                    p.Edge.Edge, kind,
                    checked(p.Floor + (bonuses.Contains(p.Edge.Edge) ? 1UL : 0UL))));
        }
        shares.Sort((a, b) => a.Edge.FineStrategicEdgeId.Value
            .CompareTo(b.Edge.FineStrategicEdgeId.Value));
        return new FoundationOperationalWorkAllocation(
            shares.ToArray(), landEffortMicro, portEffortMicro,
            edges.Any(e => e.Kind == FoundationOperationalWorkKind.Land)
                ? 0UL : landEffortMicro,
            edges.Any(e => e.Kind == FoundationOperationalWorkKind.Port)
                ? 0UL : portEffortMicro);
    }
}
