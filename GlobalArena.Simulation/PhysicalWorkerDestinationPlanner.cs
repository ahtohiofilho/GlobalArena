using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A preparation for a possible adjacent move, NEVER an order. The highest
/// raw potential does not imply the highest net economic value or optimal
/// long-term strategy; all candidates remain available for manual selection.
/// </summary>
public sealed record PhysicalWorkerDestinationPlan(
    PhysicalTacticalTileId Origin,
    PhysicalTacticalTileId Destination,
    EconomicActivityKind FromJob,
    EconomicActivityKind DestinationJob,
    ulong Workers,
    long OriginYieldPerWorkerRaw,
    long DestinationYieldPerWorkerRaw,
    ulong DestinationResidents,
    ulong DestinationTravellers);

/// <summary>
/// Bridge between canonical geographic reconnaissance (VS26) and a *separate*
/// canonical timed transit order (VS15). Sorting, validation and the proposed
/// tile are deterministic and no live-game state is committed in this step.
/// </summary>
public static class PhysicalWorkerDestinationPlanner
{
    public static PhysicalWorkerDestinationPlan PreviewHighestYield(
        CanonicalPhysicalTacticalSession session, PhysicalTacticalTileId origin,
        EconomicActivityKind fromJob, EconomicActivityKind destinationJob,
        ulong workers)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.MovementMode != PhysicalWorkerMovementMode.TimedAdjacent)
            throw new InvalidOperationException(
                "Destination planning requires timed physical movement.");
        // Checking both production rates validates job IDs and workable origin
        // using the same geography that canonical production uses.
        session.PrimitiveYieldAt(origin, fromJob);
        var survey = PhysicalAdjacentWorkSurveyProjection.Survey(
            session, origin, destinationJob);
        if (workers == 0UL)
            throw new InvalidOperationException("Select at least one worker.");
        var available = session.State.Allocations.Where(x =>
                x.TileId == origin && x.Activity == fromJob)
            .Aggregate(0UL, (total, x) => checked(total + x.Workers));
        if (available < workers)
            throw new InvalidOperationException(
                "Insufficient resident workers in the selected source activity.");
        var selected = survey.Neighbours.FirstOrDefault();
        if (selected is null)
            throw new InvalidOperationException("No workable adjacent destination.");
        return new PhysicalWorkerDestinationPlan(
            origin, selected.Tile, fromJob, destinationJob, workers,
            survey.CurrentTileYieldRaw, selected.YieldFor(destinationJob),
            selected.ResidentWorkers, selected.WorkersArriving);
    }
}
