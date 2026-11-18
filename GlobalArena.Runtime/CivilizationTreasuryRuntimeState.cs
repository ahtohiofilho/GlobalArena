using System.Numerics;

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
    BigInteger TotalInfrastructureInvestmentScaled);

/// <summary>
/// Opt-in complete treasury, indexed by canonical civilization identities.
/// Zero is a real zero; uninitialized means no authorized financial state.
/// Opening balances must be explicitly supplied at the initial-world
/// materialization boundary, and are never inferred from market proposals.
/// Subsequent cash CREDIT must be wired to a verified closed sales journal:
/// this class deliberately has no free "mint money" method.
/// </summary>
public sealed class CivilizationTreasuryRuntimeState
{
    public static CivilizationTreasuryRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<CivilizationCashAccount>());

    public bool IsInitialized { get; }
    public IReadOnlyList<CivilizationCashAccount> Accounts { get; }

    private CivilizationTreasuryRuntimeState(
        bool initialized, CivilizationCashAccount[] accounts)
    {
        IsInitialized = initialized;
        Accounts = Array.AsReadOnly(accounts);
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
        return new CivilizationTreasuryRuntimeState(true, next);
    }
}
