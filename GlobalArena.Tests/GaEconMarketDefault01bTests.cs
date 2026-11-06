using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01B: do not present a physical candidate as a
/// completed sale, fabricate a buyer, or teleport merchandise between tiles.
/// </summary>
public sealed class GaEconMarketDefault01bTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    private static CanonicalLocalMarketDestinationCandidate Wood(
        CanonicalPhysicalTacticalSession game, ulong raw) =>
        new(game.InitialTile.FineStrategicCellId.Value,
            game.InitialTile.FineStrategicCellId.Value,
            3U, raw, CanonicalLocalMarketUsePurpose.WorkforceMaterialUse,
            EconomicActivityKind.Forestry);

    [Fact]
    public void RealLocalWorkforceDestinationIsOnlyAConservedProposal()
    {
        var game = Closed();
        var wood = game.StockAt(game.InitialTile, new CommodityId(3U));
        var stone = game.StockAt(game.InitialTile, new CommodityId(2U));
        Assert.True(wood > 0UL && stone > 0UL);
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;

        var plan = CanonicalLocalMarketDestinationPreflight.FromCurrent(
            game, new[]
            {
                Wood(game, wood),
                new CanonicalLocalMarketDestinationCandidate(
                    game.InitialTile.FineStrategicCellId.Value,
                    game.InitialTile.FineStrategicCellId.Value, 2U, stone,
                    CanonicalLocalMarketUsePurpose.WorkforceMaterialUse,
                    EconomicActivityKind.Mining)
            });
        plan.VerifyCurrent();
        Assert.Equal(new BigInteger(wood) + stone, plan.CandidateRawTotal);
        Assert.Equal(2, plan.Candidates.Count);
        Assert.True(plan.AllCandidatesHaveRealLocalRecipients);
        Assert.False(plan.PermitsUnprovenRemoteTransport);
        Assert.False(plan.ProvesExecutedFinalConsumption);
        Assert.False(plan.ProvesExecutedSale);
        Assert.False(plan.AuthorizesPaymentOrRevenue);
        Assert.False(plan.ChangesWorldStateOrSave);
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(before, game.ExportSaveJson());

        var reversed = CanonicalLocalMarketDestinationPreflight.FromCurrent(
            game, plan.Candidates.Reverse());
        Assert.Equal(plan.Candidates.ToArray(), reversed.Candidates.ToArray());
    }

    [Fact]
    public void EveryUnitCanBeProposedOnlyOnceWithoutDefaultRetention()
    {
        var game = Closed();
        var wood = game.StockAt(game.InitialTile, new CommodityId(3U));
        Assert.True(wood > 1UL);

        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketDestinationPreflight.FromCurrent(game,
                new[] { Wood(game, wood + 1UL) }));

        Assert.Throws<ArgumentException>(() =>
            CanonicalLocalMarketDestinationPreflight.FromCurrent(game,
                new[] { Wood(game, 1UL), Wood(game, 1UL) }));

        Assert.Throws<ArgumentException>(() =>
            CanonicalLocalMarketDestinationPreflight.FromCurrent(game,
                Array.Empty<CanonicalLocalMarketDestinationCandidate>()));

        var partial = CanonicalLocalMarketDestinationPreflight.FromCurrent(
            game, new[] { Wood(game, 1UL) });
        Assert.Equal(BigInteger.One, partial.CandidateRawTotal);
        Assert.False(partial.ProvesExecutedSale);
    }

    [Fact]
    public void MissingMaterialPurposeOrTransportEvidenceFailsClosed()
    {
        var game = Closed();
        var tile = game.InitialTile.FineStrategicCellId.Value;
        var remote = game.AdjacentWorkable(game.InitialTile).First()
            .FineStrategicCellId.Value;
        Assert.NotEqual(tile, remote);

        var transported = Wood(game, 1UL) with { DestinationFineTile = remote };
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketDestinationPreflight.FromCurrent(game,
                new[] { transported }));
        var wrongUse = new CanonicalLocalMarketDestinationCandidate(
            tile, tile, 3U, 1UL,
            CanonicalLocalMarketUsePurpose.ResidentFoodUse);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketDestinationPreflight.FromCurrent(game,
                new[] { wrongUse }));
        var nonexistentActivity = Wood(game, 1UL) with
        {
            BeneficiaryActivity = (EconomicActivityKind)255
        };
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketDestinationPreflight.FromCurrent(game,
                new[] { nonexistentActivity }));
        Assert.Throws<ArgumentException>(() =>
            CanonicalLocalMarketDestinationPreflight.FromCurrent(game,
                new[] { Wood(game, 0UL) }));
    }

    [Fact]
    public void ExplicitRetentionExcludesGoodsAndReleasingRestoresEligibility()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.StockAt(tile, new CommodityId(3U));
        Assert.True(wood > 0UL);
        var hold = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, wood);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalMarketDestinationPreflight.FromCurrent(
                game, new[] { Wood(game, wood) }));
        game.ReleasePhysicalStockCommitment(hold.ActionOrdinal);

        var plan = CanonicalLocalMarketDestinationPreflight.FromCurrent(
            game, new[] { Wood(game, wood) });
        var save = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var second = CanonicalLocalMarketDestinationPreflight.FromCurrent(
            replay, new[] { Wood(replay, wood) });
        Assert.Equal(plan.Candidates.ToArray(), second.Candidates.ToArray());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.False(second.ProvesExecutedSale);

        game.ReassignWorkers(tile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        Assert.Throws<InvalidOperationException>(plan.VerifyCurrent);
    }

    [Fact]
    public void AggregateFoodCanBeProposedOnlyAsAggregateNotFictionalVarieties()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        var origin = game.InitialTile;
        game.ReassignWorkers(origin,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 2UL);
        game.ReassignWorkers(origin,
            EconomicActivityKind.Mining, EconomicActivityKind.Agriculture, 2UL);
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        var food = game.StockAt(origin, new CommodityId(1U));
        Assert.True(food > 0UL);
        var candidate = new CanonicalLocalMarketDestinationCandidate(
            origin.FineStrategicCellId.Value, origin.FineStrategicCellId.Value,
            1U, food, CanonicalLocalMarketUsePurpose.ResidentFoodUse);
        var plan = CanonicalLocalMarketDestinationPreflight.FromCurrent(
            game, new[] { candidate });
        Assert.Equal(new BigInteger(food), plan.CandidateRawTotal);
        Assert.False(plan.ProvesExecutedFinalConsumption);
        Assert.False(plan.ProvesExecutedSale);
        Assert.False(plan.AuthorizesPaymentOrRevenue);
    }
}
