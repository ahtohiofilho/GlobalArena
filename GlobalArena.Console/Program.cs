using System.Globalization;
using GlobalArena.Simulation.Foundation;

// A real executable headless slice of the redesigned foundation. The legacy
// M4 simulator and Godot UI remain untouched, and this is not authoritative.
if (args.Length != 1 ||
    (args[0] != "--foundation-slice" &&
     args[0] != "--foundation-calibration-ab"))
{
    Console.WriteLine("Global Arena — executable foundation preview");
    Console.WriteLine("Run: dotnet run --project GlobalArena.Console -- --foundation-slice");
    Console.WriteLine("A/B: dotnet run --project GlobalArena.Console -- --foundation-calibration-ab");
    return;
}

// A single controlled experiment, identical inputs and solver budget.
// "Fake" calibration creates ONLY the reference trade allocation; it never
// publishes a game turn. Its result is not assumed to be more accurate.
if (args[0] == "--foundation-calibration-ab")
{
    var control = CreateWorld();
    var controlTurn = control.Advance();

    var candidate = CreateWorld();
    var preview = candidate.CalibrateInitialMarket();
    if (candidate.CurrentTurn != 0)
        throw new InvalidOperationException("Calibration incorrectly advanced time.");
    var candidateTurn = candidate.Advance();

    if (controlTurn.Turn != 1 || candidateTurn.Turn != 1 ||
        Math.Abs(controlTurn.TotalProduction - candidateTurn.TotalProduction) > 1e-10 ||
        Math.Abs(controlTurn.TotalDelivered - candidateTurn.TotalDelivered) > 1e-10 ||
        !controlTurn.Workers.SequenceEqual(candidateTurn.Workers) ||
        !controlTurn.Demand.OrderBy(x => x.Key.Tile).ThenBy(x => x.Key.Commodity)
            .SequenceEqual(candidateTurn.Demand.OrderBy(x => x.Key.Tile)
                .ThenBy(x => x.Key.Commodity)))
        throw new InvalidOperationException("Calibration changed physical turn invariants.");

    Console.WriteLine("GA_FOUNDATION_CALIBRATION_AB_V1 same_workers=true " +
        "same_demand=true same_production=true same_time=true");
    Console.WriteLine($"  cold_turn=1 passes={controlTurn.AdjustmentPasses} " +
        $"opportunity_gap={F(controlTurn.MaxRelativeOpportunity)}");
    Console.WriteLine($"  fake_turn=0 passes={preview.AdjustmentPasses} " +
        $"opportunity_gap={F(preview.MaxRelativeOpportunity)}");
    Console.WriteLine($"  calibrated_real_turn=1 passes={candidateTurn.AdjustmentPasses} " +
        $"opportunity_gap={F(candidateTurn.MaxRelativeOpportunity)}");
    Console.WriteLine($"  delta_gap={F(candidateTurn.MaxRelativeOpportunity -
        controlTurn.MaxRelativeOpportunity)} " +
        "certified_F072=false production_adoption=false");
    return;
}

var world = CreateWorld();

static PhysicalWorkerTradeCycle CreateWorld() => new(
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
    maximumAdjustmentPasses: 128,
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
