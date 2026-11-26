using Godot;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

public partial class Main
{
    // Read-only reconstruction for the no-command demographic acceptance
    // scenarios. Never solve the market or feed these numbers to the allocator.
    private Dictionary<(PhysicalTacticalTileId, CommodityId), ulong> ReadOpeningAllocationPrices(FoundationTacticalHumanAiTurnHost host)
    {
        // Only public, own-node evidence from the previous committed close.
        // The smoke has stationary, exclusively occupied nodes; do not treat
        // this projection as the global internal market history.
        var hash = host.StateHash;
        var result = new Dictionary<(PhysicalTacticalTileId, CommodityId), ulong>();
        var opening = host.Snapshot;
        var cycle = opening.Treasury.LastSettledEconomicCycleOrdinal;
        foreach (var node in opening.Workers.Workers.Where(w => w.Owner == _humanCivilization)
            .Select(w => w.TacticalTileId!.Value).Distinct())
        {
            if (opening.Workers.Workers.Any(w => w.Owner != _humanCivilization && w.TacticalTileId == node))
                throw new InvalidOperationException("Allocation audit requires exclusively owned stationary sites.");
            var local = host.GetLastClosedLocalEconomics(_humanCivilization, node);
            if (cycle == 0UL)
            {
                if (local is not null) throw new InvalidOperationException("Unexpected opening price evidence.");
                continue;
            }
            if (local is null || local.EconomicCycleOrdinal != cycle || local.Commodities.Count != 8)
                throw new InvalidOperationException("Missing prior own-node price evidence.");
            foreach (var row in local.Commodities)
                if (row.LocalPriceMicro is { } price) result.Add((node, row.Commodity), price);
        }
        if (hash != host.StateHash) throw new InvalidOperationException("Opening price read mutated state.");
        return result;
    }

    private void AuditLocalAutomaticAllocation(FoundationTacticalHumanAiTurnHost host,
        WorldState opening, FoundationTacticalCollectiveReceipt receipt,
        IReadOnlyDictionary<(PhysicalTacticalTileId, CommodityId), ulong> prices)
    {
        var hash = host.StateHash;
        var agriculture = host.AgriculturalProduction ?? throw new InvalidOperationException("Missing agricultural policy.");
        var basic = agriculture.BasicResources ?? throw new InvalidOperationException("Missing basic resource policy.");
        var fine = _tacticalPreview?.AuthoritativeFineEconomy ?? throw new InvalidOperationException("Missing signed geography.");
        var timeline = receipt.PersistentTimeline ?? throw new InvalidOperationException("Missing actual calendar.");
        var own = opening.Workers.Workers.Where(w => w.Owner == _humanCivilization).ToDictionary(w => w.Id);
        var reservations = new Dictionary<PhysicalTacticalTileId, Dictionary<(PhysicalWorkerOccupation, byte?), uint>>();
        int audited = 0, stone = 0, wood = 0, crops = 0;
        foreach (var worker in own.Values.OrderBy(w => w.Id.Value))
        {
            var queue = opening.WorkerOrders.Queues.Single(q => q.Worker == worker.Id);
            if (queue.Orders.Count != 0 || (queue.BaseActivity is not null && queue.BaseActivity.Mode != GlobalArena.Runtime.PersistentOrders.FoundationWorkerBaseMode.Automatic))
                throw new InvalidOperationException("Allocation audit only supports uncommanded automatic workers.");
            var slices = timeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
            if (slices.Length != 1 || slices[0].StartMicro != 0 || slices[0].DurationMicro != 1_000_000U)
                throw new InvalidOperationException("Allocation audit requires one whole stationary interval.");
            var actual = slices[0];
            if (worker.TacticalTileId != actual.Node)
                throw new InvalidOperationException("Allocation audit requires unchanged physical node.");
            if (fine.SurfaceAtIndex(checked((int)actual.Node.FineStrategicCellId.Value - 1)) != FoundationFineSurfaceKind.Land) continue;
            if (!reservations.TryGetValue(actual.Node, out var counts))
                reservations.Add(actual.Node, counts = new());
            var candidates = new List<(string Name, FoundationLocalBaseCandidate Value)>();
            void Add(string name, PhysicalWorkerOccupation role, byte? crop, CommodityId commodity, ulong goods)
            {
                ulong? observed = prices.TryGetValue((actual.Node, commodity), out var p) ? p : null;
                candidates.Add((name, new FoundationLocalBaseCandidate(role, crop, goods, observed, 1_000_000UL)));
            }
            Add("Wood", PhysicalWorkerOccupation.Forestry, null, basic.Wood, basic.WoodWholeTurnOutput);
            Add("Stone", PhysicalWorkerOccupation.Mining, null, basic.Stone, basic.StoneWholeTurnOutput);
            foreach (var crop in CropCatalog.All)
            {
                var output = agriculture.Resolve(actual.Node, crop.Id);
                if (output.WholeTurnOutput > 0)
                    Add(crop.Id.ToString(), PhysicalWorkerOccupation.Agriculture, (byte)crop.Id, output.Commodity, output.WholeTurnOutput);
            }
            var selected = FoundationLocalBaseRevenueRanker.Select(candidates.Select(x => x.Value), counts);
            if (selected is null || actual.Activity != PhysicalWorkerActivity.Producing ||
                selected.Occupation != actual.Occupation || selected.Crop != actual.Crop)
                throw new InvalidOperationException("Reconstructed local allocation differs from committed calendar.");
            foreach (var candidate in candidates)
            {
                var c = candidate.Value;
                uint prior = counts.GetValueOrDefault((c.Occupation, c.Crop));
                GD.Print($"GA_NEXT_R2318D_CANDIDATE cycle={receipt.Turn.Value} node={actual.Node.FineStrategicCellId.Value} worker={worker.Id.Value} item={candidate.Name} output_micro={c.PotentialGoodsMicro} observed={c.LastLocalPriceMicro.HasValue} effective_price_micro={c.EffectivePriceMicro} numerator={c.EstimatedRevenue} divisor={1UL + prior} chosen={c == selected}");
            }
            var key = (selected.Occupation, selected.Crop);
            counts[key] = checked(counts.GetValueOrDefault(key) + 1U);
            audited++;
            if (selected.Occupation == PhysicalWorkerOccupation.Mining) stone++;
            else if (selected.Occupation == PhysicalWorkerOccupation.Forestry) wood++;
            else crops++;
        }
        if (audited == 0 || audited != stone + wood + crops || hash != host.StateHash)
            throw new InvalidOperationException("Allocation audit incomplete or mutated state.");
        GD.Print($"GA_NEXT_R2318D_ALLOCATION_PASS cycle={receipt.Turn.Value} own={audited} stone={stone} wood={wood} crops={crops} actual_calendar=true");
    }
}
