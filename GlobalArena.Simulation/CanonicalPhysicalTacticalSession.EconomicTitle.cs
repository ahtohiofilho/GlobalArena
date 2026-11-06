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

    private readonly List<CanonicalReversedLocalSimulatedEconomicTitle>
        _reversedLocalSimulatedEconomicTitles = new();

    public IReadOnlyList<CanonicalReversedLocalSimulatedEconomicTitle>
        ReversedLocalSimulatedEconomicTitles =>
        Array.AsReadOnly(_reversedLocalSimulatedEconomicTitles.ToArray());

    public IReadOnlyList<CanonicalLocalSimulatedTitlePosition>
        LocalSimulatedTitlePositions =>
        Array.AsReadOnly(_registeredLocalSimulatedEconomicTitles
            .Select(title => new CanonicalLocalSimulatedTitlePosition(
                title,
                _reversedLocalSimulatedEconomicTitles.SingleOrDefault(x =>
                    x.Evidence.OriginalTitleActionOrdinal == title.ActionOrdinal),
                _executedLocalRecipientCustodyDispositions.SingleOrDefault(x =>
                    x.SourceCommitmentActionOrdinal ==
                        title.Evidence.SourceCommitmentActionOrdinal)?.Kind))
            .ToArray());

    /// <summary>
    /// A NEW externally reviewed agreement reverses a previously registered
    /// simulated title while physical goods remain in the recipient's custody.
    /// The reversal does not itself return, move, sell or consume the lot.
    /// </summary>
    public CanonicalReversedLocalSimulatedEconomicTitle
        ReverseLocalSimulatedEconomicTitle(
            int originalTitleActionOrdinal,
            CanonicalLocalSimulatedTitleReversalEvidence evidence,
            ICanonicalLocalSimulatedTitleReversalReviewer reviewer)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(reviewer);
        if (evidence.OriginalTitleActionOrdinal != originalTitleActionOrdinal)
            throw new InvalidOperationException(
                "Reversal proposal does not identify the requested original title.");
        var title = _registeredLocalSimulatedEconomicTitles.SingleOrDefault(x =>
            x.ActionOrdinal == originalTitleActionOrdinal);
        if (title is null)
            throw new InvalidOperationException(
                "An actual registered in-game title is required.");
        ValidateLocalSimulatedTitleReversal(evidence, title);
        if (!reviewer.AcceptReadOnlyTitleReversal(evidence, title))
            throw new InvalidOperationException(
                "External reviewer did not approve the two-party title reversal.");
        return ReverseLocalSimulatedEconomicTitleCore(evidence);
    }

    /// <summary>
    /// Canonical replayer verifies recorded facts, but must NOT manufacture
    /// an external authority or invoke host callbacks while loading saves.
    /// </summary>
    internal CanonicalReversedLocalSimulatedEconomicTitle
        ReplayLocalSimulatedEconomicTitleReversal(
            CanonicalLocalSimulatedTitleReversalEvidence recorded) =>
        ReverseLocalSimulatedEconomicTitleCore(recorded);

    private void ValidateLocalSimulatedTitleReversal(
        CanonicalLocalSimulatedTitleReversalEvidence e,
        CanonicalRegisteredLocalSimulatedEconomicTitle title)
    {
        var first = title.Evidence;
        var issuer = e.IndependentIssuingInstitution;
        if (e.OriginalTitleActionOrdinal != title.ActionOrdinal ||
            e.SourceCommitmentActionOrdinal != first.SourceCommitmentActionOrdinal ||
            e.OriginalTitleAfterWorldHashHex != title.AfterWorldHashHex ||
            e.ReviewWorldHashHex != CurrentHash.HexDigest ||
            e.ReversalAgreementVersion == 0UL ||
            !ValidIdentifier(e.ReversalAgreementId) ||
            e.ReversalAgreementId == first.AgreementId ||
            !ValidIdentifier(e.FormerHolderAuthorizationId) ||
            !ValidIdentifier(e.OriginalHolderAcceptanceId) ||
            e.FormerHolderAuthorizationId == e.OriginalHolderAcceptanceId ||
            e.FormerHolderAuthorizationId == first.ReviewedSourceAuthorizationId ||
            e.FormerHolderAuthorizationId == first.ReviewedRecipientAuthorizationId ||
            e.OriginalHolderAcceptanceId == first.ReviewedSourceAuthorizationId ||
            e.OriginalHolderAcceptanceId == first.ReviewedRecipientAuthorizationId ||
            !ValidInstitution(issuer) ||
            issuer.Kind is not (
                ExperimentalTransportInstitutionKind.PublicAuthority or
                ExperimentalTransportInstitutionKind.Community or
                ExperimentalTransportInstitutionKind.Cooperative) ||
            issuer.InstitutionId == first.SourceInstitution.InstitutionId ||
            issuer.InstitutionId == first.ReceivingInstitution.InstitutionId ||
            e.ValidFromMovementTurn > State.MovementTurn ||
            e.ValidThroughMovementTurn < State.MovementTurn ||
            e.ValidThroughMovementTurn < e.ValidFromMovementTurn ||
            _reversedLocalSimulatedEconomicTitles.Any(x =>
                x.Evidence.OriginalTitleActionOrdinal == title.ActionOrdinal ||
                x.Evidence.ReversalAgreementId == e.ReversalAgreementId) ||
            _executedLocalRecipientCustodyDispositions.Any(x =>
                x.SourceCommitmentActionOrdinal == first.SourceCommitmentActionOrdinal))
            throw new InvalidOperationException(
                "Reversal requires a unique, currently valid two-sided authorization " +
                "for an unreversed and physically unclosed original title.");
    }

    private CanonicalReversedLocalSimulatedEconomicTitle
        ReverseLocalSimulatedEconomicTitleCore(
            CanonicalLocalSimulatedTitleReversalEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        EnsureJournalCapacity();
        VerifyPhysicalStockCommitments();
        var title = _registeredLocalSimulatedEconomicTitles.SingleOrDefault(x =>
            x.ActionOrdinal == evidence.OriginalTitleActionOrdinal);
        if (title is null)
            throw new InvalidOperationException(
                "Title reversal cannot exist without registered source title.");
        ValidateLocalSimulatedTitleReversal(evidence, title);
        var e = title.Evidence;
        Commit(State, "reverse_local_economic_title",
            e.FineTile, e.FineTile, 0, 0, 0UL,
            commitmentCommodity: e.Commodity,
            commitmentRaw: e.RawQuantity,
            commitmentKind: (byte)CanonicalPhysicalCommitmentKind.MarketOffer,
            commitmentOrdinal: e.SourceCommitmentActionOrdinal,
            titleReversalEvidence: evidence);
        var action = _journal[^1];
        var row = new CanonicalReversedLocalSimulatedEconomicTitle(
            _journal.Count, State.MovementTurn,
            action.BeforeHashHex, action.AfterHashHex, evidence);
        _reversedLocalSimulatedEconomicTitles.Add(row);
        VerifyPhysicalStockCommitments();
        return row;
    }

    private void VerifyReversedLocalSimulatedEconomicTitles()
    {
        if (_journal.Count(x => x.Kind == "reverse_local_economic_title") !=
                _reversedLocalSimulatedEconomicTitles.Count ||
            _reversedLocalSimulatedEconomicTitles
                .Select(x => x.Evidence.OriginalTitleActionOrdinal)
                .Distinct().Count() != _reversedLocalSimulatedEconomicTitles.Count ||
            _reversedLocalSimulatedEconomicTitles
                .Select(x => x.Evidence.ReversalAgreementId)
                .Distinct(StringComparer.Ordinal).Count() !=
                    _reversedLocalSimulatedEconomicTitles.Count)
            throw new InvalidOperationException(
                "Duplicate, unjournaled or missing in-game title reversal.");

        foreach (var reversal in _reversedLocalSimulatedEconomicTitles)
        {
            var e = reversal.Evidence;
            if (reversal.ActionOrdinal <= e.OriginalTitleActionOrdinal ||
                reversal.ActionOrdinal > _journal.Count)
                throw new InvalidOperationException("Invalid reversal event order.");
            var action = _journal[reversal.ActionOrdinal - 1];
            var title = _registeredLocalSimulatedEconomicTitles.SingleOrDefault(x =>
                x.ActionOrdinal == e.OriginalTitleActionOrdinal);
            if (title is null)
                throw new InvalidOperationException(
                    "Reversal points to missing historical title.");
            var original = title.Evidence;
            var issuer = e.IndependentIssuingInstitution;
            if (action.Kind != "reverse_local_economic_title" ||
                action.TitleReversalEvidence != e ||
                action.TitleEvidence is not null ||
                action.Source != original.FineTile ||
                action.Destination != original.FineTile ||
                action.CommitmentOrdinal != original.SourceCommitmentActionOrdinal ||
                action.CommitmentCommodity != original.Commodity ||
                action.CommitmentRaw != original.RawQuantity ||
                action.CommitmentKind !=
                    (byte)CanonicalPhysicalCommitmentKind.MarketOffer ||
                action.FromJob != 0 || action.ToJob != 0 || action.Workers != 0UL ||
                action.FoodRaw != 0UL || action.MaterialRaw != 0UL ||
                action.BeforeHashHex != reversal.BeforeWorldHashHex ||
                action.AfterHashHex != reversal.AfterWorldHashHex ||
                e.SourceCommitmentActionOrdinal != original.SourceCommitmentActionOrdinal ||
                e.OriginalTitleAfterWorldHashHex != title.AfterWorldHashHex ||
                e.ReviewWorldHashHex != reversal.BeforeWorldHashHex ||
                e.ReversalAgreementVersion == 0UL ||
                !ValidIdentifier(e.ReversalAgreementId) ||
                e.ReversalAgreementId == original.AgreementId ||
                !ValidIdentifier(e.FormerHolderAuthorizationId) ||
                !ValidIdentifier(e.OriginalHolderAcceptanceId) ||
                e.FormerHolderAuthorizationId == e.OriginalHolderAcceptanceId ||
                e.FormerHolderAuthorizationId == original.ReviewedSourceAuthorizationId ||
                e.FormerHolderAuthorizationId == original.ReviewedRecipientAuthorizationId ||
                e.OriginalHolderAcceptanceId == original.ReviewedSourceAuthorizationId ||
                e.OriginalHolderAcceptanceId == original.ReviewedRecipientAuthorizationId ||
                !ValidInstitution(issuer) ||
                issuer.Kind is not (
                    ExperimentalTransportInstitutionKind.PublicAuthority or
                    ExperimentalTransportInstitutionKind.Community or
                    ExperimentalTransportInstitutionKind.Cooperative) ||
                issuer.InstitutionId == original.SourceInstitution.InstitutionId ||
                issuer.InstitutionId == original.ReceivingInstitution.InstitutionId ||
                e.ValidFromMovementTurn > reversal.MovementTurn ||
                e.ValidThroughMovementTurn < reversal.MovementTurn ||
                _executedLocalRecipientCustodyDispositions.Any(x =>
                    x.SourceCommitmentActionOrdinal == original.SourceCommitmentActionOrdinal &&
                    (x.ActionOrdinal < reversal.ActionOrdinal ||
                     x.ProvesExecutedMaterialUse)))
                throw new InvalidOperationException(
                    "Historical title reversal contradicts the exact registered " +
                    "title, reviewed authorization or physical custody.");
        }
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
                     (x.ProvesReturnOfSamePhysicalCustody &&
                        !_reversedLocalSimulatedEconomicTitles.Any(r =>
                            r.Evidence.OriginalTitleActionOrdinal ==
                                title.ActionOrdinal &&
                            r.ActionOrdinal < x.ActionOrdinal)))))
                throw new InvalidOperationException(
                    "In-game title diverged from the historical physical custody and approval identity.");
        }
        VerifyReversedLocalSimulatedEconomicTitles();
    }
}
