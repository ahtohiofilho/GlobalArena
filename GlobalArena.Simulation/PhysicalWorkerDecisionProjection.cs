using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Projected, physical outcomes of five canonical economic settlements.
/// Monetary accounts are still processed by the existing engine but this
/// read model does not expose a fictional GDP, salary or household income.
/// </summary>
public sealed record PhysicalWorkerScenarioOutcome(
    RecentPhysicalEconomicCycles FoodService,
    ulong LocalFoodStockRaw,
    ulong LocalWoodStockRaw,
    ulong LocalStoneStockRaw);

/// <summary>
/// The two alternatives always start from the exact same authoritative save:
/// keep current assignments versus reassign residents on one physical tile.
/// Neither branch can mutate the actual player's session.
/// </summary>
public sealed record PhysicalWorkerDecisionComparison(
    ulong StartingMovementTurn,
    ulong ProjectedFinalMovementTurn,
    PhysicalTacticalTileId Tile,
    EconomicActivityKind FromJob,
    EconomicActivityKind ToJob,
    ulong Workers,
    PhysicalWorkerScenarioOutcome Unchanged,
    PhysicalWorkerScenarioOutcome Reassigned);

public static class PhysicalWorkerDecisionProjection
{
    public const int ForecastCycles = 5;

    public static PhysicalWorkerDecisionComparison Compare(
        CanonicalPhysicalTacticalSession session, PhysicalTacticalTileId tile,
        EconomicActivityKind fromJob, EconomicActivityKind toJob, ulong workers)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Single immutable origin. Each alternate timeline reconstructs its
        // independent canonical session and replays the exact same source.
        var sourceSave = session.ExportSaveJson();
        var unchanged = CanonicalPhysicalTacticalSession.RestoreSaveJson(sourceSave);
        var reassigned = CanonicalPhysicalTacticalSession.RestoreSaveJson(sourceSave);
        reassigned.ReassignWorkers(tile, fromJob, toJob, workers);

        var initialTurn = session.State.MovementTurn;
        var originalPlan = PhysicalEconomicCycleProgression.AdvanceBatch(
            unchanged, ForecastCycles);
        var alternativePlan = PhysicalEconomicCycleProgression.AdvanceBatch(
            reassigned, ForecastCycles);
        if (originalPlan.FinalSettlementTurn != alternativePlan.FinalSettlementTurn)
            throw new InvalidOperationException(
                "Economic alternatives diverged in their underlying time horizon.");

        static PhysicalWorkerScenarioOutcome ReadOutcome(
            CanonicalPhysicalTacticalSession candidate, PhysicalTacticalTileId selected)
        {
            return new PhysicalWorkerScenarioOutcome(
                PhysicalEconomicCycleProgression.SummarizeRecent(candidate, ForecastCycles),
                candidate.StockAt(selected, new CommodityId(1U)),
                candidate.StockAt(selected, new CommodityId(3U)),
                candidate.StockAt(selected, new CommodityId(2U)));
        }

        return new PhysicalWorkerDecisionComparison(
            initialTurn, originalPlan.FinalSettlementTurn,
            tile, fromJob, toJob, workers,
            ReadOutcome(unchanged, tile), ReadOutcome(reassigned, tile));
    }
}
