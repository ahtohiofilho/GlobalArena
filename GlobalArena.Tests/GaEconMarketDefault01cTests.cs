using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01cTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        return session;
    }

    private static CanonicalLocalMarketDestinationPreflight Plan(
        CanonicalPhysicalTacticalSession session, uint commodity,
        ulong raw, EconomicActivityKind activity)
    {
        var tile = session.InitialTile.FineStrategicCellId.Value;
        return CanonicalLocalMarketDestinationPreflight.FromCurrent(
            session, new[]
            {
                new CanonicalLocalMarketDestinationCandidate(
                    tile, tile, commodity, raw,
                    CanonicalLocalMarketUsePurpose.WorkforceMaterialUse, activity)
            });
    }

    [Fact]
    public void ExactEscrowedMaterialExecutesOnceWithAuthenticJournalAndReplay()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var stock = session.StockAt(tile, new CommodityId(3U));
        Assert.True(stock > 0UL);
        var commitment = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, stock);
        var preflight = Plan(session, 3U, stock, EconomicActivityKind.Forestry);
        var before = session.CurrentHash;
        var cycle = session.State.Cycles[^1];

        var use = CanonicalLocalMarketMaterialDestinationExecutor.Execute(
            session, preflight, commitment.ActionOrdinal,
            CanonicalMaterialDispositionKind.IntermediateInputUse);

        Assert.NotEqual(before, session.CurrentHash);
        Assert.Equal(cycle, session.State.Cycles[^1]);
        Assert.Equal(0UL, session.StockAt(tile, new CommodityId(3U)));
        Assert.Equal(stock, use.UsedRaw);
        Assert.Equal(commitment.ActionOrdinal, use.SourceCommitmentActionOrdinal);
        Assert.Equal(tile.FineStrategicCellId.Value, use.FineTile);
        Assert.Equal(EconomicActivityKind.Forestry, use.BeneficiaryActivity);
        Assert.True(use.BeneficiaryWorkers > 0UL);
        Assert.Equal(stock, use.SourceAttributions.Aggregate(0UL,
            (sum, x) => checked(sum + x.RawQuantity)));
        Assert.True(use.ProvesExecutedPhysicalMaterialUse);
        Assert.False(use.ProvesMonetarySale);
        Assert.False(use.HasExecutedTransport);
        Assert.False(use.ProvesDeliveryToFinalEconomicBuyer);
        Assert.False(use.AuthorizesPurchasingPowerIndexN);
        Assert.Single(session.ExecutedCommittedMaterialUses);
        Assert.Equal("use_committed_material_for_workers", session.Events[^1].Kind);
        Assert.Equal(commitment.ActionOrdinal, session.Events[^1].CommitmentOrdinal);
        Assert.Single(session.PhysicalStockCommitments);
        Assert.True(session.PhysicalStockCommitments[0].Consumed);
        session.CapturePhysicalStockCommitmentWitness().VerifyIndependent();

        var saved = session.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(saved, replay.ExportSaveJson());
        Assert.Equal(session.CurrentHash, replay.CurrentHash);
        Assert.Equal(session.Events.ToArray(), replay.Events.ToArray());
        Assert.Equal(session.ExecutedCommittedMaterialUses[0].UsedRaw,
            replay.ExecutedCommittedMaterialUses[0].UsedRaw);
        Assert.Equal(use.SourceAttributions.ToArray(),
            replay.ExecutedCommittedMaterialUses[0].SourceAttributions.ToArray());
    }

    [Fact]
    public void IncorrectLockQuantityOrRetainedCustodyNeverMutatesState()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var wood = session.StockAt(tile, new CommodityId(3U));
        Assert.True(wood > 2UL);
        var offer = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        var hold = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, 1UL);
        var plan = Plan(session, 3U, 2UL, EconomicActivityKind.Forestry);
        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                session, plan, offer.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                session, plan, hold.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                session, plan, 99999,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Empty(session.ExecutedCommittedMaterialUses);
    }

    [Fact]
    public void FoodMultiCandidateAndForeignOrStaleProposalFailClosed()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var wood = session.StockAt(tile, new CommodityId(3U));
        var offer = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        var candidate = Plan(session, 3U, 1UL, EconomicActivityKind.Forestry);

        var other = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                other, candidate, offer.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));

        var origin = tile.FineStrategicCellId.Value;
        var multi = CanonicalLocalMarketDestinationPreflight.FromCurrent(
            session, new[]
            {
                new CanonicalLocalMarketDestinationCandidate(origin, origin,
                    3U, 1UL, CanonicalLocalMarketUsePurpose.WorkforceMaterialUse,
                    EconomicActivityKind.Forestry),
                new CanonicalLocalMarketDestinationCandidate(origin, origin,
                    2U, 1UL, CanonicalLocalMarketUsePurpose.WorkforceMaterialUse,
                    EconomicActivityKind.Mining)
            });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                session, multi, offer.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));

        // Explicitly produce a positive food remainder in a separate
        // session. The six-food ledger remains absent, so the material-use
        // gateway must not turn aggregate food into certified intake.
        var foodGame = CanonicalPhysicalTacticalSession.Start();
        var foodTile = foodGame.InitialTile;
        foodGame.ReassignWorkers(foodTile, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 2UL);
        foodGame.ReassignWorkers(foodTile, EconomicActivityKind.Mining,
            EconomicActivityKind.Agriculture, 2UL);
        for (var i = 0; i < 6; i++) foodGame.AdvanceMovementTurn();
        Assert.True(foodGame.StockAt(foodTile, new CommodityId(1U)) > 0UL);
        var foodOrigin = foodTile.FineStrategicCellId.Value;
        var food = CanonicalLocalMarketDestinationPreflight.FromCurrent(
            foodGame, new[]
            {
                new CanonicalLocalMarketDestinationCandidate(foodOrigin,
                    foodOrigin, 1U, 1UL,
                    CanonicalLocalMarketUsePurpose.ResidentFoodUse)
            });
        var beforeFood = foodGame.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                foodGame, food, offer.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Equal(beforeFood, foodGame.ExportSaveJson());

        var before = session.CurrentHash;
        var save = session.ExportSaveJson();
        session.ReassignWorkers(tile, EconomicActivityKind.Mining,
            EconomicActivityKind.Agriculture, 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                session, candidate, offer.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.NotEqual(before, session.CurrentHash);
        Assert.NotEqual(save, session.ExportSaveJson());
        Assert.Empty(session.ExecutedCommittedMaterialUses);
        Assert.Equal(wood - 1UL, session.StockAt(tile, new CommodityId(3U)));
    }

    [Fact]
    public void AcceptedPlanCannotBeReusedOrReplayedAsAnotherSale()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var offer = session.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        var plan = Plan(session, 2U, 1UL, EconomicActivityKind.Mining);
        var use = CanonicalLocalMarketMaterialDestinationExecutor.Execute(
            session, plan, offer.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var saved = session.ExportSaveJson();
        var hash = session.CurrentHash;
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                session, plan, offer.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Equal(saved, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
        Assert.Single(session.ExecutedCommittedMaterialUses);
        Assert.Equal(use.ActionOrdinal,
            session.ExecutedCommittedMaterialUses[0].ActionOrdinal);
    }
}
