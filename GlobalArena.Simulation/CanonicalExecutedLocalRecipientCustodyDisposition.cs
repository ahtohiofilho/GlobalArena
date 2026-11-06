using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// The complete, journal-authorized outcome of physical stock previously
/// delivered into a real local recipient group's exclusive custody.
/// A return re-enters free stock; a use consumes the recipient's custody.
/// Neither constitutes an executed commercial sale or a payment.
/// </summary>
public enum CanonicalLocalRecipientCustodyDispositionKind : byte
{
    ReturnToLocalFreeStock = 1,
    ExecuteWorkforceMaterialUse = 2
}

public sealed class CanonicalExecutedLocalRecipientCustodyDisposition
{
    public int ActionOrdinal { get; }
    public int SourceCommitmentActionOrdinal { get; }
    public int OriginalTransferActionOrdinal { get; }
    public ulong MovementTurn { get; }
    public ulong FineTile { get; }
    public uint Commodity { get; }
    public ulong RawQuantity { get; }
    public CanonicalLocalRecipientCustodyDispositionKind Kind { get; }
    public EconomicActivityKind OriginalRecipientActivity { get; }
    public ulong ActiveBeneficiaryWorkers { get; }
    public CanonicalMaterialDispositionKind? MaterialUseKind { get; }
    public string BeforeWorldHashHex { get; }
    public string AfterWorldHashHex { get; }
    public IReadOnlyList<CanonicalPhysicalLotHolding> SourceAttributions { get; }

    public bool ProvesExecutedMaterialUse =>
        Kind == CanonicalLocalRecipientCustodyDispositionKind.ExecuteWorkforceMaterialUse;
    public bool ProvesReturnOfSamePhysicalCustody =>
        Kind == CanonicalLocalRecipientCustodyDispositionKind.ReturnToLocalFreeStock;
    public bool ProvesCommercialSale => false;
    public bool ProvesLegalOwnershipTransfer => false;
    public bool AuthorizesMonetaryRevenueOrTax => false;
    public bool ExecutesRemoteFreight => false;
    public bool ConsumesAggregateLegacyFood => false;

    internal CanonicalExecutedLocalRecipientCustodyDisposition(
        int actionOrdinal, int sourceCommitmentActionOrdinal,
        int originalTransferActionOrdinal, ulong movementTurn,
        ulong fineTile, uint commodity, ulong rawQuantity,
        CanonicalLocalRecipientCustodyDispositionKind kind,
        EconomicActivityKind originalRecipientActivity,
        ulong activeBeneficiaryWorkers,
        CanonicalMaterialDispositionKind? materialUseKind,
        string beforeHash, string afterHash,
        IReadOnlyList<CanonicalPhysicalLotHolding> sourceAttributions)
    {
        if (actionOrdinal <= originalTransferActionOrdinal ||
            originalTransferActionOrdinal <= sourceCommitmentActionOrdinal ||
            sourceCommitmentActionOrdinal <= 0 || fineTile == 0UL ||
            commodity is not (2U or 3U) || rawQuantity == 0UL ||
            kind is not (
                CanonicalLocalRecipientCustodyDispositionKind.ReturnToLocalFreeStock or
                CanonicalLocalRecipientCustodyDispositionKind.ExecuteWorkforceMaterialUse) ||
            originalRecipientActivity is not (
                EconomicActivityKind.Agriculture or
                EconomicActivityKind.Forestry or
                EconomicActivityKind.Mining) ||
            (kind == CanonicalLocalRecipientCustodyDispositionKind.ReturnToLocalFreeStock
                ? activeBeneficiaryWorkers != 0UL || materialUseKind.HasValue
                : activeBeneficiaryWorkers == 0UL ||
                  materialUseKind is not (
                      CanonicalMaterialDispositionKind.FinalPhysicalUse or
                      CanonicalMaterialDispositionKind.IntermediateInputUse)) ||
            string.IsNullOrWhiteSpace(beforeHash) ||
            string.IsNullOrWhiteSpace(afterHash) ||
            sourceAttributions is null || sourceAttributions.Count == 0 ||
            sourceAttributions.Aggregate(0UL,
                (sum, x) => checked(sum + x.RawQuantity)) != rawQuantity ||
            sourceAttributions.Any(x =>
                x.Custody != CanonicalPhysicalLotCustodyKind.LocalRecipientCustody ||
                x.CustodyId != (ulong)sourceCommitmentActionOrdinal ||
                x.Commodity != commodity))
            throw new InvalidOperationException(
                "Recipient custody closure requires one exact transferred physical lot, " +
                "validated disposition and a live productive group for material use.");
        ActionOrdinal = actionOrdinal;
        SourceCommitmentActionOrdinal = sourceCommitmentActionOrdinal;
        OriginalTransferActionOrdinal = originalTransferActionOrdinal;
        MovementTurn = movementTurn;
        FineTile = fineTile;
        Commodity = commodity;
        RawQuantity = rawQuantity;
        Kind = kind;
        OriginalRecipientActivity = originalRecipientActivity;
        ActiveBeneficiaryWorkers = activeBeneficiaryWorkers;
        MaterialUseKind = materialUseKind;
        BeforeWorldHashHex = beforeHash;
        AfterWorldHashHex = afterHash;
        SourceAttributions = Array.AsReadOnly(sourceAttributions.ToArray());
    }
}
