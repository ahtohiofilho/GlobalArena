using System.Globalization;
using GlobalArena.Simulation.Foundation;
using GlobalArena.Runtime;
using GlobalArena.World;
using GlobalArena.Kernel;
using GlobalArena.Simulation;

// A real executable headless slice of the redesigned foundation. The legacy
// M4 simulator and Godot UI remain untouched, and this is not authoritative.
if (args.Length != 1 ||
    (args[0] != "--foundation-slice" &&
     args[0] != "--foundation-calibration-ab" &&
     args[0] != "--foundation-goldberg" &&
     args[0] != "--foundation-worker-journal"))
{
    Console.WriteLine("Global Arena — executable foundation preview");
    Console.WriteLine("Run: dotnet run --project GlobalArena.Console -- --foundation-slice");
    Console.WriteLine("A/B: dotnet run --project GlobalArena.Console -- --foundation-calibration-ab");
    Console.WriteLine("Goldberg: dotnet run --project GlobalArena.Console -- --foundation-goldberg");
    Console.WriteLine("Authority: dotnet run --project GlobalArena.Console -- --foundation-worker-journal");
    return;
}

// Authoritative worker lifecycle: v9 physical census, real Goldberg move,
// domain event, turn commit, replay SHA256 equality and read-only economy.
if (args[0] == "--foundation-worker-journal")
{
    var canonical = CanonicalGameSession.Start(new WorldGenerationRequest(
        new WorldSeed(20261115UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0)));
    var graph = canonical.GeneratedWorld.StrategicSurfaceGraph;
    var start = graph.GetCellId(0);
    var neighbor = graph.GetCellId(graph.GetNeighborIndexes(0)[0]);
    var remote = graph.GetCellId(graph.NodeCount / 2);

    canonical.AcceptSnapshot(canonical.Snapshot
        .WithCivilizations(new CivilizationRuntimeState(new[]
        {
            new CivilizationId(1UL), new CivilizationId(2UL)
        }))
        .WithWorkers(new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                new CivilizationId(1UL), start, new CommodityId(1U),
                2_000_000UL, PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                new CivilizationId(2UL), remote, new CommodityId(2U),
                2_000_000UL, PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(3UL),
                new CivilizationId(1UL), neighbor, new CommodityId(1U),
                0UL, PhysicalWorkerActivity.Military)
        })));
    var before = canonical.Snapshot;
    var hasher = new CanonicalWorldStateHasher();
    var hashBefore = hasher.Compute(before);
    if (hashBefore.FormatVersion != 9U)
        throw new InvalidOperationException("Worker-initialized state must use v9 hash.");

    var turn = new TurnNumber(1UL);
    var context = new SimulationContext(turn, new SimulationSeed(712UL));
    var movement = new PhysicalWorkerMoveCommand(
        new CommandId(turn, 1UL), new CivilizationId(1UL),
        new PhysicalWorkerId(1UL), neighbor);
    var phase = new PhysicalWorkerMovementTurnPhase(canonical.GeneratedWorld);
    var gate = new AuthoritativeTurnCommitGate(canonical,
        new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(),
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { phase })),
        firstTurn: turn, rulesetVersion: 1U);
    var committed = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
        new TurnPolicyInput(turn, true, false),
        new TurnResolutionInput(before,
            new ISimulationCommand[] { movement }, context)));
    if (!committed.WasResolved ||
        committed.ResolutionResult is not { } result)
        throw new InvalidOperationException("Canonical movement turn was not committed.");

    var replayed = new SimulationEventLogReplayer(
        new PhysicalWorkerMoveEventExecutor(canonical.GeneratedWorld))
        .Replay(new SimulationReplayInput(before, result.EventLog, context));
    var finalHash = hasher.Compute(canonical.Snapshot);
    if (hasher.Compute(replayed) != finalHash ||
        finalHash.FormatVersion != 9U ||
        canonical.Snapshot.Revision != 1UL ||
        canonical.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
            .CellId != neighbor ||
        gate.NextTurn != new TurnNumber(2UL))
        throw new InvalidOperationException(
            "Canonical physical-worker movement journal or replay drift.");

    var economy = GoldbergFoundationSession.FromAuthoritativeWorkers(
        canonical, new Dictionary<int, double> { [1] = 1d, [2] = 0.5d },
        maximumAdjustmentPasses: 5);
    var preview = economy.Advance();
    if (preview.TotalProduction != 2d ||
        Math.Abs(preview.TotalDelivered - 2d) > 1e-9 ||
        economy.InspectTile(neighbor).Workers.Count != 2)
        throw new InvalidOperationException(
            "Read-only economic preview disagrees with canonical worker census.");

    Console.WriteLine($"GA_FOUNDATION_WORKER_AUTHORITY_V9 nodes={graph.NodeCount} " +
        $"edges={canonical.GeneratedWorld.StrategicTopology.Edges.Count} " +
        $"before_revision={before.Revision} after_revision={canonical.Snapshot.Revision} " +
        $"events={result.EventLog.Entries.Count} replay_hash_match=true " +
        $"hash_version={finalHash.FormatVersion}");
    Console.WriteLine($"  worker=1 from={start.Value} to={neighbor.Value} " +
        $"activity={canonical.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1).Activity} " +
        $"remaining_workers={canonical.Snapshot.Workers.Workers.Count}");
    Console.WriteLine($"  preview_production={F(preview.TotalProduction)} " +
        $"preview_delivered={F(preview.TotalDelivered)} " +
        $"preview_opportunity_gap={F(preview.MaxRelativeOpportunity)} " +
        "preview_authoritative=false certified_F072=false");
    return;
}

