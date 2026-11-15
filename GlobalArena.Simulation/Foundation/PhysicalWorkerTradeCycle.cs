using System.Collections.ObjectModel;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// NEW foundation, intentionally separate from the legacy M4 food/market model.
/// Headless playable-turn prototype, NOT the authoritative WorldState pipeline.
/// All nodes obey identical rules. No market regions, node tiers or hard travel
/// cutoff. This is an iterative economic approximation, NOT F072 certification.
/// </summary>
public enum FoundationWorkerActivity { Producing, Traveling, Military, Other }

public sealed record FoundationWorker(
    int Id, int Civilization, int Tile, int Commodity, double OutputPerTurn,
    FoundationWorkerActivity Activity, double DemandMultiplier = 1d);

public sealed record FoundationEdge(
    int Id, int A, int B, double FreeCost, double ThroughputScale,
    int? OwnerCivilization = null, double ExternalTariff = 0d);

public sealed record FoundationWorkerOrder(
    int WorkerId, int? NextTile = null,
    FoundationWorkerActivity? NextActivity = null);

public sealed record FoundationTrade(
    int Origin, int Destination, int Commodity, int Seller,
    double Quantity, IReadOnlyList<int> EdgeIds);

public sealed class FoundationTurn
{
    public int Turn { get; }
    public IReadOnlyList<FoundationWorker> Workers { get; }
    public IReadOnlyList<FoundationTrade> Trades { get; }
    public IReadOnlyDictionary<(int Tile, int Commodity), double> Demand { get; }
    public IReadOnlyDictionary<(int Tile, int Commodity), double> Delivered { get; }
    public IReadOnlyDictionary<(int Tile, int Commodity), double> Prices { get; }
    public IReadOnlyDictionary<int, double> EdgeLoads { get; }
    public double TotalProduction { get; }
    public double TotalDelivered { get; }
    public double MaxRelativeOpportunity { get; }
    public int AdjustmentPasses { get; }
    public bool FinancialSettlementImplemented => false;
    public bool CertifiedGlobalEquilibrium => false;

    internal FoundationTurn(int turn, FoundationWorker[] workers,
        FoundationTrade[] trades,
        Dictionary<(int, int), double> demand,
        Dictionary<(int, int), double> delivered,
        Dictionary<(int, int), double> prices,
        Dictionary<int, double> loads,
        double production, double maxOpportunity, int passes)
    {
        Turn = turn;
        Workers = Array.AsReadOnly(workers);
        Trades = Array.AsReadOnly(trades);
        Demand = new ReadOnlyDictionary<(int, int), double>(demand);
        Delivered = new ReadOnlyDictionary<(int, int), double>(delivered);
        Prices = new ReadOnlyDictionary<(int, int), double>(prices);
        EdgeLoads = new ReadOnlyDictionary<int, double>(loads);
        TotalProduction = production;
        TotalDelivered = trades.Sum(trade => trade.Quantity);
        MaxRelativeOpportunity = maxOpportunity;
        AdjustmentPasses = passes;
    }
}

internal readonly record struct FoundationProducerKey(int Tile, int Commodity, int Seller);
internal readonly record struct FoundationTradeKey(
    FoundationProducerKey Producer, int Destination, string Route);
internal sealed record FoundationRoute(double Cost, int[] Edges, string Signature);
internal sealed record FoundationSnapshot(
    Dictionary<int, double> Loads,
    Dictionary<(int, int), double> Delivered,
    Dictionary<(int, int), double> Prices);

