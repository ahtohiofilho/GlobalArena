using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationLastClosedEconomicTradePublicationTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private const string Unit = "test-normalized-physical-cargo-micro";

    private static WorldGenerationRequest Request(ulong seed = 20261012UL) =>
        new(new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0));

    private static FoundationTacticalHumanAiTurnHost Start() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(Request());

    private static FoundationTacticalCollectiveReceipt CloseOneWorkerTurn(
        FoundationTacticalHumanAiTurnHost host)
    {
        host.SetHumanReady(Red);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(host.StateHash, receipt.Hash);
        return receipt;
    }

    private static (PhysicalTacticalTileId First, PhysicalTacticalTileId Second,
        PhysicalTacticalEdgeId Edge) FirstEdge(PhysicalTacticalIncidenceMap map)
    {
        var edge = map.FineTopology.Edges[0];
        var fine = map.Refinement.FineParameters;
        return (new PhysicalTacticalTileId(fine, edge.IncidentCellIds[0]),
            new PhysicalTacticalTileId(fine, edge.IncidentCellIds[1]),
            new PhysicalTacticalEdgeId(fine, edge.Id));
    }

    private static FoundationEconomicTradeCycleReadBoundary Boundary(
        FoundationTacticalHumanAiTurnHost host) =>
        new(host.Snapshot.WorldBinding!,
            host.Incidence.Refinement.FineParameters, new[] { Red, Blue });

    private static FoundationPhysicalTradeEdgeLoadAudit Audit(
        FoundationTacticalHumanAiTurnHost host, bool withTransport = true)
    {
        var (a, b, edge) = FirstEdge(host.Incidence);
        return FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            host.Incidence, Unit,
            withTransport ? new[]
            {
                new FoundationFreightDeliveryRouteEvidence(
                    1UL, new CommodityId(1U), 2_000_000UL, Unit,
                    new[] { a, b })
            } : Array.Empty<FoundationFreightDeliveryRouteEvidence>(),
            withTransport ? new[]
            {
                new FoundationTransportEdgeOwnershipEvidence(
                    edge, FoundationInfrastructureOwnershipKind.Owned, Red)
            } : Array.Empty<FoundationTransportEdgeOwnershipEvidence>());
    }

    private static Dictionary<CivilizationId,
        IReadOnlyList<PhysicalTacticalEdgeId>> Audience(
        PhysicalTacticalEdgeId edge, bool redSees = true,
        bool blueSees = false) => new()
    {
        [Red] = redSees ? new[] { edge } :
            Array.Empty<PhysicalTacticalEdgeId>(),
        [Blue] = blueSees ? new[] { edge } :
            Array.Empty<PhysicalTacticalEdgeId>()
    };

    private static FoundationSettledEconomicTradeCycleArtifact Artifact(
        FoundationTacticalHumanAiTurnHost host,
        FoundationTacticalCollectiveReceipt turn,
        FoundationPhysicalTradeEdgeLoadAudit audit,
        IReadOnlyDictionary<CivilizationId,
            IReadOnlyList<PhysicalTacticalEdgeId>> visibility,
        ulong ordinal = 1UL, WorldStateHash? hash = null,
        WorldState? snapshot = null, TurnNumber? closedAt = null) =>
        new(snapshot ?? host.Snapshot, hash ?? turn.Hash, ordinal,
            closedAt ?? turn.Turn, audit, visibility);

    [Fact]
    public void WorkerOnlyHostNeverClaimsEconomicTradeBeforeOrAfterReadyTurn()
    {
        var host = Start();
        var initial = host.Snapshot;
        var initialHash = host.StateHash;

        var before = host.GetLastClosedEconomicTradeView(Red);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            before.Availability);
        Assert.Empty(before.UsedEdges);
        Assert.Null(before.EconomicCycleOrdinal);
        Assert.Null(before.PublishedRevision);
        Assert.Null(before.PublishedHash);
        Assert.Null(before.CargoUnitCode);
        Assert.Null(before.ClosedAtWorkerTurn);
        Assert.Equal(host.Incidence.Refinement.FineParameters,
            before.FineTopology);
        Assert.Equal(host.Snapshot.WorldBinding, before.WorldBinding);
        Assert.Same(initial, host.Snapshot);
        Assert.Equal(initialHash, host.StateHash);

        host.QueueHumanOccupation(Red, new PhysicalWorkerId(1),
            PhysicalWorkerOccupation.Mining);
        var pending = host.GetLastClosedEconomicTradeView(Red);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            pending.Availability);
        Assert.Same(initial, host.Snapshot);
        Assert.Equal(initialHash, host.StateHash);

        var receipt = CloseOneWorkerTurn(host);
        Assert.Equal(11U, receipt.Hash.FormatVersion);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Blue).Availability);
        Assert.Throws<InvalidOperationException>(() =>
            host.GetLastClosedEconomicTradeView(new CivilizationId(999)));
        Assert.Equal(receipt.Hash, host.StateHash);

        var second = CloseOneWorkerTurn(host);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(second.Hash, host.StateHash);
    }

    [Fact]
    public void PrivilegedSettlementFixturePublishesOnlyAuthorizedHistoricalEdges()
    {
        var host = Start();
        var turn = CloseOneWorkerTurn(host);
        var boundary = Boundary(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var visibility = Audience(edge);
        var audit = Audit(host);
        var artifact = Artifact(host, turn, audit, visibility);

        boundary.PublishFromSettledEconomicPhase(
            artifact, host.Snapshot, host.NextTurn);
        var red = boundary.Read(Red, host.Snapshot);
        var blue = boundary.Read(Blue, host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.Ready,
            red.Availability);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset,
            blue.Availability);
        Assert.Equal(1UL, red.EconomicCycleOrdinal);
        Assert.Equal(turn.Turn, red.ClosedAtWorkerTurn);
        Assert.Equal(turn.Revision, red.PublishedRevision);
        Assert.Equal(turn.Hash, red.PublishedHash);
        Assert.Equal(Unit, red.CargoUnitCode);
        Assert.Equal(Unit, blue.CargoUnitCode);
        var line = Assert.Single(red.UsedEdges);
        Assert.Equal(edge, line.Edge);
        Assert.Equal(2_000_000UL, line.TotalCargoMicro);
        Assert.Equal(FoundationInfrastructureOwnershipKind.Owned,
            line.OwnershipKind);
        Assert.Equal(Red, line.Owner);
        Assert.Empty(blue.UsedEdges);

        // Mutating the original caller-owned visibility list AFTER publication
        // cannot expand a hidden participant's visibility.
        visibility[Blue] = new[] { edge };
        Assert.Empty(boundary.Read(Blue, host.Snapshot).UsedEdges);

        var previouslyPublishedHash = red.PublishedHash;
        var secondWorkerTurn = CloseOneWorkerTurn(host);
        Assert.Equal(secondWorkerTurn.Hash, host.StateHash);
        Assert.Same(red, boundary.Read(Red, host.Snapshot));
        Assert.Equal(previouslyPublishedHash,
            boundary.Read(Red, host.Snapshot).PublishedHash);
        Assert.Equal(Red,
            Assert.Single(boundary.Read(Red, host.Snapshot).UsedEdges).Owner);
    }

    [Fact]
    public void PublishedZeroTransportIsDistinctFromAbsenceAndNeverInventsALine()
    {
        var host = Start();
        var boundary = Boundary(host);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            boundary.Read(Red, host.Snapshot).Availability);

        var receipt = CloseOneWorkerTurn(host);
        var noTradeAudit = Audit(host, withTransport: false);
        var visibility = new Dictionary<CivilizationId,
            IReadOnlyList<PhysicalTacticalEdgeId>>
        {
            [Red] = Array.Empty<PhysicalTacticalEdgeId>(),
            [Blue] = Array.Empty<PhysicalTacticalEdgeId>()
        };
        boundary.PublishFromSettledEconomicPhase(
            Artifact(host, receipt, noTradeAudit, visibility),
            host.Snapshot, host.NextTurn);
        var ready = boundary.Read(Red, host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.Ready,
            ready.Availability);
        Assert.Empty(ready.UsedEdges);
        Assert.Equal(1UL, ready.EconomicCycleOrdinal);
        Assert.Equal(receipt.Hash, ready.PublishedHash);
    }

    [Fact]
    public void WrongHashStaleSnapshotAndOutOfOrderCycleCannotReplacePublication()
    {
        var host = Start();
        var receipt = CloseOneWorkerTurn(host);
        var boundary = Boundary(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var audited = Audit(host);
        var visibility = Audience(edge, blueSees: true);
        var valid = Artifact(host, receipt, audited, visibility);
        boundary.PublishFromSettledEconomicPhase(
            valid, host.Snapshot, host.NextTurn);
        var original = boundary.Read(Red, host.Snapshot);

        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishFromSettledEconomicPhase(
                valid, host.Snapshot, host.NextTurn)); // idempotent retry refused
        var badHash = new WorldStateHash(
            receipt.Hash.FormatVersion, new string('A', 64));
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishFromSettledEconomicPhase(
                Artifact(host, receipt, audited, visibility, 2UL,
                    hash: badHash), host.Snapshot, host.NextTurn));
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishFromSettledEconomicPhase(
                Artifact(host, receipt, audited, visibility, 2UL,
                    snapshot: host.Snapshot.WithWorkers(host.Snapshot.Workers)),
                host.Snapshot, host.NextTurn));
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishFromSettledEconomicPhase(
                Artifact(host, receipt, audited, visibility, 2UL,
                    closedAt: new TurnNumber(22UL)),
                host.Snapshot, host.NextTurn));
        Assert.Same(original, boundary.Read(Red, host.Snapshot));
        Assert.Equal(1UL, boundary.Read(Red, host.Snapshot).EconomicCycleOrdinal);
    }

    [Fact]
    public void ForeignWorldAndInvalidVisibilityFailClosedWithoutExposingHiddenTrade()
    {
        var host = Start();
        var receipt = CloseOneWorkerTurn(host);
        var boundary = Boundary(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var audit = Audit(host);
        Assert.Throws<InvalidOperationException>(() =>
            boundary.Read(Red, CanonicalGameSession.Start(Request(999UL)).Snapshot));
        Assert.Throws<InvalidOperationException>(() =>
            boundary.Read(new CivilizationId(3UL), host.Snapshot));

        var missing = new Dictionary<CivilizationId,
            IReadOnlyList<PhysicalTacticalEdgeId>>
        {
            [Red] = new[] { edge }
        };
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishFromSettledEconomicPhase(
                Artifact(host, receipt, audit, missing),
                host.Snapshot, host.NextTurn));
        var duplicated = Audience(edge);
        duplicated[Red] = new[] { edge, edge };
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishFromSettledEconomicPhase(
                Artifact(host, receipt, audit, duplicated),
                host.Snapshot, host.NextTurn));
        var untraversed = new PhysicalTacticalEdgeId(
            host.Incidence.Refinement.FineParameters,
            host.Incidence.FineTopology.Edges[1].Id);
        var invented = Audience(edge);
        invented[Blue] = new[] { untraversed };
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishFromSettledEconomicPhase(
                Artifact(host, receipt, audit, invented),
                host.Snapshot, host.NextTurn));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            boundary.Read(Blue, host.Snapshot).Availability);
    }

    [Fact]
    public void FutureEconomicPublicationNeedsMatchingFineWorldAndValidHistory()
    {
        var host = Start();
        var turn = CloseOneWorkerTurn(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var visibility = Audience(edge);
        var boundary = Boundary(host);
        var correct = Audit(host);

        var foreignHost = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(303UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));
        var foreignAudit = Audit(foreignHost);
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishFromSettledEconomicPhase(
                Artifact(host, turn, foreignAudit, visibility),
                host.Snapshot, host.NextTurn));

        boundary.PublishFromSettledEconomicPhase(
            Artifact(host, turn, correct, visibility),
            host.Snapshot, host.NextTurn);
        var original = boundary.Read(Red, host.Snapshot);
        var nextTurn = CloseOneWorkerTurn(host);
        // A new economic cycle may be published after a *later* worker turn.
        boundary.PublishFromSettledEconomicPhase(
            Artifact(host, nextTurn, correct, visibility, ordinal: 2UL),
            host.Snapshot, host.NextTurn);
        var newer = boundary.Read(Red, host.Snapshot);
        Assert.NotSame(original, newer);
        Assert.Equal(2UL, newer.EconomicCycleOrdinal);
        Assert.Equal(nextTurn.Hash, newer.PublishedHash);
        Assert.Equal(turn.Hash, original.PublishedHash);
        Assert.Equal(Red, Assert.Single(original.UsedEdges).Owner);
    }

    private static (
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Scales)
        SettledFreightPolicy(PhysicalTacticalEdgeId edge,
            ulong freeCost = 100_000UL, ulong referenceCargo = 1_000_000UL) =>
        (new[]
        {
            new FoundationPhysicalRoutingEdgeCost(
                edge, FoundationInfrastructureOwnershipKind.Owned, Red,
                freeCost, 30_000UL)
        }, new[]
        {
            new FoundationPhysicalEdgeCarryingScale(edge, referenceCargo)
        });

    [Fact]
    public void WorkerOnlyTurnsCannotMintSettledFreightHistory()
    {
        var host = Start();
        var boundary = Boundary(host);
        Assert.Null(boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        CloseOneWorkerTurn(host);
        CloseOneWorkerTurn(host);
        Assert.Null(boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            boundary.Read(Red, host.Snapshot).Availability);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void ValidSettledCargoPublishesNextFreightExactlyOnceWithoutChangingClosedTurn()
    {
        var host = Start();
        var closed = CloseOneWorkerTurn(host);
        var boundary = Boundary(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var (roads, scales) = SettledFreightPolicy(edge);
        var cargo = Audit(host);
        var artifact = Artifact(host, closed, cargo, Audience(edge));

        boundary.PublishSettledCycleWithNextFreight(
            artifact, host.Snapshot, host.NextTurn,
            roads, scales, Unit);

        var first = Assert.IsType<FoundationPhysicalLaggedFreightQuoteState>(
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        Assert.True(first.HasSettledProvenance);
        Assert.Equal(1UL, first.AuditedPreviewCycles);
        var quote = Assert.Single(first.Edges);
        Assert.Equal(edge, quote.Edge);
        Assert.Equal(100_000UL, quote.FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(1_000_000UL, quote.ReferenceCargoMicro);
        // 2 million cargo on reference 1 million:
        // target=100_000*(1+2^4)=1_700_000.
        // Only the NEXT cycle gets 25% of the 1_600_000 surcharge.
        Assert.Equal(400_000UL,
            quote.QuotedCongestionSurchargeMicroPerWholeCargoUnit);
        var red = boundary.Read(Red, host.Snapshot);
        var blue = boundary.Read(Blue, host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.Ready,
            red.Availability);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset,
            blue.Availability);
        Assert.Equal(2_000_000UL,
            Assert.Single(red.UsedEdges).TotalCargoMicro);
        Assert.Empty(blue.UsedEdges); // No global freight leaked to client.

        // A worker-only turn cannot advance freight or rewrite the last
        // genuinely closed economic cycle.
        var nextWorkerTurn = CloseOneWorkerTurn(host);
        Assert.Same(first,
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        Assert.Same(red, boundary.Read(Red, host.Snapshot));
        Assert.Equal(nextWorkerTurn.Hash, host.StateHash);

        boundary.PublishSettledCycleWithNextFreight(
            Artifact(host, nextWorkerTurn, Audit(host),
                Audience(edge), ordinal: 2UL),
            host.Snapshot, host.NextTurn, roads, scales, Unit);
        var second = Assert.IsType<FoundationPhysicalLaggedFreightQuoteState>(
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        Assert.NotSame(first, second);
        Assert.Equal(2UL, second.AuditedPreviewCycles);
        Assert.Equal(700_000UL,
            Assert.Single(second.Edges)
                .QuotedCongestionSurchargeMicroPerWholeCargoUnit);
        Assert.Equal(400_000UL,
            Assert.Single(first.Edges)
                .QuotedCongestionSurchargeMicroPerWholeCargoUnit);
        Assert.Equal(2UL,
            boundary.Read(Red, host.Snapshot).EconomicCycleOrdinal);
    }

    [Fact]
    public void ClosedZeroCargoCoolsNextFreightButNeverInventsTrade()
    {
        var host = Start();
        var boundary = Boundary(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var (roads, scales) = SettledFreightPolicy(edge);
        var firstTurn = CloseOneWorkerTurn(host);
        boundary.PublishSettledCycleWithNextFreight(
            Artifact(host, firstTurn, Audit(host), Audience(edge)),
            host.Snapshot, host.NextTurn, roads, scales, Unit);
        var first = Assert.IsType<FoundationPhysicalLaggedFreightQuoteState>(
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        Assert.Equal(400_000UL,
            Assert.Single(first.Edges)
                .QuotedCongestionSurchargeMicroPerWholeCargoUnit);

        var nextTurn = CloseOneWorkerTurn(host);
        var none = Audit(host, withTransport: false);
        var noVisibility = new Dictionary<CivilizationId,
            IReadOnlyList<PhysicalTacticalEdgeId>>
        {
            [Red] = Array.Empty<PhysicalTacticalEdgeId>(),
            [Blue] = Array.Empty<PhysicalTacticalEdgeId>()
        };
        boundary.PublishSettledCycleWithNextFreight(
            Artifact(host, nextTurn, none, noVisibility, ordinal: 2UL),
            host.Snapshot, host.NextTurn, roads, scales, Unit);
        var cooled = Assert.IsType<FoundationPhysicalLaggedFreightQuoteState>(
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        Assert.Equal(300_000UL, Assert.Single(cooled.Edges)
            .QuotedCongestionSurchargeMicroPerWholeCargoUnit);
        Assert.Empty(boundary.Read(Red, host.Snapshot).UsedEdges);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.Ready,
            boundary.Read(Blue, host.Snapshot).Availability);
    }

    [Fact]
    public void BadWorldHashCargoScaleOwnershipAndVisibilityAreAtomicFailures()
    {
        var host = Start();
        var boundary = Boundary(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var (roads, scales) = SettledFreightPolicy(edge);
        var firstTurn = CloseOneWorkerTurn(host);
        boundary.PublishSettledCycleWithNextFreight(
            Artifact(host, firstTurn, Audit(host), Audience(edge)),
            host.Snapshot, host.NextTurn, roads, scales, Unit);
        var firstQuote = boundary.ReadLastSettledFreightForNextCycle(host.Snapshot);
        var firstView = boundary.Read(Red, host.Snapshot);
        var secondTurn = CloseOneWorkerTurn(host);
        var artifact = Artifact(host, secondTurn, Audit(host),
            Audience(edge), ordinal: 2UL);

        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishSettledCycleWithNextFreight(
                artifact, host.Snapshot, host.NextTurn,
                roads, scales, "wrong-cargo-unit"));
        Assert.Throws<ArgumentException>(() =>
            boundary.PublishSettledCycleWithNextFreight(
                artifact, host.Snapshot, host.NextTurn,
                roads, new[] { scales[0] with
                { CargoMicroAtReferenceLoad = 0UL } }, Unit));
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishSettledCycleWithNextFreight(
                artifact, host.Snapshot, host.NextTurn,
                new[] { roads[0] with
                { InfrastructureOwner = Blue } }, scales, Unit));
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishSettledCycleWithNextFreight(
                Artifact(host, secondTurn, Audit(host), Audience(edge),
                    ordinal: 2UL,
                    hash: new WorldStateHash(
                        secondTurn.Hash.FormatVersion, new string('A', 64))),
                host.Snapshot, host.NextTurn, roads, scales, Unit));
        var invented = Audience(edge);
        invented[Blue] = new[] { new PhysicalTacticalEdgeId(
            host.Incidence.Refinement.FineParameters,
            host.Incidence.FineTopology.Edges[1].Id) };
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishSettledCycleWithNextFreight(
                Artifact(host, secondTurn, Audit(host), invented,
                    ordinal: 2UL),
                host.Snapshot, host.NextTurn, roads, scales, Unit));

        Assert.Same(firstQuote,
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        Assert.Same(firstView, boundary.Read(Red, host.Snapshot));
        Assert.Equal(1UL, boundary.Read(Red, host.Snapshot).EconomicCycleOrdinal);
        Assert.Equal(secondTurn.Hash, host.StateHash);

        // A valid retry after all failures still succeeds exactly once.
        boundary.PublishSettledCycleWithNextFreight(
            artifact, host.Snapshot, host.NextTurn, roads, scales, Unit);
        Assert.Equal(2UL,
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot)!
                .AuditedPreviewCycles);
    }

    [Fact]
    public void LegacyPublicationCannotReuseEarlierSettledFreightOrSkipCycle()
    {
        var host = Start();
        var boundary = Boundary(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var (roads, scales) = SettledFreightPolicy(edge);
        var first = CloseOneWorkerTurn(host);
        var firstArtifact = Artifact(
            host, first, Audit(host), Audience(edge));
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishSettledCycleWithNextFreight(
                Artifact(host, first, Audit(host), Audience(edge),
                    ordinal: 2UL),
                host.Snapshot, host.NextTurn, roads, scales, Unit));
        Assert.Null(boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));

        boundary.PublishSettledCycleWithNextFreight(
            firstArtifact, host.Snapshot, host.NextTurn,
            roads, scales, Unit);
        var second = CloseOneWorkerTurn(host);
        var secondArtifact = Artifact(host, second,
            Audit(host), Audience(edge), ordinal: 2UL);
        // The old publication API does not carry the privileged freight
        // state. It invalidates it, rather than making up a next quote.
        boundary.PublishFromSettledEconomicPhase(
            secondArtifact, host.Snapshot, host.NextTurn);
        Assert.Throws<InvalidOperationException>(() =>
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        var third = CloseOneWorkerTurn(host);
        Assert.Throws<InvalidOperationException>(() =>
            boundary.PublishSettledCycleWithNextFreight(
                Artifact(host, third, Audit(host), Audience(edge),
                    ordinal: 3UL),
                host.Snapshot, host.NextTurn, roads, scales, Unit));
        Assert.Equal(2UL,
            boundary.Read(Red, host.Snapshot).EconomicCycleOrdinal);
    }

    [Fact]
    public void InfrastructureUpgradeResetsOnlyThisCrossingCongestionMemory()
    {
        var host = Start();
        var boundary = Boundary(host);
        var (_, _, edge) = FirstEdge(host.Incidence);
        var (roads, scales) = SettledFreightPolicy(edge);
        var first = CloseOneWorkerTurn(host);
        boundary.PublishSettledCycleWithNextFreight(
            Artifact(host, first, Audit(host), Audience(edge)),
            host.Snapshot, host.NextTurn, roads, scales, Unit);
        var second = CloseOneWorkerTurn(host);
        var upgraded = new[] { roads[0] with
        {
            PhysicalCostMicroPerWholeCargoUnit = 80_000UL,
            ExternalTariffMicroPerWholeCargoUnit = 50_000UL
        } };
        boundary.PublishSettledCycleWithNextFreight(
            Artifact(host, second, Audit(host), Audience(edge),
                ordinal: 2UL),
            host.Snapshot, host.NextTurn, upgraded, scales, Unit);
        var next = Assert.IsType<FoundationPhysicalLaggedFreightQuoteState>(
            boundary.ReadLastSettledFreightForNextCycle(host.Snapshot));
        var line = Assert.Single(next.Edges);
        Assert.Equal(80_000UL, line.FreePhysicalCostMicroPerWholeCargoUnit);
        // Reset previous congestion memory because baseline changed.
        // 80_000 * (2^4) / 4 = 320_000.
        Assert.Equal(320_000UL,
            line.QuotedCongestionSurchargeMicroPerWholeCargoUnit);
        Assert.True(next.HasSettledProvenance);
    }

}
