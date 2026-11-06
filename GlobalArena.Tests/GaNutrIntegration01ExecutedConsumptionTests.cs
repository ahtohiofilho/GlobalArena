using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-NUTR-INTEGRATION-01: only actually executed, reconciled, current-cycle
/// consumption is eligible as a future nutrition input. Legacy aggregate
/// food is never expanded into six invented varieties or a quality score.
/// </summary>
public sealed class GaNutrIntegration01ExecutedConsumptionTests
{
    private static CanonicalPhysicalTacticalSession Cash() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static CanonicalPhysicalTacticalSession ClosedLegacy()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 5; i++)
            Assert.Null(session.AdvanceMovementTurn());
        Assert.NotNull(session.AdvanceMovementTurn());
        return session;
    }

    [Fact]
    public void BeforeAnExecutedClosingCycleTheBridgeDoesNotInventConsumption()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        Assert.False(CanonicalNutritionExecutedConsumptionProjection.TryCaptureCurrent(
            session, out var empty));
        Assert.Null(empty);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalNutritionExecutedConsumptionProjection
                .FromCurrentExecutedCycle(session));

        for (var i = 0; i < 5; i++)
            session.AdvanceMovementTurn();
        Assert.False(CanonicalNutritionExecutedConsumptionProjection.TryCaptureCurrent(
            session, out var stillEmpty));
        Assert.Null(stillEmpty);
    }

    [Fact]
    public void RealLegacyLocalConsumptionReconcilesWithoutNutritionalQuality()
    {
        var session = ClosedLegacy();
        var projected = CanonicalNutritionExecutedConsumptionProjection
            .FromCurrentExecutedCycle(session);
        var actual = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var lots = session.CaptureLatestPhysicalLotWitness();
        projected.VerifyIndependent();

        Assert.Equal(actual.CycleOrdinal, projected.CycleOrdinal);
        Assert.Equal(actual.ClosingWorldHashHex, projected.ClosingWorldHashHex);
        Assert.Equal(actual.ActuallyConsumedRaw, projected.TotalConsumedRaw);
        Assert.Equal(actual.ActuallyUnmetFoodRaw, projected.TotalUnmetReferenceRaw);
        Assert.Equal(session.State.TotalWorkers, projected.TotalRealPopulation);
        Assert.Equal(projected.TotalConsumedRaw, projected.ResidentLocalConsumedRaw);
        Assert.Equal(0UL, projected.ExpeditionRationsConsumedRaw);
        Assert.Equal(CanonicalNutritionEvidenceStatus.LegacyAggregateFoodOnly,
            projected.Status);
        Assert.Equal(1, projected.ObservedPositiveFoodCommodityCount);
        Assert.All(projected.ExecutedUses, x =>
        {
            Assert.Equal(1U, x.Commodity);
            Assert.Equal(CanonicalPhysicalFinalUseChannel.ResidentLocal, x.Channel);
            Assert.Equal(0UL, x.JourneyOrderId);
            Assert.True(x.DestinationFineTile > 0UL);
            Assert.True(x.ConsumedRaw > 0UL);
        });
        Assert.Equal(actual.FinalUse.Select(x => x.ConsumedRaw).ToArray(),
            projected.ExecutedUses.Select(x => x.ConsumedRaw).ToArray());
        Assert.Equal(new BigInteger(projected.TotalConsumedRaw),
            lots.FinalUseAttributions.Aggregate(BigInteger.Zero,
                (a, x) => a + x.RawQuantity));

        Assert.True(projected.ContainsOnlyExecutedFinalUse);
        Assert.True(projected.HasReconciledPhysicalLotAccounting);
        Assert.False(projected.TreatsFifoLotOriginsAsAuthenticatedIdentity);
        Assert.True(projected.DistinguishesLocalFromExpeditionConsumption);
        Assert.False(projected.HasPerPersonFoodVarietyConsumption);
        Assert.False(projected.HasCalibratedNutritionalConversions);
        Assert.False(projected.CanComputeNutritionalQuality);
        Assert.Null(projected.NutritionalQuality);
        Assert.False(projected.CanAuthorizeProductivityEffect);
        Assert.False(projected.CanAuthorizeRealPurchasingPower);
        Assert.False(projected.ChangesCanonicalWorldOrSave);
    }

    [Fact]
    public void RealCashMarketConsumptionIsNotAnOfferOrDemandProjection()
    {
        var session = Cash();
        Assert.NotNull(session.AdvanceMovementTurn());
        var evidence = CanonicalNutritionExecutedConsumptionProjection
            .FromCurrentExecutedCycle(session);
        var proof = CanonicalExecutedLocalFoodPriceWitness
            .FromLatestRealSettlement(session);

        Assert.Equal(session.State.Cycles[^1].FoodConsumedRaw,
            evidence.TotalConsumedRaw);
        Assert.Equal(proof.TotalActualLocalFoodRaw,
            evidence.ResidentLocalConsumedRaw);
        Assert.Equal(0UL, evidence.ExpeditionRationsConsumedRaw);
        Assert.Equal(CanonicalNutritionEvidenceStatus.LegacyAggregateFoodOnly,
            evidence.Status);
        Assert.Null(evidence.NutritionalQuality);
    }

    [Fact]
    public void RepeatedObservationIsPureAndCannotModifyEconomyOrSave()
    {
        var session = ClosedLegacy();
        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();
        var cycles = session.State.Cycles.ToArray();
        var stock = session.State.Stocks.ToArray();
        for (var i = 0; i < 3; i++)
        {
            Assert.True(CanonicalNutritionExecutedConsumptionProjection
                .TryCaptureCurrent(session, out var observed));
            Assert.NotNull(observed);
            observed!.VerifyIndependent();
            Assert.Equal(hash.HexDigest, observed.ClosingWorldHashHex);
        }
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(cycles, session.State.Cycles.ToArray());
        Assert.Equal(stock, session.State.Stocks.ToArray());
    }

    [Fact]
    public void ReplayPreservesExactNutritionEvidenceWithoutSchemaMigration()
    {
        var session = ClosedLegacy();
        var original = CanonicalNutritionExecutedConsumptionProjection
            .FromCurrentExecutedCycle(session);
        var payload = session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(payload);
        var replay = CanonicalNutritionExecutedConsumptionProjection
            .FromCurrentExecutedCycle(restored);

        Assert.Equal(payload, restored.ExportSaveJson());
        Assert.Equal(session.CurrentHash, restored.CurrentHash);
        Assert.Equal(original.ClosingWorldHashHex, replay.ClosingWorldHashHex);
        Assert.Equal(original.TotalConsumedRaw, replay.TotalConsumedRaw);
        Assert.Equal(original.TotalUnmetReferenceRaw,
            replay.TotalUnmetReferenceRaw);
        Assert.Equal(original.Status, replay.Status);
        Assert.Equal(original.ExecutedUses.ToArray(), replay.ExecutedUses.ToArray());
        replay.VerifyIndependent();
        Assert.Throws<InvalidOperationException>(() =>
            original.RequireCurrent(restored));
    }

    [Fact]
    public void FutureTurnAndWorkerActionInvalidateOldEvidence()
    {
        var session = ClosedLegacy();
        var old = CanonicalNutritionExecutedConsumptionProjection
            .FromCurrentExecutedCycle(session);
        Assert.Null(session.AdvanceMovementTurn());
        Assert.False(CanonicalNutritionExecutedConsumptionProjection.TryCaptureCurrent(
            session, out var noOldReceipt));
        Assert.Null(noOldReceipt);
        Assert.Throws<InvalidOperationException>(() => old.RequireCurrent(session));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalNutritionExecutedConsumptionProjection
                .FromCurrentExecutedCycle(session));

        var other = ClosedLegacy();
        var proof = CanonicalNutritionExecutedConsumptionProjection
            .FromCurrentExecutedCycle(other);
        other.ReassignWorkers(other.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        Assert.False(CanonicalNutritionExecutedConsumptionProjection.TryCaptureCurrent(
            other, out var afterAction));
        Assert.Null(afterAction);
        Assert.Throws<InvalidOperationException>(() => proof.VerifyIndependent());
    }

    [Fact]
    public void ProvisionedExpeditionFoodIsAccountedSeparatelyFromLocalResidents()
    {
        var session = Cash();
        var start = session.InitialTile;
        session.ReassignWorkers(start, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 2UL);
        session.ReassignWorkers(start, EconomicActivityKind.Mining,
            EconomicActivityKind.Agriculture, 2UL);
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        var food = session.StockAt(start, new CommodityId(1U));
        Assert.True(food > 0UL);

        var neighbors = session.AdjacentWorkable(start).ToHashSet();
        var target = neighbors.SelectMany(session.AdjacentWorkable)
            .Where(x => x != start && !neighbors.Contains(x))
            .Distinct().OrderBy(x => x.FineStrategicCellId.Value).First();
        var order = session.OrderProvisionedLongDistanceTransit(
            start, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL, Math.Min(food, 2_000_000UL));

        Assert.NotNull(session.AdvanceMovementTurn());
        var projected = CanonicalNutritionExecutedConsumptionProjection
            .FromCurrentExecutedCycle(session);
        var actual = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var expedition = projected.ExecutedUses.Where(x =>
            x.Channel == CanonicalPhysicalFinalUseChannel.ExpeditionRations).ToArray();

        Assert.NotEmpty(expedition);
        Assert.All(expedition, x =>
        {
            Assert.Equal(order.OrderId, x.JourneyOrderId);
            Assert.Equal(1U, x.Commodity);
        });
        Assert.Equal(expedition.Aggregate(0UL,
            (s, x) => checked(s + x.ConsumedRaw)),
            projected.ExpeditionRationsConsumedRaw);
        Assert.Equal(actual.ActuallyConsumedRaw, projected.TotalConsumedRaw);
        Assert.Equal(projected.TotalConsumedRaw,
            checked(projected.ResidentLocalConsumedRaw +
                projected.ExpeditionRationsConsumedRaw));
        Assert.Equal(CanonicalNutritionEvidenceStatus.LegacyAggregateFoodOnly,
            projected.Status);
        Assert.Null(projected.NutritionalQuality);
        projected.VerifyIndependent();
    }

    [Fact]
    public void DistinctFoodCatalogCannotBeSynthesizedFromOneAggregateReceipt()
    {
        var session = ClosedLegacy();
        var projected = CanonicalNutritionExecutedConsumptionProjection
            .FromCurrentExecutedCycle(session);
        Assert.Equal([1U], projected.ExecutedUses.Select(x => x.Commodity)
            .Distinct().ToArray());
        Assert.NotEqual(6, projected.ObservedPositiveFoodCommodityCount);
        Assert.False(projected.HasPerPersonFoodVarietyConsumption);
        Assert.False(projected.CanComputeNutritionalQuality);

        // An experimental six-food value computed from caller-supplied
        // hypothetical data does not certify these real aggregate debits.
        var hypothetic = new CanonicalNutritionCandidateEvaluator().Evaluate(
            8UL, Enumerable.Range(1, 6)
                .Select(i => new CanonicalNutritionFoodIntake(
                    new CommodityId((uint)i), 100UL, 0.01)),
            new CanonicalNutritionCandidateParameters(1.0, 2.8));
        Assert.True(hypothetic.CandidateVarietyFactor > 1.0);
        Assert.Null(projected.NutritionalQuality);
        Assert.False(projected.CanAuthorizeProductivityEffect);
    }
}
