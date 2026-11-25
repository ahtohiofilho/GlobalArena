using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Money shares the EXACT rational domain of the existing precommit market
/// reconciler: 1 monetary unit = 1e16 scaled units. Capital quotes use
/// millionths of a monetary unit, hence factor 1e10. No route-level rounding.
/// </summary>
public static class GlobalArenaMoneyScale
{
    public const ulong MoneyMicroPerUnit = 1_000_000UL;
    public static readonly BigInteger ExactScaledPerUnit =
        new BigInteger(1_000_000UL) * 1_000_000UL * 10_000UL;
    public static readonly BigInteger ExactScaledPerMoneyMicro =
        ExactScaledPerUnit / MoneyMicroPerUnit;

    public static BigInteger FromMoneyMicro(ulong micro) =>
        new BigInteger(micro) * ExactScaledPerMoneyMicro;
}

/// <summary>
/// A civilization's authoritative monetary cash and investment outflow.
/// No material stores, resource inventory or buyer purse exists.
/// </summary>
public sealed record CivilizationCashAccount(
    CivilizationId Civilization, BigInteger CashScaled,
    BigInteger TotalInfrastructureInvestmentScaled)
{
    /// <summary>Verified cumulative economic income, not opening grants.</summary>
    public BigInteger TotalSettledEconomicIncomeScaled { get; init; }
}

/// <summary>
/// Persisted, trusted freight quotes derived ONLY from a settled economic
/// cycle's independently audited edge loads. They are physical costs,
/// not market prices, stock, or invented roads.
/// </summary>
public sealed record CivilizationSettledFreightQuote(
    PhysicalTacticalEdgeId Edge,
    ulong FreePhysicalCostMicroPerWholeCargoUnit,
    ulong ReferenceCargoMicro,
    ulong QuotedCongestionSurchargeMicroPerWholeCargoUnit);

/// <summary>
/// Opt-in complete treasury, indexed by canonical civilization identities.
/// Zero is a real zero; uninitialized means no authorized financial state.
/// Opening balances must be explicitly supplied at the initial-world
/// materialization boundary, and are never inferred from market proposals.
/// Subsequent cash CREDIT must be wired to a verified closed sales journal:
/// this class deliberately has no free "mint money" method.
/// </summary>
public sealed record CivilizationObservedMarketPrice(
    PhysicalTacticalTileId Node, CommodityId Commodity, ulong PriceMicro, ulong ObservedCycleOrdinal);

