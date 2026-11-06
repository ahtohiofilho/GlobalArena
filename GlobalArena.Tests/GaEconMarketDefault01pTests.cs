using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01pTests
{
    private static readonly ExperimentalTransportInstitution Origin =
        new("primitive-supply-community",
            ExperimentalTransportInstitutionKind.Community);
    private static readonly ExperimentalTransportInstitution Recipient =
        new("real-workgroup-cooperative",
            ExperimentalTransportInstitutionKind.Cooperative);
    private static readonly ExperimentalTransportInstitution Council =
        new("neolithic-independent-council",
            ExperimentalTransportInstitutionKind.Community);

    // Tests do not provide a productive legal authority.
    private sealed class TestReviewer : ICanonicalLocalEconomicRightsReviewer,
        ICanonicalLocalSimulatedTitleReversalReviewer
    {
        public bool AcceptReadOnlyRight(
            CanonicalLocalEconomicRightClaim e,
            CanonicalLocalEconomicRecipientAttribution p) =>
            e.PhysicalTransferActionOrdinal == p.PhysicalTransferActionOrdinal &&
            e.AgreementId == p.AgreementId;
        public bool AcceptReadOnlyTitleReversal(
            CanonicalLocalSimulatedTitleReversalEvidence e,
            CanonicalRegisteredLocalSimulatedEconomicTitle t) =>
            e.OriginalTitleActionOrdinal == t.ActionOrdinal &&
            e.OriginalTitleAfterWorldHashHex == t.AfterWorldHashHex;
    }

    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    private static CanonicalRegisteredLocalSimulatedEconomicTitle Register(
        CanonicalPhysicalTacticalSession game, uint commodity, ulong raw,
        TestReviewer reviewer)
    {
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile, new CommodityId(commodity),
            CanonicalPhysicalCommitmentKind.MarketOffer, raw);
        var physical = game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal,
            commodity == 2U ? EconomicActivityKind.Mining
                : EconomicActivityKind.Forestry);
        var agreementId = $"physical-market-{offer.ActionOrdinal}";
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[]
            {
                new CanonicalLocalEconomicCounterpartyDeclaration(
                    agreementId, 1UL, physical.ActionOrdinal,
                    physical.AfterWorldHashHex, physical.FineTile,
                    physical.Commodity, physical.RawQuantity,
                    physical.RecipientActivity, physical.RecipientWorkers,
                    Origin, Recipient)
            });
        CanonicalLocalEconomicRightClaim Right(
            CanonicalLocalEconomicRightScope scope) =>
            new($"right-{offer.ActionOrdinal}-{(byte)scope}", 1UL,
                game.CurrentHash.HexDigest, physical.ActionOrdinal,
                physical.AfterWorldHashHex, agreementId, 1UL,
                scope, scope == CanonicalLocalEconomicRightScope.SourceOfferDisposition
                    ? Origin : Recipient,
                Council, game.State.MovementTurn,
                checked(game.State.MovementTurn + 10UL));
        var rights = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            game, parties, new[]
            {
                Right(CanonicalLocalEconomicRightScope.SourceOfferDisposition),
                Right(CanonicalLocalEconomicRightScope.RecipientPhysicalAcceptance)
            }, reviewer);
        return game.RegisterLocalSimulatedEconomicTitle(
            offer.ActionOrdinal, rights);
    }

    private static CanonicalReversedLocalSimulatedEconomicTitle Reverse(
        CanonicalPhysicalTacticalSession game,
        CanonicalRegisteredLocalSimulatedEconomicTitle title,
        TestReviewer reviewer)
    {
        var e = new CanonicalLocalSimulatedTitleReversalEvidence(
            title.ActionOrdinal,
            title.Evidence.SourceCommitmentActionOrdinal,
            title.AfterWorldHashHex, game.CurrentHash.HexDigest,
            $"reversal-{title.ActionOrdinal}", 1UL,
            $"reverse-consent-{title.ActionOrdinal}",
            $"accept-back-{title.ActionOrdinal}",
            Council, game.State.MovementTurn,
            checked(game.State.MovementTurn + 10UL));
        return game.ReverseLocalSimulatedEconomicTitle(
            title.ActionOrdinal, e, reviewer);
    }

    [Fact]
    public void RealTitledHandoffsReceiveMarginalIndexWithoutCommercialRevenue()
    {
        var game = Closed();
        var reviewer = new TestReviewer();
        var wood = Register(game, 3U, 4UL, reviewer);
        var stone = Register(game, 2U, 3UL, reviewer);
        var saved = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var reference = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, rawUnitsPerEconomicUnit: 1UL);

        Assert.Equal(2, reference.Markets.Count);
        Assert.Equal(new BigInteger(7),
            reference.TotalHistoricallyRegisteredTitleRaw);
        Assert.Equal(new BigInteger(7), reference.TotalCurrentlyTitledRaw);
        Assert.Equal(new BigInteger(7),
            reference.TotalStillInRecipientCustodyRaw);
        Assert.Equal(BigInteger.Zero, reference.TotalPhysicallyUsedByRecipientRaw);
        Assert.Equal(BigInteger.Zero, reference.TotalReversedTitleRaw);
        Assert.Equal((uint)2, reference.Markets[0].Commodity);
        Assert.Equal((uint)3, reference.Markets[1].Commodity);
        Assert.True(reference.Markets.All(x =>
            x.RealResidents > 0UL && x.NeutralDemandPressureRaw > 0 &&
            x.GentleHypotheticalGrossValueIndex.CompareTo(
                CanonicalEndogenousPriceIndex.Zero) > 0 &&
            x.SteepHypotheticalGrossValueIndex.CompareTo(
                CanonicalEndogenousPriceIndex.Zero) > 0));
        Assert.True(reference.UsesOnlyActualPhysicallyDeliveredJournaledLots);
        Assert.True(reference.UsesOnlyExecutedInGameEconomicTitleRegistrations);
        Assert.True(reference.IgnoresUnregisteredAndReversedTitleAsCurrentValuation);
        Assert.True(reference.UsesRealResidentAllocationsForNeutralDemandPressure);
        Assert.False(reference.IsPurchasingPowerCalibrationRatified);
        Assert.False(reference.CanCapPhysicalAbsorptionUsingBuyerDemand);
        Assert.False(reference.InventsBuyerBalancesOrLoans);
        Assert.False(reference.ProvesAnExecutedCommercialSale);
        Assert.False(reference.PostsProducerRevenueOrStateTaxes);
        Assert.False(reference.ChangesWorldSaveOrPhysicalLots);
        Assert.False(reference.ExecutesUnprovenRemoteTransport);
        Assert.All(reference.Markets, row =>
        {
            Assert.False(row.ReportsActualMonetaryRevenue);
            Assert.False(row.ProvesExecutedCommercialSale);
        });
        Assert.Equal(wood.Evidence.RawQuantity + stone.Evidence.RawQuantity,
            (ulong)reference.TotalCurrentlyTitledRaw);
        reference.RequireCurrent(game);
        reference.VerifyIndependent();
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);

        var reconstructed = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        var again = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            reconstructed, 1UL);
        Assert.Equal(reference.Markets.ToArray(), again.Markets.ToArray());
        Assert.Equal(reference.TotalGentleHypotheticalGrossValueIndex,
            again.TotalGentleHypotheticalGrossValueIndex);
        Assert.Equal(saved, reconstructed.ExportSaveJson());
    }

    [Fact]
    public void ActualWorkforceUseChangesCustodyNotHistoricalTitledVolume()
    {
        var game = Closed();
        var title = Register(game, 3U, 3UL, new TestReviewer());
        var old = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, 1UL);
        game.UseLocalRecipientMaterial(
            title.Evidence.SourceCommitmentActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        Assert.Throws<InvalidOperationException>(old.VerifyIndependent);

        var next = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, 1UL);
        Assert.Single(next.Markets);
        Assert.Equal(new BigInteger(3), next.TotalHistoricallyRegisteredTitleRaw);
        Assert.Equal(new BigInteger(3), next.TotalCurrentlyTitledRaw);
        Assert.Equal(BigInteger.Zero, next.TotalStillInRecipientCustodyRaw);
        Assert.Equal(new BigInteger(3), next.TotalPhysicallyUsedByRecipientRaw);
        Assert.Equal(BigInteger.Zero, next.TotalReversedTitleRaw);
        Assert.True(next.TotalGentleHypotheticalGrossValueIndex.CompareTo(
            CanonicalEndogenousPriceIndex.Zero) > 0);
        Assert.False(next.ProvesAnExecutedCommercialSale);
        Assert.False(next.PostsProducerRevenueOrStateTaxes);
        next.VerifyIndependent();
        var saved = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(next.Markets.ToArray(),
            CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
                replay, 1UL).Markets.ToArray());
    }

    [Fact]
    public void ReversalExcludesTitleFromExposureEvenBeforePhysicalReturn()
    {
        var game = Closed();
        var reviewer = new TestReviewer();
        var title = Register(game, 2U, 4UL, reviewer);
        var before = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, 1UL);
        Assert.Equal(new BigInteger(4), before.TotalCurrentlyTitledRaw);

        Reverse(game, title, reviewer);
        Assert.Throws<InvalidOperationException>(before.VerifyIndependent);
        var reversed = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, 1UL);
        Assert.Equal(new BigInteger(4), reversed.TotalHistoricallyRegisteredTitleRaw);
        Assert.Equal(new BigInteger(4), reversed.TotalReversedTitleRaw);
        Assert.Equal(BigInteger.Zero, reversed.TotalCurrentlyTitledRaw);
        Assert.Equal(BigInteger.Zero, reversed.TotalStillInRecipientCustodyRaw);
        Assert.Equal(BigInteger.Zero, reversed.TotalPhysicallyUsedByRecipientRaw);
        Assert.Equal(CanonicalEndogenousPriceIndex.Zero,
            reversed.TotalGentleHypotheticalGrossValueIndex);
        Assert.Equal(CanonicalEndogenousPriceIndex.Zero,
            reversed.TotalSteepHypotheticalGrossValueIndex);
        var previous = game.StockAt(game.InitialTile, new CommodityId(2U));
        game.ReturnLocalRecipientCustody(
            title.Evidence.SourceCommitmentActionOrdinal);
        var returned = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, 1UL);
        Assert.Equal(reversed.Markets.ToArray(), returned.Markets.ToArray());
        Assert.Equal(previous + title.Evidence.RawQuantity,
            game.StockAt(game.InitialTile, new CommodityId(2U)));
        Assert.False(returned.ProvesAnExecutedCommercialSale);
        returned.VerifyIndependent();
        var json = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(returned.Markets.ToArray(),
            CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
                replay, 1UL).Markets.ToArray());
        Assert.Equal(json, replay.ExportSaveJson());
    }

    [Fact]
    public void UnregisteredPhysicalHandoffCannotBePretendedToBePricedTitle()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 4UL);
        game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal, EconomicActivityKind.Forestry);
        var saved = game.ExportSaveJson();

        var reference = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, 1UL);
        Assert.Empty(reference.Markets);
        Assert.Equal(BigInteger.Zero, reference.TotalCurrentlyTitledRaw);
        Assert.Equal(CanonicalEndogenousPriceIndex.Zero,
            reference.TotalGentleHypotheticalGrossValueIndex);
        Assert.False(reference.ProvesAnExecutedCommercialSale);
        reference.VerifyIndependent();
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(game, 0UL));
        Assert.Throws<ArgumentNullException>(() =>
            CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(null!, 1UL));
        var foreign = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Throws<InvalidOperationException>(() =>
            reference.RequireCurrent(foreign));
    }

    [Fact]
    public void MixedTitledConsumedReversedAndUnregisteredLotsReconcile()
    {
        var game = Closed();
        var reviewer = new TestReviewer();
        var wood = Register(game, 3U, 2UL, reviewer);
        var stone = Register(game, 2U, 3UL, reviewer);
        var another = game.CommitPhysicalStock(game.InitialTile,
            new CommodityId(3U), CanonicalPhysicalCommitmentKind.MarketOffer,
            1UL);
        game.TransferMarketOfferToLocalRecipient(
            another.ActionOrdinal, EconomicActivityKind.Forestry);
        game.UseLocalRecipientMaterial(
            wood.Evidence.SourceCommitmentActionOrdinal,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        Reverse(game, stone, reviewer);
        var reference = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, 2UL);

        Assert.Equal(new BigInteger(5),
            reference.TotalHistoricallyRegisteredTitleRaw);
        Assert.Equal(new BigInteger(3), reference.TotalReversedTitleRaw);
        Assert.Equal(new BigInteger(2), reference.TotalCurrentlyTitledRaw);
        Assert.Equal(new BigInteger(2), reference.TotalPhysicallyUsedByRecipientRaw);
        Assert.Equal(BigInteger.Zero, reference.TotalStillInRecipientCustodyRaw);
        Assert.Equal(new BigInteger(4), reference.Markets.Single(x =>
            x.Commodity == 3U).NeutralDemandPressureRaw /
            reference.Markets.Single(x => x.Commodity == 3U).RealResidents * 2);
        Assert.False(reference.ProvesAnExecutedCommercialSale);
        Assert.False(reference.PostsProducerRevenueOrStateTaxes);
        reference.VerifyIndependent();
        var json = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(reference.Markets.ToArray(),
            CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
                replay, 2UL).Markets.ToArray());
    }
}
