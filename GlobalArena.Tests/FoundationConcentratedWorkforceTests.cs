using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationConcentratedWorkforceTests
{
    private static WorldGenerationRequest Request() => new(
        new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0));

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public void EveryCivilizationOpensOnOneRealFineTileWithIndividualIds(int people)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            Request(), people);
        var state = host.Snapshot;
        Assert.Equal(11U, host.StateHash.FormatVersion);
        Assert.Equal(people * 2, state.Workers.Workers.Count);
        Assert.Equal(people * 2, state.Workers.Workers
            .Select(w => w.Id).Distinct().Count());
        Assert.All(state.Workers.Workers, w =>
            Assert.Equal(1_000_000UL, w.OutputMicroPerTurn));
        Assert.Equal(0UL, state.Revision);
        Assert.All(state.Civilizations.Civilizations, civ =>
        {
            var citizens = state.Workers.Workers.Where(w => w.Owner == civ).ToArray();
            Assert.Equal(people, citizens.Length);
            Assert.Single(citizens.Select(w => w.TacticalTileId).Distinct());
            var tile = citizens[0].TacticalTileId!.Value;
            Assert.Equal(new GoldbergParameters(18, 0), tile.FineGoldbergParameters);
            Assert.All(citizens, w =>
                Assert.Contains(w.CellId, host.Incidence.TileIncidences[
                    checked((int)tile.FineStrategicCellId.Value - 1)]
                    .IncidentCoarseCellIds));
        });
        var all = FoundationWorkforceActivityGroups.Project(state.Workers);
        Assert.Equal(6, all.Count);
        Assert.Equal(people * 2, all.Sum(g => g.Count));
        Assert.Equal(state.Workers.Workers.Select(w => w.Id).OrderBy(x => x.Value),
            all.SelectMany(g => g.Members).Select(w => w.Id).OrderBy(x => x.Value));
        Assert.All(all, group =>
        {
            Assert.NotNull(group.Occupation);
            Assert.True(group.Count >= 1);
            Assert.Equal(group.Count, group.Members.Select(w => w.Id).Distinct().Count());
        });
        if (people == 6)
        {
            Assert.All(all, group => Assert.Equal(2, group.Count));
            Assert.Equal(6m, all.Where(g => g.Owner.Value == 1UL)
                .Sum(g => g.CapacityUnits));
            Assert.Equal(6m, all.Where(g => g.Owner.Value == 1UL)
                .Sum(g => g.DemandMultiplierTotal));
        }
    }

    [Fact]
    public void SixPerCivilizationUsesExactlyThreeRealProfessionGroupsPerCapital()
    {
        var human = new CivilizationId(1UL);
        var enemy = new CivilizationId(2UL);
        var a = FoundationConcentratedWorkforceScenario.CreatePreview(Request());
        var b = FoundationConcentratedWorkforceScenario.CreatePreview(Request());
        Assert.Equal(a.StateHash, b.StateHash);
        var projected = FoundationWorkforceActivityGroups.Project(a.Snapshot.Workers);
        Assert.Equal(6, projected.Count);
        foreach (var civ in new[] { human, enemy })
        {
            var groups = projected.Where(g => g.Owner == civ).ToArray();
            Assert.Equal(3, groups.Length);
            Assert.Equal(new[] {
                PhysicalWorkerOccupation.Agriculture,
                PhysicalWorkerOccupation.Forestry,
                PhysicalWorkerOccupation.Mining
            }, groups.Select(g => g.Occupation!.Value).ToArray());
            Assert.All(groups, g => Assert.Equal(2, g.Count));
            Assert.Single(groups.Select(g => g.Tile).Distinct());
        }
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            a.Snapshot.Workers.Workers.Single(w => w.Id.Value == 7UL).Activity);
    }

    [Fact]
    public void OneIndividualLeavesOnlyItsActivityGroupThenAiActsInSameTurn()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(Request());
        var human = new CivilizationId(1UL);
        var starting = host.Snapshot;
        var originalHash = host.StateHash;
        var origin = starting.Workers.Workers[0].TacticalTileId!.Value;
        var next = host.Incidence.FineTopology.Cells[
            checked((int)origin.FineStrategicCellId.Value - 1)]
            .AdjacentCellIds[0];
        var destination = new PhysicalTacticalTileId(
            host.Incidence.Refinement.FineParameters, next);
        host.QueueHumanMove(human, new PhysicalWorkerId(1UL), destination);
        Assert.Equal(originalHash, host.StateHash);
        Assert.Same(starting, host.Snapshot);
        Assert.Equal(6, FoundationWorkforceActivityGroups
            .Project(starting.Workers).Count);

        host.SetHumanReady(human);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.Equal(2, receipt.ExecutedEvents);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(11U, receipt.Hash.FormatVersion);
        Assert.Equal(2UL, host.Snapshot.Revision);
        Assert.Equal(12, host.Snapshot.Workers.Workers.Count);
        var moved = host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL);
        Assert.Equal(destination, moved.TacticalTileId);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, moved.Occupation);
        Assert.Equal(PhysicalWorkerActivity.Traveling, moved.Activity);
        var ai = host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 7UL);
        Assert.Equal(PhysicalWorkerActivity.Producing, ai.Activity);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, ai.Occupation);
        var groups = FoundationWorkforceActivityGroups.Project(host.Snapshot.Workers);
        Assert.Equal(7, groups.Count);
        Assert.Equal(1, groups.Single(g =>
            g.Owner == human && g.Tile == origin &&
            g.Occupation == PhysicalWorkerOccupation.Agriculture).Count);
        Assert.Equal(1, groups.Single(g =>
            g.Owner == human && g.Tile == destination &&
            g.Occupation == PhysicalWorkerOccupation.Agriculture).Count);
        Assert.Equal(12, groups.Sum(g => g.Count));
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.Equal(originalHash, new CanonicalWorldStateHasher().Compute(starting));
    }

    [Fact]
    public void LegacyV10WorkersNeverAcquireInventedProductiveGroup()
    {
        var legacy = FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(Request());
        var projected = FoundationWorkforceActivityGroups.Project(legacy.Snapshot.Workers);
        Assert.Equal(legacy.Snapshot.Workers.Workers.Count, projected.Count);
        Assert.All(projected, g =>
        {
            Assert.Null(g.Occupation);
            Assert.Single(g.Members);
        });
        Assert.Throws<InvalidOperationException>(() =>
            FoundationWorkforceActivityGroups.Project(
                PhysicalWorkerRuntimeState.Uninitialized));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationConcentratedWorkforceScenario.CreatePreview(Request(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationConcentratedWorkforceScenario.CreatePreview(Request(), 121));
    }
}
