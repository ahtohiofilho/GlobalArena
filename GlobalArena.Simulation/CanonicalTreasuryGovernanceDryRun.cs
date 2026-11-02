using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// EXTERNAL, non-ratifying governance evidence for a READ-ONLY experiment.
/// Same named currency, versioned VS99 policy, live world and closed cycle.
/// This is NOT title, real mint authority or a tax statute.
/// </summary>
public sealed record CanonicalTreasuryGovernanceDeclaration(
    string GovernanceId,
    ulong GovernanceVersion,
    string EvidenceId,
    string CalibratedPolicyId,
    ulong CalibratedPolicyVersion,
    string CurrencyUnitId,
    string WorldHashHex,
    ulong CompletedEconomicCycleMovementTurn);

/// <summary>
/// A reviewer may accept laboratory provenance only. There is NO production
/// authorization interface or permissive built-in implementation.
/// </summary>
public interface ICanonicalTreasuryGovernanceReviewer
{
    bool AcceptReadOnlyGovernance(CanonicalTreasuryGovernanceDeclaration declaration);
}

public sealed record CanonicalTreasuryPublicDryRunEntry(
    ExperimentalTransportInstitution Beneficiary,
    CommodityId Commodity,
    CanonicalCalibratedMonetaryAmount ProposedNonpostedAmount);

/// <summary>
/// An isolated, immutable worksheet for ONE world/cycle/currency. The record
/// does NOT live in WorldState or a state treasury; even when re-applied, it
/// does not mint or credit currency.
/// </summary>
public sealed class CanonicalTreasuryDryRunJournal
{
    private readonly CanonicalTreasuryPublicDryRunEntry[] _entries;

    public string WorldHashHex { get; }
    public ulong CompletedEconomicCycleMovementTurn { get; }
    public string CurrencyUnitId { get; }
    public string ProposalFingerprintHex { get; }
    public IReadOnlyList<CanonicalTreasuryPublicDryRunEntry> Entries { get; }
    public CanonicalCalibratedMonetaryAmount ProposedNonpostedTotal { get; }

    internal CanonicalTreasuryDryRunJournal(
        string worldHashHex, ulong cycleTurn, string currencyUnitId,
        string fingerprintHex, CanonicalTreasuryPublicDryRunEntry[] entries,
        CanonicalCalibratedMonetaryAmount nonpostedTotal)
    {
        WorldHashHex = worldHashHex;
        CompletedEconomicCycleMovementTurn = cycleTurn;
        CurrencyUnitId = currencyUnitId;
        ProposalFingerprintHex = fingerprintHex;
        _entries = (CanonicalTreasuryPublicDryRunEntry[])entries.Clone();
        Entries = Array.AsReadOnly(_entries);
        ProposedNonpostedTotal = nonpostedTotal;
    }

    public bool IsRealWorldStateLedger => false;
    public bool CreatesSpendableTreasuryBalance => false;
}

