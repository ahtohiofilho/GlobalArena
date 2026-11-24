using System.Numerics;
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
                EffectiveUnitFreightCost(edge),
                edge.ExternalTariffMicroPerWholeCargoUnit));
            // Compatibility field retained for old historical records;
            // fixed-unit routing has no capacity-based congestion.
            // In particular, never compute investment-based K during
            // authoritative transport projection.
            scales.Add(new FoundationPhysicalEdgeCarryingScale(
                edge.Edge, edge.CargoMicroAtReferenceLoad));
        }
        if (known.Count != 0)
            throw new InvalidOperationException(
                "Authoritative physical transport inventory omits real crossings.");
        return (Array.AsReadOnly(roads.ToArray()),
            Array.AsReadOnly(scales.ToArray()));
    }

    /// <summary>
    /// R112 fixed unit freight: the natural cost belongs to one logical
    /// crossing, never to geographic kilometers or current cargo volume.
    /// Settled money investment continuously reduces that cost according to
    /// c(I)=c0/sqrt(1+I/B), with B the canonical construction quote.
    /// Exact integer arithmetic and a one-micro minimum keep rates positive.
    /// </summary>
    public static ulong EffectiveUnitFreightCost(
        PhysicalTransportEdgeRuntimeRecord edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return ProjectUnitFreightCost(edge, 0UL);
    }

    // Hypothetical additional capital, not a command or a mutation.
    // Uses exactly the same integer cost function as committed freight.
    public static ulong ProjectUnitFreightCost(
        PhysicalTransportEdgeRuntimeRecord edge, ulong additionalMoneyMicro)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ulong natural = edge.FreePhysicalCostMicroPerWholeCargoUnit;
        if (natural == 0UL)
            throw new InvalidOperationException(
                "Natural unit freight cost must remain positive.");
        ulong investment = checked(edge.CommittedInvestmentMoneyMicro.GetValueOrDefault() + additionalMoneyMicro);
        if (investment == 0UL)
            return natural;
        if (edge.CargoMicroAtReferenceLoad ==
                FoundationPhysicalEndogenousCongestionFeedback.UnboundedNaturalCargoCapacity)
            throw new InvalidOperationException(
                "Open-sea edges cannot contain direct infrastructure investments.");
        ulong reference = edge.NaturalConstructionCostMoneyMicro ??
            throw new InvalidOperationException(
                "An invested edge must retain its natural construction quote.");
        if (reference == 0UL)
            throw new InvalidOperationException(
                "The construction reference must be positive.");
        BigInteger numerator = (BigInteger)natural * natural * reference;
        BigInteger divisor = (BigInteger)reference + investment;
        BigInteger integerCost = IntegerSquareRoot(numerator / divisor);
        return integerCost < BigInteger.One ? 1UL : checked((ulong)integerCost);
    }

    /// <summary>
    /// R49 provisional, unbounded-with-respect-to-investment rule:
    /// K = Knatural + floor(Knatural * (sqrt(1 + 3*I/B) - 1)/2).
    /// This expression is evaluated EXACTLY with scaled integer square
    /// roots. I is real settled investment; B the original base quote.
    /// I=B yields a 50% improvement matching R48, while larger I keeps
    /// increasing capacity with diminishing returns and no game-stage cap.
    /// Since the integer radicand increases with I and the square root is
    /// monotone, additional funding CANNOT reduce throughput due to
    /// rounding. No floating point or port-specific rules.
    /// </summary>
    public static ulong EffectiveReferenceCargo(
        PhysicalTransportEdgeRuntimeRecord edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ulong natural = edge.CargoMicroAtReferenceLoad;
        if (natural == 0UL)
            throw new InvalidOperationException(
                "An edge must have positive natural carrying capacity.");
        if (natural == FoundationPhysicalEndogenousCongestionFeedback
                .UnboundedNaturalCargoCapacity)
        {
            if (edge.CommittedInvestmentMoneyMicro.GetValueOrDefault() != 0UL ||
                edge.OwnershipKind != PhysicalTransportOwnershipKind.NoInfrastructure ||
                edge.InfrastructureOwner.HasValue ||
                edge.ExternalTariffMicroPerWholeCargoUnit != 0UL)
                throw new InvalidOperationException(
                    "Unbounded natural sea cannot acquire infrastructure, ownership or tariffs.");
            return natural;
        }

        ulong invested = edge.CommittedInvestmentMoneyMicro ?? 0UL;
        if (invested == 0UL)
            return natural;
        ulong baseQuote = edge.NaturalConstructionCostMoneyMicro
            ?? throw new InvalidOperationException(
                "Capital-bearing crossings need a verified construction quote.");
        if (baseQuote == 0UL)
            throw new InvalidOperationException(
                "Investment requires a positive monetary construction reference.");

        BigInteger amount = new BigInteger(invested);
        BigInteger quote = new BigInteger(baseQuote);
        BigInteger baseCapacity = new BigInteger(natural);
        // floor(sqrt(floor(K^2 * (B + 3I) / B))) equals
        // floor(K * sqrt(1 + 3I/B)); exact with integer arithmetic.
        BigInteger scaledRoot = IntegerSquareRoot(
            baseCapacity * baseCapacity * (quote + 3 * amount) / quote);
        BigInteger improvement = (scaledRoot - baseCapacity) / 2;
        BigInteger capacity = baseCapacity + improvement;
        if (capacity >= ulong.MaxValue)
            throw new OverflowException(
                "Finite improved crossing exceeds the available canonical capacity range.");
        return checked((ulong)capacity);
    }

    /// <summary>
    /// Floor(sqrt(value)); exact for all nonnegative BigInteger inputs.
    /// Uses no float, random state, rounding mode or platform-dependent Math.
    /// The starting bound is derived from bit length, Newton converges
    /// monotonically downward to the exact integer square root.
    /// </summary>
    internal static BigInteger IntegerSquareRoot(BigInteger value)
    {
        if (value.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value.IsZero)
            return BigInteger.Zero;
        int bits = checked((int)value.GetBitLength());
        BigInteger x = BigInteger.One << ((bits + 1) / 2);
        while (true)
        {
            BigInteger next = (x + value / x) >> 1;
            if (next >= x)
                return x;
            x = next;
        }
    }
}
