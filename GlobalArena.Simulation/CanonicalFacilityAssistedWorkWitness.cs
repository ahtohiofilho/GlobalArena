using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A real part of the output of an executed economic closing tick, attributed
/// to existing people explicitly assigned to an operational primitive shelter.
/// The same quantity is already part of canonical worker production, never
/// additional to it. No new yield coefficient has been authorized.
/// </summary>
public sealed record CanonicalFacilityAssistedWorkRow(
    int FacilityAssemblyOrdinal, ulong FineTile, EconomicActivityKind Activity,
    uint Commodity, ulong OperatingWorkers, ulong AssistedProducedRaw);

/// <summary>
/// Replay-derived, independently reconcilable classification of REAL output
/// into facility-assisted and ordinary primitive work. Not a new inventory,
/// separate commodity, monetary receipt, or further production event.
/// </summary>
public sealed class CanonicalFacilityAssistedWorkWitness
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalExecutedPhysicalCycleProvenance _cycle;
    private readonly CanonicalFacilityAssistedWorkRow[] _rows;
    private readonly ulong _workerRevision;
    private readonly string _hash;
    private readonly int _eventCount;

    public ulong CycleOrdinal => _cycle.CycleOrdinal;
    public ulong ClosingMovementTurn => _cycle.ClosingMovementTurn;
    public IReadOnlyList<CanonicalFacilityAssistedWorkRow> Assisted =>
        Array.AsReadOnly(_rows);
    public ulong TotalAssistedProducedRaw =>
        _rows.Aggregate(0UL, (sum, x) =>
            checked(sum + x.AssistedProducedRaw));
    public bool IsActuallyExecutedOutput => true;
    public bool CreatesAdditionalProduction => false;
    public bool AuthorizesFacilityYieldMultiplier => false;
    public bool CanAuthorizeRealPurchasingPower => false;

    internal CanonicalFacilityAssistedWorkWitness(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedPhysicalCycleProvenance cycle,
        IEnumerable<CanonicalFacilityAssistedWorkRow> contributions)
    {
        _session = session;
        _cycle = cycle;
        _rows = contributions.OrderBy(x => x.FineTile)
            .ThenBy(x => (byte)x.Activity).ToArray();
        _workerRevision = session.State.WorkerRevision;
        _hash = session.CurrentHash.HexDigest;
        _eventCount = session.Events.Count;
        VerifyIndependent();
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(_session, session) ||
            session.State.WorkerRevision != _workerRevision ||
            session.CurrentHash.HexDigest != _hash ||
            session.Events.Count != _eventCount)
            throw new InvalidOperationException(
                "Facility work receipt is stale or belongs to a different canonical session.");
        _cycle.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var production = _cycle.Production
            .Where(x => x.Channel == CanonicalPhysicalProductionChannel.AssignedWorker)
            .ToDictionary(x => (x.SourceFineTile, x.Commodity, x.Activity),
                x => x.ProducedRaw);
        var facilities = _session.State.PrimitiveFacilities
            .Where(x => x.IsOperational && x.OperatingWorkers > 0UL)
            .ToDictionary(x => (x.Tile.FineStrategicCellId.Value, x.Activity), x => x);
        var seen = new HashSet<(ulong, EconomicActivityKind)>();
        foreach (var row in _rows)
        {
            var key = (row.FineTile, row.Activity);
            if (row.FacilityAssemblyOrdinal <= 0 ||
                row.OperatingWorkers == 0UL || row.AssistedProducedRaw == 0UL ||
                !seen.Add(key) ||
                !facilities.TryGetValue(key, out var site) ||
                site.AssemblyActionOrdinal != row.FacilityAssemblyOrdinal ||
                site.OperatingWorkers != row.OperatingWorkers ||
                !production.TryGetValue((row.FineTile, row.Commodity, row.Activity),
                    out var realOutput) ||
                row.AssistedProducedRaw > realOutput)
                throw new InvalidOperationException(
                    "Facility attribution exceeds actual production or its staffed capacity.");
            var active = _session.State.Allocations.FirstOrDefault(x =>
                x.TileId == site.Tile && x.Activity == site.Activity)?.Workers ?? 0UL;
            if (active < row.OperatingWorkers)
                throw new InvalidOperationException(
                    "Facility attribution references phantom workers.");
            var perWorkerRaw = (ulong)_session.PrimitiveYieldAt(
                site.Tile, site.Activity);
            if ((BigInteger)perWorkerRaw * row.OperatingWorkers !=
                row.AssistedProducedRaw)
                throw new InvalidOperationException(
                    "Facility output must equal existing primitive yield, without uplift.");
        }
        // At most one facility per tile/activity. All active facilities with
        // positive real primitive yield MUST have a corresponding receipt.
        if (facilities.Values.Any(site =>
            _session.PrimitiveYieldAt(site.Tile, site.Activity) > 0 &&
            !_rows.Any(x => x.FacilityAssemblyOrdinal == site.AssemblyActionOrdinal)))
            throw new InvalidOperationException(
                "An operated productive shelter is missing its executed output attribution.");
        if (TotalAssistedProducedRaw > _cycle.ActuallyProducedRaw)
            throw new InvalidOperationException(
                "Facility output would be double counted relative to real physical production.");
        RequireCurrent(_session);
    }
}
