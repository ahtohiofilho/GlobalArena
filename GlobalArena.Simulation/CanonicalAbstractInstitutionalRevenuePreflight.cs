using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Optional ownership/revenue allocation HYPOTHESIS for one actual physical
/// source/commodity/activity. Permille must sum to 1000 for each active source.
/// This is not proof of title, tax authority or any spendable currency.
/// </summary>
public sealed record CanonicalAbstractRevenueEntitlement(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity,
    ExperimentalTransportInstitution Beneficiary,
    int SharePermille);

/// <summary>
/// Every local or authenticated remote source contributes to the destination
/// market's integrated marginal relative-revenue index IN PROPORTION TO ITS
/// PHYSICAL VOLUME. Buyer demand is never a cap or a wallet.
/// </summary>
public sealed record CanonicalAbstractRevenueSourceMarketRow(
    EconomicPointId Source, EconomicPointId Market, CommodityId Commodity,
    EconomicActivityKind Activity, BigInteger HypotheticalPhysicalRaw,
    CanonicalEndogenousPriceIndex GentleRelativeGrossIndex,
    CanonicalEndogenousPriceIndex SteepRelativeGrossIndex);

/// <summary>
/// Hypothetical allocation of source-attributed value to an EXPLICIT
/// institutional beneficiary. PublicAuthority is not an actual treasury
/// posting. Values remain relative indices WITHIN ONE commodity.
/// </summary>
public sealed record CanonicalAbstractInstitutionalRevenueRow(
    ExperimentalTransportInstitution Beneficiary, CommodityId Commodity,
    CanonicalEndogenousPriceIndex GentleRelativeGrossIndex,
    CanonicalEndogenousPriceIndex SteepRelativeGrossIndex);

public sealed record CanonicalAbstractRevenueCommodityReconciliation(
    CommodityId Commodity, EconomicActivityKind Activity,
    BigInteger HypotheticallyAbsorbedPhysicalRaw,
    CanonicalEndogenousPriceIndex TotalGentleRelativeGrossIndex,
    CanonicalEndogenousPriceIndex TotalSteepRelativeGrossIndex,
    CanonicalEndogenousPriceIndex PublicGentleRelativeGrossIndex,
    CanonicalEndogenousPriceIndex PublicSteepRelativeGrossIndex);

internal sealed record CanonicalAbstractRevenueReconstruction(
    CanonicalAbstractRevenueSourceMarketRow[] SourceMarkets,
    CanonicalAbstractInstitutionalRevenueRow[] Institutions,
    CanonicalAbstractRevenueCommodityReconciliation[] Commodities);

/// <summary>
/// VS98: read-only origin and institutional attribution preflight anchored in
/// VS95 (real service rights), VS96 (same-market price/quantity), VS97 (same
/// snapshot region concentration) and explicit source institutional policy.
/// Abstract money can ultimately ENTER state accounts WITHOUT leaving buyer
/// accounts. This preflight does NOT invent a money conversion, taxes, title
/// deeds, actual sales, transfers or any STATE TREASURY CREDIT.
/// </summary>
public sealed class CanonicalAbstractInstitutionalRevenuePreflight
{
    private const int PermilleScale = 1000;

    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalPrimitivePhysicalDispositionTrial _disposition95;
    private readonly CanonicalPrimitiveSimultaneousAbsorptionTrial _absorption96;
    private readonly CanonicalRegionalEconomicConcentrationAudit _regional97;
    private readonly CanonicalAbstractRevenueEntitlement[] _policies;
    private readonly CanonicalAbstractRevenueSourceMarketRow[] _sourceMarkets;
    private readonly CanonicalAbstractInstitutionalRevenueRow[] _institutions;
    private readonly CanonicalAbstractRevenueCommodityReconciliation[] _commodities;

