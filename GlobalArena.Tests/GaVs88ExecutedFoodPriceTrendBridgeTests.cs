using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs88ExecutedFoodPriceTrendBridgeTests
{
    private static readonly ExperimentalSharedFreightCongestionPolicy Congestion =
        new(1, 1_000_000);

    private static CanonicalPrimitiveSiteDiscoveryPolicy Policy() => new(
        new EconomicPrimitiveCommodityMapping(
            new CommodityId(7), new CommodityId(8), new CommodityId(9)),
        new[] {
            new CanonicalPrimitiveDemandRate(new CommodityId(7),
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(new CommodityId(8), 125_000),
            new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalCycleStampedProductionProjection Proof,
        CanonicalExecutedLocalFoodPriceWitness Executed,
        CanonicalExecutedFoodPriceTrendBridge Bridge) Build(int step = 100)
    {
        var (f, _, portfolio) = GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var session = f.Session;
        // VS67 reassigns a worker after cycle 2, so cycle 3 must close
        // before capturing any authentic prior VS85 source evidence.
        Assert.NotNull(session.AdvanceMovementTurn());
        var sites = session.DiscoverPrimitiveEconomicSites(Policy());
        var freight = CanonicalSimultaneousFreightReference.FromSites(
            session, sites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        var rates = sites.Flows.SupplyFlows
            .Where(x => x.RawQuantity > 0)
            .Select(x => x.Activity).Distinct().Select(x =>
                new CanonicalWorkforceActivityRealizationPolicy(x, 500, 100));
        var prior = CanonicalWorkforceRealizationProjection.FromSites(
            session, sites, freight, rates,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion);
        var feedback = CanonicalLaggedProductionResponse.FromFrozenProjection(
            session, sites, freight, prior, 100,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion);
        var receipt = CanonicalCycleStampedPriceReceipt.Capture(
            session, feedback);
        Assert.NotNull(session.AdvanceMovementTurn());
        var currentSites = session.DiscoverPrimitiveEconomicSites(Policy());
        var currentFreight = CanonicalSimultaneousFreightReference.FromSites(
            session, currentSites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        var proof = receipt.ApplyAfterNextSettledCycle(
            session, currentSites, currentFreight,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion);
        var paid = CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(session);
        var bridge = CanonicalExecutedFoodPriceTrendBridge.FromConsecutiveSettlements(
            session, proof, paid, step);
        return (f, proof, paid, bridge);
    }

    [Fact]
    public void MatchedExecutedFoodPaymentsHaveSameNominalUnitExactWeightsAndNoSettlementAuthority()
    {
        var (f, proof, paid, bridge) = Build();
        var save = f.Session.ExportSaveJson();
        Assert.True(bridge.UsesActualExecutedFoodCashReceipts);
        Assert.False(bridge.UsesVs78HypotheticalPricesAsPayments);
        Assert.False(bridge.EstablishesActualOtherCommodityPrices);
        Assert.False(bridge.IsEconomicCausalityProof);
        Assert.False(bridge.ChangesActualWorkforceOrProduction);
        Assert.False(bridge.CreatesInventoryOrCredit);
        Assert.False(bridge.AuthorizesRealSettlement);
        Assert.Equal(proof.NextRealCompletedEconomicCycleTurn,
            bridge.TargetCycleMovementTurn);
        Assert.Equal(f.Session.State.Cycles[^2].MovementTurn,
            bridge.SourceCycleMovementTurn);
        Assert.Equal(paid.CompletedEconomicCycleMovementTurn,
            bridge.TargetCycleMovementTurn);
        Assert.True(bridge.HasComparableExecutedFoodPrice);
        Assert.NotEmpty(bridge.MatchedTiles);
        Assert.Equal(bridge.MatchedTiles.Count,
            bridge.MatchedTiles.Select(x => x.FineTile).Distinct().Count());

        var prior = BigInteger.Zero;
        var next = BigInteger.Zero;
        var previousLocal = f.Session.State.FoodCash!.Receipts
            .Where(x => x.MovementTurn == bridge.SourceCycleMovementTurn)
            .ToDictionary(x => x.Tile.FineStrategicCellId.Value);
        var currentLocal = paid.ExecutedReceipts.ToDictionary(x => x.FineTile);
        foreach (var row in bridge.MatchedTiles)
        {
            var before = previousLocal[row.FineTile];
            var after = currentLocal[row.FineTile];
            Assert.Equal(before.PriceMicrocreditsPerUnit,
                row.SourcePriceMicrocreditsPerWholeUnit);
            Assert.Equal(after.ExecutedPriceMicrocreditsPerWholeUnit,
                row.TargetPriceMicrocreditsPerWholeUnit);
            Assert.Equal(Math.Min(before.FoodRaw, after.ExecutedFoodRaw),
                row.CommonObservedFoodRaw);
            Assert.True(before.PaidMicrocredits > 0UL);
            Assert.True(after.ActuallyPaidMicrocredits > 0UL);
            prior += (BigInteger)row.CommonObservedFoodRaw *
                row.SourcePriceMicrocreditsPerWholeUnit;
            next += (BigInteger)row.CommonObservedFoodRaw *
                row.TargetPriceMicrocreditsPerWholeUnit;
        }
        Assert.Equal(prior, bridge.SourceCommonWeightedNominalPriceRaw);
        Assert.Equal(next, bridge.TargetCommonWeightedNominalPriceRaw);
        var expectedDelta = checked((int)((next - prior) *
            bridge.MaximumAdvisoryStepPermille / (next + prior)));
        Assert.Equal(expectedDelta, bridge.DirectionalAdvisoryDeltaPermille);
        Assert.Equal(Math.Clamp(
            bridge.PriorHypotheticalAgricultureUtilizationPermille + expectedDelta,
            0, 1000), bridge.SuggestedAgricultureUtilizationPermille);
        Assert.Equal(100, bridge.MaximumAdvisoryStepPermille);
        bridge.VerifyIndependent();
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(250)]
    public void AdvisoryStepRemainsBoundedAndCannotActivateEconomicExecution(int step)
    {
        var (f, _, _, bridge) = Build(step);
        Assert.InRange(bridge.DirectionalAdvisoryDeltaPermille, -step, step);
        Assert.InRange(bridge.SuggestedAgricultureUtilizationPermille, 0, 1000);
        Assert.False(bridge.ChangesActualWorkforceOrProduction);
        Assert.False(bridge.AuthorizesRealSettlement);
        var baseline = f.Session.ExportSaveJson();
        bridge.VerifyIndependent();
        Assert.Equal(baseline, f.Session.ExportSaveJson());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(251)]
    public void InvalidAdvisoryGainCannotBeUsed(int step)
    {
        var (f, proof, paid, _) = Build();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalExecutedFoodPriceTrendBridge.FromConsecutiveSettlements(
                f.Session, proof, paid, step));
    }

    [Fact]
    public void CrossSessionRestorationNeverTransplantsLivePriceAuthority()
    {
        var (f, proof, paid, bridge) = Build();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedFoodPriceTrendBridge.FromConsecutiveSettlements(
                restored, proof, paid, 100));
        var replay = CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(
            restored);
        Assert.Equal(paid.ExecutedReceipts, replay.ExecutedReceipts);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedFoodPriceTrendBridge.FromConsecutiveSettlements(
                restored, proof, replay, 100));
    }

    [Fact]
    public void CurrentStateMoveInvalidatesPriorExecutionAndAdvisory()
    {
        var (f, proof, paid, bridge) = Build();
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            bridge.VerifyIndependent());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedFoodPriceTrendBridge.FromConsecutiveSettlements(
                f.Session, proof, paid, 100));
    }

    [Fact]
    public void NetworkChangesCannotReuseSignedFoodHistoryAsEconomicAuthority()
    {
        var (f, proof, paid, bridge) = Build();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedFoodPriceTrendBridge.FromConsecutiveSettlements(
                f.Session, proof, paid, 100));
    }

    [Fact]
    public void SameWitnessProducesDeterministicReadOnlyResults()
    {
        var (f, proof, paid, bridge) = Build();
        var save = f.Session.ExportSaveJson();
        for (var i = 0; i < 3; i++)
        {
            var another = CanonicalExecutedFoodPriceTrendBridge
                .FromConsecutiveSettlements(f.Session, proof, paid, 100);
            Assert.Equal(bridge.MatchedTiles, another.MatchedTiles);
            Assert.Equal(bridge.DirectionalAdvisoryDeltaPermille,
                another.DirectionalAdvisoryDeltaPermille);
            Assert.Equal(bridge.SuggestedAgricultureUtilizationPermille,
                another.SuggestedAgricultureUtilizationPermille);
            Assert.Equal(bridge.SourceCommonWeightedNominalPriceRaw,
                another.SourceCommonWeightedNominalPriceRaw);
            Assert.Equal(bridge.TargetCommonWeightedNominalPriceRaw,
                another.TargetCommonWeightedNominalPriceRaw);
            Assert.Equal(save, f.Session.ExportSaveJson());
        }
    }
}
