using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// One-turn work proposals from ACTUAL workers in a v13 world. A fixture may
/// create an eligible Other-activity census, but no proposal is a journaled
/// action, an infrastructure upgrade, a sale or a treasury transaction.
/// </summary>
public sealed class FoundationPhysicalConstructionWorkProposalTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        WorldState World,
        PhysicalWorkerRuntimeRecord First,
        PhysicalWorkerRuntimeRecord Second,
        PhysicalTacticalEdgeId Target);

    private static Scenario Create()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var people = host.Snapshot.Workers.Workers
            .Where(w => w.Owner == Red).Take(2).ToArray();
        Assert.Equal(2, people.Length);
        var ids = people.Select(w => w.Id).ToHashSet();
        var census = new PhysicalWorkerRuntimeState(
            host.Snapshot.Workers.Workers.Select(w =>
                ids.Contains(w.Id)
                    ? w.WithTacticalLocationAndActivity(
                        w.TacticalTileId!.Value, w.CellId,
                        PhysicalWorkerActivity.Other)
                    : w),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var snapshot = host.Snapshot.WithWorkers(census);
        var operating = new FoundationNaturalTransportSeedingPolicy(
            new FoundationNaturalTransportCostBand(1_000_000UL, 1_000_000UL),
            new FoundationNaturalTransportCostBand(20_000_000UL, 100_000UL),
            new FoundationNaturalTransportCostBand(100_000_000UL, 10_000UL),
            4_000_000UL);
        var construction = new FoundationNaturalConstructionSeedingPolicy(
            2_000_000UL, 6_000_000UL, 10_000_000UL, 8_000_000UL);
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence,
            operating, construction);
        var world = snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, host.Session.GeneratedWorld, host.Incidence);
        var tile = people[0].TacticalTileId!.Value;
        var target = host.Incidence.FineTopology.Edges
            .Where(edge => edge.IncidentCellIds.Contains(
                tile.FineStrategicCellId))
            .Select(edge => new PhysicalTacticalEdgeId(
                host.Incidence.Refinement.FineParameters, edge.Id))
            .First();
        return new Scenario(host, world,
            census.Workers.Single(w => w.Id == people[0].Id),
            census.Workers.Single(w => w.Id == people[1].Id),
            target);
    }

    private static FoundationPhysicalConstructionWorkAssignment Order(
        PhysicalWorkerRuntimeRecord worker, PhysicalTacticalEdgeId edge,
        CivilizationId? issuer = null) =>
        new(issuer ?? worker.Owner, worker.Id, edge);

    private static FoundationPhysicalConstructionWorkProposal Plan(
        Scenario s, ulong laborPerWorker,
        params FoundationPhysicalConstructionWorkAssignment[] orders) =>
        FoundationPhysicalConstructionWorkPlanner.PlanCandidate(
            s.World, s.Host.Session.GeneratedWorld, s.Host.Incidence,
            new FoundationPhysicalConstructionLaborPolicy(laborPerWorker),
            orders);

    [Fact]
    public void RealIdleWorkersCreditExactlyOneEdgeAndStillGenerateDemand()
    {
        var s = Create();
        var hash = new CanonicalWorldStateHasher().Compute(s.World);
        var plan = Plan(s, 250_000UL,
            Order(s.First, s.Target), Order(s.Second, s.Target));

        Assert.False(plan.IsCommitted);
        Assert.Equal(hash, plan.SourceWorldHash);
        Assert.Equal(2, plan.Workers.Count);
        Assert.Single(plan.Edges);
        Assert.Equal(500_000UL, plan.AvailableLaborMicro);
        Assert.Equal(500_000UL, plan.CreditableLaborMicro);
        Assert.Equal(0UL, plan.UnusedLaborMicro);
        Assert.All(plan.Workers, w =>
        {
            Assert.Equal(s.Target, w.Edge);
            Assert.Equal(Red, w.Owner);
            Assert.Equal(250_000UL, w.ContributedLaborMicro);
            Assert.Equal(0UL, w.UnusedLaborMicro);
        });
        var edge = plan.Edges[0];
        Assert.Equal(s.Target, edge.Edge);
        Assert.True(edge.RequiredLaborMicro >= 2_000_000UL);
        Assert.False(edge.MeetsLaborRequirement);
        Assert.Equal(2, edge.AssignedWorkerCount);

        var inputs = FoundationPhysicalWorkforceEconomicProjector
            .Project(s.World.Workers);
        Assert.Equal(s.World.Workers.Workers.Count,
            inputs.DemandSites.Sum(d => d.WorkerCount));
        Assert.Equal(2, inputs.DemandSites.Sum(d => d.OtherWorkers));
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(s.World));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void ExcessLaborIsUncreditedAndOrderPermutationIsDeterministic()
    {
        var s = Create();
        var a = Plan(s, 100_000_000UL,
            Order(s.Second, s.Target), Order(s.First, s.Target));
        var b = Plan(s, 100_000_000UL,
            Order(s.First, s.Target), Order(s.Second, s.Target));
        Assert.Equal(a.Workers.ToArray(), b.Workers.ToArray());
        Assert.Equal(a.Edges.ToArray(), b.Edges.ToArray());
        var edge = Assert.Single(a.Edges);
        Assert.True(edge.MeetsLaborRequirement);
        Assert.Equal(edge.RequiredLaborMicro, edge.CreditableLaborMicro);
        Assert.Equal(200_000_000UL, a.AvailableLaborMicro);
        Assert.Equal(a.AvailableLaborMicro,
            checked(a.CreditableLaborMicro + a.UnusedLaborMicro));
        Assert.Equal(edge.UnusedLaborMicro, a.UnusedLaborMicro);
        Assert.Equal(2, a.Workers.Count);
        Assert.Equal(s.First.Id, a.Workers[0].WorkerId);
        Assert.True(a.Workers[1].UnusedLaborMicro > 0UL);
        Assert.True(a.Workers[0].ContributedLaborMicro > 0UL);
    }

    [Fact]
    public void ForeignDuplicateProducingOrRemoteWorkerNeverBecomesFreeLabor()
    {
        var s = Create();
        Assert.Throws<InvalidOperationException>(() => Plan(s, 1_000_000UL,
            Order(s.First, s.Target, Blue)));
        Assert.Throws<InvalidOperationException>(() => Plan(s, 1_000_000UL,
            Order(s.First, s.Target), Order(s.First, s.Target)));
        var producing = s.World.Workers.Workers.First(w =>
            w.Owner == Red && w.Activity == PhysicalWorkerActivity.Producing);
        Assert.Throws<InvalidOperationException>(() => Plan(s, 1_000_000UL,
            Order(producing, s.Target)));
        var remote = s.World.Workers.Workers.First(w =>
            w.Owner == Blue);
        Assert.Throws<InvalidOperationException>(() => Plan(s, 1_000_000UL,
            Order(remote, s.Target)));
        Assert.Equal(13U, new CanonicalWorldStateHasher()
            .Compute(s.World).FormatVersion);
    }

    [Fact]
    public void ForeignTopologyAndOldV12WorldFailClosed()
    {
        var s = Create();
        var oldRows = PhysicalTransportInfrastructureRuntimeState
            .CreateInitial(s.World.Transport.Edges.Select(e => e with
            {
                NaturalConstructionLaborMicro = null
            }));
        var oldWorld = s.Host.Snapshot.DeployInitialPhysicalTransport(
            oldRows, s.Host.Session.GeneratedWorld, s.Host.Incidence);
        Assert.Equal(12U, new CanonicalWorldStateHasher()
            .Compute(oldWorld).FormatVersion);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalConstructionWorkPlanner.PlanCandidate(
                oldWorld, s.Host.Session.GeneratedWorld,
                s.Host.Incidence,
                new FoundationPhysicalConstructionLaborPolicy(100_000UL),
                new[] { Order(s.First, s.Target) }));
        var different = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261013UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalConstructionWorkPlanner.PlanCandidate(
                s.World, different.Session.GeneratedWorld,
                s.Host.Incidence,
                new FoundationPhysicalConstructionLaborPolicy(100_000UL),
                new[] { Order(s.First, s.Target) }));
    }

    [Fact]
    public void ZeroCapacityAndAbsentAssignmentsNeverCreateWorkOrOwnership()
    {
        var s = Create();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoundationPhysicalConstructionLaborPolicy(0UL));
        var empty = Plan(s, 100_000UL);
        Assert.False(empty.IsCommitted);
        Assert.Empty(empty.Workers);
        Assert.Empty(empty.Edges);
        Assert.Equal(0UL, empty.AvailableLaborMicro);
        Assert.Equal(0UL, empty.CreditableLaborMicro);
        Assert.Equal(0UL, empty.UnusedLaborMicro);
        Assert.All(s.World.Transport.Edges, edge =>
        {
            Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
                edge.OwnershipKind);
            Assert.Null(edge.InfrastructureOwner);
            Assert.Equal(0UL, edge.ExternalTariffMicroPerWholeCargoUnit);
        });
    }
}
