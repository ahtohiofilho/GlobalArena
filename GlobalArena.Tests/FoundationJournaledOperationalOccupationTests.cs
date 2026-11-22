using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationJournaledOperationalOccupationTests
{
    private static readonly CivilizationId Human = new(1UL);
    private static readonly CivilizationId Ai = new(2UL);

    [Theory]
    [InlineData(FoundationOperationalWorkKind.Land,
        PhysicalWorkerOccupation.LandTransport)]
    [InlineData(FoundationOperationalWorkKind.Port,
        PhysicalWorkerOccupation.PortOperations)]
    public void OperationalRoleCommitsAsOneRealWorkerEventWithoutDoubleProduction(
        FoundationOperationalWorkKind operational,
        PhysicalWorkerOccupation expectedOccupation)
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var worker = fixture.Snapshot.Workers.Workers.First(w =>
            w.Owner == Human && w.Activity == PhysicalWorkerActivity.Producing);
        // R147 requires the individual to be reassigned from production to
        // Other first. The role change itself is committed in one real turn.
        var census = fixture.Snapshot.Workers.Workers.Select(w =>
            w.Id == worker.Id
                ? w.WithTacticalLocationAndActivity(
                    w.TacticalTileId!.Value, w.CellId,
                    PhysicalWorkerActivity.Other)
                : w);
        fixture.Session.AcceptSnapshot(fixture.Snapshot.WithWorkers(
            new PhysicalWorkerRuntimeState(census,
                tacticalLocationsAuthoritative: true,
                occupationsAuthoritative: true)));
        var host = new FoundationTacticalHumanAiTurnHost(
            fixture.Session, fixture.Incidence, new[] { Human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Ai] = new FoundationTacticalAiHoldPolicy()
            });
        var initial = host.StateHash;
        host.QueueHumanOperationalRole(Human, worker.Id, operational);
        Assert.Equal(initial, host.StateHash);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture,
            host.Snapshot.Workers.Workers.Single(w => w.Id == worker.Id).Occupation);
        host.SetHumanReady(Human);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        var after = host.Snapshot.Workers.Workers.Single(w => w.Id == worker.Id);
        Assert.Equal(expectedOccupation, after.Occupation);
        Assert.Equal(PhysicalWorkerActivity.Other, after.Activity);
        Assert.Equal(worker.TacticalTileId, after.TacticalTileId);
        Assert.NotEqual(initial, host.StateHash);
        Assert.Single(receipt.Events.Where(e => e.WorkerId == worker.Id));
    }

    [Fact]
    public void ProductionCannotResumeWhileWorkerHasLogisticsOccupation()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var worker = fixture.Snapshot.Workers.Workers.First(w =>
            w.Owner == Human && w.Activity == PhysicalWorkerActivity.Producing);
        var assigned = fixture.Snapshot.Workers.Workers.Select(w =>
            w.Id != worker.Id ? w : new PhysicalWorkerRuntimeRecord(
                w.Id, w.Owner, w.CellId, w.Commodity,
                w.OutputMicroPerTurn, PhysicalWorkerActivity.Other,
                w.DemandMultiplierMicro, w.TacticalTileId,
                PhysicalWorkerOccupation.LandTransport));
        fixture.Session.AcceptSnapshot(fixture.Snapshot.WithWorkers(
            new PhysicalWorkerRuntimeState(assigned,
                tacticalLocationsAuthoritative: true,
                occupationsAuthoritative: true)));
        var host = new FoundationTacticalHumanAiTurnHost(
            fixture.Session, fixture.Incidence, new[] { Human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Ai] = new FoundationTacticalAiHoldPolicy()
            });
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanActivity(Human, worker.Id,
                PhysicalWorkerActivity.Producing));
        host.QueueHumanOccupation(Human, worker.Id,
            PhysicalWorkerOccupation.Agriculture);
        host.SetHumanReady(Human);
        var receipt = host.TryCloseTurn();
        Assert.True(receipt!.ReplayMatched);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture,
            host.Snapshot.Workers.Workers.Single(w => w.Id == worker.Id).Occupation);
        host.QueueHumanActivity(Human, worker.Id,
            PhysicalWorkerActivity.Producing);
    }
}
