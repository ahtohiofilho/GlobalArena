namespace GlobalArena.Simulation;

/// <summary>
/// A transfer of IN-GAME economic title for one previously and physically
/// delivered wood/stone lot. The kernel registers it as a canonical game
/// rule, NOT as a real-world legal title deed, sale, tax event or payment.
/// Institutional identities and grant IDs come from a contemporaneously
/// reviewed 01L/01M snapshot; replay checks this immutable declaration
/// against the physical journal but cannot re-authenticate an external
/// reviewer after the fact.
/// </summary>
public sealed record CanonicalLocalSimulatedEconomicTitleEvidence(
    int SourceCommitmentActionOrdinal,
    int PhysicalTransferActionOrdinal,
    string PhysicalTransferAfterWorldHashHex,
    ulong FineTile,
    uint Commodity,
    ulong RawQuantity,
    string AgreementId,
    ulong AgreementVersion,
    ExperimentalTransportInstitution SourceInstitution,
    ExperimentalTransportInstitution ReceivingInstitution,
    string ReviewedSourceAuthorizationId,
    string ReviewedRecipientAuthorizationId);

/// <summary>
/// Replayable acknowledgement that the game has registered an economic
/// owner distinct from physical custody. Historical ownership remains
/// auditable after productive consumption; return to the former free-stock
/// owner is blocked until an explicit title reversal exists.
/// </summary>
public sealed record CanonicalRegisteredLocalSimulatedEconomicTitle(
    int ActionOrdinal,
    ulong MovementTurn,
    string BeforeWorldHashHex,
    string AfterWorldHashHex,
    CanonicalLocalSimulatedEconomicTitleEvidence Evidence)
{
    public ExperimentalTransportInstitution CurrentRecognizedEconomicHolder =>
        Evidence.ReceivingInstitution;
    public ExperimentalTransportInstitution PreviousRecognizedEconomicHolder =>
        Evidence.SourceInstitution;
    public bool IsRegisteredInGameEconomicTitle => true;
    public bool AuthenticatesLegallyEnforceableOwnership => false;
    public bool ConstitutesExecutedCommercialSale => false;
    public bool ConstitutesPaymentOrSettlement => false;
    public bool CreatesMoneyTaxOrBuyerWallets => false;
    public bool MovesOrConsumesPhysicalGoods => false;
    public bool ReplayCanReapproveExternalGrants => false;
}