/// <summary>
/// VS100: preparation of state-revenue governance. It does not transform the
/// experimental VS99 money unit into ratified currency or authorize SETTLE.
/// Instead it offers an exact, canonical proposal fingerprint and an isolated
/// "apply twice = same immutable worksheet" rehearsal, with fail-closed
/// detection of a competing proposal in the same economic cycle.
/// </summary>
public sealed class CanonicalTreasuryGovernanceDryRun
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalCalibratedStateRevenuePreflight _proposal99;
    private readonly CanonicalTreasuryGovernanceDeclaration _declaration;
    private readonly CanonicalTreasuryPublicDryRunEntry[] _entries;
    private readonly CanonicalCalibratedMonetaryAmount _publicTotal;
    private readonly string _fingerprint;

    public CanonicalTreasuryGovernanceDeclaration Declaration => _declaration;
    public string ProposalFingerprintHex => _fingerprint;
    public IReadOnlyList<CanonicalTreasuryPublicDryRunEntry> PublicEntries { get; }
    public CanonicalCalibratedMonetaryAmount ProposedPublicNonpostedTotal =>
        _publicTotal;
    public bool UsesOnlyPreviouslyCertifiedEconomicallyOriginatedProposals => true;
    public bool ExplicitlyReviewsInstitutionalGovernance => true;
    public bool RequiresHouseholdOrBuyerAccount => false;
    public bool DebitsPopulationMoney => false;
    public bool ConvertsUnratifiedRatesIntoRealCurrency => false;
    public bool RatifiesTaxOrOwnershipRights => false;
    public bool PostsRealStateTreasuryRevenue => false;
    public bool CreatesSpendableMoneyOrLoans => false;
    public bool TouchesWorldOrLegacyCashLedger => false;
    public bool CanAuthorizeSettlement => false;

    private sealed record Computed(
        CanonicalTreasuryPublicDryRunEntry[] Entries,
        CanonicalCalibratedMonetaryAmount Total,
        string Fingerprint);

    private CanonicalTreasuryGovernanceDryRun(
        CanonicalPhysicalTacticalSession session,
        CanonicalCalibratedStateRevenuePreflight proposal99,
        CanonicalTreasuryGovernanceDeclaration declaration,
        Computed computed)
    {
        _session = session;
        _proposal99 = proposal99;
        _declaration = declaration;
        _entries = computed.Entries;
        _publicTotal = computed.Total;
        _fingerprint = computed.Fingerprint;
        PublicEntries = Array.AsReadOnly(_entries);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "VS100 governance worksheet belongs to another game session.");
        _proposal99.RequireCurrent(session);
        if (_declaration.WorldHashHex != session.CurrentHash.HexDigest ||
            _declaration.CompletedEconomicCycleMovementTurn !=
                _proposal99.CompletedEconomicCycleMovementTurn)
            throw new InvalidOperationException(
                "VS100 governance is stale for the live world or economic cycle.");
    }

    private static bool IsToken(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Trim() == value && value.Length <= 256;

    private static Computed Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalCalibratedStateRevenuePreflight proposal99,
        CanonicalTreasuryGovernanceDeclaration declaration)
    {
        proposal99.RequireCurrent(session);
        proposal99.VerifyIndependent();
        if (!IsToken(declaration.GovernanceId) ||
            !IsToken(declaration.EvidenceId) ||
            !IsToken(declaration.CalibratedPolicyId) ||
            !IsToken(declaration.CurrencyUnitId) ||
            declaration.GovernanceVersion == 0 ||
            declaration.CalibratedPolicyVersion == 0 ||
            !string.Equals(declaration.CalibratedPolicyId,
                proposal99.PolicyId, StringComparison.Ordinal) ||
            declaration.CalibratedPolicyVersion != proposal99.PolicyVersion ||
            !string.Equals(declaration.CurrencyUnitId,
                proposal99.CurrencyUnitId, StringComparison.Ordinal) ||
            !string.Equals(declaration.WorldHashHex,
                session.CurrentHash.HexDigest, StringComparison.Ordinal) ||
            declaration.CompletedEconomicCycleMovementTurn !=
                proposal99.CompletedEconomicCycleMovementTurn)
            throw new ArgumentException(
                "VS100 requires explicit versioned, real-world, single-currency governance matching VS99.",
                nameof(declaration));

        var entries = proposal99.Institutions.Where(x =>
                x.Beneficiary.Kind ==
                    ExperimentalTransportInstitutionKind.PublicAuthority &&
                x.ProposedNonpostedAmount.Numerator > 0)
            .Select(x => new CanonicalTreasuryPublicDryRunEntry(
                x.Beneficiary, x.Commodity, x.ProposedNonpostedAmount))
            .OrderBy(x => x.Commodity.Value)
            .ThenBy(x => x.Beneficiary.InstitutionId, StringComparer.Ordinal)
            .ToArray();
        if (entries.Select(x =>
                (x.Beneficiary.InstitutionId, x.Commodity))
                .Distinct().Count() != entries.Length)
            throw new InvalidOperationException(
                "VS100 forbids duplicate public beneficiaries for the same product.");

        var total = entries.Aggregate(CanonicalCalibratedMonetaryAmount.Zero,
            (sum, x) => CanonicalCalibratedMonetaryAmount.Add(
                sum, x.ProposedNonpostedAmount));
        if (total.CompareTo(proposal99.ProposedPublicNonpostedTotal) != 0 ||
            total.CompareTo(proposal99.ProposedAllInstitutionsNonpostedTotal) > 0)
            throw new InvalidOperationException(
                "VS100 treasury proposal must reconcile exactly to VS99, without creating income.");

        foreach (var c in proposal99.Commodities)
        {
            var partial = entries.Where(x => x.Commodity == c.Commodity)
                .Aggregate(CanonicalCalibratedMonetaryAmount.Zero,
                    (sum, x) => CanonicalCalibratedMonetaryAmount.Add(
                        sum, x.ProposedNonpostedAmount));
            if (partial.CompareTo(c.PublicNonpostedAmount) != 0)
                throw new InvalidOperationException(
                    "VS100 cannot shift public value between products or institutions.");
        }

        // BinaryWriter prefixes strings with lengths, preventing ambiguous
        // string concatenations or separators in user-supplied IDs from
        // generating the same causal fingerprint.
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("GA-VS100-READONLY-GOVERNANCE-V1");
            writer.Write(declaration.GovernanceId);
            writer.Write(declaration.GovernanceVersion);
            writer.Write(declaration.EvidenceId);
            writer.Write(declaration.CalibratedPolicyId);
            writer.Write(declaration.CalibratedPolicyVersion);
            writer.Write(declaration.CurrencyUnitId);
            writer.Write(declaration.WorldHashHex);
            writer.Write(declaration.CompletedEconomicCycleMovementTurn);
            writer.Write(proposal99.EvidenceId);
            writer.Write((int)proposal99.Curve);
            writer.Write(proposal99.CommodityFactors.Count);
            foreach (var factor in proposal99.CommodityFactors
                .OrderBy(x => x.Commodity.Value))
            {
                writer.Write(factor.Commodity.Value);
                writer.Write(factor.CurrencyNumeratorPerRelativeIndex
                    .ToString(CultureInfo.InvariantCulture));
                writer.Write(factor.CurrencyDenominatorPerRelativeIndex
                    .ToString(CultureInfo.InvariantCulture));
            }
            // Include ALL beneficiary amounts, not just public, so a private
            // reallocation cannot sneak into an otherwise identical receipt.
            writer.Write(proposal99.Institutions.Count);
            foreach (var i in proposal99.Institutions.OrderBy(x =>
                x.Commodity.Value).ThenBy(x =>
                x.Beneficiary.InstitutionId, StringComparer.Ordinal))
            {
                writer.Write(i.Commodity.Value);
                writer.Write(i.Beneficiary.InstitutionId);
                writer.Write((int)i.Beneficiary.Kind);
                writer.Write(i.ProposedNonpostedAmount.Numerator
                    .ToString(CultureInfo.InvariantCulture));
                writer.Write(i.ProposedNonpostedAmount.Denominator
                    .ToString(CultureInfo.InvariantCulture));
            }
            writer.Write(proposal99.Commodities.Count);
            foreach (var c in proposal99.Commodities.OrderBy(x =>
                x.Commodity.Value))
            {
                writer.Write(c.Commodity.Value);
                writer.Write(c.GrossNonpostedAmount.Numerator
                    .ToString(CultureInfo.InvariantCulture));
                writer.Write(c.GrossNonpostedAmount.Denominator
                    .ToString(CultureInfo.InvariantCulture));
                writer.Write(c.PublicNonpostedAmount.Numerator
                    .ToString(CultureInfo.InvariantCulture));
                writer.Write(c.PublicNonpostedAmount.Denominator
                    .ToString(CultureInfo.InvariantCulture));
            }
        }
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(stream.ToArray()));
        proposal99.RequireCurrent(session);
        return new Computed(entries, total, fingerprint);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var replay = Reconstruct(_session, _proposal99, _declaration);
        if (!string.Equals(_fingerprint, replay.Fingerprint,
                StringComparison.Ordinal) ||
            _publicTotal.CompareTo(replay.Total) != 0 ||
            !_entries.SequenceEqual(replay.Entries))
            throw new InvalidOperationException(
                "VS100 independently rebuilt governance worksheet diverged.");
        RequireCurrent(_session);
    }

    /// <summary>
    /// Immutable laboratory simulation only: no state mutation and no
    /// spendable credits. The exact same authenticated plan in the same
    /// cycle is idempotent. A competing plan, changed calibration or
    /// conflicting external evidence FAILS CLOSED (no double issue).
    /// </summary>
    public CanonicalTreasuryDryRunJournal StageInLaboratory(
        CanonicalTreasuryDryRunJournal? existing = null)
    {
        VerifyIndependent();
        if (existing is not null)
        {
            if (existing.WorldHashHex != _declaration.WorldHashHex ||
                existing.CompletedEconomicCycleMovementTurn !=
                    _declaration.CompletedEconomicCycleMovementTurn ||
                existing.CurrencyUnitId != _declaration.CurrencyUnitId ||
                existing.ProposalFingerprintHex != _fingerprint ||
                existing.ProposedNonpostedTotal.CompareTo(_publicTotal) != 0 ||
                !existing.Entries.SequenceEqual(_entries))
                throw new InvalidOperationException(
                    "VS100 refuses competing monetary proposals or governance policies for the same simulated cycle.");
            return existing;
        }
        return new CanonicalTreasuryDryRunJournal(
            _declaration.WorldHashHex,
            _declaration.CompletedEconomicCycleMovementTurn,
            _declaration.CurrencyUnitId, _fingerprint,
            _entries, _publicTotal);
    }

    public static CanonicalTreasuryGovernanceDryRun FromReviewedGovernance(
        CanonicalPhysicalTacticalSession session,
        CanonicalCalibratedStateRevenuePreflight proposal99,
        CanonicalTreasuryGovernanceDeclaration declaration,
        ICanonicalTreasuryGovernanceReviewer reviewer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(proposal99);
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(reviewer);
        var computed = Reconstruct(session, proposal99, declaration);
        if (!reviewer.AcceptReadOnlyGovernance(declaration))
            throw new InvalidOperationException(
                "External VS100 reviewer rejected read-only governance. This is not real mint or tax authority.");
        var result = new CanonicalTreasuryGovernanceDryRun(
            session, proposal99, declaration, computed);
        result.VerifyIndependent();
        return result;
    }
}
