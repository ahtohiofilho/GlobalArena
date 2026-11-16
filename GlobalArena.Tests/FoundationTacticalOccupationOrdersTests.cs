using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationTacticalOccupationOrdersTests
{
    private static WorldGenerationRequest Request() => new(
        new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0));

    private static FoundationTacticalHumanAiTurnHost Start() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(Request());

    [Fact]
    public void InPlaceReassignmentChangesExactlyOneWorkerOnlyAfterCollectiveCommit()
    {
        var host = Start();
        var original = host.Snapshot;
        var originalHash = host.StateHash;
        var farmer = original.Workers.Workers.Single(w => w.Id.Value == 1UL);
        var otherFarmer = original.Workers.Workers.Single(w => w.Id.Value == 4UL);
        var initialCounts = FoundationWorkforceActivityGroups.Project(original.Workers);
        Assert.Equal(3, initialCounts.Count(g => g.Owner.Value == 1UL));
        Assert.Equal(2, initialCounts.Single(g =>
            g.Owner.Value == 1UL &&
            g.Occupation == PhysicalWorkerOccupation.Agriculture).Count);

        host.QueueHumanOccupation(new CivilizationId(1UL), farmer.Id,
            PhysicalWorkerOccupation.Mining);
        Assert.Equal(1, host.PendingOrderCount);
        var pending = Assert.Single(host.GetHumanOrders(new CivilizationId(1UL)));
        Assert.False(pending.IsMovement);
        Assert.Equal(PhysicalWorkerOccupation.Mining, pending.NextOccupation);
        Assert.Equal(farmer.TacticalTileId, pending.Source);
        Assert.Equal(farmer.TacticalTileId, pending.Destination);
        Assert.Equal(farmer.Activity, pending.Activity);
        Assert.Same(original, host.Snapshot);
        Assert.Equal(originalHash, host.StateHash);

        host.SetHumanReady(new CivilizationId(1UL));
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(2, receipt.ExecutedEvents); // 1 human occupation + 1 AI restart
        Assert.Equal(2UL, host.Snapshot.Revision);
        Assert.Equal(11U, receipt.Hash.FormatVersion);
        Assert.Equal(receipt.Hash, host.StateHash);
        var updated = host.Snapshot.Workers.Workers.Single(w => w.Id == farmer.Id);
        Assert.Equal(PhysicalWorkerOccupation.Mining, updated.Occupation);
        Assert.Equal(farmer.TacticalTileId, updated.TacticalTileId);
        Assert.Equal(farmer.CellId, updated.CellId);
        Assert.Equal(farmer.Activity, updated.Activity);
        Assert.Equal(farmer.OutputMicroPerTurn, updated.OutputMicroPerTurn);
        Assert.Equal(farmer.DemandMultiplierMicro, updated.DemandMultiplierMicro);
        Assert.Equal(farmer.Commodity, updated.Commodity);
        Assert.Equal(farmer.Owner, updated.Owner);
        Assert.Equal(otherFarmer, host.Snapshot.Workers.Workers.Single(
            w => w.Id == otherFarmer.Id));

        var groups = FoundationWorkforceActivityGroups.Project(host.Snapshot.Workers);
        var home = farmer.TacticalTileId!.Value;
        Assert.Equal(1, groups.Single(g => g.Owner.Value == 1UL &&
            g.Tile == home &&
            g.Occupation == PhysicalWorkerOccupation.Agriculture).Count);
        Assert.Equal(2, groups.Single(g => g.Owner.Value == 1UL &&
            g.Tile == home &&
            g.Occupation == PhysicalWorkerOccupation.Forestry).Count);
        Assert.Equal(3, groups.Single(g => g.Owner.Value == 1UL &&
            g.Tile == home &&
            g.Occupation == PhysicalWorkerOccupation.Mining).Count);
        Assert.Equal(12, groups.Sum(g => g.Count));
        Assert.NotEqual(originalHash, host.StateHash);
        Assert.Equal(originalHash, new CanonicalWorldStateHasher().Compute(original));
    }

    [Fact]
    public void XCancelAndOverwriteByMovementKeepOneWorkerOneCommand()
    {
        var host = Start();
        var human = new CivilizationId(1UL);
        var id = new PhysicalWorkerId(1UL);
        var initial = host.StateHash;
        host.QueueHumanOccupation(human, id, PhysicalWorkerOccupation.Forestry);
        Assert.True(host.CancelHumanOrder(human, id));
        Assert.Empty(host.GetHumanOrders(human));
        Assert.Equal(initial, host.StateHash);

        host.QueueHumanOccupation(human, id, PhysicalWorkerOccupation.Mining);
        var home = host.Snapshot.Workers.Workers[0].TacticalTileId!.Value;
        var neighbor = host.Incidence.FineTopology.Cells[
            checked((int)home.FineStrategicCellId.Value - 1)].AdjacentCellIds[0];
        host.QueueHumanMove(human, id,
            new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, neighbor));
        var pending = Assert.Single(host.GetHumanOrders(human));
        Assert.True(pending.IsMovement);
        Assert.Null(pending.NextOccupation);
        Assert.Equal(initial, host.StateHash);

        host.QueueHumanOccupation(human, id, PhysicalWorkerOccupation.Forestry);
        pending = Assert.Single(host.GetHumanOrders(human));
        Assert.False(pending.IsMovement);
        Assert.Equal(PhysicalWorkerOccupation.Forestry, pending.NextOccupation);
        host.SetHumanReady(human);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.Equal(PhysicalWorkerOccupation.Forestry,
            host.Snapshot.Workers.Workers[0].Occupation);
        Assert.Equal(home, host.Snapshot.Workers.Workers[0].TacticalTileId);
        Assert.Equal(2, receipt.ExecutedEvents);
    }

    [Fact]
    public void InvalidForeignSameRoleOrNonproductiveAssignmentsRejectBeforePublication()
    {
        var host = Start();
        var hash = host.StateHash;
        var human = new CivilizationId(1UL);
        var ai = new CivilizationId(2UL);
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupation(ai, new PhysicalWorkerId(1UL),
                PhysicalWorkerOccupation.Mining));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupation(human, new PhysicalWorkerId(7UL),
                PhysicalWorkerOccupation.Mining));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupation(human, new PhysicalWorkerId(1UL),
                PhysicalWorkerOccupation.Agriculture));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupation(human, new PhysicalWorkerId(1UL),
                PhysicalWorkerOccupation.Unassigned));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupation(human, new PhysicalWorkerId(1UL),
                (PhysicalWorkerOccupation)99));
        Assert.Equal(hash, host.StateHash);
        Assert.Empty(host.GetHumanOrders(human));

        host.SetHumanReady(human);
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupation(human, new PhysicalWorkerId(1UL),
                PhysicalWorkerOccupation.Mining));
        Assert.Equal(hash, host.StateHash);
    }

    [Fact]
    public void LegacyV10RejectsNewRoleWithoutChangingHistoricalHash()
    {
        var host = FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(Request());
        var before = host.StateHash;
        Assert.Equal(10U, before.FormatVersion);
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupation(new CivilizationId(1UL),
                new PhysicalWorkerId(1UL), PhysicalWorkerOccupation.Mining));
        Assert.Equal(before, host.StateHash);
        Assert.Equal(0, host.PendingOrderCount);
    }

    [Fact]
    public void TwoDifferentHumanRoleOrdersAreDeterministicAndReplayable()
    {
        var host = Start();
        var baseline = Start();
        var human = new CivilizationId(1UL);
        var first = new PhysicalWorkerId(1UL);
        var second = new PhysicalWorkerId(2UL);
        foreach (var h in new[] { host, baseline })
        {
            // Reverse preparation order to prove canonical sorting by ID.
            if (ReferenceEquals(h, host))
            {
                h.QueueHumanOccupation(human, second,
                    PhysicalWorkerOccupation.Mining);
                h.QueueHumanOccupation(human, first,
                    PhysicalWorkerOccupation.Forestry);
            }
            else
            {
                h.QueueHumanOccupation(human, first,
                    PhysicalWorkerOccupation.Forestry);
                h.QueueHumanOccupation(human, second,
                    PhysicalWorkerOccupation.Mining);
            }
            h.SetHumanReady(human);
            var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
                h.TryCloseTurn());
            Assert.True(receipt.ReplayMatched);
            Assert.Equal(3, receipt.ExecutedEvents);
            Assert.Equal(3UL, h.Snapshot.Revision);
        }
        Assert.Equal(host.StateHash, baseline.StateHash);
        Assert.Equal(12, host.Snapshot.Workers.Workers.Count);
        Assert.Equal(PhysicalWorkerOccupation.Forestry,
            host.Snapshot.Workers.Workers.Single(w => w.Id == first).Occupation);
        Assert.Equal(PhysicalWorkerOccupation.Mining,
            host.Snapshot.Workers.Workers.Single(w => w.Id == second).Occupation);
    }
}
