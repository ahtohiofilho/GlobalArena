using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// In-game economic title for a precise real wood/stone handoff.
/// Crucial separation: material custody is a physical fact (01J/01K);
/// in-game title is registered only after an independent 01M review.
/// No real-world legal title or settlement follows from this receipt.
/// </summary>
public sealed partial class CanonicalPhysicalTacticalSession
{
    private readonly List<CanonicalRegisteredLocalSimulatedEconomicTitle>
        _registeredLocalSimulatedEconomicTitles = new();

    public IReadOnlyList<CanonicalRegisteredLocalSimulatedEconomicTitle>
        RegisteredLocalSimulatedEconomicTitles =>
        Array.AsReadOnly(_registeredLocalSimulatedEconomicTitles.ToArray());

    public CanonicalRegisteredLocalSimulatedEconomicTitle
        RegisterLocalSimulatedEconomicTitle(
            int commitmentActionOrdinal,
            CanonicalLocalEconomicRightsReviewPreflight reviewedRights)
    {
        ArgumentNullException.ThrowIfNull(reviewedRights);
        reviewedRights.RequireCurrent(this);
        reviewedRights.VerifyIndependent();
        var original = _executedLocalRecipientCustodyTransfers.SingleOrDefault(x =>
            x.SourceCommitmentActionOrdinal == commitmentActionOrdinal);
        if (original is null)
            throw new InvalidOperationException(
                "Economic title can only attach to an actually delivered material lot.");

        var agreement = reviewedRights.Agreements.SingleOrDefault(x =>
            x.PhysicalAttribution.SourceCommitmentActionOrdinal ==
                commitmentActionOrdinal);
        if (agreement is null || !agreement.HasReviewedBothProposedRights ||
            !agreement.HasLivePhysicalCustody ||
            string.IsNullOrWhiteSpace(agreement.ReviewedSourceAuthorizationId) ||
            string.IsNullOrWhiteSpace(agreement.ReviewedRecipientAuthorizationId))
            throw new InvalidOperationException(
                "Both physical lot counterparties must have distinct reviewed rights.");

        var party = agreement.PhysicalAttribution;
        var record = new CanonicalLocalSimulatedEconomicTitleEvidence(
            commitmentActionOrdinal, original.ActionOrdinal,
            original.AfterWorldHashHex, original.FineTile,
            original.Commodity, original.RawQuantity,
            party.AgreementId, party.AgreementVersion,
            party.DeclaredSourceInstitution,
            party.DeclaredReceivingInstitution,
            agreement.ReviewedSourceAuthorizationId,
            agreement.ReviewedRecipientAuthorizationId);
        return RegisterLocalSimulatedEconomicTitleCore(record);
    }

    /// <summary>
    /// During replay, enforce every IN-GAME physical and journal invariant
    /// against the frozen approval identifiers. External reviewers are NOT
    /// re-run and their legal authority is NOT independently certified.
    /// </summary>
    internal CanonicalRegisteredLocalSimulatedEconomicTitle
        ReplayLocalSimulatedEconomicTitle(
            CanonicalLocalSimulatedEconomicTitleEvidence recorded) =>
        RegisterLocalSimulatedEconomicTitleCore(recorded);

    private CanonicalRegisteredLocalSimulatedEconomicTitle
        RegisterLocalSimulatedEconomicTitleCore(
            CanonicalLocalSimulatedEconomicTitleEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        EnsureJournalCapacity();
        VerifyPhysicalStockCommitments();

        var source = _physicalStockCommitments.SingleOrDefault(x =>
            x.ActionOrdinal == evidence.SourceCommitmentActionOrdinal);
        var physical = _executedLocalRecipientCustodyTransfers.SingleOrDefault(x =>
            x.ActionOrdinal == evidence.PhysicalTransferActionOrdinal);
        var closed = _executedLocalRecipientCustodyDispositions.Any(x =>
            x.SourceCommitmentActionOrdinal == evidence.SourceCommitmentActionOrdinal);
        if (source is null || !source.Transferred ||
            source.Kind != CanonicalPhysicalCommitmentKind.MarketOffer ||
            source.Commodity is not (2U or 3U) ||
            physical is null || closed ||
            physical.SourceCommitmentActionOrdinal != source.ActionOrdinal ||
            source.FineTile != evidence.FineTile ||
            source.Commodity != evidence.Commodity ||
            source.RawQuantity != evidence.RawQuantity ||
            physical.AfterWorldHashHex != evidence.PhysicalTransferAfterWorldHashHex ||
            physical.FineTile != evidence.FineTile ||
            physical.Commodity != evidence.Commodity ||
            physical.RawQuantity != evidence.RawQuantity ||
            evidence.AgreementVersion == 0UL ||
            !ValidIdentifier(evidence.AgreementId) ||
            !ValidIdentifier(evidence.ReviewedSourceAuthorizationId) ||
            !ValidIdentifier(evidence.ReviewedRecipientAuthorizationId) ||
            evidence.ReviewedSourceAuthorizationId ==
                evidence.ReviewedRecipientAuthorizationId ||
            !ValidInstitution(evidence.SourceInstitution) ||
            !ValidInstitution(evidence.ReceivingInstitution) ||
            evidence.SourceInstitution.InstitutionId ==
                evidence.ReceivingInstitution.InstitutionId ||
            _registeredLocalSimulatedEconomicTitles.Any(x =>
                x.Evidence.SourceCommitmentActionOrdinal ==
                    source.ActionOrdinal ||
                x.Evidence.AgreementId == evidence.AgreementId))
            throw new InvalidOperationException(
                "In-game title requires one unique, live, physically authentic " +
                "handoff and an explicit valid two-party authorization record.");

        Commit(State, "register_local_economic_title",
            evidence.FineTile, evidence.FineTile, 0, 0, 0UL,
            commitmentCommodity: evidence.Commodity,
            commitmentRaw: evidence.RawQuantity,
            commitmentKind: (byte)CanonicalPhysicalCommitmentKind.MarketOffer,
            commitmentOrdinal: evidence.SourceCommitmentActionOrdinal,
            titleEvidence: evidence);
        var action = _journal[^1];
        var registered = new CanonicalRegisteredLocalSimulatedEconomicTitle(
            _journal.Count, State.MovementTurn, action.BeforeHashHex,
            action.AfterHashHex, evidence);
        _registeredLocalSimulatedEconomicTitles.Add(registered);
        VerifyPhysicalStockCommitments();
        return registered;
    }

