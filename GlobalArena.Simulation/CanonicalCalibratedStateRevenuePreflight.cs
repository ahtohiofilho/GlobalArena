using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// An exact rational amount in an EXPLICIT, as-yet-unratified game currency.
/// It is neither a spendable balance nor a settled entry. All addition takes
/// place only after verifying that the quote currency is the same.
/// </summary>
public readonly record struct CanonicalCalibratedMonetaryAmount(
    BigInteger Numerator, BigInteger Denominator) :
    IComparable<CanonicalCalibratedMonetaryAmount>
{
    public static CanonicalCalibratedMonetaryAmount Zero => new(0, 1);

    public static CanonicalCalibratedMonetaryAmount Of(
        BigInteger numerator, BigInteger denominator)
    {
        if (numerator < 0 || denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator),
                "An experimental amount must be nonnegative with a positive denominator.");
        if (numerator.IsZero) return Zero;
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        return new(numerator / gcd, denominator / gcd);
    }

    public int CompareTo(CanonicalCalibratedMonetaryAmount other)
    {
        if (Denominator <= 0 || other.Denominator <= 0)
            throw new InvalidOperationException(
                "A calibrated amount has an invalid denominator.");
        return (Numerator * other.Denominator).CompareTo(
            other.Numerator * Denominator);
    }

    public static CanonicalCalibratedMonetaryAmount Add(
        CanonicalCalibratedMonetaryAmount a,
        CanonicalCalibratedMonetaryAmount b) =>
        Of(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);
}

/// <summary>
/// Explicit external sensitivity and scale for ONE commodity in a common
/// named game-currency unit. There are NO built-in nominal exchange rates.
/// </summary>
public sealed record CanonicalCommodityMonetaryCalibration(
    CommodityId Commodity, BigInteger CurrencyNumeratorPerRelativeIndex,
    BigInteger CurrencyDenominatorPerRelativeIndex);

/// <summary>
/// Caller-supplied LAB policy tied to a real world hash and economic cycle.
/// In particular EvidenceId is a claim to provenance, NOT a proof that the
/// prices/scale are canonically ratified.
/// </summary>
public sealed record CanonicalAbstractCurrencyCalibration(
    string PolicyId, ulong PolicyVersion, string CurrencyUnitId,
    string EvidenceId, string SourceWorldHashHex,
    ulong CompletedEconomicCycleMovementTurn,
    CanonicalEndogenousCurveVariant Curve,
    IReadOnlyList<CanonicalCommodityMonetaryCalibration> CommodityFactors);

/// <summary>
/// Accepts experimental/read-only policy provenance. A host implementation
/// MUST separately ratify any actual settlement. Calling this interface does
/// not grant monetary issuance or treasury posting authority.
/// </summary>
public interface ICanonicalAbstractCurrencyCalibrationReviewer
{
    bool AcceptReadOnlyCalibration(CanonicalAbstractCurrencyCalibration calibration);
}

public sealed record CanonicalCalibratedInstitutionalProposalRow(
    ExperimentalTransportInstitution Beneficiary, CommodityId Commodity,
    CanonicalCalibratedMonetaryAmount ProposedNonpostedAmount);

public sealed record CanonicalCalibratedCommodityProposalRow(
    CommodityId Commodity,
    CanonicalCalibratedMonetaryAmount GrossNonpostedAmount,
    CanonicalCalibratedMonetaryAmount PublicNonpostedAmount);

internal sealed record CanonicalCalibratedTreasuryReconstruction(
    CanonicalCalibratedInstitutionalProposalRow[] Institutions,
    CanonicalCalibratedCommodityProposalRow[] Commodities,
    CanonicalCalibratedMonetaryAmount ProposedPublicNonpostedTotal,
    CanonicalCalibratedMonetaryAmount ProposedAllInstitutionsNonpostedTotal);

