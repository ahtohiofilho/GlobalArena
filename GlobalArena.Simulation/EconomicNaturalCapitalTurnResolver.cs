using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class EconomicNaturalCapitalTurnResult
{
    public EconomicProductionDemandResult RealizedFlows { get; }
    public EconomicNaturalCapitalBook NextBook { get; }

    internal EconomicNaturalCapitalTurnResult(
        EconomicProductionDemandResult realizedFlows,
        EconomicNaturalCapitalBook nextBook)
    {
        RealizedFlows = realizedFlows;
        NextBook = nextBook;
    }
}

/// <summary>
/// G3-F opt-in ecological cycle. Production is constrained by actual in-situ
/// wood/stone availability and existing soil vitality. No synthetic buyer,
/// auto-reserve of unsold output, facility, or WorldState mutation.
/// </summary>
public sealed class EconomicNaturalCapitalTurnResolver
{
    private readonly EconomicNaturalEndowmentWorkerEconomyResolver _base =
        new(StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion);

    public EconomicNaturalCapitalTurnResult Resolve(
        WorldGenerationResult generatedWorld, WorldState world,
        EconomicNaturalCapitalBook previous, EconomicNaturalCapitalPolicy policy,
        EconomicPrimitiveCommodityMapping commodities,
        IEnumerable<EconomicDemandProfile> workerDemand,
        ulong turnOrdinal)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(commodities);
        ArgumentNullException.ThrowIfNull(workerDemand);

        if (turnOrdinal == 0UL ||
            previous.LastAppliedTurn == ulong.MaxValue ||
            turnOrdinal != previous.LastAppliedTurn + 1UL)
            throw new InvalidOperationException(
                "Natural capital must be resolved sequentially exactly once per committed turn.");
        if (world.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException("Natural capital requires a matching bound world.");

        var geography = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generatedWorld);
        var graph = generatedWorld.StrategicSurfaceGraph;
        if (previous.Cells.Count != graph.NodeCount)
            throw new InvalidOperationException("Natural capital must cover every strategic cell.");

        const ulong d = (ulong)StrategicScalarField.Denominator;
        var local = previous.Cells.ToDictionary(x => x.CellId);
        for (var i = 0; i < graph.NodeCount; i++)
        {
            var cell = graph.GetCellId(i);
            if (!local.TryGetValue(cell, out var state))
                throw new InvalidOperationException("Natural capital contains unknown or absent cells.");

            var carrying = checked((ulong)(
                ((UInt128)policy.RawWoodAtFullEndowment *
                    (ulong)geography.WoodyVegetation.GetRawValue(cell)) / d));
            var stoneBaseline = checked((ulong)(
                ((UInt128)policy.RawStoneAtFullEndowment *
                    (ulong)geography.ExposedStone.GetRawValue(cell)) / d));
            var soilBaseline = geography.SoilSuitability.GetRawValue(cell);
            if (state.RawWoodCarryingCapacity != carrying ||
                state.RawSurfaceStone > stoneBaseline ||
                state.RawSoilVitality > soilBaseline)
                throw new InvalidOperationException(
                    "Natural capital conflicts with its generated environmental endowment.");
        }

        // Only actual potential production is harvested, and demand remains
        // the sum of all workers regardless of activity/productive output.
        var baseline = _base.Resolve(
            generatedWorld, world, commodities, workerDemand);
        var points = world.Economy.EconomicPoints.ToDictionary(x => x.Id);
        var realized = new Dictionary<EconomicSupplyFlow, ulong>();
        var consumed = new Dictionary<(StrategicCellId Cell, EconomicActivityKind Kind), ulong>();

