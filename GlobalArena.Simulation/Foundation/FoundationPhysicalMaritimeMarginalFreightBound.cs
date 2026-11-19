using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R66 RESEARCH-ONLY proof boundary between physical sea-leg pruning and
/// frozen owner-specific marginal freight. This does not execute routing,
/// clear markets, quote commodity prices, read preview congestion or
/// publish settlement.
/// All values use MICRO-MONEY PER WHOLE CARGO UNIT for the SAME snapshot.
/// Caller must independently certify terrestrial access rate, land upper
/// bound and exact coast-edge identity from that same cycle and seller.
/// </summary>
public static class FoundationPhysicalMaritimeMarginalFreightBound
{
    public sealed record Decision(
        ulong? FeasibleLandFreightMicroPerWholeCargoUnit,
        UInt128 MaritimeLowerBoundMicroPerWholeCargoUnit,
        bool CanExcludePair,
        ulong EmbarkCoastRateMicroPerWholeCargoUnit,
        ulong DisembarkCoastRateMicroPerWholeCargoUnit);

    /// <summary>
    /// Takes existing cost records projected for the CURRENT cycle from
    /// authoritative transport or the settled prior-cycle freight state.
    /// The record's PhysicalCost already includes any lagged congestion
    /// surcharge: do not add it a second time.
    ///
    /// Positive maritime minimum is a proven bound on the COMPLETE
    /// pair-dependent sea quote, never a per-edge discount. The R54
    /// integer-micro quote always stays at least one micro-money.
    /// If no feasible land upper bound exists, NO pair is excluded.
    /// </summary>
    public static Decision Evaluate(
        FoundationPhysicalRoutingEdgeCost embark,
        FoundationPhysicalRoutingEdgeCost disembark,
        CivilizationId seller,
        ulong landToEmbarkFreightMicroPerWholeCargoUnit,
        ulong landFromDisembarkFreightMicroPerWholeCargoUnit,
        ulong? feasibleLandFreightMicroPerWholeCargoUnit,
        ulong positiveSeaLegMinimumMicroPerWholeCargoUnit = 1UL)
    {
        ArgumentNullException.ThrowIfNull(embark);
        ArgumentNullException.ThrowIfNull(disembark);
        if (!seller.IsValid || !embark.Edge.IsValid ||
            !disembark.Edge.IsValid || embark.Edge == disembark.Edge ||
            embark.Edge.FineGoldbergParameters !=
                disembark.Edge.FineGoldbergParameters ||
            positiveSeaLegMinimumMicroPerWholeCargoUnit == 0UL)
            throw new ArgumentException(
                "The quote needs one valid seller, two distinct real " +
                "coastal crossings and a strictly positive whole-leg minimum.");

        ulong EmbarkRate(FoundationPhysicalRoutingEdgeCost edge)
        {
            if (edge.PhysicalCostMicroPerWholeCargoUnit == 0UL ||
                !Enum.IsDefined(edge.OwnershipKind) ||
                (edge.OwnershipKind ==
                    FoundationInfrastructureOwnershipKind.Owned
                    ? edge.InfrastructureOwner is not { IsValid: true }
                    : edge.InfrastructureOwner.HasValue ||
                      edge.ExternalTariffMicroPerWholeCargoUnit != 0UL))
                throw new ArgumentException(
                    "The cycle quote violates the canonical freight " +
                    "ownership, tariff or positive-cost contract.");
            // Owner pays physical transport but is exempt from only
            // the external tariff, consistent with RouteFrom / BuildGraph.
            ulong toll = edge.InfrastructureOwner.HasValue &&
                edge.InfrastructureOwner.Value != seller
                ? edge.ExternalTariffMicroPerWholeCargoUnit : 0UL;
            return checked(edge.PhysicalCostMicroPerWholeCargoUnit + toll);
        }

        ulong first = EmbarkRate(embark);
        ulong second = EmbarkRate(disembark);
        UInt128 lower =
            (UInt128)landToEmbarkFreightMicroPerWholeCargoUnit +
            first + positiveSeaLegMinimumMicroPerWholeCargoUnit +
            second + landFromDisembarkFreightMicroPerWholeCargoUnit;
        return new Decision(
            feasibleLandFreightMicroPerWholeCargoUnit, lower,
            feasibleLandFreightMicroPerWholeCargoUnit is ulong land &&
                lower > land, first, second);
    }
}
