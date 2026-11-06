namespace GlobalArena.Simulation;

/// <summary>
/// Two-party, explicitly reviewed request to reverse IN-GAME economic title.
/// All references are to an already registered canonical title and its
/// real physical source. An authority supplied by the host must review
/// this specific request, independently of the original two grants.
/// Historical authorization identifiers are not cryptographic signatures.
/// </summary>
public sealed record CanonicalLocalSimulatedTitleReversalEvidence(
    int OriginalTitleActionOrdinal,
    int SourceCommitmentActionOrdinal,
    string OriginalTitleAfterWorldHashHex,
    string ReviewWorldHashHex,
    string ReversalAgreementId,
    ulong ReversalAgreementVersion,
    string FormerHolderAuthorizationId,
    string OriginalHolderAcceptanceId,
    ExperimentalTransportInstitution IndependentIssuingInstitution,
    ulong ValidFromMovementTurn,
    ulong ValidThroughMovementTurn);

/// <summary>
/// Host boundary: no production implementation in the kernel, no implicit
/// approvals. Only called at original execution, not during deterministic
/// save replay. A test stub does not constitute a legal instrument.
/// </summary>
public interface ICanonicalLocalSimulatedTitleReversalReviewer
{
    bool AcceptReadOnlyTitleReversal(
        CanonicalLocalSimulatedTitleReversalEvidence evidence,
        CanonicalRegisteredLocalSimulatedEconomicTitle registeredTitle);
}

/// <summary>
/// Canonical historical event: reverses economic title *within the game*,
/// without transporting the physical material, changing legal title
/// outside the game, posting a sale, receiving income or issuing credit.
/// </summary>
public sealed record CanonicalReversedLocalSimulatedEconomicTitle(
    int ActionOrdinal,
    ulong MovementTurn,
    string BeforeWorldHashHex,
    string AfterWorldHashHex,
    CanonicalLocalSimulatedTitleReversalEvidence Evidence)
{
    public bool ReversesInGameTitle => true;
    public bool ReversesPhysicalHandoff => false;
    public bool ExecutesCommercialSale => false;
    public bool PostsMonetaryRefundOrTax => false;
    public bool HasLegallyAuthenticatedTitle => false;
    public bool ReplayReauthorizesExternalReviewer => false;
}

/// <summary>
/// Current, read-only classification of a single registered historical title.
/// A reversed lot still physically sits with the former recipient until an
/// independent local-recipient custody RETURN moves it to the free stock.
/// </summary>
public sealed record CanonicalLocalSimulatedTitlePosition(
    CanonicalRegisteredLocalSimulatedEconomicTitle Registration,
    CanonicalReversedLocalSimulatedEconomicTitle? Reversal,
    CanonicalLocalRecipientCustodyDispositionKind? PhysicalDisposition)
{
    public bool IsReversed => Reversal is not null;
    public bool IsInRecipientCustody => PhysicalDisposition is null;
    public ExperimentalTransportInstitution CurrentInGameEconomicHolder =>
        IsReversed
            ? Registration.PreviousRecognizedEconomicHolder
            : Registration.CurrentRecognizedEconomicHolder;
    public bool ProvesCommercialSale => false;
    public bool AuthorizesMonetarySettlement => false;
}
