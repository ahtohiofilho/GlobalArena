using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicFacilityCommissioningPreflightTests
{
    private static readonly GoldbergParameters Fine = new(4, 0);
    private static readonly GoldbergParameters ForeignFine = new(5, 0);
    private static readonly PhysicalTacticalTileId Junction =
        new(Fine, new StrategicCellId(2UL));
    private static readonly PhysicalTacticalTileId Elsewhere =
        new(Fine, new StrategicCellId(3UL));
    private static readonly PhysicalTacticalTileId ForeignTile =
        new(ForeignFine, new StrategicCellId(2UL));

    private static TransportFacilityTransferCapability Forward() =>
        new(10UL, 20UL, 3UL);
    private static TransportFacilityTransferCapability Reverse() =>
        new(20UL, 10UL, 3UL);

    private static WorldGenerationResult Generate(ulong seed,
        GoldbergParameters parameters) =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(seed), WorldGenerationVersion.Initial,
                parameters));

    private static StrategicSurfaceGraph Geography() =>
        new(Generate(0UL, Fine).StrategicTopology);

    private static TransportFacilityRuntimeRecord Facility(
        ulong id = 900UL, bool operational = false,
        PhysicalTacticalTileId? tile = null,
        IEnumerable<TransportFacilityTransferCapability>? caps = null) =>
        new(id, tile ?? Junction, operational,
            caps ?? new[] { Forward(), Reverse() });

    private static WorldState World(
        params TransportFacilityRuntimeRecord[] facilities) =>
        WorldState.CreateBound(Generate(0UL, new GoldbergParameters(2, 0)))
            .WithTransportFacilities(new TransportFacilitiesRuntimeState(facilities));

    private static EconomicFacilityCommissioningIntent Intent(
        ulong id = 900UL, PhysicalTacticalTileId? tile = null,
        IEnumerable<TransportFacilityTransferCapability>? caps = null) =>
        new(id, tile ?? Junction, caps ?? new[] { Forward(), Reverse() });

    private static EconomicFacilityCommissioningPreflightResult Inspect(
        WorldState world,
        IEnumerable<EconomicFacilityCommissioningIntent>? intents = null,
        WorldStateHash? hash = null,
        StrategicSurfaceGraph? geography = null) =>
        new EconomicFacilityCommissioningPreflight().Inspect(
            7UL, world, hash ?? new CanonicalWorldStateHasher().Compute(world),
            geography ?? Geography(), intents ?? new[] { Intent() });

    [Fact]
    public void InactiveRecordedFacilityMayPassStructuralReviewWithoutBeingActivated()
    {
        var world = World(Facility());
        var before = new CanonicalWorldStateHasher().Compute(world);
        var result = Inspect(world);
        Assert.Equal(7UL, result.TurnOrdinal);
        Assert.Equal(world.Revision, result.WorldRevision);
        Assert.Equal(before, result.WorldHash);
        Assert.Equal(new[] { 900UL }, result.StructurallyValidatedFacilityIds);
        Assert.False(world.TransportFacilities.Facilities.Single().IsOperational);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
        Assert.Empty(world.Economy.EconomicPoints);
    }

    [Fact]
    public void PreflightCannotAuthorizeOperationalTransferThroughD2()
    {
        var world = World(Facility());
        var result = Inspect(world);
        var projector = new EconomicWorldFacilityAuthorizationProjector();
        var evidence = new[]
        {
            new EconomicFineTransferFacilityEvidence(
                900UL, new EconomicPhysicalResourceId(101UL),
                Junction, 10UL, 20UL, 3UL)
        };
        Assert.Equal(new[] { 900UL }, result.StructurallyValidatedFacilityIds);
        Assert.Throws<InvalidOperationException>(() =>
            projector.Project(7UL, world, result.WorldHash, evidence));
    }

    [Fact]
    public void UnknownAndAlreadyOperationalFacilitiesCannotBeCommissioned()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(World()));
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(World(Facility(operational: true))));
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(World(Facility()), new[] { Intent(901UL) }));
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(WorldState.CreateInitial()));
    }

    [Fact]
    public void StaleSnapshotOrOtherWorldCannotBeReused()
    {
        var before = World(Facility());
        var digest = new CanonicalWorldStateHasher().Compute(before);
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(before.AdvanceRevision(), hash: digest));
        var changed = WorldState.CreateBound(
                Generate(1UL, new GoldbergParameters(2, 0)))
            .WithTransportFacilities(new TransportFacilitiesRuntimeState(
                new[] { Facility() }));
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(changed, hash: digest));
    }

    [Fact]
    public void FineLocationAndCapabilitiesAreExactNotExtensibleByPreflight()
    {
        var world = World(Facility());
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(world, new[] { Intent(tile: Elsewhere) }));
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(world, new[] { Intent(tile: ForeignTile) }));
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(world, new[] { Intent(caps: new[] { Forward() }) }));
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(world, new[] { Intent(caps: new[]
            {
                Forward(),
                new TransportFacilityTransferCapability(20UL, 30UL, 3UL)
            }) }));
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(world, geography: new StrategicSurfaceGraph(
                Generate(0UL, ForeignFine).StrategicTopology)));
    }

    [Fact]
    public void DuplicateAndEmptyRequestsAreRejectedWithoutPartialEffects()
    {
        var world = World(Facility());
        var digest = new CanonicalWorldStateHasher().Compute(world);
        Assert.Throws<ArgumentException>(() =>
            Inspect(world, Array.Empty<EconomicFacilityCommissioningIntent>()));
        Assert.Throws<ArgumentException>(() =>
            Inspect(world, new[] { Intent(), Intent() }));
        Assert.Throws<ArgumentException>(() =>
            new EconomicFacilityCommissioningIntent(900UL, Junction,
                Array.Empty<TransportFacilityTransferCapability>()));
        Assert.Throws<ArgumentException>(() =>
            new EconomicFacilityCommissioningIntent(900UL, Junction,
                new[] { Forward(), Forward() }));
        Assert.Equal(digest, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void MultipleIndependentFacilitiesAreEvaluatedInCanonicalOrder()
    {
        var world = World(Facility(901UL), Facility(900UL));
        var a = Inspect(world, new[] { Intent(901UL), Intent(900UL) });
        var b = Inspect(world, new[] { Intent(900UL), Intent(901UL) });
        Assert.Equal(new[] { 900UL, 901UL }, a.StructurallyValidatedFacilityIds);
        Assert.Equal(a.StructurallyValidatedFacilityIds,
            b.StructurallyValidatedFacilityIds);
        Assert.Equal(a.WorldHash, b.WorldHash);
        Assert.Throws<InvalidOperationException>(() =>
            Inspect(world, new[] { Intent(900UL), Intent(902UL) }));
        Assert.All(world.TransportFacilities.Facilities,
            f => Assert.False(f.IsOperational));
    }

    [Fact]
    public void CapabilityOrderDoesNotCreateAFalseDifference()
    {
        var world = World(Facility(caps: new[] { Reverse(), Forward() }));
        var a = Inspect(world, new[] { Intent(caps: new[] { Forward(), Reverse() }) });
        var b = Inspect(world, new[] { Intent(caps: new[] { Reverse(), Forward() }) });
        Assert.Equal(a.WorldHash, b.WorldHash);
        Assert.Equal(a.StructurallyValidatedFacilityIds,
            b.StructurallyValidatedFacilityIds);
    }
}
