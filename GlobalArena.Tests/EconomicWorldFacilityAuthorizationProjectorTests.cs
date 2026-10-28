using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicWorldFacilityAuthorizationProjectorTests
{
    private static readonly GoldbergParameters Fine = new(4, 0);
    private static readonly PhysicalTacticalTileId Junction =
        new(Fine, new StrategicCellId(2UL));
    private static readonly PhysicalTacticalTileId Elsewhere =
        new(Fine, new StrategicCellId(3UL));
    private static readonly EconomicPhysicalResourceId Resource = new(101UL);

    private static TransportFacilityTransferCapability Forward() =>
        new(10UL, 20UL, 3UL);

    private static TransportFacilityTransferCapability Reverse() =>
        new(20UL, 10UL, 3UL);

    private static TransportFacilityRuntimeRecord Facility(
        bool operational = true, PhysicalTacticalTileId? location = null,
        IEnumerable<TransportFacilityTransferCapability>? capabilities = null,
        ulong facilityId = 900UL) =>
        new(facilityId, location ?? Junction, operational,
            capabilities ?? new[] { Forward() });

    private static WorldState BoundWorld() =>
        WorldState.CreateBound(new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(1UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0))));

    private static WorldState WithFacility(
        TransportFacilityRuntimeRecord? facility = null) =>
        BoundWorld().WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[] { facility ?? Facility() }));

    private static EconomicFineTransferFacilityEvidence Binding(
        PhysicalTacticalTileId? location = null, bool operational = true,
        ulong from = 10UL, ulong to = 20UL,
        ulong version = 3UL, ulong id = 900UL) =>
        new(id, Resource, location ?? Junction,
            from, to, version, operational);

    private static EconomicFineTransferFacilityAuthorizationSnapshot Project(
        WorldState world,
        IEnumerable<EconomicFineTransferFacilityEvidence>? evidence = null,
        WorldStateHash? expected = null) =>
        new EconomicWorldFacilityAuthorizationProjector().Project(
            7UL, world, expected ?? new CanonicalWorldStateHasher().Compute(world),
            evidence ?? new[] { Binding() });

    [Fact]
    public void EmptyWorldCannotInventAConstructedFacility()
    {
        var state = BoundWorld();
        Assert.Empty(state.TransportFacilities.Facilities);
        Assert.Throws<InvalidOperationException>(() => Project(state));
        Assert.Throws<InvalidOperationException>(() =>
            Project(WorldState.CreateInitial()));
    }

    [Fact]
    public void RecordedOperationalAndDirectedFacilityProjectsExactEvidence()
    {
        var world = WithFacility();
        var snapshot = Project(world);
        Assert.Equal(7UL, snapshot.TurnOrdinal);
        var binding = new EconomicPhysicalTransferResourceBinding(
            50UL, 0, Resource, Junction);
        Assert.Equal(900UL, snapshot.RequireOperationalHandoff(
            binding, 10UL, 20UL, 3UL));
        Assert.Throws<InvalidOperationException>(() =>
            snapshot.RequireOperationalHandoff(binding, 20UL, 10UL, 3UL));
    }

    [Fact]
    public void ReadOnlyProjectionRejectsStaleWorldAndModifiedFacilityStatus()
    {
        var world = WithFacility();
        var hash = new CanonicalWorldStateHasher().Compute(world);
        var closed = world.WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[] { Facility(operational: false) }));
        Assert.NotEqual(hash, new CanonicalWorldStateHasher().Compute(closed));
        Assert.Throws<InvalidOperationException>(() =>
            Project(closed, expected: hash));
        Assert.Throws<InvalidOperationException>(() => Project(closed));
        Assert.Equal(900UL, Project(world).RequireOperationalHandoff(
            new EconomicPhysicalTransferResourceBinding(50UL, 0, Resource, Junction),
            10UL, 20UL, 3UL));
    }

    [Fact]
    public void WrongTileWrongDirectionAndWrongVersionAreNotGranted()
    {
        var world = WithFacility();
        Assert.Throws<InvalidOperationException>(() =>
            Project(world, evidence: new[] { Binding(location: Elsewhere) }));
        Assert.Throws<InvalidOperationException>(() =>
            Project(world, evidence: new[] { Binding(from: 20UL, to: 10UL) }));
        Assert.Throws<InvalidOperationException>(() =>
            Project(world, evidence: new[] { Binding(version: 4UL) }));
        Assert.Throws<InvalidOperationException>(() =>
            Project(world, evidence: new[] { Binding(id: 901UL) }));
        Assert.Throws<InvalidOperationException>(() =>
            Project(world, evidence: new[] { Binding(operational: false) }));
    }

    [Fact]
    public void DuplicateIdentitiesAndCapabilitiesFailClosed()
    {
        Assert.Throws<ArgumentException>(() => new TransportFacilitiesRuntimeState(
            new[] { Facility(), Facility() }));
        Assert.Throws<ArgumentException>(() => Facility(
            capabilities: new[] { Forward(), Forward() }));
        Assert.Throws<ArgumentException>(() =>
            new TransportFacilityTransferCapability(10UL, 10UL, 3UL));
        Assert.Throws<InvalidOperationException>(() =>
            WorldState.CreateInitial().WithTransportFacilities(
                new TransportFacilitiesRuntimeState(new[] { Facility() })));
    }

    [Fact]
    public void CanonicalHashIsIndependentOfRecordAndCapabilityInputOrder()
    {
        var one = BoundWorld().WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[]
            {
                Facility(capabilities: new[] { Forward(), Reverse() }),
                Facility(facilityId: 901UL, capabilities: new[] { Reverse() })
            }));
        var two = BoundWorld().WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[]
            {
                Facility(facilityId: 901UL, capabilities: new[] { Reverse() }),
                Facility(capabilities: new[] { Reverse(), Forward() })
            }));
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(hasher.Compute(one), hasher.Compute(two));
        Assert.Equal(9U, hasher.Compute(one).FormatVersion);
        Assert.NotEqual(hasher.Compute(BoundWorld()), hasher.Compute(one));
        Assert.NotEqual(hasher.Compute(one), hasher.Compute(one.AdvanceRevision()));
        Assert.Equal(2, one.AdvanceRevision().TransportFacilities.Facilities.Count);
    }

    [Fact]
    public void CanonicalHashChangesForFineTileDirectionAndOperationalStatus()
    {
        var baseline = WithFacility();
        var hasher = new CanonicalWorldStateHasher();
        var original = hasher.Compute(baseline);
        Assert.NotEqual(original, hasher.Compute(
            WithFacility(Facility(location: Elsewhere))));
        Assert.NotEqual(original, hasher.Compute(
            WithFacility(Facility(capabilities: new[] { Reverse() }))));
        Assert.NotEqual(original, hasher.Compute(
            WithFacility(Facility(operational: false))));
        Assert.NotEqual(original, hasher.Compute(
            WithFacility(Facility(facilityId: 901UL))));
    }

    [Fact]
    public void WorldStateReplacementIsSnapshotLocalAndDoesNotCreateEconomicFlow()
    {
        var initial = BoundWorld();
        var initialHash = new CanonicalWorldStateHasher().Compute(initial);
        var withRecords = initial.WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[] { Facility() }));
        Assert.Empty(initial.TransportFacilities.Facilities);
        Assert.Equal(initialHash, new CanonicalWorldStateHasher().Compute(initial));
        Assert.Equal(initial.Revision, withRecords.Revision);
        Assert.Empty(withRecords.Economy.EconomicPoints);
        Assert.Empty(withRecords.Warfare.Units);
    }
}
