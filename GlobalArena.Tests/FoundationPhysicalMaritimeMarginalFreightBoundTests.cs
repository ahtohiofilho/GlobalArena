using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// R66 owner-specific frozen freight contract. An exact R62 sea-leg
/// lower bound is meaningful for marginal planning only in the SAME
/// per-whole-cargo monetary units and the SAME seller/snapshot.
/// These tests intentionally do not claim economic cycle integration.
/// </summary>
public sealed class FoundationPhysicalMaritimeMarginalFreightBoundTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    private static (PhysicalTacticalEdgeId A, PhysicalTacticalEdgeId B)
        RealDistinctCoastalCrossings()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)),
            workersPerCivilization: 3);
        var land = host.Incidence.TileIncidences.Select(tile =>
        {
            long sum = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                sum = checked(sum +
                    host.Session.GeneratedWorld.StrategicPhysicalFields
                        .Elevation.GetRawValue(coarse));
            return sum / tile.IncidentCoarseCellIds.Count >
                host.Session.GeneratedWorld.StrategicPhysicalFields
                    .SeaLevel.RawValue;
        }).ToArray();
        var coast = host.Incidence.FineTopology.Edges
            .Where(edge => land[checked((int)
                edge.IncidentCellIds[0].Value - 1)] != land[checked((int)
                edge.IncidentCellIds[1].Value - 1)])
            .OrderBy(edge => edge.Id.Value)
            .Take(2).Select(edge => new PhysicalTacticalEdgeId(
                host.Incidence.Refinement.FineParameters, edge.Id))
            .ToArray();
        Assert.Equal(2, coast.Length);
        return (coast[0], coast[1]);
    }

    private static FoundationPhysicalRoutingEdgeCost FrozenCost(
        PhysicalTacticalEdgeId edge, ulong physical, ulong tariff = 0UL,
        CivilizationId? owner = null)
        => new(edge,
            owner.HasValue ? FoundationInfrastructureOwnershipKind.Owned :
                FoundationInfrastructureOwnershipKind.NoInfrastructure,
            owner, physical, tariff);

    [Fact]
    public void OwnerExemptionAppliesToTariffOnlyNotPhysicalFreight()
    {
        var (a, b) = RealDistinctCoastalCrossings();
        var first = FrozenCost(a, physical: 30UL,
            tariff: 20UL, owner: Red);
        var second = FrozenCost(b, physical: 40UL,
            tariff: 10UL, owner: Blue);
        var forRed = FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
            first, second, Red,
            landToEmbarkFreightMicroPerWholeCargoUnit: 7UL,
            landFromDisembarkFreightMicroPerWholeCargoUnit: 13UL,
            feasibleLandFreightMicroPerWholeCargoUnit: 100UL);
        var forBlue = FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
            first, second, Blue, 7UL, 13UL, 100UL);
        // Red owns first coast: 30 + 0; Blue owns second coast:
        // 40 + 0. Both still pay natural/financed physical freight.
        Assert.Equal(30UL, forRed.EmbarkCoastRateMicroPerWholeCargoUnit);
        Assert.Equal(50UL, forRed.DisembarkCoastRateMicroPerWholeCargoUnit);
        Assert.Equal((UInt128)101UL,
            forRed.MaritimeLowerBoundMicroPerWholeCargoUnit);
        Assert.True(forRed.CanExcludePair);
        Assert.Equal(50UL, forBlue.EmbarkCoastRateMicroPerWholeCargoUnit);
        Assert.Equal(40UL, forBlue.DisembarkCoastRateMicroPerWholeCargoUnit);
        Assert.Equal((UInt128)111UL,
            forBlue.MaritimeLowerBoundMicroPerWholeCargoUnit);
        Assert.True(forBlue.CanExcludePair);
    }

    [Fact]
    public void EqualityKeepsCandidateAndNoTerrestrialAlternativeFailsOpen()
    {
        var (a, b) = RealDistinctCoastalCrossings();
        var first = FrozenCost(a, 30UL, owner: Red);
        var second = FrozenCost(b, 40UL, owner: Blue);
        // 7 + 30 + 1 + 40 + 13 == 91; ties must survive.
        var tie = FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
            first, second, Red, 7UL, 13UL, 91UL);
        Assert.Equal((UInt128)91UL,
            tie.MaritimeLowerBoundMicroPerWholeCargoUnit);
        Assert.False(tie.CanExcludePair);
        var disconnectedLand =
            FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
                first, second, Red, 7UL, 13UL, null);
        Assert.False(disconnectedLand.CanExcludePair);
    }

    [Fact]
    public void AlreadyIncludedFrozenCongestionMustNotBeAddedTwice()
    {
        var (a, b) = RealDistinctCoastalCrossings();
        // Cost 60 means 30 natural plus 30 CLOSED prior-cycle surcharge;
        // current preview volume is deliberately not an input.
        var first = FrozenCost(a, 60UL);
        var second = FrozenCost(b, 40UL);
        var quoted = FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
            first, second, Red, 7UL, 13UL, 110UL);
        Assert.Equal((UInt128)121UL,
            quoted.MaritimeLowerBoundMicroPerWholeCargoUnit);
        Assert.True(quoted.CanExcludePair);
        Assert.Equal(60UL, quoted.EmbarkCoastRateMicroPerWholeCargoUnit);
        Assert.Equal(40UL, quoted.DisembarkCoastRateMicroPerWholeCargoUnit);
    }

    [Fact]
    public void RejectsInvalidTariffAndUnprovedSeaMinimum()
    {
        var (a, b) = RealDistinctCoastalCrossings();
        var good = FrozenCost(a, 100UL);
        var inconsistent = new FoundationPhysicalRoutingEdgeCost(
            b, FoundationInfrastructureOwnershipKind.NoInfrastructure,
            null, 100UL, 9UL);
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
                good, inconsistent, Red, 0UL, 0UL, 200UL));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
                good, FrozenCost(b, 100UL), Red, 0UL, 0UL,
                200UL, positiveSeaLegMinimumMicroPerWholeCargoUnit: 0UL));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
                good, good, Red, 0UL, 0UL, 200UL));
    }

    [Fact]
    public void VeryLargeFreightDoesNotWrapTheProof()
    {
        var (a, b) = RealDistinctCoastalCrossings();
        var first = FrozenCost(a, ulong.MaxValue - 2UL);
        var second = FrozenCost(b, ulong.MaxValue - 2UL);
        var candidate = FoundationPhysicalMaritimeMarginalFreightBound
            .Evaluate(first, second, Red,
                ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        Assert.True(candidate.MaritimeLowerBoundMicroPerWholeCargoUnit >
            ulong.MaxValue);
        Assert.True(candidate.CanExcludePair);
    }
}
