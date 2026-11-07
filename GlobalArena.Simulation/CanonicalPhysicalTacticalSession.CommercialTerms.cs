using System.Globalization;
using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// A separately reviewed, journaled agreement to relative LOCAL material
/// market terms. This is not a completed sale: payment, currency conversion,
/// tax and abstract purchasing-power settlement remain unimplemented.
/// </summary>
public sealed partial class CanonicalPhysicalTacticalSession
{
    private readonly List<CanonicalRecordedLocalCommercialTerms>
        _recordedLocalCommercialTerms = new();

    public IReadOnlyList<CanonicalRecordedLocalCommercialTerms>
        RecordedLocalCommercialTerms =>
        Array.AsReadOnly(_recordedLocalCommercialTerms.ToArray());

    public CanonicalRecordedLocalCommercialTerms AcceptLocalCommercialTerms(
        int registeredTitleActionOrdinal,
        string termsId,
        ulong termsVersion,
        ulong rawUnitsPerEconomicUnit,
        CanonicalEndogenousCurveVariant curve,
        string sourceTermsAcceptanceId,
        string recipientTermsAcceptanceId,
        ExperimentalTransportInstitution independentReviewerInstitution,
        ICanonicalLocalCommercialTermsReviewer reviewer)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        var title = _registeredLocalSimulatedEconomicTitles.SingleOrDefault(x =>
            x.ActionOrdinal == registeredTitleActionOrdinal);
        if (title is null)
            throw new InvalidOperationException(
                "Commercial terms require an actually journaled title.");
        var proposed = BuildLocalCommercialTermsEvidence(
            title, termsId, termsVersion, rawUnitsPerEconomicUnit, curve,
            sourceTermsAcceptanceId, recipientTermsAcceptanceId,
            independentReviewerInstitution);
        ValidateLocalCommercialTermsEvidence(proposed, title);
        if (!reviewer.AcceptReadOnlyCommercialTerms(proposed, title))
            throw new InvalidOperationException(
                "The host reviewer did not authenticate the separate " +
                "acceptance of commercial terms by both institutions.");
        return AcceptLocalCommercialTermsCore(proposed);
    }

    /// <summary>
    /// Replay independently recomputes relative marginal prices from the
    /// EXACT historical state before this action. Host callbacks are NOT
    /// rerun on load; unsigned IDs are not legal signatures.
    /// </summary>
    internal CanonicalRecordedLocalCommercialTerms
        ReplayLocalCommercialTerms(
            CanonicalAcceptedLocalCommercialTermsEvidence recorded) =>
        AcceptLocalCommercialTermsCore(recorded);

    private static string CanonicalInteger(BigInteger value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private CanonicalAcceptedLocalCommercialTermsEvidence
        BuildLocalCommercialTermsEvidence(
            CanonicalRegisteredLocalSimulatedEconomicTitle title,
            string termsId,
            ulong termsVersion,
            ulong scale,
            CanonicalEndogenousCurveVariant curve,
            string sourceId,
            string recipientId,
            ExperimentalTransportInstitution reviewerInstitution)
    {
        if (scale == 0UL || curve is not (
                CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand or
                CanonicalEndogenousCurveVariant.SteepDyadicSupplyDemand))
            throw new InvalidOperationException(
                "A commercial terms reference requires positive units " +
                "and an existing marginal curve variant.");
        var e = title.Evidence;
        var reference = CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(
            this, scale);
        reference.VerifyIndependent();
        var market = reference.Markets.SingleOrDefault(x =>
            x.FineTile == e.FineTile && x.Commodity == e.Commodity);
        if (market is null ||
            market.CurrentlyTitledRaw < new BigInteger(e.RawQuantity) ||
            market.StillInRecipientCustodyRaw < new BigInteger(e.RawQuantity) ||
            market.RealResidents == 0UL)
            throw new InvalidOperationException(
                "Commercial terms require a real live titled and " +
                "physically delivered local material market.");
        var average = curve ==
            CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand
            ? market.GentleAveragePriceIndex
            : market.SteepAveragePriceIndex;
        if (average.CompareTo(CanonicalEndogenousPriceIndex.Zero) <= 0)
            throw new InvalidOperationException(
                "A nonzero material lot with real local residents must " +
                "have a positive nonmonetary marginal valuation.");
        var allocated = average.Times(new BigInteger(e.RawQuantity));
        return new CanonicalAcceptedLocalCommercialTermsEvidence(
            termsId, termsVersion,
            title.ActionOrdinal, e.SourceCommitmentActionOrdinal,
            e.PhysicalTransferActionOrdinal, e.FineTile, e.Commodity,
            e.RawQuantity, title.AfterWorldHashHex,
            CurrentHash.HexDigest, State.MovementTurn,
            scale, curve, market.RealResidents,
            CanonicalInteger(market.CurrentlyTitledRaw),
            CanonicalInteger(market.NeutralDemandPressureRaw),
            CanonicalInteger(average.Numerator),
            CanonicalInteger(average.Denominator),
            CanonicalInteger(allocated.Numerator),
            CanonicalInteger(allocated.Denominator),
            sourceId, recipientId, reviewerInstitution);
    }

    private void ValidateLocalCommercialTermsEvidence(
        CanonicalAcceptedLocalCommercialTermsEvidence proposed,
        CanonicalRegisteredLocalSimulatedEconomicTitle title)
    {
        var original = title.Evidence;
        var issuer = proposed.IndependentTermsReviewerInstitution;
        if (!ValidIdentifier(proposed.TermsId) ||
            proposed.TermsVersion == 0UL ||
            proposed.TermsId == original.AgreementId ||
            !ValidIdentifier(proposed.SourceTermsAcceptanceId) ||
            !ValidIdentifier(proposed.RecipientTermsAcceptanceId) ||
            proposed.SourceTermsAcceptanceId ==
                proposed.RecipientTermsAcceptanceId ||
            proposed.SourceTermsAcceptanceId ==
                original.ReviewedSourceAuthorizationId ||
            proposed.SourceTermsAcceptanceId ==
                original.ReviewedRecipientAuthorizationId ||
            proposed.RecipientTermsAcceptanceId ==
                original.ReviewedSourceAuthorizationId ||
            proposed.RecipientTermsAcceptanceId ==
                original.ReviewedRecipientAuthorizationId ||
            !ValidInstitution(issuer) ||
            issuer.Kind is not (
                ExperimentalTransportInstitutionKind.PublicAuthority or
                ExperimentalTransportInstitutionKind.Community or
                ExperimentalTransportInstitutionKind.Cooperative) ||
            issuer.InstitutionId == original.SourceInstitution.InstitutionId ||
            issuer.InstitutionId == original.ReceivingInstitution.InstitutionId ||
            proposed.PriceReferenceWorldHashHex != CurrentHash.HexDigest ||
            proposed.PriceReferenceMovementTurn != State.MovementTurn ||
            proposed.RegisteredTitleActionOrdinal != title.ActionOrdinal ||
            proposed.SourceCommitmentActionOrdinal !=
                original.SourceCommitmentActionOrdinal ||
            proposed.PhysicalTransferActionOrdinal !=
                original.PhysicalTransferActionOrdinal ||
            proposed.OriginalTitleAfterWorldHashHex != title.AfterWorldHashHex ||
            proposed.FineTile != original.FineTile ||
            proposed.Commodity != original.Commodity ||
            proposed.MaterialRawQuantity != original.RawQuantity ||
            _reversedLocalSimulatedEconomicTitles.Any(x =>
                x.Evidence.OriginalTitleActionOrdinal == title.ActionOrdinal) ||
            _recordedLocalCommercialTerms.Any(x =>
                x.Evidence.RegisteredTitleActionOrdinal == title.ActionOrdinal ||
                x.Evidence.TermsId == proposed.TermsId) ||
            _executedLocalRecipientCustodyDispositions.Any(x =>
                x.SourceCommitmentActionOrdinal ==
                    original.SourceCommitmentActionOrdinal))
            throw new InvalidOperationException(
                "Only an unclosed, nonreversed, uniquely identified " +
                "real titled material lot may accept separately reviewed terms.");

        // Compare EVERY economic input with the independent VS78 market
        // calculation at this exact action boundary. The external caller
        // cannot inject nominal prices, wages, sale proceeds or cash.
        var expected = BuildLocalCommercialTermsEvidence(
            title, proposed.TermsId, proposed.TermsVersion,
            proposed.RawUnitsPerEconomicUnit, proposed.Curve,
            proposed.SourceTermsAcceptanceId,
            proposed.RecipientTermsAcceptanceId,
            proposed.IndependentTermsReviewerInstitution);
        if (expected != proposed)
            throw new InvalidOperationException(
                "Commercial terms' price, quantity or provenance diverge " +
                "from the real physical title and endogenous market curve.");
    }

    private CanonicalRecordedLocalCommercialTerms AcceptLocalCommercialTermsCore(
        CanonicalAcceptedLocalCommercialTermsEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        EnsureJournalCapacity();
        VerifyPhysicalStockCommitments();
        var title = _registeredLocalSimulatedEconomicTitles.SingleOrDefault(x =>
            x.ActionOrdinal == evidence.RegisteredTitleActionOrdinal);
        if (title is null)
            throw new InvalidOperationException(
                "The commercial terms' original in-game title does not exist.");
        ValidateLocalCommercialTermsEvidence(evidence, title);
        Commit(State, "accept_local_commercial_terms",
            evidence.FineTile, evidence.FineTile, 0, 0, 0UL,
            commitmentCommodity: evidence.Commodity,
            commitmentRaw: evidence.MaterialRawQuantity,
            commitmentKind: (byte)CanonicalPhysicalCommitmentKind.MarketOffer,
            commitmentOrdinal: evidence.SourceCommitmentActionOrdinal,
            commercialTermsEvidence: evidence);
        var action = _journal[^1];
        var accepted = new CanonicalRecordedLocalCommercialTerms(
            _journal.Count, State.MovementTurn,
            action.BeforeHashHex, action.AfterHashHex, evidence);
        _recordedLocalCommercialTerms.Add(accepted);
        VerifyPhysicalStockCommitments();
        return accepted;
    }

    internal void VerifyRecordedLocalCommercialTerms()
    {
        if (_journal.Count(x => x.Kind == "accept_local_commercial_terms") !=
                _recordedLocalCommercialTerms.Count ||
            _recordedLocalCommercialTerms
                .Select(x => x.Evidence.RegisteredTitleActionOrdinal)
                .Distinct().Count() != _recordedLocalCommercialTerms.Count ||
            _recordedLocalCommercialTerms
                .Select(x => x.Evidence.TermsId)
                .Distinct(StringComparer.Ordinal).Count() !=
                _recordedLocalCommercialTerms.Count)
            throw new InvalidOperationException(
                "Canonical commercial terms action is missing or duplicated.");

        foreach (var agreed in _recordedLocalCommercialTerms)
        {
            var e = agreed.Evidence;
            var title = _registeredLocalSimulatedEconomicTitles.SingleOrDefault(x =>
                x.ActionOrdinal == e.RegisteredTitleActionOrdinal);
            if (title is null || agreed.ActionOrdinal <= title.ActionOrdinal ||
                agreed.ActionOrdinal > _journal.Count)
                throw new InvalidOperationException(
                    "Unregistered title cannot back accepted commercial terms.");
            var original = title.Evidence;
            var action = _journal[agreed.ActionOrdinal - 1];
            if (action.Kind != "accept_local_commercial_terms" ||
                action.CommercialTermsEvidence != e ||
                action.TitleEvidence is not null ||
                action.TitleReversalEvidence is not null ||
                action.Source != e.FineTile ||
                action.Destination != e.FineTile ||
                action.FromJob != 0 || action.ToJob != 0 ||
                action.Workers != 0UL ||
                action.FoodRaw != 0UL ||
                action.MaterialCommodity != 0U ||
                action.MaterialUseKind != 0 ||
                action.MaterialRaw != 0UL ||
                action.CommitmentKind !=
                    (byte)CanonicalPhysicalCommitmentKind.MarketOffer ||
                action.CommitmentCommodity != e.Commodity ||
                action.CommitmentRaw != e.MaterialRawQuantity ||
                action.CommitmentOrdinal != e.SourceCommitmentActionOrdinal ||
                action.BeforeHashHex != agreed.BeforeWorldHashHex ||
                action.AfterHashHex != agreed.AfterWorldHashHex ||
                e.PriceReferenceWorldHashHex != agreed.BeforeWorldHashHex ||
                e.PriceReferenceMovementTurn != agreed.MovementTurn ||
                e.SourceCommitmentActionOrdinal !=
                    original.SourceCommitmentActionOrdinal ||
                e.PhysicalTransferActionOrdinal !=
                    original.PhysicalTransferActionOrdinal ||
                e.OriginalTitleAfterWorldHashHex != title.AfterWorldHashHex ||
                e.FineTile != original.FineTile ||
                e.Commodity != original.Commodity ||
                e.MaterialRawQuantity != original.RawQuantity ||
                !ValidIdentifier(e.TermsId) ||
                e.TermsId == original.AgreementId ||
                e.TermsVersion == 0UL ||
                !ValidIdentifier(e.SourceTermsAcceptanceId) ||
                !ValidIdentifier(e.RecipientTermsAcceptanceId) ||
                e.SourceTermsAcceptanceId == e.RecipientTermsAcceptanceId ||
                !ValidInstitution(e.IndependentTermsReviewerInstitution) ||
                e.IndependentTermsReviewerInstitution.Kind is not (
                    ExperimentalTransportInstitutionKind.PublicAuthority or
                    ExperimentalTransportInstitutionKind.Community or
                    ExperimentalTransportInstitutionKind.Cooperative) ||
                e.IndependentTermsReviewerInstitution.InstitutionId ==
                    original.SourceInstitution.InstitutionId ||
                e.IndependentTermsReviewerInstitution.InstitutionId ==
                    original.ReceivingInstitution.InstitutionId ||
                _reversedLocalSimulatedEconomicTitles.Any(x =>
                    x.Evidence.OriginalTitleActionOrdinal ==
                        title.ActionOrdinal) ||
                _executedLocalRecipientCustodyDispositions.Any(x =>
                    x.SourceCommitmentActionOrdinal ==
                        original.SourceCommitmentActionOrdinal &&
                    (x.ActionOrdinal < agreed.ActionOrdinal ||
                     x.ProvesReturnOfSamePhysicalCustody)))
                throw new InvalidOperationException(
                    "Historical commercial terms differ from the material " +
                    "handoff, in-game title, exact action or acceptance IDs.");
        }
    }
}
