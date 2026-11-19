using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R75 RESEARCH ONLY: reference for a prospective owner tariff proportional
/// to the congestion reduction created by actual monetary investment.
/// Explicit reference cargo and revenue share are POLICY INPUTS, not
/// inferred from the settled treasury's surcharge or from ownership.
/// No world update, tariff publication, credit, sale or waterfilling.
/// </summary>
public static class FoundationPhysicalTariffBenefitReference
{
    public sealed record Quote(
        PhysicalTacticalEdgeId Edge,
        ulong ReferenceCargoMicro,
        ulong NaturalCapacityMicro,
        ulong ImprovedCapacityMicro,
        ulong NaturalCongestedCostMicro,
        ulong ImprovedCongestedCostMicro,
        ulong AvoidedPhysicalCostMicro,
        ulong ProposedExternalTariffMicro,
        ulong SellerPayableTariffMicro);

    public static Quote Evaluate(
        PhysicalTransportEdgeRuntimeRecord edge,
        CivilizationId seller,
        ulong referenceCargoMicro,
        uint ownerBenefitShareNumerator,
        uint ownerBenefitShareDenominator)
    {
        ArgumentNullException.ThrowIfNull(edge);
        if (!edge.Edge.IsValid || !seller.IsValid ||
            referenceCargoMicro == 0UL ||
            ownerBenefitShareDenominator == 0U ||
            ownerBenefitShareNumerator > ownerBenefitShareDenominator)
            throw new ArgumentException(
                "Require a real crossing, seller, positive reference cargo " +
                "and an explicitly bounded tariff share.");

        ulong natural = edge.CargoMicroAtReferenceLoad;
        if (natural == 0UL ||
            natural == FoundationPhysicalEndogenousCongestionFeedback
                .UnboundedNaturalCargoCapacity)
            throw new InvalidOperationException(
                "Open sea is not an investable tariff-bearing crossing.");
        if (edge.FreePhysicalCostMicroPerWholeCargoUnit == 0UL)
            throw new InvalidOperationException("Physical cost must be positive.");

        ulong improved = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(edge);
        if (improved < natural)
            throw new InvalidOperationException(
                "Investment cannot reduce physical carrying capacity.");

        ulong withoutInvestment =
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                edge.FreePhysicalCostMicroPerWholeCargoUnit,
                referenceCargoMicro, natural);
        ulong withInvestment =
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                edge.FreePhysicalCostMicroPerWholeCargoUnit,
                referenceCargoMicro, improved);
        if (withInvestment > withoutInvestment)
            throw new InvalidOperationException(
                "Improved physical capacity cannot raise quoted congestion.");

        ulong benefit = withoutInvestment - withInvestment;
        BigInteger tariffWide = new BigInteger(benefit) *
            ownerBenefitShareNumerator / ownerBenefitShareDenominator;
        ulong candidate = checked((ulong)tariffWide);

        // No automatic toll from title alone, and never collect on
        // unowned/natural edges. Existing stored toll is NOT overwritten.
        bool titledAndInvested =
            edge.OwnershipKind == PhysicalTransportOwnershipKind.Owned &&
            edge.InfrastructureOwner is { IsValid: true } &&
            edge.CommittedInvestmentMoneyMicro.GetValueOrDefault() > 0UL;
        if (!titledAndInvested)
            candidate = 0UL;

        return new Quote(edge.Edge, referenceCargoMicro, natural, improved,
            withoutInvestment, withInvestment, benefit, candidate,
            titledAndInvested && edge.InfrastructureOwner == seller
                ? 0UL : candidate);
    }
}
