using System.Numerics;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01oTests
{
    private static readonly ExperimentalTransportInstitution Source =
        new("initial-village", ExperimentalTransportInstitutionKind.Community);
    private static readonly ExperimentalTransportInstitution Receiver =
        new("local-artisan-group", ExperimentalTransportInstitutionKind.Cooperative);
    private static readonly ExperimentalTransportInstitution Issuer =
        new("independent-neolithic-council",
            ExperimentalTransportInstitutionKind.Community);

    // Test-only stubs; these are NOT implementations of production legal
    // authority or a substitute for independent signed rights verification.
    private sealed class ReviewStub : ICanonicalLocalEconomicRightsReviewer,
        ICanonicalLocalSimulatedTitleReversalReviewer
    {
        public bool Accepted { get; set; } = true;
        public int ReversalReviews { get; private set; }

        public bool AcceptReadOnlyRight(
            CanonicalLocalEconomicRightClaim evidence,
            CanonicalLocalEconomicRecipientAttribution party) =>
            Accepted && evidence.PhysicalTransferActionOrdinal ==
                party.PhysicalTransferActionOrdinal &&
            evidence.AgreementId == party.AgreementId;

        public bool AcceptReadOnlyTitleReversal(
            CanonicalLocalSimulatedTitleReversalEvidence evidence,
            CanonicalRegisteredLocalSimulatedEconomicTitle title)
        {
            ReversalReviews++;
            return Accepted &&
                evidence.OriginalTitleActionOrdinal == title.ActionOrdinal &&
                evidence.OriginalTitleAfterWorldHashHex ==
                    title.AfterWorldHashHex;
        }
    }

    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    private static (CanonicalRegisteredLocalSimulatedEconomicTitle Title,
        ReviewStub Reviewer, ulong OriginalStock) Registered(
        CanonicalPhysicalTacticalSession game, uint commodity = 2U,
        ulong quantity = 3UL)
    {
        var tile = game.InitialTile;
        var original = game.StockAt(tile, new CommodityId(commodity));
        var offer = game.CommitPhysicalStock(tile, new CommodityId(commodity),
            CanonicalPhysicalCommitmentKind.MarketOffer, quantity);
        var handoff = game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal, commodity == 2U
                ? EconomicActivityKind.Mining : EconomicActivityKind.Forestry);
        var declaration = new CanonicalLocalEconomicCounterpartyDeclaration(
            "agreement-original-01", 1UL, handoff.ActionOrdinal,
            handoff.AfterWorldHashHex, handoff.FineTile, handoff.Commodity,
            handoff.RawQuantity, handoff.RecipientActivity,
            handoff.RecipientWorkers, Source, Receiver);
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { declaration });
        CanonicalLocalEconomicRightClaim Right(string name,
            CanonicalLocalEconomicRightScope scope) =>
            new(name, 1UL, game.CurrentHash.HexDigest,
                handoff.ActionOrdinal, handoff.AfterWorldHashHex,
                declaration.AgreementId, declaration.AgreementVersion,
                scope,
                scope == CanonicalLocalEconomicRightScope.SourceOfferDisposition
                    ? Source : Receiver, Issuer,
                game.State.MovementTurn,
                checked(game.State.MovementTurn + 20UL));
        var reviewer = new ReviewStub();
        var rights = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            game, parties, new[]
            {
                Right("grant-source-original",
                    CanonicalLocalEconomicRightScope.SourceOfferDisposition),
                Right("grant-receiver-original",
                    CanonicalLocalEconomicRightScope.RecipientPhysicalAcceptance)
            }, reviewer);
        var title = game.RegisterLocalSimulatedEconomicTitle(
            offer.ActionOrdinal, rights);
        return (title, reviewer, original);
    }

    private static CanonicalLocalSimulatedTitleReversalEvidence Reversal(
        CanonicalPhysicalTacticalSession game,
        CanonicalRegisteredLocalSimulatedEconomicTitle title) =>
        new(title.ActionOrdinal, title.Evidence.SourceCommitmentActionOrdinal,
            title.AfterWorldHashHex, game.CurrentHash.HexDigest,
            "reversal-agreement-01", 1UL,
            "new-permission-by-former-owner",
            "new-acceptance-by-original-owner",
            Issuer, game.State.MovementTurn,
            checked(game.State.MovementTurn + 10UL));

    [Fact]
    public void ReversalChangesOnlyInGameTitleThenPhysicalReturnRestoresOriginalLots()
    {
        var game = Closed();
        var (title, reviewer, before) = Registered(game);
        var freeAfterHandoff = game.StockAt(game.InitialTile, new CommodityId(2U));
        var previousHash = game.CurrentHash;
        var reversal = game.ReverseLocalSimulatedEconomicTitle(
            title.ActionOrdinal, Reversal(game, title), reviewer);

        Assert.Equal(1, reviewer.ReversalReviews);
        Assert.Equal("reverse_local_economic_title", game.Events[^1].Kind);
        Assert.Equal(reversal.Evidence, game.Events[^1].TitleReversalEvidence);
        Assert.Equal(title.ActionOrdinal,
            reversal.Evidence.OriginalTitleActionOrdinal);
        Assert.NotEqual(previousHash, game.CurrentHash);
        Assert.Equal(freeAfterHandoff,
            game.StockAt(game.InitialTile, new CommodityId(2U)));
        Assert.Single(game.RegisteredLocalSimulatedEconomicTitles);
        Assert.Single(game.ReversedLocalSimulatedEconomicTitles);
        Assert.True(reversal.ReversesInGameTitle);
        Assert.False(reversal.ReversesPhysicalHandoff);
        Assert.False(reversal.ExecutesCommercialSale);
        Assert.False(reversal.PostsMonetaryRefundOrTax);
        Assert.False(reversal.HasLegallyAuthenticatedTitle);
        Assert.False(reversal.ReplayReauthorizesExternalReviewer);
        var position = Assert.Single(game.LocalSimulatedTitlePositions);
        Assert.True(position.IsReversed);
        Assert.True(position.IsInRecipientCustody);
        Assert.Equal(Source, position.CurrentInGameEconomicHolder);
        Assert.False(position.ProvesCommercialSale);
        Assert.False(position.AuthorizesMonetarySettlement);

        var beforeReturn = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.SimulatedTitleReversalEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
                beforeReturn)!.Version);
        var reconstructed = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            beforeReturn);
        Assert.Equal(beforeReturn, reconstructed.ExportSaveJson());
        Assert.Equal(game.CurrentHash, reconstructed.CurrentHash);
        Assert.Equal(reversal,
            reconstructed.ReversedLocalSimulatedEconomicTitles.Single());
        Assert.Equal(position,
            reconstructed.LocalSimulatedTitlePositions.Single());

        Assert.Throws<InvalidOperationException>(() =>
            game.UseLocalRecipientMaterial(
                title.Evidence.SourceCommitmentActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        var receipt = game.ReturnLocalRecipientCustody(
            title.Evidence.SourceCommitmentActionOrdinal);
        Assert.True(receipt.ProvesReturnOfSamePhysicalCustody);
        Assert.Equal(before, game.StockAt(game.InitialTile, new CommodityId(2U)));
        Assert.False(game.LocalSimulatedTitlePositions.Single().IsInRecipientCustody);

        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Equal(new BigInteger(3), coverage.TotalReturnedFromRecipientRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalTransferredToRecipientRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalExecutedPhysicalMaterialRaw);
        Assert.False(coverage.CanAuthorizeMonetaryRevenueOrTax);
        coverage.VerifyIndependent();
        var afterReturn = game.ExportSaveJson();
        var roundtrip = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            afterReturn);
        Assert.Equal(afterReturn, roundtrip.ExportSaveJson());
        Assert.Equal(game.CurrentHash, roundtrip.CurrentHash);
        Assert.Equal(game.LocalSimulatedTitlePositions.ToArray(),
            roundtrip.LocalSimulatedTitlePositions.ToArray());
    }

    [Fact]
    public void WithoutReversalTitleBlocksReturnButPermitsWorkforceMaterialUse()
    {
        var game = Closed();
        var (title, reviewer, _) = Registered(game, 3U, 2UL);
        Assert.Throws<InvalidOperationException>(() =>
            game.ReturnLocalRecipientCustody(
                title.Evidence.SourceCommitmentActionOrdinal));
        var used = game.UseLocalRecipientMaterial(
            title.Evidence.SourceCommitmentActionOrdinal,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        Assert.True(used.ProvesExecutedMaterialUse);
        Assert.Equal(2UL, used.RawQuantity);
        var saved = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.ReverseLocalSimulatedEconomicTitle(title.ActionOrdinal,
                Reversal(game, title), reviewer));
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Empty(game.ReversedLocalSimulatedEconomicTitles);
        Assert.True(game.LocalSimulatedTitlePositions.Single().IsInRecipientCustody ==
            false);
        Assert.Equal(
            CanonicalLocalRecipientCustodyDispositionKind.ExecuteWorkforceMaterialUse,
            game.LocalSimulatedTitlePositions.Single().PhysicalDisposition);
        Assert.Equal(saved,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(saved).ExportSaveJson());
    }

    [Fact]
    public void ReviewerRefusalDuplicateAndForgedEvidenceCannotMutateWorld()
    {
        var game = Closed();
        var (title, reviewer, _) = Registered(game);
        var authentic = Reversal(game, title);
        var initial = game.ExportSaveJson();
        var currentHash = game.CurrentHash;
        reviewer.Accepted = false;
        Assert.Throws<InvalidOperationException>(() =>
            game.ReverseLocalSimulatedEconomicTitle(
                title.ActionOrdinal, authentic, reviewer));
        Assert.Equal(initial, game.ExportSaveJson());
        Assert.Equal(currentHash, game.CurrentHash);
        Assert.Empty(game.ReversedLocalSimulatedEconomicTitles);

        reviewer.Accepted = true;
        var forgeries = new[]
        {
            authentic with { OriginalTitleActionOrdinal = 99999 },
            authentic with { SourceCommitmentActionOrdinal = 99999 },
            authentic with { OriginalTitleAfterWorldHashHex = new string('F', 64) },
            authentic with { ReviewWorldHashHex = new string('0', 64) },
            authentic with { ReversalAgreementId = "agreement-original-01" },
            authentic with { ReversalAgreementId = " " },
            authentic with { ReversalAgreementVersion = 0UL },
            authentic with { FormerHolderAuthorizationId =
                authentic.OriginalHolderAcceptanceId },
            authentic with { FormerHolderAuthorizationId =
                title.Evidence.ReviewedRecipientAuthorizationId },
            authentic with { OriginalHolderAcceptanceId =
                title.Evidence.ReviewedSourceAuthorizationId },
            authentic with { IndependentIssuingInstitution = Source },
            authentic with { IndependentIssuingInstitution = Receiver },
            authentic with { IndependentIssuingInstitution =
                new ExperimentalTransportInstitution("phantom",
                    ExperimentalTransportInstitutionKind.Unspecified) },
            authentic with { ValidFromMovementTurn =
                game.State.MovementTurn + 1UL },
            authentic with { ValidThroughMovementTurn =
                game.State.MovementTurn - 1UL }
        };
        foreach (var forged in forgeries)
            Assert.Throws<InvalidOperationException>(() =>
                game.ReverseLocalSimulatedEconomicTitle(
                    title.ActionOrdinal, forged, reviewer));
        Assert.Equal(initial, game.ExportSaveJson());
        Assert.Equal(currentHash, game.CurrentHash);

        game.ReverseLocalSimulatedEconomicTitle(
            title.ActionOrdinal, authentic, reviewer);
        var after = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.ReverseLocalSimulatedEconomicTitle(
                title.ActionOrdinal, authentic, reviewer));
        Assert.Equal(after, game.ExportSaveJson());
        Assert.Single(game.ReversedLocalSimulatedEconomicTitles);
    }

    [Fact]
    public void EnvelopesV41AndV43CannotBeRelabelledAndOldSavesRemainExact()
    {
        var game = Closed();
        var (title, reviewer, _) = Registered(game);
        var original = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.SimulatedEconomicTitleEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
                original)!.Version);
        Assert.Equal(original,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                original).ExportSaveJson());
        game.ReverseLocalSimulatedEconomicTitle(
            title.ActionOrdinal, Reversal(game, title), reviewer);
        var reversed = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.SimulatedTitleReversalEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
                reversed)!.Version);

        var mislabelOld = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(original)!
                with
                {
                    Version =
                        CanonicalPhysicalTacticalSession.SimulatedTitleReversalEnvelopeFormat
                });
        var mislabelNew = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(reversed)!
                with
                {
                    Version =
                        CanonicalPhysicalTacticalSession.SimulatedEconomicTitleEnvelopeFormat
                });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(mislabelOld));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(mislabelNew));
        Assert.Equal(reversed,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                reversed).ExportSaveJson());
        Assert.Equal(original, original);
    }

    [Fact]
    public void DormantTreasuryReversalReplaysWithZeroRevenue()
    {
        var game = Closed();
        game.EnableDormantTreasuryReservation();
        var (title, reviewer, _) = Registered(game);
        game.ReverseLocalSimulatedEconomicTitle(
            title.ActionOrdinal, Reversal(game, title), reviewer);
        var json = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.SimulatedTitleReversalWithTreasuryEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)!.Version);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(json, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Single(replay.ReversedLocalSimulatedEconomicTitles);
        Assert.False(replay.ReversedLocalSimulatedEconomicTitles.Single()
            .PostsMonetaryRefundOrTax);
    }

    [Fact]
    public void ReversalProofMustBelongToSameSessionAndValidTitle()
    {
        var game = Closed();
        var (title, reviewer, _) = Registered(game);
        var initial = game.ExportSaveJson();
        var foreign = CanonicalPhysicalTacticalSession.RestoreSaveJson(initial);
        var foreignTitle = foreign.RegisteredLocalSimulatedEconomicTitles.Single();
        // Identical replayed material facts allow a newly host-reviewed
        // action on the separate, independently current session.
        var foreignReviewer = new ReviewStub();
        foreign.ReverseLocalSimulatedEconomicTitle(
            foreignTitle.ActionOrdinal, Reversal(foreign, foreignTitle),
            foreignReviewer);
        Assert.Equal(initial, game.ExportSaveJson());
        Assert.Empty(game.ReversedLocalSimulatedEconomicTitles);
        Assert.Single(foreign.ReversedLocalSimulatedEconomicTitles);
        Assert.Throws<InvalidOperationException>(() =>
            game.ReverseLocalSimulatedEconomicTitle(
                title.ActionOrdinal + 10, Reversal(game, title), reviewer));
    }
}
