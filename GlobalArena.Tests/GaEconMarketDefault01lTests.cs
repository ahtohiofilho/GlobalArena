using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01lTests
{
    private static readonly ExperimentalTransportInstitution Community =
        new("neolithic-community", ExperimentalTransportInstitutionKind.Community);
    private static readonly ExperimentalTransportInstitution Cooperative =
        new("wood-and-stone-cooperative",
            ExperimentalTransportInstitutionKind.Cooperative);
    private static readonly ExperimentalTransportInstitution Public =
        new("primitive-public-authority",
            ExperimentalTransportInstitutionKind.PublicAuthority);

    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    private static CanonicalLocalEconomicCounterpartyDeclaration Claim(
        CanonicalExecutedLocalRecipientCustodyTransfer transfer,
        string agreement = "local-intent-01",
        ExperimentalTransportInstitution? source = null,
        ExperimentalTransportInstitution? target = null) =>
        new(
            agreement, 1UL, transfer.ActionOrdinal,
            transfer.AfterWorldHashHex,
            transfer.FineTile, transfer.Commodity, transfer.RawQuantity,
            transfer.RecipientActivity, transfer.RecipientWorkers,
            source ?? Community, target ?? Cooperative);

    [Fact]
    public void RealHandoffCanBeMappedToNamedCounterpartiesWithoutClaimingTitle()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 4UL);
        var handoff = game.TransferMarketOfferToLocalRecipient(
            wood.ActionOrdinal, EconomicActivityKind.Forestry);
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var proposal = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { Claim(handoff) });

        Assert.Single(proposal.Declarations);
        Assert.Single(proposal.Attributions);
        Assert.Equal(1, proposal.PhysicalTransfersCount);
        Assert.Equal(0, proposal.UnattributedPhysicalTransfersCount);
        Assert.True(proposal.AllRealRecipientTransfersHaveExplicitCounterparties);
        Assert.Equal(new BigInteger(4), proposal.AttributedHistoricalPhysicalRaw);
        Assert.Equal(new BigInteger(4), proposal.ActiveAttributedRecipientCustodyRaw);
        Assert.Equal(BigInteger.Zero, proposal.AttributedReturnedPhysicalRaw);
        Assert.Equal(BigInteger.Zero, proposal.AttributedExecutedMaterialRaw);
        var row = proposal.Attributions.Single();
        Assert.Equal(wood.ActionOrdinal, row.SourceCommitmentActionOrdinal);
        Assert.Equal(handoff.ActionOrdinal, row.PhysicalTransferActionOrdinal);
        Assert.Equal(handoff.AfterWorldHashHex,
            row.PhysicalTransferAfterWorldHashHex);
        Assert.Equal(Community, row.DeclaredSourceInstitution);
        Assert.Equal(Cooperative, row.DeclaredReceivingInstitution);
        Assert.Equal(handoff.RecipientActivity, row.RecipientActivity);
        Assert.Equal(handoff.RecipientWorkers, row.RecipientWorkersAtTransfer);
        Assert.True(row.HasLiveRecipientCustody);
        Assert.True(row.ProvesActualLocalPhysicalHandoff);
        Assert.False(row.ProvesLegalEconomicOwnership);
        Assert.False(row.ProvesAcceptedContractOrSale);
        Assert.False(row.ProvesDeliveryToRemoteBuyer);
        Assert.False(row.CanAuthorizeSettlement);
        Assert.True(proposal.AuthenticatesActualLocalRecipientHandoffs);
        Assert.True(proposal.UsesExplicitInstitutionalCounterpartDeclarations);
        Assert.False(proposal.InfersBuyerIdentityFromLaborOrSite);
        Assert.False(proposal.AuthenticatesInstitutionalLegalTitle);
        Assert.False(proposal.AuthenticatesBindingSaleContracts);
        Assert.False(proposal.AuthenticatesExecutedBuyerPayment);
        Assert.False(proposal.AuthorizesMonetaryRevenueOrTax);
        Assert.False(proposal.GrantsAbstractPurchasingPower);
        Assert.False(proposal.ChangesPhysicalStockOrCanonicalSave);
        proposal.RequireCurrent(game);
        proposal.VerifyIndependent();

        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(before, game.ExportSaveJson());
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        var replayProposal = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            replay, new[] { Claim(replay.ExecutedLocalRecipientCustodyTransfers.Single()) });
        Assert.Equal(proposal.Attributions.ToArray(), replayProposal.Attributions.ToArray());
        Assert.Equal(hash, replay.CurrentHash);
        Assert.Equal(before, replay.ExportSaveJson());
    }

    [Fact]
    public void ClosureChangesHistoricalAttributionStatusButCannotBecomeSale()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var stone = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var woodHandoff = game.TransferMarketOfferToLocalRecipient(
            wood.ActionOrdinal, EconomicActivityKind.Forestry);
        var stoneHandoff = game.TransferMarketOfferToLocalRecipient(
            stone.ActionOrdinal, EconomicActivityKind.Mining);
        var declarations = new[]
        {
            Claim(stoneHandoff, "stone-intent-02", Community, Public),
            Claim(woodHandoff, "wood-intent-01", Public, Cooperative)
        };
        var before = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, declarations);
        Assert.Equal(new BigInteger(5), before.ActiveAttributedRecipientCustodyRaw);
        Assert.Equal(2, before.Attributions.Count);
        Assert.Equal(woodHandoff.ActionOrdinal,
            before.Attributions[0].PhysicalTransferActionOrdinal);

        game.UseLocalRecipientMaterial(wood.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        game.ReturnLocalRecipientCustody(stone.ActionOrdinal);
        Assert.Throws<InvalidOperationException>(before.VerifyIndependent);
        Assert.Throws<InvalidOperationException>(() => before.RequireCurrent(game));

        var after = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, declarations);
        Assert.Equal(new BigInteger(5), after.AttributedHistoricalPhysicalRaw);
        Assert.Equal(BigInteger.Zero, after.ActiveAttributedRecipientCustodyRaw);
        Assert.Equal(new BigInteger(2), after.AttributedExecutedMaterialRaw);
        Assert.Equal(new BigInteger(3), after.AttributedReturnedPhysicalRaw);
        Assert.All(after.Attributions, row =>
        {
            Assert.False(row.HasLiveRecipientCustody);
            Assert.NotNull(row.CurrentPhysicalDispositionActionOrdinal);
            Assert.False(row.ProvesAcceptedContractOrSale);
            Assert.False(row.CanAuthorizeSettlement);
        });
        Assert.Equal(
            CanonicalLocalRecipientCustodyDispositionKind.ExecuteWorkforceMaterialUse,
            after.Attributions[0].CurrentPhysicalDisposition);
        Assert.Equal(
            CanonicalLocalRecipientCustodyDispositionKind.ReturnToLocalFreeStock,
            after.Attributions[1].CurrentPhysicalDisposition);
        after.VerifyIndependent();

        var save = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var again = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            replay, declarations);
        Assert.Equal(after.Attributions.ToArray(), again.Attributions.ToArray());
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
    }

    [Fact]
    public void PartialDeclarationsNeverManufactureIdentityForUnclaimedTransfers()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var stone = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var w = game.TransferMarketOfferToLocalRecipient(
            wood.ActionOrdinal, EconomicActivityKind.Forestry);
        game.TransferMarketOfferToLocalRecipient(
            stone.ActionOrdinal, EconomicActivityKind.Mining);
        var empty = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, Array.Empty<CanonicalLocalEconomicCounterpartyDeclaration>());
        Assert.Equal(2, empty.UnattributedPhysicalTransfersCount);
        Assert.False(empty.AllRealRecipientTransfersHaveExplicitCounterparties);
        Assert.Equal(BigInteger.Zero, empty.AttributedHistoricalPhysicalRaw);
        Assert.Empty(empty.Attributions);
        empty.VerifyIndependent();

        var partial = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { Claim(w) });
        Assert.Equal(2, partial.PhysicalTransfersCount);
        Assert.Equal(1, partial.UnattributedPhysicalTransfersCount);
        Assert.False(partial.AllRealRecipientTransfersHaveExplicitCounterparties);
        Assert.Equal(new BigInteger(2), partial.AttributedHistoricalPhysicalRaw);
        Assert.False(partial.AuthorizesMonetaryRevenueOrTax);
        partial.VerifyIndependent();
    }

    [Fact]
    public void ForgedPhysicalFieldsOrForeignOrdinalFailClosedWithoutWorldMutation()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var handoff = game.TransferMarketOfferToLocalRecipient(
            wood.ActionOrdinal, EconomicActivityKind.Forestry);
        var authentic = Claim(handoff);
        var original = game.ExportSaveJson();
        var attempts = new[]
        {
            authentic with { PhysicalTransferActionOrdinal = 99999 },
            authentic with { PhysicalTransferAfterWorldHashHex = new string('A', 64) },
            authentic with { FineTile = authentic.FineTile + 1UL },
            authentic with { Commodity = 2U },
            authentic with { RawQuantity = 4UL },
            authentic with { RecipientActivity = EconomicActivityKind.Mining },
            authentic with { RecipientWorkersAtTransfer =
                authentic.RecipientWorkersAtTransfer + 1UL }
        };
        foreach (var claim in attempts)
            Assert.Throws<InvalidOperationException>(() =>
                CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
                    game, new[] { claim }));
        Assert.Equal(original, game.ExportSaveJson());
    }

    [Fact]
    public void InvalidInstitutionalDeclarationsAndDuplicateOffersCannotPass()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var stone = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var w = game.TransferMarketOfferToLocalRecipient(
            wood.ActionOrdinal, EconomicActivityKind.Forestry);
        var s = game.TransferMarketOfferToLocalRecipient(
            stone.ActionOrdinal, EconomicActivityKind.Mining);
        var valid = Claim(w);
        var other = Claim(s, "stone-intent");
        var invalid = new[]
        {
            valid with { AgreementId = "" },
            valid with { AgreementId = " duplicate " },
            valid with { AgreementVersion = 0UL },
            valid with { DeclaredSourceInstitution = Cooperative },
            valid with { DeclaredSourceInstitution =
                new ExperimentalTransportInstitution(
                    " ", ExperimentalTransportInstitutionKind.Community) },
            valid with { DeclaredReceivingInstitution =
                new ExperimentalTransportInstitution(
                    "wood-and-stone-cooperative",
                    ExperimentalTransportInstitutionKind.Unspecified) }
        };
        foreach (var declaration in invalid)
            Assert.Throws<InvalidOperationException>(() =>
                CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
                    game, new[] { declaration }));

        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
                game, new[] { valid, valid with { AgreementId = "other" } }));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
                game, new[] { valid, other with { AgreementId = valid.AgreementId } }));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
                game, new[] { valid, other with {
                    DeclaredSourceInstitution = new ExperimentalTransportInstitution(
                        Community.InstitutionId,
                        ExperimentalTransportInstitutionKind.CommercialOperator) } }));
        Assert.Throws<ArgumentException>(() =>
            CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
                game, new CanonicalLocalEconomicCounterpartyDeclaration[] { null! }));
        var otherGame = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            game.ExportSaveJson());
        var preflight = CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(
            game, new[] { valid });
        Assert.Throws<InvalidOperationException>(() =>
            preflight.RequireCurrent(otherGame));
        Assert.False(preflight.AuthenticatesInstitutionalLegalTitle);
        Assert.False(preflight.AuthenticatesBindingSaleContracts);
    }
}
