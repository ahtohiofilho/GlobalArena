using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Institutional identity is not an individual worker wallet. The type
/// describes an expressly supplied institutional actor, never inferred from
/// economic point, tactical tile or freight route ownership.
/// </summary>
public enum ExperimentalTransportInstitutionKind
{
    Unspecified = 0,
    PublicAuthority = 1,
    Community = 2,
    Cooperative = 3,
    CommercialOperator = 4
}

public sealed record ExperimentalTransportInstitution(
    string InstitutionId,
    ExperimentalTransportInstitutionKind Kind);

public sealed record ExperimentalTransportFreightRevenueShare(
    ExperimentalTransportInstitution Beneficiary,
    FoodTransportCostFraction Fraction);

/// <summary>
/// Service operator and freight revenue beneficiaries are independent fields.
/// The operator need not receive any revenue unless explicitly in the shares.
/// </summary>
public sealed record ExperimentalTransportServiceEntitlement(
    EconomicPointId Source,
    EconomicPointId Destination,
    ulong ServiceId,
    ExperimentalTransportInstitution Operator,
    IReadOnlyList<ExperimentalTransportFreightRevenueShare> RevenueShares);

/// <summary>
/// External institutional registry declaration pinned to physical state and
/// economic calibration. Network revision is a caller-supplied provenance
/// label; ONLY the external authority can attest that it is genuine.
/// </summary>
public sealed class ExperimentalTransportInstitutionalRegistry
{
    public string RegistryId { get; }
    public ulong RegistryVersion { get; }
    public string WorldStateHash { get; }
    public ulong NetworkRevision { get; }
    public string PurchasingPolicyId { get; }
    public ulong PurchasingPolicyVersion { get; }
    public string CalibrationId { get; }
    public ulong CalibrationVersion { get; }
    public string NominalQuoteUnitId { get; }
    public IReadOnlyList<ExperimentalTransportServiceEntitlement> Entitlements { get; }

    public ExperimentalTransportInstitutionalRegistry(
        string registryId, ulong registryVersion,
        string worldStateHash, ulong networkRevision,
        string purchasingPolicyId, ulong purchasingPolicyVersion,
        string calibrationId, ulong calibrationVersion,
        string nominalQuoteUnitId,
        IEnumerable<ExperimentalTransportServiceEntitlement> entitlements)
    {
        if (string.IsNullOrWhiteSpace(registryId) || registryVersion == 0 ||
            string.IsNullOrWhiteSpace(worldStateHash) || networkRevision == 0 ||
            string.IsNullOrWhiteSpace(purchasingPolicyId) ||
            purchasingPolicyVersion == 0 ||
            string.IsNullOrWhiteSpace(calibrationId) ||
            calibrationVersion == 0 ||
            string.IsNullOrWhiteSpace(nominalQuoteUnitId))
            throw new ArgumentException(
                "Institutional provenance requires stable, nonempty versioned metadata.");
        ArgumentNullException.ThrowIfNull(entitlements);

        RegistryId = registryId;
        RegistryVersion = registryVersion;
        WorldStateHash = worldStateHash;
        NetworkRevision = networkRevision;
        PurchasingPolicyId = purchasingPolicyId;
        PurchasingPolicyVersion = purchasingPolicyVersion;
        CalibrationId = calibrationId;
        CalibrationVersion = calibrationVersion;
        NominalQuoteUnitId = nominalQuoteUnitId;
        // Defensive copy of the collection and nested benefit shares.
        // A caller's later modifications must not silently transfer revenue.
        var rows = entitlements.Select(x =>
        {
            if (x is null || x.RevenueShares is null)
                throw new ArgumentException("Entitlement must include its revenue shares.",
                    nameof(entitlements));
            return x with
            {
                RevenueShares = Array.AsReadOnly(x.RevenueShares.ToArray())
            };
        }).ToArray();
        Entitlements = Array.AsReadOnly(rows);
    }
}

