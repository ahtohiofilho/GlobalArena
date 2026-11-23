using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Read-only, viewer-scoped evidence of the latest closed temporal sale.
/// A read model, not a second market and not a source of treasury authority.
/// Amounts use the existing exact scaled-money accounting unit.
/// </summary>
public sealed record FoundationViewerTemporalEconomicClose(
    CivilizationId Viewer,
    ulong EconomicCycleOrdinal,
    string CargoUnitCode,
    ulong PhysicalGoodsProducedMicro,
    BigInteger SellerNetScaled,
    BigInteger TaxReceiptsScaled,
    BigInteger OwnerTollIncomeScaled,
    BigInteger TotalCashCreditScaled,
    WorldStateHash CommittedWorldHash);

public static class FoundationViewerTemporalEconomicCloseProjector
{
    /// <summary>
    /// A committed, replay-matched collective receipt is required. Only the
    /// requesting civilization's own financial line is exposed; global seller
    /// lines and other civilizations' tax/toll receipts are never returned.
    /// </summary>
    public static FoundationViewerTemporalEconomicClose Read(
        FoundationTacticalCollectiveReceipt receipt,
        WorldState snapshot,
        CivilizationId viewer)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!viewer.IsValid || !receipt.ReplayMatched ||
            receipt.TemporalEconomicSettlement is not { } sale ||
            receipt.EconomicSettlement is not null ||
            receipt.Turn != sale.Id.Turn ||
            snapshot.Revision != receipt.Revision ||
            new CanonicalWorldStateHasher().Compute(snapshot) != receipt.Hash ||
            !snapshot.Treasury.IsInitialized ||
            snapshot.Treasury.LastSettledEconomicCycleOrdinal !=
                sale.EconomicCycleOrdinal ||
            snapshot.Treasury.LastSettledCargoUnitCode != sale.CargoUnitCode ||
            !snapshot.Civilizations.Civilizations.Contains(viewer))
            throw new InvalidOperationException(
                "Viewer temporal economics require the matching committed journal and treasury.");
        var mine = sale.Credits.SingleOrDefault(c => c.Civilization == viewer)
            ?? throw new InvalidOperationException(
                "Missing real viewer civilization in temporal financial close.");
        if (mine.CashCreditScaled !=
                mine.SellerNetScaled + mine.SellerTaxScaled +
                mine.OwnerTollIncomeScaled ||
            sale.Credits.Aggregate(BigInteger.Zero,
                (sum, row) => sum + row.CashCreditScaled) !=
                sale.TotalCivilizationCreditsScaled ||
            sale.TotalCivilizationCreditsScaled +
                sale.PhysicalFreightScaled != sale.GrossSalesScaled)
            throw new InvalidOperationException(
                "Unbalanced temporal economic receipt cannot be displayed.");
        return new FoundationViewerTemporalEconomicClose(
            viewer, sale.EconomicCycleOrdinal, sale.CargoUnitCode,
            sale.ProducedGoodsMicro, mine.SellerNetScaled,
            mine.SellerTaxScaled, mine.OwnerTollIncomeScaled,
            mine.CashCreditScaled, receipt.Hash);
    }
}
