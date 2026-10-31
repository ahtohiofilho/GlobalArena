using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Physical potential of a neighbouring tile, without assuming that moving
/// workers instantly produces resources or creates a city/political controller.
/// Raw rates are in the SAME units as the canonical primitive production phase,
/// per worker and economic settlement (not per movement turn).
/// </summary>
public sealed record PhysicalAdjacentWorkSite(
    PhysicalTacticalTileId Tile,
    long FoodYieldPerWorkerRaw,
    long WoodYieldPerWorkerRaw,
    long StoneYieldPerWorkerRaw,
    ulong ResidentWorkers,
    ulong WorkersArriving)
{
    public long YieldFor(EconomicActivityKind activity) => activity switch
    {
        EconomicActivityKind.Agriculture => FoodYieldPerWorkerRaw,
        EconomicActivityKind.Forestry => WoodYieldPerWorkerRaw,
        EconomicActivityKind.Mining => StoneYieldPerWorkerRaw,
        _ => throw new ArgumentOutOfRangeException(nameof(activity))
    };
}

public sealed record PhysicalAdjacentWorkSurvey(
    PhysicalTacticalTileId Origin,
    EconomicActivityKind ProposedJob,
    long CurrentTileYieldRaw,
    ulong CurrentTileResidents,
    IReadOnlyList<PhysicalAdjacentWorkSite> Neighbours);

/// <summary>
/// Read-only geographical reconnaissance of the actual adjacent workable
/// hex/pentagons. Sorting is deterministic by the chosen productive activity
/// and canonical tile ID. No event, ownership or hypothetical migration.
/// </summary>
public static class PhysicalAdjacentWorkSurveyProjection
{
    public static PhysicalAdjacentWorkSurvey Survey(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId origin, EconomicActivityKind intendedJob)
    {
        ArgumentNullException.ThrowIfNull(session);
        var people = session.WorkersAt(origin); // validates the source tile.
        if (people == 0UL)
            throw new InvalidOperationException(
                "Select a physical tile with resident workers before scouting.");
        var originRate = session.PrimitiveYieldAt(origin, intendedJob);
        var candidates = session.AdjacentWorkable(origin)
            .Select(tile => new PhysicalAdjacentWorkSite(
                tile,
                session.PrimitiveYieldAt(tile, EconomicActivityKind.Agriculture),
                session.PrimitiveYieldAt(tile, EconomicActivityKind.Forestry),
                session.PrimitiveYieldAt(tile, EconomicActivityKind.Mining),
                session.WorkersAt(tile),
                session.State.Transits.Where(x => x.Destination == tile)
                    .Aggregate(0UL, (total, x) => checked(total + x.Workers))))
            .OrderByDescending(x => x.YieldFor(intendedJob))
            .ThenBy(x => x.Tile.FineStrategicCellId.Value)
            .ToArray();

        return new PhysicalAdjacentWorkSurvey(origin, intendedJob, originRate,
            people, Array.AsReadOnly(candidates));
    }
}
