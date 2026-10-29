using System.Globalization;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

/// <summary>
/// View data for physical fine-tile workforce. Coarse cells are read-only
/// buckets for zoom/summary, NEVER locations of workers, settlements or cities.
/// </summary>
internal static class GaVs03TacticalBridge
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static int Run(string[] args)
    {
        try
        {
            if (args.Length < 2 || args.Length > 67 ||
                !ulong.TryParse(args[0], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var seed) ||
                !ulong.TryParse(args[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var focus) ||
                focus == 0)
                throw new ArgumentException(
                    "Usage: tactical-bridge <seed> <coarse-focus> [m:from:to:fromJob:toJob:count|p ...].");

            var session = new EconomicPhysicalTacticalWorkerSession(seed);
            foreach (var order in args.Skip(2))
            {
                if (order == "p")
                {
                    session.AdvancePreview();
                    continue;
                }

                var fields = order.Split(':');
                if (fields.Length != 6 || fields[0] != "m" ||
                    !ulong.TryParse(fields[1], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var source) ||
                    !ulong.TryParse(fields[2], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var destination) ||
                    !byte.TryParse(fields[3], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var fromJob) ||
                    !byte.TryParse(fields[4], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var toJob) ||
                    !ulong.TryParse(fields[5], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var count) ||
                    source == 0 || destination == 0)
                    throw new ArgumentException("Malformed physical worker relocation order.");
                session.MoveWorkers(
                    session.PhysicalId(new StrategicCellId(source)),
                    session.PhysicalId(new StrategicCellId(destination)),
                    (EconomicActivityKind)fromJob,
                    (EconomicActivityKind)toJob, count);
            }

            if (focus > (ulong)session.Incidence.CoarseTopology.Cells.Count)
                throw new ArgumentOutOfRangeException(nameof(focus),
                    "Unknown strategic view focus.");
            var focusId = new StrategicCellId(focus);
            var summary = session.StrategicDisplayTotals();
            var coarseGeometry = session.CoarseWorld.StrategicSphericalGeometry;
            var coarse = session.CoarseWorld.StrategicTopology.Cells
                .Select(cell => new
                {
                    Id = cell.Id.Value,
                    Center = Vector(coarseGeometry.GetCell(cell.Id).Center),
                    Boundary = coarseGeometry.GetCell(cell.Id).BoundaryVertexIds
                        .Select(vertex => Vector(coarseGeometry.GetVertex(vertex).Position)).ToArray(),
                    Land = session.CoarseWorld.StrategicPhysicalFields.LandWater
                        .GetKind(cell.Id) == StrategicLandWaterKind.Land,
                    Biome = session.CoarseWorld.StrategicBiomes.GetKind(cell.Id).ToString(),
                    Workers = summary.TryGetValue(cell.Id, out var count) ? count : 0UL
                }).ToArray();

            // This is a localized projection of the one global fine topology.
            // It never creates a region-owned copy of an edge/vertex tile.
            var visibleIds = PhysicalTacticalViewportQuery.SelectTiles(
                session.Incidence, focusId, 1);
            var fineGeometry = session.FineWorld.StrategicSphericalGeometry;
            var physical = visibleIds.Select(id =>
            {
                var cid = id.FineStrategicCellId;
                var geo = fineGeometry.GetCell(cid);
                return new
                {
                    Id = cid.Value,
                    Center = Vector(geo.Center),
                    Boundary = geo.BoundaryVertexIds
                        .Select(v => Vector(fineGeometry.GetVertex(v).Position)).ToArray(),
                    Land = session.FineWorld.StrategicPhysicalFields.LandWater
                        .GetKind(cid) == StrategicLandWaterKind.Land,
                    Biome = session.FineWorld.StrategicBiomes.GetKind(cid).ToString(),
                    Workers = session.WorkersAt(id),
                    Workable = session.IsWorkableLand(cid),
                    Incidents = session.Incidents(id).Select(x => x.Value).ToArray(),
                    DisplayBucket = session.DisplayBucket(id).Value,
                    Adjacent = session.AdjacentWorkable(id).Select(x =>
                        x.FineStrategicCellId.Value).ToArray()
                };
            }).ToArray();

            var flows = session.Preview();
            var result = new
            {
                Schema = "GA-VS03-PHYSICAL-1",
                Seed = seed,
                Focus = focus,
                Revision = session.Revision,
                Previews = session.PreviewCount,
                InitialTile = session.InitialTile.FineStrategicCellId.Value,
                FineGoldberg = new[]
                {
                    EconomicPhysicalTacticalWorkerSession.FineParameters.M,
                    EconomicPhysicalTacticalWorkerSession.FineParameters.N
                },
                GlobalFineCount = session.Incidence.TileIncidences.Count,
                TotalWorkers = session.TotalWorkers,
                OccupiedPhysicalTiles = session.Assignments.Select(x =>
                    x.TileId).Distinct().Count(),
                Coarse = coarse,
                Physical = physical,
                Assignments = session.Assignments.Select(x => new
                {
                    Tile = x.TileId.FineStrategicCellId.Value,
                    Activity = (byte)x.Activity,
                    Workers = x.Workers
                }).ToArray(),
                Supply = flows.Supply.Select(x => new
                {
                    Tile = x.TileId.FineStrategicCellId.Value,
                    Commodity = x.CommodityId.Value,
                    Activity = (byte)x.Activity,
                    Raw = x.RawQuantity
                }).ToArray(),
                Demand = flows.Demand.Select(x => new
                {
                    Tile = x.TileId.FineStrategicCellId.Value,
                    Commodity = x.CommodityId.Value,
                    Raw = x.RawQuantity
                }).ToArray(),
                Warning = "In-memory physical tactical workers; strategic view is read-only aggregation; no cities, canonical turn or saves"
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(result, Json));
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or
            InvalidOperationException or OverflowException or
            NotSupportedException)
        {
            Console.Error.WriteLine("GA-VS03 order rejected: " + ex.Message);
            return 2;
        }
    }

    private static double[] Vector(SphericalPoint3 p) => new[] { p.X, p.Y, p.Z };
}
