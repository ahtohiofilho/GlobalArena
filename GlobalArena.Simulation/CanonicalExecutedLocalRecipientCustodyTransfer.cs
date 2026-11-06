using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A real, journal-backed non-consuming move from market custody into
/// the exclusive local custody of a REAL staffed production group.
/// The goods are no longer on the market and cannot be used by the old
/// consumer/worker inventory. This is not proof of legal title, transfer
/// of economic ownership, paid sale, fiscal income or food intake.
/// </summary>
public sealed class CanonicalExecutedLocalRecipientCustodyTransfer
{
    public int ActionOrdinal { get; }
    public int SourceCommitmentActionOrdinal { get; }
    public ulong MovementTurn { get; }
    public ulong FineTile { get; }
    public uint Commodity { get; }
    public ulong RawQuantity { get; }
    public EconomicActivityKind RecipientActivity { get; }
    public ulong RecipientWorkers { get; }
    public string BeforeWorldHashHex { get; }
    public string AfterWorldHashHex { get; }
    public IReadOnlyList<CanonicalPhysicalLotHolding> SourceAttributions { get; }

    public bool ProvesPhysicalCustodyHandoff => true;
    public bool ProvesRealLocalRecipientGroup => true;
    public bool ProvesActualConsumption => false;
    public bool ProvesExecutedCommercialSale => false;
    public bool ProvesLegalOwnershipTransfer => false;
    public bool ExecutesRemoteFreight => false;
    public bool AuthorizesPriceOrRevenuePosting => false;
    public bool AuthorizesNewPurchasingPower => false;
    public bool ChangesFoodNutrition => false;

    internal CanonicalExecutedLocalRecipientCustodyTransfer(
        int actionOrdinal, int sourceCommitmentActionOrdinal,
        ulong movementTurn, ulong fineTile, uint commodity,
        ulong rawQuantity, EconomicActivityKind activity, ulong workers,
        string beforeHash, string afterHash,
        IReadOnlyList<CanonicalPhysicalLotHolding> attributions)
    {
        if (actionOrdinal <= sourceCommitmentActionOrdinal ||
            sourceCommitmentActionOrdinal <= 0 ||
            fineTile == 0UL || commodity is not (2U or 3U) ||
            rawQuantity == 0UL || workers == 0UL ||
            activity is not (EconomicActivityKind.Agriculture or
                EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
            string.IsNullOrWhiteSpace(beforeHash) ||
            string.IsNullOrWhiteSpace(afterHash) ||
            attributions is null || attributions.Count == 0 ||
            attributions.Aggregate(0UL,
                (sum, x) => checked(sum + x.RawQuantity)) != rawQuantity ||
            attributions.Any(x => x.Custody !=
                CanonicalPhysicalLotCustodyKind.MarketCommitment ||
                x.CustodyId != (ulong)sourceCommitmentActionOrdinal ||
                x.Commodity != commodity))
            throw new InvalidOperationException(
                "Local recipient custody requires exact journaled market lots and a real workgroup.");

        ActionOrdinal = actionOrdinal;
        SourceCommitmentActionOrdinal = sourceCommitmentActionOrdinal;
        MovementTurn = movementTurn;
        FineTile = fineTile;
        Commodity = commodity;
        RawQuantity = rawQuantity;
        RecipientActivity = activity;
        RecipientWorkers = workers;
        BeforeWorldHashHex = beforeHash;
        AfterWorldHashHex = afterHash;
        SourceAttributions = Array.AsReadOnly(attributions.ToArray());
    }
}
