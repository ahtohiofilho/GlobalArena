using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01gTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        return session;
    }

    [Fact]
    public void OnlyRealCommittedMarketCustodyEntersMarginalValueReference()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.StockAt(tile, new CommodityId(3U));
        var stone = game.StockAt(tile, new CommodityId(2U));
        Assert.True(wood > 10UL);
        Assert.True(stone > 10UL);
        var initialHash = game.CurrentHash;
        var oldSave = game.ExportSaveJson();

        // Mere locally produced stock is NOT an executed offer.
        var withoutOffers = CanonicalCommittedOfferMarginalReference.FromCurrent(
            game, 1_000_000UL);
        Assert.Empty(withoutOffers.Markets);
        Assert.Equal(BigInteger.Zero, withoutOffers.TotalActuallyCommittedOfferRaw);
        Assert.Equal(0, withoutOffers.GentleTotalRevenueIndex.CompareTo(
            CanonicalEndogenousPriceIndex.Zero));
        Assert.Equal(oldSave, game.ExportSaveJson());

        var heldWood = wood / 3UL;
        game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, heldWood);
        var availableWood = game.StockAt(tile, new CommodityId(3U));
        var offeredWood = availableWood / 2UL;
        game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, offeredWood);
        game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, stone);

        var hash = game.CurrentHash;
        var before = game.ExportSaveJson();
        var result = CanonicalCommittedOfferMarginalReference.FromCurrent(
            game, 1_000_000UL);
        Assert.Equal(2, result.Markets.Count);
        Assert.Equal(new BigInteger(offeredWood) + stone,
            result.TotalActuallyCommittedOfferRaw);
        var woodIndex = result.Markets.Single(x => x.Commodity == 3U);
        Assert.Equal(tile.FineStrategicCellId.Value, woodIndex.FineTile);
        Assert.Equal(game.WorkersAt(tile), woodIndex.RealResidents);
        Assert.Equal((BigInteger)game.WorkersAt(tile) * 1_000_000UL,
            woodIndex.NeutralDemandPressureRaw);
        Assert.Equal(new BigInteger(offeredWood), woodIndex.LiveMarketOfferRaw);
        Assert.Equal(new BigInteger(heldWood),
            woodIndex.DeliberatelyRetainedRaw);
        Assert.Equal(new BigInteger(availableWood - offeredWood),
            woodIndex.UncommittedLocalRaw);
        Assert.Equal(1_000_000UL, result.RawUnitsPerEconomicUnit);
        Assert.True(result.UsesRealJournaledMarketOfferCustody);
        Assert.False(result.TreatsUncommittedStockAsExecutedOffer);
        Assert.False(result.TreatsDemandPressureAsPhysicalSaleCap);
        Assert.True(result.UsesNeutralUncalibratedPurchasingPower);
        Assert.False(result.HasAuthenticatedPurchasingPowerN);
        Assert.False(result.HasCalibratedNominalUnitPrices);
        Assert.False(result.ClaimsAllOffersHavePhysicalDestinations);
        Assert.False(result.ProvesExecutedSaleOrPayment);
        Assert.False(result.AuthorizesRevenueOrTaxPosting);
        Assert.False(result.MutatesCanonicalWorldOrSave);
        Assert.True(result.GentleTotalRevenueIndex.CompareTo(
            CanonicalEndogenousPriceIndex.Zero) > 0);
        Assert.True(result.SteepTotalRevenueIndex.CompareTo(
            CanonicalEndogenousPriceIndex.Zero) > 0);
        result.VerifyIndependent();
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.NotEqual(initialHash, hash);
    }

    [Fact]
    public void FullBatchSupplyIsQuotedWithoutAnyPhysicalDemandCap()
    {
        var game = Closed();
        var staged = CanonicalStagedAutomaticMarketOfferBatch.Prepare(game);
        var sourceSave = game.ExportSaveJson();
        var reference = CanonicalCommittedOfferMarginalReference.FromCurrent(
            staged.PreparedSession, 1_000_000UL);
        var pool = CanonicalDefaultMarketPoolPreflight.FromCurrent(
            staged.PreparedSession);

        Assert.Equal(pool.EligibleByDefaultRaw,
            reference.TotalActuallyCommittedOfferRaw);
        Assert.Equal(staged.TotalEligibleRaw,
            reference.TotalActuallyCommittedOfferRaw);
        Assert.All(reference.Markets, x =>
        {
            Assert.True(x.RealResidents > 0UL);
            Assert.True(x.LiveMarketOfferRaw > 0);
            Assert.Equal(BigInteger.Zero, x.UncommittedLocalRaw);
            Assert.True(x.GentleRevenueIndex.CompareTo(
                CanonicalEndogenousPriceIndex.Zero) > 0);
            Assert.True(x.SteepRevenueIndex.CompareTo(
                CanonicalEndogenousPriceIndex.Zero) > 0);
        });
        Assert.Equal(sourceSave, game.ExportSaveJson());
        Assert.False(reference.ProvesExecutedSaleOrPayment);

        var saved = staged.PreparedSession.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        var second = CanonicalCommittedOfferMarginalReference.FromCurrent(
            replay, 1_000_000UL);
        Assert.Equal(reference.Markets.ToArray(), second.Markets.ToArray());
        Assert.Equal(reference.TotalActuallyCommittedOfferRaw,
            second.TotalActuallyCommittedOfferRaw);
        Assert.Equal(0, reference.GentleTotalRevenueIndex.CompareTo(
            second.GentleTotalRevenueIndex));
        Assert.Equal(saved, replay.ExportSaveJson());
    }

    [Fact]
    public void ExistingDyadicCurveHasPositiveDecreasingMarginalReturnsAbovePressure()
    {
        var pressure = new BigInteger(5);
        var a = CanonicalEndogenousLocalPriceComparison.Calculate(
            new BigInteger(20), pressure, 1);
        var b = CanonicalEndogenousLocalPriceComparison.Calculate(
            new BigInteger(1000), pressure, 1);
        Assert.True(b.Revenue.CompareTo(a.Revenue) > 0);
        Assert.True(b.Last.CompareTo(a.Last) < 0);
        Assert.True(b.Average.CompareTo(a.Average) < 0);
        Assert.True(b.Revenue.CompareTo(
            CanonicalEndogenousPriceIndex.Zero) > 0);

        var steeper = CanonicalEndogenousLocalPriceComparison.Calculate(
            new BigInteger(1000), pressure, 2);
        Assert.True(steeper.Revenue.CompareTo(
            CanonicalEndogenousPriceIndex.Zero) > 0);
        Assert.True(steeper.Revenue.CompareTo(b.Revenue) < 0);
        Assert.True(steeper.Last.CompareTo(b.Last) < 0);
    }

    [Fact]
    public void ForeignOrStaleQuoteCannotAuthorizeAnything()
    {
        var game = Closed();
        var offer = game.CommitPhysicalStock(game.InitialTile,
            new CommodityId(2U), CanonicalPhysicalCommitmentKind.MarketOffer,
            1UL);
        var view = CanonicalCommittedOfferMarginalReference.FromCurrent(
            game, 1_000_000UL);
        var saved = game.ExportSaveJson();
        var other = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Throws<InvalidOperationException>(() => view.RequireCurrent(other));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalCommittedOfferMarginalReference.FromCurrent(game, 0UL));
        Assert.False(view.AuthorizesRevenueOrTaxPosting);

        game.ReleasePhysicalStockCommitment(offer.ActionOrdinal);
        Assert.Throws<InvalidOperationException>(view.VerifyIndependent);
        Assert.Throws<InvalidOperationException>(() => view.RequireCurrent(game));
    }

    [Fact]
    public void GodotModeRealOffersCanBeValuedWithoutUsingLegacyCashAsN()
    {
        var initial = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);
        var oldSave = initial.ExportSaveJson();
        var timeline = CanonicalAutomaticMarketOfferTimeline.StartOptIn(initial);
        var result = timeline.AdvanceMovementTurn();
        Assert.True(result.ClosedEconomicCycle);
        Assert.True(result.NewlyOfferedRaw > 0);
        var snapshot = timeline.CaptureCanonicalSnapshot();
        var price = CanonicalCommittedOfferMarginalReference.FromCurrent(
            snapshot, 1_000_000UL);
        Assert.Equal(result.NewlyOfferedRaw, price.TotalActuallyCommittedOfferRaw);
        Assert.False(price.HasAuthenticatedPurchasingPowerN);
        Assert.False(price.ProvesExecutedSaleOrPayment);
        Assert.False(price.AuthorizesRevenueOrTaxPosting);
        Assert.Equal(oldSave, initial.ExportSaveJson());
    }
}
