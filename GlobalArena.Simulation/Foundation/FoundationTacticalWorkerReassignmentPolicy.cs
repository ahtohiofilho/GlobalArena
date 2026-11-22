using GlobalArena.Runtime;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// One policy for BOTH the planner and deterministic command/event replay.
/// An occupational order may change activity in the same local, journaled
/// event, but ONLY to enter or leave operational logistics. This avoids a
/// temporary invalid producing-logistics worker and any double work credit.
/// No financial or freight math belongs to this policy.
/// </summary>
internal static class FoundationTacticalWorkerReassignmentPolicy
{
    public static bool IsProductive(PhysicalWorkerOccupation occupation) =>
        occupation is PhysicalWorkerOccupation.Agriculture or
            PhysicalWorkerOccupation.Forestry or
            PhysicalWorkerOccupation.Mining;

    public static bool IsOperational(PhysicalWorkerOccupation occupation) =>
        occupation is PhysicalWorkerOccupation.LandTransport or
            PhysicalWorkerOccupation.PortOperations;

    public static bool IsValidOccupationTransition(
        PhysicalWorkerRuntimeRecord worker,
        PhysicalWorkerActivity nextActivity,
        PhysicalWorkerOccupation nextOccupation,
        bool crossesEdge)
    {
        if (crossesEdge ||
            !worker.Occupation.HasValue ||
            nextOccupation == worker.Occupation.Value ||
            !Enum.IsDefined(nextOccupation))
            return false;

        if (nextActivity == worker.Activity)
            return nextActivity switch
            {
                PhysicalWorkerActivity.Producing => IsProductive(nextOccupation),
                PhysicalWorkerActivity.Other =>
                    IsProductive(nextOccupation) || IsOperational(nextOccupation),
                _ => false
            };

        if (worker.Activity == PhysicalWorkerActivity.Producing &&
            nextActivity == PhysicalWorkerActivity.Other)
            return IsOperational(nextOccupation);

        if (worker.Activity == PhysicalWorkerActivity.Other &&
            nextActivity == PhysicalWorkerActivity.Producing)
            return IsProductive(nextOccupation) &&
                worker.OutputMicroPerTurn > 0UL;

        return false;
    }
}
