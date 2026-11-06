using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// Two DIFFERENT proposed institutional permissions are required: an
/// institution's authority to dispose of this physical offered lot, and
/// the named counterparty's authority to accept it. No such permission
/// may be fabricated by guessing a worker group or a tile identity.
/// </summary>
public enum CanonicalLocalEconomicRightScope : byte
{
    SourceOfferDisposition = 1,
    RecipientPhysicalAcceptance = 2
}

/// <summary>
/// A caller-supplied, versioned, world-bound RIGHTS REVIEW declaration,
/// not an authenticated legal title or a contract of sale. Authenticity
/// depends on a separately implemented, non-permissive host reviewer.
/// </summary>
public sealed record CanonicalLocalEconomicRightClaim(
    string AuthorizationId,
    ulong AuthorizationVersion,
    string CurrentWorldHashHex,
    int PhysicalTransferActionOrdinal,
    string PhysicalTransferAfterWorldHashHex,
    string AgreementId,
    ulong AgreementVersion,
    CanonicalLocalEconomicRightScope Scope,
    ExperimentalTransportInstitution ProposedRightsHolder,
    ExperimentalTransportInstitution ProposedIssuingAuthority,
    ulong EffectiveFromMovementTurn,
    ulong EffectiveThroughMovementTurn);

/// <summary>
/// Host-side review boundary. The kernel has NO default implementation.
/// Accepting a laboratory claim means only "reviewer accepted this
/// declaration for a read-only audit"; it is not legal title, payment,
/// or a command allowing physical stock to move again.
/// </summary>
public interface ICanonicalLocalEconomicRightsReviewer
{
    bool AcceptReadOnlyRight(
        CanonicalLocalEconomicRightClaim claim,
        CanonicalLocalEconomicRecipientAttribution physicallyAuthenticatedParty);
}

