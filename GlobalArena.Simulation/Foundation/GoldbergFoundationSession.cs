using System.Collections.ObjectModel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Physical market facts for ONE canonical Goldberg strategic cell. Read-only
/// presentation seam; these values are not an authoritative WorldState overlay.
/// All cells, whether inhabited or empty, obey the same economic rules.
/// </summary>
public sealed class GoldbergFoundationTileView
{
    public StrategicCellId CellId { get; }
    public int EconomicTurn { get; }
    public IReadOnlyList<FoundationWorker> Workers { get; }
    public IReadOnlyDictionary<int, double> Demand { get; }
    public IReadOnlyDictionary<int, double> Delivered { get; }
    public IReadOnlyDictionary<int, double> Prices { get; }
    public IReadOnlyList<FoundationTrade> IncomingTrades { get; }
    public IReadOnlyList<FoundationTrade> OutgoingTrades { get; }

    internal GoldbergFoundationTileView(StrategicCellId cellId, int economicTurn,
        FoundationWorker[] workers, Dictionary<int, double> demand,
        Dictionary<int, double> delivered, Dictionary<int, double> prices,
        FoundationTrade[] incoming, FoundationTrade[] outgoing)
    {
        CellId = cellId;
        EconomicTurn = economicTurn;
        Workers = Array.AsReadOnly(workers);
        Demand = new ReadOnlyDictionary<int, double>(demand);
        Delivered = new ReadOnlyDictionary<int, double>(delivered);
        Prices = new ReadOnlyDictionary<int, double>(prices);
        IncomingTrades = Array.AsReadOnly(incoming);
        OutgoingTrades = Array.AsReadOnly(outgoing);
    }
}

/// <summary>
/// World-bound, non-authoritative gameplay bridge between the EXISTING
/// deterministic Goldberg sphere/canonical game-session binding and the NEW
/// physical-worker market prototype. This bridge neither replaces nor mutates
/// WorldState, its canonical hash, its event journal, or legacy M4 economy.
/// It is a direct runnable integration seam for later domain-authoritative
/// worker identity, financial settlement and Godot inspection.
///
/// One-based StrategicCellId/StrategicEdgeId are used unchanged as the
/// foundation's physical tile/edge int identifiers. All cells are identical
/// economically; only externally supplied worker/edge conditions can differ.
/// </summary>
public sealed class GoldbergFoundationSession
{
    private readonly CanonicalGameSession _canonicalSession;
    private readonly WorldState _canonicalSnapshotAtStart;
    private readonly PhysicalWorkerTradeCycle _cycle;

    public WorldGenerationResult GeneratedWorld => _canonicalSession.GeneratedWorld;
    public RuntimeWorldBinding WorldBinding { get; }
    public int WorldNodeCount { get; }
    public int WorldEdgeCount { get; }
    public int EconomicTurn => _cycle.CurrentTurn;
    public IReadOnlyList<FoundationWorker> Workers => _cycle.Workers;
    public FoundationTurn? LastEconomicTurn { get; private set; }
    public bool InitialMarketCalibrated => _cycle.InitialMarketCalibrated;

    public GoldbergFoundationSession(
        CanonicalGameSession canonicalSession,
        IEnumerable<FoundationWorker> workers,
        IReadOnlyDictionary<int, double> demandPerWorker,
        double freeTransportCost = 0.4d,
        double throughputScale = 2d,
        int maximumAdjustmentPasses = 8,
        double relativeOpportunityTolerance = 0.001d)
    {
        _canonicalSession = canonicalSession ??
            throw new ArgumentNullException(nameof(canonicalSession));
        ArgumentNullException.ThrowIfNull(workers);
        ArgumentNullException.ThrowIfNull(demandPerWorker);

        var generated = canonicalSession.GeneratedWorld;
        WorldBinding = RuntimeWorldBinding.FromGeneratedWorld(generated);
        _canonicalSnapshotAtStart = canonicalSession.Snapshot;
        if (_canonicalSnapshotAtStart.WorldBinding != WorldBinding)
            throw new InvalidOperationException(
                "Generated Goldberg world and authoritative world binding disagree.");

        var graph = generated.StrategicSurfaceGraph;
        WorldNodeCount = graph.NodeCount;
        WorldEdgeCount = generated.StrategicTopology.Edges.Count;
        if (WorldNodeCount == 0 || WorldNodeCount > int.MaxValue ||
            WorldEdgeCount > int.MaxValue)
            throw new NotSupportedException(
                "Prototype IDs require Goldberg node/edge counts representable in Int32.");

        // All geometry comes from the existing canonical topology. No
        // synthetic nodes, privileged markets or distance cutoff.
        var edges = generated.StrategicTopology.Edges
            .Select(edge => new FoundationEdge(
                checked((int)edge.Id.Value),
                checked((int)edge.IncidentCellIds[0].Value),
                checked((int)edge.IncidentCellIds[1].Value),
                freeTransportCost, throughputScale))
            .ToArray();

        var physicalWorkers = workers.ToArray();
        foreach (var worker in physicalWorkers)
        {
            if (worker is null || worker.Tile <= 0 ||
                !WorldBinding.Contains(new StrategicCellId(checked((ulong)worker.Tile))))
                throw new ArgumentException(
                    "Every physical worker must occupy an existing canonical Goldberg cell.",
                    nameof(workers));
        }

        _cycle = new PhysicalWorkerTradeCycle(edges, physicalWorkers,
            demandPerWorker, maximumAdjustmentPasses,
            relativeOpportunityTolerance);
    }

