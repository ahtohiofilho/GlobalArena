using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Physical turn-time policy, separate from cargo money. The length of
/// each fine edge is ONE logical crossing, regardless of geography.
/// Explicit natural time is a policy choice, not a conversion of tariffs
/// or of the freight PRICE to seconds, km or money.
/// </summary>
public sealed record FoundationGroundEdgeTravelTimePolicy(
    ulong NaturalCrossingTimeMicro)
{
    public void RequireValid()
    {
        if (NaturalCrossingTimeMicro == 0UL)
            throw new ArgumentOutOfRangeException(nameof(NaturalCrossingTimeMicro));
    }
}

public sealed record FoundationGroundEdgeTravelTimeReading(
    PhysicalTacticalEdgeId Edge,
    ulong NaturalCrossingTimeMicro,
    ulong ImprovedCrossingTimeMicro,
    ulong CommittedInvestmentMoneyMicro,
    ulong NaturalPhysicalFreightMicro,
    ulong ImprovedPhysicalFreightMicro);

/// <summary>
/// R166 ties the PHYSICAL improvement ratio of the single existing
/// infrastructure inventory to walking time:
///
///    T(I) = max(1, ceil(T_natural * C(I) / C(0)))
///
/// C(I) comes from the actual authoritative R112 investment formula,
/// never cargo volume, tolls, ownership or a market price. This is an
/// explicit first candidate time response; no authoritative movement
/// policy is silently replaced.
/// </summary>
public static class FoundationGroundEdgeTravelTimeProjector
{
    public static FoundationGroundEdgeTravelTimeReading Quote(
        WorldState world,
        PhysicalTacticalWorkerMoveEventExecutor canonicalMovement,
        PhysicalTacticalTileId source,
        PhysicalTacticalTileId destination,
        FoundationGroundEdgeTravelTimePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(canonicalMovement);
        ArgumentNullException.ThrowIfNull(policy);
        policy.RequireValid();
        if (!world.Transport.IsInitialized || world.WorldBinding is null ||
            !world.Workers.HasAuthoritativeTacticalLocations)
            throw new InvalidOperationException(
                "Time quote requires the bound canonical physical transport ledger.");
        var edge = canonicalMovement.CheckEdge(source, destination)
            ?? throw new ArgumentException("A time quote requires a real traversed edge.");
        var record = world.Transport.Edges.SingleOrDefault(row => row.Edge == edge)
            ?? throw new InvalidOperationException(
                "Travel edge missing from authoritative physical transport inventory.");
        // CheckEdge already rejects open sea and forbidden ground crossings.
        // Natural crossing time is separate from freight currency units.
        return FromInfrastructure(record, policy);
    }

    /// <summary>
    /// Pure duration conversion for the exact existing crossing ledger row.
    /// Does not read cargo, tariff, owner or settlement revenue.
    /// </summary>
    public static FoundationGroundEdgeTravelTimeReading FromInfrastructure(
        PhysicalTransportEdgeRuntimeRecord record,
        FoundationGroundEdgeTravelTimePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(policy);
        policy.RequireValid();
        ulong natural = record.FreePhysicalCostMicroPerWholeCargoUnit;
        ulong effective =
            FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(record);
        if (natural == 0UL || effective == 0UL || effective > natural)
            throw new InvalidOperationException(
                "Movement cannot improve from a nonpositive or inflated physical freight base.");
        BigInteger numerator = (BigInteger)policy.NaturalCrossingTimeMicro *
            effective;
        BigInteger duration = (numerator + natural - 1UL) / natural;
        if (duration < BigInteger.One) duration = BigInteger.One;
        return new FoundationGroundEdgeTravelTimeReading(record.Edge,
            policy.NaturalCrossingTimeMicro,
            checked((ulong)duration),
            record.CommittedInvestmentMoneyMicro.GetValueOrDefault(),
            natural, effective);
    }

    /// <summary>
    /// The normal R165 Start validation still runs, including the signed
    /// G18 graph and the original worker's actual location and identity.
    /// </summary>
    public static (
        FoundationPhysicalEdgeTravelProgress Progress,
        FoundationGroundEdgeTravelTimeReading Quote)
        Start(
            WorldState world,
            PhysicalWorkerRuntimeRecord worker,
            PhysicalTacticalTileId destination,
            PhysicalTacticalWorkerMoveEventExecutor canonicalMovement,
            FoundationGroundEdgeTravelTimePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(worker);
        if (!world.Workers.Workers.Any(row => row.Id == worker.Id &&
                row == worker))
            throw new InvalidOperationException(
                "A movement time estimate must use a worker from this exact world.");
        var quote = Quote(world, canonicalMovement,
            worker.TacticalTileId ??
                throw new InvalidOperationException("Worker lacks a physical node."),
            destination, policy);
        var travel = FoundationFractionalEdgeTravelProjector.Start(
            worker, destination, quote.ImprovedCrossingTimeMicro,
            canonicalMovement);
        if (travel.Crossing != quote.Edge)
            throw new InvalidOperationException(
                "Canonical travel path and its time quote must refer to the same edge.");
        return (travel, quote);
    }
}
