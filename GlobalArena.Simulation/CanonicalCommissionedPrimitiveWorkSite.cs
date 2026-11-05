using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Executed, journal-replayable FIELD COMMISSIONING of one pre-existing
/// primitive work activity after a material frame and a later real productive
/// economic cycle. It is NOT proof of dedicated construction labor, not a
/// new WorldState asset and does not unlock additional yield or a new product.
/// </summary>
public sealed class CanonicalCommissionedPrimitiveWorkSite
{
    public int CommissionActionOrdinal { get; }
    public int AssemblyActionOrdinal { get; }
    public ulong FineTile { get; }
    public EconomicActivityKind Activity { get; }
    public uint ExecutedCommodity { get; }
    public ulong WitnessCycleOrdinal { get; }
    public ulong WitnessMovementTurn { get; }
    public ulong WitnessProducedRaw { get; }
    public string WitnessClosingTickHashHex { get; }
    public string BeforeWorldHashHex { get; }
    public string AfterWorldHashHex { get; }

    public bool HasExecutedPostAssemblyWorkEvidence => true;
    public bool IsRegisteredForExistingPrimitiveWork => true;
    public bool IsDedicatedConstructionLaborAuthenticated => false;
    public bool IsCanonicalWorldAsset => false;
    public bool AuthorizesAdditionalProductivity => false;
    public bool CreatesTradeableManufacturedGood => false;
    public bool CanAuthorizeRealPurchasingPower => false;

    internal CanonicalCommissionedPrimitiveWorkSite(
        int actionOrdinal, int assemblyOrdinal, ulong fineTile,
        EconomicActivityKind activity, uint commodity,
        ulong cycleOrdinal, ulong movementTurn, ulong producedRaw,
        string witnessClosingTickHash, string beforeHash, string afterHash)
    {
        if (actionOrdinal <= assemblyOrdinal || assemblyOrdinal <= 0 ||
            fineTile == 0UL || cycleOrdinal == 0UL || movementTurn == 0UL ||
            producedRaw == 0UL ||
            activity is not (EconomicActivityKind.Agriculture or
                EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
            commodity != (activity switch
            {
                EconomicActivityKind.Agriculture => 1U,
                EconomicActivityKind.Mining => 2U,
                EconomicActivityKind.Forestry => 3U,
                _ => 0U
            }) ||
            string.IsNullOrEmpty(witnessClosingTickHash) ||
            string.IsNullOrEmpty(beforeHash) ||
            string.IsNullOrEmpty(afterHash))
            throw new InvalidOperationException(
                "Field commissioning requires a real frame and later executed productive evidence.");

        CommissionActionOrdinal = actionOrdinal;
        AssemblyActionOrdinal = assemblyOrdinal;
        FineTile = fineTile;
        Activity = activity;
        ExecutedCommodity = commodity;
        WitnessCycleOrdinal = cycleOrdinal;
        WitnessMovementTurn = movementTurn;
        WitnessProducedRaw = producedRaw;
        WitnessClosingTickHashHex = witnessClosingTickHash;
        BeforeWorldHashHex = beforeHash;
        AfterWorldHashHex = afterHash;
    }
}
