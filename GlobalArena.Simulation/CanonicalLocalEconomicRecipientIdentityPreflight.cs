using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// An explicit proposed institutional counterpart of ONE actually executed
/// local market-to-workgroup custody handoff. It is NOT a title deed, a buyer
/// account, a seller balance or an enforceable sale contract. The proposal is
/// tied to the exact physical journal action and its resulting world hash.
/// </summary>
public sealed record CanonicalLocalEconomicCounterpartyDeclaration(
    string AgreementId,
    ulong AgreementVersion,
    int PhysicalTransferActionOrdinal,
    string PhysicalTransferAfterWorldHashHex,
    ulong FineTile,
    uint Commodity,
    ulong RawQuantity,
    EconomicActivityKind RecipientActivity,
    ulong RecipientWorkersAtTransfer,
    ExperimentalTransportInstitution DeclaredSourceInstitution,
    ExperimentalTransportInstitution DeclaredReceivingInstitution);

/// <summary>
/// A physical handoff's separately declared institutional identities. A
/// closure from 01K remains material evidence, not sale or legal title.
/// </summary>
public sealed record CanonicalLocalEconomicRecipientAttribution(
    string AgreementId,
    ulong AgreementVersion,
    int SourceCommitmentActionOrdinal,
    int PhysicalTransferActionOrdinal,
    string PhysicalTransferAfterWorldHashHex,
    ulong FineTile,
    uint Commodity,
    ulong RawQuantity,
    EconomicActivityKind RecipientActivity,
    ulong RecipientWorkersAtTransfer,
    ExperimentalTransportInstitution DeclaredSourceInstitution,
    ExperimentalTransportInstitution DeclaredReceivingInstitution,
    CanonicalLocalRecipientCustodyDispositionKind? CurrentPhysicalDisposition,
    int? CurrentPhysicalDispositionActionOrdinal)
{
    public bool HasLiveRecipientCustody => CurrentPhysicalDisposition is null;
    public bool ProvesActualLocalPhysicalHandoff => true;
    public bool ProvesLegalEconomicOwnership => false;
    public bool ProvesAcceptedContractOrSale => false;
    public bool ProvesDeliveryToRemoteBuyer => false;
    public bool CanAuthorizeSettlement => false;
}

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01L: read-only, fail-closed join between physical
/// recipient receipts and EXPLICIT counterpart identities. Does not infer
/// an institutional owner from workers, tiles, or commodity; a named actor
/// is only a declaration until a distinct governance authority ratifies it.
/// </summary>
public sealed class CanonicalLocalEconomicRecipientIdentityPreflight
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly string _worldHash;
    private readonly int _eventCount;
    private readonly CanonicalLocalEconomicCounterpartyDeclaration[] _declarations;
    private readonly CanonicalLocalEconomicRecipientAttribution[] _rows;

    public IReadOnlyList<CanonicalLocalEconomicCounterpartyDeclaration>
        Declarations { get; }
    public IReadOnlyList<CanonicalLocalEconomicRecipientAttribution>
        Attributions { get; }
    public int PhysicalTransfersCount { get; }
    public int UnattributedPhysicalTransfersCount =>
        PhysicalTransfersCount - _rows.Length;
    public BigInteger AttributedHistoricalPhysicalRaw { get; }
    public BigInteger ActiveAttributedRecipientCustodyRaw { get; }
    public BigInteger AttributedReturnedPhysicalRaw { get; }
    public BigInteger AttributedExecutedMaterialRaw { get; }
    public bool AllRealRecipientTransfersHaveExplicitCounterparties =>
        UnattributedPhysicalTransfersCount == 0;

    public bool AuthenticatesActualLocalRecipientHandoffs => true;
    public bool UsesExplicitInstitutionalCounterpartDeclarations => true;
    public bool InfersBuyerIdentityFromLaborOrSite => false;
    public bool AuthenticatesInstitutionalLegalTitle => false;
    public bool AuthenticatesBindingSaleContracts => false;
    public bool AuthenticatesExecutedBuyerPayment => false;
    public bool AuthorizesMonetaryRevenueOrTax => false;
    public bool GrantsAbstractPurchasingPower => false;
    public bool ChangesPhysicalStockOrCanonicalSave => false;

    private CanonicalLocalEconomicRecipientIdentityPreflight(
        CanonicalPhysicalTacticalSession session,
        CanonicalLocalEconomicCounterpartyDeclaration[] declarations)
    {
        _session = session;
        _worldHash = session.CurrentHash.HexDigest;
        _eventCount = session.Events.Count;
        _declarations = (CanonicalLocalEconomicCounterpartyDeclaration[])declarations.Clone();
        Declarations = Array.AsReadOnly(_declarations);
        _rows = Reconstruct(session, _declarations);
        Attributions = Array.AsReadOnly(_rows);
        PhysicalTransfersCount = session.ExecutedLocalRecipientCustodyTransfers.Count;
        AttributedHistoricalPhysicalRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        ActiveAttributedRecipientCustodyRaw = _rows.Where(x =>
            x.HasLiveRecipientCustody).Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        AttributedReturnedPhysicalRaw = _rows.Where(x =>
            x.CurrentPhysicalDisposition ==
                CanonicalLocalRecipientCustodyDispositionKind.ReturnToLocalFreeStock)
            .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        AttributedExecutedMaterialRaw = _rows.Where(x =>
            x.CurrentPhysicalDisposition ==
                CanonicalLocalRecipientCustodyDispositionKind.ExecuteWorkforceMaterialUse)
            .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        VerifyIndependent();
    }

    public static CanonicalLocalEconomicRecipientIdentityPreflight FromCurrent(
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalLocalEconomicCounterpartyDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(declarations);
        var supplied = declarations.ToArray();
        if (supplied.Any(x => x is null))
            throw new ArgumentException(
                "Institutional agreement declarations cannot contain null records.",
                nameof(declarations));
        return new(session, supplied);
    }

    private static void RequireInstitution(ExperimentalTransportInstitution? actor)
    {
        if (actor is null || string.IsNullOrWhiteSpace(actor.InstitutionId) ||
            actor.InstitutionId != actor.InstitutionId.Trim() ||
            actor.InstitutionId.Length > 128 ||
            actor.Kind is not (
                ExperimentalTransportInstitutionKind.PublicAuthority or
                ExperimentalTransportInstitutionKind.Community or
                ExperimentalTransportInstitutionKind.Cooperative or
                ExperimentalTransportInstitutionKind.CommercialOperator))
            throw new InvalidOperationException(
                "Only an explicitly identified, typed institutional actor can be proposed.");
    }

    private static CanonicalLocalEconomicRecipientAttribution[] Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalLocalEconomicCounterpartyDeclaration[] claims)
    {
        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(session);
        coverage.VerifyIndependent();
        var transfers = session.ExecutedLocalRecipientCustodyTransfers.ToDictionary(x =>
            x.ActionOrdinal);
        var closures = session.ExecutedLocalRecipientCustodyDispositions.ToDictionary(x =>
            x.OriginalTransferActionOrdinal);
        if (claims.Select(x => x.AgreementId).Distinct(StringComparer.Ordinal)
                .Count() != claims.Length ||
            claims.Select(x => x.PhysicalTransferActionOrdinal).Distinct()
                .Count() != claims.Length)
            throw new InvalidOperationException(
                "One unique agreement identifier and at most one proposal per real transfer.");

        var institutionKinds = new Dictionary<string,
            ExperimentalTransportInstitutionKind>(StringComparer.Ordinal);
        var rows = new List<CanonicalLocalEconomicRecipientAttribution>();
        foreach (var claim in claims.OrderBy(x => x.PhysicalTransferActionOrdinal))
        {
            RequireInstitution(claim.DeclaredSourceInstitution);
            RequireInstitution(claim.DeclaredReceivingInstitution);
            if (string.IsNullOrWhiteSpace(claim.AgreementId) ||
                claim.AgreementId != claim.AgreementId.Trim() ||
                claim.AgreementId.Length > 128 ||
                claim.AgreementVersion == 0UL ||
                string.IsNullOrWhiteSpace(claim.PhysicalTransferAfterWorldHashHex) ||
                claim.DeclaredSourceInstitution == claim.DeclaredReceivingInstitution ||
                claim.DeclaredSourceInstitution.InstitutionId ==
                    claim.DeclaredReceivingInstitution.InstitutionId)
                throw new InvalidOperationException(
                    "A proposed counterpart agreement needs distinct, explicitly named institutions and versioned identity.");
            foreach (var actor in new[]
                { claim.DeclaredSourceInstitution, claim.DeclaredReceivingInstitution })
            {
                if (institutionKinds.TryGetValue(actor.InstitutionId, out var kind) &&
                    kind != actor.Kind)
                    throw new InvalidOperationException(
                        "One institutional identifier cannot silently change legal actor type.");
                institutionKinds[actor.InstitutionId] = actor.Kind;
            }

            if (!transfers.TryGetValue(claim.PhysicalTransferActionOrdinal,
                    out var receipt) ||
                receipt.AfterWorldHashHex != claim.PhysicalTransferAfterWorldHashHex ||
                receipt.FineTile != claim.FineTile ||
                receipt.Commodity != claim.Commodity ||
                receipt.RawQuantity != claim.RawQuantity ||
                receipt.RecipientActivity != claim.RecipientActivity ||
                receipt.RecipientWorkers != claim.RecipientWorkersAtTransfer ||
                !receipt.ProvesPhysicalCustodyHandoff ||
                receipt.ProvesExecutedCommercialSale ||
                receipt.ProvesLegalOwnershipTransfer)
                throw new InvalidOperationException(
                    "An institutional proposal must match the exact journaled local physical handoff.");

            closures.TryGetValue(receipt.ActionOrdinal, out var closed);
            if (closed is not null &&
                (closed.SourceCommitmentActionOrdinal !=
                    receipt.SourceCommitmentActionOrdinal ||
                 closed.OriginalRecipientActivity != receipt.RecipientActivity ||
                 closed.RawQuantity != receipt.RawQuantity))
                throw new InvalidOperationException(
                    "Recipient agreement cannot conceal or alter an actual physical return/use.");

            rows.Add(new CanonicalLocalEconomicRecipientAttribution(
                claim.AgreementId, claim.AgreementVersion,
                receipt.SourceCommitmentActionOrdinal,
                receipt.ActionOrdinal, receipt.AfterWorldHashHex,
                receipt.FineTile, receipt.Commodity, receipt.RawQuantity,
                receipt.RecipientActivity, receipt.RecipientWorkers,
                claim.DeclaredSourceInstitution,
                claim.DeclaredReceivingInstitution,
                closed?.Kind, closed?.ActionOrdinal));
        }

        if (rows.Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity) >
            session.ExecutedLocalRecipientCustodyTransfers.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.RawQuantity))
            throw new InvalidOperationException(
                "Declared transfer quantities exceed real journaled custody handoffs.");

        return rows.ToArray();
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(_session, session) ||
            _session.CurrentHash.HexDigest != _worldHash ||
            _session.Events.Count != _eventCount)
            throw new InvalidOperationException(
                "Counterparty identity preflight is foreign or stale.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var updated = Reconstruct(_session, _declarations);
        if (!_rows.SequenceEqual(updated) ||
            PhysicalTransfersCount !=
                _session.ExecutedLocalRecipientCustodyTransfers.Count ||
            AttributedHistoricalPhysicalRaw != updated.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.RawQuantity) ||
            ActiveAttributedRecipientCustodyRaw != updated.Where(x =>
                x.HasLiveRecipientCustody).Aggregate(
                    BigInteger.Zero, (sum, x) => sum + x.RawQuantity) ||
            AttributedReturnedPhysicalRaw != updated.Where(x =>
                x.CurrentPhysicalDisposition ==
                    CanonicalLocalRecipientCustodyDispositionKind.ReturnToLocalFreeStock)
                .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity) ||
            AttributedExecutedMaterialRaw != updated.Where(x =>
                x.CurrentPhysicalDisposition ==
                    CanonicalLocalRecipientCustodyDispositionKind.ExecuteWorkforceMaterialUse)
                .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity))
            throw new InvalidOperationException(
                "Institutional proposal no longer reconciles with real physical custody.");
        RequireCurrent(_session);
    }
}
