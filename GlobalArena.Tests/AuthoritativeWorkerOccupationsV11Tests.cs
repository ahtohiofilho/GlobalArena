using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>v11 occupation is an independent canonical individual property,
/// not an old aggregate workforce or a commodity-derived presentation trick.</summary>
public sealed class AuthoritativeWorkerOccupationsV11Tests
{
    private static WorldGenerationRequest Request() => new(
        new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0));

    private static FoundationTacticalHumanAiTurnHost Occupied() =>
        FoundationTacticalDemoScenario.CreateLocalHumanAiOccupationPreview(Request());

    [Fact]
    public void ExplicitOccupationMigrationKeepsSixPeopleAndHashesV8V9V10ByteCompatible()
    {
        var v10 = FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(Request());
        var before = v10.Snapshot;
        var original = v10.StateHash;
        var v11 = Occupied();
        Assert.Equal(10U, original.FormatVersion);
        Assert.Equal(10U, new CanonicalWorldStateHasher().Compute(before).FormatVersion);
        Assert.Equal(11U, v11.StateHash.FormatVersion);
        Assert.NotEqual(original.HexDigest, v11.StateHash.HexDigest);
        Assert.Equal(v11.StateHash, Occupied().StateHash);
        Assert.Equal(0UL, v11.Snapshot.Revision);
        Assert.True(v11.Snapshot.Workers.HasAuthoritativeOccupations);
        Assert.False(before.Workers.HasAuthoritativeOccupations);
        Assert.Equal(6, v11.Snapshot.Workers.Workers.Count);
        for (var i = 0; i < v10.Snapshot.Workers.Workers.Count; i++)
        {
            var old = v10.Snapshot.Workers.Workers[i];
            var next = v11.Snapshot.Workers.Workers[i];
            Assert.Equal(old.Id, next.Id);
            Assert.Equal(old.Owner, next.Owner);
            Assert.Equal(old.CellId, next.CellId);
            Assert.Equal(old.TacticalTileId, next.TacticalTileId);
            Assert.Equal(old.Activity, next.Activity);
            Assert.Equal(old.Commodity, next.Commodity);
            Assert.Equal(old.OutputMicroPerTurn, next.OutputMicroPerTurn);
            Assert.Equal(old.DemandMultiplierMicro, next.DemandMultiplierMicro);
        }
        var w = v11.Snapshot.Workers.Workers.ToDictionary(x => x.Id.Value);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, w[1].Occupation);
        Assert.Equal(PhysicalWorkerOccupation.Forestry, w[2].Occupation);
        Assert.Equal(PhysicalWorkerOccupation.Unassigned, w[3].Occupation);
        Assert.Equal(PhysicalWorkerOccupation.Mining, w[4].Occupation);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, w[5].Occupation);
        Assert.Equal(PhysicalWorkerOccupation.Unassigned, w[6].Occupation);
        Assert.Equal(2, w.Values.Count(x => x.Occupation == PhysicalWorkerOccupation.Agriculture));
        Assert.Equal(1, w.Values.Count(x => x.Occupation == PhysicalWorkerOccupation.Mining));
        Assert.Equal(1, w.Values.Count(x => x.Occupation == PhysicalWorkerOccupation.Forestry));
    }

    [Fact]
    public void InvalidOrPartialOccupationSchemasAreRejectedWithNoStateMutation()
    {
        var v10 = FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(Request());
        var source = v10.Snapshot;
        var all = source.Workers.Workers.ToDictionary(
            x => x.Id, x => PhysicalWorkerOccupation.Forestry);
        all.Remove(new PhysicalWorkerId(1UL));
        Assert.Throws<InvalidOperationException>(() =>
            source.ClassifyTacticalWorkerOccupations(all));
        all[new PhysicalWorkerId(1UL)] = (PhysicalWorkerOccupation)99;
        Assert.Throws<InvalidOperationException>(() =>
            source.ClassifyTacticalWorkerOccupations(all));
        all[new PhysicalWorkerId(1UL)] = PhysicalWorkerOccupation.Unassigned;
        Assert.Throws<InvalidOperationException>(() =>
            source.ClassifyTacticalWorkerOccupations(all));
        Assert.Throws<ArgumentException>(() =>
            new PhysicalWorkerRuntimeRecord(
                source.Workers.Workers[0].Id,
                source.Workers.Workers[0].Owner,
                source.Workers.Workers[0].CellId,
                source.Workers.Workers[0].Commodity,
                source.Workers.Workers[0].OutputMicroPerTurn,
                source.Workers.Workers[0].Activity,
                occupation: PhysicalWorkerOccupation.Mining));
        Assert.Equal(10U, v10.StateHash.FormatVersion);
        Assert.Same(source, v10.Snapshot);

        var classified = Occupied();
        Assert.Throws<InvalidOperationException>(() =>
            classified.Snapshot.ClassifyTacticalWorkerOccupations(
                classified.Snapshot.Workers.Workers.ToDictionary(
                    worker => worker.Id,
                    worker => worker.Occupation!.Value)));
        Assert.Throws<InvalidOperationException>(() =>
            classified.Snapshot.WithWorkers(v10.Snapshot.Workers));
        Assert.Throws<ArgumentException>(() =>
            new PhysicalWorkerRuntimeState(
                classified.Snapshot.Workers.Workers,
                tacticalLocationsAuthoritative: true));
        Assert.Throws<ArgumentException>(() =>
            new PhysicalWorkerRuntimeState(
                v10.Snapshot.Workers.Workers,
                tacticalLocationsAuthoritative: true,
                occupationsAuthoritative: true));
    }

    [Fact]
    public void ChangingExplicitOccupationAloneChangesV11HashWithoutCreatingOutput()
    {
        var v10 = FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(Request());
        var roles = new Dictionary<PhysicalWorkerId, PhysicalWorkerOccupation>
        {
            [new(1UL)] = PhysicalWorkerOccupation.Forestry,
            [new(2UL)] = PhysicalWorkerOccupation.Agriculture,
            [new(3UL)] = PhysicalWorkerOccupation.Unassigned,
            [new(4UL)] = PhysicalWorkerOccupation.Mining,
            [new(5UL)] = PhysicalWorkerOccupation.Forestry,
            [new(6UL)] = PhysicalWorkerOccupation.Unassigned
        };
        var modified = v10.Snapshot.ClassifyTacticalWorkerOccupations(roles);
        var demo = Occupied().Snapshot;
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(11U, hash.Compute(modified).FormatVersion);
        Assert.NotEqual(hash.Compute(demo), hash.Compute(modified));
        Assert.Equal(demo.Revision, modified.Revision);
        Assert.Equal(demo.Workers.Workers.Aggregate(0UL, (total, w) => checked(total + w.OutputMicroPerTurn)),
            modified.Workers.Workers.Aggregate(0UL, (total, w) => checked(total + w.OutputMicroPerTurn)));
        Assert.Equal(demo.Workers.Workers.Aggregate(0UL, (total, w) => checked(total + w.DemandMultiplierMicro)),
            modified.Workers.Workers.Aggregate(0UL, (total, w) => checked(total + w.DemandMultiplierMicro)));
    }

    [Fact]
    public void RealHumanAndAiFineMovesPreserveWorkOccupationAndV11Replay()
    {
        var host = Occupied();
        var before = host.Snapshot;
        var worker = before.Workers.Workers.Single(w => w.Id.Value == 1UL);
        var oldOccupation = worker.Occupation;
        var neighbor = host.Incidence.FineTopology.Cells[
            checked((int)worker.TacticalTileId!.Value.FineStrategicCellId.Value - 1)]
            .AdjacentCellIds[0];
        var target = new PhysicalTacticalTileId(
            host.Incidence.Refinement.FineParameters, neighbor);
        host.QueueHumanMove(new CivilizationId(1UL), worker.Id, target);
        Assert.Same(before, host.Snapshot);
        host.SetHumanReady(new CivilizationId(1UL));
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.Equal(11U, receipt.Hash.FormatVersion);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(2, receipt.ExecutedEvents);
        var after = host.Snapshot.Workers.Workers.Single(w => w.Id == worker.Id);
        Assert.Equal(target, after.TacticalTileId);
        Assert.Equal(PhysicalWorkerActivity.Traveling, after.Activity);
        Assert.Equal(oldOccupation, after.Occupation);
        var ai = host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL);
        Assert.Equal(PhysicalWorkerOccupation.Mining, ai.Occupation);
        Assert.Equal(PhysicalWorkerActivity.Producing, ai.Activity);
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
        Assert.Equal(2UL, host.Snapshot.Revision);
        Assert.Equal(11U, host.StateHash.FormatVersion);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.NotEqual(new CanonicalWorldStateHasher().Compute(before), host.StateHash);

        host.QueueHumanActivity(new CivilizationId(1UL), worker.Id,
            PhysicalWorkerActivity.Producing);
        host.SetHumanReady(new CivilizationId(1UL));
        var second = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.Equal(11U, second.Hash.FormatVersion);
        Assert.True(second.ReplayMatched);
        var resumed = host.Snapshot.Workers.Workers.Single(w => w.Id == worker.Id);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, resumed.Occupation);
        Assert.Equal(target, resumed.TacticalTileId);
        Assert.Equal(PhysicalWorkerActivity.Producing, resumed.Activity);
    }
}
