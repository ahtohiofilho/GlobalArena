using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R76 research-only seller-specific frozen edge quote. Explicit prospective
/// R75 tariff cannot stack on top of an already stored owner tariff.
/// Prior-cycle surcharge is admitted only from the authentic treasury and
/// only when physical base cost and effective reference capacity match.
/// No mutation, market integration, settlement or tariff publication.
/// </summary>
public static class FoundationPhysicalFrozenTariffAdmission
{
    public sealed record Evidence(
        PhysicalTacticalEdgeId Edge,
        ulong PlanningEconomicCycleOrdinal,
        ulong FrozenPhysicalCostMicro,
        ulong CarriedSurchargeMicro,
        ulong ProposedExternalTariffMicro,
        ulong SellerPayableTariffMicro,
        UInt128 SellerTotalCostMicro,
        bool UsedSettledCongestion);

    public static Evidence Inspect(
        WorldState snapshot,
        PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy cargo,
        PhysicalTacticalEdgeId edgeId,
        CivilizationId seller,
        ulong planningEconomicCycleOrdinal,
        ulong tariffReferenceCargoMicro,
        uint ownerBenefitShareNumerator,
        uint ownerBenefitShareDenominator)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(cargo);
        if (!edgeId.IsValid || !seller.IsValid ||
            !snapshot.Treasury.IsInitialized ||
            planningEconomicCycleOrdinal != checked(
                snapshot.Treasury.LastSettledEconomicCycleOrdinal + 1UL))
            throw new InvalidOperationException(
                "R76 needs a real seller and consecutive authoritative cycle.");

        var (roads, scales) =
            FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
                snapshot, incidence);
        var physical = roads.SingleOrDefault(row => row.Edge == edgeId)
            ?? throw new InvalidOperationException(
                "R76 cannot quote an unknown physical crossing.");
        ulong capacity = scales.Single(row => row.Edge == edgeId)
            .CargoMicroAtReferenceLoad;
        var inventory = snapshot.Transport.Edges.Single(row =>
            row.Edge == edgeId);

        // Existing priced edges are intentionally excluded: a prospective
        // tariff MUST NOT be added to an existing commercial toll.
        if (inventory.ExternalTariffMicroPerWholeCargoUnit != 0UL)
            throw new InvalidOperationException(
                "An existing owner tariff requires a replacement protocol, " +
                "not an additional prospective benefit tariff.");

        ulong surcharge = 0UL;
        bool settled = snapshot.Treasury.HasClosedEconomicCycle;
        if (settled)
        {
            if (snapshot.Treasury.LastSettledCargoUnitCode !=
                    cargo.CargoUnitCode)
                throw new InvalidOperationException(
                    "Closed cargo unit disagrees with the planning policy.");
            var historical = snapshot.Treasury.LastSettledFreightQuotes;
            if (historical.Count != roads.Count)
                throw new InvalidOperationException(
                    "A frozen tariff needs a complete settled edge history.");
            var prior = historical.SingleOrDefault(q => q.Edge == edgeId)
                ?? throw new InvalidOperationException(
                    "A frozen tariff needs the exact settled crossing.");
            if (capacity != ulong.MaxValue &&
                prior.FreePhysicalCostMicroPerWholeCargoUnit ==
                    physical.PhysicalCostMicroPerWholeCargoUnit &&
                prior.ReferenceCargoMicro == capacity)
                surcharge =
                    prior.QuotedCongestionSurchargeMicroPerWholeCargoUnit;
        }
        else if (snapshot.Treasury.LastSettledFreightQuotes.Count != 0 ||
                 snapshot.Treasury.LastSettledCargoUnitCode is not null)
            throw new InvalidOperationException(
                "Opening cycle cannot carry an unclosed freight history.");

        var proposed = FoundationPhysicalTariffBenefitReference.Evaluate(
            inventory, seller, tariffReferenceCargoMicro,
            ownerBenefitShareNumerator, ownerBenefitShareDenominator);
        ulong frozen = checked(
            physical.PhysicalCostMicroPerWholeCargoUnit + surcharge);
        UInt128 total = (UInt128)frozen +
            proposed.SellerPayableTariffMicro;
        return new Evidence(edgeId, planningEconomicCycleOrdinal,
            frozen, surcharge, proposed.ProposedExternalTariffMicro,
            proposed.SellerPayableTariffMicro, total, settled);
    }
}
