using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-UI-ECON-CYCLE-01: the tactical presentation reflects ONLY executed
/// canonical inventory, commitments, resident people and material sinks.
/// </summary>
public sealed class GaUiEconFlow01Tests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        Assert.NotEmpty(session.State.Cycles);
        return session;
    }

    private static CanonicalTacticalPhysicalFlowReport Read(
        CanonicalPhysicalTacticalSession session) =>
        CanonicalTacticalPhysicalFlowProjection.Read(session, session.InitialTile);

    [Fact]
    public void AtTurnZeroTheHudDoesNotInventANewEconomicCycle()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        var save = session.ExportSaveJson();
        var report = Read(session);
        Assert.Equal(0, report.ClosedEconomicCycles);
        Assert.Equal(0UL, report.LastClosedCycleTurn);
        Assert.Equal(0UL, report.LastGlobalProducedRaw);
        Assert.Equal(BigInteger.Zero, report.CommittedMarketRaw);
        Assert.Equal(BigInteger.Zero, report.DeliberatelyRetainedRaw);
        Assert.Equal(BigInteger.Zero, report.ExecutedLocalMaterialUseRaw);
        Assert.False(report.ProvesCommercialSaleOrMarketRevenue);
        Assert.False(report.UsesLegacyCashBalancesAsPowerN);
        Assert.False(report.ModifiesCanonicalSimulation);
        Assert.Equal(save, session.ExportSaveJson());
    }

    [Fact]
    public void LastCycleAndLocalFreeStocksHaveDifferentExplicitIdentities()
    {
        var session = Closed();
        var before = session.ExportSaveJson();
        var hash = session.CurrentHash;
        var report = Read(session);
        var cycle = session.State.Cycles[^1];
        Assert.Equal(cycle.MovementTurn, report.LastClosedCycleTurn);
        Assert.Equal(cycle.ProducedRaw, report.LastGlobalProducedRaw);
        Assert.Equal(cycle.FoodConsumedRaw, report.LastGlobalFoodConsumedRaw);
        Assert.Equal(cycle.FoodUnmetRaw, report.LastGlobalFoodUnmetRaw);
        Assert.Equal(session.WorkersAt(session.InitialTile), report.ResidentPeople);
        Assert.Equal(new BigInteger(session.StockAt(
            session.InitialTile, new CommodityId(1U))), report.FreeFoodRaw);
        Assert.Equal(new BigInteger(session.StockAt(
            session.InitialTile, new CommodityId(3U))), report.FreeWoodRaw);
        Assert.Equal(new BigInteger(session.StockAt(
            session.InitialTile, new CommodityId(2U))), report.FreeStoneRaw);
        Assert.True(report.ProvesPhysicalLocalInventory);
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(before, session.ExportSaveJson());
    }

    [Fact]
    public void DistinctOfferAndRetentionAppearWithoutDoubleCountingFreeStock()
    {
        var session = Closed();
        var origin = session.InitialTile;
        var wood = session.StockAt(origin, new CommodityId(3U));
        var stone = session.StockAt(origin, new CommodityId(2U));
        Assert.True(wood > 0UL);
        Assert.True(stone > 0UL);

        var a = session.CommitPhysicalStock(origin, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, wood);
        var b = session.CommitPhysicalStock(origin, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, stone);
        var report = Read(session);
        Assert.Equal(BigInteger.Zero, report.FreeWoodRaw);
        Assert.Equal(BigInteger.Zero, report.FreeStoneRaw);
        Assert.Equal(new BigInteger(wood), report.CommittedMarketRaw);
        Assert.Equal(new BigInteger(stone), report.DeliberatelyRetainedRaw);
        Assert.Equal(1, report.ActiveMarketOffers);
        Assert.Equal(1, report.ActiveRetentions);
        Assert.Equal(new[] { a.ActionOrdinal, b.ActionOrdinal },
            report.ActiveLocalCommitments.Select(x => x.ActionOrdinal).ToArray());
        Assert.Equal(BigInteger.Zero, report.ExecutedLocalMaterialUseRaw);
    }

    [Fact]
    public void RealWorkerUseClosesOfferWithoutFabricatingSaleRevenue()
    {
        var session = Closed();
        var origin = session.InitialTile;
        var wood = session.StockAt(origin, new CommodityId(3U));
        var reserved = session.CommitPhysicalStock(origin, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, wood);
        var use = session.UseCommittedLocalMaterialForWorkforce(
            reserved.ActionOrdinal, EconomicActivityKind.Forestry,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var report = Read(session);
        Assert.Equal(BigInteger.Zero, report.CommittedMarketRaw);
        Assert.Equal(0, report.ActiveMarketOffers);
        Assert.Empty(report.ActiveLocalCommitments);
        Assert.Equal(new BigInteger(wood), report.ExecutedLocalMaterialUseRaw);
        Assert.Equal(1, report.ExecutedLocalMaterialUses);
        Assert.Equal(use.ActionOrdinal, report.LastExecutedLocalUse!.ActionOrdinal);
        Assert.Equal(EconomicActivityKind.Forestry,
            report.LastExecutedLocalUse.BeneficiaryActivity);
        Assert.True(report.LastExecutedLocalUse.ProvesLocalWorkforceGroup);
        Assert.False(report.ProvesCommercialSaleOrMarketRevenue);
        Assert.False(report.UsesLegacyCashBalancesAsPowerN);
    }

    [Fact]
    public void CancelledRetentionReturnsStockAndCannotLookLikeACompletedUse()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var original = session.StockAt(tile, new CommodityId(2U));
        var lockA = session.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, original);
        Assert.Equal(BigInteger.Zero, Read(session).FreeStoneRaw);
        session.ReleasePhysicalStockCommitment(lockA.ActionOrdinal);
        var report = Read(session);
        Assert.Equal(new BigInteger(original), report.FreeStoneRaw);
        Assert.Equal(BigInteger.Zero, report.DeliberatelyRetainedRaw);
        Assert.Equal(0, report.ExecutedLocalMaterialUses);
        Assert.Null(report.LastExecutedLocalUse);
    }

    [Fact]
    public void DifferentTileCannotInheritOriginCommitments()
    {
        var session = Closed();
        var source = session.InitialTile;
        var target = session.AdjacentWorkable(source).First();
        var wood = session.StockAt(source, new CommodityId(3U));
        session.CommitPhysicalStock(source, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, wood);
        var remote = CanonicalTacticalPhysicalFlowProjection.Read(session, target);
        Assert.Equal(target.FineStrategicCellId.Value, remote.FineTile);
        Assert.Equal(BigInteger.Zero, remote.CommittedMarketRaw);
        Assert.Empty(remote.ActiveLocalCommitments);
        Assert.Equal(0, remote.ExecutedLocalMaterialUses);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalTacticalPhysicalFlowProjection.Read(session,
                new PhysicalTacticalTileId(new GoldbergParameters(3, 0),
                    source.FineStrategicCellId)));
    }

    [Fact]
    public void ReplayProducesExactlyTheSameHudReportAndNativeSaveBytes()
    {
        var session = Closed();
        var offer = session.CommitPhysicalStock(session.InitialTile,
            new CommodityId(3U), CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        session.UseCommittedLocalMaterialForWorkforce(offer.ActionOrdinal,
            EconomicActivityKind.Forestry,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        var report = Read(session);
        var save = session.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var after = Read(replay);

        Assert.Equal(session.CurrentHash, replay.CurrentHash);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(report.FineTile, after.FineTile);
        Assert.Equal(report.FreeWoodRaw, after.FreeWoodRaw);
        Assert.Equal(report.CommittedMarketRaw, after.CommittedMarketRaw);
        Assert.Equal(report.ExecutedLocalMaterialUseRaw,
            after.ExecutedLocalMaterialUseRaw);
        Assert.Equal(report.LastExecutedLocalUse!.ActionOrdinal,
            after.LastExecutedLocalUse!.ActionOrdinal);
        Assert.Equal(report.LastExecutedLocalUse.BeneficiaryWorkers,
            after.LastExecutedLocalUse.BeneficiaryWorkers);
        Assert.True(after.AuthenticatesExecutedLocalMaterialUse);
    }

    [Fact]
    public void DefaultMarketIntentCandidateExcludesOnlyDeliberateRetention()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var wood = session.StockAt(tile, new CommodityId(3U));
        var stone = session.StockAt(tile, new CommodityId(2U));
        Assert.True(wood > 0UL);
        Assert.True(stone > 0UL);
        var before = Read(session);
        Assert.Equal(new BigInteger(wood), before.PotentialMarketRaw(3U));
        Assert.Equal(new BigInteger(stone), before.PotentialMarketRaw(2U));
        Assert.False(before.ProvesAutomaticMarketSale);
        Assert.False(before.ProvesNutritionalEfficiency);

        var stockHash = session.CurrentHash;
        var stockSave = session.ExportSaveJson();
        _ = before.PotentialMarketRaw(1U);
        Assert.Equal(stockHash, session.CurrentHash);
        Assert.Equal(stockSave, session.ExportSaveJson());

        var offer = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, wood);
        var retain = session.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, stone);
        var staged = Read(session);
        Assert.Equal(BigInteger.Zero, staged.FreeWoodRaw);
        Assert.Equal(BigInteger.Zero, staged.FreeStoneRaw);
        Assert.Equal(new BigInteger(wood), staged.PotentialMarketRaw(3U));
        Assert.Equal(BigInteger.Zero, staged.PotentialMarketRaw(2U));
        Assert.False(staged.ProvesCommercialSaleOrMarketRevenue);
        Assert.False(staged.ProvesAutomaticMarketSale);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            staged.PotentialMarketRaw(999U));

        session.ReleasePhysicalStockCommitment(retain.ActionOrdinal);
        var returned = Read(session);
        Assert.Equal(new BigInteger(stone), returned.PotentialMarketRaw(2U));
        Assert.Equal(new BigInteger(wood), returned.PotentialMarketRaw(3U));
        Assert.False(returned.ProvesAutomaticMarketSale);
        Assert.Contains(returned.ActiveLocalCommitments, x =>
            x.ActionOrdinal == offer.ActionOrdinal);
    }
}
