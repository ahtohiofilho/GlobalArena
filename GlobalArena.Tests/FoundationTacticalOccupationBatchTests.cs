using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Bulk productive orders are an all-or-nothing planning operation, not a
/// distinct economic or turn phase. The authoritative journal stays v11.
/// </summary>
public sealed class FoundationTacticalOccupationBatchTests
{
    private static readonly CivilizationId Human = new(1UL);
    private static readonly CivilizationId Ai = new(2UL);

    private static WorldGenerationRequest Request() => new(
        new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0));

    private static FoundationTacticalHumanAiTurnHost Start(int peoplePerCivilization = 6) =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            Request(), peoplePerCivilization);

    private static FoundationTacticalOccupationChange Change(
        ulong id, PhysicalWorkerOccupation role) =>
        new(new PhysicalWorkerId(id), role);

    [Fact]
    public void BulkReallocationUsesOneUnchangedCensusUntilCollectiveTurnAndReplaysExactly()
    {
        var host = Start();
        var sequential = Start();
        var before = host.Snapshot;
        var hash = host.StateHash;
        var ordered = new[]
        {
            Change(4UL, PhysicalWorkerOccupation.Forestry),
            Change(2UL, PhysicalWorkerOccupation.Agriculture),
            Change(1UL, PhysicalWorkerOccupation.Mining)
        };
        Assert.Equal(3, host.QueueHumanOccupationBatch(Human, ordered));
        var pending = host.GetHumanOrders(Human);
        Assert.Equal(new ulong[] { 1UL, 2UL, 4UL },
            pending.Select(x => x.WorkerId.Value));
        Assert.All(pending, order =>
        {
            Assert.Equal(order.Source, order.Destination);
            Assert.False(order.IsMovement);
            Assert.NotNull(order.NextOccupation);
        });
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal((2, 2, 2), Counts(host));

        sequential.QueueHumanOccupation(Human, new PhysicalWorkerId(1UL),
            PhysicalWorkerOccupation.Mining);
        sequential.QueueHumanOccupation(Human, new PhysicalWorkerId(2UL),
            PhysicalWorkerOccupation.Agriculture);
        sequential.QueueHumanOccupation(Human, new PhysicalWorkerId(4UL),
            PhysicalWorkerOccupation.Forestry);

        foreach (var test in new[] { host, sequential })
        {
            test.SetHumanReady(Human);
            var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
                test.TryCloseTurn());
            Assert.True(receipt.ReplayMatched);
            Assert.Equal(4, receipt.ExecutedEvents); // 3 humans + 1 AI restart
            Assert.Equal(11U, receipt.Hash.FormatVersion);
            Assert.Equal(receipt.Hash, test.StateHash);
            Assert.Equal((1, 2, 3), Counts(test));
            Assert.Equal(12, test.Snapshot.Workers.Workers.Count);
        }
        Assert.Equal(sequential.StateHash, host.StateHash);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(before));
        Assert.All(host.Snapshot.Workers.Workers, worker =>
        {
            var earlier = before.Workers.Workers.Single(w => w.Id == worker.Id);
            Assert.Equal(earlier.Owner, worker.Owner);
            Assert.Equal(earlier.TacticalTileId, worker.TacticalTileId);
            Assert.Equal(earlier.OutputMicroPerTurn, worker.OutputMicroPerTurn);
            Assert.Equal(earlier.DemandMultiplierMicro, worker.DemandMultiplierMicro);
        });
    }

    [Fact]
    public void OneInvalidBatchMemberDoesNotModifyExistingPendingOrders()
    {
        var host = Start();
        var hash = host.StateHash;
        var first = new PhysicalWorkerId(1UL);
        var second = new PhysicalWorkerId(5UL);
        var home = host.Snapshot.Workers.Workers[0].TacticalTileId!.Value;
        var neighbor = host.Incidence.FineTopology.Cells[
            checked((int)home.FineStrategicCellId.Value - 1)].AdjacentCellIds[0];
        host.QueueHumanMove(Human, first,
            new PhysicalTacticalTileId(
                host.Incidence.Refinement.FineParameters, neighbor));
        host.QueueHumanOccupation(Human, second, PhysicalWorkerOccupation.Mining);
        var previous = host.GetHumanOrders(Human).ToArray();
        Assert.Equal(2, previous.Length);

        var badBatches = new[]
        {
            new[] { Change(1UL, PhysicalWorkerOccupation.Forestry),
                    Change(7UL, PhysicalWorkerOccupation.Mining) }, // AI-owned
            new[] { Change(1UL, PhysicalWorkerOccupation.Forestry),
                    Change(1UL, PhysicalWorkerOccupation.Mining) }, // duplicate
            new[] { Change(1UL, PhysicalWorkerOccupation.Forestry),
                    Change(2UL, PhysicalWorkerOccupation.Forestry) }, // no-op
            new[] { Change(1UL, PhysicalWorkerOccupation.Forestry),
                    Change(2UL, PhysicalWorkerOccupation.Unassigned) },
            new[] { Change(1UL, PhysicalWorkerOccupation.Forestry),
                    Change(2UL, (PhysicalWorkerOccupation)99) },
            new[] { Change(1UL, PhysicalWorkerOccupation.Forestry),
                    Change(999UL, PhysicalWorkerOccupation.Mining) }
        };
        foreach (var changes in badBatches)
        {
            Assert.Throws<InvalidOperationException>(() =>
                host.QueueHumanOccupationBatch(Human, changes));
            Assert.Equal(previous, host.GetHumanOrders(Human));
            Assert.Equal(hash, host.StateHash);
            Assert.Equal((2, 2, 2), Counts(host));
        }

        Assert.Throws<ArgumentException>(() =>
            host.QueueHumanOccupationBatch(Human,
                Array.Empty<FoundationTacticalOccupationChange>()));
        Assert.Throws<ArgumentNullException>(() =>
            host.QueueHumanOccupationBatch(Human, null!));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupationBatch(Ai,
                new[] { Change(1UL, PhysicalWorkerOccupation.Mining) }));
        Assert.Equal(previous, host.GetHumanOrders(Human));
        Assert.Equal(hash, host.StateHash);

        // A subsequent valid batch replaces only included worker orders:
        // worker #5's older order remains present.
        Assert.Equal(1, host.QueueHumanOccupationBatch(Human,
            new[] { Change(1UL, PhysicalWorkerOccupation.Forestry) }));
        var updated = host.GetHumanOrders(Human);
        Assert.Equal(2, updated.Count);
        Assert.Equal(PhysicalWorkerOccupation.Forestry,
            updated.Single(order => order.WorkerId == first).NextOccupation);
        Assert.Equal(previous.Single(order => order.WorkerId == second),
            updated.Single(order => order.WorkerId == second));
        Assert.Equal(hash, host.StateHash);
    }

    [Fact]
    public void ReadyOrLegacyWorkersCannotUseBulkOrders()
    {
        var legacy = FoundationTacticalDemoScenario
            .CreateLocalHumanAiPreview(Request());
        var oldHash = legacy.StateHash;
        Assert.Equal(10U, oldHash.FormatVersion);
        Assert.Throws<InvalidOperationException>(() =>
            legacy.QueueHumanOccupationBatch(Human,
                new[] { Change(1UL, PhysicalWorkerOccupation.Mining) }));
        Assert.Equal(oldHash, legacy.StateHash);
        Assert.Equal(0, legacy.PendingOrderCount);

        var host = Start();
        var hash = host.StateHash;
        host.SetHumanReady(Human);
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOccupationBatch(Human,
                new[] { Change(1UL, PhysicalWorkerOccupation.Mining) }));
        Assert.Equal(0, host.PendingOrderCount);
        Assert.Equal(hash, host.StateHash);
    }

    [Fact]
    public void ThirtySixWorkerBatchPreservesIdentitiesAndAllowsEmptyRoleWithoutPhantoms()
    {
        var host = Start(36);
        var before = host.Snapshot;
        var hash = host.StateHash;
        var batch = before.Workers.Workers
            .Where(w => w.Owner == Human)
            .OrderByDescending(w => w.Id.Value)
            .Select(w => new FoundationTacticalOccupationChange(w.Id,
                w.Occupation == PhysicalWorkerOccupation.Agriculture
                    ? PhysicalWorkerOccupation.Forestry
                    : PhysicalWorkerOccupation.Agriculture))
            .ToArray();
        Assert.Equal(36, batch.Length);
        Assert.Equal(36, host.QueueHumanOccupationBatch(Human, batch));
        Assert.Equal(36, host.GetHumanOrders(Human).Count);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal((12, 12, 12), Counts(host));

        host.SetHumanReady(Human);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(11U, receipt.Hash.FormatVersion);
        Assert.Equal(72, host.Snapshot.Workers.Workers.Count);
        Assert.Equal((24, 12, 0), Counts(host));
        Assert.Equal(72,
            FoundationWorkforceActivityGroups.Project(host.Snapshot.Workers)
                .Sum(group => group.Count));
        Assert.Equal(72, host.Snapshot.Workers.Workers.Select(w => w.Id)
            .Distinct().Count());
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(before));
    }

    private static (int Agriculture, int Forestry, int Mining) Counts(
        FoundationTacticalHumanAiTurnHost host)
    {
        var workers = host.Snapshot.Workers.Workers
            .Where(worker => worker.Owner == Human).ToArray();
        return (
            workers.Count(w => w.Occupation == PhysicalWorkerOccupation.Agriculture),
            workers.Count(w => w.Occupation == PhysicalWorkerOccupation.Forestry),
            workers.Count(w => w.Occupation == PhysicalWorkerOccupation.Mining));
    }
}
