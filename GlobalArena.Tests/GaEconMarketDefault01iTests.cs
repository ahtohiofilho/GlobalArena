using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01iTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    [Fact]
    public void TwoRealMaterialEscrowsAreUsedInOneAtomicReplayableBatch()
    {
        var source = Closed();
        var tile = source.InitialTile;
        var originalSave = source.ExportSaveJson();
        var originalHash = source.CurrentHash;
        var wood = source.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var stone = source.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var beforeSource = source.ExportSaveJson();
        var hash = source.CurrentHash;

        // Deliberately out of order: replay order must follow the canonical
        // commitment ordinals, not nondeterministic request enumeration.
        var batch = CanonicalStagedLocalMaterialUseBatch.Prepare(
            source, new[]
            {
                new CanonicalLocalWorkforceMaterialUseOrder(
                    stone.ActionOrdinal, EconomicActivityKind.Mining,
                    CanonicalMaterialDispositionKind.IntermediateInputUse),
                new CanonicalLocalWorkforceMaterialUseOrder(
                    wood.ActionOrdinal, EconomicActivityKind.Forestry,
                    CanonicalMaterialDispositionKind.FinalPhysicalUse)
            });
        Assert.Equal(beforeSource, source.ExportSaveJson());
        Assert.Equal(hash, source.CurrentHash);
        Assert.NotEqual(batch.SourceWorldHashHex, batch.PreparedWorldHashHex);
        Assert.Equal(hash.HexDigest, batch.SourceWorldHashHex);
        Assert.Equal(new BigInteger(5), batch.TotalUsedRaw);
        Assert.Equal(2, batch.ExecutedUses.Count);
        Assert.Equal(wood.ActionOrdinal,
            batch.ExecutedUses[0].SourceCommitmentActionOrdinal);
        Assert.Equal(stone.ActionOrdinal,
            batch.ExecutedUses[1].SourceCommitmentActionOrdinal);
        Assert.All(batch.ExecutedUses, x =>
        {
            Assert.True(x.ProvesLocalWorkforceGroup);
            Assert.True(x.ProvesExecutedPhysicalMaterialUse);
            Assert.False(x.ProvesMonetarySale);
            Assert.False(x.HasExecutedTransport);
            Assert.Equal(x.UsedRaw, x.SourceAttributions.Aggregate(
                0UL, (sum, a) => checked(sum + a.RawQuantity)));
        });
        Assert.True(batch.ExecutesRealJournaledLocalMaterialUses);
        Assert.False(batch.MakesUnrequestedMaterialUses);
        Assert.False(batch.ProvesCommercialSales);
        Assert.False(batch.CreatesMonetaryRevenue);
        Assert.False(batch.ExecutesRemoteTransport);
        Assert.False(batch.ActivatesNutritionFromAggregateFood);
        Assert.False(batch.ChangesOriginalSession);

        var coverage = batch.PhysicalCoverage;
        Assert.Equal(new BigInteger(5),
            coverage.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(new BigInteger(5),
            coverage.TotalRealWorkforceMaterialRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalPendingOfferRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalHistoricallyOfferedRaw -
            coverage.TotalExecutedPhysicalMaterialRaw);
        Assert.False(coverage.ProvesCompletedMarketSales);
        Assert.False(coverage.CanAuthorizeMonetaryRevenueOrTax);
        coverage.VerifyIndependent();

        var save = batch.PreparedSession.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(batch.PreparedSession.CurrentHash, replay.CurrentHash);
        var coverageReplay = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(replay);
        Assert.Equal(coverage.Dispositions.ToArray(),
            coverageReplay.Dispositions.ToArray());
        Assert.Equal(coverage.Markets.ToArray(), coverageReplay.Markets.ToArray());
        Assert.Equal(originalSave, CanonicalPhysicalTacticalSession
            .RestoreSaveJson(originalSave).ExportSaveJson());
        Assert.NotEqual(originalHash, source.CurrentHash); // explicit commitments only
    }

    [Fact]
    public void UnmatchedOffersAndDeliberateRetentionRemainUntouched()
    {
        var source = Closed();
        var tile = source.InitialTile;
        var wood = source.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 7UL);
        var stone = source.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 11UL);
        var retained = source.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, 2UL);
        var before = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(source);
        var original = source.ExportSaveJson();

        var result = CanonicalStagedLocalMaterialUseBatch.Prepare(source,
            new[]
            {
                new CanonicalLocalWorkforceMaterialUseOrder(
                    wood.ActionOrdinal, EconomicActivityKind.Forestry,
                    CanonicalMaterialDispositionKind.IntermediateInputUse)
            });
        var after = result.PhysicalCoverage;
        Assert.Equal(BigInteger.Zero, after.TotalExecutedPhysicalMaterialRaw -
            new BigInteger(7));
        Assert.Equal(new BigInteger(11), after.TotalPendingOfferRaw);
        Assert.Equal(before.TotalEligibleAwaitingDestinationRaw -
            new BigInteger(7), after.TotalEligibleAwaitingDestinationRaw);
        Assert.Equal(before.TotalDeliberatelyRetainedRaw,
            after.TotalDeliberatelyRetainedRaw);
        Assert.Contains(after.Dispositions, x =>
            x.CommitmentActionOrdinal == stone.ActionOrdinal &&
            x.Disposition == CanonicalMarketCustodyDisposition.PendingOffer);
        Assert.Contains(after.Dispositions, x =>
            x.CommitmentActionOrdinal == retained.ActionOrdinal &&
            x.Disposition == CanonicalMarketCustodyDisposition.ExplicitRetention);
        Assert.Equal(original, source.ExportSaveJson());
    }

    [Fact]
    public void InvalidOrDuplicatedInstructionsNeverChangeSourceGame()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var stone = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var retained = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, 1UL);
        var food = game.StockAt(tile, new CommodityId(1U));
        CanonicalPhysicalStockCommitment? offeredFood = null;
        if (food > 0UL)
            offeredFood = game.CommitPhysicalStock(tile, new CommodityId(1U),
                CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);

        var save = game.ExportSaveJson();
        var hash = game.CurrentHash;

        var valid = new CanonicalLocalWorkforceMaterialUseOrder(
            wood.ActionOrdinal, EconomicActivityKind.Forestry,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var invalid = new[]
        {
            new CanonicalLocalWorkforceMaterialUseOrder(
                retained.ActionOrdinal, EconomicActivityKind.Mining,
                CanonicalMaterialDispositionKind.FinalPhysicalUse),
            new CanonicalLocalWorkforceMaterialUseOrder(
                99999, EconomicActivityKind.Mining,
                CanonicalMaterialDispositionKind.FinalPhysicalUse),
            new CanonicalLocalWorkforceMaterialUseOrder(
                stone.ActionOrdinal, (EconomicActivityKind)255,
                CanonicalMaterialDispositionKind.IntermediateInputUse),
            new CanonicalLocalWorkforceMaterialUseOrder(
                stone.ActionOrdinal, EconomicActivityKind.Mining,
                (CanonicalMaterialDispositionKind)255)
        };
        foreach (var bad in invalid)
            Assert.Throws<InvalidOperationException>(() =>
                CanonicalStagedLocalMaterialUseBatch.Prepare(game,
                    new[] { valid, bad }));
        Assert.Throws<ArgumentException>(() =>
            CanonicalStagedLocalMaterialUseBatch.Prepare(game,
                new[] { valid, valid }));
        Assert.Throws<ArgumentException>(() =>
            CanonicalStagedLocalMaterialUseBatch.Prepare(game,
                Array.Empty<CanonicalLocalWorkforceMaterialUseOrder>()));
        if (offeredFood is not null)
            Assert.Throws<InvalidOperationException>(() =>
                CanonicalStagedLocalMaterialUseBatch.Prepare(game,
                    new[] { valid, new CanonicalLocalWorkforceMaterialUseOrder(
                        offeredFood.ActionOrdinal, EconomicActivityKind.Agriculture,
                        CanonicalMaterialDispositionKind.FinalPhysicalUse) }));
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.ExecutedCommittedMaterialUses);
    }

    [Fact]
    public void OptInTimelineAdoptsOnlyCompletePhysicalBatchWithCheckpointReplay()
    {
        var playable = CanonicalPhysicalTacticalSession.Start();
        var initialSave = playable.ExportSaveJson();
        var timeline = CanonicalAutomaticMarketOfferTimeline.StartOptIn(playable);
        for (var i = 0; i < 6; i++) timeline.AdvanceMovementTurn();
        var before = timeline.ExportCheckpointJson();
        var supply = timeline.CaptureCanonicalSnapshot();
        var wood = supply.PhysicalStockCommitments.First(x =>
            x.IsLive && x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer &&
            x.Commodity == 3U);
        var stone = supply.PhysicalStockCommitments.First(x =>
            x.IsLive && x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer &&
            x.Commodity == 2U);

        var result = timeline.ApplyExplicitLocalMaterialUses(new[]
        {
            new CanonicalLocalWorkforceMaterialUseOrder(
                wood.ActionOrdinal, EconomicActivityKind.Forestry,
                CanonicalMaterialDispositionKind.FinalPhysicalUse),
            new CanonicalLocalWorkforceMaterialUseOrder(
                stone.ActionOrdinal, EconomicActivityKind.Mining,
                CanonicalMaterialDispositionKind.IntermediateInputUse)
        });
        Assert.Equal(new BigInteger(wood.RawQuantity) + stone.RawQuantity,
            result.TotalUsedRaw);
        Assert.Equal(6UL, timeline.MovementTurn);
        Assert.Equal(1, timeline.ClosedEconomicCycles);
        Assert.NotEqual(before, timeline.ExportCheckpointJson());
        Assert.Equal(initialSave, playable.ExportSaveJson());
        Assert.False(timeline.ExecutesAutomaticSales);

        // Return value is detached. Its mutation must not change timeline.
        var frozen = timeline.ExportCheckpointJson();
        result.PreparedSession.AdvanceMovementTurn();
        Assert.Equal(frozen, timeline.ExportCheckpointJson());

        var replay = CanonicalAutomaticMarketOfferTimeline
            .RestoreCheckpointJson(frozen);
        Assert.Equal(frozen, replay.ExportCheckpointJson());
        Assert.Equal(timeline.WorldHashHex, replay.WorldHashHex);
        Assert.Equal(2, replay.CaptureCanonicalSnapshot()
            .ExecutedCommittedMaterialUses.Count);
        Assert.False(replay.ExecutesAutomaticSales);
    }

    [Fact]
    public void InvalidTimelineBatchDoesNotCommitPartialPhysicalUses()
    {
        var timeline = CanonicalAutomaticMarketOfferTimeline.StartOptIn(
            CanonicalPhysicalTacticalSession.Start());
        for (var i = 0; i < 6; i++) timeline.AdvanceMovementTurn();
        var offer = timeline.CaptureCanonicalSnapshot()
            .PhysicalStockCommitments.First(x =>
                x.IsLive && x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer &&
                x.Commodity == 3U);
        var before = timeline.ExportCheckpointJson();
        Assert.Throws<InvalidOperationException>(() =>
            timeline.ApplyExplicitLocalMaterialUses(new[]
            {
                new CanonicalLocalWorkforceMaterialUseOrder(
                    offer.ActionOrdinal, EconomicActivityKind.Forestry,
                    CanonicalMaterialDispositionKind.FinalPhysicalUse),
                new CanonicalLocalWorkforceMaterialUseOrder(
                    99999, EconomicActivityKind.Mining,
                    CanonicalMaterialDispositionKind.FinalPhysicalUse)
            }));
        Assert.Equal(before, timeline.ExportCheckpointJson());
        Assert.Empty(timeline.CaptureCanonicalSnapshot()
            .ExecutedCommittedMaterialUses);
    }
}
