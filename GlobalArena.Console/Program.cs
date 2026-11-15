using System.Globalization;
using GlobalArena.Simulation.Foundation;

// A real executable headless slice of the redesigned foundation. The legacy
// M4 simulator and Godot UI remain untouched, and this is not authoritative.
if (args.Length != 1 || args[0] != "--foundation-slice")
{
    Console.WriteLine("Global Arena — executable foundation preview");
    Console.WriteLine("Run: dotnet run --project GlobalArena.Console -- --foundation-slice");
    return;
}

var world = new PhysicalWorkerTradeCycle(
    new[]
    {
        new FoundationEdge(1, 0, 1, 0.40d, 2.0d, OwnerCivilization: 1,
            ExternalTariff: 0.025d),
        new FoundationEdge(2, 1, 2, 0.40d, 2.0d, OwnerCivilization: 2,
            ExternalTariff: 0.025d)
    },
    new[]
    {
        new FoundationWorker(1, 1, 0, 0, 1d, FoundationWorkerActivity.Producing),
        new FoundationWorker(2, 2, 2, 1, 1d, FoundationWorkerActivity.Producing),
        new FoundationWorker(3, 1, 1, 0, 0d, FoundationWorkerActivity.Military),
        new FoundationWorker(4, 2, 2, 0, 1d, FoundationWorkerActivity.Producing),
        new FoundationWorker(5, 1, 0, 1, 1d, FoundationWorkerActivity.Traveling)
    },
    new Dictionary<int, double> { [0] = 1d, [1] = 1d },
    maximumAdjustmentPasses: 8,
    relativeOpportunityTolerance: 0.001d);

Show(world.Advance());
Show(world.Advance(new[]
{
    new FoundationWorkerOrder(5, NextTile: 1)
}));
Show(world.Advance(new[]
{
    new FoundationWorkerOrder(5, NextActivity: FoundationWorkerActivity.Producing)
}));

static void Show(FoundationTurn turn)
{
    Console.WriteLine(
        $"turn={turn.Turn} workers={turn.Workers.Count} " +
        $"produced={F(turn.TotalProduction)} delivered={F(turn.TotalDelivered)} " +
        $"passes={turn.AdjustmentPasses} opportunity_gap={F(turn.MaxRelativeOpportunity)} " +
        $"certified_F072={turn.CertifiedGlobalEquilibrium.ToString().ToLowerInvariant()}");
    foreach (var (key, demand) in turn.Demand
                 .OrderBy(x => x.Key.Tile).ThenBy(x => x.Key.Commodity))
    {
        var q = turn.Delivered.GetValueOrDefault(key);
        var price = turn.Prices.TryGetValue(key, out var p) ? F(p) : "UNPRICED_Q_ZERO";
        Console.WriteLine($"  tile={key.Tile} item={key.Commodity} demand={F(demand)} " +
                          $"quantity={F(q)} price={price}");
    }
    foreach (var (edge, load) in turn.EdgeLoads.OrderBy(x => x.Key))
        Console.WriteLine($"  edge={edge} physical_load={F(load)}");
    Console.WriteLine("  money=NOT_IMPLEMENTED  military_battles=NOT_IMPLEMENTED");
}

static string F(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
