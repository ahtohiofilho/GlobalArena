namespace GlobalArena.Runtime;

/// <summary>
/// VS101 dormant reservation of the FUTURE public treasury's persistent
/// position in WorldState. It contains NO money, no institutional ownership,
/// no currency scale, and no issuance or settlement capability. The only
/// accepted state is a world-bound, immutable, empty reservation.
/// </summary>
public sealed record CanonicalTreasuryReservationRuntimeState
{
    public const uint SchemaVersion = 1U;

    public string WorldSignatureSha256Hex { get; }
    public uint Version => SchemaVersion;
    public ulong SpendableBalanceMinorUnits => 0UL;
    public int MonetaryReceiptCount => 0;
    public bool IsDormant => true;
    public bool AcceptsRealIssuance => false;
    public bool DebitsPopulationAccounts => false;

    private CanonicalTreasuryReservationRuntimeState(
        string worldSignatureSha256Hex)
    {
        if (!IsCanonicalDigest(worldSignatureSha256Hex))
            throw new ArgumentException(
                "Treasury reservation requires exact uppercase SHA256 world signature.");
        WorldSignatureSha256Hex = worldSignatureSha256Hex;
    }

    internal static bool IsCanonicalDigest(string? hex) =>
        hex is { Length: 64 } &&
        hex.All(c => (c >= '0' && c <= '9') ||
            (c >= 'A' && c <= 'F'));

    public static CanonicalTreasuryReservationRuntimeState CreateDormant(
        RuntimeWorldBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return new(binding.WorldSignatureSha256Hex);
    }

    public void RequireWorld(RuntimeWorldBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (WorldSignatureSha256Hex != binding.WorldSignatureSha256Hex)
            throw new InvalidOperationException(
                "Treasury reservation cannot be attached to a foreign generated world.");
    }

    /// <summary>
    /// The decoder can restore ONLY the dormant V1 state. Future entries,
    /// balances and currencies cannot be smuggled into a V1 archive.
    /// </summary>
    public static CanonicalTreasuryReservationRuntimeState RestoreDormant(
        RuntimeWorldBinding binding, uint version, string worldSignature,
        ulong spendableBalance, int monetaryReceipts)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (version != SchemaVersion || spendableBalance != 0UL ||
            monetaryReceipts != 0 ||
            !IsCanonicalDigest(worldSignature) ||
            worldSignature != binding.WorldSignatureSha256Hex)
            throw new InvalidOperationException(
                "VS101 only restores a V1 zero-money, zero-receipt, world-bound dormant treasury.");
        return CreateDormant(binding);
    }
}
