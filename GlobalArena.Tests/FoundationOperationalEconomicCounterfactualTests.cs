using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationOperationalEconomicCounterfactualTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public FoundationOperationalEconomicCounterfactualTests(
        Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private static readonly CivilizationId Human = new(1UL);
    private static readonly CivilizationId Ai = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationAuthoritativeEconomicSettlementPolicy MarketPolicy() =>
        new(
            new FoundationActivityCommodityDemandPolicy(
                new[] { Grain, Wood },
                Enum.GetValues<FoundationPhysicalDemandProfile>()
                    .SelectMany(profile => new[] { Grain, Wood }.Select(item =>
                        new FoundationActivityCommodityDemandCoefficient(
                            profile, item, 1_000_000UL)))),
            new FoundationCommodityCargoConversionPolicy(
                "r154-physical-cargo", new[]
                {
                    new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
                    new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
                }),
            new[]
            {
                new FoundationPhysicalEconomicTaxRate(Human, 1_000U),
                new FoundationPhysicalEconomicTaxRate(Ai, 500U)
            }, maxProducerPasses: 64);

    private static (
        WorldState Before, WorldState After,
        PhysicalTacticalIncidenceMap Incidence,
        FoundationFineFirstPhysicalGeography Geography,
        PhysicalWorkerRuntimeRecord Reassigned) Scenario(
        int humanWorkerOrdinal = 0,
        PhysicalWorkerOccupation destination =
            PhysicalWorkerOccupation.LandTransport)
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var geometry = FoundationFineFirstPhysicalGeography.Generate(
            fixture.Session.GeneratedWorld, fixture.Incidence);
        var seeded = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            fixture.Session.GeneratedWorld, fixture.Incidence, geometry,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var before = fixture.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, fixture.Session.GeneratedWorld, fixture.Incidence);
        var person = before.Workers.Workers.Where(worker =>
            worker.Owner == Human &&
            worker.Activity == PhysicalWorkerActivity.Producing)
            .OrderBy(worker => worker.Id.Value)
            .Skip(humanWorkerOrdinal).First();
        var workers = before.Workers.Workers.Select(worker =>
            worker.Id == person.Id
                ? worker.WithTacticalAssignment(
                    worker.TacticalTileId!.Value, worker.CellId,
                    PhysicalWorkerActivity.Other,
                    destination)
                : worker).ToArray();
        var after = before.WithWorkers(new PhysicalWorkerRuntimeState(
            workers, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true));
        return (before, after, fixture.Incidence, geometry, person);
    }

    private static FoundationOperationalEconomicCounterfactual Compare(
        WorldState before, WorldState after,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography) =>
        FoundationOperationalEconomicCounterfactualProjector.Compare(
            before, after, incidence, geography, MarketPolicy(),
            new FoundationOperationalProductivityPolicy(
                new FoundationOperationalProductivityBand(
                    1_000_000UL, 10_000U),
                new FoundationOperationalProductivityBand(
                    1_000_000UL, 10_000U)),
            new FoundationOperationalFreightShadowPolicy(
                1_000_000UL, 1_000_000UL));

    [Fact]
    public void RealWorkerTransferCreatesThreeReconciledUncommittedEconomies()
    {
        var s = Scenario();
        var initialRevision = s.Before.Revision;
        var reassignedRevision = s.After.Revision;
        var result = Compare(s.Before, s.After, s.Incidence, s.Geography);

        Assert.False(result.IsCommitted);
        Assert.Equal(new BigInteger(s.Reassigned.OutputMicroPerTurn),
            result.LostProductionGoodsMicro);
        Assert.Equal(result.AllProducingBaseline.ProducedAndSoldGoodsMicro -
            s.Reassigned.OutputMicroPerTurn,
            result.ReassignedWithoutServiceDiscount.ProducedAndSoldGoodsMicro);
        Assert.Equal(result.ReassignedWithoutServiceDiscount.ProducedAndSoldGoodsMicro,
            result.ReassignedWithServiceDiscount.ProducedAndSoldGoodsMicro);
        Assert.True(result.CommittedOperationalService.TotalAllocatedEffortMicro > 0UL);
        Assert.True(result.CommittedOperationalService.TotalServiceMicro > BigInteger.Zero);
        Assert.Contains(result.FreightShadow.EdgeReadings, row =>
            row.OperationalServiceMicro > 0UL &&
            row.ShadowPhysicalCostMicroPerWholeCargoUnit <=
                row.OriginalPhysicalCostMicroPerWholeCargoUnit);
        Assert.True(result.AllProducingBaseline.GrossSalesScaled > BigInteger.Zero);
        Assert.True(result.ReassignedWithoutServiceDiscount.GrossSalesScaled > BigInteger.Zero);
        Assert.True(result.ReassignedWithServiceDiscount.GrossSalesScaled > BigInteger.Zero);
        Assert.Equal(result.ReassignedWithServiceDiscount.GrossSalesScaled -
            result.ReassignedWithoutServiceDiscount.GrossSalesScaled,
            result.OperationalGrossSalesDeltaScaled);
        Assert.Equal(result.ReassignedWithServiceDiscount.NetSellerReceiptsScaled -
            result.AllProducingBaseline.NetSellerReceiptsScaled,
            result.OverallSellerNetDeltaScaled);
        Assert.Equal(initialRevision, s.Before.Revision);
        Assert.Equal(reassignedRevision, s.After.Revision);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            s.Before.Workers.Workers.Single(w =>
                w.Id == s.Reassigned.Id).Activity);
        Assert.Equal(PhysicalWorkerActivity.Other,
            s.After.Workers.Workers.Single(w =>
                w.Id == s.Reassigned.Id).Activity);
    }

    [Fact]
    public void QuantitativeTransferAndLogisticsEffectsReconcileWithoutAssumingProfit()
    {
        var scenario = Scenario();
        var comparison = Compare(scenario.Before, scenario.After,
            scenario.Incidence, scenario.Geography);
        var result = FoundationOperationalEconomicAssessmentProjector
            .Evaluate(comparison);

        Assert.False(result.IsCommitted);
        Assert.Equal(-new BigInteger(scenario.Reassigned.OutputMicroPerTurn),
            result.ProductionOpportunityDelta.GoodsMicro);
        Assert.Equal(BigInteger.Zero, result.LogisticsServiceDelta.GoodsMicro);
        Assert.Equal(result.ProductionOpportunityDelta.GoodsMicro,
            result.CombinedDelta.GoodsMicro);

        Assert.Equal(result.ProductionOpportunityDelta.GrossSalesScaled +
            result.LogisticsServiceDelta.GrossSalesScaled,
            result.CombinedDelta.GrossSalesScaled);
        Assert.Equal(result.ProductionOpportunityDelta.PhysicalFreightScaled +
            result.LogisticsServiceDelta.PhysicalFreightScaled,
            result.CombinedDelta.PhysicalFreightScaled);
        Assert.Equal(result.ProductionOpportunityDelta.NetSellersScaled +
            result.LogisticsServiceDelta.NetSellersScaled,
            result.CombinedDelta.NetSellersScaled);
        Assert.Equal(result.ProductionOpportunityDelta
                .TotalCivilizationCashEquivalentScaled +
            result.LogisticsServiceDelta
                .TotalCivilizationCashEquivalentScaled,
            result.CombinedDelta.TotalCivilizationCashEquivalentScaled);

        // Exact total civilization cash equivalent is gross sales minus
        // physical freight; paid tolls and taxes are transfers to other
        // civilization accounts, not resources destroyed by the economy.
        foreach (var reading in new[]
        {
            comparison.AllProducingBaseline,
            comparison.ReassignedWithoutServiceDiscount,
            comparison.ReassignedWithServiceDiscount
        })
            Assert.Equal(reading.GrossSalesScaled -
                reading.PhysicalFreightScaled,
                reading.NetSellerReceiptsScaled +
                reading.RevenueTaxScaled + reading.ExternalTollsScaled);

        var expectedOutcome = result.CombinedDelta
            .TotalCivilizationCashEquivalentScaled.Sign;
        Assert.Equal(expectedOutcome < 0
                ? FoundationOperationalEconomicRelativeOutcome.Worse
                : expectedOutcome > 0
                    ? FoundationOperationalEconomicRelativeOutcome.Better
                    : FoundationOperationalEconomicRelativeOutcome.Equivalent,
            result.CombinedCashOutcome);

        _output.WriteLine(
            "GA_NEXT_R155_ECONOMIC_DIAGNOSTIC " +
            $"goods_A={comparison.AllProducingBaseline.ProducedAndSoldGoodsMicro} " +
            $"goods_B={comparison.ReassignedWithoutServiceDiscount.ProducedAndSoldGoodsMicro} " +
            $"goods_C={comparison.ReassignedWithServiceDiscount.ProducedAndSoldGoodsMicro} " +
            $"cash_A={comparison.AllProducingBaseline.GrossSalesScaled -
                comparison.AllProducingBaseline.PhysicalFreightScaled} " +
            $"cash_B={comparison.ReassignedWithoutServiceDiscount.GrossSalesScaled -
                comparison.ReassignedWithoutServiceDiscount.PhysicalFreightScaled} " +
            $"cash_C={comparison.ReassignedWithServiceDiscount.GrossSalesScaled -
                comparison.ReassignedWithServiceDiscount.PhysicalFreightScaled} " +
            $"transfer_delta={result.ProductionOpportunityDelta.TotalCivilizationCashEquivalentScaled} " +
            $"logistics_delta={result.LogisticsServiceDelta.TotalCivilizationCashEquivalentScaled} " +
            $"combined_delta={result.CombinedDelta.TotalCivilizationCashEquivalentScaled} " +
            $"break_even_need={result.RequiredServiceGainToBreakEvenScaled} " +
            $"break_even_gap={result.RemainingBreakEvenGapScaled} " +
            $"outcome={result.CombinedCashOutcome} " +
            $"routes_A={comparison.AllProducingBaseline.UsedPhysicalCrossings} " +
            $"routes_B={comparison.ReassignedWithoutServiceDiscount.UsedPhysicalCrossings} " +
            $"routes_C={comparison.ReassignedWithServiceDiscount.UsedPhysicalCrossings}");

        // R156: a PASSING xUnit test may hide ITestOutputHelper in standard
        // GitHub logs. Publish exact evidence as explicit CI artifacts instead.
        // No report is created during regular local unit-test execution.
        var reportDirectory = Environment.GetEnvironmentVariable(
            "GA_R156_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
            var moneyScale = FoundationPhysicalEconomicAccountingProposal.MoneyScale;

            static string Money(BigInteger scaled)
            {
                bool negative = scaled.Sign < 0;
                BigInteger whole = BigInteger.DivRem(BigInteger.Abs(scaled),
                    FoundationPhysicalEconomicAccountingProposal.MoneyScale,
                    out var fractional);
                string decimalPart = fractional.ToString().PadLeft(16, '0')
                    .TrimEnd('0');
                return (negative ? "-" : "") + whole.ToString() +
                    (decimalPart.Length > 0 ? "." + decimalPart : "");
            }

            static object ScenarioEntry(
                FoundationOperationalEconomicScenarioReading row)
                => new
                {
                    goodsMicro = row.ProducedAndSoldGoodsMicro,
                    deliveries = row.DeliveryCount,
                    usedCrossings = row.UsedPhysicalCrossings,
                    traversalCargoMicro = row.TotalEdgeTraversalCargoMicro,
                    grossSalesScaled = row.GrossSalesScaled.ToString(),
                    physicalFreightScaled = row.PhysicalFreightScaled.ToString(),
                    externalTollsScaled = row.ExternalTollsScaled.ToString(),
                    revenueTaxScaled = row.RevenueTaxScaled.ToString(),
                    sellerNetScaled = row.NetSellerReceiptsScaled.ToString(),
                    totalCivilizationCashEquivalentScaled =
                        (row.GrossSalesScaled - row.PhysicalFreightScaled)
                            .ToString()
                };

            var json = new
            {
                schema = "ga-next-r156-economic-diagnostic-v1",
                source = "FoundationOperationalEconomicCounterfactualTests",
                worldSeed = 20261012UL,
                workersPerCivilization = 3,
                workerTransferred = scenario.Reassigned.Id.Value,
                transferredFrom = "Producing",
                transferredTo = "LandTransport",
                landReferenceServiceMicro = 1_000_000UL,
                portReferenceServiceMicro = 1_000_000UL,
                moneyScale = moneyScale.ToString(),
                stateCommitted = false,
                populationGrowthEvaluated = false,
                scenarios = new
                {
                    A = ScenarioEntry(comparison.AllProducingBaseline),
                    B = ScenarioEntry(comparison.ReassignedWithoutServiceDiscount),
                    C = ScenarioEntry(comparison.ReassignedWithServiceDiscount)
                },
                deltas = new
                {
                    transferCashScaled = result.ProductionOpportunityDelta
                        .TotalCivilizationCashEquivalentScaled.ToString(),
                    serviceCashScaled = result.LogisticsServiceDelta
                        .TotalCivilizationCashEquivalentScaled.ToString(),
                    overallCashScaled = result.CombinedDelta
                        .TotalCivilizationCashEquivalentScaled.ToString(),
                    goodsLostMicro = comparison.LostProductionGoodsMicro.ToString(),
                    requiredServiceToBreakEvenScaled =
                        result.RequiredServiceGainToBreakEvenScaled.ToString(),
                    actualServiceCashScaled =
                        result.ActualServiceCashGainScaled.ToString(),
                    breakEvenGapScaled = result.RemainingBreakEvenGapScaled.ToString(),
                    classification = result.CombinedCashOutcome.ToString()
                }
            };
            string jsonPath = Path.Combine(reportDirectory,
                "r156-economic-diagnostic.json");
            File.WriteAllText(jsonPath,
                System.Text.Json.JsonSerializer.Serialize(json,
                    new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true }) + Environment.NewLine);

            var a = comparison.AllProducingBaseline;
            var b = comparison.ReassignedWithoutServiceDiscount;
            var c = comparison.ReassignedWithServiceDiscount;
            string markdown =
                "# GA-Next R156 — Diagnóstico econômico real (pré-commit)\n\n" +
                "Cenário determinístico: seed 20261012, 3 trabalhadores por civilização; " +
                $"trabalhador #{scenario.Reassigned.Id.Value} realocado para LAND.\n\n" +
                "| Métrica | A: todos produzindo | B: realocado sem benefício | C: realocado com serviço |\n" +
                "|---|---:|---:|---:|\n" +
                $"| Mercadorias (micro) | {a.ProducedAndSoldGoodsMicro} | {b.ProducedAndSoldGoodsMicro} | {c.ProducedAndSoldGoodsMicro} |\n" +
                $"| Entregas | {a.DeliveryCount} | {b.DeliveryCount} | {c.DeliveryCount} |\n" +
                $"| Arestas usadas | {a.UsedPhysicalCrossings} | {b.UsedPhysicalCrossings} | {c.UsedPhysicalCrossings} |\n" +
                $"| Tráfego em arestas (micro) | {a.TotalEdgeTraversalCargoMicro} | {b.TotalEdgeTraversalCargoMicro} | {c.TotalEdgeTraversalCargoMicro} |\n" +
                $"| Receita bruta (moeda) | {Money(a.GrossSalesScaled)} | {Money(b.GrossSalesScaled)} | {Money(c.GrossSalesScaled)} |\n" +
                $"| Frete físico (moeda) | {Money(a.PhysicalFreightScaled)} | {Money(b.PhysicalFreightScaled)} | {Money(c.PhysicalFreightScaled)} |\n" +
                $"| Crédito conjunto (moeda) | {Money(a.GrossSalesScaled - a.PhysicalFreightScaled)} | {Money(b.GrossSalesScaled - b.PhysicalFreightScaled)} | {Money(c.GrossSalesScaled - c.PhysicalFreightScaled)} |\n\n" +
                "## Decomposição causal\n\n" +
                $"* A → B (custo de realocar): **{Money(result.ProductionOpportunityDelta.TotalCivilizationCashEquivalentScaled)}** moeda.\n" +
                $"* B → C (benefício do serviço): **{Money(result.LogisticsServiceDelta.TotalCivilizationCashEquivalentScaled)}** moeda.\n" +
                $"* A → C (saldo combinado): **{Money(result.CombinedDelta.TotalCivilizationCashEquivalentScaled)}** moeda — **{result.CombinedCashOutcome}**.\n" +
                $"* Produção perdida: **{comparison.LostProductionGoodsMicro}** micro-mercadorias.\n" +
                $"* Benefício necessário para equilíbrio: **{Money(result.RequiredServiceGainToBreakEvenScaled)}** moeda.\n" +
                $"* Lacuna remanescente: **{Money(result.RemainingBreakEvenGapScaled)}** moeda.\n\n" +
                "A referência monetária é exata: valores escalados estão no arquivo JSON. " +
                "Sem ajuste de parâmetros, sem liquidação, sem modificação de tesouro. " +
                "O resultado é **agregado das civilizações**, não recomendação por cidade. " +
                "Realocação é diferente de nascimento: crescimento populacional não foi simulado.\n";
            File.WriteAllText(Path.Combine(reportDirectory,
                "r156-economic-diagnostic.md"), markdown);
            _output.WriteLine("GA_NEXT_R156_REPORT_PASS " + jsonPath);
        }
    }

    [Fact]
    public void TradePreservingLogisticsComparisonMeasuresRealUsedCrossings()
    {
        // i=1 produces WOOD. Reallocating i=0 (GRAIN) in R156 made
        // both civilizations self-sufficient and eliminated ALL trade.
        // Changing the candidate, not the market coefficients, must
        // preserve commercial flows and expose genuine route overlap.
        var scenario = Scenario(humanWorkerOrdinal: 1);
        Assert.Equal(Wood, scenario.Reassigned.Commodity);

        var comparison = Compare(scenario.Before, scenario.After,
            scenario.Incidence, scenario.Geography);
        var assessment = FoundationOperationalEconomicAssessmentProjector
            .Evaluate(comparison);

        Assert.True(comparison.ReassignedWithoutServiceDiscount
            .UsedPhysicalCrossings > 0,
            "A logistics counterfactual requires REAL trade after reassignment.");
        Assert.True(comparison.ReassignedWithoutServiceDiscount
            .TotalEdgeTraversalCargoMicro > 0,
            "Goods must traverse real physical crossings.");
        Assert.Equal(0, comparison.AllProducingBaseline.UsedOperationalCrossings);
        Assert.True(comparison.ReassignedWithoutServiceDiscount
            .UsedOperationalCrossings > 0,
            "Real workers must operate at least one commercially USED edge.");
        Assert.True(comparison.ReassignedWithServiceDiscount
            .UsedOperationalCrossings > 0,
            "The shadow economic result must include actual operated trade.");
        Assert.True(assessment.LogisticsServiceDelta
            .TotalCivilizationCashEquivalentScaled > BigInteger.Zero,
            "This fixed seed has a measurable, non-forced benefit from real work.");
        Assert.Equal(comparison.ReassignedWithoutServiceDiscount
                .ProducedAndSoldGoodsMicro,
            comparison.ReassignedWithServiceDiscount.ProducedAndSoldGoodsMicro);
        Assert.Equal(new BigInteger(scenario.Reassigned.OutputMicroPerTurn),
            comparison.LostProductionGoodsMicro);

        string? folder = Environment.GetEnvironmentVariable("GA_R157_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            var moneyScale = FoundationPhysicalEconomicAccountingProposal.MoneyScale;
            var a = comparison.AllProducingBaseline;
            var b = comparison.ReassignedWithoutServiceDiscount;
            var c = comparison.ReassignedWithServiceDiscount;
            object Reading(FoundationOperationalEconomicScenarioReading item) => new
            {
                goodsMicro = item.ProducedAndSoldGoodsMicro,
                deliveries = item.DeliveryCount,
                tradeCrossings = item.UsedPhysicalCrossings,
                tradedAndOperatedCrossings = item.UsedOperationalCrossings,
                traversalCargoMicro = item.TotalEdgeTraversalCargoMicro,
                grossScaled = item.GrossSalesScaled.ToString(),
                freightScaled = item.PhysicalFreightScaled.ToString(),
                collectiveCashScaled =
                    (item.GrossSalesScaled - item.PhysicalFreightScaled).ToString()
            };
            var report = new
            {
                schema = "ga-next-r157-trade-preserving-logistics-v1",
                source = "FoundationOperationalEconomicCounterfactualTests",
                worldSeed = 20261012UL,
                targetWorkerId = scenario.Reassigned.Id.Value,
                targetCommodity = scenario.Reassigned.Commodity.Value,
                workerOperation = "LandTransport",
                capitalPolicy = "same-authoritative-infrastructure-in-A-B-C",
                stateCommitted = false,
                moneyScale = moneyScale.ToString(),
                A = Reading(a),
                B = Reading(b),
                C = Reading(c),
                transferCashDeltaScaled = assessment.ProductionOpportunityDelta
                    .TotalCivilizationCashEquivalentScaled.ToString(),
                logisticsCashDeltaScaled = assessment.LogisticsServiceDelta
                    .TotalCivilizationCashEquivalentScaled.ToString(),
                combinedCashDeltaScaled = assessment.CombinedDelta
                    .TotalCivilizationCashEquivalentScaled.ToString(),
                effectiveServiceMicro =
                    comparison.CommittedOperationalService.TotalServiceMicro.ToString(),
                activeServiceCrossings = comparison.FreightShadow.EdgeReadings
                    .Count(edge => edge.OperationalServiceMicro > 0UL),
                discountedPhysicalCrossings = comparison.FreightShadow.EdgeReadings
                    .Count(edge => edge.ShadowPhysicalCostMicroPerWholeCargoUnit <
                        edge.OriginalPhysicalCostMicroPerWholeCargoUnit),
                outcome = assessment.CombinedCashOutcome.ToString()
            };
            string path = Path.Combine(folder, "r157-trade-preserving.json");
            File.WriteAllText(path,
                System.Text.Json.JsonSerializer.Serialize(report,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                + Environment.NewLine);
            _output.WriteLine("GA_NEXT_R157_ECONOMIC_REPORT_PASS " + path);
        }
    }

    [Fact]
    public void RealMarginalTransferRankingSharesOnePhysicalBaselineAndMeasuresCost()
    {
        var fixture = Scenario();
        var selected = fixture.Before.Workers.Workers
            .Where(w => w.Owner == Human &&
                w.Activity == PhysicalWorkerActivity.Producing)
            .OrderBy(w => w.Id.Value).Take(2)
            .Select(w => w.Id).ToArray();
        var baselineHash = new CanonicalWorldStateHasher()
            .Compute(fixture.Before);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var diagnostics = FoundationRealMarginalTransferEvaluator.Evaluate(
            fixture.Before, fixture.Incidence, fixture.Geography,
            MarketPolicy(),
            new FoundationOperationalProductivityPolicy(
                new FoundationOperationalProductivityBand(
                    1_000_000UL, 10_000U),
                new FoundationOperationalProductivityBand(
                    1_000_000UL, 10_000U)),
            new FoundationOperationalFreightShadowPolicy(
                1_000_000UL, 1_000_000UL),
            selected, PhysicalWorkerOccupation.LandTransport,
            evaluationBudget: 2, topCount: 2);
        clock.Stop();

        Assert.False(diagnostics.IsCommitted);
        Assert.Equal(2, diagnostics.Evaluated.Count);
        Assert.Equal(6, diagnostics.CompleteMarketSolutions);
        Assert.Equal(2, diagnostics.Ranking.EvaluatedCandidates);
        Assert.Equal(FoundationMarginalLaborChangeKind.ExistingWorkerTransfer,
            diagnostics.Ranking.ChangeKind);
        Assert.Equal(diagnostics.Evaluated[0].BaselineIdentity,
            diagnostics.Evaluated[1].BaselineIdentity);
        Assert.Equal(baselineHash, new CanonicalWorldStateHasher()
            .Compute(fixture.Before));
        Assert.Equal(selected, fixture.Before.Workers.Workers
            .Where(w => w.Owner == Human &&
                w.Activity == PhysicalWorkerActivity.Producing)
            .OrderBy(w => w.Id.Value).Take(2).Select(w => w.Id));
        Assert.All(diagnostics.Evaluated, item =>
        {
            Assert.Equal(Human, item.Owner);
            Assert.Equal(FoundationMarginalLaborChangeKind.ExistingWorkerTransfer,
                item.ChangeKind);
            Assert.Equal(item.DestinationTile, item.SourceTile);
        });
        Assert.Equal(diagnostics.Evaluated
                .OrderBy(x => x.CashDeltaScaled)
                .Select(x => x.ExistingWorkerId),
            diagnostics.Ranking.LowestReturns
                .Select(x => x.ExistingWorkerId));
        Assert.Throws<ArgumentException>(() =>
            FoundationRealMarginalTransferEvaluator.Evaluate(
                fixture.Before, fixture.Incidence, fixture.Geography,
                MarketPolicy(),
                new FoundationOperationalProductivityPolicy(
                    new FoundationOperationalProductivityBand(
                        1_000_000UL, 10_000U),
                    new FoundationOperationalProductivityBand(
                        1_000_000UL, 10_000U)),
                new FoundationOperationalFreightShadowPolicy(
                    1_000_000UL, 1_000_000UL),
                selected, PhysicalWorkerOccupation.LandTransport,
                evaluationBudget: 1, topCount: 2));

        string? folder = Environment.GetEnvironmentVariable("GA_R159_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            var payload = new
            {
                schema = "ga-next-r159-real-marginal-transfer-v1",
                source = "one-identical-canonical-world-baseline",
                baseline = diagnostics.Ranking.BaselineIdentity,
                testedCandidates = diagnostics.Evaluated.Count,
                completeMarketSolutions = diagnostics.CompleteMarketSolutions,
                elapsedMilliseconds = clock.ElapsedMilliseconds,
                moneyScale =
                    FoundationPhysicalEconomicAccountingProposal.MoneyScale.ToString(),
                outcomeBasis = "collective-gross-less-physical-freight",
                scope = "existing-workers-in-place-only",
                workerAdditionsEvaluated = false,
                migrationEvaluated = false,
                committed = false,
                decisions = diagnostics.Evaluated.Select(x => new
                {
                    workerId = x.ExistingWorkerId!.Value.Value,
                    tileId = x.DestinationTile.FineStrategicCellId.Value,
                    occupation = x.DestinationOccupation.ToString(),
                    cashDeltaScaled = x.CashDeltaScaled.ToString()
                })
            };
            string path = Path.Combine(folder, "r159-real-marginal.json");
            File.WriteAllText(path,
                System.Text.Json.JsonSerializer.Serialize(payload,
                    new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true }) + Environment.NewLine);
            _output.WriteLine("GA_NEXT_R159_REAL_MARGINAL_PASS " + path);
        }
    }

    [Fact]
    public void CurrentProductionMarginalReturnKeepsWorkerDemandAndComparesRealMarkets()
    {
        var fixture = Scenario();
        var ids = fixture.Before.Workers.Workers
            .Where(w => w.Owner == Human &&
                w.Activity == PhysicalWorkerActivity.Producing)
            .OrderBy(w => w.Id.Value).Take(2)
            .Select(w => w.Id).ToArray();
        var originalHash = new CanonicalWorldStateHasher()
            .Compute(fixture.Before);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var report = FoundationCurrentWorkMarginalProjector.Evaluate(
            fixture.Before, fixture.Incidence, fixture.Geography,
            MarketPolicy(), ids, evaluationBudget: 2, topCount: 2);
        watch.Stop();

        Assert.False(report.IsCommitted);
        Assert.Equal(3, report.CompleteMarketSolutions);
        Assert.Equal(2, report.AllReadings.Count);
        Assert.All(report.AllReadings, r =>
        {
            Assert.Equal(Human, r.Owner);
            Assert.Equal(1_000_000UL, r.LostProductionMicro);
            Assert.Equal(fixture.Reassigned.TacticalTileId, r.Tile);
        });
        Assert.Equal(report.AllReadings
                .OrderBy(x => x.ContributionScaled)
                .Select(x => x.Worker),
            report.LowestContributions.Select(x => x.Worker));
        Assert.Equal(originalHash, new CanonicalWorldStateHasher()
            .Compute(fixture.Before));
        Assert.All(fixture.Before.Workers.Workers,
            w => Assert.NotEqual(PhysicalWorkerOccupation.Unassigned,
                w.Occupation));
        Assert.Throws<ArgumentException>(() =>
            FoundationCurrentWorkMarginalProjector.Evaluate(
                fixture.Before, fixture.Incidence, fixture.Geography,
                MarketPolicy(), ids, evaluationBudget: 1, topCount: 2));

        var folder = Environment.GetEnvironmentVariable("GA_R160_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            var payload = new
            {
                schema = "ga-next-r160-current-work-marginal-v1",
                baseline = report.BaselineIdentity,
                populationUnchanged = true,
                consumptionUnchanged = true,
                stateCommitted = report.IsCommitted,
                moneyScale =
                    FoundationPhysicalEconomicAccountingProposal.MoneyScale.ToString(),
                completeMarketSolutions = report.CompleteMarketSolutions,
                elapsedMilliseconds = watch.ElapsedMilliseconds,
                metric = "baseline_collective_cash_minus_same_population_one_worker_inactive",
                candidates = report.AllReadings.Select(r => new
                {
                    workerId = r.Worker.Value,
                    ownerId = r.Owner.Value,
                    tileId = r.Tile.FineStrategicCellId.Value,
                    occupation = r.CurrentOccupation.ToString(),
                    commodity = r.Commodity.Value,
                    outputMicro = r.LostProductionMicro,
                    contributionScaled = r.ContributionScaled.ToString()
                }),
                lowestWorkerId = report.LowestContributions[0].Worker.Value,
                highestWorkerId = report.HighestContributions[0].Worker.Value
            };
            string path = Path.Combine(folder, "r160-current-work-marginal.json");
            File.WriteAllText(path,
                System.Text.Json.JsonSerializer.Serialize(payload,
                    new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true }) + Environment.NewLine);
            _output.WriteLine("GA_NEXT_R160_CURRENT_WORK_PASS " + path);
        }
    }

    [Fact]
    public void AddingOneRealWorkerChangesBothSupplyAndConsumptionInTheSameMarket()
    {
        var fixture = Scenario();
        var baselineHash = new CanonicalWorldStateHasher()
            .Compute(fixture.Before);
        var originWorkers = fixture.Before.Workers.Workers
            .Where(w => w.Owner == Human &&
                w.Activity == PhysicalWorkerActivity.Producing)
            .OrderBy(w => w.Id.Value).Take(2).ToArray();
        var proposals = originWorkers
            .Select(w => new FoundationNewWorkerProductiveOpportunity(w.Id))
            .ToArray();

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var result = FoundationNewWorkerProductiveMarginalProjector.Evaluate(
            fixture.Before, fixture.Incidence, fixture.Geography,
            MarketPolicy(), proposals, evaluationBudget: 2, topCount: 2);
        timer.Stop();

        Assert.False(result.IsCommitted);
        Assert.Equal(3, result.CompleteMarketSolutions);
        Assert.Equal(2, result.Evaluated.Count);
        Assert.Equal(FoundationMarginalLaborChangeKind.NewWorkerAddition,
            result.Ranking.ChangeKind);
        Assert.Equal(2, result.Ranking.EvaluatedCandidates);
        Assert.All(result.Ranking.HighestOpportunities,
            x => { Assert.Null(x.ExistingWorkerId); Assert.Null(x.SourceTile); });
        Assert.All(result.Evaluated, x =>
        {
            Assert.Equal(Human, x.Owner);
            Assert.Equal(1_000_000UL, x.AddedProductionMicro);
            Assert.Equal(2_000_000UL, x.AddedDemandMicro);
            Assert.Equal(originWorkers[0].TacticalTileId, x.Tile);
        });
        Assert.Equal(result.Evaluated
                .OrderByDescending(x => x.CollectiveCashGainScaled)
                .Select(x => x.CollectiveCashGainScaled),
            result.Ranking.HighestOpportunities.Select(x => x.CashDeltaScaled));

        // A real newborn remains a buyer; no existing employee is repurposed.
        // Demand totals are observed from the existing authoritative workforce
        // pricing, not inferred from a producer's supply alone.
        var first = originWorkers[0];
        var newWorker = new PhysicalWorkerRuntimeRecord(
            new PhysicalWorkerId(fixture.Before.Workers.Workers
                .Max(w => w.Id.Value) + 1UL),
            first.Owner, first.CellId, first.Commodity,
            first.OutputMicroPerTurn, PhysicalWorkerActivity.Producing,
            first.DemandMultiplierMicro, first.TacticalTileId, first.Occupation);
        var originalDemand = FoundationPhysicalCommodityDemandProjector
            .Project(fixture.Before.Workers, MarketPolicy().Demand);
        var expanded = new PhysicalWorkerRuntimeState(
            fixture.Before.Workers.Workers.Append(newWorker),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var expandedDemand = FoundationPhysicalCommodityDemandProjector
            .Project(expanded, MarketPolicy().Demand);
        Assert.Equal(fixture.Before.Workers.Workers.Count + 1,
            expanded.Workers.Count);
        Assert.Equal(originalDemand.DistinctWorkers + 1,
            expandedDemand.DistinctWorkers);
        Assert.Equal(2_000_000UL,
            expandedDemand.TotalCommodityDemandMicro -
            originalDemand.TotalCommodityDemandMicro);
        Assert.Equal(1_000_000UL,
            expandedDemand.TotalActiveCapacityMicroPerTurn -
            originalDemand.TotalActiveCapacityMicroPerTurn);
        Assert.Equal(baselineHash, new CanonicalWorldStateHasher()
            .Compute(fixture.Before));
        Assert.Throws<ArgumentException>(() =>
            FoundationNewWorkerProductiveMarginalProjector.Evaluate(
                fixture.Before, fixture.Incidence, fixture.Geography,
                MarketPolicy(), proposals, evaluationBudget: 1, topCount: 2));
        Assert.Throws<ArgumentException>(() =>
            FoundationNewWorkerProductiveMarginalProjector.Evaluate(
                fixture.Before, fixture.Incidence, fixture.Geography,
                MarketPolicy(), new[] { proposals[0], proposals[0] },
                evaluationBudget: 2, topCount: 2));

        string? folder = Environment.GetEnvironmentVariable("GA_R161_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            var payload = new
            {
                schema = "ga-next-r161-new-worker-marginal-v1",
                baseline = result.BaselineIdentity,
                oldPopulation = fixture.Before.Workers.Workers.Count,
                proposedPopulation = fixture.Before.Workers.Workers.Count + 1,
                oneNewWorkerPerAlternative = true,
                demandAdded = true,
                newProductionAdded = true,
                noExistingWorkerRemoved = true,
                authoritativeStateCommitted = false,
                moneyScale =
                    FoundationPhysicalEconomicAccountingProposal.MoneyScale.ToString(),
                elapsedMilliseconds = timer.ElapsedMilliseconds,
                marketSolutions = result.CompleteMarketSolutions,
                scenario = "existing-node-existing-productive-archetype",
                candidates = result.Evaluated.Select(x => new
                {
                    sourceArchetypeId = x.ArchetypeWorker.Value,
                    ownerId = x.Owner.Value,
                    tileId = x.Tile.FineStrategicCellId.Value,
                    occupation = x.Occupation.ToString(),
                    commodity = x.Commodity.Value,
                    addedProductionMicro = x.AddedProductionMicro,
                    addedConsumptionDemandMicro = x.AddedDemandMicro,
                    collectiveCashGainScaled = x.CollectiveCashGainScaled.ToString()
                })
            };
            string path = Path.Combine(folder, "r161-new-worker-marginal.json");
            File.WriteAllText(path,
                System.Text.Json.JsonSerializer.Serialize(payload,
                    new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true }) + Environment.NewLine);
            _output.WriteLine("GA_NEXT_R161_NEW_WORKER_REPORT " + path);
        }
    }

    [Fact]
    public void CounterfactualRejectsUnchangedWorkersAndTamperedLocations()
    {
        var s = Scenario();
        Assert.Throws<ArgumentException>(() =>
            Compare(s.Before, s.Before, s.Incidence, s.Geography));

        var relocated = s.After.Workers.Workers.Select(worker =>
            worker.Id == s.Reassigned.Id
                ? worker.WithTacticalLocationAndActivity(
                    new PhysicalTacticalTileId(
                        worker.TacticalTileId!.Value.FineGoldbergParameters,
                        new StrategicCellId(251UL)),
                    worker.CellId, PhysicalWorkerActivity.Other)
                : worker).ToArray();
        var altered = s.After.WithWorkers(new PhysicalWorkerRuntimeState(
            relocated, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true));
        Assert.Throws<ArgumentException>(() =>
            Compare(s.Before, altered, s.Incidence, s.Geography));
    }
}
