using System.Numerics;
using GlobalArena.Economy;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Actual reconciled local stock at the close of a physical cycle. Opening
/// stock includes historic inventories; new production is NOT interchangeable
/// with closing inventory once residents have physically consumed food.
/// </summary>
public readonly record struct CanonicalLocalMarketStockFact(
    ulong FineTile, uint Commodity,
    ulong OpeningRaw, ulong ExecutedWorkerProductionRaw,
    ulong ExecutedResidentConsumptionRaw, ulong ClosingAvailableRaw);

/// <summary>
/// Real expedition custody is physically conserved but is not local market
/// inventory. OrderId is an actual journey ID, not a local tile.
/// </summary>
public readonly record struct CanonicalExpeditionCargoStockFact(
    ulong OrderId, uint Commodity, BigInteger ClosingCargoRaw);

/// <summary>
/// A site-level price HYPOTHESIS based on true post-consumption local stock.
/// Explicit proposed retention is not an executed stock reservation.
/// No quantity is shipped, sold, consumed or credited by this quote.
/// </summary>
public sealed record CanonicalLocalAvailabilityMarketCandidate(
    ulong FineTile, uint Commodity, ulong ResidentWorkers,
    ulong PhysicalClosingStockRaw, ulong HypotheticalRetentionRaw,
    ulong HypotheticalOfferedRaw, ulong RawUnitsPerEconomicUnit,
    PopulationPriceQuote Quote)
{
    public bool HasAuthenticClosingLocalStock => true;
    public bool ProvesActualMarketOffer => false;
    public bool ProvesExecutedRetentionOrCommitment => false;
    public bool IncludesExpeditionCargoAsLocalStock => false;
    public bool CreatesMonetaryPurchasingPower => false;
    public bool IsExecutedSale => false;
}