    public IReadOnlyList<CanonicalAbstractRevenueEntitlement> Entitlements { get; }
    public IReadOnlyList<CanonicalAbstractRevenueSourceMarketRow> SourceMarkets { get; }
    public IReadOnlyList<CanonicalAbstractInstitutionalRevenueRow> Institutions { get; }
    public IReadOnlyList<CanonicalAbstractRevenueCommodityReconciliation> Commodities { get; }
    public ulong CompletedEconomicCycleMovementTurn =>
        _absorption96.CompletedEconomicCycleMovementTurn;

    public bool OriginatesRevenueFromPhysicalOfferAndDemandFunctions => true;
    public bool UsesBuyerCashBalances => false;
    public bool HasBuyerBudgetOrDebit => false;
    public bool IssuesLoansOrCreatesCredit => false;
    public bool RequiresAnExistingPopulationMoneySupply => false;
    public bool MakesAnyNumericalMonetaryConversion => false;
    public bool UsesExplicitInstitutionalBeneficiaries => true;
    public bool AuthenticatesLegalOwnershipOrTaxRights => false;
    public bool PostsSpendableStateTreasuryCredits => false;
    public bool ChangesLegacyFoodCashAccounts => false;
    public bool CreatesPhysicalStockOrDelivery => false;
    public bool MutatesCanonicalWorldOrSave => false;
    public bool CanAuthorizeSettlement => false;

