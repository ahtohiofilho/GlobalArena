using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01hTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    [Fact]
    public void FreeProductionIsStillUnresolvedWithoutARealOfferOrSale()
    {
        var game = Closed();
        var previous = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var pool = CanonicalDefaultMarketPoolPreflight.FromCurrent(game);
        Assert.True(pool.EligibleByDefaultRaw > 0);

        var view = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Empty(view.Dispositions);
        Assert.Equal(BigInteger.Zero, view.TotalHistoricallyOfferedRaw);
        Assert.Equal(BigInteger.Zero, view.TotalPendingOfferRaw);
        Assert.Equal(BigInteger.Zero, view.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(pool.EligibleByDefaultRaw,
            view.TotalEligibleAwaitingDestinationRaw);
        Assert.Equal(BigInteger.Zero, view.TotalDeliberatelyRetainedRaw);
        Assert.True(view.AuthenticatesRealOfferAndMaterialUseJournals);
        Assert.False(view.AuthenticatesRemoteMarketDeliveries);
        Assert.False(view.AuthenticatesResidentFoodIngestion);
        Assert.False(view.ProvesCompletedMarketSales);
        Assert.False(view.CanAuthorizeMonetaryRevenueOrTax);
        Assert.False(view.MutatesCanonicalStateOrSave);
        view.VerifyIndependent();
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(previous, game.ExportSaveJson());
    }

    [Fact]
    public void OneRealMaterialSinkIsReconciledToItsExactCustodyReceipt()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.StockAt(tile, new CommodityId(3U));
        Assert.True(wood > 10UL);

        var retained = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, 2UL);
        var offered = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var origin = tile.FineStrategicCellId.Value;
        var plan = CanonicalLocalMarketDestinationPreflight.FromCurrent(game,
            new[] {
                new CanonicalLocalMarketDestinationCandidate(origin, origin,
                    3U, 3UL, CanonicalLocalMarketUsePurpose.WorkforceMaterialUse,
                    EconomicActivityKind.Forestry)
            });
        var use = CanonicalLocalMarketMaterialDestinationExecutor.Execute(
            game, plan, offered.ActionOrdinal,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        var save = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var view = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        var woodRow = view.Markets.Single(x => x.Commodity == 3U &&
            x.FineTile == origin);

        Assert.Equal(new BigInteger(3), woodRow.HistoricallyOfferedRaw);
        Assert.Equal(BigInteger.Zero, woodRow.PendingOfferRaw);
        Assert.Equal(BigInteger.Zero, woodRow.ReleasedToFreeRaw);
        Assert.Equal(new BigInteger(3), woodRow.ExecutedLocalMaterialRaw);
        Assert.Equal(new BigInteger(3), woodRow.OfWhichRealWorkforceRaw);
        Assert.Equal(new BigInteger(2), woodRow.ExplicitRetentionRaw);
        Assert.Equal(new BigInteger(wood - 5UL), woodRow.UncommittedFreeRaw);
        Assert.Equal(new BigInteger(wood - 5UL),
            woodRow.EligibleAwaitingDestinationRaw);

        var traced = view.Dispositions.Single(x =>
            x.CommitmentActionOrdinal == offered.ActionOrdinal);
        Assert.Equal(CanonicalMarketCustodyDisposition.ExecutedLocalWorkforceMaterialUse,
            traced.Disposition);
        Assert.Equal(use.ActionOrdinal, traced.PhysicalUseActionOrdinal);
        Assert.Equal(EconomicActivityKind.Forestry, traced.BeneficiaryActivity);
        Assert.True(traced.BeneficiaryWorkers > 0UL);
        Assert.True(traced.ProvesRealPhysicalMaterialSink);
        Assert.False(traced.ProvesExecutedCommercialSale);
        Assert.Contains(view.Dispositions, x =>
            x.CommitmentActionOrdinal == retained.ActionOrdinal &&
            x.Disposition == CanonicalMarketCustodyDisposition.ExplicitRetention);
        Assert.Equal(new BigInteger(3), view.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(new BigInteger(3), view.TotalRealWorkforceMaterialRaw);
        Assert.False(view.ProvesCompletedMarketSales);
        Assert.False(view.CanAuthorizeMonetaryRevenueOrTax);
        view.VerifyIndependent();
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());

        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var cloned = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(replay);
        Assert.Equal(view.Dispositions.ToArray(), cloned.Dispositions.ToArray());
        Assert.Equal(view.Markets.ToArray(), cloned.Markets.ToArray());
        Assert.Equal(view.TotalExecutedPhysicalMaterialRaw,
            cloned.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
    }

    [Fact]
    public void ReleaseThenReofferCannotCountIdenticalGoodsTwiceAsAvailable()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var before = game.StockAt(tile, new CommodityId(2U));
        Assert.True(before > 4UL);

        var first = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        game.ReleasePhysicalStockCommitment(first.ActionOrdinal);
        var second = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var view = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        var row = view.Markets.Single(x => x.Commodity == 2U &&
            x.FineTile == tile.FineStrategicCellId.Value);

        Assert.Equal(new BigInteger(4), row.HistoricallyOfferedRaw);
        Assert.Equal(new BigInteger(2), row.ReleasedToFreeRaw);
        Assert.Equal(new BigInteger(2), row.PendingOfferRaw);
        Assert.Equal(BigInteger.Zero, row.ExecutedLocalMaterialRaw);
        Assert.Equal(new BigInteger(before - 2UL), row.UncommittedFreeRaw);
        Assert.Equal(new BigInteger(before),
            row.EligibleAwaitingDestinationRaw);
        Assert.Equal(new BigInteger(before),
            CanonicalDefaultMarketPoolPreflight.FromCurrent(game)
                .ForTileCommodity(tile.FineStrategicCellId.Value, 2U)
                .EligibleByDefaultRaw);
        Assert.Contains(view.Dispositions, x =>
            x.CommitmentActionOrdinal == first.ActionOrdinal &&
            x.Disposition == CanonicalMarketCustodyDisposition.ReturnedToFreeStock &&
            !x.PhysicalUseActionOrdinal.HasValue);
        Assert.Contains(view.Dispositions, x =>
            x.CommitmentActionOrdinal == second.ActionOrdinal &&
            x.Disposition == CanonicalMarketCustodyDisposition.PendingOffer);
        Assert.False(view.ProvesCompletedMarketSales);
        view.VerifyIndependent();
    }

    [Fact]
    public void AutomaticOffersArePendingRatherThanDeclaredSold()
    {
        var game = Closed();
        var originalSave = game.ExportSaveJson();
        var batch = CanonicalStagedAutomaticMarketOfferBatch.Prepare(game);
        var staged = batch.PreparedSession;
        var view = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(staged);

        Assert.Equal(batch.NewlyOfferedRaw,
            view.TotalHistoricallyOfferedRaw);
        Assert.Equal(batch.TotalEligibleRaw,
            view.TotalPendingOfferRaw);
        Assert.Equal(BigInteger.Zero, view.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(BigInteger.Zero, view.TotalRealWorkforceMaterialRaw);
        Assert.Equal(BigInteger.Zero,
            view.Markets.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.UncommittedFreeRaw));
        Assert.Equal(batch.TotalEligibleRaw,
            view.TotalEligibleAwaitingDestinationRaw);
        Assert.All(view.Dispositions, row =>
        {
            Assert.Equal(CanonicalMarketCustodyDisposition.PendingOffer,
                row.Disposition);
            Assert.False(row.ProvesRealPhysicalMaterialSink);
            Assert.False(row.ProvesExecutedCommercialSale);
        });
        Assert.False(view.ProvesCompletedMarketSales);
        Assert.False(view.CanAuthorizeMonetaryRevenueOrTax);
        Assert.Equal(originalSave, game.ExportSaveJson());
    }

    [Fact]
    public void HistoricalUseWithoutWorkforceRemainsPhysicalButNotAProvenBuyer()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        game.UseCommittedLocalMaterial(offer.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var view = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        var use = view.Dispositions.Single(x =>
            x.CommitmentActionOrdinal == offer.ActionOrdinal);

        Assert.Equal(CanonicalMarketCustodyDisposition.ExecutedLocalMaterialUse,
            use.Disposition);
        Assert.True(use.ProvesRealPhysicalMaterialSink);
        Assert.Null(use.BeneficiaryActivity);
        Assert.Equal(0UL, use.BeneficiaryWorkers);
        Assert.Equal(BigInteger.One, view.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(BigInteger.Zero, view.TotalRealWorkforceMaterialRaw);
        Assert.False(view.ProvesCompletedMarketSales);
    }

    [Fact]
    public void ForeignAndStaleCoverageFailsClosedWithoutChangingTheSave()
    {
        var game = Closed();
        var committed = game.CommitPhysicalStock(game.InitialTile,
            new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        var view = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        var other = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            game.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            view.RequireCurrent(other));
        var before = game.ExportSaveJson();
        game.ReleasePhysicalStockCommitment(committed.ActionOrdinal);
        Assert.Throws<InvalidOperationException>(view.VerifyIndependent);
        Assert.Throws<InvalidOperationException>(() => view.RequireCurrent(game));
        Assert.NotEqual(before, game.ExportSaveJson());
        Assert.False(view.CanAuthorizeMonetaryRevenueOrTax);
    }
}
