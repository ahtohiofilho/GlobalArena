using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

// R119 research-only comparison: no WorldState mutation, no prices or
// authoritative route quotes. Every edge is one normalized traversal.
internal static class FoundationShallowDepthRoutingAb
{
    private sealed record Crossing(int A, int B, int Id,
        FoundationFineTransportCrossingKind Kind, long Depth,
        ulong NaturalFreight, ulong ConstructionMoney);

    private sealed record Route(ulong Cost, int[] Edges);

    private static long Quantile(long[] sorted, int numerator, int denominator) =>
        sorted[checked((int)((long)(sorted.Length - 1) * numerator / denominator))];

    private static ulong Increment(ulong baseCost, long depth,
        long threshold, uint percent)
    {
        if (threshold <= 0 || depth >= threshold || percent == 0)
            return 0UL;
        // All research alternatives use deterministic, integer-only
        // arithmetic. No depth-to-meters conversion or dynamic congestion.
        UInt128 numerator = (UInt128)baseCost * percent *
            (ulong)(threshold - Math.Max(0L, depth));
        return checked((ulong)(numerator / ((UInt128)100 * (ulong)threshold)));
    }

    private static ulong Cost(Crossing e, long threshold,
        uint portPercent, uint seaPercent)
    {
        uint percent = e.Kind switch
        {
            FoundationFineTransportCrossingKind.Port => portPercent,
            FoundationFineTransportCrossingKind.OpenSea => seaPercent,
            _ => 0U
        };
        return checked(e.NaturalFreight +
            Increment(e.NaturalFreight, e.Depth, threshold, percent));
    }

    private static Route Shortest(int source, int destination,
        List<(int To, int Edge)>[] adjacency, Crossing[] crossings,
        long threshold, uint portPercent, uint seaPercent)
    {
        var best = Enumerable.Repeat(ulong.MaxValue, adjacency.Length).ToArray();
        var priorNode = Enumerable.Repeat(-1, adjacency.Length).ToArray();
        var priorEdge = Enumerable.Repeat(-1, adjacency.Length).ToArray();
        var queue = new PriorityQueue<int, (ulong Cost, int Node)>();
        best[source] = 0UL;
        queue.Enqueue(source, (0UL, source));
        while (queue.TryDequeue(out int at, out var priority))
        {
            if (priority.Cost != best[at]) continue;
            if (at == destination) break;
            foreach (var (to, id) in adjacency[at])
            {
                ulong candidate = checked(priority.Cost +
                    Cost(crossings[id], threshold, portPercent, seaPercent));
                if (candidate >= best[to]) continue;
                best[to] = candidate;
                priorNode[to] = at;
                priorEdge[to] = id;
                queue.Enqueue(to, (candidate, to));
            }
        }
        if (best[destination] == ulong.MaxValue)
            throw new InvalidOperationException("Research pair is disconnected.");
        var path = new List<int>();
        for (int at = destination; at != source; at = priorNode[at])
        {
            if (at < 0 || priorEdge[at] < 0)
                throw new InvalidOperationException("Invalid path replay.");
            path.Add(priorEdge[at]);
        }
        path.Reverse();
        return new Route(best[destination], path.ToArray());
    }

