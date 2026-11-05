using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

/// <summary>
/// GA-NUTR-ARCH-01: non-live nutrition candidate. The existing food demand,
/// cash market, production, physical stocks and save/hash semantics remain
/// authoritative until a separate real-consumption integration is approved.
/// </summary>
public sealed class GaNutrArch01CandidateNutritionTests
{
    private static CanonicalNutritionFoodIntake[] Foods(
        ulong first, ulong second = 0UL, ulong third = 0UL,
        ulong fourth = 0UL, ulong fifth = 0UL, ulong sixth = 0UL,
        double unitsPerRaw = 0.25) =>
    [
        new(new CommodityId(1U), first, unitsPerRaw),
        new(new CommodityId(2U), second, unitsPerRaw),
        new(new CommodityId(3U), third, unitsPerRaw),
        new(new CommodityId(4U), fourth, unitsPerRaw),
        new(new CommodityId(5U), fifth, unitsPerRaw),
        new(new CommodityId(6U), sixth, unitsPerRaw)
    ];

    private static CanonicalNutritionCandidateParameters Experimental() =>
        new(sigma: 1.0, nu: 2.8);

    [Fact]
    public void UncalibratedCandidateHasNoLiveQualityOrProductionEffect()
    {
        var evaluated = new CanonicalNutritionCandidateEvaluator().Evaluate(
            4UL, Foods(8UL, 8UL, 8UL, 8UL, 8UL, 8UL), Experimental());

        Assert.True(evaluated.FoodPerPerson > 0.0);
        Assert.True(evaluated.Diversity > 0.0);
        Assert.Null(evaluated.CandidateQuality);
        Assert.False(evaluated.HasExplicitSufficiencyCurve);
        Assert.False(evaluated.CertifiesExecutedPhysicalConsumption);
        Assert.False(evaluated.AuthorizesProductivityChange);
        Assert.False(evaluated.AuthorizesRealPurchasingPower);
        Assert.False(evaluated.MutatesCanonicalWorldOrSave);
    }

    [Fact]
    public void AbsentFoodAndSingleFoodProduceNoArtificialVarietyBonus()
    {
        var evaluator = new CanonicalNutritionCandidateEvaluator();
        var starving = evaluator.Evaluate(4UL, Foods(0UL), Experimental(),
            f => f / (1.0 + f));
        Assert.Equal(0.0, starving.FoodPerPerson);
        Assert.Equal(0.0, starving.Diversity);
        Assert.Equal(0.0, starving.DiversityActivation);
        Assert.Equal(1.0, starving.CandidateVarietyFactor);
        Assert.Equal(0.0, starving.CandidateQuality!.Value);

        var onlyOne = evaluator.Evaluate(4UL, Foods(24UL), Experimental());
        Assert.Equal(0.0, onlyOne.Diversity, 12);
        Assert.Equal(1.0, onlyOne.CandidateVarietyFactor, 12);
    }

    [Fact]
    public void EqualTotalConsumptionWithSixVarietiesRaisesDiversityNotSupply()
    {
        var evaluator = new CanonicalNutritionCandidateEvaluator();
        var single = evaluator.Evaluate(4UL, Foods(24UL), Experimental());
        var diverse = evaluator.Evaluate(4UL,
            Foods(4UL, 4UL, 4UL, 4UL, 4UL, 4UL), Experimental());

        Assert.Equal(single.FoodPerPerson, diverse.FoodPerPerson, 12);
        Assert.Equal(single.DiversityActivation, diverse.DiversityActivation, 12);
        Assert.Equal(0.0, single.Diversity, 12);
        Assert.True(diverse.Diversity > single.Diversity);
        Assert.True(diverse.CandidateVarietyFactor > single.CandidateVarietyFactor);
    }

    [Fact]
    public void ScarcitySuppressesVarietyWhileAbundanceEnablesIt()
    {
        var evaluator = new CanonicalNutritionCandidateEvaluator();
        var scarce = evaluator.Evaluate(4UL,
            Foods(1UL, 1UL, 1UL, 1UL, 1UL, 1UL, unitsPerRaw: 0.02),
            Experimental());
        var sufficient = evaluator.Evaluate(4UL,
            Foods(8UL, 8UL, 8UL, 8UL, 8UL, 8UL), Experimental());

        Assert.True(scarce.Diversity > 0.0);
        Assert.True(scarce.DiversityActivation < sufficient.DiversityActivation);
        Assert.True(scarce.DiversityActivation < 0.000001);
        Assert.True(sufficient.CandidateVarietyFactor > scarce.CandidateVarietyFactor);
    }

