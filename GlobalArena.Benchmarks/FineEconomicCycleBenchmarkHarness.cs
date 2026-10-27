using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Empirical whole-economic-close measurement FROM a completed, immutable
/// synthetic fine-world snapshot: actual built-asset route discovery,
/// whole-service ex-ante freight quotation, then dynamic commodity market
/// clearing. Topology/asset fixture construction is intentionally excluded;
/// there is no invented movement-round cadence or performance acceptance cap.
/// </summary>
public static class FineEconomicCycleBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private const int Samples = 5;

    private enum Pattern : byte { Complete, Local, Hub }

    private sealed record Scenario(
        string Id, GoldbergParameters Resolution, int BuiltCells,
        int Producers, int Destinations, int Commodities, Pattern Connectivity);

    private static readonly Scenario[] Cases =
    {
        new("fine-smoke", new GoldbergParameters(2, 0), 30, 2, 2, 2,
            Pattern.Complete),
        new("fine-local", new GoldbergParameters(4, 0), 100, 6, 6, 2,
            Pattern.Local),
        new("fine-hub", new GoldbergParameters(8, 0), 300, 8, 8, 3,
            Pattern.Hub),
        new("fine-connected", new GoldbergParameters(12, 0), 750, 12, 12, 3,
            Pattern.Complete)
    };

    private sealed class SyntheticWholeServicePricing
        : IEconomicFineTransportItineraryPricing
    {
        public int Factories { get; private set; }
        public List<SyntheticCostFunction> Functions { get; } = new();

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicFineTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportBuiltAsset> orderedAssets)
        {
            Factories++;
            if (orderedAssets.Count == 0 || orderedAssets.Any(x =>
                x.TechnologyTypeId != intent.TechnologyTypeId ||
                x.TechnologyCatalogVersion != intent.TechnologyCatalogVersion))
                throw new InvalidOperationException(
                    "Benchmark service used invalid physical assets.");

            // Synthetic-only continuous cost derived from FULL itinerary
            // length and forecast. Not a production tariff or technology rate.
            var function = new SyntheticCostFunction(orderedAssets.Count);
            Functions.Add(function);
            return function;
        }
    }

    private sealed class SyntheticCostFunction(int assetCount)
        : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Calls { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Calls++;
            var d = expectedLoadMicrograms + 1_000_000_000;
            return new FoodTransportCostFraction(
                ((BigInteger)20 + 8 * assetCount) * d +
                120_000_000_000, d);
        }
    }

    private sealed class SyntheticCommodityValue(
        ulong initial, ulong slope) : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(
            ulong alreadyAllocatedRawQuantity) =>
            new((BigInteger)initial * Unit -
                (BigInteger)slope * alreadyAllocatedRawQuantity, Unit);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(
            ulong allocatedRawQuantity)
        {
            var quantity = (BigInteger)allocatedRawQuantity;
            return new FoodTransportCostFraction(
                2 * (BigInteger)initial * quantity * Unit -
                    (BigInteger)slope * quantity * quantity,
                2 * (BigInteger)Unit * Unit);
        }
    }

    private sealed record Snapshot(
        StrategicSurfaceGraph Geography,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> Anchors,
        EconomicTransportBuiltAsset[] Assets,
        EconomicFineTransportServiceIntent[] Intents,
        EconomicProductionDemandResult Flows,
        FoodCommodityCatalog Commodities,
        EconomicTurnCommodityValueOffer[] Values,
        EconomicTurnSourceReservation[] Reservations,
        EconomicCongestionMarketSearchPolicy Policy);

    private sealed record Sample(
        double DiscoveryMs, double QuotationMs, double ClearingMs, double TotalMs,
        long DiscoveryBytes, long QuotationBytes, long ClearingBytes,
        long TotalBytes, int AvailableOffers, int SelectedQuotes,
        int FunctionEvaluations, int Trades, string Fingerprint,
        long LaneExaminations, long MarginalEvaluations);

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("Economic cycle benchmark requires Release.");
        Console.WriteLine("RESULT=FAIL_FINE_ECONOMIC_CYCLE_CONFIGURATION");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"ENV_ARCH={RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"ENV_PROCESSORS={Environment.ProcessorCount}");
            Console.WriteLine("SCOPE=synthetic-built-fine-topology-post-movement-snapshot");
            Console.WriteLine($"SAMPLES_PER_MODE={Samples}");
            Console.WriteLine("PERFORMANCE_THRESHOLDS=none");
            Console.WriteLine("WORLD_SNAPSHOT_CONSTRUCTION=outside-cycle-timings");
            foreach (var scenario in Cases)
                MeasureScenario(scenario);
            Console.WriteLine("RESULT=PASS_FINE_ECONOMIC_CYCLE_BENCHMARK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("RESULT=FAIL_FINE_ECONOMIC_CYCLE_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void MeasureScenario(Scenario scenario)
    {
        var snapshotStart = Stopwatch.GetTimestamp();
        var snapshot = BuildSnapshot(scenario);
        var snapshotMs = Stopwatch.GetElapsedTime(snapshotStart).TotalMilliseconds;
        if (scenario.Id == "fine-smoke")
            CheckSmallWorldExactPathOracle(snapshot);

        var first = Measure(snapshot, EconomicTurnFreightCandidateSelectionMode.Exhaustive);
        var firstIndexed = Measure(snapshot,
            EconomicTurnFreightCandidateSelectionMode.DependencyIndex);
        AssertSameEconomics(first, firstIndexed, scenario.Id);

        var reference = new Sample[Samples];
        var indexed = new Sample[Samples];
        for (var i = 0; i < Samples; i++)
        {
            reference[i] = Measure(
                snapshot, EconomicTurnFreightCandidateSelectionMode.Exhaustive);
            indexed[i] = Measure(
                snapshot, EconomicTurnFreightCandidateSelectionMode.DependencyIndex);
            AssertSameEconomics(reference[i], indexed[i], scenario.Id);
            if (reference[i].Fingerprint != first.Fingerprint ||
                indexed[i].Fingerprint != first.Fingerprint)
                throw new InvalidOperationException(
                    $"{scenario.Id}: cycle ledger or physical quote changed across replays.");
        }

        Console.WriteLine($"SCENARIO={scenario.Id};" +
            $"fine_cells={scenario.Resolution.StrategicCellCount};" +
            $"fine_edges={scenario.Resolution.StrategicEdgeCount};" +
            $"built_assets={snapshot.Assets.Length};" +
            $"intent_count={snapshot.Intents.Length};" +
            $"commodities={scenario.Commodities};" +
            $"snapshot_fixture_ms={snapshotMs:F3};" +
            $"quotes={first.SelectedQuotes};" +
            $"trades={first.Trades};" +
            $"fingerprint={first.Fingerprint}");

        WriteMeasurements(scenario.Id, "exhaustive", reference);
        WriteMeasurements(scenario.Id, "indexed", indexed);
    }

    private static void AssertSameEconomics(Sample oracle, Sample indexed, string id)
    {
        if (oracle.Fingerprint != indexed.Fingerprint ||
            oracle.AvailableOffers != indexed.AvailableOffers ||
            oracle.SelectedQuotes != indexed.SelectedQuotes ||
            oracle.FunctionEvaluations != indexed.FunctionEvaluations ||
            oracle.Trades != indexed.Trades)
            throw new InvalidOperationException(
                $"{id}: indexed clearing disagrees with exhaustive full-cycle economics.");
    }

    /// <summary>
    /// A constructed tree has exactly one simple asset path per pair.
    /// The exponentially expensive whole-service price oracle must therefore
    /// agree with the faster physical connectivity discovery in this small
    /// fixture. Kept outside timed samples; large-world oracle is NOT used.
    /// </summary>
    private static void CheckSmallWorldExactPathOracle(Snapshot snapshot)
    {
        var access = StrategicEdgeAccessSnapshot.AllOpen;
        var discovery = new EconomicFineTransportServiceItineraryDiscovery();
        var offers = discovery.Discover(
            snapshot.Geography, snapshot.Anchors, access,
            snapshot.Assets, snapshot.Intents, new SyntheticWholeServicePricing());
        var expected = new EconomicTransportServiceQuoteResolver().Resolve(
            1UL, snapshot.Geography, snapshot.Anchors,
            snapshot.Flows, access, snapshot.Assets, offers);

        var oraclePricing = new SyntheticWholeServicePricing();
        var exact = new EconomicFinePriceAwareServiceResolver().Resolve(
            1UL, snapshot.Geography, snapshot.Anchors,
            snapshot.Flows, access, snapshot.Assets, snapshot.Intents,
            oraclePricing,
            new EconomicFinePriceAwareSearchBudget(200_000UL, 100UL));

        if (Fingerprint(expected, Array.Empty<EconomicTurnLockedFreightTrade>()) !=
            Fingerprint(exact, Array.Empty<EconomicTurnLockedFreightTrade>()) ||
            oraclePricing.Factories != snapshot.Intents.Length ||
            oraclePricing.Functions.Any(x => x.Calls != 1))
            throw new InvalidOperationException(
                "Small-world exhaustive whole-itinerary oracle disagrees with " +
                "the routed quote reference.");
        Console.WriteLine(
            $"SMALL_ORACLE=PASS;itineraries={exact.Count};" +
            $"pricing_evaluations={oraclePricing.Functions.Count}");
    }

    private static Sample Measure(
        Snapshot snapshot, EconomicTurnFreightCandidateSelectionMode mode)
    {
        var costFactory = new SyntheticWholeServicePricing();
        var discoverer = new EconomicFineTransportServiceItineraryDiscovery();
        var quoter = new EconomicTransportServiceQuoteResolver();
        var market = new EconomicTurnLockedFreightMarketResolver();
        var diagnostics = new EconomicTurnFreightMarketDiagnostics();
        var startBytes = GC.GetAllocatedBytesForCurrentThread();
        var totalStart = Stopwatch.GetTimestamp();

        var phaseStart = Stopwatch.GetTimestamp();
        var offers = discoverer.Discover(
            snapshot.Geography, snapshot.Anchors,
            StrategicEdgeAccessSnapshot.AllOpen,
            snapshot.Assets, snapshot.Intents, costFactory);
        var discoveryMs = Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;
        var discoveryBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;

        phaseStart = Stopwatch.GetTimestamp();
        var selections = quoter.Resolve(
            1UL, snapshot.Geography, snapshot.Anchors, snapshot.Flows,
            StrategicEdgeAccessSnapshot.AllOpen,
            snapshot.Assets, offers);
        var quotationMs = Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;
        var quotationBytes = GC.GetAllocatedBytesForCurrentThread() -
            startBytes - discoveryBytes;

        phaseStart = Stopwatch.GetTimestamp();
        var trades = market.Resolve(
            1UL, snapshot.Flows, snapshot.Commodities,
            selections.Select(x => x.Quote),
            snapshot.Values, snapshot.Reservations, snapshot.Policy,
            diagnostics, mode);
        var clearingMs = Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;
        var totalMs = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;
        var totalBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;
        var clearingBytes = totalBytes - quotationBytes - discoveryBytes;

        if (costFactory.Factories != offers.Count ||
            costFactory.Functions.Count != offers.Count ||
            costFactory.Functions.Any(x => x.Calls != 1) ||
            selections.Count != offers.Count ||
            offers.Count != snapshot.Intents.Length ||
            diagnostics.AcceptedTrades != trades.Count)
            throw new InvalidOperationException(
                "Every authorized built service must be quoted exactly once, " +
                "and clearing must not re-evaluate freight prices.");

        // Auditing is deliberately outside all three timed phases.
        var transcript = Fingerprint(selections, trades);
        return new Sample(
            discoveryMs, quotationMs, clearingMs, totalMs,
            discoveryBytes, quotationBytes, clearingBytes, totalBytes,
            offers.Count, selections.Count, costFactory.Functions.Sum(x => x.Calls),
            trades.Count, transcript,
            diagnostics.LaneExaminations, diagnostics.MarginalValueEvaluations);
    }

    private static string Fingerprint(
        IReadOnlyList<EconomicTransportServiceSelection> selections,
        IReadOnlyList<EconomicTurnLockedFreightTrade> trades)
    {
        var sb = new StringBuilder();
        foreach (var s in selections)
        {
            sb.Append("Q:").Append(s.ServiceId).Append(':')
                .Append(s.Quote.TurnOrdinal).Append(':')
                .Append(s.Quote.RouteKey.SourceEconomicPointId.Value).Append(':')
                .Append(s.Quote.RouteKey.DestinationEconomicPointId.Value).Append(':')
                .Append(s.Quote.RulesetVersion).Append(':')
                .Append(s.Quote.ExpectedLoadMicrograms).Append(':')
                .Append(s.Quote.RawUnitCostPerGram.Numerator).Append('/')
                .Append(s.Quote.RawUnitCostPerGram.Denominator).Append(':');
            foreach (var assetId in s.AssetIds)
                sb.Append(assetId).Append(',');
            sb.Append(';');
        }
        foreach (var t in trades)
        {
            sb.Append("T:").Append(t.DecisionOrdinal).Append(':')
                .Append(t.SourceEconomicPointId.Value).Append(':')
                .Append((byte)t.SourceActivity).Append(':')
                .Append(t.DestinationEconomicPointId.Value).Append(':')
                .Append(t.CommodityId.Value).Append(':')
                .Append(t.RawQuantity).Append(':')
                .Append(t.RawMarginalCommodityUnitValueAtDecision.Numerator)
                .Append('/').Append(t.RawMarginalCommodityUnitValueAtDecision.Denominator)
                .Append(':').Append(t.RawCommodityValue.Numerator)
                .Append('/').Append(t.RawCommodityValue.Denominator)
                .Append(':').Append(t.RawFreightCharge.Numerator)
                .Append('/').Append(t.RawFreightCharge.Denominator)
                .Append(':').Append(t.RawProducerReservationCost.Numerator)
                .Append('/').Append(t.RawProducerReservationCost.Denominator)
                .Append(':').Append(t.RawNetReturn.Numerator)
                .Append('/').Append(t.RawNetReturn.Denominator).Append(';');
        }
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static void WriteMeasurements(
        string id, string mode, IReadOnlyList<Sample> measurements)
    {
        static string Metric(
            IReadOnlyList<Sample> samples, Func<Sample, double> selector)
        {
            var numbers = samples.Select(selector).Order().ToArray();
            return $"median={numbers[numbers.Length / 2]:F3};" +
                $"max={numbers[^1]:F3}";
        }

        static string Memory(
            IReadOnlyList<Sample> samples, Func<Sample, long> selector)
        {
            var numbers = samples.Select(selector).Order().ToArray();
            return $"median={numbers[numbers.Length / 2]};" +
                $"max={numbers[^1]}";
        }

        Console.WriteLine($"PHASE={id};mode={mode};step=discovery;" +
            $"ms_{Metric(measurements, x => x.DiscoveryMs)};" +
            $"bytes_{Memory(measurements, x => x.DiscoveryBytes)}");
        Console.WriteLine($"PHASE={id};mode={mode};step=quotation;" +
            $"ms_{Metric(measurements, x => x.QuotationMs)};" +
            $"bytes_{Memory(measurements, x => x.QuotationBytes)}");
        Console.WriteLine($"PHASE={id};mode={mode};step=clearing;" +
            $"ms_{Metric(measurements, x => x.ClearingMs)};" +
            $"bytes_{Memory(measurements, x => x.ClearingBytes)}");
        Console.WriteLine($"PHASE={id};mode={mode};step=total;" +
            $"ms_{Metric(measurements, x => x.TotalMs)};" +
            $"bytes_{Memory(measurements, x => x.TotalBytes)};" +
            $"lane_examinations={measurements[0].LaneExaminations};" +
            $"marginal_evaluations={measurements[0].MarginalEvaluations};" +
            $"samples={measurements.Count};status=OBSERVATIONAL");
    }

    private static Snapshot BuildSnapshot(Scenario scenario)
    {
        var geography = new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(scenario.Resolution));
        var byEndpoints = new Dictionary<(int, int), StrategicEdgeId>();
        foreach (var edge in geography.StrategicTopology.Edges)
        {
            var a = geography.GetNodeIndex(edge.IncidentCellIds[0]);
            var b = geography.GetNodeIndex(edge.IncidentCellIds[1]);
            if (a > b)
                (a, b) = (b, a);
            if (!byEndpoints.TryAdd((a, b), edge.Id))
                throw new InvalidOperationException("Invalid fine topology edge identity.");
        }

        // Synthetic constructed infrastructure: a connected, sparse tree
        // embedded in actual generated fine Goldberg geography. The other
        // geographic corridors are NOT constructed and remain unavailable.
        var visited = new List<int> { 0 };
        var found = new HashSet<int> { 0 };
        var queue = new Queue<int>();
        var built = new List<EconomicTransportBuiltAsset>();
        queue.Enqueue(0);
        while (queue.Count != 0 && visited.Count < scenario.BuiltCells)
        {
            var parent = queue.Dequeue();
            foreach (var child in geography.GetNeighborIndexes(parent))
            {
                if (visited.Count == scenario.BuiltCells)
                    break;
                if (!found.Add(child))
                    continue;
                visited.Add(child);
                queue.Enqueue(child);
                var a = Math.Min(parent, child);
                var b = Math.Max(parent, child);
                var edgeId = byEndpoints[(a, b)];
                built.Add(new EconomicTransportBuiltAsset(
                    edgeId.Value, 10UL,
                    new PhysicalTacticalEdgeId(scenario.Resolution, edgeId),
                    1UL));
            }
        }
        if (visited.Count != scenario.BuiltCells ||
            built.Count + 1 != visited.Count)
            throw new InvalidOperationException(
                "Physical test infrastructure was not built as a tree.");

        var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>();
        for (var i = 0; i < scenario.Producers; i++)
            anchors.Add(new EconomicPointId((ulong)(i + 1)),
                new PhysicalTacticalTileId(scenario.Resolution,
                    geography.GetCellId(visited[1 + 4 * i])));
        for (var i = 0; i < scenario.Destinations; i++)
            anchors.Add(new EconomicPointId((ulong)(i + 1_001)),
                new PhysicalTacticalTileId(scenario.Resolution,
                    geography.GetCellId(visited[visited.Count - 2 - 4 * i])));

        var intents = new List<EconomicFineTransportServiceIntent>();
        ulong serviceId = 1UL;
        for (var i = 0; i < scenario.Producers; i++)
            for (var j = 0; j < scenario.Destinations; j++)
            {
                bool connected = scenario.Connectivity switch
                {
                    Pattern.Complete => true,
                    Pattern.Local => j == i ||
                        j == (i + 1) % scenario.Destinations ||
                        j == (i + scenario.Destinations - 1) % scenario.Destinations,
                    Pattern.Hub => i == 0 || j == 0 ||
                        j == i || j == (i + 1) % scenario.Destinations,
                    _ => throw new InvalidOperationException("Invalid graph pattern.")
                };
                if (!connected)
                    continue;
                intents.Add(new EconomicFineTransportServiceIntent(
                    serviceId++,
                    new StrategicEconomicRouteKey(
                        new EconomicPointId((ulong)(i + 1)),
                        new EconomicPointId((ulong)(j + 1_001))),
                    10UL, 1UL,
                    (BigInteger)(900 + i * 200 + j * 100) * 1_000_000));
            }

        var commodities = new List<FoodCommodityDefinition>();
        var supplies = new List<EconomicSupplyFlow>();
        var demands = new List<EconomicDemandFlow>();
        var values = new List<EconomicTurnCommodityValueOffer>();
        var reservations = new List<EconomicTurnSourceReservation>();
        for (var c = 0; c < scenario.Commodities; c++)
        {
            var commodity = new CommodityId((uint)(c + 7));
            commodities.Add(new FoodCommodityDefinition(
                commodity, $"fine-synthetic-{c}", (ulong)(1_000 + 500 * c)));
            for (var i = 0; i < scenario.Producers; i++)
            {
                var source = new EconomicPointId((ulong)(i + 1));
                var activity = i % 2 == 0
                    ? EconomicActivityKind.Agriculture
                    : EconomicActivityKind.Mining;
                supplies.Add(new EconomicSupplyFlow(
                    source, commodity, activity, 3UL * Unit));
                reservations.Add(new EconomicTurnSourceReservation(
                    source, commodity, activity, (ulong)(20_000 + i * 1_000)));
            }
            for (var j = 0; j < scenario.Destinations; j++)
            {
                var destination = new EconomicPointId((ulong)(j + 1_001));
                demands.Add(new EconomicDemandFlow(
                    destination, commodity, 4UL * Unit));
                values.Add(new EconomicTurnCommodityValueOffer(
                    destination, commodity,
                    new SyntheticCommodityValue(
                        (ulong)(900_000 + c * 2_000 + j * 500),
                        (ulong)(2_000 + j * 50))));
            }
        }

        return new Snapshot(
            geography, anchors, built.ToArray(), intents.ToArray(),
            new EconomicProductionDemandResult(supplies, demands),
            new FoodCommodityCatalog(commodities),
            values.ToArray(), reservations.ToArray(),
            new EconomicCongestionMarketSearchPolicy(
                Unit, scenario.Producers * scenario.Commodities * 6 + 100));
    }
}
