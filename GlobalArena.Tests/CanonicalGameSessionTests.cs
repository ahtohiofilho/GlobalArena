using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class CanonicalGameSessionTests
{
    private static WorldGenerationRequest Request(ulong seed = 20261012UL) =>
        new(new WorldSeed(seed), WorldGenerationVersion.Initial, new GoldbergParameters(3, 0));

    [Fact]
    public void StartsWithRealBoundWorldWithoutInventedCivilizationsOrControls()
    {
        var session = CanonicalGameSession.Start(Request());
        Assert.True(session.Snapshot.IsWorldBound);
        Assert.Equal(0UL, session.Snapshot.Revision);
        Assert.Empty(session.Snapshot.Civilizations.Civilizations);
        Assert.Empty(session.Snapshot.Territory.Controls);
        Assert.Equal(92, session.GeneratedWorld.StrategicTopology.Cells.Count);
    }

    [Fact]
    public void BindingMatchesTheActualGeneratedWorld()
    {
        var session = CanonicalGameSession.Start(Request());
        Assert.Equal(RuntimeWorldBinding.FromGeneratedWorld(session.GeneratedWorld), session.Snapshot.WorldBinding);
    }

    [Fact]
    public void SameCanonicalRequestProducesMatchingWorldBindings()
    {
        var left = CanonicalGameSession.Start(Request());
        var right = CanonicalGameSession.Start(Request());
        Assert.Equal(left.Snapshot.WorldBinding, right.Snapshot.WorldBinding);
    }

    [Fact]
    public void CanonicalPhysicalTileInspectsBoundRuntimeWithoutFictitiousControl()
    {
        var session = CanonicalGameSession.Start(Request());
        var incidence = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(3, 0), new GoldbergParameters(18, 0)));
        var tile = incidence.TileIncidences[0].PhysicalTacticalTileId;
        var inspected = TacticalTileRuntimeInspector.Read(incidence, session.GeneratedWorld, tile, session.Snapshot);
        Assert.Equal(TacticalRuntimeAvailability.Bound, inspected.Availability);
        Assert.Equal(0UL, inspected.Revision);
        Assert.NotEmpty(inspected.IncidentControls);
        Assert.All(inspected.IncidentControls, entry => Assert.Null(entry.Controller));
    }

    [Fact]
    public void AcceptSnapshotPreservesAuthoritativeWorldBinding()
    {
        var session = CanonicalGameSession.Start(Request());
        var revision = session.Snapshot.AdvanceRevision();
        session.AcceptSnapshot(revision);
        Assert.Same(revision, session.Snapshot);
        Assert.Equal(1UL, session.Snapshot.Revision);
    }

    [Fact]
    public void RejectsSnapshotFromDifferentGeneratedWorld()
    {
        var session = CanonicalGameSession.Start(Request());
        var another = CanonicalGameSession.Start(Request(20261013UL));
        Assert.Throws<InvalidOperationException>(() => session.AcceptSnapshot(another.Snapshot));
    }

    [Fact]
    public void RejectsRevisionRegression()
    {
        var session = CanonicalGameSession.Start(Request());
        var initial = session.Snapshot;
        session.AcceptSnapshot(initial.AdvanceRevision());
        Assert.Throws<InvalidOperationException>(() => session.AcceptSnapshot(initial));
    }

    [Fact]
    public void RejectsUnboundRuntimeSnapshot()
    {
        var session = CanonicalGameSession.Start(Request());
        Assert.Throws<InvalidOperationException>(() => session.AcceptSnapshot(WorldState.CreateInitial()));
    }
}
