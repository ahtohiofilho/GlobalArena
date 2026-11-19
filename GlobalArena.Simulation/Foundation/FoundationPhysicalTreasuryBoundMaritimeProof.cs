using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R69 candidate-only proof boundary: the authoritative WorldState.Treasury
/// supplies the economic-cycle ordinal, cargo-unit code and exact complete
/// settled freight memory. No caller-provided historical quote state or
/// guessed cycle is allowed. The real commodity is converted with the same
/// declared physical cargo policy used by the delivery auditor.
/// This is not a route choice, market clearing or sale.
/// </summary>
public static class FoundationPhysicalTreasuryBoundMaritimeProof
{
    public sealed record Evidence(
        FoundationPhysicalWorldBoundMaritimeFreightProof.Evidence Route,
        CommodityId Commodity,
        ulong GoodsMicro,
        ulong CargoMicro,
        string CargoUnitCode,
        ulong PlanningEconomicCycleOrdinal,
        ulong PreviousSettledEconomicCycleOrdinal,
        bool UsedVerifiedSettledFreight);

    public static Evidence Evaluate(
        WorldState snapshot,
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy cargoPolicy,
        CommodityId commodity,
        ulong goodsMicro,
        CivilizationId seller,
        PhysicalTacticalTileId source,
        PhysicalTacticalTileId destination,
        PhysicalTacticalEdgeId embarkEdge,
        PhysicalTacticalEdgeId disembarkEdge,
        ulong planningEconomicCycleOrdinal)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(cargoPolicy);
        if (!commodity.IsValid || goodsMicro == 0UL ||
            planningEconomicCycleOrdinal == 0UL ||
            !snapshot.Treasury.IsInitialized)
            throw new ArgumentException(
                "Economic freight proof needs an actual commodity, positive " +
                "goods, valid treasury and planning cycle.");

        ulong lastSettled = snapshot.Treasury.LastSettledEconomicCycleOrdinal;
        if (planningEconomicCycleOrdinal != checked(lastSettled + 1UL))
            throw new InvalidOperationException(
                "The current candidate must immediately follow the last " +
                "authoritatively CLOSED economic cycle, without stale or future quotations.");

        // Physical cargo conversion is declared per commodity, not
        // assumed equal for food, minerals or any other goods.
        // CargoMicro rejects unknown items and nonrepresentable amounts.
        ulong cargo = cargoPolicy.CargoMicro(commodity, goodsMicro);
        if (cargo == 0UL)
            throw new InvalidOperationException(
                "An economic trade cannot have zero comparable cargo.");

        FoundationPhysicalLaggedFreightQuoteState? closed = null;
        if (lastSettled != 0UL)
        {
            if (!snapshot.Treasury.HasClosedEconomicCycle ||
                snapshot.Treasury.LastSettledCargoUnitCode !=
                    cargoPolicy.CargoUnitCode)
                throw new InvalidOperationException(
                    "The settled freight cargo-unit domain differs from " +
                    "the commodity conversion policy.");

            // These records were admitted only through the treasury's
            // trusted, audited, consecutive settlement boundary. They
            // are never copied from a preview or arbitrary caller state.
            var entries = snapshot.Treasury.LastSettledFreightQuotes
                .Select(q => new FoundationPhysicalLaggedFreightEdgeState(
                    q.Edge, q.FreePhysicalCostMicroPerWholeCargoUnit,
                    q.ReferenceCargoMicro,
                    q.QuotedCongestionSurchargeMicroPerWholeCargoUnit))
                .ToArray();
            closed = new FoundationPhysicalLaggedFreightQuoteState(
                lastSettled, entries, hasSettledProvenance: true);
        }
        else if (snapshot.Treasury.HasClosedEconomicCycle ||
                 snapshot.Treasury.LastSettledFreightQuotes.Count != 0 ||
                 snapshot.Treasury.LastSettledCargoUnitCode is not null)
            throw new InvalidOperationException(
                "Opening-cycle freight cannot claim historical settlement.");

        var route = FoundationPhysicalWorldBoundMaritimeFreightProof.Evaluate(
            snapshot, world, incidence, seller, source, destination,
            embarkEdge, disembarkEdge, previousClosedCycleQuotes: closed);
        return new Evidence(route, commodity, goodsMicro, cargo,
            cargoPolicy.CargoUnitCode, planningEconomicCycleOrdinal,
            lastSettled, closed is not null);
    }
}
