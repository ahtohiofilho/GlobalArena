using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Executed sink for real wood/stone stock. Intermediate use is a recorded
/// input without an invented manufactured output. Final use does not claim
/// a building was constructed or that a market transaction occurred.
/// </summary>
public enum CanonicalMaterialDispositionKind : byte
{
    FinalPhysicalUse = 1,
    IntermediateInputUse = 2
}

/// <summary>
/// One journal-authorized physical withdrawal. The attribution of particular
/// fungible origin vintages is FIFO bookkeeping, not authenticated lot identity.
/// </summary>
public sealed class CanonicalExecutedMaterialDisposition
{
    public int ActionOrdinal { get; }
    public ulong MovementTurn { get; }
    public ulong FineTile { get; }
    public uint Commodity { get; }
    public CanonicalMaterialDispositionKind Kind { get; }
    public ulong UsedRaw { get; }
    public string BeforeWorldHashHex { get; }
    public string AfterWorldHashHex { get; }
    public IReadOnlyList<CanonicalPhysicalLotHolding> SourceAttributions { get; }

    public bool ProvesActualMaterialDebit => true;
    public bool ProvesActualFinalProductOrIntermediateInput =>
        Kind is CanonicalMaterialDispositionKind.FinalPhysicalUse or
            CanonicalMaterialDispositionKind.IntermediateInputUse;
    public bool ProvesConstructionOrFinishedManufacturing => false;
    public bool ProvesOriginalFungibleLotIdentity => false;
    public bool CreatesMoneyOrCredit => false;
    public bool CanAuthorizeRealPurchasingPower => false;

    internal CanonicalExecutedMaterialDisposition(
        int actionOrdinal, ulong movementTurn, ulong fineTile,
        uint commodity, CanonicalMaterialDispositionKind kind,
        ulong usedRaw, string before, string after,
        IReadOnlyList<CanonicalPhysicalLotHolding> sources)
    {
        if (actionOrdinal <= 0 || fineTile == 0UL ||
            commodity is not (2U or 3U) || usedRaw == 0UL ||
            kind is not (CanonicalMaterialDispositionKind.FinalPhysicalUse or
                CanonicalMaterialDispositionKind.IntermediateInputUse) ||
            string.IsNullOrEmpty(before) || string.IsNullOrEmpty(after) ||
            sources.Count == 0 ||
            sources.Aggregate(0UL, (total, x) =>
                checked(total + x.RawQuantity)) != usedRaw)
            throw new InvalidOperationException(
                "An executed material disposition requires one real, conserved physical debit.");
        ActionOrdinal = actionOrdinal;
        MovementTurn = movementTurn;
        FineTile = fineTile;
        Commodity = commodity;
        Kind = kind;
        UsedRaw = usedRaw;
        BeforeWorldHashHex = before;
        AfterWorldHashHex = after;
        SourceAttributions = Array.AsReadOnly(sources.ToArray());
    }
}