        foreach (var group in baseline.SupplyFlows.GroupBy(x => (
            Cell: points[x.EconomicPointId].StrategicCellId, Kind: x.Activity)))
        {
            var cell = group.Key.Cell;
            var state = local[cell];
            var rows = group.OrderBy(x => x.EconomicPointId.Value).ToArray();
            ulong available;

            switch (group.Key.Kind)
            {
                case EconomicActivityKind.Agriculture:
                    var baselineSoil = geography.SoilSuitability.GetRawValue(cell);
                    ulong food = 0UL;
                    foreach (var row in rows)
                    {
                        var harvest = baselineSoil == 0L
                            ? 0UL
                            : checked((ulong)(
                                ((UInt128)row.RawQuantity *
                                    (ulong)state.RawSoilVitality) /
                                (ulong)baselineSoil));
                        realized.Add(row, harvest);
                        food = checked(food + harvest);
                    }
                    consumed[(cell, group.Key.Kind)] = food;
                    continue;

                case EconomicActivityKind.Forestry:
                    available = state.RawStandingWood;
                    break;
                case EconomicActivityKind.Mining:
                    available = state.RawSurfaceStone;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unexpected nonproductive supply activity.");
            }

            // Multiple economic points sharing one strategic cell cannot
            // harvest the same biomass/stone twice. Split scarce material
            // proportionally; fractional leftovers go deterministically to
            // largest remainder then smallest EconomicPointId.
            var total = rows.Aggregate(0UL,
                (sum, row) => checked(sum + row.RawQuantity));
            var granted = Math.Min(total, available);
            ulong distributed = 0UL;
            var shares = rows.Select(row =>
            {
                var scaled = (UInt128)row.RawQuantity * granted;
                var q = checked((ulong)(scaled / total));
                distributed = checked(distributed + q);
                return (Row: row, Quantity: q, Remainder: scaled % total);
            }).ToArray();
            var remaining = granted - distributed;
            var extras = shares
                .OrderByDescending(x => x.Remainder)
                .ThenBy(x => x.Row.EconomicPointId.Value)
                .Take(checked((int)remaining))
                .Select(x => x.Row)
                .ToHashSet();

            foreach (var share in shares)
                realized.Add(share.Row,
                    checked(share.Quantity + (extras.Contains(share.Row) ? 1UL : 0UL)));
            consumed[(cell, group.Key.Kind)] = granted;
        }

        var flows = new EconomicProductionDemandResult(
            baseline.SupplyFlows
                .Where(x => realized[x] > 0UL)
                .Select(x => new EconomicSupplyFlow(
                    x.EconomicPointId, x.CommodityId, x.Activity, realized[x])),
            baseline.DemandFlows);

        var next = new List<EconomicNaturalCapitalCellState>(previous.Cells.Count);
        foreach (var state in previous.Cells)
        {
            var cell = state.CellId;
            consumed.TryGetValue((cell, EconomicActivityKind.Agriculture), out var food);
            consumed.TryGetValue((cell, EconomicActivityKind.Forestry), out var wood);
            consumed.TryGetValue((cell, EconomicActivityKind.Mining), out var stone);

            var afterWood = state.RawStandingWood - wood;
            var afterStone = state.RawSurfaceStone - stone;
            var soilWear = (UInt128)food * (ulong)policy.RawSoilWearPerFoodUnit / d;
            var wear = soilWear >= (UInt128)state.RawSoilVitality
                ? state.RawSoilVitality : checked((long)soilWear);
            var soilAfter = state.RawSoilVitality - wear;

            var woodGap = state.RawWoodCarryingCapacity - afterWood;
            var naturalWood = (ulong)geography.WoodyVegetation.GetRawValue(cell);
            var regrown = checked((ulong)(
                ((UInt128)woodGap * (ulong)policy.RawWoodRecoveryFraction *
                    naturalWood) / ((UInt128)d * d)));
            afterWood = checked(afterWood + regrown);

            var soilGap = geography.SoilSuitability.GetRawValue(cell) - soilAfter;
            var water = generatedWorld.StrategicClimateFields.WaterAvailability
                .GetRawValue(cell);
            var recovered = checked((long)(
                ((Int128)soilGap * policy.RawSoilRecoveryFraction * water) /
                ((Int128)d * d)));
            soilAfter = checked(soilAfter + recovered);

            next.Add(new EconomicNaturalCapitalCellState(
                cell, state.RawWoodCarryingCapacity,
                afterWood, afterStone, soilAfter));
        }

        return new EconomicNaturalCapitalTurnResult(flows,
            new EconomicNaturalCapitalBook(turnOrdinal,
                StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion, next));
    }
}
