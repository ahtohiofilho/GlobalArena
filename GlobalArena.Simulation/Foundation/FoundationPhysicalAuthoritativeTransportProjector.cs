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
                edge.FreePhysicalCostMicroPerWholeCargoUnit,
                edge.ExternalTariffMicroPerWholeCargoUnit));
            // The only capital ledger is the already-hashed, already-
            // debited money committed to this exact physical crossing.
            // All land/coast crossings follow the SAME diminishing-return
            // function; no "port" object or separate investment economy.
            scales.Add(new FoundationPhysicalEdgeCarryingScale(
                edge.Edge, EffectiveReferenceCargo(edge)));
        }
        if (known.Count != 0)
            throw new InvalidOperationException(
                "Authoritative physical transport inventory omits real crossings.");
        return (Array.AsReadOnly(roads.ToArray()),
            Array.AsReadOnly(scales.ToArray()));
    }

    /// <summary>
    /// R48 EXPERIMENTAL efficiency contract, not approved final balance:
    /// K_effective = K_natural * (1 + I / (B + I)).
    /// B is the immutable, real monetary construction quote; I is the
    /// authenticated capital ledger already debited from civilization cash.
    /// This rational form needs no floating point, has strictly decreasing
    /// marginal returns in I, and keeps all rates deterministic. Under the
    /// current one-construction funding gate 0 <= I <= B, so capacity gains
    /// range from zero to 50%. Further funding stages need a separate gate.
    /// Sea is physically unbounded permanently and rejects investment;
    /// it never receives a finite artificial carrying capacity.
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
        if (baseQuote == 0UL || invested > baseQuote)
            throw new InvalidOperationException(
                "Investment must respect the already authorized construction budget.");

        BigInteger numerator = (BigInteger)natural * invested;
        BigInteger denominator = (BigInteger)baseQuote + invested;
        // Exact integer truncation is deterministic; not a fictional
        // absolute shipment cap. Reject overflow, never silently clamp.
        BigInteger capacity = (BigInteger)natural + numerator / denominator;
        if (capacity >= ulong.MaxValue)
            throw new OverflowException(
                "A finite built crossing cannot use the unbounded-sea marker.");
        return checked((ulong)capacity);
    }
}
