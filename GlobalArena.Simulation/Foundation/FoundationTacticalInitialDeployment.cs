using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit v9->v10 materialization policy. The game owner supplies a real
/// location for each existing worker; no source ID is reinterpreted, no
/// population is added, and every tile is checked against canonical incidence.
/// </summary>
public static class FoundationTacticalInitialDeployment
{
    public static WorldState Deploy(
        CanonicalGameSession session, PhysicalTacticalIncidenceMap incidence,
        IReadOnlyDictionary<PhysicalWorkerId, PhysicalTacticalTileId> positions)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(positions);
        var before = session.Snapshot;
        if (!before.Workers.IsInitialized ||
            before.Workers.HasAuthoritativeTacticalLocations ||
            before.Workers.Workers.Count != positions.Count ||
            positions.Keys.Any(id => !id.IsValid) ||
            before.Workers.Workers.Any(worker => !positions.ContainsKey(worker.Id)))
            throw new InvalidOperationException(
                "Initial tactical deployment requires exactly one explicit position " +
                "for every existing canonical v9 worker.");

        var upgraded = new PhysicalWorkerRuntimeState(
            before.Workers.Workers.Select(worker =>
                new PhysicalWorkerRuntimeRecord(
                    worker.Id, worker.Owner, worker.CellId, worker.Commodity,
                    worker.OutputMicroPerTurn, worker.Activity,
                    worker.DemandMultiplierMicro, positions[worker.Id])),
            tacticalLocationsAuthoritative: true);
        // Performs world signature and coarse->fine incidence validation
        // before any immutable snapshot is published.
        var deployed = before.DeployTacticalWorkers(
            upgraded, session.GeneratedWorld, incidence);
        session.AcceptSnapshot(deployed);
        return deployed;
    }
}

/// <summary>
/// Explicit demo scenario only. The chosen fine tiles are actual G(18,0)
/// topology members, with deterministic unique initial placement inside
/// each worker's existing coarse region. Terrain suitability / coast / sea
/// eligibility and production zoning are NOT modeled by this policy.
/// Production worldgen must supply its own physical placement rules.
/// </summary>
public static class FoundationTacticalDemoScenario
{
    public static FoundationTacticalHumanAiTurnHost CreateLocalHumanAiPreview(
        WorldGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // This foundation scenario already has six individually identified
        // workers. AI #4 starts Traveling and resumes productive activity on
        // the first collective turn. No fictitious workers are created.
        var v9 = FoundationHumanAiLocalPreview.CreateLocalPreview(request);
        var session = v9.Session;
        var coarse = session.GeneratedWorld.Request.StrategicParameters;
        if (coarse.M == 0 || coarse.N != 0)
            throw new NotSupportedException(
                "Demo initial placement requires a class-I G(k,0) world.");
        var fine = new GoldbergParameters(checked(coarse.M * 6), 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(coarse, fine));
        var available = map.TileIncidences
            .Where(tile => tile.IncidentCoarseCellIds.Count == 1)
            .GroupBy(tile => tile.IncidentCoarseCellIds[0])
            .ToDictionary(
                group => group.Key,
                group => new Queue<PhysicalTacticalTileId>(
                    group.Select(item => item.PhysicalTacticalTileId)
                        .OrderBy(id => id.FineStrategicCellId.Value)));

        var selected = new Dictionary<PhysicalWorkerId, PhysicalTacticalTileId>();
        foreach (var worker in session.Snapshot.Workers.Workers
                     .OrderBy(worker => worker.Id.Value))
        {
            if (!available.TryGetValue(worker.CellId, out var regionTiles) ||
                regionTiles.Count == 0)
                throw new InvalidOperationException(
                    "Demo has no unused incident fine tile for initial worker.");
            selected.Add(worker.Id, regionTiles.Dequeue());
        }
        FoundationTacticalInitialDeployment.Deploy(session, map, selected);
        return new FoundationTacticalHumanAiTurnHost(
            session, map, new[] { v9.PlayerCivilization },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [new CivilizationId(2UL)] =
                    new FoundationTacticalAiResumeProductionPolicy()
            });
    }
}