/// <summary>
/// GA-ECON-OFFER-AVAIL-01: end-of-cycle, read-only evidence boundary.
/// Collects only ACTUAL stock/consumption and distinct expedition custody
/// using the independently checked physical and FIFO-accounting witnesses.
/// A separate recorded market-offer action is required to make an actual
/// offer: the legacy kernel currently does not record such an action.
/// </summary>
public sealed class CanonicalPhysicalMarketAvailabilityWitness
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalExecutedPhysicalCycleProvenance _physical;
    private readonly CanonicalPhysicalLotCycleWitness _lots;
    private readonly CanonicalLocalMarketStockFact[] _local;
    private readonly CanonicalExpeditionCargoStockFact[] _cargo;

    public ulong CycleOrdinal => _physical.CycleOrdinal;
    public ulong ClosingMovementTurn => _physical.ClosingMovementTurn;
    public string ClosingWorldHashHex => _physical.ClosingWorldHashHex;
    public ulong TotalRealPopulation => _physical.TotalRealPopulation;
    public IReadOnlyList<CanonicalLocalMarketStockFact> LocalClosingStock { get; }
    public IReadOnlyList<CanonicalExpeditionCargoStockFact> ExpeditionCargo { get; }
    public BigInteger TotalLocalClosingRaw { get; }
    public BigInteger TotalExpeditionCargoRaw { get; }
    public BigInteger TotalExecutedResidentConsumptionRaw { get; }
    public BigInteger TotalExecutedExpeditionConsumptionRaw { get; }

    public bool ProvesExecutedPhysicalProductionAndConsumption => true;
    public bool ProvesLocalClosingStockAndExpeditionCustody => true;
    public bool ProvesActualGoodsWereOfferedForSale => false;
    public bool ProvesOutstandingLocalCommitmentsAreFullyAccounted => false;
    public bool ProvesRealPurchasingPowerN => false;
    public bool AuthorizesActualMarketSettlement => false;
    public bool ModifiesCashStocksSavesOrHash => false;

    private CanonicalPhysicalMarketAvailabilityWitness(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedPhysicalCycleProvenance physical,
        CanonicalPhysicalLotCycleWitness lots)
    {
        _session = session;
        _physical = physical;
        _lots = lots;
        _physical.RequireCurrent(session);
        _lots.RequireCurrent(session);
        _physical.VerifyIndependent();
        _lots.VerifyIndependent();
        if (physical.CycleOrdinal != lots.CycleOrdinal ||
            physical.ClosingMovementTurn != lots.ClosingMovementTurn ||
            !string.Equals(physical.ClosingWorldHashHex,
                lots.ClosingWorldHashHex, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Local stock and expedition custody must come from the same executed cycle.");

        _local = physical.LocalStock.Select(x =>
            new CanonicalLocalMarketStockFact(
                x.FineTile, x.Commodity, x.OpeningRaw,
                x.WorkerProducedRaw, x.ResidentConsumedRaw, x.ClosingRaw))
            .OrderBy(x => x.FineTile).ThenBy(x => x.Commodity).ToArray();
        LocalClosingStock = Array.AsReadOnly(_local);

        // The physical stock row is the one and only authority for the
        // closing local quantity. FIFO accounting is a reconciliation
        // convention; it may not relabel older inventory as new production.
        var localLots = lots.ClosingLots
            .Where(x => x.Custody == CanonicalPhysicalLotCustodyKind.LocalStock)
            .GroupBy(x => (FineTile: x.CustodyId, x.Commodity))
            .ToDictionary(x => x.Key, x => x.Aggregate(
                BigInteger.Zero, (sum, lot) => sum + lot.RawQuantity));
        var positiveLocal = _local
            .Where(x => x.ClosingAvailableRaw > 0UL)
            .ToDictionary(x => (x.FineTile, x.Commodity),
                x => new BigInteger(x.ClosingAvailableRaw));

        if (localLots.Count != positiveLocal.Count ||
            positiveLocal.Any(x =>
                !localLots.TryGetValue(x.Key, out var fromLots) ||
                fromLots != x.Value) ||
            _local.Any(x => (BigInteger)x.OpeningRaw +
                x.ExecutedWorkerProductionRaw -
                x.ExecutedResidentConsumptionRaw !=
                x.ClosingAvailableRaw))
            throw new InvalidOperationException(
                "Executed local inventories must reconcile with the physical lot custody.");

        _cargo = lots.ClosingLots
            .Where(x => x.Custody == CanonicalPhysicalLotCustodyKind.ExpeditionCargo)
            .GroupBy(x => (OrderId: x.CustodyId, x.Commodity))
            .Select(x => new CanonicalExpeditionCargoStockFact(
                x.Key.OrderId, x.Key.Commodity, x.Aggregate(
                    BigInteger.Zero, (sum, lot) => sum + lot.RawQuantity)))
            .OrderBy(x => x.OrderId).ThenBy(x => x.Commodity).ToArray();
        ExpeditionCargo = Array.AsReadOnly(_cargo);

        TotalLocalClosingRaw = _local.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.ClosingAvailableRaw);
        TotalExpeditionCargoRaw = _cargo.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.ClosingCargoRaw);
        TotalExecutedResidentConsumptionRaw = _local.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.ExecutedResidentConsumptionRaw);
        TotalExecutedExpeditionConsumptionRaw = physical.FinalUse
            .Where(x => x.Channel == CanonicalPhysicalFinalUseChannel.ExpeditionRations)
            .Aggregate(BigInteger.Zero, (sum, x) => sum + x.ConsumedRaw);

        var total = TotalExecutedResidentConsumptionRaw +
            TotalExecutedExpeditionConsumptionRaw;
        if (total != physical.ActuallyConsumedRaw ||
            _cargo.Any(x => x.ClosingCargoRaw <= BigInteger.Zero))
            throw new InvalidOperationException(
                "Resident consumption and expedition rations must equal the executed cycle.");
        VerifyIndependent();
    }

    public static CanonicalPhysicalMarketAvailabilityWitness FromCurrentExecutedCycle(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var physical = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var lots = session.CaptureLatestPhysicalLotWitness();
        return new(session, physical, lots);
    }

    /// <summary>
    /// Quotes one existing local stock identity against its ACTUAL local
    /// resident workforce. This is not a connected regional market. The
    /// offer is PROPOSED from uncommitted closing stock; commitments outside
    /// the current ledger are not known, so no executed sale is authorized.
    /// </summary>
    public CanonicalLocalAvailabilityMarketCandidate QuoteLocalCandidate(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId tile, CommodityId commodity,
        ulong hypotheticalRetentionRaw, ulong rawUnitsPerEconomicUnit,
        AbstractPurchasingPowerIndex candidatePurchasingPower,
        PopulationPriceFunction experimentalPriceCurve)
    {
        ArgumentNullException.ThrowIfNull(experimentalPriceCurve);
        RequireCurrent(session);
        if (!commodity.IsValid)
            throw new ArgumentOutOfRangeException(nameof(commodity));
        if (rawUnitsPerEconomicUnit == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawUnitsPerEconomicUnit));

        var at = _local.FirstOrDefault(x =>
            x.FineTile == tile.FineStrategicCellId.Value &&
            x.Commodity == commodity.Value);
        if (at.Commodity == 0U)
            throw new ArgumentException(
                "Local market availability requires an existing physically reconciled stock identity.",
                nameof(commodity));
        if (hypotheticalRetentionRaw > at.ClosingAvailableRaw)
            throw new ArgumentOutOfRangeException(nameof(hypotheticalRetentionRaw),
                "A proposed retention cannot exceed actually closing local stock.");

        var residents = session.WorkersAt(tile);
        if (residents == 0UL)
            throw new InvalidOperationException(
                "A local price hypothesis requires actual resident buyers at this tile.");
        if (session.StockAt(tile, commodity) != at.ClosingAvailableRaw)
            throw new InvalidOperationException(
                "Closing local stock differs from the current physical tile.");

        var candidate = PhysicalOffer.FromAvailable(
            (decimal)at.ClosingAvailableRaw / rawUnitsPerEconomicUnit,
            (decimal)hypotheticalRetentionRaw / rawUnitsPerEconomicUnit);
        var quote = PopulationPriceKernel.Quote(
            residents, candidate, candidatePurchasingPower, experimentalPriceCurve);
        RequireCurrent(session);
        return new CanonicalLocalAvailabilityMarketCandidate(
            at.FineTile, at.Commodity, residents,
            at.ClosingAvailableRaw, hypotheticalRetentionRaw,
            at.ClosingAvailableRaw - hypotheticalRetentionRaw,
            rawUnitsPerEconomicUnit, quote);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Physical market availability belongs to another canonical session.");
        _physical.RequireCurrent(session);
        _lots.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _physical.VerifyIndependent();
        _lots.VerifyIndependent();
        var physicalRows = _physical.LocalStock
            .Select(x => new CanonicalLocalMarketStockFact(
                x.FineTile, x.Commodity, x.OpeningRaw,
                x.WorkerProducedRaw, x.ResidentConsumedRaw, x.ClosingRaw))
            .OrderBy(x => x.FineTile).ThenBy(x => x.Commodity).ToArray();
        var cargoRows = _lots.ClosingLots
            .Where(x => x.Custody == CanonicalPhysicalLotCustodyKind.ExpeditionCargo)
            .GroupBy(x => (OrderId: x.CustodyId, x.Commodity))
            .Select(x => new CanonicalExpeditionCargoStockFact(
                x.Key.OrderId, x.Key.Commodity, x.Aggregate(
                    BigInteger.Zero, (sum, lot) => sum + lot.RawQuantity)))
            .OrderBy(x => x.OrderId).ThenBy(x => x.Commodity).ToArray();
        if (!_local.SequenceEqual(physicalRows) ||
            !_cargo.SequenceEqual(cargoRows) ||
            _local.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ClosingAvailableRaw) != TotalLocalClosingRaw ||
            _cargo.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ClosingCargoRaw) != TotalExpeditionCargoRaw ||
            _local.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ExecutedResidentConsumptionRaw) !=
                TotalExecutedResidentConsumptionRaw ||
            _physical.FinalUse.Where(x =>
                x.Channel == CanonicalPhysicalFinalUseChannel.ExpeditionRations)
                .Aggregate(BigInteger.Zero,
                    (sum, x) => sum + x.ConsumedRaw) !=
                TotalExecutedExpeditionConsumptionRaw)
            throw new InvalidOperationException(
                "Physical availability evidence is no longer consistent with closing stock.");
        RequireCurrent(_session);
    }
}
