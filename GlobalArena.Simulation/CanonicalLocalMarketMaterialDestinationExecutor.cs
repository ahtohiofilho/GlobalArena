namespace GlobalArena.Simulation;

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01C: connect an authenticated, single-destination
/// physical plan to the existing JOURNALED material-use action. This gateway
/// executes a local MATERIAL sink only, not a commercial sale or a fictional
/// automatic purchase. The already committed market lots are used exactly once.
/// </summary>
public static class CanonicalLocalMarketMaterialDestinationExecutor
{
    public static CanonicalExecutedCommittedMaterialUse Execute(
        CanonicalPhysicalTacticalSession session,
        CanonicalLocalMarketDestinationPreflight proposal,
        int commitmentActionOrdinal,
        CanonicalMaterialDispositionKind useKind)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(proposal);
        proposal.RequireCurrent(session);

        if (useKind is not (CanonicalMaterialDispositionKind.FinalPhysicalUse or
                            CanonicalMaterialDispositionKind.IntermediateInputUse))
            throw new ArgumentOutOfRangeException(nameof(useKind));
        if (proposal.Candidates.Count != 1)
            throw new InvalidOperationException(
                "One physical execution must consume exactly one certified destination.");

        var destination = proposal.Candidates[0];
        if (destination.Purpose != CanonicalLocalMarketUsePurpose.WorkforceMaterialUse ||
            destination.Commodity is not (2U or 3U) ||
            !destination.BeneficiaryActivity.HasValue ||
            destination.SourceFineTile != destination.DestinationFineTile)
            throw new InvalidOperationException(
                "This journal path authenticates only real same-tile workforce material use.");

        var locked = session.PhysicalStockCommitments.SingleOrDefault(x =>
            x.ActionOrdinal == commitmentActionOrdinal && x.IsLive &&
            x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer &&
            x.FineTile == destination.SourceFineTile &&
            x.Commodity == destination.Commodity &&
            x.RawQuantity == destination.RawQuantity);

        if (locked is null)
            throw new InvalidOperationException(
                "Physical destination must match exactly one existing market escrow; " +
                "free or retained goods cannot be silently consumed.");

        // No provisional journal action and no second debit: the existing
        // single canonical action consumes one escrowed lot custody with its
        // real local group, source attributions, and replayable receipt.
        var executed = session.UseCommittedLocalMaterialForWorkforce(
            commitmentActionOrdinal, destination.BeneficiaryActivity.Value,
            useKind);

        if (executed.SourceCommitmentActionOrdinal != commitmentActionOrdinal ||
            executed.FineTile != destination.DestinationFineTile ||
            executed.Commodity != destination.Commodity ||
            executed.UsedRaw != destination.RawQuantity ||
            executed.BeneficiaryActivity != destination.BeneficiaryActivity ||
            !executed.ProvesExecutedPhysicalMaterialUse ||
            executed.ProvesMonetarySale ||
            executed.HasExecutedTransport)
            throw new InvalidOperationException(
                "Executed local physical destination contradicted its certified proposal.");

        return executed;
    }
}
