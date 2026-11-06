using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Authenticated journal-backed material sink fulfilled from previously
/// committed MARKET custody. A physical use is NOT a sale: buyer identity,
/// ownership transfer, market price and fiscal revenue remain unproven.
/// </summary>
public sealed class CanonicalExecutedCommittedMaterialUse
{
    public int ActionOrdinal { get; }
    public int SourceCommitmentActionOrdinal { get; }
    public ulong MovementTurn { get; }
    public ulong FineTile { get; }
    public uint Commodity { get; }
    public CanonicalMaterialDispositionKind Kind { get; }
    public ulong UsedRaw { get; }
    public string BeforeWorldHashHex { get; }
    public string AfterWorldHashHex { get; }
    public IReadOnlyList<CanonicalPhysicalLotHolding> SourceAttributions { get; }

    public bool ProvesExecutedPhysicalMaterialUse => true;
    public bool ProvesDeliveryToFinalEconomicBuyer => false;
    public bool ProvesMonetarySale => false;
    public bool HasExecutedTransport => false;
    public bool CreatesNewPhysicalStock => false;
    public bool AuthorizesPurchasingPowerIndexN => false;

    internal CanonicalExecutedCommittedMaterialUse(
        int actionOrdinal, int sourceCommitmentActionOrdinal,
        ulong movementTurn, ulong fineTile, uint commodity,
        CanonicalMaterialDispositionKind kind, ulong usedRaw,
        string beforeHash, string afterHash,
        IReadOnlyList<CanonicalPhysicalLotHolding> sourceAttributions)
    {
        if (actionOrdinal <= sourceCommitmentActionOrdinal ||
            sourceCommitmentActionOrdinal <= 0 || fineTile == 0UL ||
            commodity is not (2U or 3U) || usedRaw == 0UL ||
            kind is not (CanonicalMaterialDispositionKind.FinalPhysicalUse or
                CanonicalMaterialDispositionKind.IntermediateInputUse) ||
            string.IsNullOrEmpty(beforeHash) ||
            string.IsNullOrEmpty(afterHash) ||
            sourceAttributions is null ||
            sourceAttributions.Count == 0 ||
            sourceAttributions.Aggregate(0UL, (sum, x) =>
                checked(sum + x.RawQuantity)) != usedRaw)
            throw new InvalidOperationException(
                "Executed market-custody material use requires exact physical attribution.");

        ActionOrdinal = actionOrdinal;
        SourceCommitmentActionOrdinal = sourceCommitmentActionOrdinal;
        MovementTurn = movementTurn;
        FineTile = fineTile;
        Commodity = commodity;
        Kind = kind;
        UsedRaw = usedRaw;
        BeforeWorldHashHex = beforeHash;
        AfterWorldHashHex = afterHash;
        SourceAttributions = Array.AsReadOnly(sourceAttributions.ToArray());
    }
}
