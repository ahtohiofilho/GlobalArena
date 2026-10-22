using GlobalArena.World;

namespace GlobalArena.Runtime;

// R35: inspection reads a provided WorldState; it never instantiates, advances or mutates it.
public enum TacticalRuntimeAvailability
{
    NoRuntimeState,
    UnboundWorld,
    WorldMismatch,
    Bound
}

public sealed record TacticalTileControlFact(
    StrategicCellId StrategicCellId,
    CivilizationId? Controller);

public sealed class TacticalTileRuntimeInspection
{
    public PhysicalTacticalTileId TileId { get; }
    public TacticalRuntimeAvailability Availability { get; }
    public ulong? Revision { get; }
    public IReadOnlyList<TacticalTileControlFact> IncidentControls { get; }

    internal TacticalTileRuntimeInspection(
        PhysicalTacticalTileId tileId,
        TacticalRuntimeAvailability availability,
        ulong? revision,
        TacticalTileControlFact[] facts)
    {
        TileId = tileId;
        Availability = availability;
        Revision = revision;
        IncidentControls = Array.AsReadOnly(facts);
    }
}

public static class TacticalTileRuntimeInspector
{
    public static TacticalTileRuntimeInspection Read(
        PhysicalTacticalIncidenceMap map,
        WorldGenerationResult presentedWorld,
        PhysicalTacticalTileId tileId,
        WorldState? runtimeState)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(presentedWorld);
        if (!tileId.IsValid ||
            tileId.FineGoldbergParameters != map.Refinement.FineParameters)
            throw new ArgumentException("Tile identity is not part of the canonical fine topology.", nameof(tileId));
        if (presentedWorld.Request.StrategicParameters != map.Refinement.CoarseParameters ||
            presentedWorld.StrategicTopology.Cells.Count != map.CoarseTopology.Cells.Count)
            throw new ArgumentException("Presented world and physical incidence map use different strategic topologies.", nameof(presentedWorld));

        var index = checked((int)tileId.FineStrategicCellId.Value - 1);
        if (index < 0 || index >= map.TileIncidences.Count ||
            map.TileIncidences[index].PhysicalTacticalTileId != tileId)
            throw new ArgumentOutOfRangeException(nameof(tileId), "Canonical tile identity is not present in the incidence map.");

        var incidence = map.TileIncidences[index].IncidentCoarseCellIds;
        var availability = TacticalRuntimeAvailability.NoRuntimeState;
        if (runtimeState is not null)
        {
            if (!runtimeState.IsWorldBound)
                availability = TacticalRuntimeAvailability.UnboundWorld;
            else if (runtimeState.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(presentedWorld))
                availability = TacticalRuntimeAvailability.WorldMismatch;
            else
                availability = TacticalRuntimeAvailability.Bound;
        }

        var facts = new TacticalTileControlFact[incidence.Count];
        for (int i = 0; i < incidence.Count; i++)
        {
            CivilizationId? controller = null;
            if (availability == TacticalRuntimeAvailability.Bound &&
                runtimeState!.Territory.TryGetController(incidence[i], out var found))
                controller = found;
            facts[i] = new TacticalTileControlFact(incidence[i], controller);
        }
        return new TacticalTileRuntimeInspection(
            tileId, availability,
            availability == TacticalRuntimeAvailability.Bound ? runtimeState!.Revision : null,
            facts);
    }
}
