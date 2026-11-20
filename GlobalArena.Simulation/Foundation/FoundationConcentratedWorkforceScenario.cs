using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit demonstration of the originally approved concentrated opening:
/// each civilization's individual physical workers start together in ONE
/// canonical G(18,0) tile. The six-per-civilization default is a balance
/// calibration parameter, not a permanent civilization/worker rule.
/// Never recycle strategic CellId as physical fine tile position.
/// </summary>
public static class FoundationConcentratedWorkforceScenario
{
    public const int DefaultWorkersPerCivilization = 6;

    public static FoundationTacticalHumanAiTurnHost CreatePreview(
        WorldGenerationRequest request,
        int workersPerCivilization = DefaultWorkersPerCivilization,
        GoldbergScaledRefinement? startupRefinement = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Demo-size guard only; the production engine has no six-worker cap.
        if (workersPerCivilization < 1 || workersPerCivilization > 120)
            throw new ArgumentOutOfRangeException(nameof(workersPerCivilization),
                "The preview accepts 1–120 workers per civilization.");

        var session = CanonicalGameSession.Start(request);
        var graph = session.GeneratedWorld.StrategicSurfaceGraph;
        if (graph.NodeCount < 4)
            throw new ArgumentException("Concentrated preview needs 4+ coarse nodes.");
        var coarse = request.StrategicParameters;
        if (coarse.M <= 0 || coarse.N != 0)
            throw new NotSupportedException(
                "This concentrated preview uses class-I scaled Goldberg worlds.");
        // R105: if a startup refinement is supplied, it is authoritative
        // for both physical workers and the economic host. Do not silently
        // recreate a sixfold grid divergent from the visual contract.
        var refinement = startupRefinement ??
            new GoldbergScaledRefinement(
                coarse, new GoldbergParameters(checked(coarse.M * 6), 0));
        if (refinement.CoarseParameters != coarse)
            throw new ArgumentException(
                "Concentrated workforce refinement must match the world request.",
                nameof(startupRefinement));
        var incidence = PhysicalTacticalIncidenceMapper.Materialize(refinement);
        var human = new CivilizationId(1UL);
        var ai = new CivilizationId(2UL);
        var humanHome = graph.GetCellId(0);
        var aiHome = graph.GetCellId(graph.NodeCount / 2);
        var civilizationRoster = new CivilizationRuntimeState(new[]
        {
            new CivilizationRuntimeRecord(human, humanHome),
            new CivilizationRuntimeRecord(ai, aiHome)
        });

        var initial = new List<PhysicalWorkerRuntimeRecord>(
            checked(workersPerCivilization * 2));
        var roles = new Dictionary<PhysicalWorkerId, PhysicalWorkerOccupation>(
            checked(workersPerCivilization * 2));
        for (var civIndex = 0; civIndex < 2; civIndex++)
        {
            var civ = civIndex == 0 ? human : ai;
            var home = civIndex == 0 ? humanHome : aiHome;
            for (var i = 0; i < workersPerCivilization; i++)
            {
                var id = new PhysicalWorkerId(
                    checked((ulong)(civIndex * workersPerCivilization + i + 1)));
                var role = (i % 3) switch
                {
                    0 => PhysicalWorkerOccupation.Agriculture,
                    1 => PhysicalWorkerOccupation.Forestry,
                    _ => PhysicalWorkerOccupation.Mining
                };
                // Commodity IDs are only valid existing demonstration
                // placeholders; role is independently authored. No invented
                // commodity-name mapping or market demand is implied.
                var commodity = new CommodityId(i % 2 == 0 ? 1U : 2U);
                var status = civIndex == 1 && i == 0
                    ? PhysicalWorkerActivity.Traveling
                    : PhysicalWorkerActivity.Producing;
                initial.Add(new PhysicalWorkerRuntimeRecord(
                    id, civ, home, commodity, 1_000_000UL, status));
                roles.Add(id, role);
            }
        }
        session.AcceptSnapshot(session.Snapshot
            .WithCivilizations(civilizationRoster)
            .WithWorkers(new PhysicalWorkerRuntimeState(initial)));

        static PhysicalTacticalTileId GetDemoHome(
            PhysicalTacticalIncidenceMap map, StrategicCellId coarseHome) =>
            map.TileIncidences
                .Where(tile => tile.IncidentCoarseCellIds.Count == 1 &&
                    tile.IncidentCoarseCellIds[0] == coarseHome)
                .OrderBy(tile => tile.PhysicalTacticalTileId.FineStrategicCellId.Value)
                .Select(tile => tile.PhysicalTacticalTileId)
                .FirstOrDefault() is { IsValid: true } location
                    ? location
                    : throw new InvalidOperationException(
                        "No real interior physical tile for the demo capital.");

        var humanPhysicalHome = GetDemoHome(incidence, humanHome);
        var aiPhysicalHome = GetDemoHome(incidence, aiHome);
        if (humanPhysicalHome == aiPhysicalHome)
            throw new InvalidOperationException(
                "Distinct civilization capital regions share the same fine tile.");

        var positions = initial.ToDictionary(
            worker => worker.Id,
            worker => worker.Owner == human ? humanPhysicalHome : aiPhysicalHome);
        FoundationTacticalInitialDeployment.Deploy(session, incidence, positions);
        session.AcceptSnapshot(session.Snapshot
            .ClassifyTacticalWorkerOccupations(roles));

        return new FoundationTacticalHumanAiTurnHost(
            session, incidence, new[] { human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [ai] = new FoundationTacticalAiResumeProductionPolicy()
            });
    }
}