/// <summary>
/// NO permissive built-in implementation. In an operational integration the
/// host must authenticate the registry issuer, world/network revision and
/// actual service authorization. A laboratory stub is NOT a settlement grant.
/// </summary>
public interface IExperimentalTransportInstitutionalRegistryAuthority
{
    bool AcceptReadOnlyRegistry(
        ExperimentalTransportInstitutionalRegistry registry);
}

public sealed record ExperimentalTransportFreightBeneficiaryCharge(
    ExperimentalTransportInstitution Beneficiary,
    FoodTransportCostFraction AbstractFreightRevenue);

public sealed record ExperimentalTransportInstitutionalLot(
    ulong LotId,
    EconomicPointId Source,
    EconomicPointId Buyer,
    ulong ServiceId,
    ExperimentalTransportInstitution Operator,
    FoodTransportCostFraction AbstractFreightDebit,
    IReadOnlyList<ExperimentalTransportFreightBeneficiaryCharge> Beneficiaries);

public sealed record ExperimentalTransportInstitutionalAggregate(
    ExperimentalTransportInstitution Institution,
    FoodTransportCostFraction AbstractFreightReceivable);

public sealed record ExperimentalTransportInstitutionalProjection(
    string RegistryId,
    ulong RegistryVersion,
    string WorldStateHash,
    ulong NetworkRevision,
    ExperimentalAbstractPhysicalFreightReconciliation PhysicalReconciliation,
    IReadOnlyList<ExperimentalTransportInstitutionalLot> Lots,
    IReadOnlyList<ExperimentalTransportInstitutionalAggregate> InstitutionalReceivables,
    FoodTransportCostFraction AbstractBuyerFreightCharges,
    FoodTransportCostFraction AbstractInstitutionalFreightReceivables)
{
    // Accepted provenance makes an AUDITABLE proposal, not title deeds,
    // spendable cash, a bank balance, dispatch, tax or SETTLE authorization.
    public bool CanAuthorizeRealSettlement => false;
}

