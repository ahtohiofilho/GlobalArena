using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Read-only availability is explicit: absence, wrong binding, and a valid
/// initialized-but-empty tile are never conflated. No worker or market is
/// created by this inspection, even for uninhabited Goldberg cells.
/// </summary>
public enum StrategicWorkerRuntimeAvailability
{
    NoRuntimeState = 0,
    UnboundWorld = 1,
    WorldMismatch = 2,
    CensusUninitialized = 3,
    Bound = 4
}

public sealed record StrategicPhysicalWorkerFact(
    PhysicalWorkerId Id,
    CivilizationId Owner,
    StrategicCellId CellId,
    CommodityId Commodity,
    ulong OutputMicroPerTurn,
    PhysicalWorkerActivity Activity,
    uint DemandMultiplierMicro);

public sealed class StrategicPhysicalWorkerInspection
{
    public StrategicCellId CellId { get; }
    public StrategicWorkerRuntimeAvailability Availability { get; }
    public ulong? Revision { get; }
    public IReadOnlyList<StrategicPhysicalWorkerFact> Workers { get; }
    public int DemandingWorkerCount => Workers.Count;
    public int ProducingWorkerCount => Workers.Count(w =>
        w.Activity == PhysicalWorkerActivity.Producing && w.OutputMicroPerTurn > 0UL);
    public int TravelingWorkerCount => Workers.Count(w =>
        w.Activity == PhysicalWorkerActivity.Traveling);
    public int MilitaryWorkerCount => Workers.Count(w =>
        w.Activity == PhysicalWorkerActivity.Military);

    internal StrategicPhysicalWorkerInspection(
        StrategicCellId cellId, StrategicWorkerRuntimeAvailability availability,
        ulong? revision, StrategicPhysicalWorkerFact[] workers)
    {
        CellId = cellId;
        Availability = availability;
        Revision = revision;
        Workers = Array.AsReadOnly(workers);
    }
}

/// <summary>
/// Main's strategic selected-tile inspector. It reads ONLY v9 authoritative
/// physical worker records, not the legacy economic-point workforce,
/// experimental C# solver output, tactical fine-cell identities or Godot data.
/// No economic tick, move command, price or simulation state mutation occurs.
/// </summary>
public static class StrategicPhysicalWorkerInspector
{
    public static StrategicPhysicalWorkerInspection Read(
        WorldGenerationResult presentedWorld,
        StrategicCellId selectedCell,
        WorldState? authoritativeState)
    {
        ArgumentNullException.ThrowIfNull(presentedWorld);
        if (!selectedCell.IsValid ||
            !RuntimeWorldBinding.FromGeneratedWorld(presentedWorld).Contains(selectedCell))
            throw new ArgumentOutOfRangeException(nameof(selectedCell),
                "Selected tile must be an actual Goldberg strategic cell.");

        var expected = RuntimeWorldBinding.FromGeneratedWorld(presentedWorld);
        var availability = authoritativeState switch
        {
            null => StrategicWorkerRuntimeAvailability.NoRuntimeState,
            { WorldBinding: null } => StrategicWorkerRuntimeAvailability.UnboundWorld,
            _ when authoritativeState.WorldBinding != expected =>
                StrategicWorkerRuntimeAvailability.WorldMismatch,
            _ when !authoritativeState.Workers.IsInitialized =>
                StrategicWorkerRuntimeAvailability.CensusUninitialized,
            _ => StrategicWorkerRuntimeAvailability.Bound
        };
        if (availability != StrategicWorkerRuntimeAvailability.Bound)
            return new StrategicPhysicalWorkerInspection(selectedCell,
                availability, availability == StrategicWorkerRuntimeAvailability.CensusUninitialized
                    ? authoritativeState!.Revision : null,
                Array.Empty<StrategicPhysicalWorkerFact>());

        var workers = authoritativeState!.Workers.Workers
            .Where(w => w.CellId == selectedCell)
            .OrderBy(w => w.Id.Value)
            .Select(w => new StrategicPhysicalWorkerFact(
                w.Id, w.Owner, w.CellId, w.Commodity, w.OutputMicroPerTurn,
                w.Activity, w.DemandMultiplierMicro))
            .ToArray();

        return new StrategicPhysicalWorkerInspection(
            selectedCell, availability, authoritativeState.Revision, workers);
    }
}
