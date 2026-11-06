using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01mTests
{
    private static readonly ExperimentalTransportInstitution Source =
        new("village-producers", ExperimentalTransportInstitutionKind.Community);
    private static readonly ExperimentalTransportInstitution Buyer =
        new("artisan-cooperative", ExperimentalTransportInstitutionKind.Cooperative);
    private static readonly ExperimentalTransportInstitution Issuer =
        new("local-community-council", ExperimentalTransportInstitutionKind.Community);

    // Strictly a TEST stub. Production must supply independent, evidence-
    // verifying host authority; accepting this stub cannot ratify legal title.
    private sealed class TestReviewer : ICanonicalLocalEconomicRightsReviewer
    {
        public bool Accept { get; set; } = true;
        public int Calls { get; private set; }
        public bool AcceptReadOnlyRight(
            CanonicalLocalEconomicRightClaim claim,
            CanonicalLocalEconomicRecipientAttribution party)
        {
            Calls++;
            return Accept &&
                claim.PhysicalTransferActionOrdinal ==
                    party.PhysicalTransferActionOrdinal &&
                claim.AgreementId == party.AgreementId;
        }
    }

    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    private static CanonicalLocalEconomicCounterpartyDeclaration PartyClaim(
        CanonicalExecutedLocalRecipientCustodyTransfer receipt,
        string agreement = "trade-intent-01") =>
        new(agreement, 1UL, receipt.ActionOrdinal,
            receipt.AfterWorldHashHex, receipt.FineTile,
            receipt.Commodity, receipt.RawQuantity,
            receipt.RecipientActivity, receipt.RecipientWorkers, Source, Buyer);

    private static CanonicalLocalEconomicRightClaim Right(
        CanonicalPhysicalTacticalSession game,
        CanonicalExecutedLocalRecipientCustodyTransfer receipt,
        CanonicalLocalEconomicRightScope scope,
        string id = "right-01",
        string agreement = "trade-intent-01")
    {
        var current = game.State.MovementTurn;
        return new(id, 1UL, game.CurrentHash.HexDigest,
            receipt.ActionOrdinal, receipt.AfterWorldHashHex,
            agreement, 1UL, scope,
            scope == CanonicalLocalEconomicRightScope.SourceOfferDisposition
                ? Source : Buyer,
            Issuer, current, checked(current + 10UL));
    }

    [Fact]
    public void TwoDistinctRightsAreReviewedAgainstOneAuthenticLivePhysicalHandoff()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile,
            new CommodityId(3U), CanonicalPhysicalCommitmentKind.MarketOffer, 4UL);
        var delivery = game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal, EconomicActivityKind.Forestry);
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { PartyClaim(delivery) });
        var reviewer = new TestReviewer();
        var beforeSave = game.ExportSaveJson();
        var beforeHash = game.CurrentHash;

        var rights = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            game, parties, new[]
            {
                Right(game, delivery,
                    CanonicalLocalEconomicRightScope.RecipientPhysicalAcceptance,
                    "right-buyer"),
                Right(game, delivery,
                    CanonicalLocalEconomicRightScope.SourceOfferDisposition,
                    "right-seller")
            }, reviewer);

        Assert.Equal(2, rights.Claims.Count);
        Assert.Equal(1, rights.FullyReviewedLocalCustodiesCount);
        Assert.Equal(0, rights.UnreviewedOrPartlyReviewedCount);
        Assert.Equal(new BigInteger(4), rights.FullyReviewedActivePhysicalRaw);
        Assert.True(rights.UsesRealJournaledPhysicalHandoffs);
        Assert.True(rights.RequiresExplicitIndependentIssuingAuthority);
        Assert.True(rights.UsesHostSuppliedRightsReviewer);
        Assert.False(rights.HasBuiltInPermissiveRightsReviewer);
        Assert.False(rights.TreatsWorkerGroupsAsLegalOwners);
        Assert.False(rights.AuthenticatesLegallyEnforceableTitle);
        Assert.False(rights.IssuesBindingSalesContracts);
        Assert.False(rights.ExecutesCustodyOrTitleChanges);
        Assert.False(rights.AuthorizesMoneySettlementOrTaxes);
        Assert.False(rights.CreatesBuyerCashOrCredit);
        Assert.False(rights.MutatesWorldStateOrCanonicalSave);
        var row = Assert.Single(rights.Agreements);
        Assert.True(row.HasReviewedBothProposedRights);
        Assert.True(row.HasLivePhysicalCustody);
        Assert.Equal("right-seller", row.ReviewedSourceAuthorizationId);
        Assert.Equal("right-buyer", row.ReviewedRecipientAuthorizationId);
        Assert.Equal(delivery.ActionOrdinal,
            row.PhysicalAttribution.PhysicalTransferActionOrdinal);
        Assert.False(row.IsCommercialSale);
        Assert.False(row.TransfersLegalTitle);
        Assert.False(row.AuthorizesPaymentOrTaxes);
        Assert.True(reviewer.Calls >= 4);
        rights.VerifyIndependent();
        rights.RequireCurrent(game);
        Assert.Equal(beforeSave, game.ExportSaveJson());
        Assert.Equal(beforeHash, game.CurrentHash);

        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(beforeSave);
        var replayReceipt = replay.ExecutedLocalRecipientCustodyTransfers.Single();
        var replayParties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            replay, new[] { PartyClaim(replayReceipt) });
        var replayRights = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            replay, replayParties, new[]
            {
                Right(replay, replayReceipt,
                    CanonicalLocalEconomicRightScope.SourceOfferDisposition,
                    "right-seller"),
                Right(replay, replayReceipt,
                    CanonicalLocalEconomicRightScope.RecipientPhysicalAcceptance,
                    "right-buyer")
            }, new TestReviewer());
        Assert.Equal(rights.Agreements.ToArray(), replayRights.Agreements.ToArray());
        Assert.Equal(beforeHash, replay.CurrentHash);
        Assert.Equal(beforeSave, replay.ExportSaveJson());
    }

    [Fact]
    public void MissingSecondRightDoesNotAuthenticateBothPartiesOrInventAuthority()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var delivery = game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal, EconomicActivityKind.Mining);
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { PartyClaim(delivery) });

        var empty = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            game, parties,
            Array.Empty<CanonicalLocalEconomicRightClaim>(), new TestReviewer());
        Assert.Equal(0, empty.FullyReviewedLocalCustodiesCount);
        Assert.Equal(1, empty.UnreviewedOrPartlyReviewedCount);
        Assert.Equal(BigInteger.Zero, empty.FullyReviewedActivePhysicalRaw);
        Assert.False(empty.Agreements.Single().HasReviewedBothProposedRights);

        var partial = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            game, parties, new[]
            {
                Right(game, delivery,
                    CanonicalLocalEconomicRightScope.SourceOfferDisposition,
                    "only-source")
            }, new TestReviewer());
        Assert.Equal("only-source",
            partial.Agreements.Single().ReviewedSourceAuthorizationId);
        Assert.Null(partial.Agreements.Single().ReviewedRecipientAuthorizationId);
        Assert.Equal(BigInteger.Zero, partial.FullyReviewedActivePhysicalRaw);
        Assert.False(partial.AuthorizesMoneySettlementOrTaxes);
    }

    [Fact]
    public void ReviewerDenialOrSubsequentRevocationInvalidatesRightsReview()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var delivery = game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal, EconomicActivityKind.Forestry);
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { PartyClaim(delivery) });
        var grant = Right(game, delivery,
            CanonicalLocalEconomicRightScope.SourceOfferDisposition, "r1");
        var reviewer = new TestReviewer { Accept = false };
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
                game, parties, new[] { grant }, reviewer));

        reviewer.Accept = true;
        var approved = CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
            game, parties, new[] { grant }, reviewer);
        reviewer.Accept = false;
        Assert.Throws<InvalidOperationException>(approved.VerifyIndependent);
        reviewer.Accept = true;
        approved.VerifyIndependent();

        game.ReturnLocalRecipientCustody(offer.ActionOrdinal);
        Assert.Throws<InvalidOperationException>(approved.VerifyIndependent);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
                game,
                CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
                    game, new[] { PartyClaim(delivery) }),
                new[] { grant }, reviewer));
    }

    [Fact]
    public void ForgedOrExpiredAuthorizationCannotBorrowRealWorldProvenance()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var delivery = game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal, EconomicActivityKind.Forestry);
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { PartyClaim(delivery) });
        var valid = Right(game, delivery,
            CanonicalLocalEconomicRightScope.SourceOfferDisposition);
        var snapshot = game.ExportSaveJson();
        var bad = new[]
        {
            valid with { CurrentWorldHashHex = new string('F', 64) },
            valid with { PhysicalTransferActionOrdinal = 99999 },
            valid with { PhysicalTransferAfterWorldHashHex = new string('0', 64) },
            valid with { AgreementId = "wrong" },
            valid with { AgreementVersion = 12UL },
            valid with { ProposedRightsHolder = Buyer },
            valid with { EffectiveFromMovementTurn = game.State.MovementTurn + 1UL },
            valid with { EffectiveThroughMovementTurn = game.State.MovementTurn - 1UL },
            valid with { AuthorizationVersion = 0UL },
            valid with { AuthorizationId = " " },
            valid with { Scope = (CanonicalLocalEconomicRightScope)255 },
            valid with { ProposedIssuingAuthority = Source },
            valid with { ProposedIssuingAuthority = Buyer },
            valid with { ProposedIssuingAuthority = new ExperimentalTransportInstitution(
                "unidentified", ExperimentalTransportInstitutionKind.Unspecified) },
            valid with { ProposedIssuingAuthority = new ExperimentalTransportInstitution(
                "operator-issuer", ExperimentalTransportInstitutionKind.CommercialOperator) },
            valid with { ProposedIssuingAuthority = new ExperimentalTransportInstitution(
                "", ExperimentalTransportInstitutionKind.PublicAuthority) }
        };
        foreach (var claim in bad)
            Assert.Throws<InvalidOperationException>(() =>
                CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
                    game, parties, new[] { claim }, new TestReviewer()));
        Assert.Equal(snapshot, game.ExportSaveJson());
    }

    [Fact]
    public void DuplicateScopeIdentifierAndForeignPhysicalPartyFailClosed()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var delivery = game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal, EconomicActivityKind.Mining);
        var parties = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { PartyClaim(delivery) });
        var right = Right(game, delivery,
            CanonicalLocalEconomicRightScope.SourceOfferDisposition, "seller");
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
                game, parties, new[] { right, right }, new TestReviewer()));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
                game, parties,
                new[] { right, right with { AuthorizationId = "different" } },
                new TestReviewer()));
        Assert.Throws<ArgumentException>(() =>
            CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
                game, parties, new CanonicalLocalEconomicRightClaim[] { null! },
                new TestReviewer()));
        var foreign = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            game.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(
                foreign, parties, new[] { right }, new TestReviewer()));
    }
}