public sealed record CanonicalLocalEconomicRightsReviewRow(
    CanonicalLocalEconomicRecipientAttribution PhysicalAttribution,
    string? ReviewedSourceAuthorizationId,
    string? ReviewedRecipientAuthorizationId)
{
    public bool HasReviewedBothProposedRights =>
        ReviewedSourceAuthorizationId is not null &&
        ReviewedRecipientAuthorizationId is not null;
    public bool HasLivePhysicalCustody =>
        PhysicalAttribution.HasLiveRecipientCustody;
    public bool IsCommercialSale => false;
    public bool TransfersLegalTitle => false;
    public bool AuthorizesPaymentOrTaxes => false;
}

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01M. Bind explicitly declared party rights to
/// the EXACT 01L physical attribution and current 01K custody position.
/// No institution or reviewer is inferred; missing permissions remain
/// missing. All approvals are revocable: VerifyIndependent re-queries the
/// caller-supplied reviewer, and any world mutation makes this stale.
/// </summary>
public sealed class CanonicalLocalEconomicRightsReviewPreflight
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalLocalEconomicRecipientIdentityPreflight _parties;
    private readonly ICanonicalLocalEconomicRightsReviewer _reviewer;
    private readonly CanonicalLocalEconomicRightClaim[] _claims;
    private readonly CanonicalLocalEconomicRightsReviewRow[] _rows;
    private readonly string _snapshotHash;
    private readonly int _snapshotEvents;

    public IReadOnlyList<CanonicalLocalEconomicRightClaim> Claims { get; }
    public IReadOnlyList<CanonicalLocalEconomicRightsReviewRow> Agreements { get; }
    public int FullyReviewedLocalCustodiesCount =>
        _rows.Count(x => x.HasReviewedBothProposedRights);
    public int UnreviewedOrPartlyReviewedCount =>
        _rows.Length - FullyReviewedLocalCustodiesCount;
    public BigInteger FullyReviewedActivePhysicalRaw =>
        _rows.Where(x => x.HasReviewedBothProposedRights &&
            x.HasLivePhysicalCustody)
            .Aggregate(BigInteger.Zero,
                (total, x) => total + x.PhysicalAttribution.RawQuantity);

    public bool UsesRealJournaledPhysicalHandoffs => true;
    public bool RequiresExplicitIndependentIssuingAuthority => true;
    public bool UsesHostSuppliedRightsReviewer => true;
    public bool HasBuiltInPermissiveRightsReviewer => false;
    public bool TreatsWorkerGroupsAsLegalOwners => false;
    public bool AuthenticatesLegallyEnforceableTitle => false;
    public bool IssuesBindingSalesContracts => false;
    public bool ExecutesCustodyOrTitleChanges => false;
    public bool AuthorizesMoneySettlementOrTaxes => false;
    public bool CreatesBuyerCashOrCredit => false;
    public bool MutatesWorldStateOrCanonicalSave => false;

    private CanonicalLocalEconomicRightsReviewPreflight(
        CanonicalPhysicalTacticalSession session,
        CanonicalLocalEconomicRecipientIdentityPreflight parties,
        CanonicalLocalEconomicRightClaim[] claims,
        ICanonicalLocalEconomicRightsReviewer reviewer)
    {
        _session = session;
        _parties = parties;
        _reviewer = reviewer;
        _snapshotHash = session.CurrentHash.HexDigest;
        _snapshotEvents = session.Events.Count;
        _claims = (CanonicalLocalEconomicRightClaim[])claims.Clone();
        Claims = Array.AsReadOnly(_claims);
        _rows = Reconstruct(session, parties, _claims, reviewer);
        Agreements = Array.AsReadOnly(_rows);
        VerifyIndependent();
    }

    public static CanonicalLocalEconomicRightsReviewPreflight FromCurrent(
        CanonicalPhysicalTacticalSession session,
        CanonicalLocalEconomicRecipientIdentityPreflight parties,
        IEnumerable<CanonicalLocalEconomicRightClaim> proposedRights,
        ICanonicalLocalEconomicRightsReviewer reviewer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(parties);
        ArgumentNullException.ThrowIfNull(proposedRights);
        ArgumentNullException.ThrowIfNull(reviewer);
        parties.RequireCurrent(session);
        parties.VerifyIndependent();
        var claims = proposedRights.ToArray();
        if (claims.Any(x => x is null))
            throw new ArgumentException(
                "Rights reviews cannot contain null grants.", nameof(proposedRights));
        return new(session, parties, claims, reviewer);
    }

    private static CanonicalLocalEconomicRightsReviewRow[] Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalLocalEconomicRecipientIdentityPreflight parties,
        CanonicalLocalEconomicRightClaim[] claims,
        ICanonicalLocalEconomicRightsReviewer reviewer)
    {
        parties.RequireCurrent(session);
        parties.VerifyIndependent();
        var actors = new Dictionary<string,
            ExperimentalTransportInstitutionKind>(StringComparer.Ordinal);
        var byTransfer = parties.Attributions.ToDictionary(x =>
            x.PhysicalTransferActionOrdinal);
        var grants = new Dictionary<(int,
            CanonicalLocalEconomicRightScope), string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var current = session.State.MovementTurn;
        foreach (var claim in claims.OrderBy(x => x.PhysicalTransferActionOrdinal)
                     .ThenBy(x => x.Scope))
        {
            if (string.IsNullOrWhiteSpace(claim.AuthorizationId) ||
                claim.AuthorizationId != claim.AuthorizationId.Trim() ||
                claim.AuthorizationId.Length > 128 ||
                !seenIds.Add(claim.AuthorizationId) ||
                claim.AuthorizationVersion == 0UL ||
                claim.Scope is not (
                    CanonicalLocalEconomicRightScope.SourceOfferDisposition or
                    CanonicalLocalEconomicRightScope.RecipientPhysicalAcceptance) ||
                claim.CurrentWorldHashHex != session.CurrentHash.HexDigest ||
                claim.EffectiveFromMovementTurn > current ||
                claim.EffectiveThroughMovementTurn < current ||
                claim.EffectiveThroughMovementTurn < claim.EffectiveFromMovementTurn ||
                !byTransfer.TryGetValue(claim.PhysicalTransferActionOrdinal,
                    out var party) ||
                !party.HasLiveRecipientCustody ||
                party.PhysicalTransferAfterWorldHashHex !=
                    claim.PhysicalTransferAfterWorldHashHex ||
                party.AgreementId != claim.AgreementId ||
                party.AgreementVersion != claim.AgreementVersion)
                throw new InvalidOperationException(
                    "A proposed right must match a current, unclosed physical transfer, " +
                    "its agreement and a bounded, presently valid authorization.");

            var expected = claim.Scope ==
                CanonicalLocalEconomicRightScope.SourceOfferDisposition
                ? party.DeclaredSourceInstitution
                : party.DeclaredReceivingInstitution;
            var issuer = claim.ProposedIssuingAuthority;
            if (claim.ProposedRightsHolder != expected ||
                issuer is null ||
                string.IsNullOrWhiteSpace(issuer.InstitutionId) ||
                issuer.InstitutionId != issuer.InstitutionId.Trim() ||
                issuer.InstitutionId.Length > 128 ||
                issuer.Kind is not (
                    ExperimentalTransportInstitutionKind.Community or
                    ExperimentalTransportInstitutionKind.Cooperative or
                    ExperimentalTransportInstitutionKind.PublicAuthority) ||
                issuer.InstitutionId == party.DeclaredSourceInstitution.InstitutionId ||
                issuer.InstitutionId == party.DeclaredReceivingInstitution.InstitutionId ||
                !grants.TryAdd((claim.PhysicalTransferActionOrdinal,
                    claim.Scope), claim.AuthorizationId))
                throw new InvalidOperationException(
                    "Rights need the correct physical party, unique scope and " +
                    "an explicitly separate, typed reviewing institution.");

            foreach (var actor in new[] { expected, issuer })
            {
                if (actors.TryGetValue(actor.InstitutionId, out var kind) &&
                    kind != actor.Kind)
                    throw new InvalidOperationException(
                        "Institution identifier has contradictory governance types.");
                actors[actor.InstitutionId] = actor.Kind;
            }
            // No built-in permissive implementation. A host reviewer must
            // authenticate the grant provenance. This is not yet legal title.
            if (!reviewer.AcceptReadOnlyRight(claim, party))
                throw new InvalidOperationException(
                    "The external rights reviewer refused the proposal.");
        }

        return parties.Attributions.OrderBy(x => x.PhysicalTransferActionOrdinal)
            .Select(x => new CanonicalLocalEconomicRightsReviewRow(
                x,
                grants.GetValueOrDefault((x.PhysicalTransferActionOrdinal,
                    CanonicalLocalEconomicRightScope.SourceOfferDisposition)),
                grants.GetValueOrDefault((x.PhysicalTransferActionOrdinal,
                    CanonicalLocalEconomicRightScope.RecipientPhysicalAcceptance))))
            .ToArray();
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(session, _session) ||
            _session.CurrentHash.HexDigest != _snapshotHash ||
            _session.Events.Count != _snapshotEvents)
            throw new InvalidOperationException(
                "Rights review is foreign or stale.");
        _parties.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var rebuilt = Reconstruct(_session, _parties, _claims, _reviewer);
        if (!_rows.SequenceEqual(rebuilt))
            throw new InvalidOperationException(
                "Rights review diverged from current physical evidence and issuer.");
        RequireCurrent(_session);
    }
}
