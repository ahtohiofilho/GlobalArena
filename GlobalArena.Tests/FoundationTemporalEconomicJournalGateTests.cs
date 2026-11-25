using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationTemporalEconomicJournalGateTests
{
    private static (WorldState World, PhysicalWorkerRuntimeRecord Worker,
        PhysicalTacticalTileId Destination,
        PhysicalTacticalWorkerMoveEventExecutor Validator,
        PhysicalTacticalIncidenceMap Incidence,
        FoundationFineFirstPhysicalGeography Fine) Fixture()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
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
        var seeded = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            demo.Session.GeneratedWorld, demo.Incidence, fine,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var bound = demo.Session.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, demo.Session.GeneratedWorld, demo.Incidence);
        return (bound, worker, adjacent, validator, demo.Incidence, fine);
    }



    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R2271TemporalHostPublishesActualLocalOrRemoteTrade(bool remote)
    {
        var (world, worker, destination, _, incidence, _) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        // Explicit opening fixture: supply at one node, consumers either
        // at that node or across a real adjacent crossing. No fabricated routes.
        var node = worker.TacticalTileId!.Value;
        var foreign = world.Civilizations.Civilizations.Single(c => c != worker.Owner);
        var remoteRegion = incidence.TileIncidences[
            (int)destination.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
        var census = world.Workers.Workers.Select(w =>
            w.WithTacticalLocationAndActivity(
                remote && w.Owner == foreign ? destination : node,
                remote && w.Owner == foreign ? remoteRegion : worker.CellId,
                w.Owner == foreign ? PhysicalWorkerActivity.Other : PhysicalWorkerActivity.Producing));
        var prepared = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.WithWorkers(new PhysicalWorkerRuntimeState(census,
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true))
                .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                    world.Civilizations.Civilizations.Select(c => (c, 0UL)))));
        demo.Session.AcceptSnapshot(prepared);
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
            world.Civilizations.Civilizations,
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
            enableMonetaryInfrastructure: true, settlementPolicy: Policy(world.Civilizations.Civilizations),
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(worker.Owner).Availability);
        host.SetHumanReady(worker.Owner);
        Assert.Null(host.TryCloseTurn());
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(worker.Owner).Availability);
        host.SetHumanReady(foreign);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        var sale = Assert.IsType<FoundationTemporalSalesSettledEvent>(receipt.TemporalEconomicSettlement);
        Assert.True(sale.GrossSalesScaled > BigInteger.Zero);
        foreach (var civ in world.Civilizations.Civilizations)
        {
            var view = host.GetLastClosedEconomicTradeView(civ);
            Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset, view.Availability);
            Assert.Equal((ulong?)1UL, view.EconomicCycleOrdinal);
            Assert.Equal((WorldStateHash?)receipt.Hash, view.PublishedHash);
            Assert.Equal(sale.AuditedParticipantEdges![civ].ToArray(), view.UsedEdges.ToArray());
            if (remote) Assert.NotEmpty(view.UsedEdges);
            else Assert.Empty(view.UsedEdges);
        }
        Assert.Throws<InvalidOperationException>(() =>
            host.GetLastClosedEconomicTradeView(new CivilizationId(uint.MaxValue)));
        Assert.Equal(receipt.Hash, host.StateHash);
    }

    [Fact]
    public void R2281MarginalRevenueUsesCanonicalMarketAndPreservesOpening()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var items = crops.Values.Concat(opening.Workers.Workers.Select(w => w.Commodity)).Distinct().ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2281", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64);
        var calendar = opening.Workers.Workers.Select(w => new FoundationPersistentWorkerSlice(
            w.Id, w.TacticalTileId!.Value, 0U, 1_000_000U,
            PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)).ToArray();
        var hasher = new CanonicalWorldStateHasher();
        var before = hasher.Compute(opening);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var measured = FoundationBaseMarginalRevenueEvaluator.EvaluateWithDiagnostics(
            opening, incidence, fine, policy, calendar, worker.Id, 0U, agriculture);
        timer.Stop();
        var rows = measured.Candidates;
        Assert.NotEmpty(rows);
        Assert.Equal(rows.Count + 1, measured.FinancialEvaluations);
        Console.WriteLine($"R2282_REFERENCE crops/native workers={calendar.Length} candidates={rows.Count} financial_evaluations={measured.FinancialEvaluations} elapsed_ms={timer.ElapsedMilliseconds}");
        Assert.Equal(rows.OrderByDescending(x => x.MarginalCreditScaled).ThenBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray(), rows.ToArray());
        var repeated = FoundationBaseMarginalRevenueEvaluator.Evaluate(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), worker.Id, 0U, agriculture);
        Assert.Equal(rows.ToArray(), repeated.ToArray());
        var eligible = crops.Keys.Where(c => agriculture.Resolve(worker.TacticalTileId!.Value, c).WholeTurnOutput > 0)
            .Select(c => (byte)c).OrderBy(c => c).ToArray();
        Assert.Equal(eligible, rows.Where(x => x.Crop.HasValue).Select(x => x.Crop!.Value).OrderBy(c => c).ToArray());
        foreach (var row in rows)
        {
            Assert.Equal(1_000_000U, row.EffortMicro);
            Assert.Equal(BigInteger.Zero, row.BaselineCreditScaled);
            Assert.True(row.MarginalCreditScaled > 0);
        }
        Assert.Equal(before, hasher.Compute(opening));
        Assert.Equal(0UL, opening.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.All(calendar, x => Assert.Equal(PhysicalWorkerOccupation.Unassigned, x.Occupation));
        var busy = calendar.Select(x => x.Worker == worker.Id ? x with
            { Activity = PhysicalWorkerActivity.Producing, Occupation = PhysicalWorkerOccupation.Agriculture, Crop = eligible[0] } : x).ToArray();
        Assert.Throws<InvalidOperationException>(() => FoundationBaseMarginalRevenueEvaluator.Evaluate(
            opening, incidence, fine, policy, busy, worker.Id, 0U, agriculture));

        // A real producer and the remaining idle workers define one complete
        // reference calendar. Split the target's interval to verify that
        // operational effort is fractional rather than a whole worker.
        var producer = opening.Workers.Workers.First(w => w.Id != worker.Id && w.Owner == worker.Owner);
        var partial = calendar.Where(s => s.Worker != worker.Id)
            .Select(s => s.Worker == producer.Id ? s with
            { Activity = PhysicalWorkerActivity.Producing, Occupation = producer.Occupation!.Value } : s)
            .Concat(new[]
            {
                new FoundationPersistentWorkerSlice(worker.Id, worker.TacticalTileId!.Value, 0U, 600_000U,
                    PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, 1UL),
                new FoundationPersistentWorkerSlice(worker.Id, worker.TacticalTileId!.Value, 600_000U, 400_000U,
                    PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)
            }).ToArray();
        var operations = new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL);
        timer.Restart();
        var reference = FoundationBaseMarginalRevenueEvaluator.EvaluateWithDiagnostics(
            opening, incidence, fine, policy, partial, worker.Id, 600_000U, agriculture, operations);
        timer.Stop();
        Assert.Equal(reference.Candidates.Count + 1, reference.FinancialEvaluations);
        Console.WriteLine($"R2282_REFERENCE operational workers={calendar.Length} candidates={reference.Candidates.Count} financial_evaluations={reference.FinancialEvaluations} elapsed_ms={timer.ElapsedMilliseconds}");
        var land = Assert.Single(reference.Candidates, x => x.Occupation == PhysicalWorkerOccupation.LandTransport);
        Assert.Equal(400_000U, land.EffortMicro);
        Assert.Null(land.Crop);
        int nodeIndex = (int)worker.TacticalTileId!.Value.FineStrategicCellId.Value - 1;
        bool portEligible = fine.FineElevationRaw[nodeIndex] > fine.SeaLevelRaw &&
            incidence.FineTopology.Edges.Any(e =>
                e.IncidentCellIds.Contains(worker.TacticalTileId.Value.FineStrategicCellId) &&
                fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                    (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Port);
        Assert.Equal(portEligible, reference.Candidates.Any(x => x.Occupation == PhysicalWorkerOccupation.PortOperations));

        // Independent direct use of the canonical reconciler, not an invented
        // per-edge tariff or a benefit estimated from global freight savings.
        FoundationTemporalFinancialReconciliation Reconcile(IReadOnlyList<FoundationPersistentWorkerSlice> input) =>
            FoundationTemporalFinancialReconciler.Evaluate(opening, incidence,
                FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, input, policy.Demand, agriculture),
                policy, fine, opening, operations);
        BigInteger OwnerCredit(FoundationTemporalFinancialReconciliation f)
        {
            var account = f.Accounting.Accounts.Single(a => a.Civilization == worker.Owner);
            return account.SellerNetScaled + account.RevenueTaxScaled + account.TollIncomeScaled;
        }
        var baseline = Reconcile(partial);
        Assert.Equal(OwnerCredit(baseline), land.BaselineCreditScaled);
        var frozen = FoundationFrozenMarginalRevenueEstimator.Evaluate(opening, incidence, fine, policy,
            partial, new[] { new FoundationBaseMarginalInterval(worker.Id, 600_000U) }, agriculture, operations);
        Assert.Equal(1, frozen.FinancialEvaluations);
        Assert.Equal(reference.Candidates.Select(x => (x.Occupation, x.Crop)).OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray(),
            frozen.Intervals.Single().Candidates.Select(x => (x.Occupation, x.Crop)).OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray());
        Assert.All(frozen.Intervals.Single().Candidates, x => Assert.Equal(400_000U, x.EffortMicro));
        foreach (var candidate in reference.Candidates.Where(x =>
            x.Occupation is PhysicalWorkerOccupation.LandTransport or PhysicalWorkerOccupation.PortOperations))
        {
            var modified = partial.Select(s => s.Worker == worker.Id && s.StartMicro == 600_000U
                ? s with { Occupation = candidate.Occupation } : s).ToArray();
            var direct = Reconcile(modified);
            Assert.Equal(OwnerCredit(direct), candidate.CandidateCreditScaled);
            var quotes = Assert.IsAssignableFrom<IReadOnlyList<FoundationOperationalFreightShadowEdge>>(direct.OperationalQuotes);
            Assert.NotEmpty(quotes);
            Assert.Equal(400_000UL, quotes.Aggregate(0UL, (sum, q) => checked(sum + q.OperationalServiceMicro)));
            Assert.All(quotes, q => Assert.True(q.ShadowPhysicalCostMicroPerWholeCargoUnit <= q.OriginalPhysicalCostMicroPerWholeCargoUnit));
        }
        Assert.Equal(before, hasher.Compute(opening));
        Assert.All(partial.Where(s => s.Worker == worker.Id), s => Assert.Equal(PhysicalWorkerOccupation.Unassigned, s.Occupation));
        Assert.Equal(0UL, opening.Treasury.LastSettledEconomicCycleOrdinal);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    public void R2283FrozenBatchMatchesReferenceAndReusesOnlyEquivalentFullIdleWorkers(int workersPerCivilization)
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: workersPerCivilization);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        // Explicit homogeneous opening within each owner, not between owners.
        // Keep individual IDs and complete queue census. This exposes the
        // concentrated-opening reuse opportunity without changing live rules.
        var people = opening.Workers.Workers;
        var representatives = people.GroupBy(w => w.Owner).ToDictionary(g => g.Key, g => g.First());
        var homogeneous = people.Select(w =>
        {
            var template = representatives[w.Owner];
            return new PhysicalWorkerRuntimeRecord(w.Id, w.Owner, template.CellId,
                template.Commodity, template.OutputMicroPerTurn, template.Activity,
                template.DemandMultiplierMicro, template.TacticalTileId, template.Occupation);
        }).ToArray();
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(homogeneous,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var items = crops.Values.Concat(homogeneous.Select(w => w.Commodity)).Distinct().ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c,
                    profile == FoundationPhysicalDemandProfile.AgricultureProduction ? 1_300_000UL : 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2283", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64);
        var operations = new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL);
        var calendar = homogeneous.Select(w => new FoundationPersistentWorkerSlice(
            w.Id, w.TacticalTileId!.Value, 0U, 1_000_000U,
            PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)).ToArray();
        var requests = calendar.Select(s => new FoundationBaseMarginalInterval(s.Worker, s.StartMicro)).ToArray();
        var before = new CanonicalWorldStateHasher().Compute(opening);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var batch = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), requests.Reverse().ToArray(), agriculture, operations);
        timer.Stop();
        Assert.Equal(requests, batch.Intervals.Select(x => x.Interval).ToArray());
        Assert.True(batch.EquivalentIntervalsReused > 0);
        var directEvaluations = 0;
        var separateTimer = System.Diagnostics.Stopwatch.StartNew();
        foreach (var entry in batch.Intervals)
        {
            var direct = FoundationBaseMarginalRevenueEvaluator.EvaluateWithDiagnostics(opening, incidence, fine, policy,
                calendar, entry.Interval.Worker, entry.Interval.StartMicro, agriculture, operations);
            Assert.Equal(direct.Candidates.ToArray(), entry.Candidates.ToArray());
            directEvaluations += direct.FinancialEvaluations;
        }
        separateTimer.Stop();
        Assert.True(batch.FinancialEvaluations < directEvaluations);
        Console.WriteLine($"R2283_BATCH workers={homogeneous.Length} reused={batch.EquivalentIntervalsReused} batch_evaluations={batch.FinancialEvaluations} separate_evaluations={directEvaluations} batch_ms={timer.ElapsedMilliseconds} separate_ms={separateTimer.ElapsedMilliseconds}");
        // Split two equivalent workers into partial intervals; they must
        // remain independently evaluated even if the economic rows match.
        var twins = homogeneous.Where(w => w.Owner == homogeneous[0].Owner).Take(2).Select(w => w.Id).ToHashSet();
        var partial = calendar.SelectMany(s => twins.Contains(s.Worker)
            ? new[] { s with { DurationMicro = 600_000U }, s with { StartMicro = 600_000U, DurationMicro = 400_000U } }
            : new[] { s }).ToArray();
        var partialRequests = twins.OrderBy(id => id.Value)
            .Select(id => new FoundationBaseMarginalInterval(id, 600_000U)).ToArray();
        var partialBatch = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            partial, partialRequests, agriculture, operations);
        Assert.Equal(0, partialBatch.EquivalentIntervalsReused);
        foreach (var entry in partialBatch.Intervals)
        {
            var direct = FoundationBaseMarginalRevenueEvaluator.Evaluate(opening, incidence, fine, policy,
                partial, entry.Interval.Worker, entry.Interval.StartMicro, agriculture, operations);
            Assert.Equal(direct.ToArray(), entry.Candidates.ToArray());
        }
        Assert.Throws<ArgumentException>(() => FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(
            opening, incidence, fine, policy, calendar, new[] { requests[0], requests[0] }, agriculture, operations));
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(opening));
        Assert.Equal(0UL, opening.Treasury.LastSettledEconomicCycleOrdinal);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    public void R2284BoundedReferenceMeasuresHeterogeneousWorkersWithoutPartialRankings(int workersPerCivilization)
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: workersPerCivilization);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        // Deliberately unique economic facts even at the same node.
        // Native roles/commodities remain authentic to the test scenario.
        var workers = opening.Workers.Workers.Select(w => new PhysicalWorkerRuntimeRecord(
            w.Id, w.Owner, w.CellId, w.Commodity, checked(1_000_000UL + 10_000UL * w.Id.Value),
            w.Activity, w.DemandMultiplierMicro, w.TacticalTileId, w.Occupation)).ToArray();
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var items = crops.Values.Concat(workers.Select(w => w.Commodity)).Distinct().ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2284", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64);
        var operations = new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL);
        var calendar = workers.Select(w => new FoundationPersistentWorkerSlice(
            w.Id, w.TacticalTileId!.Value, 0U, 1_000_000U,
            PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)).ToArray();
        var requests = calendar.Select(s => new FoundationBaseMarginalInterval(s.Worker, s.StartMicro)).ToArray();
        var before = new CanonicalWorldStateHasher().Compute(opening);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var exact = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            calendar, requests, agriculture, operations);
        watch.Stop();
        long exactMs = watch.ElapsedMilliseconds;
        watch.Restart();
        var estimate = FoundationFrozenMarginalRevenueEstimator.Evaluate(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), requests.Reverse().ToArray(), agriculture, operations);
        watch.Stop();
        long estimateMs = watch.ElapsedMilliseconds;
        Assert.False(estimate.IsCommitted);
        Assert.Equal(1, estimate.FinancialEvaluations);
        Assert.Equal(requests, estimate.Intervals.Select(x => x.Interval).ToArray());
        int comparable = 0, agreement = 0;
        BigInteger regret = BigInteger.Zero, opportunity = BigInteger.Zero;
        foreach (var interval in estimate.Intervals)
        {
            var reference = exact.Intervals.Single(x => x.Interval == interval.Interval).Candidates;
            Assert.Equal(reference.Select(x => (x.Occupation, x.Crop)).OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray(),
                interval.Candidates.Select(x => (x.Occupation, x.Crop)).OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray());
            if (reference.Count == 0) continue;
            comparable++;
            var first = interval.Candidates[0];
            var selected = reference.Single(x => x.Occupation == first.Occupation && x.Crop == first.Crop);
            var best = BigInteger.Max(BigInteger.Zero, reference[0].MarginalCreditScaled);
            var achieved = first.EstimatedCreditDeltaScaled > 0 ? selected.MarginalCreditScaled : BigInteger.Zero;
            if (achieved == best) agreement++;
            regret += best - achieved;
            opportunity += best;
        }
        Console.WriteLine($"R2285_FROZEN workers={workers.Length} solves={estimate.FinancialEvaluations} trees={estimate.RouteTreesBuilt} elapsed_ms={estimateMs} comparable={comparable} exact_best_matches={agreement} regret_scaled={regret} opportunity_scaled={opportunity}");
        var repeated = FoundationFrozenMarginalRevenueEstimator.Evaluate(opening, incidence, fine, policy,
            calendar, requests, agriculture, operations);
        foreach (var row in estimate.Intervals)
            Assert.Equal(row.Candidates.ToArray(), repeated.Intervals.Single(x => x.Interval == row.Interval).Candidates.ToArray());
        Assert.Equal(0, exact.EquivalentIntervalsReused);
        Assert.Equal(0, exact.DeferredIntervals);
        Assert.Equal(1 + exact.Intervals.Sum(x => x.Candidates.Count), exact.FinancialEvaluations);
        const int budget = 18;
        watch.Restart();
        var limited = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), requests.Reverse().ToArray(), agriculture, operations, budget);
        watch.Stop();
        Assert.InRange(limited.FinancialEvaluations, 1, budget);
        Assert.InRange(limited.DeferredIntervals, 1, requests.Length - 1);
        Assert.Equal(requests, limited.Intervals.Select(x => x.Interval).ToArray());
        foreach (var row in limited.Intervals)
        {
            if (row.DeferredByBudget) Assert.Empty(row.Candidates);
            else Assert.Equal(exact.Intervals.Single(x => x.Interval == row.Interval).Candidates.ToArray(),
                row.Candidates.ToArray());
        }
        Console.WriteLine($"R2284_HETEROGENEOUS workers={workers.Length} exact_solves={exact.FinancialEvaluations} exact_ms={exactMs} budget={budget} bounded_solves={limited.FinancialEvaluations} bounded_ms={watch.ElapsedMilliseconds} deferred={limited.DeferredIntervals}");
        var zero = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            calendar, requests, agriculture, operations, 0);
        Assert.Equal(0, zero.FinancialEvaluations);
        Assert.Equal(exact.Intervals.Count(x => x.Candidates.Count > 0), zero.DeferredIntervals);
        Assert.All(zero.Intervals, x => Assert.Empty(x.Candidates));
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(opening));
        Assert.Equal(0UL, opening.Treasury.LastSettledEconomicCycleOrdinal);
    }

    [Fact]
    public void R2286ExperimentalAutomaticBaseUsesOnlyAvailableSuffixAndReplaysCollectively()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(worker.TacticalTileId!.Value.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var own = opening.Workers.Workers.Where(w => w.Owner == worker.Owner).ToArray();
        var second = own.First(w => w.Id != worker.Id);
        var manual = own.Single(w => w.Id != worker.Id && w.Id != second.Id);
        // Other workers provide real demand but no independent native output.
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
            w.WithTacticalLocationAndActivity(w.TacticalTileId!.Value, w.CellId, PhysicalWorkerActivity.Other)),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2286", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64);
        var movement = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            movement, policy, fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL,
            agriculture: agriculture, enableExperimentalAutomaticBase: true);
        var disabled = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            movement, policy, fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL,
            agriculture: agriculture);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var automatic = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic);
        var manualCrop = (byte)4;
        var manualBase = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, manualCrop);
        var commands = new ISimulationCommand[]
        {
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 1UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id, FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity: automatic)),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(second.Owner, second.Id, FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity: automatic)),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 3UL),
                new FoundationPersistentOrderEdit(manual.Owner, manual.Id, FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity: manualBase)),
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 4UL), worker.Owner, edge,
                600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) })
        };
        var before = new CanonicalWorldStateHasher().Compute(opening);
        var input = new TurnResolutionInput(opening, commands, context);
        Assert.Throws<InvalidOperationException>(() => disabled.Resolve(input, opening));
        var resolved = phase.Resolve(input, opening);
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        var first = timeline.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(2, first.Length);
        Assert.Equal(600_000U, first[0].DurationMicro);
        Assert.NotNull(first[0].ProjectId);
        Assert.Equal(600_000U, first[1].StartMicro);
        Assert.Equal(400_000U, first[1].DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing, first[1].Activity);
        Assert.Null(first[1].ProjectId);
        var full = Assert.Single(timeline.Slices.Where(x => x.Worker == second.Id));
        Assert.Equal(1_000_000U, full.DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing, full.Activity);
        Assert.All(timeline.Slices.Where(x => x.Worker == manual.Id), x =>
        {
            Assert.Equal(PhysicalWorkerActivity.Producing, x.Activity);
            Assert.Equal(manualCrop, x.Crop);
        });
        Assert.All(timeline.Slices.GroupBy(x => x.Worker), g => Assert.Equal(1_000_000UL,
            g.Aggregate(0UL, (sum, x) => sum + x.DurationMicro)));
        Assert.Equal(automatic, resolved.ResultingWorldState.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).BaseActivity);
        Assert.Equal(1UL, resolved.ResultingWorldState.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(resolved.ResultingWorldState.Constructions.Projects.Single().IsActivated);
        var replay = phase.ReplayPersistentJournal(opening, resolved.EventLog, context);
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(hasher.Compute(resolved.ResultingWorldState), hasher.Compute(replay));
        Assert.Equal(before, hasher.Compute(opening));
    }

    private static FoundationActivityCommodityDemandPolicy Demand()
    {
        var items = new[] { new CommodityId(1U), new CommodityId(2U) };
        return new FoundationActivityCommodityDemandPolicy(
            items, Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => items.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));
    }

    private static (
        WorldState Before, WorldState After,
        FoundationEdgeTravelTurnEvent Event,
        PhysicalWorkerRuntimeRecord Worker,
        PhysicalTacticalTileId Destination)
        FirstTurn()
    {
        var (world, worker, destination, movement, _, _) = Fixture();
        var before = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var phase = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var command = new FoundationEdgeTravelTurnCommand(
            new CommandId(turn, 1UL), worker.Owner, worker.Id,
            destination, isStart: true);
        var resolved = phase.Resolve(
            new TurnResolutionInput(before, new ISimulationCommand[] { command },
                context), before);
        return (before, resolved.ResultingWorldState,
            Assert.IsType<FoundationEdgeTravelTurnEvent>(
                Assert.Single(resolved.Events)), worker, destination);
    }




    [Fact]
    public void R212HostPublishesOnlyAllReadyAndInterruptsActiveOccupation()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        demo.Session.AcceptFineFirstOpening(beginning, incidence, fine, signature);
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
            beginning.Civilizations.Civilizations,
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
            enableMonetaryInfrastructure: true, settlementPolicy: Policy(beginning.Civilizations.Civilizations),
            fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL);
        Assert.True(host.PersistentOrdersEnabled);
        var owner = worker.Owner;
        var foreign = beginning.Civilizations.Civilizations.First(c => c != owner);
        host.QueueHumanPersistentEdit(owner, new FoundationPersistentOrderEdit(owner, worker.Id,
            FoundationPersistentOrderEditKind.Insert, OrderKind: FoundationWorkerOrderKind.Occupation,
            Occupation: PhysicalWorkerOccupation.Agriculture));
        Assert.Single(host.GetHumanPersistentQueues(owner).Single(q => q.Worker == worker.Id).Orders);
        Assert.Empty(host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanPersistentEdit(foreign,
            new FoundationPersistentOrderEdit(foreign, worker.Id, FoundationPersistentOrderEditKind.Insert,
                OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.Mining)));
        host.SetHumanReady(owner);
        Assert.Null(host.TryCloseTurn());
        Assert.Same(beginning, host.Snapshot);
        host.SetHumanReady(foreign);
        var first = host.TryCloseTurn();
        Assert.NotNull(first);
        Assert.NotNull(first!.PersistentTimeline);
        Assert.Single(first.PersistentEdits);
        Assert.Equal(1UL, host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).ActiveOrderId);
        Assert.Equal(0, host.PendingOrderCount);
        // Interruption is staged privately; it removes the active head, not
        // the worker, and leaves no implicit productive activity.
        var interrupted = host.Snapshot.InterruptWorkerOrder(owner, worker.Id, 1UL);
        Assert.Empty(interrupted.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        Assert.Equal(PhysicalWorkerActivity.Other, interrupted.Workers.Workers.Single(w => w.Id == worker.Id).Activity);
        host.QueueHumanPersistentEdit(owner, new FoundationPersistentOrderEdit(owner, worker.Id,
            FoundationPersistentOrderEditKind.Interrupt, OrderId: 1UL));
        Assert.Empty(host.GetHumanPersistentQueues(owner).Single(q => q.Worker == worker.Id).Orders);
        Assert.Equal(1UL, host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).ActiveOrderId);
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanPersistentEdit(owner,
            new FoundationPersistentOrderEdit(owner, worker.Id, FoundationPersistentOrderEditKind.Interrupt, OrderId: 1UL)));
    }

    [Fact]
    public void R217BirthsAfterEconomicCloseAreAtomicAndReplayable()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), Policy(beginning.Civilizations.Civilizations),
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, enableDemographicGrowth: true);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.Insert, Index: 1,
                    OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.Agriculture))
        };
        var resolved = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var next = resolved.ResultingWorldState;
        var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        Assert.Single(resolved.Events.OfType<FoundationFundedWorkerIntentEvent>());
        Assert.Equal(1UL, next.Treasury.LastSettledEconomicCycleOrdinal);
        var batch = Assert.IsType<FoundationPhysicalBirthBatchEvent>(resolved.Events.Last());
        Assert.IsType<FoundationTemporalSalesSettledEvent>(resolved.Events.Reverse().Skip(1).First());
        Assert.Equal(beginning.Civilizations.Civilizations.Count, batch.Newborns.Count);
        Assert.Equal(beginning.Workers.Workers.Count + batch.Newborns.Count, next.Workers.Workers.Count);
        Assert.Equal(next.Workers.Workers.Count, next.WorkerOrders.Queues.Count);
        Assert.Equal(21U, new CanonicalWorldStateHasher().Compute(next).FormatVersion);
        var presence = FoundationPhysicalPresenceDemographicProjector.Project(beginning, timeline.Slices.Select(s => new FoundationWorkerTemporalNodeSlice(s.Worker, s.Node, s.DurationMicro, s.Activity == PhysicalWorkerActivity.Producing ? s.DurationMicro : 0U, s.Activity, s.Occupation)).ToArray(), Demand());
        var expected = FoundationPhysicalPresenceDemographicProjector.PlanBirths(beginning, presence,
            beginning.Civilizations.Civilizations, context.Turn.Value);
        Assert.Equal(expected.Births.Select(b => b.NatalNode), batch.Newborns.Select(w => w.NatalNode!.Value));
        foreach (var child in batch.Newborns)
        {
            Assert.Equal(child.NatalNode, child.TacticalTileId);
            Assert.Equal(PhysicalWorkerOccupation.Unassigned, child.Occupation);
            Assert.Empty(next.WorkerOrders.Queues.Single(q => q.Worker == child.Id).Orders);
            Assert.DoesNotContain(timeline.Slices, s => s.Worker == child.Id);
            Assert.Equal(child.NatalNode, child.WithTacticalAssignment(child.TacticalTileId!.Value,
                child.CellId, PhysicalWorkerActivity.Producing, PhysicalWorkerOccupation.Agriculture).NatalNode);
        }
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
        foreach (var events in new[] { resolved.Events.Take(resolved.Events.Count() - 1).ToArray(),
            resolved.Events.Append(batch).ToArray(),
            resolved.Events.Take(resolved.Events.Count - 1).Append(batch with { ScheduleDigest = "forged" }).ToArray() })
        {
            var log = new SimulationEventLog(events.Select((e, i) => new SimulationEventLogEntry((ulong)i + 1, e, true, true)).ToArray());
            Assert.ThrowsAny<Exception>(() => phase.ReplayPersistentJournal(beginning, log, context));
        }
        Assert.Equal(0UL, beginning.Treasury.LastSettledEconomicCycleOrdinal);
        var secondContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var second = phase.Resolve(new TurnResolutionInput(next, Array.Empty<ISimulationCommand>(), secondContext), next);
        var secondTimeline = Assert.Single(second.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        foreach (var child in batch.Newborns)
            Assert.Equal(1_000_000L, secondTimeline.Slices.Where(s => s.Worker == child.Id).Sum(s => (long)s.DurationMicro));
        Assert.Equal(hash.Compute(second.ResultingWorldState), hash.Compute(phase.ReplayPersistentJournal(next, second.EventLog, secondContext)));
    }

    [Fact]
    public void R219AExplicitCropUsesArrivalRemainderAndCanonicalMarketReplay()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.Insert, Index: 1,
                    OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.Agriculture, Crop: 4))
        };
        var resolved = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var next = resolved.ResultingWorldState;
        var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        Assert.Single(resolved.Events.OfType<FoundationFundedWorkerIntentEvent>());
        Assert.Equal(1UL, next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(next.Constructions.Projects.Single().IsActivated);
        var slices = timeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.Equal(600_000U, slices[0].DurationMicro);
        Assert.Equal(1UL, slices[0].ProjectId);
        Assert.Equal(400_000U, slices[1].DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
        Assert.Equal((byte)4, slices[1].Crop);
        Assert.Equal((byte)4, next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders.Single().Crop);
        var expected = agriculture.Resolve(slices[1].Node, GlobalArena.Foundation.Agriculture.CropId.Rice);
        Assert.Equal(crops[GlobalArena.Foundation.Agriculture.CropId.Rice], expected.Commodity);
        ulong total = timeline.Slices.Where(s => s.Activity == PhysicalWorkerActivity.Producing)
            .GroupBy(s => (s.Worker, s.Node, s.Occupation, s.Crop))
            .Aggregate(0UL, (sum, group) => checked(sum + (ulong)((UInt128)
                (group.Key.Crop.HasValue ? expected.WholeTurnOutput : beginning.Workers.Workers.Single(w => w.Id == group.Key.Worker).OutputMicroPerTurn)
                * (ulong)group.Sum(s => (long)s.DurationMicro) / 1_000_000UL)));
        Assert.Equal(total, sale.ProducedGoodsMicro);
        var intake = Assert.IsAssignableFrom<IReadOnlyList<FoundationWorkerFoodIntake>>(sale.FoodIntake);
        Assert.Equal(beginning.Workers.Workers.Count * 6, intake.Count);
        Assert.Equal(total, intake.Aggregate(0UL, (sum, row) => checked(sum + row.ConsumedMicro)));
        foreach (var cropItem in crops.Values)
        {
            ulong cropProduction = timeline.Slices.Where(s => s.Activity == PhysicalWorkerActivity.Producing)
                .GroupBy(s => (s.Worker, s.Node, s.Occupation, s.Crop))
                .Where(g => (g.Key.Crop.HasValue ? agriculture.Resolve(g.Key.Node, (GlobalArena.Foundation.Agriculture.CropId)g.Key.Crop.Value).Commodity
                    : beginning.Workers.Workers.Single(w => w.Id == g.Key.Worker).Commodity) == cropItem)
                .Aggregate(0UL, (sum, g) => checked(sum + (ulong)((UInt128)
                    (g.Key.Crop.HasValue ? agriculture.Resolve(g.Key.Node, (GlobalArena.Foundation.Agriculture.CropId)g.Key.Crop.Value).WholeTurnOutput
                    : beginning.Workers.Workers.Single(w => w.Id == g.Key.Worker).OutputMicroPerTurn)
                    * (ulong)g.Sum(s => (long)s.DurationMicro) / 1_000_000UL)));
            Assert.Equal(cropProduction, intake.Where(x => x.Commodity == cropItem)
                .Aggregate(0UL, (sum, x) => checked(sum + x.ConsumedMicro)));
        }
        var conversions = crops.Values.ToDictionary(c => c, _ => 0.000001);
        var nutrition = FoundationSettledFoodIntake.Evaluate(worker.Id, intake, conversions,
            new CanonicalNutritionCandidateParameters(0.5, 2.8));
        Assert.Null(nutrition.CandidateQuality);
        Assert.False(nutrition.AuthorizesProductivityChange);

        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(22U, hash.Compute(next).FormatVersion);
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
        var water = Enumerable.Range(0, fine.FineElevationRaw.Count).First(i => fine.SurfaceAtIndex(i) != FoundationFineSurfaceKind.Land);
        var waterTile = new PhysicalTacticalTileId(fine.FineParameters, new StrategicCellId((ulong)water + 1UL));
        foreach (var crop in crops.Keys) Assert.Equal(0UL, agriculture.Resolve(waterTile, crop).WholeTurnOutput);
        Assert.Throws<ArgumentException>(() => beginning.InsertWorkerOrder(worker.Owner, worker.Id, 0,
            FoundationWorkerOrderKind.Occupation, occupation: PhysicalWorkerOccupation.Mining, crop: 4));
    }

    [Fact]
    public void R219BConstructionRemainderOperatesOpeningInfrastructureAndReplays()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture, operationalPolicy: new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.Insert, Index: 1,
                    OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.LandTransport))
        };
        var resolved = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var next = resolved.ResultingWorldState;
        var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        Assert.Single(resolved.Events.OfType<FoundationFundedWorkerIntentEvent>());
        Assert.Equal(1UL, next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(next.Constructions.Projects.Single().IsActivated);
        var slices = timeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.Equal(600_000U, slices[0].DurationMicro);
        Assert.Equal(1UL, slices[0].ProjectId);
        Assert.Equal(400_000U, slices[1].DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Other, slices[1].Activity);
        Assert.Equal(PhysicalWorkerOccupation.LandTransport, slices[1].Occupation);
        var quotes = Assert.IsAssignableFrom<IReadOnlyList<FoundationOperationalFreightShadowEdge>>(sale.OperationalQuotes);
        Assert.NotEmpty(quotes);
        Assert.Equal(400_000UL, quotes.Aggregate(0UL, (sum, q) => checked(sum + q.OperationalServiceMicro)));
        foreach (var quote in quotes)
        {
            var openingEdge = beginning.Transport.Edges.Single(e => e.Edge == quote.Edge);
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(openingEdge), quote.OriginalPhysicalCostMicroPerWholeCargoUnit);
            Assert.True(quote.ShadowPhysicalCostMicroPerWholeCargoUnit < quote.OriginalPhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(FoundationOperationalWorkKind.Land, quote.WorkKind);
        }
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
    }

    [Fact]
    public void R2232ManualBaseResumesAfterConstructionAndReplays()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture, operationalPolicy: new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity:
                    new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 4)))
        };
        var resolved = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var next = resolved.ResultingWorldState;
        var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        Assert.Single(resolved.Events.OfType<FoundationFundedWorkerIntentEvent>());
        Assert.Equal(1UL, next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(next.Constructions.Projects.Single().IsActivated);
        var slices = timeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.Equal(600_000U, slices[0].DurationMicro);
        Assert.Equal(1UL, slices[0].ProjectId);
        Assert.Equal(400_000U, slices[1].DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, slices[1].Occupation);
        Assert.Equal((byte)4, slices[1].Crop);
        var queue = next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id);
        Assert.Empty(queue.Orders);
        Assert.Equal((byte)4, queue.BaseActivity!.Crop);
        var input = FoundationTemporalEconomicTurnProjector.ProjectPersistent(beginning, timeline.Slices, demand, agriculture);
        var output = Assert.Single(input.Production, p => p.Worker == worker.Id);
        var expected = agriculture.Resolve(tile, GlobalArena.Foundation.Agriculture.CropId.Rice);
        Assert.Equal(expected.Commodity, output.Commodity);
        Assert.Equal(400_000U, output.EffortMicro);
        Assert.Equal(expected.WholeTurnOutput * 400_000UL / 1_000_000UL, output.ProducedMicro);
        Assert.Equal(1_000_000L, slices.Sum(s => (long)s.DurationMicro));
        Assert.NotNull(sale.FoodIntake);
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
        var secondContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var second = phase.Resolve(new TurnResolutionInput(next, Array.Empty<ISimulationCommand>(), secondContext), next);
        var secondTimeline = Assert.Single(second.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        var baseSlices = secondTimeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.All(baseSlices, s => { Assert.Equal((byte)4, s.Crop); Assert.Equal(PhysicalWorkerActivity.Producing, s.Activity); });
        Assert.Equal(1_000_000L, baseSlices.Sum(s => (long)s.DurationMicro));
        Assert.Equal(hash.Compute(second.ResultingWorldState), hash.Compute(phase.ReplayPersistentJournal(next, second.EventLog, secondContext)));
    }

    [Fact]
    public void R2233BaseResumesAtArrivalAndExactBoundary()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        // Select a real ground crossing whose destination supports the chosen crop.
        // A ground crossing alone does not guarantee agricultural suitability.
        var candidate = (from w in beginning.Workers.Workers
            where w.Activity != PhysicalWorkerActivity.Traveling && w.TacticalTileId.HasValue
            let source = w.TacticalTileId!.Value
            from edge in incidence.FineTopology.Edges
            where edge.IncidentCellIds.Contains(source.FineStrategicCellId)
                && fine.CrossingAtIndices((int)edge.IncidentCellIds[0].Value - 1,
                    (int)edge.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground
            let target = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
                edge.IncidentCellIds.Single(n => n != source.FineStrategicCellId))
            from crop in crops.Keys
            where agriculture.Resolve(target, crop).WholeTurnOutput > 0
            orderby w.Id != worker.Id, crop != GlobalArena.Foundation.Agriculture.CropId.Rice,
                w.Id.Value, target.FineStrategicCellId.Value, (byte)crop
            select (Worker: w, Source: source, Destination: target, Crop: crop)).First();
        worker = candidate.Worker;
        Assert.NotEqual(PhysicalWorkerActivity.Traveling, worker.Activity);
        Assert.DoesNotContain(beginning.EdgeTravels.Pending, p => p.Worker == worker.Id);
        var tile = candidate.Source;
        var destination = candidate.Destination;
        var selectedCrop = (byte)candidate.Crop;
        Assert.True(agriculture.Resolve(destination, candidate.Crop).WholeTurnOutput > 0);
        var baseActivity = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual,
            PhysicalWorkerOccupation.Agriculture, selectedCrop);
        var moving = beginning.SetWorkerBaseActivity(worker.Owner, worker.Id, baseActivity)
            .InsertWorkerOrder(worker.Owner, worker.Id, 0, FoundationWorkerOrderKind.Move, destination);
        var scheduler = new FoundationPersistentWorkerTurnScheduler(new FoundationConstructionApproachRouter(
            demo.Session.GeneratedWorld, incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)), agriculture);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var (first, travel) = scheduler.Record(moving, context);
        var firstSlices = travel.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.All(firstSlices, x => Assert.Equal(PhysicalWorkerActivity.Traveling, x.Activity));
        Assert.Equal(1_000_000L, firstSlices.Sum(x => (long)x.DurationMicro));
        var pending = first.EdgeTravels.Pending.Single(x => x.Worker == worker.Id);
        Assert.Equal(400_000UL, pending.RemainingMicro);
        var secondContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var (second, arrival) = scheduler.Record(first, secondContext);
        var slices = arrival.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(400_000U, slices[0].DurationMicro);
        Assert.Equal(tile, slices[0].Node);
        Assert.Equal(600_000U, slices[1].DurationMicro);
        Assert.Equal(destination, slices[1].Node);
        Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
        Assert.Equal(selectedCrop, slices[1].Crop);
        Assert.Empty(second.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        Assert.Equal(baseActivity, second.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).BaseActivity);
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(second), hash.Compute(scheduler.Execute(first, arrival, secondContext)));
        var exact = new FoundationPersistentWorkerTurnScheduler(new FoundationConstructionApproachRouter(
            demo.Session.GeneratedWorld, incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_000_000UL)), agriculture);
        var (boundary, boundaryEvent) = exact.Record(moving, context);
        Assert.All(boundaryEvent.Slices.Where(x => x.Worker == worker.Id), x => Assert.Null(x.Crop));
        Assert.Equal(destination, boundary.Workers.Workers.Single(x => x.Id == worker.Id).TacticalTileId);
        var (following, followingEvent) = exact.Record(boundary, secondContext);
        Assert.Empty(following.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        var followingSlices = followingEvent.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(1_000_000L, followingSlices.Sum(x => (long)x.DurationMicro));
        Assert.All(followingSlices, x => { Assert.Equal(PhysicalWorkerActivity.Producing, x.Activity); Assert.Equal(selectedCrop, x.Crop); });
    }

    [Fact]
    public void R2233PrivateManualBasePublishesOnlyAllReady()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        demo.Session.AcceptFineFirstOpening(beginning, incidence, fine, signature);
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
            beginning.Civilizations.Civilizations, new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
            enableMonetaryInfrastructure: true, settlementPolicy: policy, fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture);
        var owner = worker.Owner;
        var foreign = beginning.Civilizations.Civilizations.First(c => c != owner);
        var rice = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 4);
        var hash = host.StateHash;
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanBaseActivity(foreign, worker.Id, rice));
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanBaseActivity(owner, worker.Id,
            new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic)));
        Assert.Equal(0, host.PendingOrderCount);
        host.QueueHumanBaseActivity(owner, worker.Id, rice);
        host.QueueHumanFundedConstruction(owner, edge, 600_000UL,
            new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) });
        var planned = host.GetHumanPersistentQueues(owner).Single(q => q.Worker == worker.Id);
        Assert.Equal(rice, planned.BaseActivity);
        Assert.Single(planned.Orders);
        Assert.DoesNotContain(host.GetHumanPersistentQueues(foreign), q => q.Worker == worker.Id);
        Assert.Null(host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).BaseActivity);
        Assert.Equal(hash, host.StateHash);
        host.SetHumanReady(owner);
        Assert.Null(host.TryCloseTurn());
        Assert.Same(beginning, host.Snapshot);
        host.SetHumanReady(foreign);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        Assert.Single(receipt.PersistentEdits);
        Assert.Single(receipt.PersistentFunding);
        Assert.Equal(1UL, host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        var committed = host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id);
        Assert.Equal(rice, committed.BaseActivity);
        Assert.Empty(committed.Orders);
        var slices = receipt.PersistentTimeline!.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(600_000U, slices[0].DurationMicro);
        Assert.Equal(400_000U, slices[1].DurationMicro);
        Assert.Equal((byte)4, slices[1].Crop);
        Assert.Equal(0, host.PendingOrderCount);
        var trade = host.GetLastClosedEconomicTradeView(owner);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset, trade.Availability);
        Assert.Equal((ulong?)1UL, trade.EconomicCycleOrdinal);
        Assert.Equal((WorldStateHash?)receipt.Hash, trade.PublishedHash);
        Assert.Equal(receipt.TemporalEconomicSettlement!.AuditedParticipantEdges![owner].ToArray(),
            trade.UsedEdges.ToArray());
    }

    [Fact]
    public void R2233TwoFiniteBuildsThenBase()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var otherEdge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters,
            incidence.FineTopology.Edges.First(e => e.Id != crossing.Id &&
                e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
                fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                    (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground).Id);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 1UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity:
                    new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 4))),
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 2UL), worker.Owner,
                edge, 300_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 3UL), worker.Owner,
                otherEdge, 500_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 1) })
        };
        var result = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var timeline = Assert.Single(result.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        var slices = timeline.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(3, slices.Length);
        Assert.Equal(300_000U, slices[0].DurationMicro);
        Assert.Equal(1UL, slices[0].ProjectId);
        Assert.Equal(500_000U, slices[1].DurationMicro);
        Assert.Equal(2UL, slices[1].ProjectId);
        Assert.Equal(200_000U, slices[2].DurationMicro);
        Assert.Equal((byte)4, slices[2].Crop);
        Assert.All(result.ResultingWorldState.Constructions.Projects, p => Assert.True(p.IsActivated));
        Assert.Empty(result.ResultingWorldState.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(result.ResultingWorldState),
            hash.Compute(phase.ReplayPersistentJournal(beginning, result.EventLog, context)));
    }

    [Fact]
    public void R2232ManualBaseLogisticsUsesOnlyResidualTimeAndReplays()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture, operationalPolicy: new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity:
                    new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.LandTransport)))
        };
        var resolved = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var next = resolved.ResultingWorldState;
        var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        Assert.Single(resolved.Events.OfType<FoundationFundedWorkerIntentEvent>());
        Assert.Equal(1UL, next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(next.Constructions.Projects.Single().IsActivated);
        var slices = timeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.Equal(600_000U, slices[0].DurationMicro);
        Assert.Equal(1UL, slices[0].ProjectId);
        Assert.Equal(400_000U, slices[1].DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Other, slices[1].Activity);
        Assert.Equal(PhysicalWorkerOccupation.LandTransport, slices[1].Occupation);
        Assert.Empty(next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        var quotes = Assert.IsAssignableFrom<IReadOnlyList<FoundationOperationalFreightShadowEdge>>(sale.OperationalQuotes);
        Assert.NotEmpty(quotes);
        Assert.Equal(400_000UL, quotes.Aggregate(0UL, (sum, q) => checked(sum + q.OperationalServiceMicro)));
        foreach (var quote in quotes)
        {
            var openingEdge = beginning.Transport.Edges.Single(e => e.Edge == quote.Edge);
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(openingEdge), quote.OriginalPhysicalCostMicroPerWholeCargoUnit);
            Assert.True(quote.ShadowPhysicalCostMicroPerWholeCargoUnit < quote.OriginalPhysicalCostMicroPerWholeCargoUnit);
        }
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
    }

    [Fact]
    public void R211CollectiveFundingAndTimelineSettleOneMarketAndReplay()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), Policy(beginning.Civilizations.Civilizations),
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.Insert, Index: 1,
                    OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.Agriculture))
        };
        var resolved = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var next = resolved.ResultingWorldState;
        var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        Assert.Single(resolved.Events.OfType<FoundationFundedWorkerIntentEvent>());
        Assert.Equal(1UL, next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(next.Constructions.Projects.Single().IsActivated);
        var slices = timeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.Equal(600_000U, slices[0].DurationMicro);
        Assert.Equal(1UL, slices[0].ProjectId);
        Assert.Equal(400_000U, slices[1].DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
        ulong expectedGoods = timeline.Slices.Where(s => s.Activity == PhysicalWorkerActivity.Producing)
            .GroupBy(s => (s.Worker, s.Node, s.Occupation))
            .Aggregate(0UL, (sum, g) => checked(sum + (ulong)((UInt128)beginning.Workers.Workers
                .Single(w => w.Id == g.Key.Worker).OutputMicroPerTurn *
                (ulong)g.Sum(s => (long)s.DurationMicro) / 1_000_000UL)));
        Assert.Equal(expectedGoods, sale.ProducedGoodsMicro);
        Assert.Equal(sale.GrossSalesScaled, sale.TotalCivilizationCreditsScaled + sale.PhysicalFreightScaled);
        foreach (var credit in sale.Credits)
            Assert.Equal(beginning.Treasury.Get(credit.Civilization).CashScaled + credit.CashCreditScaled -
                (credit.Civilization == worker.Owner ? GlobalArenaMoneyScale.FromMoneyMicro(600_000UL) : BigInteger.Zero),
                next.Treasury.Get(credit.Civilization).CashScaled);
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
        var duplicate = new SimulationEventLog(resolved.Events.Take(3).Append(timeline).Append(sale)
            .Select((e, i) => new SimulationEventLogEntry((ulong)i + 1, e, true, true)).ToArray());
        Assert.ThrowsAny<Exception>(() => phase.ReplayPersistentJournal(beginning, duplicate, context));
        Assert.Empty(beginning.Constructions.Projects);
        Assert.Equal(0UL, beginning.Treasury.LastSettledEconomicCycleOrdinal);
    }

    [Fact]
    public void R210PersistentBuildUsesArrivalRemainderAndContinuesWithoutNewOrder()
    {
        var (world, worker, adjacent, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(adjacent.FineStrategicCellId) &&
            !e.IncidentCellIds.Contains(worker.TacticalTileId!.Value.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var initial = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                world.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var funding = new FoundationFundedWorkerIntentJournal(new FoundationConstructionFundingExecutor(
            new FoundationMonetaryInfrastructureEventExecutor(demo.Session.GeneratedWorld, incidence), 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var (funded, _) = funding.Record(initial, new EventId(new CommandId(context.Turn, 1UL), 1UL),
            worker.Owner, edge, 1_200_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }, context);
        funded = funded.InsertWorkerOrder(worker.Owner, worker.Id, 1, FoundationWorkerOrderKind.Occupation,
            occupation: PhysicalWorkerOccupation.Agriculture);
        var scheduler = new FoundationPersistentWorkerTurnScheduler(new FoundationConstructionApproachRouter(
            demo.Session.GeneratedWorld, incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)));
        var hash = new CanonicalWorldStateHasher();
        var state = funded;
        for (ulong turn = 1; turn <= 3; turn++)
        {
            context = new SimulationContext(new TurnNumber(turn), new SimulationSeed(712UL));
            var (next, ev) = scheduler.Record(state, context);
            Assert.Equal(hash.Compute(next), hash.Compute(scheduler.Execute(state, ev, context)));
            Assert.Throws<InvalidOperationException>(() => scheduler.Execute(next, ev, context));
            Assert.All(ev.Slices.GroupBy(x => x.Worker), g => Assert.Equal(1_000_000L, g.Sum(x => (long)x.DurationMicro)));
            var slices = ev.Slices.Where(x => x.Worker == worker.Id).ToArray();
            Assert.Equal(turn == 1 ? 0UL : turn == 2 ? 600_000UL : 1_200_000UL,
                next.Constructions.Projects.Single().CompletedWorkMicro);
            Assert.Equal(turn == 3, next.Constructions.Projects.Single().IsActivated);
            if (turn == 2)
            {
                Assert.Equal(400_000U, slices[0].DurationMicro);
                Assert.Equal(600_000U, slices[1].DurationMicro);
                Assert.Equal(1UL, slices[1].ProjectId);
            }
            if (turn == 3)
            {
                Assert.Equal(600_000U, slices[0].DurationMicro);
                Assert.Equal(400_000U, slices[1].DurationMicro);
                Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
                Assert.Equal(2UL, next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).ActiveOrderId);
            }
            Assert.Equal(funded.Treasury.Get(worker.Owner).CashScaled, next.Treasury.Get(worker.Owner).CashScaled);
            state = next;
        }
    }

    [Fact]
    public void R209RemoteBuildHeadAutomaticallyApproachesSiteAcrossTurns()
    {
        var (world, worker, adjacent, movement, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            demo.Session.GeneratedWorld, incidence, fine);
        var source = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(adjacent.FineStrategicCellId) &&
            !e.IncidentCellIds.Contains(source.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var initial = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                world.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var funding = new FoundationFundedWorkerIntentJournal(new FoundationConstructionFundingExecutor(
            new FoundationMonetaryInfrastructureEventExecutor(demo.Session.GeneratedWorld, incidence), 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var (funded, _) = funding.Record(initial, new EventId(new CommandId(context.Turn, 1UL), 1UL),
            worker.Owner, edge, 1_600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }, context);
        var policy = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var router = new FoundationConstructionApproachRouter(demo.Session.GeneratedWorld, incidence, fine, signature, policy);
        var plan = router.Plan(funded, worker.Id);
        Assert.Equal(FoundationConstructionApproachStatus.RouteAvailable, plan.Status);
        Assert.Equal(source, plan.Path[0]);
        Assert.Contains(plan.Path.Last().FineStrategicCellId, crossing.IncidentCellIds);
        Assert.Equal(new BigInteger(1_400_000UL), plan.RemainingTravelMicro);
        var hasher = new CanonicalWorldStateHasher();
        var (first, receipt) = router.Advance(funded, worker.Id);
        Assert.NotNull(receipt);
        Assert.Equal(400_000UL, first.EdgeTravels.Pending.Single().RemainingMicro);
        Assert.Equal(FoundationConstructionApproachStatus.CrossingInProgress, router.Plan(first, worker.Id).Status);
        Assert.Equal(hasher.Compute(first), hasher.Compute(router.Advance(funded, worker.Id).State));
        var (arrived, second) = router.Advance(first, worker.Id);
        Assert.NotNull(second);
        Assert.Empty(arrived.EdgeTravels.Pending);
        Assert.Equal(FoundationConstructionApproachStatus.AtSite, router.Plan(arrived, worker.Id).Status);
        Assert.Same(arrived, router.Advance(arrived, worker.Id).State);
        Assert.Equal(0UL, arrived.Constructions.Projects.Single().CompletedWorkMicro);
        Assert.Same(funded.Treasury, arrived.Treasury);
        Assert.Same(funded.WorkerOrders, arrived.WorkerOrders);
        Assert.Equal(FoundationConstructionApproachStatus.NoBuildHead,
            router.Plan(initial, worker.Id).Status);
    }

    [Fact]
    public void FundWorkAndCommissionAcrossCyclesUsingOnlyOpeningInfrastructure()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        // Retain the proven legacy fixture binding and signed surface for
        // routing. One stationary worker is reserved exclusively for work.
        var movement = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var admission = new FoundationMonetaryInfrastructureEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var builder = world.Workers.Workers.First(w => w.Owner == worker.Owner &&
            w.Occupation == PhysicalWorkerOccupation.Forestry);
        var tile = builder.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        world = world.WithWorkers(new PhysicalWorkerRuntimeState(
            world.Workers.Workers.Select(w => w.Id == builder.Id
                ? w.WithTacticalAssignment(tile, w.CellId, PhysicalWorkerActivity.Other,
                    PhysicalWorkerOccupation.Unassigned) : w),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                world.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()));
        var policy = Policy(beginning.Civilizations.Civilizations);
        var travelPolicy = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var construction = new FoundationConstructionCollectiveTurnPhase(
            new FoundationConstructionFundingExecutor(admission, 1_000_000UL),
            incidence, fine, temporal: true);
        var hasher = new CanonicalWorldStateHasher();
        var natural = beginning.Transport.Edges.Single(e => e.Edge == edge);
        ulong originalFreight = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(natural);
        var state = beginning;
        const ulong money = 1_600_000UL;
        FoundationTemporalSalesSettledEvent? completionSale = null;
        for (ulong turn = 1; turn <= 4; turn++)
        {
            var context = new SimulationContext(new TurnNumber(turn), new SimulationSeed(712UL));
            ISimulationCommand[] commands = turn == 1
                ? new ISimulationCommand[] { new FoundationMonetaryInfrastructureCommand(
                    new CommandId(context.Turn, 1UL), builder.Owner, edge, money) }
                : turn <= 3
                    ? new ISimulationCommand[] { new FoundationConstructionWorkCommand(
                        new CommandId(context.Turn, 1UL), builder.Owner, builder.Id, 1UL) }
                    : Array.Empty<ISimulationCommand>();
            var opening = state;
            var staged = construction.Resolve(new TurnResolutionInput(opening, commands, context), opening);
            var receipt = FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
                opening, staged.Events, movement, travelPolicy, incidence, policy, context,
                admission, fine, construction);
            state = receipt.Next;
            Assert.True(receipt.ReplayMatched);
            Assert.Equal(turn, state.Treasury.LastSettledEconomicCycleOrdinal);
            Assert.Equal(receipt.Hash, hasher.Compute(FoundationTemporalEconomicJournalGate.ReplayJournal(
                opening, receipt.Journal, movement, travelPolicy, incidence, policy, context,
                fine, admission, construction)));
            Assert.Equal(receipt.EconomicEvent.GrossSalesScaled,
                receipt.EconomicEvent.TotalCivilizationCreditsScaled + receipt.EconomicEvent.PhysicalFreightScaled);
            foreach (var credit in receipt.EconomicEvent.Credits)
            {
                var debit = turn == 1 && credit.Civilization == builder.Owner
                    ? GlobalArenaMoneyScale.FromMoneyMicro(money) : BigInteger.Zero;
                Assert.Equal(opening.Treasury.Get(credit.Civilization).CashScaled - debit + credit.CashCreditScaled,
                    state.Treasury.Get(credit.Civilization).CashScaled);
            }
            var calendar = FoundationTemporalEconomicTurnProjector.Project(opening, staged.ResultingWorldState,
                Array.Empty<FoundationEdgeTravelTurnEvent>(), policy.Demand);
            var expected = FoundationTemporalFinancialReconciler.Evaluate(opening, incidence, calendar, policy, fine, opening);
            Assert.Equal(expected.Accounting.PhysicalFreightScaled, receipt.EconomicEvent.PhysicalFreightScaled);
            Assert.Equal(expected.Accounting.GrossSalesScaled, receipt.EconomicEvent.GrossSalesScaled);
            Assert.Equal(expected.Accounting.ExternalTollsScaled, receipt.EconomicEvent.ExternalTollsScaled);
            var project = Assert.Single(state.Constructions.Projects);
            Assert.Equal(turn == 1 ? 0UL : turn == 2 ? 1_000_000UL : money, project.CompletedWorkMicro);
            Assert.Equal(turn >= 3, project.IsActivated);
            var physical = state.Transport.Edges.Single(e => e.Edge == edge);
            Assert.Equal(turn < 3 ? originalFreight :
                FoundationPhysicalAuthoritativeTransportProjector.ProjectUnitFreightCost(natural, money),
                FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(physical));
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(physical),
                state.Treasury.LastSettledFreightQuotes.Single(q => q.Edge == edge).FreePhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(money),
                state.Treasury.Get(builder.Owner).TotalInfrastructureInvestmentScaled);
            Assert.DoesNotContain(calendar.Production, p => p.Worker == builder.Id);
            if (turn == 3)
            {
                var batch = Assert.IsType<FoundationConstructionWorkBatchEvent>(Assert.Single(staged.Events));
                Assert.Equal(400_000UL, Assert.Single(batch.Receipts).ResidualWorkMicro);
                completionSale = receipt.EconomicEvent;
            }
            if (turn == 4)
            {
                Assert.NotNull(completionSale);
                Assert.Equal(3UL, completionSale!.EconomicCycleOrdinal);
                Assert.Equal(money, physical.CommittedInvestmentMoneyMicro.GetValueOrDefault());
            }
        }
    }

    [Fact]
    public void OneFullPhysicalTravelTurnCreditsRealTreasuryOnceAndReplays()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 0UL)));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var beforeHash = new CanonicalWorldStateHasher().Compute(beginning);
        var travel = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(beginning, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, context), beginning);
        var movementEvent = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(travel.Events));
        var policy = Policy(beginning.Civilizations.Civilizations);
        var receipt = FoundationTemporalEconomicJournalGate.CloseAndReplay(
            beginning, new[] { movementEvent }, movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            incidence, policy, context, fine);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(receipt.Hash, new CanonicalWorldStateHasher().Compute(receipt.Next));
        Assert.Equal(beforeHash, new CanonicalWorldStateHasher().Compute(beginning));
        Assert.Single(receipt.Next.EdgeTravels.Pending);
        Assert.Equal(400_000UL,
            receipt.Next.EdgeTravels.Pending[0].RemainingMicro);
        Assert.Equal(1UL,
            receipt.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(receipt.EconomicEvent.ProducedGoodsMicro > 0UL);
        Assert.Equal(receipt.EconomicEvent.GrossSalesScaled,
            receipt.EconomicEvent.TotalCivilizationCreditsScaled +
            receipt.EconomicEvent.PhysicalFreightScaled);
        Assert.Equal(receipt.EconomicEvent.GrossSalesScaled,
            receipt.EconomicEvent.SellerNetScaled +
            receipt.EconomicEvent.TaxesScaled +
            receipt.EconomicEvent.ExternalTollsScaled +
            receipt.EconomicEvent.PhysicalFreightScaled);
        Assert.Equal(receipt.EconomicEvent.TotalCivilizationCreditsScaled,
            receipt.EconomicEvent.Credits.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.CashCreditScaled));
        foreach (var credited in receipt.EconomicEvent.Credits)
            Assert.Equal(credited.CashCreditScaled,
                receipt.Next.Treasury.Get(credited.Civilization).CashScaled);
        Assert.Equal(2, receipt.Journal.Entries.Count);
        // Turn 2 must consume the remaining 40% in transit, work 60%
        // at the destination and publish exactly one additional cycle.
        var nextContext = new SimulationContext(
            new TurnNumber(2UL), new SimulationSeed(712UL));
        var nextMove = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(receipt.Next, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(new TurnNumber(2UL), 1UL),
                    worker.Owner, worker.Id, destination, isStart: false)
            }, nextContext), receipt.Next);
        var secondEvent = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(nextMove.Events));
        // The second-cycle market in this deliberately small scenario
        // cannot certify a fixed point under its explicit 64-pass budget.
        // The gate MUST refuse treasury credit, while the physical 40/60
        // timeline remains fully deterministic and auditable.
        var timeline = FoundationTemporalEconomicTurnProjector.Project(
            receipt.Next, nextMove.ResultingWorldState,
            new[] { secondEvent }, policy.Demand);
        Assert.Equal(400_000U,
            secondEvent.Receipt.TurnResult!.TimeSlices[0].PresenceMicro);
        Assert.Equal(600_000U,
            secondEvent.Receipt.TurnResult.TimeSlices[1].ProductiveEffortMicro);
        Assert.Contains(timeline.Production,
            p => p.Worker == worker.Id && p.Node == destination &&
                p.EffortMicro == 600_000U);
        Assert.Empty(nextMove.ResultingWorldState.EdgeTravels.Pending);
        Assert.Equal(destination,
            nextMove.ResultingWorldState.Workers.Workers.Single(w => w.Id == worker.Id)
                .TacticalTileId);
        var hashBeforeRejectedClose = new CanonicalWorldStateHasher()
            .Compute(receipt.Next);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplay(
                receipt.Next, new[] { secondEvent }, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, nextContext, fine));
        Assert.Equal(1UL, receipt.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(hashBeforeRejectedClose,
            new CanonicalWorldStateHasher().Compute(receipt.Next));
        var authenticatedReplay =
            FoundationTemporalEconomicJournalGate.ReplayJournal(
                beginning, receipt.Journal, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, fine);
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(authenticatedReplay));
        var settled = receipt.EconomicEvent;
        var forgedMoney = new FoundationTemporalSalesSettledEvent(
            settled.Id, settled.BeginningHash, settled.PreSettlementHash,
            settled.PreSettlementRevision, settled.EconomicCycleOrdinal,
            settled.ScheduleDigest + "FAKE", settled.CargoUnitCode,
            settled.ProducedGoodsMicro, settled.GrossSalesScaled,
            settled.PhysicalFreightScaled, settled.SellerNetScaled,
            settled.TaxesScaled, settled.ExternalTollsScaled,
            settled.Credits);
        var tamperedJournal = new SimulationEventLog(new[]
        {
            new SimulationEventLogEntry(1UL, movementEvent,
                wasEligible: true, wasExecuted: true),
            new SimulationEventLogEntry(2UL, forgedMoney,
                wasEligible: true, wasExecuted: true)
        });
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.ReplayJournal(
                beginning, tamperedJournal, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, fine));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplay(
                receipt.Next, new[] { movementEvent }, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, fine));
    }

    [Fact]
    public void RejectsTamperedTravelReceiptBeforeAnyFinancialCommit()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 0UL)));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var travel = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(beginning, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, context), beginning);
        var actual = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(travel.Events));
        var forged = new FoundationEdgeTravelTurnEvent(
            actual.Id, actual.Issuer, actual.WorkerId, actual.Destination,
            actual.Receipt with { Revision = actual.Receipt.Revision + 1UL });
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplay(
                beginning, new[] { forged }, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, Policy(beginning.Civilizations.Civilizations), context,
                fine));
        Assert.Equal(0UL, beginning.Treasury.LastSettledEconomicCycleOrdinal);
    }

    private static FoundationAuthoritativeEconomicSettlementPolicy Policy(
        IEnumerable<CivilizationId> roster)
    {
        var demand = Demand();
        return new FoundationAuthoritativeEconomicSettlementPolicy(
            demand,
            new FoundationCommodityCargoConversionPolicy(
                "temporal-cargo",
                demand.Commodities.Select(c =>
                    new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            roster.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
    }

    [Fact]
    public void PreFundedCapitalInvestmentPrecedesTemporalSalesAndReplaysAsOneJournal()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c =>
                (c, c == worker.Owner ? 5_000_000UL : 0UL)));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var travel = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(beginning, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, context), beginning);
        var movementEvent = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(travel.Events));
        var edge = movement.CheckEdge(worker.TacticalTileId!.Value, destination)!.Value;
        var capital = new FoundationMonetaryInfrastructureInvestedEvent(
            new EventId(new CommandId(turn, 2UL), 1UL),
            worker.Owner, edge, 100_000UL,
            travel.ResultingWorldState.Revision);

        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        // The fixture's WorldBinding is the original generated-world binding,
        // not the promoted fine-first binding. The investment executor must
        // match that exact identity; a forged binding must remain rejected.
        var investmentExecutor = new FoundationMonetaryInfrastructureEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var policy = Policy(beginning.Civilizations.Civilizations);
        var journal = FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
            beginning, new ISimulationEvent[] { movementEvent, capital },
            movement, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            incidence, policy, context, investmentExecutor, fine);
        Assert.True(journal.ReplayMatched);
        Assert.Equal(3, journal.Journal.Entries.Count);
        Assert.Equal(1UL, journal.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(journal.Hash, new CanonicalWorldStateHasher().Compute(
            FoundationTemporalEconomicJournalGate.ReplayJournal(
                beginning, journal.Journal, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, fine, investmentExecutor)));
        Assert.Equal(100_000UL,
            journal.Next.Transport.Edges.Single(e => e.Edge == edge)
                .CommittedInvestmentMoneyMicro);
        Assert.Equal(journal.EconomicEvent.GrossSalesScaled,
            journal.EconomicEvent.TotalCivilizationCreditsScaled +
            journal.EconomicEvent.PhysicalFreightScaled);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
                beginning, new ISimulationEvent[] { capital, movementEvent },
                movement, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, investmentExecutor, fine));
        Assert.Equal(0UL, beginning.Treasury.LastSettledEconomicCycleOrdinal);
    }
    [Fact]
    public void LocalOccupationChangeAffectsFullTurnDemandAndCanonicalSupplyLot()
    {
        var (world, worker, _, _, incidence, _) = Fixture();
        Assert.Equal(PhysicalWorkerActivity.Producing, worker.Activity);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, worker.Occupation);
        var begin = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var localExecutor = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var atNode = worker.TacticalTileId!.Value;
        var ev = new PhysicalTacticalWorkerMovedEvent(
            new EventId(new CommandId(turn, 1UL), 1UL),
            worker.Owner, worker.Id, atNode, atNode, null,
            localExecutor.PresentationRegion(atNode, worker.CellId),
            PhysicalWorkerActivity.Producing, PhysicalWorkerOccupation.Forestry);
        var after = localExecutor.Execute(begin, ev, context);
        Assert.Equal(PhysicalWorkerOccupation.Forestry,
            after.Workers.Workers.Single(w => w.Id == worker.Id).Occupation);
        var input = FoundationTemporalEconomicTurnProjector.Project(
            begin, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
            new[] { ev }, Demand());
        var row = Assert.Single(input.Slices, s => s.Worker == worker.Id);
        Assert.Equal(1_000_000U, row.PresenceMicro);
        Assert.Equal(1_000_000U, row.ProductiveEffortMicro);
        Assert.Equal(PhysicalWorkerOccupation.Forestry,
            input.EffectiveOccupations[worker.Id]);
        // Distinct activity coefficients prove the journaled occupation,
        // rather than the stale census occupation, drives consumer demand.
        var goods = new[] { new CommodityId(1U), new CommodityId(2U) };
        var profilePolicy = new FoundationActivityCommodityDemandPolicy(
            goods, Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => goods.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item,
                        profile == FoundationPhysicalDemandProfile.ForestryProduction
                            ? 2_000_000UL : 1_000_000UL))));
        var historical = FoundationTemporalEconomicTurnProjector.Project(
            begin, begin, Array.Empty<FoundationEdgeTravelTurnEvent>(),
            profilePolicy);
        var reassigned = FoundationTemporalEconomicTurnProjector.Project(
            begin, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
            new[] { ev }, profilePolicy);
        foreach (var item in goods)
        {
            var oldDemand = historical.Presence.Demand.Single(d =>
                d.Owner == worker.Owner && d.Node == atNode &&
                d.Commodity == item).DemandMicro;
            var newDemand = reassigned.Presence.Demand.Single(d =>
                d.Owner == worker.Owner && d.Node == atNode &&
                d.Commodity == item).DemandMicro;
            Assert.Equal(1_000_000UL, newDemand - oldDemand);
        }
        var market = FoundationTemporalNativeMarketInputs.FromTemporal(
            begin, input, Demand());
        Assert.Contains(market.SupplyLots, lot =>
            lot.Owner == worker.Owner &&
            lot.Occupation == PhysicalWorkerOccupation.Forestry &&
            lot.Tile == atNode);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicTurnProjector.Project(
                begin, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
                Demand()));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicTurnProjector.Project(
                begin, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
                new[] { ev, ev }, Demand()));
    }

    [Fact]
    public void CollectiveHostCommitsOneAtomicTemporalSaleAfterAnOrderedPhysicalTurn()
    {
        var (world, worker, destination, _, incidence, _) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 0UL)));
        var prepared = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        demo.Session.AcceptSnapshot(prepared);
        var host = new FoundationTacticalHumanAiTurnHost(
            demo.Session, incidence, new[] { worker.Owner },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [world.Civilizations.Civilizations.Single(c => c != worker.Owner)] =
                    new FoundationTacticalAiHoldPolicy()
            }, enableMonetaryInfrastructure: true,
            settlementPolicy: Policy(world.Civilizations.Civilizations),
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var initial = host.StateHash;
        var snapshotBefore = host.Snapshot;
        Assert.Null(host.GetLastClosedTemporalEconomics(worker.Owner));
        var aiCivilization = world.Civilizations.Civilizations.Single(c => c != worker.Owner);
        Assert.Throws<InvalidOperationException>(() =>
            host.GetLastClosedTemporalEconomics(aiCivilization));
        host.QueueHumanMove(worker.Owner, worker.Id, destination);
        Assert.Equal(initial, host.StateHash);
        host.SetHumanReady(worker.Owner);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.NotNull(receipt.TemporalEconomicSettlement);
        Assert.Null(receipt.EconomicSettlement);
        Assert.Single(receipt.EdgeTravels);
        Assert.Equal(2, receipt.ExecutedEvents);
        Assert.Equal(1UL, host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(400_000UL,
            host.Snapshot.EdgeTravels.Pending.Single().RemainingMicro);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.NotEqual(initial, receipt.Hash);
        var publicForViewer =
            FoundationViewerTemporalEconomicCloseProjector.Read(
                receipt, host.Snapshot, worker.Owner);
        var expected = receipt.TemporalEconomicSettlement!.Credits.Single(
            credit => credit.Civilization == worker.Owner);
        Assert.Equal(expected.CashCreditScaled, publicForViewer.TotalCashCreditScaled);
        Assert.Equal(expected.SellerNetScaled, publicForViewer.SellerNetScaled);
        Assert.Equal(expected.SellerTaxScaled, publicForViewer.TaxReceiptsScaled);
        Assert.Equal(expected.OwnerTollIncomeScaled,
            publicForViewer.OwnerTollIncomeScaled);
        Assert.Equal(host.StateHash, publicForViewer.CommittedWorldHash);
        Assert.Equal(publicForViewer,
            host.GetLastClosedTemporalEconomics(worker.Owner));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationViewerTemporalEconomicCloseProjector.Read(
                receipt, snapshotBefore, worker.Owner));
        Assert.Throws<InvalidOperationException>(() =>
            host.GetLastClosedTemporalEconomics(aiCivilization));
        var outsider = new CivilizationId(uint.MaxValue);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationViewerTemporalEconomicCloseProjector.Read(
                receipt, host.Snapshot, outsider));

    }

    [Fact]
    public void AtomicJournalReplaysLocalOccupationAndTravelInTheSameTurn()
    {
        var (world, traveler, destination, _, incidence, fine) = Fixture();
        // This fixture uses the generated-world binding, not the promoted
        // fine-first world binding. Both the local and travel events must be
        // validated by the same matching canonical executor.
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var movement = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var localWorker = world.Workers.Workers.Single(w =>
            w.Owner == traveler.Owner &&
            w.Occupation == PhysicalWorkerOccupation.Forestry);
        Assert.NotEqual(traveler.Id, localWorker.Id);
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 0UL)));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var location = localWorker.TacticalTileId!.Value;
        var local = new PhysicalTacticalWorkerMovedEvent(
            new EventId(new CommandId(turn, 1UL), 1UL),
            localWorker.Owner, localWorker.Id, location, location, null,
            movement.PresentationRegion(location, localWorker.CellId),
            PhysicalWorkerActivity.Producing,
            PhysicalWorkerOccupation.Agriculture);
        var afterLocal = movement.Execute(beginning, local, context);
        var travelResult = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(afterLocal, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 2UL), traveler.Owner, traveler.Id,
                    destination, isStart: true)
            }, context), afterLocal);
        var travel = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(travelResult.Events));
        var policy = Policy(beginning.Civilizations.Civilizations);
        var receipt = FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
            beginning, new ISimulationEvent[] { local, travel }, movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), incidence,
            policy, context, investmentExecutor: null, signedFine: fine);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(3, receipt.Journal.Entries.Count);
        Assert.Equal(1UL, receipt.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture,
            receipt.Next.Workers.Workers.Single(w => w.Id == localWorker.Id).Occupation);
        Assert.Equal(400_000UL, receipt.Next.EdgeTravels.Pending.Single()
            .RemainingMicro);
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(
                FoundationTemporalEconomicJournalGate.ReplayJournal(
                    beginning, receipt.Journal, movement,
                    new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                    incidence, policy, context, fine)));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
                beginning, new ISimulationEvent[] { travel, local }, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), incidence,
                policy, context, investmentExecutor: null, signedFine: fine));
    }

    [Fact]
    public void R219A2PresenceSharesConserveAndResolveTiesByWorker()
    {
        var a = new PhysicalWorkerId(1UL);
        var b = new PhysicalWorkerId(2UL);
        var c = new PhysicalWorkerId(3UL);
        var shares = FoundationSettledFoodIntake.Split(101UL,
            new[] { (c, 400_000UL), (b, 600_000UL), (a, 1_000_000UL) });
        Assert.Equal(new[] { (a, 51UL), (b, 30UL), (c, 20UL) }, shares);
        Assert.Equal(FoundationSettledFoodIntake.Split(101UL,
            new[] { (a, 1_000_000UL), (b, 600_000UL), (c, 400_000UL) }), shares);
        Assert.Equal(new[] { (a, 1UL), (b, 0UL) },
            FoundationSettledFoodIntake.Split(1UL, new[] { (b, 1UL), (a, 1UL) }));
        Assert.Throws<ArgumentException>(() => FoundationSettledFoodIntake.Split(1UL,
            new[] { (a, 0UL) }));
    }

    [Fact]
    public void R219A2NutritionScalarRequiresExplicitSufficiencyAndSixFoods()
    {
        var worker = new PhysicalWorkerId(1UL);
        var items = Enumerable.Range(1, 6).Select(i => new CommodityId((uint)i)).ToArray();
        var conversions = items.ToDictionary(c => c, _ => 0.000001);
        var mono = items.Select((c, i) => new FoundationWorkerFoodIntake(worker, c, i == 0 ? 6_000_000UL : 0UL)).ToArray();
        var mixed = items.Select(c => new FoundationWorkerFoodIntake(worker, c, 1_000_000UL)).ToArray();
        var p = new CanonicalNutritionCandidateParameters(0.5, 2.8);
        var m = FoundationSettledFoodIntake.Evaluate(worker, mono, conversions, p);
        var v = FoundationSettledFoodIntake.Evaluate(worker, mixed, conversions, p, f => f / (1 + f));
        Assert.Equal(m.FoodPerPerson, v.FoodPerPerson);
        Assert.Equal(1.0, m.CandidateVarietyFactor);
        Assert.True(v.CandidateVarietyFactor > m.CandidateVarietyFactor);
        Assert.Null(m.CandidateQuality);
        Assert.True(v.CandidateQuality > 0);
        Assert.Throws<ArgumentException>(() => FoundationSettledFoodIntake.Evaluate(worker, mixed.Skip(1).ToArray(), conversions, p));
    }

}
