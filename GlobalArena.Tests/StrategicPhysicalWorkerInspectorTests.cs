using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Godot-independent contract for the EXACT read-only inspector used by the
/// strategic selected-cell panel. No authored example state inside the UI.
/// </summary>
public sealed class StrategicPhysicalWorkerInspectorTests
{
    private static CanonicalGameSession Session(ulong seed = 20261012UL) =>
        CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));

    private static CanonicalGameSession Materialized()
    {
        var session = Session();
        session.AcceptSnapshot(session.Snapshot
            .WithCivilizations(new CivilizationRuntimeState(new[]
            {
                new CivilizationId(1UL), new CivilizationId(2UL)
            }))
            .WithWorkers(new PhysicalWorkerRuntimeState(new[]
            {
                new PhysicalWorkerRuntimeRecord(
                    new PhysicalWorkerId(9UL), new CivilizationId(1UL),
                    new StrategicCellId(1UL), new CommodityId(1U),
                    2_000_000UL, PhysicalWorkerActivity.Producing,
                    1_250_000U),
                new PhysicalWorkerRuntimeRecord(
                    new PhysicalWorkerId(2UL), new CivilizationId(2UL),
                    new StrategicCellId(3UL), new CommodityId(2U),
                    4_000_000UL, PhysicalWorkerActivity.Traveling,
                    1_000_000U),
                new PhysicalWorkerRuntimeRecord(
                    new PhysicalWorkerId(5UL), new CivilizationId(2UL),
                    new StrategicCellId(1UL), new CommodityId(2U),
                    0UL, PhysicalWorkerActivity.Military,
                    800_000U),
                new PhysicalWorkerRuntimeRecord(
                    new PhysicalWorkerId(6UL), new CivilizationId(1UL),
                    new StrategicCellId(1UL), new CommodityId(1U),
                    0UL, PhysicalWorkerActivity.Other)
            })));
        return session;
    }

    [Fact]
    public void AbsentSnapshotHasItsOwnExplicitAvailabilityAndNoInventedWorker()
    {
        var s = Session();
        var result = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(1UL), null);
        Assert.Equal(StrategicWorkerRuntimeAvailability.NoRuntimeState,
            result.Availability);
        Assert.Null(result.Revision);
        Assert.Empty(result.Workers);
        Assert.Equal(0, result.DemandingWorkerCount);
    }

    [Fact]
    public void UnboundWorldDoesNotExposeWorkerFacts()
    {
        var s = Session();
        var result = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(1UL), WorldState.CreateInitial());
        Assert.Equal(StrategicWorkerRuntimeAvailability.UnboundWorld,
            result.Availability);
        Assert.Empty(result.Workers);
        Assert.Null(result.Revision);
    }

    [Fact]
    public void WrongGeneratedWorldNeverLeaksCensusFacts()
    {
        var s = Materialized();
        var different = Session(seed: 20261013UL);
        var result = StrategicPhysicalWorkerInspector.Read(
            different.GeneratedWorld, new StrategicCellId(1UL), s.Snapshot);
        Assert.Equal(StrategicWorkerRuntimeAvailability.WorldMismatch,
            result.Availability);
        Assert.Empty(result.Workers);
        Assert.Null(result.Revision);
    }

    [Fact]
    public void UninitializedCensusIsNotMisrepresentedAsZeroWorkers()
    {
        var s = Session();
        var result = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(1UL), s.Snapshot);
        Assert.Equal(StrategicWorkerRuntimeAvailability.CensusUninitialized,
            result.Availability);
        Assert.Equal(0UL, result.Revision);
        Assert.Empty(result.Workers);
    }

    [Fact]
    public void InitializedEmptyCensusReturnsBoundEmptyTileNotUnavailable()
    {
        var s = Session();
        var state = s.Snapshot
            .WithWorkers(new PhysicalWorkerRuntimeState(
                Array.Empty<PhysicalWorkerRuntimeRecord>()));
        var result = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(90UL), state);
        Assert.Equal(StrategicWorkerRuntimeAvailability.Bound,
            result.Availability);
        Assert.Equal(0UL, result.Revision);
        Assert.Empty(result.Workers);
    }

    [Fact]
    public void SelectedStrategicTileHasOnlyItsOwnPhysicalWorkersInIdOrder()
    {
        var s = Materialized();
        var result = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(1UL), s.Snapshot);
        Assert.Equal(StrategicWorkerRuntimeAvailability.Bound,
            result.Availability);
        Assert.Equal(0UL, result.Revision);
        Assert.Equal(new ulong[] { 5UL, 6UL, 9UL },
            result.Workers.Select(w => w.Id.Value));
        Assert.All(result.Workers, w =>
            Assert.Equal(new StrategicCellId(1UL), w.CellId));
        Assert.Equal(3, result.DemandingWorkerCount);
        Assert.Equal(1, result.ProducingWorkerCount);
        Assert.Equal(0, result.TravelingWorkerCount);
        Assert.Equal(1, result.MilitaryWorkerCount);
        var farmer = result.Workers.Single(w => w.Id.Value == 9UL);
        Assert.Equal(new CivilizationId(1UL), farmer.Owner);
        Assert.Equal(2_000_000UL, farmer.OutputMicroPerTurn);
        Assert.Equal(1_250_000U, farmer.DemandMultiplierMicro);
        Assert.Equal(new CommodityId(1U), farmer.Commodity);
        var soldier = result.Workers.Single(w => w.Id.Value == 5UL);
        Assert.Equal(800_000U, soldier.DemandMultiplierMicro);
    }

    [Fact]
    public void SelectionOfNeighborDoesNotAccidentallyAggregateAdjacentWorkers()
    {
        var s = Materialized();
        var result = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(3UL), s.Snapshot);
        Assert.Equal(new ulong[] { 2UL }, result.Workers.Select(w => w.Id.Value));
        Assert.Equal(1, result.TravelingWorkerCount);
        Assert.Equal(0, result.ProducingWorkerCount);
        Assert.Equal(1, result.DemandingWorkerCount);
        Assert.Equal(4_000_000UL, result.Workers[0].OutputMicroPerTurn);
        // Capacity is not current production when worker travels.
        Assert.Equal(PhysicalWorkerActivity.Traveling, result.Workers[0].Activity);
    }

    [Fact]
    public void InvalidOrFineGridOnlyIdentifiersAreNeverAcceptedAsStrategicTiles()
    {
        var s = Materialized();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StrategicPhysicalWorkerInspector.Read(s.GeneratedWorld,
                default, s.Snapshot));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StrategicPhysicalWorkerInspector.Read(s.GeneratedWorld,
                new StrategicCellId(93UL), s.Snapshot));
        Assert.Throws<ArgumentNullException>(() =>
            StrategicPhysicalWorkerInspector.Read(null!,
                new StrategicCellId(1UL), s.Snapshot));
    }

    [Fact]
    public void InspectorDoesNotPublishOrAdvanceWorldStateAndIsReadOnly()
    {
        var s = Materialized();
        var before = s.Snapshot;
        var first = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(1UL), before);
        var second = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(1UL), before);
        Assert.Equal(first.Workers, second.Workers);
        Assert.Same(before, s.Snapshot);
        Assert.Equal(0UL, s.Snapshot.Revision);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<StrategicPhysicalWorkerFact>)first.Workers)
            .Add(first.Workers[0]));
        Assert.Equal(9U, new GlobalArena.Simulation.CanonicalWorldStateHasher()
            .Compute(s.Snapshot).FormatVersion);
    }

    [Fact]
    public void NewCanonicalSnapshotChangesInspectionWithoutCachedStaleData()
    {
        var s = Materialized();
        var old = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(1UL), s.Snapshot);
        Assert.Equal(3, old.DemandingWorkerCount);
        var moved = s.Snapshot.Workers.Workers.Select(w => w.Id.Value == 9UL
            ? w.WithLocationAndActivity(new StrategicCellId(3UL),
                PhysicalWorkerActivity.Traveling)
            : w);
        s.AcceptSnapshot(s.Snapshot
            .WithWorkers(new PhysicalWorkerRuntimeState(moved))
            .AdvanceRevision());

        var current = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(1UL), s.Snapshot);
        var destination = StrategicPhysicalWorkerInspector.Read(
            s.GeneratedWorld, new StrategicCellId(3UL), s.Snapshot);
        Assert.Equal(1UL, current.Revision);
        Assert.Equal(2, current.DemandingWorkerCount);
        Assert.Equal(2, destination.DemandingWorkerCount);
        Assert.Equal(2, destination.TravelingWorkerCount);
        Assert.Equal(3, old.DemandingWorkerCount); // old read remains immutable
    }
}
