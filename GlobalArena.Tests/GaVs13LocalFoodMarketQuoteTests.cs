using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs13LocalFoodMarketQuoteTests
{
    private static CanonicalPhysicalTacticalSession NewGame() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C);

    [Fact]
    public void QuoteUsesOnlyLocalPhysicalOutputAndDoesNotChangeCanonicalWorldOrSave()
    {
        var session = NewGame();
        var before = session.Game.Snapshot;
        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();
        var initial = session.QuoteNextLocalFoodMarket(session.InitialTile, 40.0);

        Assert.Equal(8UL, initial.Workers);
        Assert.Equal(session.InitialTile, initial.Tile);
        Assert.Equal(0UL, initial.RawAvailableStock);
        Assert.True(initial.RawProjectedCropOutput > 0UL);
        Assert.Equal(4_000_000UL, initial.RawFoodNeed);
        Assert.True(initial.ReferenceSupply > 0.0);
        Assert.True(initial.PricePerWholeFoodUnit >= 0.0);
        Assert.InRange(initial.ReferenceSold, 0.0, initial.ReferenceSupply);
        Assert.True(initial.ReferenceBuyerSpending <= 320.0 + 1e-6);
        Assert.True(initial.ReferenceBudgetResidual > 0.0);
        Assert.Equal(initial.ReferenceBuyerSpending, initial.ReferenceSellerReceipts, 6);

        var repeated = session.QuoteNextLocalFoodMarket(session.InitialTile, 40.0);
        Assert.Equal(initial, repeated);
        Assert.Same(before, session.Game.Snapshot);
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Empty(session.Events);
        Assert.Equal(0UL, session.State.MovementTurn);
    }

    [Fact]
    public void SingleLocalBuyerWithScarceFoodMustGeneratePositivePriceAndPayment()
    {
        // Reproduces the Windows VS13 screenshot: 3.546 units, eight workers,
        // hypothetical purchasing power 40 each, demand 4 units.
        // Before the correction, the bisection yielded price=0, sold=3.546,
        // spending=0 and H=320 because demand was capped at the supply.
        var game = NewGame();
        var before = game.Game.Snapshot;
        var initialHash = game.CurrentHash;
        var quote = new EconomicLocalFoodMarketQuoteResolver().Resolve(
            game.InitialTile, 0UL, 8UL, 0UL, 3_546_000UL,
            4_000_000UL, 40.0);

        Assert.InRange(quote.PricePerWholeFoodUnit, 60.44, 60.45);
        Assert.InRange(quote.ReferenceSold, 3.546 - 1e-8, 3.546 + 1e-8);
        Assert.InRange(quote.ReferenceBuyerSpending, 214.32, 214.34);
        Assert.InRange(quote.ReferenceBudgetResidual, 105.65, 105.69);
        Assert.InRange(quote.ReferenceUnsold, 0.0, 1e-8);
        Assert.Equal(quote.ReferenceBuyerSpending, quote.ReferenceSellerReceipts, 8);
        Assert.InRange(Math.Abs(quote.ReferenceBudgetResidual +
            quote.ReferenceBuyerSpending - 320.0), 0.0, 1e-8);
        var ratio = quote.ReferenceBudgetResidual / 16.0;
        var marginalWtp = ratio * ratio * 2.0 / (1.0 + quote.ReferenceSold / 8.0);
        Assert.InRange(Math.Abs(marginalWtp - quote.PricePerWholeFoodUnit), 0, 1e-7);

        // The price quote is not the actual settlement.
        Assert.Same(before, game.Game.Snapshot);
        Assert.Equal(initialHash, game.CurrentHash);
        Assert.Empty(game.Events);
    }

    [Fact]
    public void QuoteDoesNotGrantLocalTransportOrFoodToEmptySupplyTile()
    {
        var session = NewGame();
        var neighbor = session.AdjacentWorkable(session.InitialTile)[0];
        session.MoveWorkers(session.InitialTile, neighbor,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);

        var snapshot = session.Game.Snapshot;
        var hash = session.CurrentHash;
        var quoted = session.QuoteNextLocalFoodMarket(neighbor, 40.0);
        Assert.Equal(3UL, quoted.Workers);
        Assert.Equal(0UL, quoted.RawAvailableStock);
        Assert.Equal(0UL, quoted.RawProjectedCropOutput);
        Assert.Equal(1_500_000UL, quoted.RawFoodNeed);
        Assert.Equal(0.0, quoted.ReferenceSold);
        Assert.Equal(0.0, quoted.PricePerWholeFoodUnit);
        Assert.Equal(120.0, quoted.ReferenceBudgetResidual);
        Assert.Same(snapshot, session.Game.Snapshot);
        Assert.Equal(hash, session.CurrentHash);
    }

    [Fact]
    public void PriceSolverAuditsBudgetAndDoesNotGuaranteeRevenue()
    {
        var resolver = new EconomicLocalFoodMarketQuoteResolver();
        var tile = NewGame().InitialTile;
        var less = resolver.Resolve(tile, 0UL, 8UL, 0UL,
            3_000_000UL, 4_000_000UL, 10.0);
        var more = resolver.Resolve(tile, 0UL, 8UL, 0UL,
            3_000_000UL, 4_000_000UL, 100.0);
        Assert.InRange(less.ReferenceSold, 0.0, less.ReferenceSupply);
        Assert.InRange(more.ReferenceSold, 0.0, more.ReferenceSupply);
        Assert.True(more.PricePerWholeFoodUnit >= less.PricePerWholeFoodUnit);
        Assert.True(less.ReferenceBuyerSpending <= 80.0);
        Assert.True(more.ReferenceBuyerSpending <= 800.0);
        Assert.True(less.ReferenceBudgetResidual > 0);
        Assert.True(more.ReferenceBudgetResidual > 0);

        var oversupplied = resolver.Resolve(tile, 0UL, 8UL, 0UL,
            1_000_000_000UL, 4_000_000UL, 1.0);
        Assert.True(oversupplied.PricePerWholeFoodUnit >= 0.0);
        Assert.True(oversupplied.PricePerWholeFoodUnit < less.PricePerWholeFoodUnit);
        Assert.True(oversupplied.ReferenceBuyerSpending <= 8.0 + 1e-6);
        Assert.InRange(oversupplied.ReferenceSold, 0.0, oversupplied.ReferenceSupply);
    }

    [Fact]
    public void InvalidBudgetsAreRejectedWithoutAnyMonetaryPosting()
    {
        var resolver = new EconomicLocalFoodMarketQuoteResolver();
        var tile = NewGame().InitialTile;
        foreach (var budget in new[] { -1.0, 0.0, double.NaN, double.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                resolver.Resolve(tile, 0UL, 8UL, 0UL,
                    1_000_000UL, 4_000_000UL, budget));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            resolver.Resolve(tile, 0UL, 0UL, 0UL, 0UL, 0UL, 40.0));
    }

    [Fact]
    public void PhysicalTurnAndV11ReplayAreUnaffectedByPreview()
    {
        var game = NewGame();
        var before = game.QuoteNextLocalFoodMarket(game.InitialTile, 40.0);
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        var after = game.QuoteNextLocalFoodMarket(game.InitialTile, 40.0);
        Assert.True(after.RawAvailableStock >= 0UL);
        Assert.Equal(before.RawProjectedCropOutput, after.RawProjectedCropOutput);
        var save = game.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(after, reopened.QuoteNextLocalFoodMarket(reopened.InitialTile, 40.0));
    }
}