    private CanonicalAbstractInstitutionalRevenuePreflight(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitivePhysicalDispositionTrial disposition95,
        CanonicalPrimitiveSimultaneousAbsorptionTrial absorption96,
        CanonicalRegionalEconomicConcentrationAudit regional97,
        CanonicalAbstractRevenueEntitlement[] policies,
        CanonicalAbstractRevenueReconstruction result)
    {
        _session = session;
        _sites = sites;
        _disposition95 = disposition95;
        _absorption96 = absorption96;
        _regional97 = regional97;
        _policies = (CanonicalAbstractRevenueEntitlement[])policies.Clone();
        _sourceMarkets = result.SourceMarkets;
        _institutions = result.Institutions;
        _commodities = result.Commodities;
        Entitlements = Array.AsReadOnly(_policies);
        SourceMarkets = Array.AsReadOnly(_sourceMarkets);
        Institutions = Array.AsReadOnly(_institutions);
        Commodities = Array.AsReadOnly(_commodities);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "VS98 institutional revenue preflight belongs to another session.");
        _sites.RequireCurrent(session);
        _disposition95.RequireCurrent(session);
        _disposition95.RequireSameSites(_sites);
        _absorption96.RequireSameDisposition(_disposition95);
        _regional97.RequireSameAbsorption(_absorption96);
        _regional97.RequireCurrent(session);
    }

    private static CanonicalAbstractRevenueReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitivePhysicalDispositionTrial disposition95,
        CanonicalPrimitiveSimultaneousAbsorptionTrial absorption96,
        CanonicalRegionalEconomicConcentrationAudit regional97,
        CanonicalAbstractRevenueEntitlement[] policies)
    {
        sites.RequireCurrent(session);
        disposition95.RequireCurrent(session);
        disposition95.RequireSameSites(sites);
        absorption96.RequireSameDisposition(disposition95);
        regional97.RequireSameAbsorption(absorption96);
        absorption96.VerifyIndependent();
        regional97.VerifyIndependent();

        var markets = absorption96.Markets.ToDictionary(
            x => (x.Market, x.Commodity));
        var sources = disposition95.Sources.ToDictionary(
            x => (x.Source, x.Commodity, x.Activity));
        var contributions = new Dictionary<
            (EconomicPointId Source, EconomicPointId Market,
             CommodityId Commodity, EconomicActivityKind Activity), BigInteger>();

        void Add(EconomicPointId source, EconomicPointId market,
            CommodityId commodity, EconomicActivityKind activity, BigInteger qty)
        {
            if (qty < 0 ||
                !sources.ContainsKey((source, commodity, activity)) ||
                !markets.ContainsKey((market, commodity)))
                throw new InvalidOperationException(
                    "VS98 needs authenticated source, commodity and receiving market.");
            if (qty.IsZero) return;
            var key = (source, market, commodity, activity);
            contributions.TryGetValue(key, out var already);
            contributions[key] = already + qty;
        }

        foreach (var s in disposition95.Sources)
            Add(s.Source, s.Source, s.Commodity, s.Activity,
                s.ProposedLocalUseRaw);
        foreach (var lot in disposition95.ProposedRemoteLots)
            Add(lot.Offer.Source, lot.Offer.Buyer, lot.Offer.Commodity,
                lot.Offer.Activity, lot.RawQuantity);

        var totalBySource = new Dictionary<
            (EconomicPointId Source, CommodityId Commodity,
             EconomicActivityKind Activity), BigInteger>();
        var byMarket = new Dictionary<
            (EconomicPointId Market, CommodityId Commodity), BigInteger>();
        foreach (var (key, q) in contributions)
        {
            var origin = (key.Source, key.Commodity, key.Activity);
            totalBySource.TryGetValue(origin, out var x);
            totalBySource[origin] = x + q;
            var dest = (key.Market, key.Commodity);
            byMarket.TryGetValue(dest, out var y);
            byMarket[dest] = y + q;
        }

        foreach (var s in disposition95.Sources)
        {
            totalBySource.TryGetValue((s.Source, s.Commodity, s.Activity),
                out var committed);
            if (committed != s.ProposedLocalUseRaw +
                    s.ProposedRemoteDeliveryRaw)
                throw new InvalidOperationException(
                    "VS98 cannot omit or duplicate a source's certified physical offer.");
        }
        foreach (var m in absorption96.Markets)
        {
            byMarket.TryGetValue((m.Market, m.Commodity), out var q);
            if (q != m.TotalHypotheticallyAbsorbedRaw)
                throw new InvalidOperationException(
                    "VS98 cannot invent buyer demand, supply or destination.");
        }

        var policyGroups = policies.GroupBy(x =>
                (x.Source, x.Commodity, x.Activity))
            .ToDictionary(x => x.Key, x => x.ToArray());
        if (policyGroups.Count != totalBySource.Count ||
            totalBySource.Keys.Any(x => !policyGroups.ContainsKey(x)))
            throw new ArgumentException(
                "VS98 requires exact explicit institutional coverage for every active source and commodity.",
                nameof(policies));

        var kinds = new Dictionary<string,
            ExperimentalTransportInstitutionKind>(StringComparer.Ordinal);
        foreach (var (key, shares) in policyGroups)
        {
            if (!sources.ContainsKey(key) ||
                shares.Sum(x => x.SharePermille) != PermilleScale ||
                shares.Select(x => x.Beneficiary.InstitutionId)
                    .Distinct(StringComparer.Ordinal).Count() != shares.Length)
                throw new ArgumentException(
                    "One active source must allocate exactly 1000 permille to distinct institutions.",
                    nameof(policies));
            foreach (var p in shares)
            {
                if (kinds.TryGetValue(p.Beneficiary.InstitutionId, out var old) &&
                    old != p.Beneficiary.Kind)
                    throw new ArgumentException(
                        "The same institution ID cannot change legal kind.",
                        nameof(policies));
                kinds[p.Beneficiary.InstitutionId] = p.Beneficiary.Kind;
            }
        }

        var sourceRows = new List<CanonicalAbstractRevenueSourceMarketRow>();
        var byInstitution = new Dictionary<
            (string InstitutionId, CommodityId Commodity),
            (ExperimentalTransportInstitution Institution,
             CanonicalEndogenousPriceIndex Gentle,
             CanonicalEndogenousPriceIndex Steep)>();
        foreach (var (key, qty) in contributions
            .OrderBy(x => x.Key.Commodity.Value)
            .ThenBy(x => x.Key.Market.Value)
            .ThenBy(x => x.Key.Source.Value)
            .ThenBy(x => x.Key.Activity))
        {
            var m = markets[(key.Market, key.Commodity)];
            if (qty > m.TotalHypotheticallyAbsorbedRaw ||
                m.TotalHypotheticallyAbsorbedRaw <= 0)
                throw new InvalidOperationException(
                    "VS98 market fractional revenue requires actual positive physical contribution.");
            var gentle = m.GentleGrossRevenueIndex.Times(qty)
                .DividedBy(m.TotalHypotheticallyAbsorbedRaw);
            var steep = m.SteepGrossRevenueIndex.Times(qty)
                .DividedBy(m.TotalHypotheticallyAbsorbedRaw);
            sourceRows.Add(new(key.Source, key.Market,
                key.Commodity, key.Activity, qty, gentle, steep));

            foreach (var p in policyGroups[(
                key.Source, key.Commodity, key.Activity)])
            {
                var instKey = (p.Beneficiary.InstitutionId, key.Commodity);
                byInstitution.TryGetValue(instKey, out var previous);
                var g = gentle.Times(p.SharePermille).DividedBy(PermilleScale);
                var s = steep.Times(p.SharePermille).DividedBy(PermilleScale);
                byInstitution[instKey] = (
                    p.Beneficiary,
                    CanonicalEndogenousPriceIndex.Add(
                        previous.Institution is null
                            ? CanonicalEndogenousPriceIndex.Zero : previous.Gentle, g),
                    CanonicalEndogenousPriceIndex.Add(
                        previous.Institution is null
                            ? CanonicalEndogenousPriceIndex.Zero : previous.Steep, s));
            }
        }

        var institutions = byInstitution.OrderBy(x => x.Key.Commodity.Value)
            .ThenBy(x => x.Key.InstitutionId, StringComparer.Ordinal)
            .Select(x => new CanonicalAbstractInstitutionalRevenueRow(
                x.Value.Institution, x.Key.Commodity,
                x.Value.Gentle, x.Value.Steep)).ToArray();

        var totals = new List<CanonicalAbstractRevenueCommodityReconciliation>();
        foreach (var c in absorption96.Commodities.OrderBy(x => x.Commodity.Value))
        {
            var sourceSum = sourceRows.Where(x => x.Commodity == c.Commodity);
            var institutionSum = institutions.Where(x =>
                x.Commodity == c.Commodity).ToArray();
            var quantity = sourceSum.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.HypotheticalPhysicalRaw);
            var sourceGentle = sourceSum.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.GentleRelativeGrossIndex));
            var sourceSteep = sourceSum.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.SteepRelativeGrossIndex));
            var institutionGentle = institutionSum.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.GentleRelativeGrossIndex));
            var institutionSteep = institutionSum.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.SteepRelativeGrossIndex));
            if (quantity != c.LocallyAbsorbedRaw + c.RemotelyAbsorbedRaw ||
                sourceGentle.CompareTo(c.GentleGrossRevenueIndex) != 0 ||
                sourceSteep.CompareTo(c.SteepGrossRevenueIndex) != 0 ||
                institutionGentle.CompareTo(sourceGentle) != 0 ||
                institutionSteep.CompareTo(sourceSteep) != 0)
                throw new InvalidOperationException(
                    "VS98 source and beneficiary attribution must conserve each commodity's physical offers and exact relative revenue.");
            var publicRows = institutionSum.Where(x =>
                x.Beneficiary.Kind ==
                    ExperimentalTransportInstitutionKind.PublicAuthority);
            var publicGentle = publicRows.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.GentleRelativeGrossIndex));
            var publicSteep = publicRows.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.SteepRelativeGrossIndex));
            totals.Add(new(c.Commodity, c.Activity, quantity,
                sourceGentle, sourceSteep, publicGentle, publicSteep));
        }
        sites.RequireCurrent(session);
        disposition95.RequireCurrent(session);
        absorption96.RequireCurrent(session);
        regional97.RequireCurrent(session);
        return new(sourceRows.ToArray(), institutions, totals.ToArray());
    }

    private static bool Same(CanonicalEndogenousPriceIndex a,
        CanonicalEndogenousPriceIndex b) => a.CompareTo(b) == 0;

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var other = Reconstruct(_session, _sites, _disposition95,
            _absorption96, _regional97, _policies);
        if (_sourceMarkets.Length != other.SourceMarkets.Length ||
            _institutions.Length != other.Institutions.Length ||
            _commodities.Length != other.Commodities.Length ||
            _sourceMarkets.Where((x, i) =>
                x.Source != other.SourceMarkets[i].Source ||
                x.Market != other.SourceMarkets[i].Market ||
                x.Commodity != other.SourceMarkets[i].Commodity ||
                x.Activity != other.SourceMarkets[i].Activity ||
                x.HypotheticalPhysicalRaw !=
                    other.SourceMarkets[i].HypotheticalPhysicalRaw ||
                !Same(x.GentleRelativeGrossIndex,
                    other.SourceMarkets[i].GentleRelativeGrossIndex) ||
                !Same(x.SteepRelativeGrossIndex,
                    other.SourceMarkets[i].SteepRelativeGrossIndex)).Any() ||
            _institutions.Where((x, i) =>
                x.Beneficiary != other.Institutions[i].Beneficiary ||
                x.Commodity != other.Institutions[i].Commodity ||
                !Same(x.GentleRelativeGrossIndex,
                    other.Institutions[i].GentleRelativeGrossIndex) ||
                !Same(x.SteepRelativeGrossIndex,
                    other.Institutions[i].SteepRelativeGrossIndex)).Any() ||
            _commodities.Where((x, i) =>
                x.Commodity != other.Commodities[i].Commodity ||
                x.Activity != other.Commodities[i].Activity ||
                x.HypotheticallyAbsorbedPhysicalRaw !=
                    other.Commodities[i].HypotheticallyAbsorbedPhysicalRaw ||
                !Same(x.TotalGentleRelativeGrossIndex,
                    other.Commodities[i].TotalGentleRelativeGrossIndex) ||
                !Same(x.TotalSteepRelativeGrossIndex,
                    other.Commodities[i].TotalSteepRelativeGrossIndex) ||
                !Same(x.PublicGentleRelativeGrossIndex,
                    other.Commodities[i].PublicGentleRelativeGrossIndex) ||
                !Same(x.PublicSteepRelativeGrossIndex,
                    other.Commodities[i].PublicSteepRelativeGrossIndex)).Any())
            throw new InvalidOperationException(
                "VS98 institutional source/revenue replay differs from authenticated physical markets.");
        RequireCurrent(_session);
    }

    public static CanonicalAbstractInstitutionalRevenuePreflight FromCertifiedMarkets(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitivePhysicalDispositionTrial physical95,
        CanonicalPrimitiveSimultaneousAbsorptionTrial absorption96,
        CanonicalRegionalEconomicConcentrationAudit regional97,
        IEnumerable<CanonicalAbstractRevenueEntitlement> proposedEntitlements)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(physical95);
        ArgumentNullException.ThrowIfNull(absorption96);
        ArgumentNullException.ThrowIfNull(regional97);
        ArgumentNullException.ThrowIfNull(proposedEntitlements);

        var policies = proposedEntitlements.ToArray();
        if (policies.Any(x =>
                x is null ||
                !x.Source.IsValid ||
                !x.Commodity.IsValid ||
                x.Beneficiary is null ||
                string.IsNullOrWhiteSpace(x.Beneficiary.InstitutionId) ||
                !Enum.IsDefined(x.Beneficiary.Kind) ||
                x.Beneficiary.Kind ==
                    ExperimentalTransportInstitutionKind.Unspecified ||
                x.SharePermille is < 1 or > PermilleScale))
            throw new ArgumentException(
                "VS98 requires positive institutional ownership hypotheses with valid identified beneficiaries.",
                nameof(proposedEntitlements));

        var sorted = policies.OrderBy(x => x.Source.Value)
            .ThenBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity)
            .ThenBy(x => x.Beneficiary.InstitutionId, StringComparer.Ordinal)
            .ToArray();
        var reconstruction = Reconstruct(session, sites, physical95,
            absorption96, regional97, sorted);
        var output = new CanonicalAbstractInstitutionalRevenuePreflight(
            session, sites, physical95, absorption96, regional97,
            sorted, reconstruction);
        output.VerifyIndependent();
        return output;
    }
}
