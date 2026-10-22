using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalTileRuntimeInspectorTests
{
    private static readonly GoldbergScaledRefinement Refinement = new(
        new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));

    private static (PhysicalTacticalIncidenceMap Map, WorldGenerationResult World) Fixture(ulong seed = 20261012UL)
    {
        var map = PhysicalTacticalIncidenceMapper.Materialize(Refinement);
        var world = new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial, Refinement.CoarseParameters));
        return (map, world);
    }

    [Fact]
    public void WithoutRuntimeStatePreservesCanonicalIncidenceAndNeverInventsOwners()
    {
        var (map, world) = Fixture();
        var incidence = map.TileIncidences.First(item => item.IncidentCoarseCellIds.Count == 3);
        var inspected = TacticalTileRuntimeInspector.Read(map, world, incidence.PhysicalTacticalTileId, null);
        Assert.Equal(TacticalRuntimeAvailability.NoRuntimeState, inspected.Availability);
        Assert.Null(inspected.Revision);
        Assert.Equal(incidence.IncidentCoarseCellIds, inspected.IncidentControls.Select(fact => fact.StrategicCellId));
        Assert.All(inspected.IncidentControls, fact => Assert.Null(fact.Controller));
    }

    [Fact]
    public void UnboundWorldIsDifferentFromBoundWorldWithoutControl()
    {
        var (map, world) = Fixture();
        var id = map.TileIncidences[0].PhysicalTacticalTileId;
        var unbound = TacticalTileRuntimeInspector.Read(map, world, id, WorldState.CreateInitial());
        var bound = TacticalTileRuntimeInspector.Read(map, world, id, WorldState.CreateBound(world));
        Assert.Equal(TacticalRuntimeAvailability.UnboundWorld, unbound.Availability);
        Assert.Equal(TacticalRuntimeAvailability.Bound, bound.Availability);
        Assert.Null(unbound.Revision);
        Assert.True(bound.Revision.HasValue);
        Assert.Equal(0UL, bound.Revision.Value);
        Assert.All(bound.IncidentControls, fact => Assert.Null(fact.Controller));
    }

    [Fact]
    public void BoundStateReadsActualControlFromEveryIncidentStrategicCellWithoutMutatingRevision()
    {
        var (map, world) = Fixture();
        var incidence = map.TileIncidences.First(item => item.IncidentCoarseCellIds.Count == 2);
        var controlled = incidence.IncidentCoarseCellIds[0];
        var controller = new CivilizationId(1UL);
        var state = WorldState.CreateBound(world)
            .WithCivilizations(new CivilizationRuntimeState(new[] { controller }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[] {
                new StrategicTerritoryControlEntry(controlled, controller) }));
        var revBefore = state.Revision;
        var result = TacticalTileRuntimeInspector.Read(map, world, incidence.PhysicalTacticalTileId, state);
        Assert.Equal(TacticalRuntimeAvailability.Bound, result.Availability);
        Assert.Equal(2, result.IncidentControls.Count);
        Assert.Equal(controlled, result.IncidentControls[0].StrategicCellId);
        Assert.True(result.IncidentControls[0].Controller.HasValue);
        Assert.Equal(controller, result.IncidentControls[0].Controller.GetValueOrDefault());
        Assert.Null(result.IncidentControls[1].Controller);
        Assert.Equal(revBefore, state.Revision);
        Assert.True(result.Revision.HasValue);
        Assert.Equal(revBefore, result.Revision.Value);
    }

    [Fact]
    public void RejectsControlFromMismatchedWorldSignature()
    {
        var (map, world) = Fixture();
        var other = Fixture(20261013UL).World;
        var state = WorldState.CreateBound(other);
        var result = TacticalTileRuntimeInspector.Read(map, world, map.TileIncidences[0].PhysicalTacticalTileId, state);
        Assert.Equal(TacticalRuntimeAvailability.WorldMismatch, result.Availability);
        Assert.Null(result.Revision);
        Assert.All(result.IncidentControls, fact => Assert.Null(fact.Controller));
    }

    [Fact]
    public void RejectsTileOutsideCurrentPhysicalIdentity()
    {
        var (map, world) = Fixture();
        var differentFine = new GoldbergParameters(12, 0);
        var invalid = new PhysicalTacticalTileId(differentFine, new StrategicCellId(1UL));
        Assert.Throws<ArgumentException>(() => TacticalTileRuntimeInspector.Read(map, world, invalid, null));
    }
}