/// <summary>
/// READ-ONLY attribution of VS54's already reconciled freight to explicitly
/// certified institutional beneficiaries. Checks the live session hash,
/// exact service coverage, policy/version/unit and rational shares.
/// No government tax, household cash, operator salary or residual creditor
/// is invented; the full freight is allocated once or the projection fails.
/// </summary>
public sealed class ExperimentalTransportInstitutionalFreightProjector
{
    public ExperimentalTransportInstitutionalProjection Project(
        CanonicalPhysicalTacticalSession session,
        ExperimentalAbstractPurchasingProjection purchasing,
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> nominalQuotes,
        ExperimentalAbstractPriceFreightCalibrationPolicy calibrationPolicy,
        string physicalFreightNominalUnitId,
        IEnumerable<ExperimentalSimultaneousFreightLane> offeredLanes,
        IEnumerable<ExperimentalAbstractPhysicalFreightDelivery> selectedDeliveries,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        ExperimentalTransportInstitutionalRegistry registry,
        IExperimentalTransportInstitutionalRegistryAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(purchasing);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(nominalQuotes);
        ArgumentNullException.ThrowIfNull(calibrationPolicy);
        ArgumentNullException.ThrowIfNull(offeredLanes);
        ArgumentNullException.ThrowIfNull(selectedDeliveries);
        ArgumentNullException.ThrowIfNull(congestionPolicy);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(authority);

        var currentHash = session.CurrentHash;
        if (!string.Equals(registry.WorldStateHash, currentHash,
                StringComparison.Ordinal) ||
            !string.Equals(registry.PurchasingPolicyId,
                purchasing.PolicyId, StringComparison.Ordinal) ||
            registry.PurchasingPolicyVersion != purchasing.PolicyVersion ||
            !string.Equals(registry.PurchasingPolicyId,
                calibrationPolicy.PurchasingPolicyId, StringComparison.Ordinal) ||
            registry.PurchasingPolicyVersion !=
                calibrationPolicy.PurchasingPolicyVersion ||
            !string.Equals(registry.CalibrationId,
                calibrationPolicy.CalibrationId, StringComparison.Ordinal) ||
            registry.CalibrationVersion != calibrationPolicy.Version ||
            !string.Equals(registry.NominalQuoteUnitId,
                calibrationPolicy.NominalQuoteUnitId, StringComparison.Ordinal))
            throw new ArgumentException(
                "Transport institutional registry is stale or mismatched to the physical world or abstract pricing policy.",
                nameof(registry));

        var actualServices = purchasing.UnresolvedCarriers
            .Select(x => (x.Source, x.Destination, x.ServiceId)).ToHashSet();
        if (actualServices.Count != purchasing.UnresolvedCarriers.Count)
            throw new ArgumentException(
                "The original physical service provenance is not unique.",
                nameof(purchasing));

        var rights = new Dictionary<
            (EconomicPointId Source, EconomicPointId Destination, ulong ServiceId),
            ExperimentalTransportServiceEntitlement>();
        var institutionKinds = new Dictionary<string,
            ExperimentalTransportInstitutionKind>(StringComparer.Ordinal);

        void ValidateInstitution(ExperimentalTransportInstitution institution)
        {
            if (institution is null ||
                string.IsNullOrWhiteSpace(institution.InstitutionId) ||
                !Enum.IsDefined(institution.Kind) ||
                institution.Kind == ExperimentalTransportInstitutionKind.Unspecified)
                throw new ArgumentException(
                    "A revenue entitlement requires an explicit institution identity and type.",
                    nameof(registry));
            if (institutionKinds.TryGetValue(institution.InstitutionId, out var kind))
            {
                if (kind != institution.Kind)
                    throw new ArgumentException(
                        "One institution ID cannot have contradictory legal categories.",
                        nameof(registry));
            }
            else
                institutionKinds.Add(institution.InstitutionId, institution.Kind);
        }

        foreach (var entitlement in registry.Entitlements)
        {
            if (entitlement is null ||
                !entitlement.Source.IsValid ||
                !entitlement.Destination.IsValid ||
                entitlement.ServiceId == 0 ||
                !rights.TryAdd((entitlement.Source, entitlement.Destination,
                    entitlement.ServiceId), entitlement))
                throw new ArgumentException(
                    "Each declared service requires one unique institutional entitlement.",
                    nameof(registry));
            ValidateInstitution(entitlement.Operator);
            if (entitlement.RevenueShares is null ||
                entitlement.RevenueShares.Count == 0)
                throw new ArgumentException(
                    "Institutional freight proceeds cannot disappear into an undeclared residual.",
                    nameof(registry));
            var beneficiaryIds = new HashSet<string>(StringComparer.Ordinal);
            var totalShare = ExperimentalCashMoney.Zero;
            foreach (var share in entitlement.RevenueShares)
            {
                if (share is null || share.Fraction is null ||
                    share.Fraction.Numerator.Sign <= 0)
                    throw new ArgumentException(
                        "Revenue shares must be strictly positive rational fractions.",
                        nameof(registry));
                ValidateInstitution(share.Beneficiary);
                if (!beneficiaryIds.Add(share.Beneficiary.InstitutionId))
                    throw new ArgumentException(
                        "A beneficiary may occur only once per service.",
                        nameof(registry));
                totalShare = ExperimentalCashMoney.Add(totalShare, share.Fraction);
            }
            if (totalShare.CompareTo(new FoodTransportCostFraction(1, 1)) != 0)
                throw new ArgumentException(
                    "Every service must distribute exactly 100% of its freight, with no invented tax or rounding remainder.",
                    nameof(registry));
        }
        if (!rights.Keys.ToHashSet().SetEquals(actualServices))
            throw new ArgumentException(
                "Institutional registry must cover exactly the VS52 declared services.",
                nameof(registry));

        if (!authority.AcceptReadOnlyRegistry(registry))
            throw new InvalidOperationException(
                "External institutional authority rejected the registry.");

        // VS54 is recomputed from actual inputs; an unrelated or stale
        // caller-generated reconciliation is never accepted as evidence.
        var physical = new ExperimentalAbstractPhysicalFreightReconciler()
            .Project(purchasing, flows, catalog, nominalQuotes,
                calibrationPolicy, physicalFreightNominalUnitId,
                offeredLanes, selectedDeliveries, congestionPolicy);

        var byInstitution = new Dictionary<string, (
            ExperimentalTransportInstitution Institution,
            FoodTransportCostFraction Receivable)>(StringComparer.Ordinal);
        var lots = new List<ExperimentalTransportInstitutionalLot>();
        var buyerTotal = ExperimentalCashMoney.Zero;
        foreach (var delivery in physical.Deliveries.OrderBy(x => x.LotId))
        {
            var right = rights[(delivery.Source, delivery.Buyer, delivery.ServiceId)];
            var fragments = new List<ExperimentalTransportFreightBeneficiaryCharge>();
            var chargeTotal = ExperimentalCashMoney.Zero;
            foreach (var share in right.RevenueShares.OrderBy(
                         x => x.Beneficiary.InstitutionId, StringComparer.Ordinal))
            {
                var amount = ExperimentalCashMoney.Multiply(
                    delivery.AbstractPhysicalFreight,
                    share.Fraction.Numerator, share.Fraction.Denominator);
                chargeTotal = ExperimentalCashMoney.Add(chargeTotal, amount);
                fragments.Add(new ExperimentalTransportFreightBeneficiaryCharge(
                    share.Beneficiary, amount));
                if (byInstitution.TryGetValue(
                        share.Beneficiary.InstitutionId, out var existing))
                    byInstitution[share.Beneficiary.InstitutionId] =
                        (existing.Institution,
                            ExperimentalCashMoney.Add(existing.Receivable, amount));
                else
                    byInstitution.Add(share.Beneficiary.InstitutionId,
                        (share.Beneficiary, amount));
            }
            if (chargeTotal.CompareTo(delivery.AbstractPhysicalFreight) != 0)
                throw new InvalidOperationException(
                    "Distributed institutional revenue cannot exceed or fall short of buyer freight charge.");
            buyerTotal = ExperimentalCashMoney.Add(
                buyerTotal, delivery.AbstractPhysicalFreight);
            lots.Add(new ExperimentalTransportInstitutionalLot(
                delivery.LotId, delivery.Source, delivery.Buyer,
                delivery.ServiceId, right.Operator,
                delivery.AbstractPhysicalFreight,
                Array.AsReadOnly(fragments.ToArray())));
        }

        var payees = byInstitution.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new ExperimentalTransportInstitutionalAggregate(
                x.Value.Institution, x.Value.Receivable)).ToArray();
        var revenue = ExperimentalCashMoney.Zero;
        foreach (var amount in payees)
            revenue = ExperimentalCashMoney.Add(
                revenue, amount.AbstractFreightReceivable);
        if (buyerTotal.CompareTo(physical.AbstractPhysicalFreight) != 0 ||
            revenue.CompareTo(buyerTotal) != 0)
            throw new InvalidOperationException(
                "Institutional freight must conserve the exact VS54 per-buyer charges.");

        if (!string.Equals(session.CurrentHash, currentHash,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Institutional projection observed a changing physical world.");

        return new ExperimentalTransportInstitutionalProjection(
            registry.RegistryId, registry.RegistryVersion, registry.WorldStateHash,
            registry.NetworkRevision, physical, Array.AsReadOnly(lots.ToArray()),
            Array.AsReadOnly(payees), buyerTotal, revenue);
    }
}
