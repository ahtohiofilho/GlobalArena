using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01A: stock is eligible by default; only executed
/// deliberate retention removes it from admission. Admission is NEVER sale.
/// </summary>
public sealed class GaEconMarketDefault01AAdmissionTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        Assert.Single(session.State.Cycles);
        return session;
    }

    [Fact]
    public void InitialAndExecutedCycleAdmissionNeverCreateSalesMoneyOrNutrition()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        var initialSave = session.ExportSaveJson();
        var initialHash = session.CurrentHash;
        var initial = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        initial.VerifyIndependent();
        Assert.Equal(0, initial.ClosedEconomicCycles);
        Assert.False(initial.ProvesActualSaleOrRevenue);
        Assert.False(initial.ProvesNutritionalQuality);
        Assert.False(initial.ChangesWorldOrSave);
        Assert.True(initial.ComputesWithoutDemandCaps);
        Assert.True(initial.ExcludesExpeditionCargo);
        Assert.Equal(initialSave, session.ExportSaveJson());
        Assert.Equal(initialHash, session.CurrentHash);

        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(initial.VerifyIndependent);
        var after = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        Assert.Equal(1, after.ClosedEconomicCycles);
        Assert.True(after.TotalEligibleForMarketRaw > 0);
        Assert.Equal(BigInteger.Zero, after.TotalDeliberatelyRetainedRaw);
        Assert.Equal(BigInteger.Zero, after.TotalAlreadyOfferedRaw);
        Assert.Equal(after.TotalFreeLocalRaw, after.TotalEligibleForMarketRaw);
        Assert.All(after.Rows, row => Assert.False(row.IsExecutedSale));
    }

    [Fact]
    public void EveryCanonicalLocalStockIsAutomaticallyEligibleUnlessActuallyRetained()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var woodId = new CommodityId(3U);
        var stoneId = new CommodityId(2U);
        var wood = session.StockAt(tile, woodId);
        var stone = session.StockAt(tile, stoneId);
        Assert.True(wood > 0);
        Assert.True(stone > 0);

        var original = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        Assert.Equal(new BigInteger(wood),
            original.EligibleAt(tile.FineStrategicCellId.Value, 3U));
        Assert.Equal(new BigInteger(stone),
            original.EligibleAt(tile.FineStrategicCellId.Value, 2U));

        var partial = wood / 2UL;
        Assert.True(partial > 0);
        var escrow = session.CommitPhysicalStock(tile, woodId,
            CanonicalPhysicalCommitmentKind.MarketOffer, partial);
        var reserved = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        var woodRow = Assert.Single(reserved.Rows, x =>
            x.FineTile == tile.FineStrategicCellId.Value &&
            x.Commodity == 3U);
        Assert.Equal(new BigInteger(wood - partial), woodRow.FreeLocalRaw);
        Assert.Equal(new BigInteger(partial), woodRow.ExistingMarketCommitmentRaw);
        Assert.Equal(new BigInteger(wood), woodRow.EligibleForMarketRaw);

        var retention = session.CommitPhysicalStock(tile, stoneId,
            CanonicalPhysicalCommitmentKind.DeliberateRetention, stone);
        var retained = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        Assert.Equal(BigInteger.Zero, retained.EligibleAt(
            tile.FineStrategicCellId.Value, 2U));
        Assert.Equal(new BigInteger(stone),
            Assert.Single(retained.Rows, x =>
                x.FineTile == tile.FineStrategicCellId.Value &&
                x.Commodity == 2U).DeliberatelyRetainedRaw);
        Assert.Equal(new BigInteger(wood), retained.EligibleAt(
            tile.FineStrategicCellId.Value, 3U));
        Assert.False(retained.ProvesActualSaleOrRevenue);

        session.ReleasePhysicalStockCommitment(retention.ActionOrdinal);
        var released = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        Assert.Equal(new BigInteger(stone), released.EligibleAt(
            tile.FineStrategicCellId.Value, 2U));
        Assert.Equal(new BigInteger(wood), released.EligibleAt(
            tile.FineStrategicCellId.Value, 3U));
        Assert.Equal(new BigInteger(partial), released.TotalAlreadyOfferedRaw);
        Assert.Contains(released.Rows, x =>
            x.FineTile == tile.FineStrategicCellId.Value &&
            x.Commodity == 3U);
        Assert.Contains(session.PhysicalStockCommitments, x =>
            x.ActionOrdinal == escrow.ActionOrdinal && x.IsLive);
    }

    [Fact]
    public void SnapshotReconcilesAllLocalTilesAndCommoditiesWithoutCapsOrDuplication()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var wood = session.StockAt(tile, new CommodityId(3U));
        session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, wood / 3UL);
        session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, wood / 3UL);
        var saved = session.ExportSaveJson();
        var hash = session.CurrentHash;
        var plan = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        plan.VerifyIndependent();

        var expectedFree = session.State.Stocks.Aggregate(
            BigInteger.Zero, (sum, row) => sum + row.RawQuantity);
        var expectedMarket = session.PhysicalStockCommitments
            .Where(x => x.IsLive &&
                x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer)
            .Aggregate(BigInteger.Zero, (sum, row) => sum + row.RawQuantity);
        var expectedRetention = session.PhysicalStockCommitments
            .Where(x => x.IsLive &&
                x.Kind == CanonicalPhysicalCommitmentKind.DeliberateRetention)
            .Aggregate(BigInteger.Zero, (sum, row) => sum + row.RawQuantity);
        Assert.Equal(expectedFree, plan.TotalFreeLocalRaw);
        Assert.Equal(expectedMarket, plan.TotalAlreadyOfferedRaw);
        Assert.Equal(expectedRetention, plan.TotalDeliberatelyRetainedRaw);
        Assert.Equal(expectedFree + expectedMarket,
            plan.TotalEligibleForMarketRaw);
        Assert.Equal(expectedFree + expectedMarket + expectedRetention,
            plan.Rows.Aggregate(BigInteger.Zero,
                (sum, row) => sum + row.AccountedPhysicalRaw));
        Assert.Equal(plan.Rows.Count, plan.Rows
            .Select(x => (x.FineTile, x.Commodity)).Distinct().Count());
        Assert.Equal(plan.Rows.OrderBy(x => x.FineTile)
            .ThenBy(x => x.Commodity), plan.Rows);
        Assert.Equal(saved, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
        Assert.False(plan.ProvesActualSaleOrRevenue);
        Assert.True(plan.ComputesWithoutDemandCaps);
    }

    [Fact]
    public void SaveReplayRegeneratesIdenticalAdmissionButDoesNotBorrowAuthority()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var wood = session.StockAt(tile, new CommodityId(3U));
        var stone = session.StockAt(tile, new CommodityId(2U));
        session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, wood);
        session.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, stone);
        var before = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        var saved = session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        var after = CanonicalDefaultMarketAdmissionPlan.Capture(restored);

        Assert.Equal(saved, restored.ExportSaveJson());
        Assert.Equal(session.CurrentHash, restored.CurrentHash);
        Assert.Equal(before.Rows.ToArray(), after.Rows.ToArray());
        Assert.Equal(before.TotalEligibleForMarketRaw,
            after.TotalEligibleForMarketRaw);
        Assert.Equal(before.TotalDeliberatelyRetainedRaw,
            after.TotalDeliberatelyRetainedRaw);
        Assert.Throws<InvalidOperationException>(() =>
            before.RequireCurrent(restored));
        after.VerifyIndependent();
    }

    [Fact]
    public void RefusesStaleOrInvalidIdentityWithoutMutatingTheCanonicalGame()
    {
        var session = Closed();
        var plan = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        var save = session.ExportSaveJson();
        var hash = session.CurrentHash;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            plan.EligibleAt(0UL, 1U));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            plan.EligibleAt(session.InitialTile.FineStrategicCellId.Value, 0U));
        Assert.Equal(BigInteger.Zero, plan.EligibleAt(999999UL, 999U));
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);

        session.ReassignWorkers(session.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        Assert.Throws<InvalidOperationException>(plan.VerifyIndependent);
        Assert.Throws<InvalidOperationException>(() =>
            plan.EligibleAt(session.InitialTile.FineStrategicCellId.Value, 1U));
    }

    [Fact]
    public void CarriedFoodIsNotIncludedInLocalMarketStock()
    {
        var session = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);
        var start = session.InitialTile;
        session.ReassignWorkers(start, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 2UL);
        session.ReassignWorkers(start, EconomicActivityKind.Mining,
            EconomicActivityKind.Agriculture, 2UL);
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        var food = session.StockAt(start, new CommodityId(1U));
        Assert.True(food > 0);
        var neighbors = session.AdjacentWorkable(start).ToHashSet();
        var destination = neighbors.SelectMany(session.AdjacentWorkable)
            .Where(x => x != start && !neighbors.Contains(x))
            .Distinct().OrderBy(x => x.FineStrategicCellId.Value).First();
        session.OrderProvisionedLongDistanceTransit(start, destination,
            EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL, Math.Min(food, 2_000_000UL));
        Assert.NotNull(session.AdvanceMovementTurn());

        var plan = CanonicalDefaultMarketAdmissionPlan.Capture(session);
        var lots = session.CaptureLatestPhysicalLotWitness();
        var expedition = lots.ClosingLots
            .Where(x => x.Custody ==
                CanonicalPhysicalLotCustodyKind.ExpeditionCargo)
            .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        Assert.True(expedition > 0);
        var physicalLocal = session.State.Stocks.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        Assert.Equal(physicalLocal, plan.TotalFreeLocalRaw);
        Assert.Equal(BigInteger.Zero, plan.TotalAlreadyOfferedRaw);
        Assert.Equal(BigInteger.Zero, plan.TotalDeliberatelyRetainedRaw);
        Assert.True(plan.ExcludesExpeditionCargo);
        Assert.Equal(physicalLocal, plan.TotalEligibleForMarketRaw);
        plan.VerifyIndependent();
    }
}
