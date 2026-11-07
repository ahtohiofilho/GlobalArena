namespace GlobalArena.Simulation;

/// <summary>
/// Explicit, two-party acknowledgement of REAL delivered material and
/// an exact endogenous RELATIVE price reference. No cash is paid: this
/// is an executed COMMERCIAL TERMS acknowledgement, NOT a settled sale.
/// The quote uses actual local residents and currently registered
/// simulated economic title, never a household wallet or hard demand cap.
/// Rational numerators and denominators are canonical decimal strings.
/// </summary>
public sealed record CanonicalAcceptedLocalCommercialTermsEvidence(
    string TermsId,
    ulong TermsVersion,
    int RegisteredTitleActionOrdinal,
    int SourceCommitmentActionOrdinal,
    int PhysicalTransferActionOrdinal,
    ulong FineTile,
    uint Commodity,
    ulong MaterialRawQuantity,
    string OriginalTitleAfterWorldHashHex,
    string PriceReferenceWorldHashHex,
    ulong PriceReferenceMovementTurn,
    ulong RawUnitsPerEconomicUnit,
    CanonicalEndogenousCurveVariant Curve,
    ulong RealLocalResidents,
    string MarketCurrentlyTitledRaw,
    string LocalDemandPressureRaw,
    string RelativeAveragePriceNumerator,
    string RelativeAveragePriceDenominator,
    string AllocatedRelativeGrossNumerator,
    string AllocatedRelativeGrossDenominator,
    string SourceTermsAcceptanceId,
    string RecipientTermsAcceptanceId,
    ExperimentalTransportInstitution IndependentTermsReviewerInstitution);

/// <summary>
/// Interface boundary for verifying the fresh, separate consent of BOTH
/// parties to this exact commercial reference. The kernel supplies NO
/// permissive implementation; test stubs cannot confer legal ownership,
/// monetary proceeds, fiscal powers, or real-world legal agreements.
/// </summary>
public interface ICanonicalLocalCommercialTermsReviewer
{
    bool AcceptReadOnlyCommercialTerms(
        CanonicalAcceptedLocalCommercialTermsEvidence proposed,
        CanonicalRegisteredLocalSimulatedEconomicTitle registeredTitle);
}

/// <summary>
/// Actual canonical *acceptance of terms*, anchored in real material and
/// in-game title. The material remains in physical custody and may be
/// consumed only by the existing canonical workforce-use command.
/// A nominally priced, paid commercial sale does not exist yet.
/// </summary>
public sealed record CanonicalRecordedLocalCommercialTerms(
    int ActionOrdinal,
    ulong MovementTurn,
    string BeforeWorldHashHex,
    string AfterWorldHashHex,
    CanonicalAcceptedLocalCommercialTermsEvidence Evidence)
{
    public bool RecordsExecutedTwoPartyTermsAcceptance => true;
    public bool ConfirmsUnderlyingPhysicalDelivery => true;
    public bool ConfirmsInGameEconomicTitle => true;
    public bool RegistersNominalCurrencyPrice => false;
    public bool ProvesCommercialSaleSettled => false;
    public bool ExecutesBuyerPaymentOrCredit => false;
    public bool AuthorizesProducerRevenueOrTreasuryTax => false;
    public bool MovesOrConsumesAdditionalMaterial => false;
    public bool RatifiesPurchasingPowerCalibration => false;
    public bool ReplaysExternalConsentAuthentication => false;
}
