using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// v12 transport is complete, world-bound and hashed; it does NOT commit
/// freight, money, global market outcomes or invent historical roads.
/// </summary>
public sealed class FoundationPhysicalAuthoritativeTransportTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static PhysicalTransportEdgeRuntimeRecord[] Complete(
        PhysicalTacticalIncidenceMap map, ulong firstCost = 1_000_000UL)
    {
        var fine = map.Refinement.FineParameters;
        return map.FineTopology.Edges.Select((edge, index) =>
            new PhysicalTransportEdgeRuntimeRecord(
                new PhysicalTacticalEdgeId(fine, edge.Id),
                index == 0 ? PhysicalTransportOwnershipKind.Owned :
                    PhysicalTransportOwnershipKind.NoInfrastructure,
                index == 0 ? Blue : null,
                index == 0 ? firstCost : 10_000_000UL,
                50_000_000UL,
                index == 0 ? 25_000UL : 0UL)).ToArray();
    }

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain, Wood },
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(p =>
                new[] { Grain, Wood }.Select(commodity =>
                    new FoundationActivityCommodityDemandCoefficient(
                        p, commodity, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("hash-bound-freight-unit", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
            new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
        });

    [Fact]
    public void CompleteTransportIsCanonicalHashedAndSurvivesWorldCopies()
    {
        var host = Host();
        var raw = host.Snapshot;
        var hasher = new CanonicalWorldStateHasher();
        Assert.False(raw.Transport.IsInitialized);
        Assert.Equal(11U, hasher.Compute(raw).FormatVersion);

        var input = Complete(host.Incidence);
        var roads = PhysicalTransportInfrastructureRuntimeState
            .CreateInitial(input.Reverse());
        var snapshot = raw.DeployInitialPhysicalTransport(
            roads, host.Session.GeneratedWorld, host.Incidence);
        var hash = hasher.Compute(snapshot);
        Assert.True(snapshot.Transport.IsInitialized);
        Assert.Equal(12U, hash.FormatVersion);
        Assert.NotEqual(hasher.Compute(raw), hash);
        Assert.Equal(host.Incidence.FineTopology.Edges.Count,
            snapshot.Transport.Edges.Count);
        Assert.Equal(hash, hasher.Compute(raw.DeployInitialPhysicalTransport(
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(input),
            host.Session.GeneratedWorld, host.Incidence)));
        Assert.Equal(roads, snapshot.Transport);
        Assert.Equal(hash.FormatVersion,
            hasher.Compute(snapshot.AdvanceRevision()).FormatVersion);
        Assert.Same(snapshot.Transport, snapshot.AdvanceRevision().Transport);
        Assert.Equal(11U, hasher.Compute(raw).FormatVersion);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void OwnerAndNaturalFreightAreImmutableEconomicFactsNotPreviewQuotes()
    {
        var host = Host();
        var initial = Complete(host.Incidence);
        var baseWorld = host.Snapshot;
        var a = baseWorld.DeployInitialPhysicalTransport(
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(initial),
            host.Session.GeneratedWorld, host.Incidence);
        var modified = initial.ToArray();
        modified[0] = modified[0] with
        {
            FreePhysicalCostMicroPerWholeCargoUnit = 2_000_000UL
        };
        var b = baseWorld.DeployInitialPhysicalTransport(
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(modified),
            host.Session.GeneratedWorld, host.Incidence);
        Assert.NotEqual(new CanonicalWorldStateHasher().Compute(a),
            new CanonicalWorldStateHasher().Compute(b));
        Assert.Equal(1_000_000UL,
            a.Transport.Edges[0].FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(2_000_000UL,
            b.Transport.Edges[0].FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Throws<InvalidOperationException>(() =>
            a.DeployInitialPhysicalTransport(a.Transport,
                host.Session.GeneratedWorld, host.Incidence));
    }

    [Fact]
    public void PartialDuplicateOrForeignOwnershipCannotBeDeployed()
    {
        var host = Host();
        var roads = Complete(host.Incidence);
        var world = host.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            world.DeployInitialPhysicalTransport(
                PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                    roads.Skip(1)), host.Session.GeneratedWorld, host.Incidence));
        Assert.Throws<ArgumentException>(() =>
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                roads.Append(roads[0])));
        Assert.Throws<ArgumentException>(() =>
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                roads.Select((r, i) => i == 0
                    ? r with { CargoMicroAtReferenceLoad = 0UL } : r)));
        Assert.Throws<InvalidOperationException>(() =>
            world.DeployInitialPhysicalTransport(
                PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                    roads.Select((r, i) => i == 0
                        ? r with
                        {
                            InfrastructureOwner = new CivilizationId(999UL)
                        } : r)), host.Session.GeneratedWorld, host.Incidence));
        Assert.Throws<ArgumentException>(() =>
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                roads.Select((r, i) => i == 1
                    ? r with { ExternalTariffMicroPerWholeCargoUnit = 1UL }
                    : r)));
        Assert.False(world.Transport.IsInitialized);
    }

    [Fact]
    public void WorldCandidateReadsOnlyHashedFreightWithoutEconomicPublication()
    {
        var host = Host();
        var uninitialized = host.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalLaggedFreightCyclePlanner.CoordinateWorldCandidate(
                uninitialized, host.Incidence, Demand(), Cargo()));

        var snapshot = uninitialized.DeployInitialPhysicalTransport(
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                Complete(host.Incidence)), host.Session.GeneratedWorld,
                host.Incidence);
        var before = new CanonicalWorldStateHasher().Compute(snapshot);
        var projection = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(snapshot, host.Incidence);
        Assert.Equal(host.Incidence.FineTopology.Edges.Count,
            projection.Roads.Count);
        Assert.Equal(projection.Roads.Count, projection.Scales.Count);
        Assert.Equal(FoundationInfrastructureOwnershipKind.Owned,
            projection.Roads[0].OwnershipKind);
        Assert.Equal(Blue, projection.Roads[0].InfrastructureOwner);
        Assert.All(projection.Roads.Skip(1), row =>
        {
            Assert.Equal(FoundationInfrastructureOwnershipKind.NoInfrastructure,
                row.OwnershipKind);
            Assert.Null(row.InfrastructureOwner);
            Assert.Equal(0UL, row.ExternalTariffMicroPerWholeCargoUnit);
        });
        var result = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(
                snapshot, host.Incidence, Demand(), Cargo(),
                maxProducerPasses: 64);
        Assert.Equal(projection.Roads.Count, result.FixedCycleEdges.Count);
        Assert.False(result.RepricedWithinCycle);
        Assert.False(result.NextCycleQuotes.HasSettledProvenance);
        Assert.Equal(result.Distribution.AllocationAudit.TotalAvailableGoodsMicro,
            result.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(snapshot));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }
}
