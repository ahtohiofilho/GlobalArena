using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class EconomicMarketAllocationResolver
{
    public EconomicMarketAllocationResult Resolve(
        EconomicProductionDemandResult productionDemand,
        IEnumerable<EconomicTransferSignal> transferSignals,
        EconomicMarketAllocationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(
            productionDemand);

        ArgumentNullException.ThrowIfNull(
            transferSignals);

        ArgumentNullException.ThrowIfNull(
            policy);

        var signals =
            transferSignals.ToArray();

        if (signals.Any(
            signal =>
                signal is null))
        {
            throw new ArgumentException(
                "Transfer signal collection cannot contain null entries.",
                nameof(transferSignals));
        }

        signals =
            signals
                .OrderBy(
                    signal =>
                        signal.RouteKey.SourceEconomicPointId.Value)
                .ThenBy(
                    signal =>
                        signal.RouteKey.DestinationEconomicPointId.Value)
                .ToArray();

        for (var index = 1;
             index < signals.Length;
             index++)
        {
            if (signals[index - 1].RouteKey
                == signals[index].RouteKey)
            {
                throw new ArgumentException(
                    "Transfer signal collection cannot contain duplicate directional route keys.",
                    nameof(transferSignals));
            }
        }

        ValidateTransferSignalEndpoints(
            productionDemand,
            signals);

        var supplyCommodityIds =
            productionDemand
                .SupplyFlows
                .Select(
                    flow =>
                        flow.CommodityId)
                .Distinct()
                .ToHashSet();

        var commodityIds =
            productionDemand
                .DemandFlows
                .Select(
                    flow =>
                        flow.CommodityId)
                .Where(
                    supplyCommodityIds.Contains)
                .Distinct()
                .OrderBy(
                    commodityId =>
                        commodityId.Value)
                .ToArray();

        var allocations =
            new List<EconomicTradeAllocation>();

        foreach (var commodityId in
            commodityIds)
        {
            ResolveCommodity(
                productionDemand,
                signals,
                policy,
                commodityId,
                allocations);
        }

        return new EconomicMarketAllocationResult(
            allocations);
    }

    private static void ValidateTransferSignalEndpoints(
        EconomicProductionDemandResult productionDemand,
        IReadOnlyList<EconomicTransferSignal> signals)
    {
        var supplyPointIds =
            productionDemand
                .SupplyFlows
                .Select(
                    flow =>
                        flow.EconomicPointId)
                .ToHashSet();

        var demandPointIds =
            productionDemand
                .DemandFlows
                .Select(
                    flow =>
                        flow.EconomicPointId)
                .ToHashSet();

        foreach (var signal in
            signals)
        {
            if (!supplyPointIds.Contains(
                signal.RouteKey.SourceEconomicPointId))
            {
                throw new ArgumentException(
                    $"Transfer signal source EconomicPoint {signal.RouteKey.SourceEconomicPointId.Value} has no supply flow.",
                    nameof(signals));
            }

            if (!demandPointIds.Contains(
                signal.RouteKey.DestinationEconomicPointId))
            {
                throw new ArgumentException(
                    $"Transfer signal destination EconomicPoint {signal.RouteKey.DestinationEconomicPointId.Value} has no demand flow.",
                    nameof(signals));
            }
        }
    }

    private static void ResolveCommodity(
        EconomicProductionDemandResult productionDemand,
        IReadOnlyList<EconomicTransferSignal> signals,
        EconomicMarketAllocationPolicy policy,
        CommodityId commodityId,
        List<EconomicTradeAllocation> allocations)
    {
        var supplies =
            productionDemand
                .SupplyFlows
                .Where(
                    flow =>
                        flow.CommodityId == commodityId)
                .OrderBy(
                    flow =>
                        flow.EconomicPointId.Value)
                .ThenBy(
                    flow =>
                        (byte)flow.Activity)
                .ToArray();

        var demands =
            productionDemand
                .DemandFlows
                .Where(
                    flow =>
                        flow.CommodityId == commodityId)
                .OrderBy(
                    flow =>
                        flow.EconomicPointId.Value)
                .ToArray();

        if (supplies.Length == 0
            || demands.Length == 0)
        {
            return;
        }

        var supplyPointIds =
            supplies
                .Select(
                    flow =>
                        flow.EconomicPointId)
                .ToHashSet();

        var demandPointIds =
            demands
                .Select(
                    flow =>
                        flow.EconomicPointId)
                .ToHashSet();

        var relevantSignals =
            signals
                .Where(
                    signal =>
                        supplyPointIds.Contains(
                            signal.RouteKey.SourceEconomicPointId)
                        && demandPointIds.Contains(
                            signal.RouteKey.DestinationEconomicPointId))
                .OrderBy(
                    signal =>
                        signal.RouteKey.SourceEconomicPointId.Value)
                .ThenBy(
                    signal =>
                        signal.RouteKey.DestinationEconomicPointId.Value)
                .ToArray();

        if (relevantSignals.Length == 0)
        {
            return;
        }

        var network =
            new ResidualNetwork();

        var sourceNode =
            network.AddNode();

        var supplyContexts =
            supplies
                .Select(
                    flow =>
                        new SupplyContext(
                            flow,
                            network.AddNode()))
                .ToArray();

        var demandContexts =
            demands
                .Select(
                    flow =>
                        new DemandContext(
                            flow,
                            network.AddNode()))
                .ToArray();

        var demandByPoint =
            demandContexts.ToDictionary(
                context =>
                    context.Flow.EconomicPointId);

        var routeContexts =
            relevantSignals
                .Select(
                    signal =>
                        new RouteContext(
                            signal,
                            network.AddNode(),
                            demandByPoint[
                                signal.RouteKey.DestinationEconomicPointId]))
                .ToArray();

        var sinkNode =
            network.AddNode();

        foreach (var supplyContext in
            supplyContexts)
        {
            supplyContext.SourceEdge =
                network.AddEdge(
                    sourceNode,
                    supplyContext.Node,
                    supplyContext.Flow.RawQuantity,
                    0);
        }

        var connectors =
            new List<ConnectorContext>();

        foreach (var supplyContext in
            supplyContexts)
        {
            foreach (var routeContext in
                routeContexts)
            {
                if (routeContext.Signal.RouteKey.SourceEconomicPointId
                    != supplyContext.Flow.EconomicPointId)
                {
                    continue;
                }

                var edge =
                    network.AddEdge(
                        supplyContext.Node,
                        routeContext.Node,
                        supplyContext.Flow.RawQuantity,
                        0);

                var connector =
                    new ConnectorContext(
                        supplyContext,
                        routeContext,
                        edge);

                supplyContext.Connectors.Add(
                    connector);

                routeContext.Connectors.Add(
                    connector);

                connectors.Add(
                    connector);
            }
        }

        foreach (var routeContext in
            routeContexts)
        {
            var quantityBands =
                CreateQuantityBands(
                    routeContext.Signal.RawEffectiveCapacity,
                    policy.TransferBandCount);

            foreach (var band in
                quantityBands)
            {
                var rawUnitTransferCost =
                    routeContext.Signal.GetRawMarginalUnitCost(
                        band.RawUpperEndpoint);

                var edge =
                    network.AddEdge(
                        routeContext.Node,
                        routeContext.Destination.Node,
                        band.RawQuantity,
                        (Int128)rawUnitTransferCost);

                routeContext.Bands.Add(
                    new RouteBandContext(
                        edge,
                        rawUnitTransferCost));
            }
        }

        foreach (var demandContext in
            demandContexts)
        {
            var quantityBands =
                CreateQuantityBands(
                    demandContext.Flow.RawQuantity,
                    policy.DemandBandCount);

            foreach (var band in
                quantityBands)
            {
                var rawUnitValue =
                    GetDemandBandUnitValue(
                        policy,
                        demandContext.Flow.RawQuantity,
                        band.RawUpperEndpoint);

                var edge =
                    network.AddEdge(
                        demandContext.Node,
                        sinkNode,
                        band.RawQuantity,
                        -(Int128)rawUnitValue);

                demandContext.Bands.Add(
                    new DemandBandContext(
                        edge,
                        rawUnitValue));
            }
        }

        while (TryFindProfitablePath(
            network,
            sourceNode,
            sinkNode,
            out var path))
        {
            var rawAugment =
                path
                    .Min(
                        edge =>
                            edge.ResidualCapacity);

            if (rawAugment == 0UL)
            {
                throw new InvalidOperationException(
                    "Market allocation residual path has zero augmenting capacity.");
            }

            foreach (var edge in
                path)
            {
                edge.ResidualCapacity -=
                    rawAugment;

                edge.Reverse!.ResidualCapacity =
                    checked(
                        edge.Reverse.ResidualCapacity
                        + rawAugment);
            }
        }

        ValidateFlowConservation(
            supplyContexts,
            routeContexts,
            demandContexts);

        BuildCanonicalAllocations(
            commodityId,
            routeContexts,
            demandContexts,
            allocations);
    }

    private static IReadOnlyList<QuantityBand> CreateQuantityBands(
        ulong rawQuantity,
        int requestedBandCount)
    {
        if (rawQuantity == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawQuantity),
                "Band quantity must be positive.");
        }

        var actualBandCount =
            rawQuantity < (ulong)requestedBandCount
                ? checked((int)rawQuantity)
                : requestedBandCount;

        var baseQuantity =
            rawQuantity
            / (ulong)actualBandCount;

        var remainder =
            rawQuantity
            % (ulong)actualBandCount;

        var bands =
            new List<QuantityBand>(
                actualBandCount);

        ulong cumulative = 0UL;

        for (var index = 0;
             index < actualBandCount;
             index++)
        {
            var rawBandQuantity =
                baseQuantity
                + ((ulong)index < remainder
                    ? 1UL
                    : 0UL);

            cumulative =
                checked(
                    cumulative
                    + rawBandQuantity);

            bands.Add(
                new QuantityBand(
                    rawBandQuantity,
                    cumulative));
        }

        if (cumulative
            != rawQuantity)
        {
            throw new InvalidOperationException(
                "Deterministic quantity band partition did not preserve total quantity.");
        }

        return bands;
    }

    private static ulong GetDemandBandUnitValue(
        EconomicMarketAllocationPolicy policy,
        ulong rawDemandQuantity,
        ulong rawUpperEndpoint)
    {
        var spread =
            policy.RawInitialUnitValue
            - policy.RawFloorUnitValue;

        var rawDrop =
            ((UInt128)spread
                * rawUpperEndpoint)
            / rawDemandQuantity;

        return policy.RawInitialUnitValue
            - (ulong)rawDrop;
    }

    private static bool TryFindProfitablePath(
        ResidualNetwork network,
        int sourceNode,
        int sinkNode,
        out IReadOnlyList<ResidualEdge> path)
    {
        var nodeCount =
            network.NodeCount;

        var reachable =
            new bool[nodeCount];

        var distances =
            new Int128[nodeCount];

        var predecessor =
            new ResidualEdge?[nodeCount];

        reachable[sourceNode] =
            true;

        for (var iteration = 0;
             iteration < nodeCount - 1;
             iteration++)
        {
            var changed =
                false;

            for (var fromNode = 0;
                 fromNode < nodeCount;
                 fromNode++)
            {
                if (!reachable[fromNode])
                {
                    continue;
                }

                foreach (var edge in
                    network.GetEdges(
                        fromNode))
                {
                    if (edge.ResidualCapacity == 0UL)
                    {
                        continue;
                    }

                    var candidate =
                        checked(
                            distances[fromNode]
                            + edge.UnitCost);

                    if (!reachable[edge.To]
                        || candidate < distances[edge.To])
                    {
                        reachable[edge.To] =
                            true;

                        distances[edge.To] =
                            candidate;

                        predecessor[edge.To] =
                            edge;

                        changed =
                            true;
                    }
                }
            }

            if (!changed)
            {
                break;
            }
        }

        for (var fromNode = 0;
             fromNode < nodeCount;
             fromNode++)
        {
            if (!reachable[fromNode])
            {
                continue;
            }

            foreach (var edge in
                network.GetEdges(
                    fromNode))
            {
                if (edge.ResidualCapacity == 0UL
                    || !reachable[edge.To])
                {
                    continue;
                }

                var candidate =
                    checked(
                        distances[fromNode]
                        + edge.UnitCost);

                if (candidate
                    < distances[edge.To])
                {
                    throw new InvalidOperationException(
                        "Market allocation residual network contains an unexpected negative cycle.");
                }
            }
        }

        if (!reachable[sinkNode]
            || distances[sinkNode] >= 0)
        {
            path =
                Array.Empty<ResidualEdge>();

            return false;
        }

        var reversePath =
            new List<ResidualEdge>();

        var cursor =
            sinkNode;

        var stepCount =
            0;

        while (cursor
            != sourceNode)
        {
            if (stepCount
                >= nodeCount)
            {
                throw new InvalidOperationException(
                    "Market allocation predecessor chain contains a cycle.");
            }

            var edge =
                predecessor[cursor];

            if (edge is null)
            {
                throw new InvalidOperationException(
                    "Market allocation profitable path reconstruction encountered a missing predecessor.");
            }

            reversePath.Add(
                edge);

            cursor =
                edge.From;

            stepCount++;
        }

        reversePath.Reverse();

        path =
            reversePath;

        return true;
    }

    private static void ValidateFlowConservation(
        IReadOnlyList<SupplyContext> supplies,
        IReadOnlyList<RouteContext> routes,
        IReadOnlyList<DemandContext> demands)
    {
        foreach (var supply in
            supplies)
        {
            var sourceFlow =
                GetForwardFlow(
                    supply.SourceEdge!);

            UInt128 connectorFlow = 0U;

            foreach (var connector in
                supply.Connectors)
            {
                connectorFlow +=
                    GetForwardFlow(
                        connector.Edge);
            }

            if (connectorFlow
                != sourceFlow)
            {
                throw new InvalidOperationException(
                    "Market allocation producer flow conservation failed.");
            }
        }

        foreach (var route in
            routes)
        {
            UInt128 connectorFlow = 0U;
            UInt128 bandFlow = 0U;

            foreach (var connector in
                route.Connectors)
            {
                connectorFlow +=
                    GetForwardFlow(
                        connector.Edge);
            }

            foreach (var band in
                route.Bands)
            {
                bandFlow +=
                    GetForwardFlow(
                        band.Edge);
            }

            if (connectorFlow
                != bandFlow)
            {
                throw new InvalidOperationException(
                    "Market allocation route flow conservation failed.");
            }
        }

        foreach (var demand in
            demands)
        {
            UInt128 inbound = 0U;
            UInt128 outbound = 0U;

            foreach (var route in
                routes)
            {
                if (route.Destination
                    != demand)
                {
                    continue;
                }

                foreach (var band in
                    route.Bands)
                {
                    inbound +=
                        GetForwardFlow(
                            band.Edge);
                }
            }

            foreach (var band in
                demand.Bands)
            {
                outbound +=
                    GetForwardFlow(
                        band.Edge);
            }

            if (inbound
                != outbound)
            {
                throw new InvalidOperationException(
                    "Market allocation demand flow conservation failed.");
            }
        }
    }

    private static void BuildCanonicalAllocations(
        CommodityId commodityId,
        IReadOnlyList<RouteContext> routes,
        IReadOnlyList<DemandContext> demands,
        List<EconomicTradeAllocation> output)
    {
        var aggregate =
            new Dictionary<AllocationKey, ulong>();

        foreach (var demand in
            demands)
        {
            var costChunks =
                new List<CostChunk>();

            foreach (var route in
                routes
                    .Where(
                        route =>
                            route.Destination == demand)
                    .OrderBy(
                        route =>
                            route.Signal.RouteKey.SourceEconomicPointId.Value)
                    .ThenBy(
                        route =>
                            route.Signal.RouteKey.DestinationEconomicPointId.Value))
            {
                var connectorRemainders =
                    route
                        .Connectors
                        .Select(
                            connector =>
                                new ConnectorRemainder(
                                    connector,
                                    GetForwardFlow(
                                        connector.Edge)))
                        .Where(
                            remainder =>
                                remainder.RawRemaining > 0UL)
                        .OrderBy(
                            remainder =>
                                (byte)remainder.Connector.Supply.Flow.Activity)
                        .ToList();

                var connectorIndex =
                    0;

                foreach (var band in
                    route
                        .Bands
                        .OrderBy(
                            band =>
                                band.RawUnitTransferCost))
                {
                    var bandRemaining =
                        GetForwardFlow(
                            band.Edge);

                    while (bandRemaining > 0UL)
                    {
                        while (connectorIndex < connectorRemainders.Count
                            && connectorRemainders[connectorIndex].RawRemaining == 0UL)
                        {
                            connectorIndex++;
                        }

                        if (connectorIndex
                            >= connectorRemainders.Count)
                        {
                            throw new InvalidOperationException(
                                "Route cost-band flow cannot be attributed to source activities.");
                        }

                        var connectorRemainder =
                            connectorRemainders[connectorIndex];

                        var rawChunk =
                            Math.Min(
                                bandRemaining,
                                connectorRemainder.RawRemaining);

                        costChunks.Add(
                            new CostChunk(
                                connectorRemainder.Connector.Supply.Flow.EconomicPointId,
                                connectorRemainder.Connector.Supply.Flow.Activity,
                                demand.Flow.EconomicPointId,
                                rawChunk,
                                band.RawUnitTransferCost));

                        bandRemaining -=
                            rawChunk;

                        connectorRemainder.RawRemaining -=
                            rawChunk;
                    }
                }

                if (connectorRemainders.Any(
                    remainder =>
                        remainder.RawRemaining != 0UL))
                {
                    throw new InvalidOperationException(
                        "Source activity flow remains after route cost-band attribution.");
                }
            }

            costChunks =
                costChunks
                    .OrderByDescending(
                        chunk =>
                            chunk.RawUnitTransferCost)
                    .ThenBy(
                        chunk =>
                            chunk.SourceEconomicPointId.Value)
                    .ThenBy(
                        chunk =>
                            (byte)chunk.SourceActivity)
                    .ToList();

            var valueChunks =
                demand
                    .Bands
                    .Select(
                        band =>
                            new ValueChunk(
                                GetForwardFlow(
                                    band.Edge),
                                band.RawUnitValue))
                    .Where(
                        chunk =>
                            chunk.RawRemaining > 0UL)
                    .OrderByDescending(
                        chunk =>
                            chunk.RawUnitValue)
                    .ToList();

            var costIndex =
                0;

            var valueIndex =
                0;

            while (costIndex < costChunks.Count
                && valueIndex < valueChunks.Count)
            {
                var costChunk =
                    costChunks[costIndex];

                var valueChunk =
                    valueChunks[valueIndex];

                if (valueChunk.RawUnitValue
                    <= costChunk.RawUnitTransferCost)
                {
                    throw new InvalidOperationException(
                        "Final market flow cannot be decomposed into strictly positive unit-net-return settlements.");
                }

                var rawChunk =
                    Math.Min(
                        costChunk.RawRemaining,
                        valueChunk.RawRemaining);

                var key =
                    new AllocationKey(
                        commodityId,
                        costChunk.SourceEconomicPointId,
                        costChunk.SourceActivity,
                        costChunk.DestinationEconomicPointId,
                        valueChunk.RawUnitValue,
                        costChunk.RawUnitTransferCost);

                if (aggregate.TryGetValue(
                    key,
                    out var existing))
                {
                    aggregate[key] =
                        checked(
                            existing
                            + rawChunk);
                }
                else
                {
                    aggregate.Add(
                        key,
                        rawChunk);
                }

                costChunk.RawRemaining -=
                    rawChunk;

                valueChunk.RawRemaining -=
                    rawChunk;

                if (costChunk.RawRemaining == 0UL)
                {
                    costIndex++;
                }

                if (valueChunk.RawRemaining == 0UL)
                {
                    valueIndex++;
                }
            }

            if (costChunks.Any(
                    chunk =>
                        chunk.RawRemaining != 0UL)
                || valueChunks.Any(
                    chunk =>
                        chunk.RawRemaining != 0UL))
            {
                throw new InvalidOperationException(
                    "Market settlement decomposition did not preserve demand flow quantity.");
            }
        }

        foreach (var pair in
            aggregate
                .OrderBy(
                    pair =>
                        pair.Key.CommodityId.Value)
                .ThenBy(
                    pair =>
                        pair.Key.SourceEconomicPointId.Value)
                .ThenBy(
                    pair =>
                        (byte)pair.Key.SourceActivity)
                .ThenBy(
                    pair =>
                        pair.Key.DestinationEconomicPointId.Value)
                .ThenByDescending(
                    pair =>
                        pair.Key.RawUnitValue)
                .ThenBy(
                    pair =>
                        pair.Key.RawUnitTransferCost))
        {
            output.Add(
                new EconomicTradeAllocation(
                    pair.Key.SourceEconomicPointId,
                    pair.Key.SourceActivity,
                    pair.Key.DestinationEconomicPointId,
                    pair.Key.CommodityId,
                    pair.Value,
                    pair.Key.RawUnitValue,
                    pair.Key.RawUnitTransferCost));
        }
    }

    private static ulong GetForwardFlow(
        ResidualEdge edge)
    {
        if (!edge.IsOriginal)
        {
            throw new ArgumentException(
                "Forward flow can only be read from original residual-network edges.",
                nameof(edge));
        }

        if (edge.ResidualCapacity
            > edge.OriginalCapacity)
        {
            throw new InvalidOperationException(
                "Residual edge capacity exceeds original capacity.");
        }

        return edge.OriginalCapacity
            - edge.ResidualCapacity;
    }

    private readonly record struct QuantityBand(
        ulong RawQuantity,
        ulong RawUpperEndpoint);

    private readonly record struct AllocationKey(
        CommodityId CommodityId,
        EconomicPointId SourceEconomicPointId,
        EconomicActivityKind SourceActivity,
        EconomicPointId DestinationEconomicPointId,
        ulong RawUnitValue,
        ulong RawUnitTransferCost);

    private sealed class SupplyContext
    {
        public EconomicSupplyFlow Flow { get; }

        public int Node { get; }

        public ResidualEdge? SourceEdge { get; set; }

        public List<ConnectorContext> Connectors { get; } =
            new();

        public SupplyContext(
            EconomicSupplyFlow flow,
            int node)
        {
            Flow = flow;
            Node = node;
        }
    }

    private sealed class DemandContext
    {
        public EconomicDemandFlow Flow { get; }

        public int Node { get; }

        public List<DemandBandContext> Bands { get; } =
            new();

        public DemandContext(
            EconomicDemandFlow flow,
            int node)
        {
            Flow = flow;
            Node = node;
        }
    }

    private sealed class RouteContext
    {
        public EconomicTransferSignal Signal { get; }

        public int Node { get; }

        public DemandContext Destination { get; }

        public List<ConnectorContext> Connectors { get; } =
            new();

        public List<RouteBandContext> Bands { get; } =
            new();

        public RouteContext(
            EconomicTransferSignal signal,
            int node,
            DemandContext destination)
        {
            Signal = signal;
            Node = node;
            Destination = destination;
        }
    }

    private sealed class ConnectorContext
    {
        public SupplyContext Supply { get; }

        public RouteContext Route { get; }

        public ResidualEdge Edge { get; }

        public ConnectorContext(
            SupplyContext supply,
            RouteContext route,
            ResidualEdge edge)
        {
            Supply = supply;
            Route = route;
            Edge = edge;
        }
    }

    private sealed class RouteBandContext
    {
        public ResidualEdge Edge { get; }

        public ulong RawUnitTransferCost { get; }

        public RouteBandContext(
            ResidualEdge edge,
            ulong rawUnitTransferCost)
        {
            Edge = edge;
            RawUnitTransferCost = rawUnitTransferCost;
        }
    }

    private sealed class DemandBandContext
    {
        public ResidualEdge Edge { get; }

        public ulong RawUnitValue { get; }

        public DemandBandContext(
            ResidualEdge edge,
            ulong rawUnitValue)
        {
            Edge = edge;
            RawUnitValue = rawUnitValue;
        }
    }

    private sealed class ConnectorRemainder
    {
        public ConnectorContext Connector { get; }

        public ulong RawRemaining { get; set; }

        public ConnectorRemainder(
            ConnectorContext connector,
            ulong rawRemaining)
        {
            Connector = connector;
            RawRemaining = rawRemaining;
        }
    }

    private sealed class CostChunk
    {
        public EconomicPointId SourceEconomicPointId { get; }

        public EconomicActivityKind SourceActivity { get; }

        public EconomicPointId DestinationEconomicPointId { get; }

        public ulong RawRemaining { get; set; }

        public ulong RawUnitTransferCost { get; }

        public CostChunk(
            EconomicPointId sourceEconomicPointId,
            EconomicActivityKind sourceActivity,
            EconomicPointId destinationEconomicPointId,
            ulong rawRemaining,
            ulong rawUnitTransferCost)
        {
            SourceEconomicPointId = sourceEconomicPointId;
            SourceActivity = sourceActivity;
            DestinationEconomicPointId = destinationEconomicPointId;
            RawRemaining = rawRemaining;
            RawUnitTransferCost = rawUnitTransferCost;
        }
    }

    private sealed class ValueChunk
    {
        public ulong RawRemaining { get; set; }

        public ulong RawUnitValue { get; }

        public ValueChunk(
            ulong rawRemaining,
            ulong rawUnitValue)
        {
            RawRemaining = rawRemaining;
            RawUnitValue = rawUnitValue;
        }
    }

    private sealed class ResidualNetwork
    {
        private readonly List<List<ResidualEdge>> _edges =
            new();

        private int _nextSequence;

        public int NodeCount =>
            _edges.Count;

        public int AddNode()
        {
            _edges.Add(
                new List<ResidualEdge>());

            return _edges.Count - 1;
        }

        public ResidualEdge AddEdge(
            int from,
            int to,
            ulong capacity,
            Int128 unitCost)
        {
            if (capacity == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity),
                    "Residual-network forward edge capacity must be positive.");
            }

            var forward =
                new ResidualEdge(
                    from,
                    to,
                    capacity,
                    capacity,
                    unitCost,
                    _nextSequence++,
                    true);

            var reverse =
                new ResidualEdge(
                    to,
                    from,
                    0UL,
                    0UL,
                    -unitCost,
                    _nextSequence++,
                    false);

            forward.Reverse =
                reverse;

            reverse.Reverse =
                forward;

            _edges[from].Add(
                forward);

            _edges[to].Add(
                reverse);

            return forward;
        }

        public IReadOnlyList<ResidualEdge> GetEdges(
            int node)
        {
            return _edges[node];
        }
    }

    private sealed class ResidualEdge
    {
        public int From { get; }

        public int To { get; }

        public ulong OriginalCapacity { get; }

        public ulong ResidualCapacity { get; set; }

        public Int128 UnitCost { get; }

        public int Sequence { get; }

        public bool IsOriginal { get; }

        public ResidualEdge? Reverse { get; set; }

        public ResidualEdge(
            int from,
            int to,
            ulong originalCapacity,
            ulong residualCapacity,
            Int128 unitCost,
            int sequence,
            bool isOriginal)
        {
            From = from;
            To = to;
            OriginalCapacity = originalCapacity;
            ResidualCapacity = residualCapacity;
            UnitCost = unitCost;
            Sequence = sequence;
            IsOriginal = isOriginal;
        }
    }
}
