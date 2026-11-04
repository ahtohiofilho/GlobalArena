using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// VS109 contract: a market price signal is not a ceiling on human food
/// consumption. Historic quota mathematics persists ONLY for V9-V20 replay.
/// Nutrition and a productivity multiplier are deliberately not inferred.
/// </summary>
public sealed class GaVs109FoodPriceNutritionBoundaryTests
{
    [Fact]
    public void PricePressureReferenceCanMoveIndependentlyOfHistoricIntakeQuota()
    {
        var residents = 8UL;
        var legacy = LegacyPhysicalFoodIntakeTarget.FromResidents(residents);
        var lowPricePressure = PhysicalFoodPricePressureReference.FromResidents(
            residents, 250_000UL);
        var highPricePressure = PhysicalFoodPricePressureReference.FromResidents(
            residents, 1_250_000UL);

        Assert.Equal(4_000_000UL, legacy.RawQuota);
        Assert.Equal(2_000_000UL, lowPricePressure.RawPressure);
        Assert.Equal(10_000_000UL, highPricePressure.RawPressure);

        // An arbitrary supply may exceed BOTH pressure references; price
        // pressure calibrates market prices but never rejects this quantity.
        var offered = new BigInteger(12_000_000);
        var low = CanonicalEndogenousLocalPriceComparison.Calculate(
            offered, new BigInteger(lowPricePressure.RawPressure), sensitivity: 1);
        var high = CanonicalEndogenousLocalPriceComparison.Calculate(
            offered, new BigInteger(highPricePressure.RawPressure), sensitivity: 1);
        Assert.True(high.Average.CompareTo(low.Average) > 0);
        Assert.True(high.Revenue.CompareTo(low.Revenue) > 0);
        Assert.Equal(12_000_000, (long)offered);

        // The only cap is visibly scoped to historical saved rulesets.
        Assert.Equal(legacy.RawQuota, legacy.DeliverDirect(12_000_000UL));
        Assert.Equal(legacy.RawQuota,
            legacy.LimitLegacyQuotedPurchase(12_000_000UL, 12_000_000UL));
    }

    [Fact]
    public void HistoricalIntakeArithmeticFailsClosedAndDoesNotInventNutrition()
    {
        var target = LegacyPhysicalFoodIntakeTarget.FromResidents(3UL);
        Assert.Equal(1_500_000UL, target.RawQuota);
        Assert.Equal(0UL, target.DeliverDirect(0UL));
        Assert.Equal(750_000UL, target.DeliverDirect(750_000UL));
        Assert.Equal(700_000UL, target.LimitLegacyQuotedPurchase(700_000UL, 10_000_000UL));
        Assert.Equal(1_500_000UL, target.LimitLegacyQuotedPurchase(10_000_000UL, 10_000_000UL));
        Assert.Equal(800_000UL, target.LegacyResidual(700_000UL));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.LegacyResidual(2_000_000UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalFoodPricePressureReference.FromResidents(1UL, 0UL));
        Assert.Throws<OverflowException>(() =>
            PhysicalFoodPricePressureReference.FromResidents(ulong.MaxValue, 2UL));
        Assert.Throws<OverflowException>(() =>
            LegacyPhysicalFoodIntakeTarget.FromResidents(ulong.MaxValue));
    }

    [Fact]
    public void SessionPriceReferenceAndLegacyQuotaAreDistinctReadModelsWithBitExactReplay()
    {
        var game = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement);
        var originalHash = game.CurrentHash;
        var originalSave = game.ExportSaveJson();

        var pressure = game.FoodPriceReferenceAt(game.InitialTile);
        var quota = game.LegacyFoodIntakeTargetAt(game.InitialTile);
        Assert.Equal(4_000_000UL, pressure.RawPressure);
        Assert.Equal(4_000_000UL, quota.RawQuota);
        Assert.Equal(quota.RawQuota, game.FoodDemandAt(game.InitialTile));
        Assert.Equal(CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker,
            LegacyPhysicalFoodIntakeTarget.RawPerResident);
        Assert.Equal(originalHash, game.CurrentHash);
        Assert.Equal(originalSave, game.ExportSaveJson());

        var beforeQuote = game.QuoteNextLocalFoodMarket(
            game.InitialTile, referenceBudgetPerWorker: 40);
        Assert.Equal(pressure.RawPressure, beforeQuote.RawFoodNeed);
        Assert.Equal(originalHash, game.CurrentHash);

        var cycle = Assert.IsType<PhysicalEconomicCycle>(game.AdvanceMovementTurn());
        Assert.Equal(1UL, cycle.MovementTurn);
        Assert.Equal(4_000_000UL,
            checked(cycle.FoodConsumedRaw + cycle.FoodUnmetRaw));
        var saved = game.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(saved, restored.ExportSaveJson());
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(cycle, Assert.Single(restored.State.Cycles));
        Assert.Equal(game.FoodPriceReferenceAt(game.InitialTile),
            restored.FoodPriceReferenceAt(restored.InitialTile));
        Assert.Equal(game.LegacyFoodIntakeTargetAt(game.InitialTile),
            restored.LegacyFoodIntakeTargetAt(restored.InitialTile));
    }
}
