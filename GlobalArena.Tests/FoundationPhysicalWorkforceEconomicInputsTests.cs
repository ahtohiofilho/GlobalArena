using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalWorkforceEconomicInputsTests
{
    private static readonly CivilizationId Human = new(1UL);
    private static readonly CivilizationId Ai = new(2UL);

    private static WorldGenerationRequest Request() => new(
        new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0));

    private static FoundationTacticalHumanAiTurnHost Start(int workers = 6) =>
        FoundationConcentratedWorkforceScenario.CreatePreview(Request(), workers);

    [Fact]
    public void OnePhysicalWorkerCreatesExactlyOneDemandSourceAndOnlyActiveWorkersSupply()
    {
        var host = Start();
        var initial = host.Snapshot;
        var initialHash = host.StateHash;
        var inputs = host.GetPhysicalEconomicInputs();

        Assert.Same(initial, host.Snapshot);
        Assert.Equal(initialHash, host.StateHash);
        Assert.Equal(12, inputs.WorkerCount);
        Assert.Equal(11, inputs.ActiveProducingWorkerCount);
        Assert.Equal(11, inputs.PositiveSupplyWorkerCount);
        Assert.Equal(12_000_000UL, inputs.TotalDemandMultiplierMicro);
        Assert.Equal(11_000_000UL, inputs.TotalActiveCapacityMicroPerTurn);
        Assert.Equal(2, inputs.DemandSites.Count);
        Assert.All(inputs.DemandSites, site =>
        {
            Assert.Equal(6, site.WorkerCount);
            Assert.Equal(6_000_000UL, site.DemandMultiplierMicro);
        });
        Assert.Equal(0, inputs.DemandSites.Single(x => x.Owner == Human).TravelingWorkers);
        Assert.Equal(1, inputs.DemandSites.Single(x => x.Owner == Ai).TravelingWorkers);
        Assert.Equal(6, inputs.SupplyLots.Where(x => x.Owner == Human)
            .Sum(x => x.WorkerCount));
        Assert.Equal(5, inputs.SupplyLots.Where(x => x.Owner == Ai)
            .Sum(x => x.WorkerCount));
        Assert.Equal(12, inputs.DemandSites.Sum(x => x.WorkerCount));
        Assert.Equal(11, inputs.SupplyLots.Sum(x => x.WorkerCount));
        Assert.All(inputs.SupplyLots, lot =>
        {
            Assert.NotEqual(PhysicalWorkerOccupation.Unassigned, lot.Occupation);
            Assert.True(lot.CapacityMicroPerTurn > 0UL);
            Assert.Equal(new GoldbergParameters(18, 0),
                lot.Tile.FineGoldbergParameters);
        });
    }

    [Fact]
    public void PendingWorkerOrdersCannotChangeEconomicInputsButCommitChangesPhysicalSites()
    {
        var host = Start();
        var original = host.Snapshot;
        var hash = host.StateHash;
        var initial = host.GetPhysicalEconomicInputs();
        var workerOne = original.Workers.Workers.Single(w => w.Id.Value == 1UL);
        var origin = workerOne.TacticalTileId!.Value;
        var adjacent = host.Incidence.FineTopology.Cells[
            checked((int)origin.FineStrategicCellId.Value - 1)]
            .AdjacentCellIds[0];
        var destination = new PhysicalTacticalTileId(
            host.Incidence.Refinement.FineParameters, adjacent);

        Assert.Equal(1, host.QueueHumanOccupationBatch(Human, new[]
        {
            new FoundationTacticalOccupationChange(
                new PhysicalWorkerId(4UL), PhysicalWorkerOccupation.Mining)
        }));
        host.QueueHumanMove(Human, workerOne.Id, destination);
        var planning = host.GetPhysicalEconomicInputs();
        Assert.Equal(initial.DemandSites.ToArray(), planning.DemandSites.ToArray());
        Assert.Equal(initial.SupplyLots.ToArray(), planning.SupplyLots.ToArray());
        Assert.Same(original, host.Snapshot);
        Assert.Equal(hash, host.StateHash);

        host.SetHumanReady(Human);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(11U, receipt.Hash.FormatVersion);
        Assert.Equal(3, receipt.ExecutedEvents); // 2 human actions + AI restart
        var actual = host.GetPhysicalEconomicInputs();
        Assert.Equal(12, actual.WorkerCount);
        Assert.Equal(12_000_000UL, actual.TotalDemandMultiplierMicro);
        Assert.Equal(11_000_000UL, actual.TotalActiveCapacityMicroPerTurn);
        Assert.Equal(3, actual.DemandSites.Count); // two capitals and one arrival tile
        var arrival = Assert.Single(actual.DemandSites.Where(site =>
            site.Owner == Human && site.Tile == destination));
        Assert.Equal(1, arrival.WorkerCount);
        Assert.Equal(1, arrival.TravelingWorkers);
        Assert.Equal(1_000_000UL, arrival.DemandMultiplierMicro);
        Assert.DoesNotContain(actual.SupplyLots, lot =>
            lot.Tile == destination && lot.Owner == Human);
        Assert.Equal(5, actual.DemandSites.Single(site =>
            site.Owner == Human && site.Tile == origin).WorkerCount);
        Assert.Equal(6, actual.DemandSites.Single(site =>
            site.Owner == Ai).WorkerCount);
        Assert.Equal(0, actual.DemandSites.Single(site =>
            site.Owner == Ai).TravelingWorkers);
        Assert.Contains(actual.SupplyLots, lot =>
            lot.Owner == Human &&
            lot.Occupation == PhysicalWorkerOccupation.Mining &&
            lot.Commodity == original.Workers.Workers
                .Single(w => w.Id.Value == 4UL).Commodity);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(original));
        Assert.Equal(receipt.Hash, host.StateHash);
    }

    [Fact]
    public void ZeroOutputMilitaryAndTravelingWorkersDemandWithoutPhantomSupply()
    {
        var host = Start();
        var workers = host.Snapshot.Workers.Workers.Take(4).ToArray();
        var sample = workers[0];
        var tactical = sample.TacticalTileId!.Value;
        var census = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(101),
                Human, sample.CellId, sample.Commodity, 2_000_000UL,
                PhysicalWorkerActivity.Producing, 1_500_000U,
                tactical, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(102),
                Human, sample.CellId, sample.Commodity, 0UL,
                PhysicalWorkerActivity.Producing, 750_000U,
                tactical, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(103),
                Human, sample.CellId, sample.Commodity, 8_000_000UL,
                PhysicalWorkerActivity.Military, 2_000_000U,
                tactical, PhysicalWorkerOccupation.Forestry),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(104),
                Human, sample.CellId, sample.Commodity, 5_000_000UL,
                PhysicalWorkerActivity.Traveling, 500_000U,
                tactical, PhysicalWorkerOccupation.Mining),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(105),
                Human, sample.CellId, sample.Commodity, 8_000_000UL,
                PhysicalWorkerActivity.Other, 600_000U,
                tactical, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        var result = FoundationPhysicalWorkforceEconomicProjector.Project(census);
        Assert.Equal(5, result.WorkerCount);
        Assert.Equal(2, result.ActiveProducingWorkerCount);
        Assert.Equal(1, result.PositiveSupplyWorkerCount);
        Assert.Equal(5_350_000UL, result.TotalDemandMultiplierMicro);
        Assert.Equal(2_000_000UL, result.TotalActiveCapacityMicroPerTurn);
        var site = Assert.Single(result.DemandSites);
        Assert.Equal(2, site.ProducingWorkers);
        Assert.Equal(1, site.MilitaryWorkers);
        Assert.Equal(1, site.TravelingWorkers);
        Assert.Equal(1, site.OtherWorkers);
        Assert.Equal(1, Assert.Single(result.SupplyLots).WorkerCount);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture,
            Assert.Single(result.SupplyLots).Occupation);
    }

    [Fact]
    public void TenThousandRealWorkersAggregateInConstantNumberOfPhysicalSites()
    {
        var reference = Start().Snapshot.Workers.Workers[0];
        var census = new PhysicalWorkerRuntimeState(
            Enumerable.Range(1, 10_000).Select(i =>
                new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId((ulong)i),
                    i % 2 == 0 ? Human : Ai,
                    reference.CellId, reference.Commodity,
                    (ulong)(1_000_000 + i % 5),
                    i % 13 == 0
                        ? PhysicalWorkerActivity.Traveling
                        : PhysicalWorkerActivity.Producing,
                    (uint)(1_000_000 + i % 7),
                    reference.TacticalTileId,
                    i % 3 == 0
                        ? PhysicalWorkerOccupation.Agriculture
                        : i % 3 == 1
                            ? PhysicalWorkerOccupation.Forestry
                            : PhysicalWorkerOccupation.Mining)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);

        var originalHashFreeCensus = census.Workers.ToArray();
        var result = FoundationPhysicalWorkforceEconomicProjector.Project(census);
        var reverse = new PhysicalWorkerRuntimeState(
            census.Workers.Reverse(),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var afterReverse = FoundationPhysicalWorkforceEconomicProjector.Project(reverse);
        Assert.Equal(10_000, result.WorkerCount);
        Assert.Equal(2, result.DemandSites.Count);
        Assert.Equal(10_000, result.DemandSites.Sum(x => x.WorkerCount));
        Assert.Equal(10_000, result.DemandSites.Sum(x =>
            x.ProducingWorkers + x.TravelingWorkers + x.MilitaryWorkers +
            x.OtherWorkers));
        Assert.True(result.SupplyLots.Count <= 6);
        Assert.Equal(result.DemandSites.ToArray(), afterReverse.DemandSites.ToArray());
        Assert.Equal(result.SupplyLots.ToArray(), afterReverse.SupplyLots.ToArray());
        Assert.Equal(originalHashFreeCensus,
            census.Workers.ToArray()); // no worker changed by projection
        Assert.Equal(census.Workers.Count(worker =>
            worker.Activity == PhysicalWorkerActivity.Producing),
            result.PositiveSupplyWorkerCount);
    }

    [Fact]
    public void OldSchemasFailClosedAndEmptyV11DoesNotInventMarkets()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalWorkforceEconomicProjector.Project(
                PhysicalWorkerRuntimeState.Uninitialized));
        var old = FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(Request());
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalWorkforceEconomicProjector.Project(old.Snapshot.Workers));
        var empty = new PhysicalWorkerRuntimeState(
            Array.Empty<PhysicalWorkerRuntimeRecord>(),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var projected = FoundationPhysicalWorkforceEconomicProjector.Project(empty);
        Assert.Empty(projected.DemandSites);
        Assert.Empty(projected.SupplyLots);
        Assert.Equal(0UL, projected.TotalDemandMultiplierMicro);
        Assert.Equal(0UL, projected.TotalActiveCapacityMicroPerTurn);
    }

    [Fact]
    public void OverflowInActiveSupplyIsRejectedInsteadOfSilentlyWrapping()
    {
        var worker = Start().Snapshot.Workers.Workers[0];
        var census = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1),
                Human, worker.CellId, worker.Commodity, ulong.MaxValue,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                worker.TacticalTileId, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2),
                Human, worker.CellId, worker.Commodity, 1UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                worker.TacticalTileId, PhysicalWorkerOccupation.Agriculture)
        }, tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        Assert.Throws<OverflowException>(() =>
            FoundationPhysicalWorkforceEconomicProjector.Project(census));
    }
}
