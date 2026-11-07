using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01qTests
{
    private static readonly ExperimentalTransportInstitution Source =
        new("primitive-producers", ExperimentalTransportInstitutionKind.Community);
    private static readonly ExperimentalTransportInstitution Recipient =
        new("primitive-cooperative", ExperimentalTransportInstitutionKind.Cooperative);
    private static readonly ExperimentalTransportInstitution Council =
        new("independent-neolithic-council",
            ExperimentalTransportInstitutionKind.Community);

    // This is a TEST STUB, not an authenticated production or legal reviewer.
    private sealed class TestReviewer :
        ICanonicalLocalEconomicRightsReviewer,
        ICanonicalLocalSimulatedTitleReversalReviewer,
        ICanonicalLocalCommercialTermsReviewer
    {
        public bool Accept { get; set; } = true;
        public int TermsCalls { get; private set; }

        public bool AcceptReadOnlyRight(
            CanonicalLocalEconomicRightClaim e,
            CanonicalLocalEconomicRecipientAttribution p) =>
            Accept && e.PhysicalTransferActionOrdinal ==
                p.PhysicalTransferActionOrdinal && e.AgreementId == p.AgreementId;

        public bool AcceptReadOnlyTitleReversal(
            CanonicalLocalSimulatedTitleReversalEvidence e,
            CanonicalRegisteredLocalSimulatedEconomicTitle t) =>
            Accept && e.OriginalTitleActionOrdinal == t.ActionOrdinal &&
                e.OriginalTitleAfterWorldHashHex == t.AfterWorldHashHex;

        public bool AcceptReadOnlyCommercialTerms(
            CanonicalAcceptedLocalCommercialTermsEvidence e,
            CanonicalRegisteredLocalSimulatedEconomicTitle t)
        {
            TermsCalls++;
            return Accept && e.RegisteredTitleActionOrdinal == t.ActionOrdinal &&
                e.OriginalTitleAfterWorldHashHex == t.AfterWorldHashHex;
        }
    }

    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    private static CanonicalRegisteredLocalSimulatedEconomicTitle Title(
        CanonicalPhysicalTacticalSession game, uint commodity, ulong amount,
        TestReviewer reviewer)
    {
        var commitment = game.CommitPhysicalStock(game.InitialTile,
            new CommodityId(commodity), CanonicalPhysicalCommitmentKind.MarketOffer,
            amount);
        var delivered = game.TransferMarketOfferToLocalRecipient(
            commitment.ActionOrdinal,
            commodity == 2U ? EconomicActivityKind.Mining
                : EconomicActivityKind.Forestry);
        var agreementId = $"title-intent-{commitment.ActionOrdinal}";
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[]
            {
                new CanonicalLocalEconomicCounterpartyDeclaration(
                    agreementId, 1UL, delivered.ActionOrdinal,
                    delivered.AfterWorldHashHex, delivered.FineTile,
                    delivered.Commodity, delivered.RawQuantity,
                    delivered.RecipientActivity, delivered.RecipientWorkers,
                    Source, Recipient)
            });
        CanonicalLocalEconomicRightClaim Grant(CanonicalLocalEconomicRightScope scope) =>
            new($"grant-{commitment.ActionOrdinal}-{(byte)scope}", 1UL,
                game.CurrentHash.HexDigest, delivered.ActionOrdinal,
                delivered.AfterWorldHashHex, agreementId, 1UL,
                scope, scope ==
                    CanonicalLocalEconomicRightScope.SourceOfferDisposition
                        ? Source : Recipient,
                Council, game.State.MovementTurn,
                checked(game.State.MovementTurn + 10UL));
        var rights = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            game, parties, new[]
            {
                Grant(CanonicalLocalEconomicRightScope.SourceOfferDisposition),
                Grant(CanonicalLocalEconomicRightScope.RecipientPhysicalAcceptance)
            }, reviewer);
        return game.RegisterLocalSimulatedEconomicTitle(
            commitment.ActionOrdinal, rights);
    }

    private static CanonicalRecordedLocalCommercialTerms AcceptTerms(
        CanonicalPhysicalTacticalSession game,
        CanonicalRegisteredLocalSimulatedEconomicTitle title,
        TestReviewer reviewer,
        CanonicalEndogenousCurveVariant curve =
            CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand) =>
        game.AcceptLocalCommercialTerms(
            title.ActionOrdinal, $"terms-{title.ActionOrdinal}", 1UL, 2UL,
            curve, $"source-terms-{title.ActionOrdinal}",
            $"recipient-terms-{title.ActionOrdinal}", Council, reviewer);

    private static CanonicalLocalSimulatedTitleReversalEvidence Reversal(
        CanonicalPhysicalTacticalSession game,
        CanonicalRegisteredLocalSimulatedEconomicTitle title) =>
        new(title.ActionOrdinal,
            title.Evidence.SourceCommitmentActionOrdinal,
            title.AfterWorldHashHex, game.CurrentHash.HexDigest,
            $"reversal-{title.ActionOrdinal}", 1UL,
            $"reverse-permission-{title.ActionOrdinal}",
            $"return-acceptance-{title.ActionOrdinal}",
            Council, game.State.MovementTurn,
            checked(game.State.MovementTurn + 10UL));

    [Fact]
    public void RealTitledMaterialCanAcceptCalculatedPriceWithoutBecomingSettledSale()
    {
        var game = Closed();
        var reviewer = new TestReviewer();
        var title = Title(game, 3U, 4UL, reviewer);
        var free = game.StockAt(game.InitialTile, new CommodityId(3U));
        var oldHash = game.CurrentHash;
        var reference = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            game, 2UL);
        var row = Assert.Single(reference.Markets);

        var accepted = AcceptTerms(game, title, reviewer);
        Assert.Equal(1, reviewer.TermsCalls);
        Assert.Equal("accept_local_commercial_terms", game.Events[^1].Kind);
        Assert.Equal(accepted.Evidence, game.Events[^1].CommercialTermsEvidence);
        Assert.NotEqual(oldHash, game.CurrentHash);
        Assert.Equal(free, game.StockAt(game.InitialTile, new CommodityId(3U)));
        Assert.Equal(title.ActionOrdinal,
            accepted.Evidence.RegisteredTitleActionOrdinal);
        Assert.Equal(title.AfterWorldHashHex,
            accepted.Evidence.OriginalTitleAfterWorldHashHex);
        Assert.Equal(oldHash.HexDigest, accepted.Evidence.PriceReferenceWorldHashHex);
        Assert.Equal(4UL, accepted.Evidence.MaterialRawQuantity);
        Assert.Equal(row.RealResidents, accepted.Evidence.RealLocalResidents);
        Assert.Equal(row.CurrentlyTitledRaw.ToString(CultureInfo.InvariantCulture),
            accepted.Evidence.MarketCurrentlyTitledRaw);
        Assert.Equal(row.NeutralDemandPressureRaw.ToString(
            CultureInfo.InvariantCulture), accepted.Evidence.LocalDemandPressureRaw);
        Assert.Equal(row.GentleAveragePriceIndex.Numerator.ToString(
            CultureInfo.InvariantCulture),
            accepted.Evidence.RelativeAveragePriceNumerator);
        Assert.Equal(row.GentleAveragePriceIndex.Denominator.ToString(
            CultureInfo.InvariantCulture),
            accepted.Evidence.RelativeAveragePriceDenominator);
        var exactLotValue =
            row.GentleAveragePriceIndex.Times(new BigInteger(4));
        Assert.Equal(exactLotValue.Numerator.ToString(
            CultureInfo.InvariantCulture),
            accepted.Evidence.AllocatedRelativeGrossNumerator);
        Assert.Equal(exactLotValue.Denominator.ToString(
            CultureInfo.InvariantCulture),
            accepted.Evidence.AllocatedRelativeGrossDenominator);
        Assert.True(accepted.RecordsExecutedTwoPartyTermsAcceptance);
        Assert.True(accepted.ConfirmsUnderlyingPhysicalDelivery);
        Assert.True(accepted.ConfirmsInGameEconomicTitle);
        Assert.False(accepted.RegistersNominalCurrencyPrice);
        Assert.False(accepted.ProvesCommercialSaleSettled);
        Assert.False(accepted.ExecutesBuyerPaymentOrCredit);
        Assert.False(accepted.AuthorizesProducerRevenueOrTreasuryTax);
        Assert.False(accepted.MovesOrConsumesAdditionalMaterial);
        Assert.False(accepted.RatifiesPurchasingPowerCalibration);
        Assert.False(accepted.ReplaysExternalConsentAuthentication);
        Assert.Single(game.RegisteredLocalSimulatedEconomicTitles);
        Assert.Single(game.RecordedLocalCommercialTerms);
        Assert.Empty(game.ReversedLocalSimulatedEconomicTitles);

        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Equal(new BigInteger(4), coverage.TotalTransferredToRecipientRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalExecutedPhysicalMaterialRaw);
        Assert.False(coverage.ProvesCompletedMarketSales);
        coverage.VerifyIndependent();

        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.LocalCommercialTermsEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!.Version);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(accepted, replay.RecordedLocalCommercialTerms.Single());
        Assert.Equal(game.Events.ToArray(), replay.Events.ToArray());
    }

    [Fact]
    public void TermsMustHaveSeparatelyReviewedConsentAndCannotBeDuplicated()
    {
        var game = Closed();
        var reviewer = new TestReviewer();
        var title = Title(game, 2U, 3UL, reviewer);
        var previous = game.ExportSaveJson();
        reviewer.Accept = false;
        Assert.Throws<InvalidOperationException>(() =>
            AcceptTerms(game, title, reviewer));
        Assert.Empty(game.RecordedLocalCommercialTerms);
        Assert.Equal(previous, game.ExportSaveJson());

        reviewer.Accept = true;
        Assert.Throws<InvalidOperationException>(() =>
            game.AcceptLocalCommercialTerms(title.ActionOrdinal,
                "terms-bad", 1UL, 0UL,
                CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand,
                "fresh-consent-A", "fresh-consent-B", Council, reviewer));
        Assert.Throws<InvalidOperationException>(() =>
            game.AcceptLocalCommercialTerms(title.ActionOrdinal,
                "terms-bad", 1UL, 2UL,
                (CanonicalEndogenousCurveVariant)255,
                "fresh-consent-A", "fresh-consent-B", Council, reviewer));
        Assert.Throws<InvalidOperationException>(() =>
            game.AcceptLocalCommercialTerms(title.ActionOrdinal,
                "terms-bad", 1UL, 2UL,
                CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand,
                "duplicate-consent", "duplicate-consent", Council, reviewer));
        Assert.Throws<InvalidOperationException>(() =>
            game.AcceptLocalCommercialTerms(title.ActionOrdinal,
                "terms-bad", 1UL, 2UL,
                CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand,
                "fresh-consent-A", "fresh-consent-B", Source, reviewer));
        Assert.Empty(game.RecordedLocalCommercialTerms);
        Assert.Equal(previous, game.ExportSaveJson());
        var receipt = AcceptTerms(game, title, reviewer,
            CanonicalEndogenousCurveVariant.SteepDyadicSupplyDemand);
        Assert.Equal(
            CanonicalEndogenousCurveVariant.SteepDyadicSupplyDemand,
            receipt.Evidence.Curve);
        var committed = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            AcceptTerms(game, title, reviewer));
        Assert.Equal(committed, game.ExportSaveJson());
        Assert.Single(game.RecordedLocalCommercialTerms);
    }

    [Fact]
    public void CommercialTermsPreventSilentTitleReversalButAllowActualWorkforceUse()
    {
        var game = Closed();
        var reviewer = new TestReviewer();
        var title = Title(game, 3U, 2UL, reviewer);
        AcceptTerms(game, title, reviewer);
        var saved = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.ReverseLocalSimulatedEconomicTitle(
                title.ActionOrdinal, Reversal(game, title), reviewer));
        Assert.Throws<InvalidOperationException>(() =>
            game.ReturnLocalRecipientCustody(
                title.Evidence.SourceCommitmentActionOrdinal));
        Assert.Equal(saved, game.ExportSaveJson());
        var used = game.UseLocalRecipientMaterial(
            title.Evidence.SourceCommitmentActionOrdinal,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        Assert.True(used.ProvesExecutedMaterialUse);
        Assert.Equal(2UL, used.RawQuantity);
        Assert.Single(game.RecordedLocalCommercialTerms);
        Assert.False(game.RecordedLocalCommercialTerms.Single()
            .ProvesCommercialSaleSettled);
        var json = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(json, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Single(replay.ExecutedLocalRecipientCustodyDispositions);
        Assert.Single(replay.RecordedLocalCommercialTerms);
    }

    [Fact]
    public void ForgedSelfConsistentlyRehashedQuoteIsRejectedByExactReplay()
    {
        var game = Closed();
        var title = Title(game, 2U, 3UL, new TestReviewer());
        AcceptTerms(game, title, new TestReviewer());
        var json = game.ExportSaveJson();
        var wrapper = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)!;
        var payload =
            JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
                wrapper.PayloadJson)!;
        var actions = payload.Actions.ToArray();
        var last = actions[^1];
        Assert.NotNull(last.CommercialTermsEvidence);
        actions[^1] = last with
        {
            CommercialTermsEvidence = last.CommercialTermsEvidence! with
            {
                RelativeAveragePriceNumerator = "99999999"
            }
        };
        var forgedPayload = JsonSerializer.Serialize(
            payload with { Actions = actions });
        var forged = JsonSerializer.Serialize(wrapper with
        {
            PayloadJson = forgedPayload,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(forgedPayload)))
        });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(forged));
        Assert.Equal(json, game.ExportSaveJson());
        Assert.Equal(json,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(json)
                .ExportSaveJson());
    }

    [Fact]
    public void PreviousEnvelopesCannotBeRelabeledAsCommercialAcceptance()
    {
        var game = Closed();
        var title = Title(game, 3U, 2UL, new TestReviewer());
        var oldSave = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.SimulatedEconomicTitleEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
                oldSave)!.Version);
        AcceptTerms(game, title, new TestReviewer());
        var newSave = game.ExportSaveJson();
        var oldEnvelope =
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(oldSave)!;
        var newEnvelope =
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(newSave)!;
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                JsonSerializer.Serialize(oldEnvelope with
                {
                    Version =
                        CanonicalPhysicalTacticalSession.LocalCommercialTermsEnvelopeFormat
                })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                JsonSerializer.Serialize(newEnvelope with
                {
                    Version =
                        CanonicalPhysicalTacticalSession.SimulatedEconomicTitleEnvelopeFormat
                })));
        Assert.Equal(oldSave,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(oldSave)
                .ExportSaveJson());
        Assert.Equal(newSave,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(newSave)
                .ExportSaveJson());
    }

    [Fact]
    public void DormantTreasuryIsNeverCreditedByAcknowledgedCommercialTerms()
    {
        var game = Closed();
        game.EnableDormantTreasuryReservation();
        var title = Title(game, 2U, 3UL, new TestReviewer());
        var receipt = AcceptTerms(game, title, new TestReviewer());
        Assert.False(receipt.AuthorizesProducerRevenueOrTreasuryTax);
        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.LocalCommercialTermsWithTreasuryEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!.Version);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Single(replay.RecordedLocalCommercialTerms);
    }

    [Fact]
    public void MixedHistoricalReversalAndLaterValidTermsCanReplayTogether()
    {
        var game = Closed();
        var reviewer = new TestReviewer();
        var first = Title(game, 2U, 2UL, reviewer);
        var second = Title(game, 3U, 3UL, reviewer);
        game.ReverseLocalSimulatedEconomicTitle(first.ActionOrdinal,
            Reversal(game, first), reviewer);
        var agreed = AcceptTerms(game, second, reviewer);
        Assert.Equal(second.ActionOrdinal,
            agreed.Evidence.RegisteredTitleActionOrdinal);
        Assert.Single(game.ReversedLocalSimulatedEconomicTitles);
        Assert.Single(game.RecordedLocalCommercialTerms);
        var saved = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.LocalCommercialTermsEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
                saved)!.Version);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(saved, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Single(replay.RecordedLocalCommercialTerms);
        Assert.Single(replay.ReversedLocalSimulatedEconomicTitles);
    }
}
