using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationGroundEdgeTravelTimeProjectorTests
{
    private static (WorldState World, PhysicalWorkerRuntimeRecord Worker,
        PhysicalTacticalTileId Destination,
        PhysicalTacticalWorkerMoveEventExecutor Validator) Fixture()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 1);
        var worker = demo.Session.Snapshot.Workers.Workers.First(w =>
            w.Activity == PhysicalWorkerActivity.Producing);
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            demo.Session.GeneratedWorld, demo.Incidence);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            demo.Session.GeneratedWorld, demo.Incidence, fine);
        var validator = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, demo.Incidence, fine, signature);
        var location = worker.TacticalTileId!.Value;
        var adjacent = demo.Incidence.FineTopology.Edges
            .Where(edge => edge.IncidentCellIds.Contains(
                location.FineStrategicCellId))
            .Select(edge => edge.IncidentCellIds.Single(
                id => id != location.FineStrategicCellId))
            .Where(id => fine.CanTraverseOnFoot(checked((int)id.Value - 1)))
            .Select(id => new PhysicalTacticalTileId(
                demo.Incidence.Refinement.FineParameters, id))
            .First(tile =>
            {
                try { return validator.CheckEdge(location, tile).HasValue; }
                catch (InvalidOperationException) { return false; }
            });
        return (demo.Session.Snapshot, worker, adjacent, validator);
    }

    [Fact]
    public void ExistingActualInfrastructureAndPhysicalEdgeProvideDurationForR165()
    {
        var (world, person, to, validator) = Fixture();
        Assert.True(world.Transport.IsInitialized);
        var hash = new CanonicalWorldStateHasher().Compute(world);
        var policy = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var (travel, reading) = FoundationGroundEdgeTravelTimeProjector.Start(
            world, person, to, validator, policy);
        Assert.Equal(validator.CheckEdge(person.TacticalTileId!.Value, to),
            reading.Edge);
        Assert.Equal(travel.Crossing, reading.Edge);
        Assert.Equal(reading.ImprovedCrossingTimeMicro,
            travel.RemainingTravelMicro);
        Assert.Equal(reading.NaturalCrossingTimeMicro,
            1_400_000UL);
        Assert.True(reading.ImprovedCrossingTimeMicro > 0UL);
        Assert.True(reading.ImprovedCrossingTimeMicro <=
            reading.NaturalCrossingTimeMicro);
        var first = FoundationFractionalEdgeTravelProjector.Advance(
            travel, person, validator);
        Assert.Equal(1_000_000U,
            first.TimeSlices.Sum(x => x.PresenceMicro));
        if (travel.RemainingTravelMicro > 1_000_000UL)
        {
            Assert.NotNull(first.ContinuingTravel);
            var second = FoundationFractionalEdgeTravelProjector.Advance(
                first.ContinuingTravel!, person, validator);
            Assert.True(second.Arrived);
            Assert.Equal(to, second.PhysicalTileAfterTurn);
            Assert.Equal(1_000_000U,
                second.TimeSlices.Sum(x => x.PresenceMicro));
        }
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void InvestedCrossingIsFasterWithoutCargoTariffOrOwnershipDependency()
    {
        var (world, person, to, validator) = Fixture();
        var id = validator.CheckEdge(person.TacticalTileId!.Value, to)!.Value;
        var raw = world.Transport.Edges.Single(e => e.Edge == id);
        const ulong reference = 1_000_000UL;
        var natural = raw with
        {
            NaturalConstructionCostMoneyMicro = reference,
            CommittedInvestmentMoneyMicro = 0UL
        };
        var policy = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var a = FoundationGroundEdgeTravelTimeProjector.FromInfrastructure(
            natural, policy);
        var b = FoundationGroundEdgeTravelTimeProjector.FromInfrastructure(
            natural with
            {
                CommittedInvestmentMoneyMicro = 3_000_000UL
            }, policy);
        var c = FoundationGroundEdgeTravelTimeProjector.FromInfrastructure(
            natural with
            {
                CommittedInvestmentMoneyMicro = 15_000_000UL
            }, policy);
        Assert.Equal(1_400_000UL, a.ImprovedCrossingTimeMicro);
        Assert.True(b.ImprovedCrossingTimeMicro < a.ImprovedCrossingTimeMicro);
        Assert.True(c.ImprovedCrossingTimeMicro <= b.ImprovedCrossingTimeMicro);
        Assert.Equal(b.ImprovedCrossingTimeMicro,
            FoundationGroundEdgeTravelTimeProjector.FromInfrastructure(
                natural with
                {
                    CommittedInvestmentMoneyMicro = 3_000_000UL,
                    ExternalTariffMicroPerWholeCargoUnit = 800_000UL,
                    CargoMicroAtReferenceLoad =
                        checked(natural.CargoMicroAtReferenceLoad + 1UL),
                    InfrastructureOwner = person.Owner,
                    OwnershipKind = PhysicalTransportOwnershipKind.Owned
                }, policy).ImprovedCrossingTimeMicro);
        Assert.Equal(b,
            FoundationGroundEdgeTravelTimeProjector.FromInfrastructure(
                natural with { CommittedInvestmentMoneyMicro = 3_000_000UL },
                policy));
    }

    [Fact]
    public void ForeignWorkerOrInvalidPolicyCannotStart()
    {
        var (world, worker, to, validator) = Fixture();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationGroundEdgeTravelTimeProjector.Start(
                world, worker, to, validator,
                new FoundationGroundEdgeTravelTimePolicy(0UL)));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationGroundEdgeTravelTimeProjector.Start(
                world, worker with { }, to, validator,
                new FoundationGroundEdgeTravelTimePolicy(1_000_000UL)));
        Assert.Throws<ArgumentException>(() =>
            FoundationGroundEdgeTravelTimeProjector.Start(
                world, worker, worker.TacticalTileId!.Value, validator,
                new FoundationGroundEdgeTravelTimePolicy(1_000_000UL)));
    }
}
