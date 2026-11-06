using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// An explicit, verifiable instruction to physically use ONE existing market
/// escrow lot at its own tile, by a real allocated work group. This is not
/// an instruction to sell, transport, or arbitrarily discard all offers.
/// </summary>
public sealed record CanonicalLocalWorkforceMaterialUseOrder(
    int MarketCommitmentActionOrdinal,
    EconomicActivityKind BeneficiaryActivity,
    CanonicalMaterialDispositionKind UseKind);

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01I: execute multiple authenticated local material
/// uses in an ISOLATED canonical session and expose their true journal receipts.
/// If any order fails, the original session and its save remain unchanged.
/// Goods that lack a legitimate proposed material use remain locked and unsold.
/// </summary>
public sealed class CanonicalStagedLocalMaterialUseBatch
{
    public CanonicalPhysicalTacticalSession PreparedSession { get; }
    public IReadOnlyList<CanonicalExecutedCommittedMaterialUse> ExecutedUses { get; }
    public BigInteger TotalUsedRaw { get; }
    public string SourceWorldHashHex { get; }
    public string PreparedWorldHashHex => PreparedSession.CurrentHash.HexDigest;
    public CanonicalMarketExecutedPhysicalCoverage PhysicalCoverage { get; }

    public bool ExecutesRealJournaledLocalMaterialUses => true;
    public bool MakesUnrequestedMaterialUses => false;
    public bool ProvesCommercialSales => false;
    public bool CreatesMonetaryRevenue => false;
    public bool ExecutesRemoteTransport => false;
    public bool ActivatesNutritionFromAggregateFood => false;
    public bool ChangesOriginalSession => false;

    private CanonicalStagedLocalMaterialUseBatch(
        CanonicalPhysicalTacticalSession prepared,
        IReadOnlyList<CanonicalExecutedCommittedMaterialUse> executions,
        string sourceWorldHashHex)
    {
        PreparedSession = prepared;
        ExecutedUses = Array.AsReadOnly(executions.ToArray());
        SourceWorldHashHex = sourceWorldHashHex;
        TotalUsedRaw = ExecutedUses.Aggregate(
            BigInteger.Zero, (total, x) => total + x.UsedRaw);
        PhysicalCoverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(prepared);
        PhysicalCoverage.VerifyIndependent();
    }

