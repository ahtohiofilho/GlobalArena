using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Monetarily financed construction from actual CIVILIZATION treasury.
/// Physical workers produce/demand at their node but are NEVER required
/// or consumed to make an infrastructure investment; no commodity stock.
/// </summary>
public sealed class FoundationMonetaryInfrastructureInvestmentTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalTacticalEdgeId RemoteEdge,
        PhysicalWorkerId Producer);

    private static FoundationNaturalTransportSeedingPolicy Operation() =>
        new(new FoundationNaturalTransportCostBand(1_000_000UL, 1_000_000UL),
            new FoundationNaturalTransportCostBand(20_000_000UL, 100_000UL),
            new FoundationNaturalTransportCostBand(100_000_000UL, 10_000UL),
            4_000_000UL);

    private static FoundationMonetaryConstructionPolicy Money() =>
        new(2_000_000UL, 4_000_000UL, 6_000_000UL, 8_000_000UL);

    private static Scenario Create(
        ulong redOpeningMoneyMicro = 25_000_000UL,
        ulong blueOpeningMoneyMicro = 0UL)
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence,
            Operation(), Money());
        var deployed = fixture.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, fixture.Session.GeneratedWorld, fixture.Incidence);
        var opening = CivilizationTreasuryRuntimeState.CreateOpening(
            new[] { (Red, redOpeningMoneyMicro), (Blue, blueOpeningMoneyMicro) });
        fixture.Session.AcceptSnapshot(deployed.DeployInitialTreasury(opening));
        var person = fixture.Snapshot.Workers.Workers.First(w =>
            w.Owner == Red && w.Activity == PhysicalWorkerActivity.Producing);
        // The edge is NOT adjacent to the investing civilization's workers.
        // No contrived builder or demand market is materialized on the edge.
        var finiteEdges = seeded.Transport.Edges
            .Where(row => row.CargoMicroAtReferenceLoad != ulong.MaxValue)
            .Select(row => row.Edge.FineStrategicEdgeId).ToHashSet();
        var crossing = fixture.Incidence.FineTopology.Edges.First(edge =>
            finiteEdges.Contains(edge.Id) &&
            !edge.IncidentCellIds.Contains(
                person.TacticalTileId!.Value.FineStrategicCellId));
        var edgeId = new PhysicalTacticalEdgeId(
            fixture.Incidence.Refinement.FineParameters, crossing.Id);
        var host = new FoundationTacticalHumanAiTurnHost(
            fixture.Session, fixture.Incidence,
            new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            },
            enableMonetaryInfrastructure: true);
        return new Scenario(host, edgeId, person.Id);
    }

    private static PhysicalTransportEdgeRuntimeRecord Edge(Scenario s) =>
        s.Host.Snapshot.Transport.Edges.Single(x =>
            x.Edge == s.RemoteEdge);

    [Fact]
    public void MonetaryConstructionQuotesAreQuadraticWhileOperatingIsLinear()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var physical = fixture.Session.GeneratedWorld;
        var incidence = fixture.Incidence;
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            physical, incidence, Operation(), Money());
        Assert.True(seeded.Transport.HasMonetaryConstructionCosts);
        Assert.False(seeded.Transport.HasCommittedInvestments);
        Assert.All(seeded.Transport.Edges, edge =>
        {
            Assert.NotNull(edge.NaturalConstructionCostMoneyMicro);
            Assert.Null(edge.CommittedInvestmentMoneyMicro);
            Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
                edge.OwnershipKind);
            Assert.Equal(0UL, edge.ExternalTariffMicroPerWholeCargoUnit);
        });
        var elevation = physical.StrategicPhysicalFields.Elevation;
        long sea = physical.StrategicPhysicalFields.SeaLevel.RawValue;
        var tileElevation = incidence.TileIncidences.Select(tile =>
            tile.IncidentCoarseCellIds.Aggregate(0L,
                (sum, coarse) => checked(sum +
                    elevation.GetRawValue(coarse))) /
                tile.IncidentCoarseCellIds.Count).ToArray();
        var byEdge = seeded.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        UInt128 scale = (UInt128)StrategicScalarField.Denominator;
        int nonFlatLand = 0;
        foreach (var crossing in incidence.FineTopology.Edges)
        {
            int a = checked((int)crossing.IncidentCellIds[0].Value - 1);
            int b = checked((int)crossing.IncidentCellIds[1].Value - 1);
            bool la = tileElevation[a] > sea;
            bool lb = tileElevation[b] > sea;
            var item = byEdge[crossing.Id];
            if (la && lb)
            {
                Int128 deltaSigned =
                    (Int128)tileElevation[a] - tileElevation[b];
                if (deltaSigned < 0) deltaSigned = -deltaSigned;
                UInt128 d = (UInt128)deltaSigned;
                if (d > 0) nonFlatLand++;
                Assert.Equal(checked((ulong)(1_000_000UL +
                    (UInt128)4_000_000UL * d / scale)),
                    item.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(checked((ulong)(2_000_000UL +
                    (UInt128)8_000_000UL * d * d / (scale * scale))),
                    item.NaturalConstructionCostMoneyMicro);
            }
            else if (la || lb)
            {
                Assert.Equal(20_000_000UL,
                    item.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(4_000_000UL,
                    item.NaturalConstructionCostMoneyMicro);
            }
            else
            {
                Assert.Equal(100_000_000UL,
                    item.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(6_000_000UL,
                    item.NaturalConstructionCostMoneyMicro);
            }
        }
        Assert.True(nonFlatLand > 0);
    }

    [Fact]
    public void AllReadyTurnDebitsCashAndCreditsRemoteEdgeWithoutWorkersOrMaterials()
    {
        var s = Create();
        var before = s.Host.Snapshot;
        var previousHash = s.Host.StateHash;
        var previousDemand =
            FoundationPhysicalWorkforceEconomicProjector.Project(before.Workers);
        BigInteger oldCash = before.Treasury.Get(Red).CashScaled;
        var original = Edge(s);
        var initialProjected = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(before, s.Host.Incidence).Scales
            .Single(row => row.Edge == s.RemoteEdge)
            .CargoMicroAtReferenceLoad;
        Assert.Equal(original.CargoMicroAtReferenceLoad, initialProjected);
        const ulong spend = 175_000UL;

        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, spend);
        Assert.Equal(1, s.Host.PendingOrderCount);
        Assert.Equal(previousHash, s.Host.StateHash);
        Assert.Null(s.Host.TryCloseTurn()); // Human not ready.
        Assert.Same(before, s.Host.Snapshot);

        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        Assert.Empty(receipt.Events);
        var ev = Assert.Single(receipt.InfrastructureInvestments);
        Assert.Equal(Red, ev.Investor);
        Assert.Equal(s.RemoteEdge, ev.Edge);
        Assert.Equal(spend, ev.MoneyMicro);
        Assert.Equal(14U, receipt.Hash.FormatVersion);
        Assert.Equal(before.Revision + 1UL, s.Host.Snapshot.Revision);
        Assert.Equal(receipt.Hash, s.Host.StateHash);
        Assert.Equal(spend, Edge(s).CommittedInvestmentMoneyMicro);
        Assert.Equal(original.NaturalConstructionCostMoneyMicro,
            Edge(s).NaturalConstructionCostMoneyMicro);
        Assert.Equal(original.FreePhysicalCostMicroPerWholeCargoUnit,
            Edge(s).FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(original.CargoMicroAtReferenceLoad,
            Edge(s).CargoMicroAtReferenceLoad); // immutable natural baseline
        ulong effective = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(s.Host.Snapshot, s.Host.Incidence).Scales
            .Single(row => row.Edge == s.RemoteEdge)
            .CargoMicroAtReferenceLoad;
        Assert.True(effective > initialProjected,
            "Capital already debited from the real treasury must improve throughput.");
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            Edge(s).OwnershipKind);
        Assert.Equal(Red, Edge(s).InfrastructureOwner);
        Assert.Equal(0UL, Edge(s).ExternalTariffMicroPerWholeCargoUnit);

        var amount = GlobalArenaMoneyScale.FromMoneyMicro(spend);
        Assert.Equal(oldCash - amount,
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(amount,
            s.Host.Snapshot.Treasury.Get(Red)
                .TotalInfrastructureInvestmentScaled);
        Assert.Equal(BigInteger.Zero,
            s.Host.Snapshot.Treasury.Get(Blue)
                .TotalInfrastructureInvestmentScaled);
        Assert.Same(before.Workers, s.Host.Snapshot.Workers);
        Assert.Equal(previousDemand.DemandSites.ToArray(),
            FoundationPhysicalWorkforceEconomicProjector
                .Project(s.Host.Snapshot.Workers).DemandSites.ToArray());
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(0, s.Host.PendingOrderCount);
    }

    [Fact]
    public void RealCashFundingCanBeSpreadAcrossTurnsWithoutChangingFreight()
    {
        var s = Create();
        ulong cost = Edge(s).NaturalConstructionCostMoneyMicro!.Value;
        Assert.True(cost > 100_000UL);
        BigInteger initial = s.Host.Snapshot.Treasury.Get(Red).CashScaled;
        for (int turn = 1; turn <= 2; turn++)
        {
            ulong part = turn == 1 ? 100_000UL : cost - 100_000UL;
            s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, part);
            s.Host.SetHumanReady(Red);
            var receipt = s.Host.TryCloseTurn();
            Assert.NotNull(receipt);
            Assert.True(receipt!.ReplayMatched);
            Assert.Equal(14U, receipt.Hash.FormatVersion);
        }
        Assert.Equal(cost, Edge(s).CommittedInvestmentMoneyMicro);
        var funded = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(s.Host.Snapshot, s.Host.Incidence).Scales
            .Single(row => row.Edge == s.RemoteEdge).CargoMicroAtReferenceLoad;
        Assert.Equal(Edge(s).CargoMicroAtReferenceLoad +
            Edge(s).CargoMicroAtReferenceLoad / 2UL, funded);
        Assert.Equal(initial - GlobalArenaMoneyScale.FromMoneyMicro(cost),
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(cost),
            s.Host.Snapshot.Treasury.Get(Red)
                .TotalInfrastructureInvestmentScaled);
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            Edge(s).OwnershipKind);
        Assert.Equal(Red, Edge(s).InfrastructureOwner);
        // Once the original construction quote is fully funded, further
        // upgrades remain possible on the SAME physical edge.
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, cost);
        s.Host.SetHumanReady(Red);
        var upgraded = s.Host.TryCloseTurn();
        Assert.NotNull(upgraded);
        Assert.True(upgraded!.ReplayMatched);
        Assert.Single(upgraded.InfrastructureInvestments);
        Assert.Equal(checked(cost * 2UL),
            Edge(s).CommittedInvestmentMoneyMicro);
        ulong afterUpgrade = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(s.Host.Snapshot, s.Host.Incidence).Scales
            .Single(row => row.Edge == s.RemoteEdge).CargoMicroAtReferenceLoad;
        Assert.True(afterUpgrade > funded);
        Assert.Equal(initial - GlobalArenaMoneyScale.FromMoneyMicro(cost * 2UL),
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            Edge(s).OwnershipKind);
        Assert.Equal(Red, Edge(s).InfrastructureOwner);
    }

    [Fact]
    public void ProducingWorkerCanChangeOccupationWhileCivilizationFundsRemoteEdge()
    {
        var s = Create();
        var before = s.Host.Snapshot;
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 50_000UL);
        s.Host.QueueHumanActivity(Red, s.Producer,
            PhysicalWorkerActivity.Other);
        Assert.Equal(2, s.Host.PendingOrderCount);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Single(receipt!.Events);
        Assert.Single(receipt.InfrastructureInvestments);
        Assert.Equal(before.Revision + 2UL, s.Host.Snapshot.Revision);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(14U, s.Host.StateHash.FormatVersion);
        Assert.Equal(PhysicalWorkerActivity.Other,
            s.Host.Snapshot.Workers.Workers.Single(w =>
                w.Id == s.Producer).Activity);
        Assert.Equal(50_000UL, Edge(s).CommittedInvestmentMoneyMicro);
    }

    [Fact]
    public void NoOverdraftForgedFundsOrDuplicateInvestmentCanBePublished()
    {
        var s = Create(redOpeningMoneyMicro: 300_000UL);
        var snapshot = s.Host.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(
                Red, s.RemoteEdge, 300_001UL));
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(
                Blue, s.RemoteEdge, 1UL));
        Assert.Equal(0, s.Host.PendingOrderCount);
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 150_000UL);
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(
                Red, s.RemoteEdge, 1UL));
        Assert.True(s.Host.CancelHumanMonetaryInvestment(
            Red, s.RemoteEdge));
        Assert.Same(snapshot, s.Host.Snapshot);
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 150_000UL);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        var ev = Assert.Single(receipt!.InfrastructureInvestments);
        var executor = new FoundationMonetaryInfrastructureEventExecutor(
            s.Host.Session.GeneratedWorld, s.Host.Incidence);
        var context = new SimulationContext(
            ev.Id.Turn, new SimulationSeed(712UL));
        var independent = executor.Execute(snapshot, ev, context);
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(independent));
        var forged = new FoundationMonetaryInfrastructureInvestedEvent(
            ev.Id, ev.Investor, ev.Edge, 300_001UL,
            ev.ExpectedRevision);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(snapshot, forged, context));
        var foreign = new FoundationMonetaryInfrastructureInvestedEvent(
            ev.Id, Blue, ev.Edge, ev.MoneyMicro, ev.ExpectedRevision);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(snapshot, foreign, context));
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(independent, ev, context));
    }

    [Fact]
    public void V12QuotesStayUnmodifiedAndOpeningIsExplicitNotMarketRevenue()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var previous = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence, Operation());
        var v12 = fixture.Snapshot.DeployInitialPhysicalTransport(
            previous.Transport, fixture.Session.GeneratedWorld,
            fixture.Incidence);
        Assert.Equal(12U, new CanonicalWorldStateHasher()
            .Compute(v12).FormatVersion);
        Assert.False(v12.Transport.HasMonetaryConstructionCosts);
        Assert.All(v12.Transport.Edges, row =>
            Assert.Null(row.NaturalConstructionCostMoneyMicro));
        Assert.False(v12.Treasury.IsInitialized);
        var v13seed = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence,
            Operation(), Money());
        var v13 = fixture.Snapshot.DeployInitialPhysicalTransport(
            v13seed.Transport, fixture.Session.GeneratedWorld,
            fixture.Incidence);
        Assert.Equal(13U, new CanonicalWorldStateHasher()
            .Compute(v13).FormatVersion);
        Assert.False(v13.Treasury.IsInitialized);
        Assert.Throws<InvalidOperationException>(() =>
            v13.RecordMonetaryInfrastructureInvestment(
                Red, v13.Transport.Edges[0].Edge, 1UL));
        Assert.Equal(GlobalArenaMoneyScale.ExactScaledPerUnit,
            FoundationPhysicalEconomicAccountingProposal.MoneyScale);
        Assert.Equal(GlobalArenaMoneyScale.ExactScaledPerUnit,
            GlobalArenaMoneyScale.FromMoneyMicro(1_000_000UL));
        Assert.Throws<InvalidOperationException>(() =>
            v12.DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(
                    new[] { (Red, 0UL), (Blue, 0UL) })));
        Assert.Throws<ArgumentException>(() =>
            CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 0UL), (Red, 0UL) }));
        var zero = v13.DeployInitialTreasury(
            CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 0UL), (Blue, 0UL) }));
        Assert.Equal(13U, new CanonicalWorldStateHasher()
            .Compute(zero).FormatVersion);
        Assert.Equal(BigInteger.Zero, zero.Treasury.Get(Red).CashScaled);
        Assert.Equal(BigInteger.Zero,
            zero.Treasury.Get(Blue).CashScaled);
    }
    [Fact]
    public void NaturalCoastIsAnInvestableGargleneckButOpenSeaIsNeverConstructible()
    {
        var s = Create();
        var start = s.Host.Snapshot;
        var sea = start.Transport.Edges.First(row =>
            row.CargoMicroAtReferenceLoad == ulong.MaxValue);
        var coast = start.Transport.Edges.First(row =>
            row.FreePhysicalCostMicroPerWholeCargoUnit == 20_000_000UL &&
            row.CargoMicroAtReferenceLoad == 100_000UL);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            coast.OwnershipKind);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            sea.OwnershipKind);
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(Red, sea.Edge, 1UL));
        Assert.Equal(0, s.Host.PendingOrderCount);
        Assert.Same(start, s.Host.Snapshot);
        Assert.Equal(ulong.MaxValue,
            FoundationPhysicalAuthoritativeTransportProjector
                .FromWorld(start, s.Host.Incidence).Scales
                .Single(x => x.Edge == sea.Edge).CargoMicroAtReferenceLoad);

        // No port entity or construction mode: invest in the actual coast
        // edge using the same monetary command as every terrestrial edge.
        const ulong spend = 1_000_000UL;
        s.Host.QueueHumanMonetaryInvestment(Red, coast.Edge, spend);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        Assert.Single(receipt.InfrastructureInvestments);
        var after = s.Host.Snapshot;
        Assert.Equal(start.Transport.Edges.Count, after.Transport.Edges.Count);
        var built = after.Transport.Edges.Single(x => x.Edge == coast.Edge);
        Assert.Equal(spend, built.CommittedInvestmentMoneyMicro);
        Assert.Equal(coast.CargoMicroAtReferenceLoad,
            built.CargoMicroAtReferenceLoad);
        var effective = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(after, s.Host.Incidence).Scales
            .Single(x => x.Edge == coast.Edge).CargoMicroAtReferenceLoad;
        Assert.True(effective > coast.CargoMicroAtReferenceLoad);
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            built.OwnershipKind);
        Assert.Equal(Red, built.InfrastructureOwner);
        Assert.Equal(0UL, built.ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(ulong.MaxValue,
            FoundationPhysicalAuthoritativeTransportProjector
                .FromWorld(after, s.Host.Incidence).Scales
                .Single(x => x.Edge == sea.Edge).CargoMicroAtReferenceLoad);
        Assert.Throws<InvalidOperationException>(() =>
            after.Transport.RecordMoneyInvestment(Red, sea.Edge, 1UL));
    }

    [Fact]
    public void TheSameInvestmentFunctionHasDiminishingMarginalCapacityReturns()
    {
        var s = Create();
        var natural = s.Host.Snapshot.Transport.Edges.First(x =>
            x.CargoMicroAtReferenceLoad != ulong.MaxValue &&
            x.NaturalConstructionCostMoneyMicro is > 0UL);
        ulong cost = natural.NaturalConstructionCostMoneyMicro!.Value;
        ulong quarter = cost / 4UL;
        Assert.True(quarter > 0UL);
        ulong k0 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural);
        ulong k1 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = quarter });
        ulong k2 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = quarter * 2UL });
        ulong k4 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = cost });
        Assert.True(k0 < k1 && k1 < k2 && k2 < k4);
        // For equal-sized investment increments the earlier increment
        // must return more additional capacity than the later increment.
        Assert.True(k1 - k0 > (k2 - k1));
        Assert.True(k2 - k0 > k4 - k2);
        Assert.Equal(k0 + k0 / 2UL, k4);
        // Unlike R48, the curve continues past the original construction
        // quote rather than approaching a 2x hard ceiling.
        ulong k8 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = cost * 8UL });
        ulong k16 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = cost * 16UL });
        Assert.True(k16 > k8 && k8 > k4);
        Assert.True(k16 > k0 * 2UL);
        Assert.True(k8 - k4 > 0UL);
        // The growth is sublinear, not an artificial cap.
        Assert.True(k16 - k8 < k8 - k0);
    }

    [Fact]
    public void ExactIntegerInvestmentCurveNeverLosesCapacityAtRoundingBoundaries()
    {
        var s = Create();
        var natural = s.Host.Snapshot.Transport.Edges.First(e =>
            e.CargoMicroAtReferenceLoad != ulong.MaxValue);
        var edge = natural with
        {
            CargoMicroAtReferenceLoad = 10_000UL,
            NaturalConstructionCostMoneyMicro = 7UL
        };
        ulong previous = 0UL;
        for (ulong investment = 0UL; investment <= 500UL; investment++)
        {
            ulong current = FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveReferenceCargo(edge with
                { CommittedInvestmentMoneyMicro = investment });
            if (investment > 0UL)
                Assert.True(current >= previous,
                    "More verified capital may never lower transport capacity.");
            previous = current;
        }
        Assert.True(previous > 20_000UL,
            "Repeated improvements must overcome two times natural throughput.");
        // An independent arbitrary-integer sanity check for the square root.
        for (int i = 0; i < 100; i++)
        {
            BigInteger n = (BigInteger)i * i + i;
            BigInteger root = FoundationPhysicalAuthoritativeTransportProjector
                .IntegerSquareRoot(n);
            Assert.True(root * root <= n &&
                (root + 1) * (root + 1) > n);
        }
    }

}
