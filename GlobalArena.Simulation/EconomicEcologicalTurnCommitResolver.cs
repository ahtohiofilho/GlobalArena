using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class EconomicEcologicalTurnCommitResult
{
    public EconomicEcologicalCheckpoint ProposedNext { get; }
    public EconomicProductionDemandResult RealizedProductionAndDemand { get; }
    public EconomicProductionDemandResult MarketOfferAndDemand { get; }

    internal EconomicEcologicalTurnCommitResult(
        EconomicEcologicalCheckpoint proposedNext,
        EconomicProductionDemandResult realizedProductionAndDemand,
        EconomicProductionDemandResult marketOfferAndDemand)
    {
        ProposedNext = proposedNext;
        RealizedProductionAndDemand = realizedProductionAndDemand;
        MarketOfferAndDemand = marketOfferAndDemand;
    }
}

/// <summary>
/// A single deterministic, compare-and-swap gated economic production
/// projection across ecological capital and intentional origin holdings.
/// No unproven destination purchase is credited. This remains a proposed
/// checkpoint and is not installed by the gameplay turn scheduler.
/// </summary>
public sealed class EconomicEcologicalTurnCommitResolver
{
    private readonly EconomicNaturalCapitalTurnResolver _natural = new();
    private readonly EconomicDeliberateMaterialFlowResolver _planner = new();
    private readonly EconomicMaterialReserveAccrualResolver _accrual = new();

    public EconomicEcologicalTurnCommitResult Propose(
        WorldGenerationResult generatedWorld,
        EconomicEcologicalCheckpoint previous,
        string expectedCheckpointSha256Hex,
        ulong turnOrdinal,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        previous.RequireExpectedDigest(expectedCheckpointSha256Hex);

        // Detect a stale/cross-world snapshot before deriving any goods.
        var world = previous.World;
        if (world.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException("Turn proposal needs the checkpoint's bound world.");
        if (new CanonicalWorldStateHasher().Compute(world) != previous.WorldHash)
            throw new InvalidOperationException("Checkpoint no longer matches WorldState.");

        var holds = originHolds.ToArray();
        if (holds.Any(x => x is null))
            throw new ArgumentException("Hold orders must not contain null.");
        var demand = workerDemand.ToArray();

        var ecological = _natural.Resolve(
            generatedWorld, world, previous.NaturalCapital,
            previous.Policy, previous.Commodities, demand, turnOrdinal);

        var offers = ecological.RealizedFlows;
        var book = previous.MaterialReserves;

        // Resolve all civilization-specific holds against the SAME actual
        // ecological output. Each issuer's pre-market projection reduces
        // supply, while the book cursor marks each civilization's turn.
        // This prevents a later issuer from resurrecting already held goods.
        foreach (var owner in world.Civilizations.Civilizations.OrderBy(x => x.Value))
        {
            var points = world.Economy.EconomicPoints
                .Where(x => x.Owner == owner).Select(x => x.Id).ToHashSet();
            var orders = holds.Where(x => points.Contains(x.EconomicPointId)).ToArray();

            var planned = _planner.Resolve(
                world, owner, offers, orders,
                Array.Empty<EconomicMaterialDestinationAcquisitionOrder>());
            book = _accrual.AccrueOriginHolds(
                world, owner, turnOrdinal, book, planned);
            offers = planned.MarketFlows;
        }

        // Unknown points cannot be silently dropped when partitioning orders.
        var known = world.Economy.EconomicPoints.Select(x => x.Id).ToHashSet();
        if (holds.Any(x => !known.Contains(x.EconomicPointId)))
            throw new InvalidOperationException(
                "Origin hold references an unknown economic point.");

        // No WorldState mutation, no new WorldState hash format, and no
        // assertion that the market has paid or delivered anything.
        var next = new EconomicEcologicalCheckpoint(
            generatedWorld, world, ecological.NextBook, book,
            previous.Policy, previous.Commodities);

        return new EconomicEcologicalTurnCommitResult(
            next, ecological.RealizedFlows, offers);
    }
}
