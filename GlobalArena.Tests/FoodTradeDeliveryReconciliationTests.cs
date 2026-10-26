using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoodTradeDeliveryReconciliationTests
{
    private static FoodCommodityCatalog Catalog() =>
        new(new[]
        {
            new FoodCommodityDefinition(new CommodityId(8U), "roots", 2_000UL),
            new FoodCommodityDefinition(new CommodityId(7U), "grain", 1_000UL)
        });

    private static EconomicTradeAllocation Trade(
        ulong source = 1UL, ulong destination = 2UL,
        uint commodity = 7U, ulong quantity = 1_000_000UL,
        ulong price = 900_000UL, ulong transport = 100_000UL) =>
        new(new EconomicPointId(source), EconomicActivityKind.Agriculture,
            new EconomicPointId(destination), new CommodityId(commodity),
            quantity, price, transport);

    private static FoodTradeDeliveryAttestation Receipt(
        EconomicTradeAllocation trade,
        ulong delivered = 500_000UL,
        string source = "delivery.log") =>
        new(trade, delivered, source);

    private static FoodTradeDeliveryAttestationSnapshot Snapshot(
        params FoodTradeDeliveryAttestation[] receipts) =>
        new(new TurnNumber(1UL), receipts);

    private static FoodMarketDemandCoverageResult Coverage(
        params FoodMarketDemandCoverageEntry[] rows) =>
        new(rows);

    private static FoodMarketDemandCoverageEntry Covered(
        ulong destination = 2UL, uint commodity = 7U,
        ulong demand = 5_000_000UL, ulong trade = 1_000_000UL) =>
        new(new EconomicPointId(destination), new CommodityId(commodity),
            demand, trade);

    private static FoodTradeDeliveryReconciliationResult Resolve(
        EconomicMarketAllocationResult market,
        FoodMarketDemandCoverageResult coverage,
        FoodTradeDeliveryAttestationSnapshot? receipts = null,
        FoodCommodityCatalog? catalog = null,
        ulong turn = 1UL) =>
        new FoodTradeDeliveryReconciliationResolver().Resolve(
            market, coverage, catalog ?? Catalog(),
            receipts ?? Snapshot(), new TurnNumber(turn));

    [Fact]
    public void MissingReceiptMeansUnknownDeliveryNotZeroDelivered()
    {
        var trade = Trade();
        var result = Resolve(
            new EconomicMarketAllocationResult(new[] { trade }),
            Coverage(Covered()));

        var row = Assert.Single(result.Entries);
        Assert.False(row.HasAttestation);
        Assert.Null(row.RawAttestedDeliveredQuantity);
        Assert.Null(row.RawAllocationWithoutAttestedDelivery);
        Assert.Null(row.EvidenceSourceKey);

        var total = Assert.Single(result.CommoditySummaries);
        Assert.Equal(1_000_000UL, total.RawAllocated);
        Assert.Equal(0UL, total.RawAllocatedWithAttestation);
        Assert.Equal(1_000_000UL, total.RawAllocatedWithUnknownDelivery);
        Assert.Equal(0UL, total.RawAttestedDelivered);
    }

    [Fact]
    public void ExplicitZeroAttestationIsDistinctFromMissingEvidence()
    {
        var trade = Trade();
        var result = Resolve(
            new EconomicMarketAllocationResult(new[] { trade }),
            Coverage(Covered()),
            Snapshot(Receipt(trade, delivered: 0UL)));

        var row = Assert.Single(result.Entries);
        Assert.True(row.HasAttestation);
        Assert.Equal(0UL, row.RawAttestedDeliveredQuantity);
        Assert.Equal(1_000_000UL, row.RawAllocationWithoutAttestedDelivery);
        Assert.Equal("delivery.log", row.EvidenceSourceKey);
        Assert.Equal(1_000_000UL,
            result.CommoditySummaries.Single().RawAllocatedNotAttestedDelivered);
        Assert.Equal(0UL,
            result.CommoditySummaries.Single().RawAllocatedWithUnknownDelivery);
    }

    [Fact]
    public void PartialOrCompleteDeliveryAttestationNeverAltersMarketDecision()
    {
        var trade = Trade();
        var market = new EconomicMarketAllocationResult(new[] { trade });
        var coverage = Coverage(Covered());

        var partial = Resolve(market, coverage,
            Snapshot(Receipt(trade, delivered: 400_000UL)));
        var complete = Resolve(market, coverage,
            Snapshot(Receipt(trade, delivered: 1_000_000UL)));

        Assert.Equal(400_000UL,
            partial.CommoditySummaries.Single().RawAttestedDelivered);
        Assert.Equal(600_000UL,
            partial.CommoditySummaries.Single().RawAllocatedNotAttestedDelivered);
        Assert.Equal(1_000_000UL,
            complete.CommoditySummaries.Single().RawAttestedDelivered);
        Assert.Equal(0UL,
            complete.CommoditySummaries.Single().RawAllocatedNotAttestedDelivered);
        Assert.Equal(1_000_000UL, market.TotalRawAllocatedQuantity);
        Assert.Equal(1_000_000UL, coverage.TotalRawTradeAllocatedQuantity);
    }

    [Fact]
    public void MultipleSuppliersRetainIndependentEvidenceAndUnknownQuantities()
    {
        var one = Trade(source: 1UL, quantity: 2_000_000UL);
        var two = Trade(source: 3UL, quantity: 1_000_000UL);
        var market = new EconomicMarketAllocationResult(new[] { two, one });
        var coverage = Coverage(Covered(trade: 3_000_000UL));

        var result = Resolve(market, coverage,
            Snapshot(Receipt(one, delivered: 1_500_000UL)));

        Assert.Equal(new[] { 1UL, 3UL },
            result.Entries.Select(e => e.Allocation.SourceEconomicPointId.Value));
        Assert.True(result.Entries[0].HasAttestation);
        Assert.False(result.Entries[1].HasAttestation);
        var total = Assert.Single(result.CommoditySummaries);
        Assert.Equal(3_000_000UL, total.RawAllocated);
        Assert.Equal(2_000_000UL, total.RawAllocatedWithAttestation);
        Assert.Equal(1_000_000UL, total.RawAllocatedWithUnknownDelivery);
        Assert.Equal(1_500_000UL, total.RawAttestedDelivered);
        Assert.Equal(500_000UL, total.RawAllocatedNotAttestedDelivered);
    }

    [Fact]
    public void DifferentFoodsAreNeverSummedAsIfTheirMassScaleMatched()
    {
        var grain = Trade(commodity: 7U, quantity: 1_000_000UL);
        var roots = Trade(commodity: 8U, quantity: 2_000_000UL);
        var result = Resolve(
            new EconomicMarketAllocationResult(new[] { roots, grain }),
            Coverage(
                Covered(commodity: 8U, trade: 2_000_000UL),
                Covered(commodity: 7U, trade: 1_000_000UL)),
            Snapshot(Receipt(grain), Receipt(roots, delivered: 1_000_000UL)));

        Assert.Equal(new[] { 7U, 8U },
            result.CommoditySummaries.Select(s => s.CommodityId.Value));
        Assert.Equal(new[] { 500_000UL, 1_000_000UL },
            result.CommoditySummaries.Select(s => s.RawAttestedDelivered));
        Assert.Equal(2, result.CommoditySummaries.Count);
        Assert.Equal(1_000UL, Catalog().Commodities[0].GramsPerFlowUnit);
        Assert.Equal(2_000UL, Catalog().Commodities[1].GramsPerFlowUnit);
    }

    [Fact]
    public void NonfoodTradeIsIgnoredAndCannotHaveFoodDeliveryAttestation()
    {
        var food = Trade();
        var nonfood = Trade(commodity: 55U, destination: 3UL);
        var market = new EconomicMarketAllocationResult(new[] { food, nonfood });
        var coverage = Coverage(Covered());

        Assert.Single(Resolve(market, coverage).Entries);
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(market, coverage,
                Snapshot(Receipt(nonfood, delivered: 100_000UL))));
    }

    [Fact]
    public void AttestationForUnselectedTradeFailsEvenIfFoodIsRegistered()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(new EconomicMarketAllocationResult(Array.Empty<EconomicTradeAllocation>()),
                Coverage(Covered(trade: 0UL)),
                Snapshot(Receipt(Trade()))));
    }

    [Fact]
    public void MatchingTradeIdentityRequiresExactPriceAndCost()
    {
        var actual = Trade();
        var market = new EconomicMarketAllocationResult(new[] { actual });
        var coverage = Coverage(Covered());
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(market, coverage,
                Snapshot(Receipt(Trade(price: 850_000UL)))));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(market, coverage,
                Snapshot(Receipt(Trade(transport: 90_000UL)))));
    }

    [Fact]
    public void MatchingPriceAndRouteWithWrongAllocationQuantityIsRejected()
    {
        var actual = Trade(quantity: 1_000_000UL);
        var stale = Trade(quantity: 2_000_000UL);
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(
                new EconomicMarketAllocationResult(new[] { actual }),
                Coverage(Covered()),
                Snapshot(Receipt(stale, delivered: 400_000UL))));
    }

    [Fact]
    public void OverDeliveryIsRejectedAtAttestationConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Receipt(Trade(), delivered: 1_000_001UL));
    }

    [Fact]
    public void MarketAndCoverageInputsMustAgreeExactly()
    {
        var market = new EconomicMarketAllocationResult(new[] { Trade() });

        Assert.Throws<InvalidOperationException>(() =>
            Resolve(market, Coverage(Covered(trade: 0UL))));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(market, Coverage(Covered(trade: 2_000_000UL))));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(market, Coverage(Covered(destination: 4UL))));
    }

    [Fact]
    public void UnmatchedFoodDemandWithoutTradeIsAllowed()
    {
        var result = Resolve(
            new EconomicMarketAllocationResult(Array.Empty<EconomicTradeAllocation>()),
            Coverage(Covered(trade: 0UL)));

        Assert.Empty(result.Entries);
        Assert.Empty(result.CommoditySummaries);
    }

    [Fact]
    public void SnapshotMustMatchCurrentTurnEvenWhenNoTradeOccurred()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(
                new EconomicMarketAllocationResult(Array.Empty<EconomicTradeAllocation>()),
                Coverage(), Snapshot(), turn: 2UL));
        Assert.Throws<ArgumentException>(() =>
            new FoodTradeDeliveryAttestationSnapshot(
                default, Array.Empty<FoodTradeDeliveryAttestation>()));
    }

    [Fact]
    public void DuplicateEvidenceForOneMarketAllocationCannotDoubleCountDelivery()
    {
        var trade = Trade();
        Assert.Throws<ArgumentException>(() =>
            Snapshot(Receipt(trade, source: "a"),
                Receipt(trade, source: "b")));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(new FoodTradeDeliveryAttestation[] { null! }));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodTradeDeliveryAttestationSnapshot(new TurnNumber(1UL), null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Survey")]
    [InlineData("note 42")]
    [InlineData("café")]
    public void NoncanonicalEvidenceKeysAreRejected(string key)
    {
        Assert.Throws<ArgumentException>(() =>
            Receipt(Trade(), source: key));
    }

    [Fact]
    public void NullReferenceAndEvidenceSourceAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FoodTradeDeliveryAttestation(null!, 0UL, "report"));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodTradeDeliveryAttestation(Trade(), 0UL, null!));
    }

    [Fact]
    public void CanonicalSnapshotAndReportCannotBeModifiedByCaller()
    {
        var first = Trade(source: 3UL);
        var second = Trade(source: 1UL);
        var input = new List<FoodTradeDeliveryAttestation>
        {
            Receipt(first), Receipt(second)
        };
        var snapshot = new FoodTradeDeliveryAttestationSnapshot(
            new TurnNumber(1UL), input);
        input.Clear();

        Assert.Equal(new[] { 1UL, 3UL },
            snapshot.Attestations.Select(r => r.ReferencedAllocation.SourceEconomicPointId.Value));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoodTradeDeliveryAttestation>)snapshot.Attestations).Clear());

        var result = Resolve(
            new EconomicMarketAllocationResult(new[] { first, second }),
            Coverage(Covered(trade: 2_000_000UL)),
            snapshot);
        Assert.Equal(2, result.Entries.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoodTradeDeliveryReconciliationEntry>)result.Entries).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoodTradeDeliveryCommoditySummary>)result.CommoditySummaries).Clear());
    }

    [Fact]
    public void DirectAggregateOverflowAndDuplicatedTradesFailClosed()
    {
        var huge = Trade(source: 1UL, quantity: ulong.MaxValue,
            price: 1UL, transport: 0UL);
        var extra = Trade(source: 3UL, quantity: 1UL,
            price: 1UL, transport: 0UL);

        Assert.Throws<OverflowException>(() =>
            new FoodTradeDeliveryReconciliationResult(new[]
            {
                new FoodTradeDeliveryReconciliationEntry(huge, null),
                new FoodTradeDeliveryReconciliationEntry(extra, null)
            }));

        var row = new FoodTradeDeliveryReconciliationEntry(Trade(), null);
        Assert.Throws<ArgumentException>(() =>
            new FoodTradeDeliveryReconciliationResult(new[] { row, row }));
        Assert.Throws<ArgumentException>(() =>
            new FoodTradeDeliveryReconciliationResult(
                new FoodTradeDeliveryReconciliationEntry[] { null! }));
    }

    [Fact]
    public void DirectEntryRejectsMismatchedEvidenceAndNullAllocation()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FoodTradeDeliveryReconciliationEntry(null!, null));
        Assert.Throws<ArgumentException>(() =>
            new FoodTradeDeliveryReconciliationEntry(
                Trade(), Receipt(Trade(destination: 3UL))));
    }

    [Fact]
    public void ResolverRejectsNullInputs()
    {
        var resolver = new FoodTradeDeliveryReconciliationResolver();
        var market = new EconomicMarketAllocationResult(new[] { Trade() });
        var coverage = Coverage(Covered());
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(null!, coverage, Catalog(), Snapshot(), new TurnNumber(1UL)));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(market, null!, Catalog(), Snapshot(), new TurnNumber(1UL)));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(market, coverage, null!, Snapshot(), new TurnNumber(1UL)));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(market, coverage, Catalog(), null!, new TurnNumber(1UL)));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodTradeDeliveryReconciliationResult(null!));
    }

    [Fact]
    public void ActualMarketPhaseAndIndependentAccessStayDistinctFromShipmentAttestations()
    {
        var state = WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(new[]
            {
                new CivilizationId(1UL), new CivilizationId(2UL)
            }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL), new CivilizationId(1UL),
                        new StrategicCellId(1UL), 2UL,
                        new[] { new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture, 2UL) }),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL), new CivilizationId(2UL),
                        new StrategicCellId(2UL), 0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(new EconomicPointId(2UL), 10UL)
                }));

        var catalog = Catalog();
        var profiles = FoodResidentDemandProfileMaterializer.Materialize(
            state, catalog,
            new[] { new FoodResidentConsumptionRequirement(
                new EconomicPointId(2UL), new CommodityId(7U), 500UL) });

        var phase = new EconomicResidentFoodAccessTurnPhase(
            new[] { new EconomicProductionPotential(
                new EconomicPointId(1UL), new CommodityId(7U),
                EconomicActivityKind.Agriculture, 1_000_000L) },
            profiles,
            new[] { new EconomicTransferSignal(
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL), new EconomicPointId(2UL)),
                1, 100_000UL, 1_000_000UL, 0UL) },
            new EconomicMarketAllocationPolicy(900_000UL, 900_000UL, 1, 1),
            catalog,
            new FoodAccessObservationSnapshot(
                new TurnNumber(1UL),
                new[] { new FoodPointAccessObservation(
                    new EconomicPointId(2UL), new CommodityId(7U),
                    2_000_000UL, "survey-a") }));

        var hash = new CanonicalWorldStateHasher().Compute(state);
        var resolved = phase.Resolve(
            new TurnResolutionInput(
                state, Array.Empty<ISimulationCommand>(),
                new SimulationContext(new TurnNumber(1UL), new SimulationSeed(3UL))),
            state);

        var tradeMarket = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            resolved.Events[1]).Allocation;
        var tradeCoverage = Assert.IsType<EconomicFoodMarketCoverageResolvedEvent>(
            resolved.Events[2]).Coverage;
        var independentAccess = Assert.IsType<EconomicFoodAccessCoverageResolvedEvent>(
            resolved.Events[3]).Coverage;

        var chosen = Assert.Single(tradeMarket.Allocations);
        var report = new FoodTradeDeliveryReconciliationResolver().Resolve(
            tradeMarket, tradeCoverage, catalog,
            Snapshot(Receipt(chosen, delivered: 600_000UL,
                source: "carrier-001")),
            new TurnNumber(1UL));

        Assert.Equal(1_000_000UL,
            report.CommoditySummaries.Single().RawAllocated);
        Assert.Equal(600_000UL,
            report.CommoditySummaries.Single().RawAttestedDelivered);
        Assert.Equal(2_000_000UL,
            independentAccess.Entries.Single().RawAccessibleQuantity);
        Assert.Equal(1_000_000UL,
            tradeCoverage.Entries.Single().RawTradeAllocatedQuantity);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
        Assert.Same(state, resolved.ResultingWorldState);
    }
}