/// <summary>
/// A first executable C# economic cycle for the redesigned foundation:
/// - physical workers create both local demand and (when producing) supply;
/// - workers traveling/military/idle still demand exactly once at their tile;
/// - production groups aggregate by (tile, commodity, civilization);
/// - all production is actually allocated (no artificial buyers or stock);
/// - price sqrt(D/Q), Q=0 unpriced (never a price floor);
/// - edge cost FreeCost*(1+(load/scale)^4) plus seller-external tariff;
/// - adaptive conservative trade redistribution under an explicit pass budget.
/// Invariants are enforced; numerical optimality is monitored, not invented.
/// </summary>
public sealed class PhysicalWorkerTradeCycle
{
    private readonly SortedDictionary<int, FoundationEdge> _edges = new();
    private readonly SortedDictionary<int, List<(int Next, int Edge)>> _adj = new();
    private readonly SortedDictionary<int, double> _demandPerWorker = new();
    private SortedDictionary<int, FoundationWorker> _workers = new();
    private Dictionary<FoundationTradeKey, double> _allocations = new();
    private HashSet<(int Tile, int Commodity)> _previousMarkets = new();
    private int _turn;

    public int MaximumAdjustmentPasses { get; }
    public double RelativeOpportunityTolerance { get; }
    public double MaximumStep { get; }
    public double ResponseScale { get; }
    public IReadOnlyList<FoundationWorker> Workers =>
        Array.AsReadOnly(_workers.Values.ToArray());

    public PhysicalWorkerTradeCycle(
        IEnumerable<FoundationEdge> edges,
        IEnumerable<FoundationWorker> workers,
        IReadOnlyDictionary<int, double> demandPerWorker,
        int maximumAdjustmentPasses = 4,
        double relativeOpportunityTolerance = 0.001d,
        double maximumStep = 0.30d,
        double responseScale = 0.10d)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(workers);
        ArgumentNullException.ThrowIfNull(demandPerWorker);
        if (maximumAdjustmentPasses < 1 ||
            !ValidNonnegative(relativeOpportunityTolerance) ||
            relativeOpportunityTolerance >= 1d ||
            !double.IsFinite(maximumStep) || maximumStep <= 0d || maximumStep > 1d ||
            !double.IsFinite(responseScale) || responseScale <= 0d)
            throw new ArgumentOutOfRangeException(nameof(maximumAdjustmentPasses),
                "Pass budget and adaptive correction parameters must be valid.");

        MaximumAdjustmentPasses = maximumAdjustmentPasses;
        RelativeOpportunityTolerance = relativeOpportunityTolerance;
        MaximumStep = maximumStep;
        ResponseScale = responseScale;

        foreach (var (commodity, amount) in demandPerWorker.OrderBy(x => x.Key))
        {
            if (commodity < 0 || !double.IsFinite(amount) || amount <= 0d)
                throw new ArgumentException("Every declared commodity has positive worker demand.");
            _demandPerWorker.Add(commodity, amount);
        }
        if (_demandPerWorker.Count == 0)
            throw new ArgumentException("At least one commodity is required.");