// First actual generated-world integration: use EXISTING canonical sphere nodes,
// edges and world binding with actual physical workers and movement orders.
// The prototype deliberately does not publish into authoritative WorldState.
if (args[0] == "--foundation-goldberg")
{
    var canonical = CanonicalGameSession.Start(new WorldGenerationRequest(
        new WorldSeed(20261115UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0)));
    var graph = canonical.GeneratedWorld.StrategicSurfaceGraph;
    var c0 = checked((int)graph.GetCellId(0).Value);
    var c1Index = graph.GetNeighborIndexes(0)[0];
    var c1 = checked((int)graph.GetCellId(c1Index).Value);
    var c2Index = graph.GetNeighborIndexes(c1Index)
        .First(index => index != 0);
    var c2 = checked((int)graph.GetCellId(c2Index).Value);
    var remote = checked((int)graph.GetCellId(graph.NodeCount / 2).Value);

    var economy = new GoldbergFoundationSession(canonical,
        new[]
        {
            new FoundationWorker(1, 1, c0, 0, 2d,
                FoundationWorkerActivity.Producing),
            new FoundationWorker(2, 2, remote, 1, 2d,
                FoundationWorkerActivity.Producing),
            new FoundationWorker(3, 1, c1, 0, 0d,
                FoundationWorkerActivity.Military),
            new FoundationWorker(4, 2, c2, 1, 1d,
                FoundationWorkerActivity.Producing),
            new FoundationWorker(5, 1, c0, 1, 1d,
                FoundationWorkerActivity.Traveling),
            new FoundationWorker(6, 2, remote, 0, 1d,
                FoundationWorkerActivity.Producing)
        },
        new Dictionary<int, double> { [0] = 1d, [1] = 0.7d },
        maximumAdjustmentPasses: 8);

    Console.WriteLine($"GA_FOUNDATION_GOLDBERG_V1 " +
        $"nodes={economy.WorldNodeCount} edges={economy.WorldEdgeCount} " +
        $"world_sha256={economy.WorldBinding.WorldSignatureSha256Hex} " +
        "all_tiles_same_laws=true authoritative=false");
    ShowGoldberg(economy.Advance());
    ShowGoldberg(economy.Advance(new[]
    {
        new FoundationWorkerOrder(5, NextTile: c1)
    }));
    ShowGoldberg(economy.Advance(new[]
    {
        new FoundationWorkerOrder(5, NextActivity: FoundationWorkerActivity.Producing)
    }));
    var inspected = economy.InspectTile(new StrategicCellId(checked((ulong)c1)));
    Console.WriteLine($"  selected_canonical_tile={inspected.CellId.Value} " +
        $"workers={inspected.Workers.Count} " +
        $"demanded_commodities={inspected.Demand.Count} " +
        $"priced_commodities={inspected.Prices.Count}");
    Console.WriteLine("  worldstate=UNCHANGED financial_settlement=NOT_IMPLEMENTED " +
        "battles=NOT_IMPLEMENTED certified_F072=false");
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

static void ShowGoldberg(FoundationTurn turn)
{
    var allDemandTiles = turn.Demand.Keys.Select(x => x.Tile).Distinct().Count();
    var producingTiles = turn.Workers
        .Where(x => x.Activity == FoundationWorkerActivity.Producing &&
            x.OutputPerTurn > 0d)
        .Select(x => x.Tile).Distinct().Count();
    Console.WriteLine($"  goldberg_turn={turn.Turn} " +
        $"workers={turn.Workers.Count} producing_tiles={producingTiles} " +
        $"demand_tiles={allDemandTiles} " +
        $"produced={F(turn.TotalProduction)} delivered={F(turn.TotalDelivered)} " +
        $"trades={turn.Trades.Count} loaded_edges={turn.EdgeLoads.Count} " +
        $"passes={turn.AdjustmentPasses} opportunity_gap={F(turn.MaxRelativeOpportunity)}");
}

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
