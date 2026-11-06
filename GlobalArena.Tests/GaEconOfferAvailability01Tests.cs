using System.Numerics;
using GlobalArena.Economy;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-OFFER-AVAIL-01: closing LOCAL stocks, consumed resident food
/// and expedition rations must reconcile; an unexecuted market offer
/// cannot be certified by an experimental abstract-pricing calculation.
/// </summary>
public sealed class GaEconOfferAvailability01Tests
{
    private const ulong Unit = 1_000_000UL;
    private static decimal ExperimentalPrice(decimal ratio) => 2m / (1m + ratio);

    private static CanonicalPhysicalTacticalSession ClosedLegacy()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 5; i++)
            Assert.Null(session.AdvanceMovementTurn());
        Assert.NotNull(session.AdvanceMovementTurn());
        return session;
    }

    private static CanonicalPhysicalTacticalSession Cash()
    {
        return CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);
    }

    private static (PhysicalTacticalTileId Tile, CommodityId Commodity)
        ActualPositiveLocalStock(CanonicalPhysicalTacticalSession session)
    {
        var stock = session.State.Stocks
            .Where(x => x.RawQuantity > 0UL &&
                session.WorkersAt(x.TileId) > 0UL)
            .OrderBy(x => x.TileId.FineStrategicCellId.Value)
            .ThenBy(x => x.Commodity.Value)
            .First();
        return (stock.TileId, stock.Commodity);
    }

    private static CanonicalLocalAvailabilityMarketCandidate Quote(
        CanonicalPhysicalMarketAvailabilityWitness proof,
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId tile, CommodityId commodity,
        ulong retained = 0UL,
        AbstractPurchasingPowerIndex? power = null) =>
        proof.QuoteLocalCandidate(session, tile, commodity,
            retained, Unit, power ?? AbstractPurchasingPowerIndex.Neutral,
            ExperimentalPrice);

    [Fact]
    public void UnclosedCycleCannotInventAvailableStockOrOffer()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalMarketAvailabilityWitness.FromCurrentExecutedCycle(session));
        for (var i = 0; i < 5; i++) session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalMarketAvailabilityWitness.FromCurrentExecutedCycle(session));
    }

    [Fact]
    public void RealResidentFoodUseIsDeductedBeforeAnyMarketAvailability()
    {
        var session = ClosedLegacy();
        var proof = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        var executed = session.CaptureLatestExecutedPhysicalCycleProvenance();
        proof.VerifyIndependent();

        Assert.Equal(executed.CycleOrdinal, proof.CycleOrdinal);
        Assert.Equal(executed.ClosingWorldHashHex, proof.ClosingWorldHashHex);
        Assert.Equal(executed.TotalRealPopulation, proof.TotalRealPopulation);
        Assert.Equal(executed.LocalStock.Count, proof.LocalClosingStock.Count);
        Assert.Equal(executed.ActuallyConsumedRaw,
            proof.TotalExecutedResidentConsumptionRaw +
            proof.TotalExecutedExpeditionConsumptionRaw);
        Assert.Contains(proof.LocalClosingStock, row =>
            row.Commodity == 1U && row.ExecutedResidentConsumptionRaw > 0UL);

        foreach (var row in proof.LocalClosingStock)
        {
            Assert.Equal((BigInteger)row.OpeningRaw +
                row.ExecutedWorkerProductionRaw -
                row.ExecutedResidentConsumptionRaw,
                new BigInteger(row.ClosingAvailableRaw));
        }
        Assert.Equal(proof.LocalClosingStock.Aggregate(
            BigInteger.Zero, (s, x) => s + x.ClosingAvailableRaw),
            proof.TotalLocalClosingRaw);
        Assert.Empty(proof.ExpeditionCargo);

        Assert.True(proof.ProvesExecutedPhysicalProductionAndConsumption);
        Assert.True(proof.ProvesLocalClosingStockAndExpeditionCustody);
        Assert.False(proof.ProvesActualGoodsWereOfferedForSale);
        Assert.False(proof.ProvesOutstandingLocalCommitmentsAreFullyAccounted);
        Assert.False(proof.ProvesRealPurchasingPowerN);
        Assert.False(proof.AuthorizesActualMarketSettlement);
        Assert.False(proof.ModifiesCashStocksSavesOrHash);
    }

    [Fact]
    public void ActualClosingStockNotProducedGrossQuantityIsTheCandidateBase()
    {
        var session = ClosedLegacy();
        var proof = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        var (tile, commodity) = ActualPositiveLocalStock(session);
        var row = proof.LocalClosingStock.Single(x =>
            x.FineTile == tile.FineStrategicCellId.Value &&
            x.Commodity == commodity.Value);
        var candidate = Quote(proof, session, tile, commodity);

        Assert.Equal(row.ClosingAvailableRaw, candidate.PhysicalClosingStockRaw);
        Assert.Equal(row.ClosingAvailableRaw, candidate.HypotheticalOfferedRaw);
        Assert.Equal(0UL, candidate.HypotheticalRetentionRaw);
        Assert.Equal(session.WorkersAt(tile), candidate.ResidentWorkers);
        Assert.Equal((decimal)candidate.ResidentWorkers,
            candidate.Quote.EffectiveDemand);
        Assert.Equal((decimal)row.ClosingAvailableRaw / Unit,
            candidate.Quote.Offered);
        Assert.Equal(candidate.Quote.Price * candidate.Quote.EffectiveDemand,
            candidate.Quote.ReferenceRevenue);
        Assert.True(candidate.HasAuthenticClosingLocalStock);
        Assert.False(candidate.ProvesActualMarketOffer);
        Assert.False(candidate.ProvesExecutedRetentionOrCommitment);
        Assert.False(candidate.IncludesExpeditionCargoAsLocalStock);
        Assert.False(candidate.CreatesMonetaryPurchasingPower);
        Assert.False(candidate.IsExecutedSale);
        Assert.False(candidate.Quote.IsExecutedSettlement);
    }

    [Fact]
    public void ExplicitRetentionAndDemandPressureCannotCapAvailableInventory()
    {
        var session = ClosedLegacy();
        var proof = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        var (tile, commodity) = ActualPositiveLocalStock(session);
        var all = Quote(proof, session, tile, commodity);
        var retainedRaw = all.PhysicalClosingStockRaw / 2UL;
        var retained = Quote(proof, session, tile, commodity, retainedRaw);
        var zero = Quote(proof, session, tile, commodity,
            all.PhysicalClosingStockRaw);

        Assert.Equal(all.PhysicalClosingStockRaw,
            retained.PhysicalClosingStockRaw);
        Assert.Equal(all.PhysicalClosingStockRaw - retainedRaw,
            retained.HypotheticalOfferedRaw);
        Assert.Equal((decimal)retained.HypotheticalOfferedRaw / Unit,
            retained.Quote.Offered);
        Assert.Equal(0UL, zero.HypotheticalOfferedRaw);
        Assert.False(zero.Quote.HasOffer);
        Assert.Equal(0m, zero.Quote.Price);

        var tiny = Quote(proof, session, tile, commodity,
            power: AbstractPurchasingPowerIndex.FromRelativeCapacity(
                0.000000000001m));
        Assert.Equal(all.PhysicalClosingStockRaw, tiny.HypotheticalOfferedRaw);
        Assert.True(tiny.Quote.Offered > tiny.Quote.EffectiveDemand);
        Assert.Equal(tiny.Quote.Price * tiny.Quote.EffectiveDemand,
            tiny.Quote.ReferenceRevenue);
        Assert.False(tiny.IsExecutedSale);
    }

    [Fact]
    public void CashModeWalletsDoNotSetPotentialPhysicalAvailability()
    {
        var session = Cash();
        Assert.NotNull(session.AdvanceMovementTurn());
        var proof = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        var saved = session.ExportSaveJson();
        var hash = session.CurrentHash;
        var accounts = session.State.FoodCash!.Accounts.ToArray();
        var receipts = session.State.FoodCash.Receipts.ToArray();

        var (tile, commodity) = ActualPositiveLocalStock(session);
        var candidate = Quote(proof, session, tile, commodity,
            power: AbstractPurchasingPowerIndex.FromRelativeCapacity(2m));
        Assert.Equal(session.WorkersAt(tile) * 2m,
            candidate.Quote.EffectiveDemand);
        Assert.Equal(session.StockAt(tile, commodity),
            candidate.HypotheticalOfferedRaw);
        Assert.Equal(saved, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(accounts, session.State.FoodCash.Accounts.ToArray());
        Assert.Equal(receipts, session.State.FoodCash.Receipts.ToArray());
    }

    [Fact]
    public void ExpeditionCargoAndRationsCannotBeSoldAgainAsLocalStock()
    {
        var session = Cash();
        var start = session.InitialTile;
        session.ReassignWorkers(start, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 2UL);
        session.ReassignWorkers(start, EconomicActivityKind.Mining,
            EconomicActivityKind.Agriculture, 2UL);
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        var food = session.StockAt(start, new CommodityId(1U));
        Assert.True(food > 0UL);
        var neighbors = session.AdjacentWorkable(start).ToHashSet();
        var destination = neighbors.SelectMany(session.AdjacentWorkable)
            .Where(x => x != start && !neighbors.Contains(x))
            .Distinct().OrderBy(x => x.FineStrategicCellId.Value).First();
        var trip = session.OrderProvisionedLongDistanceTransit(start,
            destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL, Math.Min(food, 2_000_000UL));
        Assert.NotNull(session.AdvanceMovementTurn());

        var proof = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        var physical = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var lot = session.CaptureLatestPhysicalLotWitness();
        var cargoByTrip = lot.ClosingLots.Where(x =>
                x.Custody == CanonicalPhysicalLotCustodyKind.ExpeditionCargo &&
                x.CustodyId == trip.OrderId)
            .Aggregate(BigInteger.Zero, (s, x) => s + x.RawQuantity);
        Assert.Contains(proof.ExpeditionCargo, x =>
            x.OrderId == trip.OrderId &&
            x.ClosingCargoRaw == cargoByTrip);
        Assert.Equal(cargoByTrip, proof.ExpeditionCargo.Where(
            x => x.OrderId == trip.OrderId)
            .Aggregate(BigInteger.Zero, (s, x) => s + x.ClosingCargoRaw));
        Assert.Equal(physical.ActuallyConsumedRaw,
            proof.TotalExecutedResidentConsumptionRaw +
            proof.TotalExecutedExpeditionConsumptionRaw);
        Assert.True(proof.TotalExecutedExpeditionConsumptionRaw > 0);
        Assert.Equal(session.State.Stocks.Aggregate(
            BigInteger.Zero, (s, x) => s + x.RawQuantity),
            proof.TotalLocalClosingRaw);
        Assert.False(proof.ProvesActualGoodsWereOfferedForSale);
        proof.VerifyIndependent();
    }

    [Fact]
    public void OldStockRemainsRealAndIsNotRelabelledAsFreshProduction()
    {
        var session = ClosedLegacy();
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        var proof = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        Assert.Equal(2UL, proof.CycleOrdinal);
        Assert.Equal(12UL, proof.ClosingMovementTurn);
        Assert.Contains(proof.LocalClosingStock, x => x.OpeningRaw > 0UL);
        Assert.All(proof.LocalClosingStock, x =>
            Assert.Equal((BigInteger)x.OpeningRaw +
                x.ExecutedWorkerProductionRaw -
                x.ExecutedResidentConsumptionRaw,
                new BigInteger(x.ClosingAvailableRaw)));
        proof.VerifyIndependent();
    }

    [Fact]
    public void SaveReplayRestoresExactlySameLocalAndExpeditionFacts()
    {
        var session = ClosedLegacy();
        var original = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        var save = session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var replay = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(restored);

        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(session.CurrentHash, restored.CurrentHash);
        Assert.Equal(original.ClosingWorldHashHex, replay.ClosingWorldHashHex);
        Assert.Equal(original.LocalClosingStock.ToArray(),
            replay.LocalClosingStock.ToArray());
        Assert.Equal(original.ExpeditionCargo.ToArray(),
            replay.ExpeditionCargo.ToArray());
        Assert.Equal(original.TotalLocalClosingRaw, replay.TotalLocalClosingRaw);
        Assert.Throws<InvalidOperationException>(() =>
            original.RequireCurrent(restored));
        replay.VerifyIndependent();
    }

    [Fact]
    public void NextNonClosingTurnAndWorkerActionInvalidateReceipt()
    {
        var session = ClosedLegacy();
        var before = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        Assert.Null(session.AdvanceMovementTurn());
        Assert.Throws<InvalidOperationException>(before.VerifyIndependent);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalMarketAvailabilityWitness
                .FromCurrentExecutedCycle(session));

        var changed = ClosedLegacy();
        var receipt = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(changed);
        changed.ReassignWorkers(changed.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            receipt.RequireCurrent(changed));
    }

    [Fact]
    public void MissingCommodityInvalidInputsAndForeignSessionFailClosed()
    {
        var session = ClosedLegacy();
        var proof = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        var (tile, commodity) = ActualPositiveLocalStock(session);
        var gross = session.StockAt(tile, commodity);

        Assert.Throws<ArgumentException>(() =>
            Quote(proof, session, tile, new CommodityId(999U)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Quote(proof, session, tile, commodity, gross + 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            proof.QuoteLocalCandidate(session, tile, commodity, 0UL, 0UL,
                AbstractPurchasingPowerIndex.Neutral, ExperimentalPrice));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            proof.QuoteLocalCandidate(session, tile, commodity, 0UL, Unit,
                default, ExperimentalPrice));
        Assert.Throws<ArgumentNullException>(() =>
            proof.QuoteLocalCandidate(session, tile, commodity, 0UL, Unit,
                AbstractPurchasingPowerIndex.Neutral, null!));

        var foreign = ClosedLegacy();
        Assert.Throws<InvalidOperationException>(() =>
            proof.RequireCurrent(foreign));
    }

    [Fact]
    public void RepeatedQuotesNeverModifyCashStocksCyclesOrSaves()
    {
        var session = ClosedLegacy();
        var proof = CanonicalPhysicalMarketAvailabilityWitness
            .FromCurrentExecutedCycle(session);
        var (tile, commodity) = ActualPositiveLocalStock(session);
        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();
        var stocks = session.State.Stocks.ToArray();
        var cycles = session.State.Cycles.ToArray();
        var calls = 0;

        decimal Curve(decimal x)
        {
            calls++;
            return 2m / (1m + x);
        }
        for (var i = 0; i < 3; i++)
        {
            var quote = proof.QuoteLocalCandidate(
                session, tile, commodity, 0UL, Unit,
                AbstractPurchasingPowerIndex.Neutral, Curve);
            Assert.False(quote.IsExecutedSale);
            proof.VerifyIndependent();
        }
        Assert.Equal(3, calls);
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(stocks, session.State.Stocks.ToArray());
        Assert.Equal(cycles, session.State.Cycles.ToArray());
    }
}