public sealed class CivilizationTreasuryRuntimeState
{
    public static CivilizationTreasuryRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<CivilizationCashAccount>());

    public bool IsInitialized { get; }
    public IReadOnlyList<CivilizationCashAccount> Accounts { get; }

    public ulong LastSettledEconomicCycleOrdinal { get; }
    public string? LastSettledCargoUnitCode { get; }
    public IReadOnlyList<CivilizationSettledFreightQuote>
        LastSettledFreightQuotes { get; }

    // Null preserves the historical state format; an empty non-null ledger
    // means the opt-in market has closed without a sale observation.
    internal IReadOnlyList<CivilizationObservedMarketPrice>? LocalPriceHistory { get; }

    public bool HasClosedEconomicCycle =>
        LastSettledEconomicCycleOrdinal > 0UL;

    private CivilizationTreasuryRuntimeState(
        bool initialized, CivilizationCashAccount[] accounts)
    {
        IsInitialized = initialized;
        Accounts = Array.AsReadOnly(accounts);
        LastSettledEconomicCycleOrdinal = 0UL;
        LastSettledCargoUnitCode = null;
        LastSettledFreightQuotes = Array.Empty<CivilizationSettledFreightQuote>();
    }

    private CivilizationTreasuryRuntimeState(
        CivilizationTreasuryRuntimeState source,
        CivilizationCashAccount[] accounts, ulong ordinal,
        string cargoUnitCode, CivilizationSettledFreightQuote[] quotes,
        IReadOnlyList<CivilizationObservedMarketPrice>? localPrices = null)
    {
        IsInitialized = source.IsInitialized;
        Accounts = Array.AsReadOnly(accounts);
        LastSettledEconomicCycleOrdinal = ordinal;
        LastSettledCargoUnitCode = cargoUnitCode;
        LastSettledFreightQuotes = Array.AsReadOnly(quotes);
        LocalPriceHistory = localPrices ?? source.LocalPriceHistory;
    }

    public static CivilizationTreasuryRuntimeState CreateOpening(
        IEnumerable<(CivilizationId Civilization, ulong OpeningMoneyMicro)>
            balances)
    {
        ArgumentNullException.ThrowIfNull(balances);
        var records = balances.Select(x => new CivilizationCashAccount(
            x.Civilization,
            GlobalArenaMoneyScale.FromMoneyMicro(x.OpeningMoneyMicro),
            BigInteger.Zero))
            .OrderBy(x => x.Civilization.Value)
            .ToArray();
        if (records.Length == 0 ||
            records.Any(r => !r.Civilization.IsValid) ||
            records.Select(r => r.Civilization).Distinct().Count() !=
                records.Length)
            throw new ArgumentException(
                "An opening treasury must explicitly name each real " +
                "civilization exactly once.", nameof(balances));
        return new CivilizationTreasuryRuntimeState(true, records);
    }

    public CivilizationCashAccount Get(CivilizationId civilization) =>
        Accounts.FirstOrDefault(x => x.Civilization == civilization) ??
        throw new InvalidOperationException(
            "This civilization has no authorized treasury account.");

    public CivilizationTreasuryRuntimeState DebitConstruction(
        CivilizationId civilization, ulong moneyMicro)
    {
        if (!IsInitialized || !civilization.IsValid || moneyMicro == 0UL)
            throw new InvalidOperationException(
                "An accepted investment must debit real positive cash " +
                "from an initialized civilization treasury.");
        var amount = GlobalArenaMoneyScale.FromMoneyMicro(moneyMicro);
        var account = Get(civilization);
        if (account.CashScaled < amount)
            throw new InvalidOperationException(
                "Insufficient treasury cash: no virtual financing, " +
                "buyer wallets or negative construction balances.");
        var next = Accounts.Select(row => row.Civilization == civilization
            ? row with
            {
                CashScaled = row.CashScaled - amount,
                TotalInfrastructureInvestmentScaled =
                    row.TotalInfrastructureInvestmentScaled + amount
            }
            : row).ToArray();
        // Investment cannot erase the provenance of previous closed
        // economic cycles or their next-cycle congestion quotes.
        return HasClosedEconomicCycle
            ? new CivilizationTreasuryRuntimeState(
                this, next, LastSettledEconomicCycleOrdinal,
                LastSettledCargoUnitCode!,
                LastSettledFreightQuotes.ToArray())
            : new CivilizationTreasuryRuntimeState(true, next);
    }

    /// <summary>
    /// Internal capability, available only to the simulation's validated
    /// system-event executor. There is deliberately NO public money-credit
    /// API for arbitrary market previews or client-provided revenues.
    /// </summary>
    internal CivilizationTreasuryRuntimeState CloseVerifiedEconomicCycle(
        ulong economicCycleOrdinal,
        string cargoUnitCode,
        IReadOnlyDictionary<CivilizationId, BigInteger> creditsScaled,
        IEnumerable<CivilizationSettledFreightQuote> nextFreightQuotes,
        IEnumerable<CivilizationObservedMarketPrice>? observedPrices = null)
    {
        ArgumentNullException.ThrowIfNull(creditsScaled);
        ArgumentNullException.ThrowIfNull(nextFreightQuotes);
        if (!IsInitialized || economicCycleOrdinal == 0UL ||
            economicCycleOrdinal != checked(LastSettledEconomicCycleOrdinal + 1UL) ||
            string.IsNullOrWhiteSpace(cargoUnitCode) ||
            creditsScaled.Count != Accounts.Count ||
            Accounts.Any(account =>
                !creditsScaled.TryGetValue(account.Civilization, out var credit) ||
                credit.Sign < 0))
            throw new InvalidOperationException(
                "Economic credit must be nonnegative, complete, funded by " +
                "a single consecutive audited closed market cycle.");

        var quotes = nextFreightQuotes.ToArray();
        if (quotes.Length == 0 ||
            quotes.Any(q => q is null || !q.Edge.IsValid ||
                q.FreePhysicalCostMicroPerWholeCargoUnit == 0UL ||
                q.ReferenceCargoMicro == 0UL) ||
            quotes.Select(q => q.Edge).Distinct().Count() != quotes.Length)
            throw new InvalidOperationException(
                "Settled freight must include exact unique real edge quotes.");
        Array.Sort(quotes, (a, b) =>
            a.Edge.FineStrategicEdgeId.Value.CompareTo(
                b.Edge.FineStrategicEdgeId.Value));
        var next = Accounts.Select(account =>
        {
            BigInteger credit = creditsScaled[account.Civilization];
            return account with
            {
                CashScaled = account.CashScaled + credit,
                TotalSettledEconomicIncomeScaled =
                    account.TotalSettledEconomicIncomeScaled + credit
            };
        }).ToArray();
        if (LocalPriceHistory is not null && observedPrices is null)
            throw new InvalidOperationException("Cannot disable a deployed local price history.");
        IReadOnlyList<CivilizationObservedMarketPrice>? history = null;
        if (observedPrices is not null)
        {
            var updates = observedPrices.ToArray();
            if (updates.Any(p => p is null || !p.Node.IsValid || !p.Commodity.IsValid ||
                    p.ObservedCycleOrdinal != economicCycleOrdinal) ||
                updates.Select(p => (p.Node, p.Commodity)).Distinct().Count() != updates.Length)
                throw new InvalidOperationException("Invalid audited local market observations.");
            var merged = (LocalPriceHistory ?? Array.Empty<CivilizationObservedMarketPrice>())
                .ToDictionary(p => (p.Node, p.Commodity));
            foreach (var p in updates) merged[(p.Node, p.Commodity)] = p;
            history = Array.AsReadOnly(merged.Values.OrderBy(p => p.Node.FineGoldbergParameters.M)
                .ThenBy(p => p.Node.FineGoldbergParameters.N)
                .ThenBy(p => p.Node.FineStrategicCellId.Value).ThenBy(p => p.Commodity.Value).ToArray());
        }
        return new CivilizationTreasuryRuntimeState(
            this, next, economicCycleOrdinal, cargoUnitCode, quotes, history);
    }
}