        foreach (var edge in edges)
        {
            if (edge is null || edge.Id < 0 || edge.A < 0 || edge.B < 0 ||
                edge.A == edge.B || !double.IsFinite(edge.FreeCost) ||
                edge.FreeCost <= 0 || !double.IsFinite(edge.ThroughputScale) ||
                edge.ThroughputScale <= 0 || !ValidNonnegative(edge.ExternalTariff) ||
                (edge.OwnerCivilization is null && edge.ExternalTariff > 0))
                throw new ArgumentException("Invalid logical edge/continuous congestion policy.");
            if (!_edges.TryAdd(edge.Id, edge))
                throw new ArgumentException("Duplicate edge identity.");
            AddNeighbor(edge.A, edge.B, edge.Id);
            AddNeighbor(edge.B, edge.A, edge.Id);
        }
        foreach (var worker in workers)
        {
            ValidateWorker(worker);
            if (!_workers.TryAdd(worker.Id, worker))
                throw new ArgumentException("Duplicate physical worker identity.");
            if (!_adj.ContainsKey(worker.Tile))
                _adj.Add(worker.Tile, new List<(int, int)>());
        }
        if (_workers.Count == 0)
            throw new ArgumentException("At least one physical worker is required.");
        foreach (var adjacency in _adj.Values)
            adjacency.Sort((a, b) =>
            {
                var n = a.Next.CompareTo(b.Next);
                return n != 0 ? n : a.Edge.CompareTo(b.Edge);
            });
    }

    public FoundationTurn Advance(IEnumerable<FoundationWorkerOrder>? orders = null)
    {
        // Validate the entire movement/activity batch BEFORE altering this state.
        var nextWorkers = new SortedDictionary<int, FoundationWorker>(_workers);
        var seen = new HashSet<int>();
        foreach (var order in orders ?? Array.Empty<FoundationWorkerOrder>())
        {
            if (order is null || !nextWorkers.TryGetValue(order.WorkerId, out var worker) ||
                !seen.Add(order.WorkerId))
                throw new ArgumentException("Unknown or duplicated worker order.");
            if (order.NextTile is int tile)
            {
                if (tile != worker.Tile && !_adj[worker.Tile].Any(x => x.Next == tile))
                    throw new ArgumentException("Movement must traverse one existing edge.");
                worker = worker with { Tile = tile, Activity =
                    order.NextActivity ?? FoundationWorkerActivity.Traveling };
            }
            else if (order.NextActivity is FoundationWorkerActivity mode)
            {
                worker = worker with { Activity = mode };
            }
            ValidateWorker(worker);
            nextWorkers[order.WorkerId] = worker;
        }

        var demand = new Dictionary<(int, int), double>();
        var production = new Dictionary<FoundationProducerKey, double>();
        foreach (var worker in nextWorkers.Values)
        {
            foreach (var (commodity, perWorker) in _demandPerWorker)
            {
                var key = (worker.Tile, commodity);
                demand[key] = demand.GetValueOrDefault(key) +
                    perWorker * worker.DemandMultiplier;
            }
            if (worker.Activity == FoundationWorkerActivity.Producing &&
                worker.OutputPerTurn > 0d)
            {
                var key = new FoundationProducerKey(
                    worker.Tile, worker.Commodity, worker.Civilization);
                production[key] = production.GetValueOrDefault(key) +
                    worker.OutputPerTurn;
            }
        }

        // The absence of supply for a commodity never creates a synthetic
        // producer. Such markets remain unpriced/undelivered this turn.
        var pricedMarkets = demand.Keys
            .Where(key => production.Keys.Any(group => group.Commodity == key.Item2))
            .ToHashSet();

        Dictionary<FoundationTradeKey, double> next;
        var sameMarkets = _previousMarkets.SetEquals(pricedMarkets);
        var sameGroups = production.Count ==
            _allocations.Keys.Select(key => key.Producer).Distinct().Count() &&
            production.Keys.All(group =>
                _allocations.Keys.Any(key => key.Producer == group));
        if (sameMarkets && sameGroups)
        {
            // Exact group-supply rescaling of the previous economic state.
            next = new Dictionary<FoundationTradeKey, double>();
            foreach (var group in production.Keys.OrderBy(x => x.Tile)
                         .ThenBy(x => x.Commodity).ThenBy(x => x.Seller))
            {
                var previous = _allocations
                    .Where(pair => pair.Key.Producer == group).ToArray();
                var sum = previous.Sum(pair => pair.Value);
                if (sum <= 0d)
                    throw new InvalidOperationException("Missing funded producer allocation.");
                foreach (var (key, mass) in previous)
                    next[key] = mass * production[group] / sum;
                NormalizeGroup(next, group, production[group]);
            }
        }
        else
        {
            // Physical mass seeding, not invented Q or artificial demand:
            // distribute EACH actual producer's supply to reachable markets
            // so sqrt(D/Q) can be evaluated for those markets. New market
            // locations force a fresh seed; otherwise the prior state persists.
            next = new Dictionary<FoundationTradeKey, double>();
            var zeroLoads = new Dictionary<int, double>();
            foreach (var (group, supply) in production
                .OrderBy(x => x.Key.Tile).ThenBy(x => x.Key.Commodity)
                .ThenBy(x => x.Key.Seller))
            {
                var routes = ShortestPaths(group.Tile, group.Seller, zeroLoads);
                var markets = pricedMarkets
                    .Where(m => m.Commodity == group.Commodity &&
                        routes.ContainsKey(m.Tile))
                    .OrderBy(m => m.Tile).ToArray();
                if (markets.Length == 0)
                    throw new InvalidOperationException("Producer has no reachable demand.");
                foreach (var market in markets)
                {
                    var route = routes[market.Tile];
                    next.Add(new FoundationTradeKey(
                        group, market.Tile, route.Signature),
                        supply / markets.Length);
                }
                NormalizeGroup(next, group, supply);
            }
        }
        CheckConservation(next, production);

        var passes = 0;
        for (var pass = 0; pass < MaximumAdjustmentPasses; pass++)
        {
            var snapshot = Evaluate(next, demand);
            var replacement = new Dictionary<FoundationTradeKey, double>();
            var largestOpportunity = 0d;
            foreach (var (group, supply) in production
                .OrderBy(x => x.Key.Tile).ThenBy(x => x.Key.Commodity)
                .ThenBy(x => x.Key.Seller))
            {
                var old = next.Where(x => x.Key.Producer == group)
                    .OrderBy(x => x.Key.Destination)
                    .ThenBy(x => x.Key.Route, StringComparer.Ordinal).ToArray();
                var shortest = ShortestPaths(group.Tile, group.Seller, snapshot.Loads);
                var bestScore = double.NegativeInfinity;
                FoundationTradeKey bestKey = default;
                foreach (var market in pricedMarkets
                    .Where(m => m.Commodity == group.Commodity)
                    .OrderBy(m => m.Tile))
                {
                    if (!shortest.TryGetValue(market.Tile, out var route) ||
                        !snapshot.Prices.TryGetValue(market, out var price))
                        continue; // Q=0 is genuinely unpriced, not price-floored.
                    var score = price / (1d + route.Cost);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestKey = new FoundationTradeKey(
                            group, market.Tile, route.Signature);
                    }
                }
                if (!double.IsFinite(bestScore) || bestScore <= 0)
                    throw new InvalidOperationException("Produced commodity has no priced market.");
                var currentScore = old.Sum(x =>
                    x.Value * snapshot.Prices[(x.Key.Destination, group.Commodity)] /
                    (1d + RouteCost(x.Key.Route, group.Seller, snapshot.Loads))) / supply;
                var opportunity = Math.Max(0d, (bestScore - currentScore) / bestScore);
                largestOpportunity = Math.Max(largestOpportunity, opportunity);
                // Adaptive: large economic advantage => larger correction;
                // close to optimum => small correction / explicit stop.
                // Parameters are calibration knobs, NOT ratified F072 rules.
                var alpha = opportunity <= RelativeOpportunityTolerance ? 0d :
                    MaximumStep * opportunity / (opportunity + ResponseScale);
                foreach (var (key, mass) in old)
                    replacement[key] = mass * (1d - alpha);
                if (alpha > 0d)
                    replacement[bestKey] =
                        replacement.GetValueOrDefault(bestKey) + supply * alpha;
                NormalizeGroup(replacement, group, supply);
            }
            if (largestOpportunity <= RelativeOpportunityTolerance)
                break;
            next = replacement;
            passes++;
            CheckConservation(next, production);
        }

        var final = Evaluate(next, demand);
        var regret = MaxOpportunity(next, production, pricedMarkets, final);
        var ordered = next.Where(x => x.Value > 0d)
            .OrderBy(x => x.Key.Producer.Tile)
            .ThenBy(x => x.Key.Producer.Commodity)
            .ThenBy(x => x.Key.Producer.Seller)
            .ThenBy(x => x.Key.Destination)
            .ThenBy(x => x.Key.Route, StringComparer.Ordinal)
            .Select(x => new FoundationTrade(
                x.Key.Producer.Tile, x.Key.Destination,
                x.Key.Producer.Commodity, x.Key.Producer.Seller,
                x.Value, Array.AsReadOnly(Decode(x.Key.Route))))
            .ToArray();

        // Commit only after constructing a consistent complete economic turn.
        _allocations = next;
        _workers = nextWorkers;
        _previousMarkets = pricedMarkets;
        _turn++;
        return new FoundationTurn(_turn, nextWorkers.Values.ToArray(), ordered,
            demand, final.Delivered, final.Prices, final.Loads,
            production.Values.Sum(), regret, passes);
    }

    private double MaxOpportunity(
        Dictionary<FoundationTradeKey, double> trades,
        Dictionary<FoundationProducerKey, double> production,
        HashSet<(int Tile, int Commodity)> markets,
        FoundationSnapshot snapshot)
    {
        var largest = 0d;
        foreach (var (group, supply) in production)
        {
            var routes = ShortestPaths(group.Tile, group.Seller, snapshot.Loads);
            var best = markets.Where(m => m.Commodity == group.Commodity &&
                routes.ContainsKey(m.Tile) &&
                snapshot.Prices.ContainsKey(m))
                .Select(m => snapshot.Prices[m] / (1d + routes[m.Tile].Cost))
                .DefaultIfEmpty(0d).Max();
            if (best <= 0d)
                continue;
            var actual = trades.Where(x => x.Key.Producer == group).Sum(x =>
                x.Value * snapshot.Prices[(x.Key.Destination, group.Commodity)] /
                (1d + RouteCost(x.Key.Route, group.Seller, snapshot.Loads))) / supply;
            largest = Math.Max(largest, Math.Max(0d, (best - actual) / best));
        }
        return largest;
    }

    private FoundationSnapshot Evaluate(
        Dictionary<FoundationTradeKey, double> allocations,
        Dictionary<(int, int), double> demand)
    {
        var load = new Dictionary<int, double>();
        var delivered = new Dictionary<(int, int), double>();
        foreach (var (trade, quantity) in allocations
            .OrderBy(x => x.Key.Producer.Tile)
            .ThenBy(x => x.Key.Producer.Commodity)
            .ThenBy(x => x.Key.Producer.Seller)
            .ThenBy(x => x.Key.Destination)
            .ThenBy(x => x.Key.Route, StringComparer.Ordinal))
        {
            if (quantity < 0 || !double.IsFinite(quantity))
                throw new InvalidOperationException("Nonphysical shipment quantity.");
            var key = (trade.Destination, trade.Producer.Commodity);
            delivered[key] = delivered.GetValueOrDefault(key) + quantity;
            foreach (var id in Decode(trade.Route))
                load[id] = load.GetValueOrDefault(id) + quantity;
        }
        var prices = new Dictionary<(int, int), double>();
        foreach (var (key, desired) in demand)
        {
            // Zero Q has NO price. Unmet demand is reported separately.
            if (!delivered.TryGetValue(key, out var quantity) || quantity <= 0d)
                continue;
            var price = Math.Sqrt(desired / quantity);
            if (!double.IsFinite(price) || price <= 0d)
                throw new InvalidOperationException("Nonfinite commodity quote.");
            prices[key] = price;
        }
        return new FoundationSnapshot(load, delivered, prices);
    }

    private Dictionary<int, FoundationRoute> ShortestPaths(
        int start, int seller, Dictionary<int, double> loads)
    {
        var best = new Dictionary<int, FoundationRoute>
        {
            [start] = new FoundationRoute(0d, Array.Empty<int>(), "")
        };
        var queue = new PriorityQueue<int, (double Cost, int Hops, string Route, int Node)>();
        queue.Enqueue(start, (0d, 0, "", start));
        while (queue.TryDequeue(out var node, out var priority))
        {
            var incumbent = best[node];
            if (priority.Cost != incumbent.Cost ||
                !string.Equals(priority.Route, incumbent.Signature, StringComparison.Ordinal))
                continue;
            foreach (var (next, edgeId) in _adj[node])
            {
                var candidateCost = incumbent.Cost +
                    EdgeCost(_edges[edgeId], seller, loads.GetValueOrDefault(edgeId));
                if (!double.IsFinite(candidateCost))
                    throw new InvalidOperationException("Congestion overflow.");
                var path = incumbent.Edges.Append(edgeId).ToArray();
                var signature = Encode(path);
                var candidate = new FoundationRoute(candidateCost, path, signature);
                if (best.TryGetValue(next, out var old) &&
                    !(candidateCost < old.Cost - 1e-10 ||
                    (Math.Abs(candidateCost - old.Cost) <= 1e-10 &&
                    (path.Length < old.Edges.Length ||
                    (path.Length == old.Edges.Length &&
                    string.CompareOrdinal(signature, old.Signature) < 0)))))
                    continue;
                best[next] = candidate;
                queue.Enqueue(next, (candidateCost, path.Length, signature, next));
            }
        }
        return best;
    }

    private double RouteCost(string route, int seller, Dictionary<int, double> loads)
    {
        var cost = 0d;
        foreach (var edge in Decode(route))
            cost += EdgeCost(_edges[edge], seller, loads.GetValueOrDefault(edge));
        return cost;
    }

    private static double EdgeCost(FoundationEdge edge, int seller, double load)
    {
        // All edges use the same functional family, no artificial class
        // distinctions or hard freight capacity. External tariff is additive.
        var saturation = load / edge.ThroughputScale;
        return edge.FreeCost * (1d + Math.Pow(saturation, 4d)) +
            (edge.OwnerCivilization == seller ? 0d : edge.ExternalTariff);
    }

    private void AddNeighbor(int source, int destination, int edgeId)
    {
        if (!_adj.TryGetValue(source, out var list))
        {
            list = new List<(int, int)>();
            _adj[source] = list;
        }
        list.Add((destination, edgeId));
    }

    private void ValidateWorker(FoundationWorker worker)
    {
        if (worker is null || worker.Id < 0 || worker.Civilization < 0 ||
            worker.Tile < 0 || !_demandPerWorker.ContainsKey(worker.Commodity) ||
            !ValidNonnegative(worker.OutputPerTurn) ||
            !double.IsFinite(worker.DemandMultiplier) || worker.DemandMultiplier <= 0d ||
            !Enum.IsDefined(worker.Activity))
            throw new ArgumentException("Invalid physical worker/activity.");
    }

    private static bool ValidNonnegative(double value) =>
        double.IsFinite(value) && value >= 0d;

    private static string Encode(IEnumerable<int> edges) =>
        string.Join(",", edges.Select(edge => edge.ToString("D10",
            System.Globalization.CultureInfo.InvariantCulture)));

    private static int[] Decode(string signature) =>
        signature.Length == 0 ? Array.Empty<int>() :
            signature.Split(',').Select(part => int.Parse(part,
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    private static void NormalizeGroup(
        Dictionary<FoundationTradeKey, double> allocations,
        FoundationProducerKey group, double supply)
    {
        var keys = allocations.Keys.Where(key => key.Producer == group)
            .OrderBy(x => x.Destination)
            .ThenBy(x => x.Route, StringComparer.Ordinal).ToArray();
        if (keys.Length == 0)
            throw new InvalidOperationException("Producer allocation is empty.");
        var residual = supply - keys.Sum(key => allocations[key]);
        allocations[keys[0]] += residual;
        if (allocations[keys[0]] < -1e-9)
            throw new InvalidOperationException("Producer allocation became negative.");
        if (allocations[keys[0]] < 0d)
            allocations[keys[0]] = 0d; // float roundoff only
    }

    private static void CheckConservation(
        Dictionary<FoundationTradeKey, double> allocations,
        Dictionary<FoundationProducerKey, double> production)
    {
        foreach (var (group, supply) in production)
        {
            var actual = allocations.Where(x => x.Key.Producer == group).Sum(x => x.Value);
            if (Math.Abs(actual - supply) > 1e-10 * Math.Max(1d, supply))
                throw new InvalidOperationException("Production is not fully sold.");
        }
        if (allocations.Keys.Any(key => !production.ContainsKey(key.Producer)))
            throw new InvalidOperationException("Allocation without a physical producer.");
    }
}
