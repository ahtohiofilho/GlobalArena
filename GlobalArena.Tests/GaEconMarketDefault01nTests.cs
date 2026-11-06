using System.Numerics;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01nTests
{
    private static readonly ExperimentalTransportInstitution Source =
        new("neolithic-producer-community",
            ExperimentalTransportInstitutionKind.Community);
    private static readonly ExperimentalTransportInstitution Recipient =
        new("neolithic-artisans",
            ExperimentalTransportInstitutionKind.Cooperative);
    private static readonly ExperimentalTransportInstitution ReviewerInstitution =
        new("tribal-council",
            ExperimentalTransportInstitutionKind.Community);

    private sealed class TestRightsReviewer : ICanonicalLocalEconomicRightsReviewer
    {
        public bool Allowed { get; set; } = true;
        public bool AcceptReadOnlyRight(
            CanonicalLocalEconomicRightClaim claim,
            CanonicalLocalEconomicRecipientAttribution party) =>
            Allowed && claim.PhysicalTransferActionOrdinal ==
                party.PhysicalTransferActionOrdinal &&
            claim.PhysicalTransferAfterWorldHashHex ==
                party.PhysicalTransferAfterWorldHashHex &&
            claim.AgreementId == party.AgreementId;
    }

    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    private static (
        CanonicalExecutedLocalRecipientCustodyTransfer Transfer,
        CanonicalLocalEconomicRightsReviewPreflight Rights,
        TestRightsReviewer Reviewer) HandedOffWithReviewedRights(
            CanonicalPhysicalTacticalSession game,
            uint commodity = 3U, ulong quantity = 4UL)
    {
        var tile = game.InitialTile;
        var hold = game.CommitPhysicalStock(
            tile, new CommodityId(commodity),
            CanonicalPhysicalCommitmentKind.MarketOffer, quantity);
        var transferred = game.TransferMarketOfferToLocalRecipient(
            hold.ActionOrdinal, commodity == 2U
                ? EconomicActivityKind.Mining : EconomicActivityKind.Forestry);
        var claim = new CanonicalLocalEconomicCounterpartyDeclaration(
            "title-agreement-01", 1UL, transferred.ActionOrdinal,
            transferred.AfterWorldHashHex, transferred.FineTile,
            transferred.Commodity, transferred.RawQuantity,
            transferred.RecipientActivity, transferred.RecipientWorkers,
            Source, Recipient);
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { claim });
        CanonicalLocalEconomicRightClaim Right(
            string id, CanonicalLocalEconomicRightScope scope) =>
            new(id, 1UL, game.CurrentHash.HexDigest,
                transferred.ActionOrdinal, transferred.AfterWorldHashHex,
                claim.AgreementId, claim.AgreementVersion, scope,
                scope == CanonicalLocalEconomicRightScope.SourceOfferDisposition
                    ? Source : Recipient,
                ReviewerInstitution, game.State.MovementTurn,
                checked(game.State.MovementTurn + 10UL));
        var reviewer = new TestRightsReviewer();
        var rights = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            game, parties, new[]
            {
                Right("source-authorization-01",
                    CanonicalLocalEconomicRightScope.SourceOfferDisposition),
                Right("recipient-authorization-01",
                    CanonicalLocalEconomicRightScope.RecipientPhysicalAcceptance)
            }, reviewer);
        return (transferred, rights, reviewer);
    }

    [Fact]
    public void RegistersTrueInGameTitleOverExistingRealCustodyWithoutSaleOrPayment()
    {
        var game = Closed();
        var beforeWood = game.StockAt(game.InitialTile, new CommodityId(3U));
        var (transfer, rights, _) = HandedOffWithReviewedRights(game);
        var freeAfterPhysicalDelivery =
            game.StockAt(game.InitialTile, new CommodityId(3U));
        var priorHash = game.CurrentHash;

        var title = game.RegisterLocalSimulatedEconomicTitle(
            transfer.SourceCommitmentActionOrdinal, rights);
        Assert.Equal(freeAfterPhysicalDelivery,
            game.StockAt(game.InitialTile, new CommodityId(3U)));
        Assert.Equal(beforeWood - 4UL, freeAfterPhysicalDelivery);
        Assert.NotEqual(priorHash, game.CurrentHash);
        Assert.Equal("register_local_economic_title", game.Events[^1].Kind);
        Assert.Equal(title.Evidence, game.Events[^1].TitleEvidence);
        Assert.Equal(title.ActionOrdinal, game.Events.Count);
        Assert.Equal(title.ActionOrdinal,
            game.RegisteredLocalSimulatedEconomicTitles.Single().ActionOrdinal);
        Assert.Equal(transfer.ActionOrdinal, title.Evidence.PhysicalTransferActionOrdinal);
        Assert.Equal(transfer.AfterWorldHashHex,
            title.Evidence.PhysicalTransferAfterWorldHashHex);
        Assert.Equal(transfer.RawQuantity, title.Evidence.RawQuantity);
        Assert.Equal(Source, title.PreviousRecognizedEconomicHolder);
        Assert.Equal(Recipient, title.CurrentRecognizedEconomicHolder);
        Assert.True(title.IsRegisteredInGameEconomicTitle);
        Assert.False(title.AuthenticatesLegallyEnforceableOwnership);
        Assert.False(title.ConstitutesExecutedCommercialSale);
        Assert.False(title.ConstitutesPaymentOrSettlement);
        Assert.False(title.CreatesMoneyTaxOrBuyerWallets);
        Assert.False(title.MovesOrConsumesPhysicalGoods);
        Assert.False(title.ReplayCanReapproveExternalGrants);
        Assert.True(game.PhysicalStockCommitments.Single().Transferred);
        Assert.Single(game.ExecutedLocalRecipientCustodyTransfers);
        Assert.Empty(game.ExecutedLocalRecipientCustodyDispositions);

        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Equal(new BigInteger(4), coverage.TotalTransferredToRecipientRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalReturnedFromRecipientRaw);
        Assert.False(coverage.ProvesCompletedMarketSales);
        Assert.False(coverage.CanAuthorizeMonetaryRevenueOrTax);
        coverage.VerifyIndependent();

        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.SimulatedEconomicTitleEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!.Version);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(game.Events.ToArray(), replay.Events.ToArray());
        Assert.Equal(title,
            replay.RegisteredLocalSimulatedEconomicTitles.Single());
        Assert.Equal(coverage.Markets.ToArray(),
            CanonicalMarketExecutedPhysicalCoverage.FromCurrent(replay)
                .Markets.ToArray());
    }

    [Fact]
    public void RegistrationsBlockImplicitReturnButPermitActualMaterialUse()
    {
        var game = Closed();
        var (transfer, rights, _) = HandedOffWithReviewedRights(
            game, commodity: 2U, quantity: 3UL);
        game.RegisterLocalSimulatedEconomicTitle(
            transfer.SourceCommitmentActionOrdinal, rights);
        var saveBefore = game.ExportSaveJson();

        Assert.Throws<InvalidOperationException>(() =>
            game.ReturnLocalRecipientCustody(
                transfer.SourceCommitmentActionOrdinal));
        Assert.Equal(saveBefore, game.ExportSaveJson());

        var used = game.UseLocalRecipientMaterial(
            transfer.SourceCommitmentActionOrdinal,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        Assert.True(used.ProvesExecutedMaterialUse);
        Assert.Equal(3UL, used.RawQuantity);
        Assert.Single(game.RegisteredLocalSimulatedEconomicTitles);
        Assert.False(game.RegisteredLocalSimulatedEconomicTitles.Single()
            .ConstitutesExecutedCommercialSale);
        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Equal(new BigInteger(3), coverage.TotalExecutedRecipientMaterialRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalTransferredToRecipientRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalPendingOfferRaw);
        var save = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Single(replay.RegisteredLocalSimulatedEconomicTitles);
        Assert.Single(replay.ExecutedLocalRecipientCustodyDispositions);
        Assert.Throws<InvalidOperationException>(() =>
            replay.ReturnLocalRecipientCustody(
                transfer.SourceCommitmentActionOrdinal));
    }

    [Fact]
    public void IncompleteOrRevokedRightsCannotAlterGameTitleOrJournal()
    {
        var game = Closed();
        var (transfer, rights, reviewer) = HandedOffWithReviewedRights(game);
        reviewer.Allowed = false;
        var before = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.RegisterLocalSimulatedEconomicTitle(
                transfer.SourceCommitmentActionOrdinal, rights));
        Assert.Empty(game.RegisteredLocalSimulatedEconomicTitles);
        Assert.Equal(before, game.ExportSaveJson());

        reviewer.Allowed = true;
        rights.VerifyIndependent();
        game.RegisterLocalSimulatedEconomicTitle(
            transfer.SourceCommitmentActionOrdinal, rights);
        var after = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.RegisterLocalSimulatedEconomicTitle(
                transfer.SourceCommitmentActionOrdinal, rights));
        Assert.Equal(after, game.ExportSaveJson());
        Assert.Single(game.RegisteredLocalSimulatedEconomicTitles);
    }

    [Fact]
    public void OldSaveAndWrongEnvelopeCannotAcquireInGameTitle()
    {
        var game = Closed();
        var (transfer, rights, _) = HandedOffWithReviewedRights(game);
        var oldSave = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.LocalRecipientCustodyEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(oldSave)!.Version);
        Assert.Equal(oldSave,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(oldSave).ExportSaveJson());
        game.RegisterLocalSimulatedEconomicTitle(
            transfer.SourceCommitmentActionOrdinal, rights);
        var newSave = game.ExportSaveJson();
        Assert.NotEqual(oldSave, newSave);

        var formerMislabel = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(oldSave)!
                with { Version =
                    CanonicalPhysicalTacticalSession.SimulatedEconomicTitleEnvelopeFormat });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(formerMislabel));

        var newMislabel = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(newSave)!
                with { Version =
                    CanonicalPhysicalTacticalSession.LocalRecipientCustodyEnvelopeFormat });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(newMislabel));
        Assert.Equal(newSave, game.ExportSaveJson());
    }

    [Fact]
    public void DormantTreasuryDoesNotCreateFundsAndUsesSeparateEnvelope()
    {
        var game = Closed();
        game.EnableDormantTreasuryReservation();
        var (transfer, rights, _) = HandedOffWithReviewedRights(game);
        game.RegisterLocalSimulatedEconomicTitle(
            transfer.SourceCommitmentActionOrdinal, rights);
        var json = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.SimulatedEconomicTitleWithTreasuryEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)!.Version);
        var roundtrip = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(json, roundtrip.ExportSaveJson());
        Assert.Equal(game.CurrentHash, roundtrip.CurrentHash);
        Assert.Single(roundtrip.RegisteredLocalSimulatedEconomicTitles);
    }
}
