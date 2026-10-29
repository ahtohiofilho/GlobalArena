using System.Globalization;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

/// <summary>
/// Versioned, stateless, read-only JSON viewport protocol for Godot. All
/// gameplay orders are recomputed by the actual C# engine on every request.
/// This avoids having two authorities or inventing a GDScript simulation.
/// This is deliberately a short-lived offline development bridge, NOT an
/// authenticated or durable save protocol.
/// </summary>
internal static class GaVs02GodotBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static int Run(string[] args)
    {
        try
        {
            if (args.Length is < 1 or > 65 ||
                !ulong.TryParse(args[0], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var seed))
                throw new ArgumentException(
                    "Expected godot-bridge <seed> [f:<source>:<tile>:<workers>|p ...].");
            var session = new EconomicNeolithicInteractionSession(seed);
            foreach (var command in args.Skip(1))
            {
                if (command == "p")
                {
                    session.AdvancePreview();
                    continue;
                }
                var fields = command.Split(':');
                if (fields.Length != 4 || fields[0] != "f" ||
                    !ulong.TryParse(fields[1], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var source) ||
                    !ulong.TryParse(fields[2], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var target) ||
                    !ulong.TryParse(fields[3], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var workers) ||
                    source == 0 || target == 0 || workers == 0)
                    throw new ArgumentException("Invalid founding command.");
                session.Found(new EconomicPointId(source),
                    new StrategicCellId(target), workers);
            }

            var world = session.CurrentWorld;
            var byCell = world.Economy.EconomicPoints.ToDictionary(
                point => point.StrategicCellId);
            var frontiers = world.Economy.EconomicPoints.Select(point => new
            {
                SourcePointId = point.Id.Value,
                TargetCellIds = session.AvailableLandNeighbors(point.Id)
                    .Select(id => id.Value).ToArray()
            }).ToArray();
            var foundable = frontiers.SelectMany(x => x.TargetCellIds)
                .ToHashSet();

            // All positions originate from the canonical Goldberg spherical
            // geometry, not a separately approximated drawing in GDScript.
            var geometry = session.GeneratedWorld.StrategicSphericalGeometry;
            var land = session.GeneratedWorld.StrategicPhysicalFields.LandWater;
            var biomes = session.GeneratedWorld.StrategicBiomes;
            var cells = session.GeneratedWorld.StrategicTopology.Cells
                .Select(cell =>
                {
                    var geom = geometry.GetCell(cell.Id);
                    byCell.TryGetValue(cell.Id, out var point);
                    return new
                    {
                        Id = cell.Id.Value,
                        Land = land.GetKind(cell.Id) == StrategicLandWaterKind.Land,
                        Biome = biomes.GetKind(cell.Id).ToString(),
                        Center = Coordinates(geom.Center),
                        Boundary = geom.BoundaryVertexIds.Select(id =>
                            Coordinates(geometry.GetVertex(id).Position)).ToArray(),
                        Neighbors = cell.AdjacentCellIds.Select(id => id.Value).ToArray(),
                        PointId = point?.Id.Value ?? 0UL,
                        Workers = point?.Workforce ?? 0UL,
                        Owner = point?.Owner.Value ?? 0UL,
                        Foundable = foundable.Contains(cell.Id.Value)
                    };
                }).ToArray();

            var result = session.PreviewGeographicEconomy();
            var supply = result.SupplyFlows.Select(x => new
            {
                PointId = x.EconomicPointId.Value,
                CommodityId = x.CommodityId.Value,
                Activity = x.Activity.ToString(),
                RawQuantity = x.RawQuantity
            }).ToArray();
            var demand = result.DemandFlows.Select(x => new
            {
                PointId = x.EconomicPointId.Value,
                CommodityId = x.CommodityId.Value,
                RawQuantity = x.RawQuantity
            }).ToArray();
            var snapshot = new
            {
                Schema = "GA-VS02-DEV-1",
                Seed = seed,
                WorldRevision = world.Revision,
                PreviewTurns = session.CompletedPreviewTurns,
                CellCount = cells.Length,
                Cells = cells,
                Frontiers = frontiers,
                Supply = supply,
                Demand = demand,
                TotalWorkers = world.Economy.EconomicPoints.Aggregate(
                    0UL, (sum, p) => checked(sum + p.Workforce)),
                SettlementCount = world.Economy.EconomicPoints.Count,
                Warning = "Development bridge: no canonical turns, market settlement, save or combat"
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(snapshot, JsonOptions));
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or
            InvalidOperationException or OverflowException)
        {
            // Machine-readable refusal. Never expose stack traces or
            // silently accept partially applied command histories.
            Console.Error.WriteLine("GA-VS02 bridge rejected: " + ex.Message);
            return 2;
        }
    }

    private static double[] Coordinates(SphericalPoint3 p) =>
        new[] { p.X, p.Y, p.Z };
}