    /// <summary>
    /// A one-time option, NEVER implicit and never a real game turn.
    /// Exactly the same restrictions and unapproved precision as PR #352.
    /// </summary>
    public FoundationInitialCalibration CalibrateInitialMarket()
    {
        VerifyCanonicalSnapshotUnchanged();
        return _cycle.CalibrateInitialMarket();
    }

    /// <summary>
    /// Every worker order moves on an ACTUAL Goldberg incident edge. The
    /// physical market state advances; the authoritative session does not.
    /// </summary>
    public FoundationTurn Advance(IEnumerable<FoundationWorkerOrder>? orders = null)
    {
        VerifyCanonicalSnapshotUnchanged();
        var result = _cycle.Advance(orders);
        LastEconomicTurn = result;
        return result;
    }

    /// <summary>
    /// A read-only view of the exact selected Goldberg cell, ready for a
    /// future Godot panel to request on click. Reading does not resolve a turn.
    /// </summary>
    public GoldbergFoundationTileView InspectTile(StrategicCellId cellId)
    {
        VerifyCanonicalSnapshotUnchanged();
        if (!WorldBinding.Contains(cellId))
            throw new ArgumentOutOfRangeException(nameof(cellId),
                "Inspection requires a real cell in the bound Goldberg world.");
        var tile = checked((int)cellId.Value);
        var workers = Workers.Where(worker => worker.Tile == tile)
            .OrderBy(worker => worker.Id).ToArray();
        var demand = LastEconomicTurn?.Demand
            .Where(item => item.Key.Tile == tile)
            .ToDictionary(item => item.Key.Commodity, item => item.Value)
            ?? new Dictionary<int, double>();
        var delivered = LastEconomicTurn?.Delivered
            .Where(item => item.Key.Tile == tile)
            .ToDictionary(item => item.Key.Commodity, item => item.Value)
            ?? new Dictionary<int, double>();
        var prices = LastEconomicTurn?.Prices
            .Where(item => item.Key.Tile == tile)
            .ToDictionary(item => item.Key.Commodity, item => item.Value)
            ?? new Dictionary<int, double>();
        var incoming = LastEconomicTurn?.Trades
            .Where(trade => trade.Destination == tile)
            .OrderBy(trade => trade.Commodity)
            .ThenBy(trade => trade.Origin)
            .ThenBy(trade => trade.Seller).ToArray()
            ?? Array.Empty<FoundationTrade>();
        var outgoing = LastEconomicTurn?.Trades
            .Where(trade => trade.Origin == tile)
            .OrderBy(trade => trade.Commodity)
            .ThenBy(trade => trade.Destination)
            .ThenBy(trade => trade.Seller).ToArray()
            ?? Array.Empty<FoundationTrade>();
        return new GoldbergFoundationTileView(cellId, EconomicTurn,
            workers, demand, delivered, prices, incoming, outgoing);
    }

    private void VerifyCanonicalSnapshotUnchanged()
    {
        // A different authoritative snapshot may carry new worker/territory
        // orders that this non-authoritative prototype has not consumed.
        // Fail closed instead of silently presenting stale economic overlay.
        if (!ReferenceEquals(_canonicalSnapshotAtStart, _canonicalSession.Snapshot) ||
            _canonicalSession.Snapshot.WorldBinding != WorldBinding)
            throw new InvalidOperationException(
                "Authoritative WorldState changed; foundation preview requires an explicit rebind.");
    }
}