    public static CanonicalStagedLocalMaterialUseBatch Prepare(
        CanonicalPhysicalTacticalSession source,
        IEnumerable<CanonicalLocalWorkforceMaterialUseOrder> instructions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(instructions);

        var requested = instructions.ToArray();
        if (requested.Length == 0 || requested.Any(x => x is null))
            throw new ArgumentException(
                "At least one real workgroup material-use order is required.",
                nameof(instructions));
        if (requested.Select(x => x.MarketCommitmentActionOrdinal)
            .Distinct().Count() != requested.Length)
            throw new ArgumentException(
                "One physical market escrow may be consumed only once in a batch.",
                nameof(instructions));

        var sourceHash = source.CurrentHash.HexDigest;
        var sourceSave = source.ExportSaveJson();
        var before = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(source);
        var commitments = source.PhysicalStockCommitments.ToDictionary(
            x => x.ActionOrdinal);

        // Validate ALL explicit recipients and exact material escrows before
        // creating any journal action, rather than consuming surplus simply
        // because it is available for the market.
        foreach (var order in requested)
        {
            if (order.MarketCommitmentActionOrdinal <= 0 ||
                order.UseKind is not (
                    CanonicalMaterialDispositionKind.FinalPhysicalUse or
                    CanonicalMaterialDispositionKind.IntermediateInputUse) ||
                order.BeneficiaryActivity is not (
                    EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or
                    EconomicActivityKind.Mining) ||
                !commitments.TryGetValue(order.MarketCommitmentActionOrdinal,
                    out var lockRecord) ||
                !lockRecord.IsLive ||
                lockRecord.Kind != CanonicalPhysicalCommitmentKind.MarketOffer ||
                lockRecord.Commodity is not (2U or 3U) ||
                !source.State.Allocations.Any(x =>
                    x.TileId.FineStrategicCellId.Value == lockRecord.FineTile &&
                    x.Activity == order.BeneficiaryActivity && x.Workers > 0UL))
                throw new InvalidOperationException(
                    "Material use requires one living local workgroup and one exact " +
                    "unconsumed wood/stone market commitment.");
        }

        // The new journal operations are performed ONLY on a replayed clone.
        var cloned = CanonicalPhysicalTacticalSession.RestoreSaveJson(sourceSave);
        if (cloned.ExportSaveJson() != sourceSave ||
            cloned.CurrentHash.HexDigest != sourceHash)
            throw new InvalidOperationException(
                "Cannot safely stage uses from a non-replayable canonical game.");

        var executed = new List<CanonicalExecutedCommittedMaterialUse>();
        foreach (var order in requested.OrderBy(x =>
            x.MarketCommitmentActionOrdinal))
        {
            var locked = cloned.PhysicalStockCommitments.Single(x =>
                x.ActionOrdinal == order.MarketCommitmentActionOrdinal);
            var destination = new CanonicalLocalMarketDestinationCandidate(
                locked.FineTile, locked.FineTile, locked.Commodity,
                locked.RawQuantity, CanonicalLocalMarketUsePurpose.WorkforceMaterialUse,
                order.BeneficiaryActivity);
            // A fresh preflight is necessary after EVERY real journal action,
            // because prior plans have rightly become stale.
            var preflight = CanonicalLocalMarketDestinationPreflight.FromCurrent(
                cloned, new[] { destination });
            var receipt = CanonicalLocalMarketMaterialDestinationExecutor.Execute(
                cloned, preflight, order.MarketCommitmentActionOrdinal, order.UseKind);
            if (receipt.SourceCommitmentActionOrdinal != locked.ActionOrdinal ||
                receipt.UsedRaw != locked.RawQuantity ||
                receipt.BeneficiaryActivity != order.BeneficiaryActivity ||
                !receipt.ProvesLocalWorkforceGroup ||
                !receipt.ProvesExecutedPhysicalMaterialUse ||
                receipt.ProvesMonetarySale || receipt.HasExecutedTransport)
                throw new InvalidOperationException(
                    "One staged local material use contradicts its physical order.");
            executed.Add(receipt);
        }

        var result = new CanonicalStagedLocalMaterialUseBatch(
            cloned, executed, sourceHash);
        var after = result.PhysicalCoverage;
        if (result.TotalUsedRaw <= 0 ||
            before.TotalPendingOfferRaw - result.TotalUsedRaw !=
                after.TotalPendingOfferRaw ||
            before.TotalExecutedPhysicalMaterialRaw + result.TotalUsedRaw !=
                after.TotalExecutedPhysicalMaterialRaw ||
            before.TotalRealWorkforceMaterialRaw + result.TotalUsedRaw !=
                after.TotalRealWorkforceMaterialRaw ||
            before.TotalEligibleAwaitingDestinationRaw - result.TotalUsedRaw !=
                after.TotalEligibleAwaitingDestinationRaw ||
            before.TotalDeliberatelyRetainedRaw != after.TotalDeliberatelyRetainedRaw ||
            before.Markets.Sum(x => x.UncommittedFreeRaw) !=
                after.Markets.Sum(x => x.UncommittedFreeRaw))
            throw new InvalidOperationException(
                "Staged local uses did not conserve source custody, " +
                "executed physical volume and pending quantities.");

        // A batch result is always restorable as the same canonical event
        // sequence. No new event type or save version is introduced.
        var preparedSave = cloned.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(preparedSave);
        if (restored.ExportSaveJson() != preparedSave ||
            restored.CurrentHash != cloned.CurrentHash ||
            restored.ExecutedCommittedMaterialUses.Count !=
                cloned.ExecutedCommittedMaterialUses.Count)
            throw new InvalidOperationException(
                "Material-use batch failed deterministic canonical replay.");

        if (source.ExportSaveJson() != sourceSave ||
            source.CurrentHash.HexDigest != sourceHash)
            throw new InvalidOperationException(
                "Material-use batch must not mutate the source game.");

        return result;
    }
}
