using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>Opening population is retained permanently, independently of
/// subsequent losses. Purchased count can only increase.</summary>
public sealed record FoundationPurchasedDemographyAccount(
    CivilizationId Civilization, ulong InitialWorkers, ulong PurchasedWorkers)
{
    public ulong HistoricalAcquisitions => checked(InitialWorkers + PurchasedWorkers);
    public ulong NextHistoricalIndex => checked(HistoricalAcquisitions + 1UL);
}

public sealed class FoundationPurchasedDemographyRuntimeState
{
    public static FoundationPurchasedDemographyRuntimeState Uninitialized { get; } =
        new(false, 0UL, 0UL, Array.Empty<FoundationPurchasedDemographyAccount>());

    public bool IsInitialized { get; }
    public ulong InitialMoneyMicro { get; }
    public ulong ReferenceAcquisitions { get; }
    public IReadOnlyList<FoundationPurchasedDemographyAccount> Accounts { get; }
    private readonly IReadOnlyDictionary<CivilizationId, FoundationPurchasedDemographyAccount> _accounts;

    private FoundationPurchasedDemographyRuntimeState(bool initialized, ulong initialMoneyMicro,
        ulong referenceAcquisitions, FoundationPurchasedDemographyAccount[] accounts)
    {
        if (initialized && (initialMoneyMicro == 0UL || referenceAcquisitions == 0UL || accounts.Length == 0))
            throw new ArgumentException("Purchased demography requires explicit positive price parameters and a roster.");
        if (accounts.Any(a => !a.Civilization.IsValid) ||
            accounts.Select(a => a.Civilization).Distinct().Count() != accounts.Length)
            throw new ArgumentException("Demographic account roster must be unique and valid.");
        foreach (var account in accounts) _ = account.NextHistoricalIndex;
        IsInitialized = initialized;
        InitialMoneyMicro = initialMoneyMicro;
        ReferenceAcquisitions = referenceAcquisitions;
        Accounts = Array.AsReadOnly(accounts.OrderBy(a => a.Civilization.Value).ToArray());
        _accounts = Accounts.ToDictionary(a => a.Civilization);
    }

    public static FoundationPurchasedDemographyRuntimeState CreateOpening(
        IEnumerable<(CivilizationId Civilization, ulong InitialWorkers)> roster,
        ulong initialMoneyMicro, ulong referenceAcquisitions) =>
        new(true, initialMoneyMicro, referenceAcquisitions,
            (roster ?? throw new ArgumentNullException(nameof(roster)))
            .Select(r => new FoundationPurchasedDemographyAccount(r.Civilization, r.InitialWorkers, 0UL)).ToArray());

    public FoundationPurchasedDemographyAccount Get(CivilizationId civilization) =>
        _accounts.TryGetValue(civilization, out var account) ? account :
            throw new InvalidOperationException("Missing demographic account.");

    // Detached ledger transition only. R236D must attach this together with
    // canonical treasury debit and authorized newborns, never independently.
    public FoundationPurchasedDemographyRuntimeState RecordAcquisitions(CivilizationId civilization, ulong quantity)
    {
        if (quantity == 0UL) throw new ArgumentOutOfRangeException(nameof(quantity));
        var account = Get(civilization);
        var updated = account with { PurchasedWorkers = checked(account.PurchasedWorkers + quantity) };
        return new(true, InitialMoneyMicro, ReferenceAcquisitions,
            Accounts.Select(a => a.Civilization == civilization ? updated : a).ToArray());
    }
}