/// <summary>
/// VS99: deliberate currency-calibration lab, not the state treasury itself.
/// Gross and public revenue are derived from VS98 institutionally allocated
/// source/market relative indices through an externally reviewed per-commodity
/// conversion in one named currency. No population account is debited.
/// Without explicit, world-bound calibration/reviewer this fails closed.
/// No new spendable coins, state treasury mutation, taxes, property or SETTLE.
/// </summary>
public sealed class CanonicalCalibratedStateRevenuePreflight
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalAbstractInstitutionalRevenuePreflight _abstract98;
    private readonly CanonicalAbstractCurrencyCalibration _calibration;
    private readonly CanonicalCommodityMonetaryCalibration[] _factors;
    private readonly CanonicalCalibratedInstitutionalProposalRow[] _institutions;
    private readonly CanonicalCalibratedCommodityProposalRow[] _commodities;
    private readonly CanonicalCalibratedMonetaryAmount _stateTotal;
    private readonly CanonicalCalibratedMonetaryAmount _grossTotal;

    public string PolicyId => _calibration.PolicyId;
    public ulong PolicyVersion => _calibration.PolicyVersion;
    public string CurrencyUnitId => _calibration.CurrencyUnitId;
    public string EvidenceId => _calibration.EvidenceId;
    public ulong CompletedEconomicCycleMovementTurn =>
        _calibration.CompletedEconomicCycleMovementTurn;
    public CanonicalEndogenousCurveVariant Curve => _calibration.Curve;
    public IReadOnlyList<CanonicalCommodityMonetaryCalibration> CommodityFactors { get; }
    public IReadOnlyList<CanonicalCalibratedInstitutionalProposalRow> Institutions { get; }
    public IReadOnlyList<CanonicalCalibratedCommodityProposalRow> Commodities { get; }
    public CanonicalCalibratedMonetaryAmount ProposedPublicNonpostedTotal =>
        _stateTotal;
    public CanonicalCalibratedMonetaryAmount ProposedAllInstitutionsNonpostedTotal =>
        _grossTotal;

    public bool HasExplicitCurrencyUnitAndFactors => true;
    public bool UsesReviewedReadOnlyPolicy => true;
    public bool IncomeOriginatesFromEconomicFunctions => true;
    public bool RequiresPopulationOrBuyerCash => false;
    public bool DebitsPopulationOrBuyerCash => false;
    public bool RequiresInitialMoneySupply => false;
    public bool IsActualTaxationOrExecutedCommerce => false;
    public bool IsScaleRatifiedForRealSettlement => false;
    public bool IssuesSpendableCurrency => false;
    public bool PostsToStateTreasury => false;
    public bool ModifiesLegacyFoodCashLedger => false;
    public bool CreatesInventoryOrCredit => false;
    public bool CanAuthorizeRealSettlement => false;
    public bool MutatesWorldOrSave => false;

    private CanonicalCalibratedStateRevenuePreflight(
        CanonicalPhysicalTacticalSession session,
        CanonicalAbstractInstitutionalRevenuePreflight abstract98,
        CanonicalAbstractCurrencyCalibration calibration,
        CanonicalCommodityMonetaryCalibration[] factors,
        CanonicalCalibratedTreasuryReconstruction result)
    {
        _session = session;
        _abstract98 = abstract98;
        _calibration = calibration with {
            CommodityFactors = Array.AsReadOnly(factors)
        };
        _factors = (CanonicalCommodityMonetaryCalibration[])factors.Clone();
        CommodityFactors = Array.AsReadOnly(_factors);
        _institutions = result.Institutions;
        _commodities = result.Commodities;
        _stateTotal = result.ProposedPublicNonpostedTotal;
        _grossTotal = result.ProposedAllInstitutionsNonpostedTotal;
        Institutions = Array.AsReadOnly(_institutions);
        Commodities = Array.AsReadOnly(_commodities);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "VS99 money proposal belongs to a different physical session.");
        _abstract98.RequireCurrent(session);
        if (session.CurrentHash.HexDigest != _calibration.SourceWorldHashHex ||
            _abstract98.CompletedEconomicCycleMovementTurn !=
                _calibration.CompletedEconomicCycleMovementTurn)
            throw new InvalidOperationException(
                "VS99 calibration is stale for this world or economic cycle.");
    }

    private static CanonicalCalibratedMonetaryAmount ConvertIndex(
        CanonicalEndogenousPriceIndex index,
        CanonicalCommodityMonetaryCalibration factor)
    {
        if (index.Denominator <= 0 || index.Numerator < 0 ||
            factor.CurrencyNumeratorPerRelativeIndex <= 0 ||
            factor.CurrencyDenominatorPerRelativeIndex <= 0)
            throw new InvalidOperationException(
                "VS99 conversion requires valid nonnegative relative revenue and an explicit positive nominal factor.");
        return CanonicalCalibratedMonetaryAmount.Of(
            index.Numerator * factor.CurrencyNumeratorPerRelativeIndex,
            index.Denominator * factor.CurrencyDenominatorPerRelativeIndex);
    }

    private static CanonicalCalibratedTreasuryReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalAbstractInstitutionalRevenuePreflight abstract98,
        CanonicalAbstractCurrencyCalibration policy,
        CanonicalCommodityMonetaryCalibration[] factors)
    {
        abstract98.RequireCurrent(session);
        abstract98.VerifyIndependent();

        if (string.IsNullOrWhiteSpace(policy.PolicyId) ||
            string.IsNullOrWhiteSpace(policy.CurrencyUnitId) ||
            string.IsNullOrWhiteSpace(policy.EvidenceId) ||
            policy.PolicyId.Trim() != policy.PolicyId ||
            policy.CurrencyUnitId.Trim() != policy.CurrencyUnitId ||
            policy.EvidenceId.Trim() != policy.EvidenceId ||
            policy.PolicyVersion == 0UL ||
            policy.SourceWorldHashHex != session.CurrentHash.HexDigest ||
            policy.CompletedEconomicCycleMovementTurn !=
                abstract98.CompletedEconomicCycleMovementTurn ||
            policy.Curve is not (
                CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand or
                CanonicalEndogenousCurveVariant.SteepDyadicSupplyDemand))
            throw new ArgumentException(
                "VS99 needs identified, versioned, cycle-bound and explicitly chosen currency calibration.",
                nameof(policy));

        var expected = abstract98.Commodities
            .Select(x => x.Commodity).ToHashSet();
        var rates = new Dictionary<CommodityId,
            CanonicalCommodityMonetaryCalibration>();
        if (factors.Length != expected.Count || expected.Count != 3)
            throw new ArgumentException(
                "VS99 must explicitly calibrate all three primitive commodities.",
                nameof(factors));
        foreach (var factor in factors)
        {
            if (factor is null || !factor.Commodity.IsValid ||
                factor.CurrencyNumeratorPerRelativeIndex <= 0 ||
                factor.CurrencyDenominatorPerRelativeIndex <= 0 ||
                !expected.Contains(factor.Commodity) ||
                !rates.TryAdd(factor.Commodity, factor))
                throw new ArgumentException(
                    "Missing, duplicated or nonpositive per-commodity monetary calibration.",
                    nameof(factors));
        }

        CanonicalEndogenousPriceIndex SelectIndex(
            CanonicalEndogenousPriceIndex gentle,
            CanonicalEndogenousPriceIndex steep) =>
            policy.Curve == CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand
                ? gentle : steep;

        var institutionRows = abstract98.Institutions.Select(row =>
            new CanonicalCalibratedInstitutionalProposalRow(
                row.Beneficiary, row.Commodity,
                ConvertIndex(SelectIndex(row.GentleRelativeGrossIndex,
                    row.SteepRelativeGrossIndex), rates[row.Commodity])))
            .OrderBy(x => x.Commodity.Value)
            .ThenBy(x => x.Beneficiary.InstitutionId, StringComparer.Ordinal)
            .ToArray();
        var totalByCommodity = abstract98.Commodities.Select(row =>
        {
            var rate = rates[row.Commodity];
            return new CanonicalCalibratedCommodityProposalRow(
                row.Commodity,
                ConvertIndex(SelectIndex(
                    row.TotalGentleRelativeGrossIndex,
                    row.TotalSteepRelativeGrossIndex), rate),
                ConvertIndex(SelectIndex(
                    row.PublicGentleRelativeGrossIndex,
                    row.PublicSteepRelativeGrossIndex), rate));
        }).OrderBy(x => x.Commodity.Value).ToArray();

        var state = CanonicalCalibratedMonetaryAmount.Zero;
        var gross = CanonicalCalibratedMonetaryAmount.Zero;
        foreach (var row in totalByCommodity)
        {
            var sameProduct = institutionRows.Where(x =>
                x.Commodity == row.Commodity).ToArray();
            var sum = sameProduct.Aggregate(
                CanonicalCalibratedMonetaryAmount.Zero,
                (current, x) => CanonicalCalibratedMonetaryAmount.Add(
                    current, x.ProposedNonpostedAmount));
            var publicSum = sameProduct.Where(x =>
                x.Beneficiary.Kind ==
                    ExperimentalTransportInstitutionKind.PublicAuthority)
                .Aggregate(CanonicalCalibratedMonetaryAmount.Zero,
                    (current, x) => CanonicalCalibratedMonetaryAmount.Add(
                        current, x.ProposedNonpostedAmount));
            if (sum.CompareTo(row.GrossNonpostedAmount) != 0 ||
                publicSum.CompareTo(row.PublicNonpostedAmount) != 0 ||
                publicSum.CompareTo(sum) > 0)
                throw new InvalidOperationException(
                    "VS99 institutional values must conserve each commodity's source-generated, calibrated gross and public proposals.");
            state = CanonicalCalibratedMonetaryAmount.Add(
                state, publicSum);
            gross = CanonicalCalibratedMonetaryAmount.Add(
                gross, sum);
        }

        abstract98.RequireCurrent(session);
        return new(institutionRows, totalByCommodity, state, gross);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var result = Reconstruct(_session, _abstract98, _calibration, _factors);
        if (_institutions.Length != result.Institutions.Length ||
            _commodities.Length != result.Commodities.Length ||
            !_institutions.SequenceEqual(result.Institutions) ||
            !_commodities.SequenceEqual(result.Commodities) ||
            _stateTotal != result.ProposedPublicNonpostedTotal ||
            _grossTotal != result.ProposedAllInstitutionsNonpostedTotal)
            throw new InvalidOperationException(
                "VS99 independently rebuilt institutional currency proposals diverged.");
        RequireCurrent(_session);
    }

    public static CanonicalCalibratedStateRevenuePreflight FromReviewedCalibration(
        CanonicalPhysicalTacticalSession session,
        CanonicalAbstractInstitutionalRevenuePreflight abstract98,
        CanonicalAbstractCurrencyCalibration calibration,
        ICanonicalAbstractCurrencyCalibrationReviewer reviewer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(abstract98);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(reviewer);
        ArgumentNullException.ThrowIfNull(calibration.CommodityFactors);
        var factors = calibration.CommodityFactors.ToArray();
        var result = Reconstruct(session, abstract98, calibration, factors);
        var frozen = calibration with {
            CommodityFactors = Array.AsReadOnly(factors)
        };
        if (!reviewer.AcceptReadOnlyCalibration(frozen))
            throw new InvalidOperationException(
                "VS99 external experimental calibration reviewer rejected the currency policy; no unreviewed default conversion exists.");
        var output = new CanonicalCalibratedStateRevenuePreflight(
            session, abstract98, frozen, factors, result);
        output.VerifyIndependent();
        return output;
    }
}
