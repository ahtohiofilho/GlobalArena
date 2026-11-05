using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A conserved physical composite of actual wood and stone. The assembly
/// records incorporated materials; it is NOT a commissioned production site,
/// a building, a new tradeable commodity, or an economic output valuation.
/// </summary>
public enum CanonicalPrimitiveAssemblyKind : byte
{
    UncommissionedMaterialFrame = 1
}

/// <summary>
/// Read-only replayed journal proof that two physical input stocks were
/// withdrawn in one atomic action and incorporated into a material frame.
/// The original identity of fungible inventory remains FIFO attribution.
/// </summary>
public sealed class CanonicalExecutedPrimitiveAssembly
{
    public int ActionOrdinal { get; }
    public ulong FineTile { get; }
    public ulong MovementTurn { get; }
    public CanonicalPrimitiveAssemblyKind Kind { get; }
    public ulong IncorporatedWoodRaw { get; }
    public ulong IncorporatedStoneRaw { get; }
    public string BeforeWorldHashHex { get; }
    public string AfterWorldHashHex { get; }
    public IReadOnlyList<CanonicalPhysicalLotHolding> WoodSources { get; }
    public IReadOnlyList<CanonicalPhysicalLotHolding> StoneSources { get; }

    public bool HasRealConservedMaterialInputs => true;
    public bool HasJournalReconstructablePhysicalAssembly => true;
    public bool IsCommissionedOrProductiveFacility => false;
    public bool CreatesNewTradeableCommodity => false;
    public bool InventsConversionYieldOrLaborTime => false;
    public bool AuthenticatesIndividualFungibleLotIdentity => false;
    public bool HasProducedEconomicFinalValue => false;
    public bool CanAuthorizeRealPurchasingPower => false;

    internal CanonicalExecutedPrimitiveAssembly(
        int ordinal, ulong tile, ulong turn, CanonicalPrimitiveAssemblyKind kind,
        ulong wood, ulong stone, string before, string after,
        IReadOnlyList<CanonicalPhysicalLotHolding> woodSources,
        IReadOnlyList<CanonicalPhysicalLotHolding> stoneSources)
    {
        ArgumentNullException.ThrowIfNull(woodSources);
        ArgumentNullException.ThrowIfNull(stoneSources);
        if (ordinal <= 0 || tile == 0UL ||
            kind != CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame ||
            wood == 0UL || stone == 0UL ||
            string.IsNullOrEmpty(before) || string.IsNullOrEmpty(after) ||
            woodSources.Count == 0 || stoneSources.Count == 0 ||
            woodSources.Any(x => x.Commodity != 3U ||
                x.Custody != CanonicalPhysicalLotCustodyKind.LocalStock ||
                x.CustodyId != tile || x.RawQuantity == 0UL) ||
            stoneSources.Any(x => x.Commodity != 2U ||
                x.Custody != CanonicalPhysicalLotCustodyKind.LocalStock ||
                x.CustodyId != tile || x.RawQuantity == 0UL) ||
            woodSources.Aggregate(0UL, (sum, x) =>
                checked(sum + x.RawQuantity)) != wood ||
            stoneSources.Aggregate(0UL, (sum, x) =>
                checked(sum + x.RawQuantity)) != stone)
            throw new InvalidOperationException(
                "A primitive physical assembly requires two exact conserved real material inputs.");

        ActionOrdinal = ordinal;
        FineTile = tile;
        MovementTurn = turn;
        Kind = kind;
        IncorporatedWoodRaw = wood;
        IncorporatedStoneRaw = stone;
        BeforeWorldHashHex = before;
        AfterWorldHashHex = after;
        WoodSources = Array.AsReadOnly(woodSources.ToArray());
        StoneSources = Array.AsReadOnly(stoneSources.ToArray());
    }
}