    private static bool ValidIdentifier(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 128 &&
        id == id.Trim();

    private static bool ValidInstitution(ExperimentalTransportInstitution? actor) =>
        actor is not null && ValidIdentifier(actor.InstitutionId) &&
        actor.Kind is (
            ExperimentalTransportInstitutionKind.PublicAuthority or
            ExperimentalTransportInstitutionKind.Community or
            ExperimentalTransportInstitutionKind.Cooperative or
            ExperimentalTransportInstitutionKind.CommercialOperator);

    /// <summary>
    /// This is a game simulation registration, not external property law.
    /// Recheck that no event, title or later return exists without a real
    /// delivered commitment; material-use consumption can follow title.
    /// </summary>
    internal void VerifyRegisteredLocalSimulatedEconomicTitles()
    {
        var titleEvents = _journal
            .Where(x => x.Kind == "register_local_economic_title").ToArray();
        if (titleEvents.Length != _registeredLocalSimulatedEconomicTitles.Count ||
            _registeredLocalSimulatedEconomicTitles
                .Select(x => x.Evidence.SourceCommitmentActionOrdinal)
                .Distinct().Count() != titleEvents.Length ||
            _registeredLocalSimulatedEconomicTitles
                .Select(x => x.Evidence.AgreementId)
                .Distinct(StringComparer.Ordinal).Count() != titleEvents.Length)
            throw new InvalidOperationException(
                "Unbacked, duplicated or missing in-game title registration.");

        foreach (var title in _registeredLocalSimulatedEconomicTitles)
        {
            if (title.ActionOrdinal <= title.Evidence.PhysicalTransferActionOrdinal ||
                title.ActionOrdinal > _journal.Count)
                throw new InvalidOperationException(
                    "In-game title registered without prior physical transfer.");
            var action = _journal[title.ActionOrdinal - 1];
            var e = title.Evidence;
            var source = _physicalStockCommitments.SingleOrDefault(x =>
                x.ActionOrdinal == e.SourceCommitmentActionOrdinal);
            var delivered = _executedLocalRecipientCustodyTransfers.SingleOrDefault(x =>
                x.ActionOrdinal == e.PhysicalTransferActionOrdinal);
            if (action.Kind != "register_local_economic_title" ||
                action.TitleEvidence != e ||
                action.Source != e.FineTile ||
                action.Destination != e.FineTile ||
                action.CommitmentOrdinal != e.SourceCommitmentActionOrdinal ||
                action.CommitmentCommodity != e.Commodity ||
                action.CommitmentRaw != e.RawQuantity ||
                action.CommitmentKind !=
                    (byte)CanonicalPhysicalCommitmentKind.MarketOffer ||
                action.FromJob != 0 || action.ToJob != 0 ||
                action.Workers != 0UL ||
                action.FoodRaw != 0UL || action.MaterialRaw != 0UL ||
                action.BeforeHashHex != title.BeforeWorldHashHex ||
                action.AfterHashHex != title.AfterWorldHashHex ||
                source is null || delivered is null ||
                !source.Transferred ||
                source.FineTile != e.FineTile ||
                source.Commodity != e.Commodity ||
                source.RawQuantity != e.RawQuantity ||
                source.Kind != CanonicalPhysicalCommitmentKind.MarketOffer ||
                delivered.SourceCommitmentActionOrdinal != source.ActionOrdinal ||
                delivered.AfterWorldHashHex != e.PhysicalTransferAfterWorldHashHex ||
                delivered.FineTile != e.FineTile ||
                delivered.Commodity != e.Commodity ||
                delivered.RawQuantity != e.RawQuantity ||
                !ValidIdentifier(e.AgreementId) || e.AgreementVersion == 0UL ||
                !ValidIdentifier(e.ReviewedSourceAuthorizationId) ||
                !ValidIdentifier(e.ReviewedRecipientAuthorizationId) ||
                e.ReviewedSourceAuthorizationId == e.ReviewedRecipientAuthorizationId ||
                !ValidInstitution(e.SourceInstitution) ||
                !ValidInstitution(e.ReceivingInstitution) ||
                e.SourceInstitution.InstitutionId ==
                    e.ReceivingInstitution.InstitutionId ||
                _executedLocalRecipientCustodyDispositions.Any(x =>
                    x.SourceCommitmentActionOrdinal ==
                        e.SourceCommitmentActionOrdinal &&
                    (x.ActionOrdinal < title.ActionOrdinal ||
                     x.ProvesReturnOfSamePhysicalCustody)))
                throw new InvalidOperationException(
                    "In-game title diverged from the historical physical custody and approval identity.");
        }
    }
}
