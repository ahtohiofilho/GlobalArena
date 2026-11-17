using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalCommodityDemandTests
{
    private static readonly CivilizationId Human = new(1UL);
    private static readonly CommodityId ItemOne = new(1U);
    private static readonly CommodityId ItemTwo = new(2U);

    private static WorldGenerationRequest Request() => new(
        new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0));

    private static FoundationTacticalHumanAiTurnHost Start() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(Request());

    // TEST-ONLY weights. They are deliberately asymmetric, and must not be
    // installed as defaults in the game. Demand varies both in composition
    // AND total by worker activity; every activity wants every declared item.
    private static FoundationActivityCommodityDemandPolicy Policy() =>
        new(new[] { ItemTwo, ItemOne },
            new[]
            {
                Rule(FoundationPhysicalDemandProfile.AgricultureProduction, 2, 1),
                Rule(FoundationPhysicalDemandProfile.ForestryProduction, 1, 3),
                Rule(FoundationPhysicalDemandProfile.MiningProduction, 4, 2),
                Rule(FoundationPhysicalDemandProfile.Traveling, 2, 2),
                Rule(FoundationPhysicalDemandProfile.Military, 5, 4),
                Rule(FoundationPhysicalDemandProfile.Other, 3, 1)
            }.SelectMany(x => x));

    private static FoundationActivityCommodityDemandCoefficient[] Rule(
        FoundationPhysicalDemandProfile profile, ulong first, ulong second) =>
        new[]
        {
            new FoundationActivityCommodityDemandCoefficient(
                profile, ItemOne, checked(first * 1_000_000UL)),
            new FoundationActivityCommodityDemandCoefficient(
                profile, ItemTwo, checked(second * 1_000_000UL))
        };

    [Fact]
    public void ExistingPhysicalCensusCreatesNoPhantomBuyersAndDemandVariesByActivity()
    {
        var host = Start();
        var hash = host.StateHash;
        var old = host.GetPhysicalEconomicInputs();
        var result = host.GetCommodityMarketInputs(Policy());

        Assert.Equal(hash, host.StateHash);
        Assert.Equal(12, result.DistinctWorkers);
        Assert.Equal(2, result.Commodities.Count);
        Assert.Equal(new[] { ItemOne, ItemTwo }, result.Commodities);
        Assert.Equal(4, result.DemandByCivilization.Count);
        Assert.Equal(4, result.MarketDemand.Count);
        Assert.Equal(53_000_000UL, result.TotalCommodityDemandMicro);
        Assert.Equal(11_000_000UL, result.TotalActiveCapacityMicroPerTurn);
        Assert.Equal(old.SupplyLots.ToArray(), result.SupplyLots.ToArray());
        Assert.Equal(old.DemandSites.Sum(site => site.WorkerCount) * 2,
            result.DemandByCivilization.Sum(line => line.WorkerCount));
        Assert.Equal(28_000_000UL,
            result.MarketDemand.Where(x => x.Commodity == ItemOne)
                .Aggregate(0UL, (sum, x) => checked(sum + x.DemandMicro)));
        Assert.Equal(25_000_000UL,
            result.MarketDemand.Where(x => x.Commodity == ItemTwo)
                .Aggregate(0UL, (sum, x) => checked(sum + x.DemandMicro)));
        Assert.All(result.DemandByCivilization, line =>
        {
            Assert.Equal(6, line.WorkerCount);
            Assert.True(line.DemandMicro > 0UL);
            Assert.Equal(new GoldbergParameters(18, 0), line.Tile.FineGoldbergParameters);
        });
        Assert.Equal(result.TotalCommodityDemandMicro,
            result.MarketDemand.Aggregate(0UL,
                (sum, x) => checked(sum + x.DemandMicro)));
    }

    [Fact]
    public void PlannedOrdersAreNotDemandAndOnlyCommittedActivityChangesMarket()
    {
        var host = Start();
        var before = host.Snapshot;
        var hash = host.StateHash;
        var initial = host.GetCommodityMarketInputs(Policy());
        var origin = before.Workers.Workers.Single(w => w.Id.Value == 1UL)
            .TacticalTileId!.Value;
        var adjacent = host.Incidence.FineTopology.Cells[
            checked((int)origin.FineStrategicCellId.Value - 1)]
            .AdjacentCellIds[0];
        var destination = new PhysicalTacticalTileId(
            host.Incidence.Refinement.FineParameters, adjacent);

        host.QueueHumanOccupationBatch(Human, new[]
        {
            new FoundationTacticalOccupationChange(
                new PhysicalWorkerId(4UL), PhysicalWorkerOccupation.Mining)
        });
        host.QueueHumanMove(Human, new PhysicalWorkerId(1UL), destination);

        var pending = host.GetCommodityMarketInputs(Policy());
        Assert.Equal(initial.DemandByCivilization.ToArray(),
            pending.DemandByCivilization.ToArray());
        Assert.Equal(initial.SupplyLots.ToArray(), pending.SupplyLots.ToArray());
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);

        host.SetHumanReady(Human);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(11U, receipt.Hash.FormatVersion);
        var committed = host.GetCommodityMarketInputs(Policy());
        Assert.Equal(12, committed.DistinctWorkers);
        Assert.Equal(6, committed.DemandByCivilization.Count);
        Assert.Equal(6, committed.MarketDemand.Count);
        Assert.Equal(56_000_000UL, committed.TotalCommodityDemandMicro);
        Assert.Equal(11_000_000UL, committed.TotalActiveCapacityMicroPerTurn);
        Assert.All(committed.MarketDemand, market =>
            Assert.True(market.DemandMicro > 0UL));
        foreach (var commodity in committed.Commodities)
        {
            var arrival = Assert.Single(committed.DemandByCivilization, x =>
                x.Owner == Human && x.Tile == destination && x.Commodity == commodity);
            Assert.Equal(1, arrival.WorkerCount);
            Assert.Equal(2_000_000UL, arrival.DemandMicro);
        }
        Assert.DoesNotContain(committed.SupplyLots, lot =>
            lot.Tile == destination && lot.Owner == Human);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(before));
        Assert.Equal(receipt.Hash, host.StateHash);
    }

    [Fact]
    public void MilitaryAndOtherDemandByTheirOwnProfileRegardlessOfDormantOccupation()
    {
        var sample = Start().Snapshot.Workers.Workers[0];
        var tile = sample.TacticalTileId!.Value;
        var workers = new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(71), Human,
                sample.CellId, ItemOne, 0UL,
                PhysicalWorkerActivity.Producing, 2_000_000U,
                tile, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(72), Human,
                sample.CellId, ItemTwo, 7_000_000UL,
                PhysicalWorkerActivity.Military, 1_000_000U,
                tile, PhysicalWorkerOccupation.Forestry),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(73), Human,
                sample.CellId, ItemTwo, 7_000_000UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                tile, PhysicalWorkerOccupation.Unassigned),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(74), Human,
                sample.CellId, ItemOne, 7_000_000UL,
                PhysicalWorkerActivity.Traveling, 1_000_000U,
                tile, PhysicalWorkerOccupation.Mining)
        };
        var census = new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        var result = FoundationPhysicalCommodityDemandProjector.Project(census, Policy());
        Assert.Equal(4, result.DistinctWorkers);
        Assert.Empty(result.SupplyLots);
        Assert.Equal(22_000_000UL, result.TotalCommodityDemandMicro);
        Assert.Equal(0UL, result.TotalActiveCapacityMicroPerTurn);
        var first = Assert.Single(result.DemandByCivilization, line =>
            line.Commodity == ItemOne);
        var second = Assert.Single(result.DemandByCivilization, line =>
            line.Commodity == ItemTwo);
        Assert.Equal(14_000_000UL, first.DemandMicro); // 4 + 5 + 3 + 2
        Assert.Equal(8_000_000UL, second.DemandMicro); // 2 + 4 + 1 + 2 = 9? See below
    }

    [Fact]
    public void PolicyIsCompleteExplicitPositiveAndHasNoImplicitDefaultOrDuplicateEntry()
    {
        var good = Policy();
        Assert.Equal(2_000_000UL,
            good.IntensityMicroPerWorker(
                FoundationPhysicalDemandProfile.Traveling, ItemOne));
        var all = new[]
        {
            Rule(FoundationPhysicalDemandProfile.AgricultureProduction, 2, 1),
            Rule(FoundationPhysicalDemandProfile.ForestryProduction, 1, 3),
            Rule(FoundationPhysicalDemandProfile.MiningProduction, 4, 2),
            Rule(FoundationPhysicalDemandProfile.Traveling, 2, 2),
            Rule(FoundationPhysicalDemandProfile.Military, 5, 4),
            Rule(FoundationPhysicalDemandProfile.Other, 3, 1)
        }.SelectMany(x => x).ToArray();
        Assert.Throws<ArgumentException>(() =>
            new FoundationActivityCommodityDemandPolicy(
                new[] { ItemOne, ItemOne }, all));
        Assert.Throws<ArgumentException>(() =>
            new FoundationActivityCommodityDemandPolicy(
                Array.Empty<CommodityId>(), all));
        Assert.Throws<ArgumentException>(() =>
            new FoundationActivityCommodityDemandPolicy(
                new[] { ItemOne, ItemTwo }, all.Skip(1)));
        Assert.Throws<ArgumentException>(() =>
            new FoundationActivityCommodityDemandPolicy(
                new[] { ItemOne, ItemTwo }, all.Append(all[0])));
        Assert.Throws<ArgumentException>(() =>
            new FoundationActivityCommodityDemandPolicy(
                new[] { ItemOne, ItemTwo }, all.Append(
                    new FoundationActivityCommodityDemandCoefficient(
                        FoundationPhysicalDemandProfile.Other,
                        new CommodityId(99U), 1_000_000UL))));
        Assert.Throws<ArgumentException>(() =>
            new FoundationActivityCommodityDemandPolicy(
                new[] { ItemOne, ItemTwo },
                all.Select((rule, i) => i == 0
                    ? rule with { IntensityMicroPerWorker = 0UL }
                    : rule)));
        Assert.Throws<ArgumentException>(() =>
            new FoundationActivityCommodityDemandPolicy(
                new[] { ItemOne, ItemTwo },
                all.Select((rule, i) => i == 0
                    ? rule with { Profile = (FoundationPhysicalDemandProfile)99 }
                    : rule)));
    }

    [Fact]
    public void TenThousandWorkersAreAggregatedIntoTwoSitesAndFourDemandLines()
    {
        var sample = Start().Snapshot.Workers.Workers[0];
        var array = Enumerable.Range(1, 10_000)
            .Select(i => new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)i), i % 2 == 0
                    ? Human : new CivilizationId(2UL), sample.CellId,
                i % 3 == 0 ? ItemTwo : ItemOne,
                1_000_000UL, i % 11 == 0
                    ? PhysicalWorkerActivity.Traveling
                    : PhysicalWorkerActivity.Producing,
                1_000_000U, sample.TacticalTileId,
                i % 3 == 0 ? PhysicalWorkerOccupation.Mining
                    : i % 3 == 1 ? PhysicalWorkerOccupation.Agriculture
                    : PhysicalWorkerOccupation.Forestry))
            .ToArray();
        var census = new PhysicalWorkerRuntimeState(array,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        var permuted = new PhysicalWorkerRuntimeState(array.Reverse(),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        var one = FoundationPhysicalCommodityDemandProjector.Project(census, Policy());
        var two = FoundationPhysicalCommodityDemandProjector.Project(permuted, Policy());
        Assert.Equal(10_000, one.DistinctWorkers);
        Assert.Equal(4, one.DemandByCivilization.Count);
        Assert.Equal(2, one.MarketDemand.Count);
        Assert.Equal(20_000, one.DemandByCivilization.Sum(x => x.WorkerCount));
        Assert.Equal(one.TotalCommodityDemandMicro, two.TotalCommodityDemandMicro);
        Assert.Equal(one.MarketDemand.ToArray(), two.MarketDemand.ToArray());
        Assert.Equal(one.DemandByCivilization.ToArray(), two.DemandByCivilization.ToArray());
        Assert.Equal(one.SupplyLots.ToArray(), two.SupplyLots.ToArray());
    }

    [Fact]
    public void UnrepresentableTinyDemandIsRejectedInsteadOfInventingAPositiveFloor()
    {
        var sample = Start().Snapshot.Workers.Workers[0];
        var tiny = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1), Human,
                sample.CellId, ItemOne, 0UL,
                PhysicalWorkerActivity.Other, 1U,
                sample.TacticalTileId, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCommodityDemandProjector.Project(tiny, Policy()));

        var allMax = Enum.GetValues<FoundationPhysicalDemandProfile>()
            .SelectMany(p => new[] { ItemOne, ItemTwo }.Select(item =>
                new FoundationActivityCommodityDemandCoefficient(
                    p, item, ulong.MaxValue)));
        var overflow = new FoundationActivityCommodityDemandPolicy(
            new[] { ItemOne, ItemTwo }, allMax);
        var huge = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1), Human,
                sample.CellId, ItemOne, 0UL,
                PhysicalWorkerActivity.Other, uint.MaxValue,
                sample.TacticalTileId, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        Assert.Throws<OverflowException>(() =>
            FoundationPhysicalCommodityDemandProjector.Project(huge, overflow));
    }

    [Fact]
    public void LegacyV10AndAbsentCensusFailClosedAndEmptyV11AddsNoBuyers()
    {
        var old = FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(Request());
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCommodityDemandProjector.Project(
                old.Snapshot.Workers, Policy()));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCommodityDemandProjector.Project(
                PhysicalWorkerRuntimeState.Uninitialized, Policy()));
        var empty = new PhysicalWorkerRuntimeState(
            Array.Empty<PhysicalWorkerRuntimeRecord>(),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        var result = FoundationPhysicalCommodityDemandProjector.Project(empty, Policy());
        Assert.Empty(result.MarketDemand);
        Assert.Empty(result.DemandByCivilization);
        Assert.Empty(result.SupplyLots);
        Assert.Equal(0UL, result.TotalCommodityDemandMicro);
    }
}