    [Fact]
    public void ExplicitExperimentalSufficiencyCurveControlsCandidateQuality()
    {
        static double ExperimentalCurve(double f) => f / (1.0 + f);
        var result = new CanonicalNutritionCandidateEvaluator().Evaluate(
            4UL, Foods(8UL, 8UL, 8UL, 8UL, 8UL, 8UL),
            Experimental(), ExperimentalCurve);

        Assert.True(result.HasExplicitSufficiencyCurve);
        Assert.Equal(ExperimentalCurve(result.FoodPerPerson) *
            result.CandidateVarietyFactor, result.CandidateQuality!.Value, 12);
        Assert.False(result.AuthorizesProductivityChange);
    }

    [Fact]
    public void ExistingEconomicPreferenceShapeAgreesWithoutMoneyCoupling()
    {
        var foods = Foods(8UL, 4UL, 3UL, 1UL, 2UL, 0UL);
        var parameters = Experimental();
        var nutrition = new CanonicalNutritionCandidateEvaluator().Evaluate(
            4UL, foods, parameters);
        var monetaryModel = new EconomicFoodPreferenceEvaluator().Evaluate(
            population: 4.0,
            monetaryResidual: 10.0,
            foodQuantities: foods.Select(x =>
                x.ConsumedRaw * x.UnitsPerRaw),
            parameters: new EconomicFoodPreferenceParameters(
                parameters.Sigma, parameters.Nu, lambda: 0.0));

        Assert.Equal(monetaryModel.FoodPerResident, nutrition.FoodPerPerson, 12);
        Assert.Equal(monetaryModel.Diversity, nutrition.Diversity, 12);
        Assert.Equal(monetaryModel.AbundanceFactor,
            nutrition.DiversityActivation, 12);
        Assert.Equal(monetaryModel.VarietyMultiplier,
            nutrition.CandidateVarietyFactor, 12);
    }

    [Fact]
    public void CallingOrderCannotChangeSixFoodCandidate()
    {
        var evaluator = new CanonicalNutritionCandidateEvaluator();
        var original = Foods(9UL, 3UL, 1UL, 2UL, 11UL, 4UL);
        var a = evaluator.Evaluate(8UL, original, Experimental());
        var b = evaluator.Evaluate(8UL, original.Reverse(), Experimental());

        Assert.Equal(a.FoodPerPerson, b.FoodPerPerson);
        Assert.Equal(a.Diversity, b.Diversity);
        Assert.Equal(a.DiversityActivation, b.DiversityActivation);
        Assert.Equal(a.CandidateVarietyFactor, b.CandidateVarietyFactor);
    }

    [Fact]
    public void MissingOrDuplicateVarietiesCannotMasqueradeAsSixRealFoods()
    {
        var evaluator = new CanonicalNutritionCandidateEvaluator();
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(
            4UL, Foods(8UL).Take(1), Experimental()));
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(
            4UL, Foods(8UL).Select(x => x with
            {
                Commodity = new CommodityId(1U)
            }), Experimental()));
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(
            4UL, Foods(8UL).Select(x => x with
            {
                UnitsPerRaw = double.NaN
            }), Experimental()));
        Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.Evaluate(
            0UL, Foods(8UL), Experimental()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CanonicalNutritionCandidateParameters(0.0, 2.8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CanonicalNutritionCandidateParameters(1.0, double.NaN));
    }

    [Fact]
    public void CandidateEvaluationDoesNotChangeCanonicalHashSaveOrCycles()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();

        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();
        var cycle = session.State.Cycles[^1];

        // Hypothetical values only: NEVER inferred from the current legacy
        // single-commodity production/consumption or claimed as real nutrition.
        _ = new CanonicalNutritionCandidateEvaluator().Evaluate(
            8UL, Foods(3UL, 3UL, 3UL, 3UL, 3UL, 3UL),
            Experimental(), f => f / (1.0 + f));

        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(cycle, session.State.Cycles[^1]);
        Assert.Equal(save, CanonicalPhysicalTacticalSession
            .RestoreSaveJson(save).ExportSaveJson());
    }

    [Fact]
    public void InvalidOrOverflowingCandidateCurveIsNotAccepted()
    {
        var evaluator = new CanonicalNutritionCandidateEvaluator();
        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(
            4UL, Foods(8UL), Experimental(), f => double.NaN));
        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(
            4UL, Foods(8UL), Experimental(), f => -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.Evaluate(
            4UL, Foods(ulong.MaxValue, unitsPerRaw: 1e308), Experimental()));
    }
}