    internal static void Run()
    {
        // Fixed candidate intensity percentages are only A/B probes, not
        // balance decisions. A threshold comes from the generated SEA depth
        // distribution; it is descriptive, never a permanent gameplay rule.
        foreach (ulong seed in new[] { 20261012UL, 20261013UL })
        {
            var coarse = new GoldbergParameters(3, 0);
            var fineParameters = new GoldbergParameters(18, 0);
            var incidence = PhysicalTacticalIncidenceMapper.Materialize(
                new GoldbergScaledRefinement(coarse, fineParameters));
            var world = new DeterministicWorldGenerator().Generate(
                new WorldGenerationRequest(new WorldSeed(seed),
                    WorldGenerationVersion.Initial, coarse));
            var fine = FoundationFineFirstPhysicalGeography.Generate(world, incidence);
            long seaLevel = fine.SeaLevelRaw;
            long[] depths = fine.FineElevationRaw.Select(x =>
                Math.Max(0L, seaLevel - x)).ToArray();
            var seaDepths = Enumerable.Range(0, depths.Length)
                .Where(i => fine.FineElevationRaw[i] <= seaLevel)
                .Select(i => depths[i]).OrderBy(x => x).ToArray();
            var policy = new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(250_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(500_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL);
            var money = new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL);
            var seeded = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
                world, incidence, fine, policy, money);
            var inventory = seeded.Transport.Edges.ToDictionary(
                row => row.Edge.FineStrategicEdgeId.Value);
            var crossings = new Crossing[incidence.FineTopology.Edges.Count];
            var graph = Enumerable.Range(0, depths.Length)
                .Select(_ => new List<(int To, int Edge)>()).ToArray();
            var coastalNodes = new SortedSet<int>();
            for (int i = 0; i < crossings.Length; i++)
            {
                var edge = incidence.FineTopology.Edges[i];
                int a = checked((int)edge.IncidentCellIds[0].Value - 1);
                int b = checked((int)edge.IncidentCellIds[1].Value - 1);
                var mode = fine.CrossingAtIndices(a, b);
                var record = inventory[edge.Id.Value];
                long d = mode == FoundationFineTransportCrossingKind.Port
                    ? (fine.FineElevationRaw[a] <= seaLevel ? depths[a] : depths[b])
                    : mode == FoundationFineTransportCrossingKind.OpenSea
                        ? Math.Min(depths[a], depths[b]) : 0L;
                crossings[i] = new Crossing(a, b, i, mode, d,
                    record.FreePhysicalCostMicroPerWholeCargoUnit,
                    record.NaturalConstructionCostMoneyMicro ??
                        throw new InvalidOperationException("Missing construction cost."));
                if (mode == FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
                    continue;
                graph[a].Add((b, i));
                graph[b].Add((a, i));
                if (mode == FoundationFineTransportCrossingKind.Port)
                    coastalNodes.Add(fine.FineElevationRaw[a] > seaLevel ? a : b);
            }
            foreach (var neighbors in graph)
                neighbors.Sort((x, y) => x.Edge.CompareTo(y.Edge));
            // Compare only mutually reachable coastal endpoints. Frozen
            // boundaries can split the graph, and must stay physically blocked.
            var components = Enumerable.Repeat(-1, graph.Length).ToArray();
            int componentId = 0;
            for (int first = 0; first < graph.Length; first++)
            {
                if (components[first] >= 0) continue;
                var queue = new Queue<int>();
                components[first] = componentId;
                queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue();
                    foreach (var (to, _) in graph[at])
                    {
                        if (components[to] >= 0) continue;
                        components[to] = componentId;
                        queue.Enqueue(to);
                    }
                }
                componentId++;
            }
            int[] coast = coastalNodes.GroupBy(node => components[node])
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .First().OrderBy(node => node).ToArray();
            if (coast.Length < 32)
                throw new InvalidOperationException("Insufficient connected coastal sample.");
            // 24 widely spaced, reproducible land-to-land coastal demands.
            // Do not claim that these synthetic pairs represent worker demand.
            var pairs = Enumerable.Range(0, 24).Select(i =>
            {
                int a = coast[i * coast.Length / 24];
                int b = coast[((i + 12) % 24) * coast.Length / 24];
                return (A: a, B: b);
            }).ToArray();
            Console.WriteLine($"GA_R119_WORLD seed={seed} " +
                $"nodes={graph.Length} edges={crossings.Length} " +
                $"sample_pairs={pairs.Length} cost_units=micro_per_unit_cargo " +
                "qa_policy=true actual_worker_demands=false depth_in_meters=false");

            foreach (var profile in new[]
            {
                (Name: "mild-p25", Percentile: 25, Port: 10U, Sea: 3U,
                    Construction: 20U),
                (Name: "moderate-p50", Percentile: 50, Port: 15U, Sea: 5U,
                    Construction: 30U)
            })
            {
                long cutoff = Quantile(seaDepths, profile.Percentile, 100);
                if (cutoff <= 0)
                    throw new InvalidOperationException("Missing depth variation.");
                int alteredPorts = 0, alteredSea = 0;
                ulong portMoneyIncrease = 0UL;
                foreach (var e in crossings)
                {
                    if (e.Kind == FoundationFineTransportCrossingKind.Port)
                    {
                        if (Increment(e.NaturalFreight, e.Depth,
                                cutoff, profile.Port) > 0) alteredPorts++;
                        portMoneyIncrease = checked(portMoneyIncrease +
                            Increment(e.ConstructionMoney, e.Depth,
                                cutoff, profile.Construction));
                    }
                    else if (e.Kind == FoundationFineTransportCrossingKind.OpenSea &&
                             Increment(e.NaturalFreight, e.Depth,
                                cutoff, profile.Sea) > 0)
                        alteredSea++;
                }
                int changed = 0, maritimeBaseline = 0, maritimeCandidate = 0;
                int portChoiceChanges = 0, meaningfulChanges = 0;
                ulong baselineSum = 0UL, candidateSum = 0UL;
                ulong baselineRouteUnderCandidate = 0UL, totalReroutingBenefit = 0UL;
                int baselinePortCrossings = 0, candidatePortCrossings = 0;
                // R121 causal attribution: compare changed port depths on
                // exactly the same demand pair; also isolate each modifier
                // to avoid mistaking route tie breaking for coastal selection.
                int deeperPortChoices = 0, shallowerPortChoices = 0;
                int equalDepthPortChoices = 0, unequalPortCounts = 0;
                int portOnlyRouteChanges = 0, seaOnlyRouteChanges = 0;
                int changedByBothModifiers = 0;
                ulong baselineNaturalAtNewRoute = 0UL;
                ulong candidateShallowPenalty = 0UL;
                ulong baselineShallowPenalty = 0UL;
                foreach (var (a, b) in pairs)
                {
                    var baseline = Shortest(a, b, graph, crossings, cutoff, 0U, 0U);
                    var candidate = Shortest(a, b, graph, crossings,
                        cutoff, profile.Port, profile.Sea);
                    if (!baseline.Edges.SequenceEqual(candidate.Edges)) changed++;
                    ulong keepOldRoute = 0UL;
                    foreach (int id in baseline.Edges)
                        keepOldRoute = checked(keepOldRoute +
                            Cost(crossings[id], cutoff, profile.Port, profile.Sea));
                    if (candidate.Cost > keepOldRoute)
                        throw new InvalidOperationException(
                            "Candidate routing must beat or match the old route at candidate costs.");
                    ulong benefit = keepOldRoute - candidate.Cost;
                    if (benefit > 0UL) meaningfulChanges++;
                    totalReroutingBenefit = checked(totalReroutingBenefit + benefit);
                    baselineRouteUnderCandidate = checked(
                        baselineRouteUnderCandidate + keepOldRoute);
                    var oldPorts = baseline.Edges.Where(id =>
                        crossings[id].Kind == FoundationFineTransportCrossingKind.Port)
                        .OrderBy(id => id);
                    var newPorts = candidate.Edges.Where(id =>
                        crossings[id].Kind == FoundationFineTransportCrossingKind.Port)
                        .OrderBy(id => id);
                    bool portChanged = !oldPorts.SequenceEqual(newPorts);
                    if (portChanged) portChoiceChanges++;
                    var beforePorts = oldPorts.ToArray();
                    var afterPorts = newPorts.ToArray();
                    if (portChanged)
                    {
                        if (beforePorts.Length == afterPorts.Length)
                        {
                            long beforeDepth = beforePorts.Sum(
                                id => crossings[id].Depth);
                            long afterDepth = afterPorts.Sum(
                                id => crossings[id].Depth);
                            if (afterDepth > beforeDepth) deeperPortChoices++;
                            else if (afterDepth < beforeDepth) shallowerPortChoices++;
                            else equalDepthPortChoices++;
                        }
                        else unequalPortCounts++;
                    }
                    var portOnly = Shortest(a, b, graph, crossings,
                        cutoff, profile.Port, 0U);
                    var seaOnly = Shortest(a, b, graph, crossings,
                        cutoff, 0U, profile.Sea);
                    bool differsPort = !baseline.Edges.SequenceEqual(portOnly.Edges);
                    bool differsSea = !baseline.Edges.SequenceEqual(seaOnly.Edges);
                    if (differsPort) portOnlyRouteChanges++;
                    if (differsSea) seaOnlyRouteChanges++;
                    if (differsPort && differsSea) changedByBothModifiers++;

                    foreach (int id in candidate.Edges)
                    {
                        ulong naturalCost = crossings[id].NaturalFreight;
                        baselineNaturalAtNewRoute = checked(
                            baselineNaturalAtNewRoute + naturalCost);
                        candidateShallowPenalty = checked(
                            candidateShallowPenalty + Cost(crossings[id],
                                cutoff, profile.Port, profile.Sea) - naturalCost);
                    }
                    foreach (int id in baseline.Edges)
                        baselineShallowPenalty = checked(
                            baselineShallowPenalty + Cost(crossings[id],
                                cutoff, profile.Port, profile.Sea) -
                            crossings[id].NaturalFreight);
                    baselineSum = checked(baselineSum + baseline.Cost);
                    candidateSum = checked(candidateSum + candidate.Cost);
                    int bPorts = baseline.Edges.Count(e =>
                        crossings[e].Kind == FoundationFineTransportCrossingKind.Port);
                    int cPorts = candidate.Edges.Count(e =>
                        crossings[e].Kind == FoundationFineTransportCrossingKind.Port);
                    baselinePortCrossings += bPorts;
                    candidatePortCrossings += cPorts;
                    if (bPorts > 0) maritimeBaseline++;
                    if (cPorts > 0) maritimeCandidate++;
                }
                if (portChoiceChanges != deeperPortChoices +
                    shallowerPortChoices + equalDepthPortChoices +
                    unequalPortCounts ||
                    checked(baselineNaturalAtNewRoute +
                        candidateShallowPenalty) != candidateSum ||
                    checked(baselineSum + baselineShallowPenalty) !=
                        baselineRouteUnderCandidate)
                    throw new InvalidOperationException(
                        "R121 causal attribution must reconcile every route, " +
                        "natural price and shallow-depth surcharge.");
                // This trace is descriptive, NOT proof of a better sea
                // state or port geometry: the choice also depends on land
                // access and the entire path. Baseline and candidate have
                // identical physical topology and blocked-ice constraints.
                Console.WriteLine($"GA_R121_CAUSAL seed={seed} profile={profile.Name} " +
                    $"demand_pairs={pairs.Length} " +
                    $"port_switches={portChoiceChanges} " +
                    $"switched_to_deeper_total_port_depth={deeperPortChoices} " +
                    $"switched_to_shallower_total_port_depth={shallowerPortChoices} " +
                    $"switched_equal_port_depth={equalDepthPortChoices} " +
                    $"switched_different_port_count={unequalPortCounts} " +
                    $"port_modifier_alone_route_changes={portOnlyRouteChanges} " +
                    $"sea_modifier_alone_route_changes={seaOnlyRouteChanges} " +
                    $"both_alone_changed_route={changedByBothModifiers} " +
                    $"baseline_natural_total={baselineSum} " +
                    $"candidate_route_natural_total={baselineNaturalAtNewRoute} " +
                    $"baseline_route_shallow_surcharge={baselineShallowPenalty} " +
                    $"candidate_route_shallow_surcharge={candidateShallowPenalty} " +
                    $"candidate_total={candidateSum} " +
                    $"reroute_savings_vs_old_at_new_rates={totalReroutingBenefit} " +
                    "depth_units=relative_raw_no_meters=true " +
                    "synthetic_pairs_not_settled_worker_demands=true");
                Console.WriteLine($"GA_R119_AB seed={seed} profile={profile.Name} " +
                    $"depth_cutoff_raw={cutoff} port_percent={profile.Port} " +
                    $"sea_percent={profile.Sea} construction_percent={profile.Construction} " +
                    $"altered_ports={alteredPorts} altered_sea={alteredSea} " +
                    $"added_port_construction_micro={portMoneyIncrease} " +
                    $"route_changes={changed}/{pairs.Length} " +
                    $"port_choice_changes={portChoiceChanges}/{pairs.Length} " +
                    $"meaningful_reroutes={meaningfulChanges}/{pairs.Length} " +
                    $"old_routes_under_new_costs={baselineRouteUnderCandidate} " +
                    $"rerouting_benefit={totalReroutingBenefit} " +
                    $"maritime_pairs_base={maritimeBaseline} " +
                    $"maritime_pairs_candidate={maritimeCandidate} " +
                    $"port_crossings_base={baselinePortCrossings} " +
                    $"port_crossings_candidate={candidatePortCrossings} " +
                    $"cost_sum_base={baselineSum} cost_sum_candidate={candidateSum}");
            }
        }
    }
}
