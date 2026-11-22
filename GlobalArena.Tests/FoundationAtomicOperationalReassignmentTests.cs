using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationAtomicOperationalReassignmentTests
{
    private static readonly CivilizationId Human = new(1UL);
    private static readonly CivilizationId Ai = new(2UL);

    private static (FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeRecord Person) Open()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var person = fixture.Snapshot.Workers.Workers.First(w =>
            w.Owner == Human && w.Activity == PhysicalWorkerActivity.Producing);
        var host = new FoundationTacticalHumanAiTurnHost(
            fixture.Session, fixture.Incidence, new[] { Human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Ai] = new FoundationTacticalAiHoldPolicy()
            });
        return (host, person);
    }

    [Theory]
    [InlineData(FoundationOperationalWorkKind.Land,
        PhysicalWorkerOccupation.LandTransport)]
    [InlineData(FoundationOperationalWorkKind.Port,
        PhysicalWorkerOccupation.PortOperations)]
    public void ProducingWorkerBecomesOperatorInOneTurnWithOneReplayableEvent(
        FoundationOperationalWorkKind role,
        PhysicalWorkerOccupation occupation)
    {
        var (host, person) = Open();
        var beforeHash = host.StateHash;
        var before = FoundationPhysicalWorkforceEconomicProjector.Project(
            host.Snapshot.Workers);
        host.QueueHumanOperationalRole(Human, person.Id, role);
        Assert.Equal(1, host.PendingOrderCount);
        Assert.Equal(beforeHash, host.StateHash);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            host.Snapshot.Workers.Workers.Single(w =>
                w.Id == person.Id).Activity);
        host.SetHumanReady(Human);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        var after = host.Snapshot.Workers.Workers.Single(w =>
            w.Id == person.Id);
        Assert.Equal(PhysicalWorkerActivity.Other, after.Activity);
        Assert.Equal(occupation, after.Occupation);
        Assert.Equal(person.TacticalTileId, after.TacticalTileId);
        Assert.Equal(person.Owner, after.Owner);
        Assert.Single(receipt.Events, e => e.WorkerId == person.Id);
        var projected = FoundationPhysicalWorkforceEconomicProjector.Project(
            host.Snapshot.Workers);
        Assert.Equal(before.ActiveProducingWorkerCount - 1,
            projected.ActiveProducingWorkerCount);
        Assert.Equal(before.TotalActiveCapacityMicroPerTurn -
            person.OutputMicroPerTurn, projected.TotalActiveCapacityMicroPerTurn);
        Assert.Equal(before.TotalDemandMultiplierMicro,
            projected.TotalDemandMultiplierMicro);
    }

    [Fact]
    public void ReturningFromLandLogisticsToProductionRequiresOnlyOneMoreTurn()
    {
        var (host, person) = Open();
        host.QueueHumanOperationalRole(Human, person.Id,
            FoundationOperationalWorkKind.Land);
        host.SetHumanReady(Human);
        Assert.True(host.TryCloseTurn()!.ReplayMatched);

        var hash = host.StateHash;
        host.QueueHumanProductiveRole(Human, person.Id,
            PhysicalWorkerOccupation.Mining);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(PhysicalWorkerActivity.Other,
            host.Snapshot.Workers.Workers.Single(w =>
                w.Id == person.Id).Activity);
        host.SetHumanReady(Human);
        var receipt = host.TryCloseTurn();
        Assert.True(receipt!.ReplayMatched);
        Assert.Single(receipt.Events, e => e.WorkerId == person.Id);
        var after = host.Snapshot.Workers.Workers.Single(w =>
            w.Id == person.Id);
        Assert.Equal(PhysicalWorkerActivity.Producing, after.Activity);
        Assert.Equal(PhysicalWorkerOccupation.Mining, after.Occupation);
        Assert.Equal(person.TacticalTileId, after.TacticalTileId);
    }

    [Fact]
    public void CancelledDirectLogisticsOrderLeavesProducerCompletelyUnchanged()
    {
        var (host, person) = Open();
        host.QueueHumanOperationalRole(Human, person.Id,
            FoundationOperationalWorkKind.Land);
        Assert.True(host.CancelHumanOrder(Human, person.Id));
        Assert.Equal(0, host.PendingOrderCount);
        host.SetHumanReady(Human);
        var receipt = host.TryCloseTurn();
        Assert.True(receipt!.ReplayMatched);
        Assert.DoesNotContain(receipt.Events, e => e.WorkerId == person.Id);
        var after = host.Snapshot.Workers.Workers.Single(w =>
            w.Id == person.Id);
        Assert.Equal(person.Occupation, after.Occupation);
        Assert.Equal(person.Activity, after.Activity);
        Assert.Equal(person.TacticalTileId, after.TacticalTileId);
    }

    [Fact]
    public void DirectOperationalOrderRejectsForeignOrUnchangedWorker()
    {
        var (host, person) = Open();
        var foreign = host.Snapshot.Workers.Workers.First(w => w.Owner == Ai);
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOperationalRole(Human, foreign.Id,
                FoundationOperationalWorkKind.Port));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            host.QueueHumanOperationalRole(Human, person.Id,
                (FoundationOperationalWorkKind)99));
        host.QueueHumanOperationalRole(Human, person.Id,
            FoundationOperationalWorkKind.Land);
        host.SetHumanReady(Human);
        Assert.True(host.TryCloseTurn()!.ReplayMatched);
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanOperationalRole(Human, person.Id,
                FoundationOperationalWorkKind.Land));
    }
}
