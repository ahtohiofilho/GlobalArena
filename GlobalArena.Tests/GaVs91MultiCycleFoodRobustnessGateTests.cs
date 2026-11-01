using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs91MultiCycleFoodRobustnessGateTests
{
    private static CanonicalFoodRobustnessScenario Policy(
        string id = "baseline", int gain = 100, int coverage = 0,
        int shift = 1000, int minimumWindows = 1,
        int minimumNonzero = 0, int maximumReversals = 1) =>
        new(id, gain, coverage, shift, minimumWindows,
            minimumNonzero, maximumReversals);

    private static CanonicalMultiCycleExecutedFoodWindow Window(
        ulong sourceTurn, ulong targetTurn, long sourceFood = 100,
        long targetFood = 100, long common = 100,
        long sourceWeightedPrice = 1000, long targetWeightedPrice = 2000,
        int tiles = 1) =>
        new(sourceTurn, targetTurn, sourceFood, targetFood, common,
            sourceWeightedPrice, targetWeightedPrice, tiles);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalMultiCycleFoodPriceStabilityReport History,
        CanonicalMultiCycleFoodRobustnessGate Gate) Build()
    {
        var (f, _, history) = GaVs90MultiCycleFoodPriceStabilityTests.Build();
        var gate = CanonicalMultiCycleFoodRobustnessGate.FromVerifiedHistory(
            f.Session, history,
            new[] {
                Policy("baseline", 100),
                Policy("strict-volume", 150, coverage: 1000, shift: 0),
                Policy("directional", 250, minimumNonzero: 1)
            });
        return (f, history, gate);
    }

    [Fact]
    public void CertifiedRealCyclePolicyStressHasNoAuthorityToModifyWorld()
    {
        var (f, history, gate) = Build();
        Assert.True(gate.UsesOnlyActuallyExecutedLocalFoodSales);
        Assert.False(gate.ClaimsMeasuredCausalSupplyOrDemandShock);
        Assert.False(gate.ClaimsPredictiveAccuracy);
        Assert.False(gate.PromotesAnyGainAutomatically);
        Assert.False(gate.EstablishesNonFoodMarketPrices);
        Assert.False(gate.CanAuthorizeRealProductionOrSettlement);
        Assert.False(gate.ChangesWorldOrSave);
        Assert.False(gate.CreatesInventoryOrCredit);
        Assert.Equal(3, gate.Scenarios.Count);
        Assert.Equal(3, gate.Observations.Count);
        Assert.Equal(history.Windows.Count, gate.Observations[0].ObservedWindows);
        Assert.Equal(history.ComparableWindowCount,
            gate.Observations[0].ActuallyComparableWindows);
        Assert.All(gate.Observations, x =>
        {
            Assert.InRange(x.WindowsMeetingBothCoverageAndVolumeShift,
                0, x.ActuallyComparableWindows);
            Assert.InRange(x.NonzeroQualifiedDirectionalWindows,
                0, x.WindowsMeetingBothCoverageAndVolumeShift);
            Assert.InRange(x.AdjacentQualifiedNonzeroReversals,
                0, x.WindowsMeetingBothCoverageAndVolumeShift);
        });
        Assert.True(gate.Observations[0].WithinDeclaredCriteria);
        gate.VerifyIndependent();
    }

    [Fact]
    public void LowCoverageCannotPassAsExecutedPriceEvidence()
    {
        var windows = new[] {
            Window(1, 2, common: 20),
            Window(2, 3, common: 30)
        };
        var score = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(windows,
                Policy(coverage: 500));
        Assert.Equal(
            CanonicalFoodRobustnessDiagnostic.InsufficientPhysicalCoverage,
            score.Diagnostic);
        Assert.Equal(2, score.ActuallyComparableWindows);
        Assert.Equal(2, score.RejectedForCoverage);
        Assert.Equal(0, score.WindowsMeetingPhysicalCoverage);
    }

    [Fact]
    public void ObservedSalesVolumeShiftUsesExactRelativePhysicalVolumes()
    {
        var windows = new[] {
            Window(1, 2, sourceFood: 100, targetFood: 200,
                common: 90),
            Window(2, 3, sourceFood: 200, targetFood: 100,
                common: 90)
        };
        var strict = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(windows,
                Policy(coverage: 400, shift: 499));
        Assert.Equal(
            CanonicalFoodRobustnessDiagnostic.ExcessiveObservedVolumeShift,
            strict.Diagnostic);
        Assert.Equal(2, strict.WindowsMeetingPhysicalCoverage);
        Assert.Equal(2, strict.RejectedForVolumeShift);
        var boundary = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(windows,
                Policy(coverage: 400, shift: 500));
        Assert.Equal(
            CanonicalFoodRobustnessDiagnostic.WithinDeclaredObservationalCriteria,
            boundary.Diagnostic);
        Assert.Equal(2, boundary.WindowsMeetingBothCoverageAndVolumeShift);
    }

    [Fact]
    public void MissingPaidSalesAreNotImputedAsAZeroNominalFoodPrice()
    {
        var empty = new[] {
            Window(1, 2, sourceFood: 0, targetFood: 100,
                common: 0, sourceWeightedPrice: 0,
                targetWeightedPrice: 0, tiles: 0),
            Window(2, 3, sourceFood: 100, targetFood: 100,
                common: 0, sourceWeightedPrice: 0,
                targetWeightedPrice: 0, tiles: 0)
        };
        var result = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(empty, Policy());
        Assert.Equal(
            CanonicalFoodRobustnessDiagnostic.NoComparableExecutedFoodSales,
            result.Diagnostic);
        Assert.Equal(0, result.ActuallyComparableWindows);
        Assert.Equal(0, result.NonzeroQualifiedDirectionalWindows);
    }

    [Fact]
    public void RequiredMinimumHistoryAndNonzeroEvidenceAreSeparateCriteria()
    {
        var windows = new[] {
            Window(1, 2, common: 20),
            Window(2, 3)
        };
        var shortSample = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(windows,
                Policy(coverage: 800, minimumWindows: 2));
        Assert.Equal(
            CanonicalFoodRobustnessDiagnostic.InsufficientComparableHistory,
            shortSample.Diagnostic);
        Assert.Equal(1,
            shortSample.WindowsMeetingBothCoverageAndVolumeShift);

        var flat = new[] {
            Window(1, 2, sourceWeightedPrice: 1000,
                targetWeightedPrice: 1000),
            Window(2, 3, sourceWeightedPrice: 1000,
                targetWeightedPrice: 1000)
        };
        var noDirectionalSignal = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(flat,
                Policy(minimumNonzero: 1));
        Assert.Equal(
            CanonicalFoodRobustnessDiagnostic
                .InsufficientNonzeroDirectionalEvidence,
            noDirectionalSignal.Diagnostic);
        Assert.Equal(2, noDirectionalSignal.WindowsMeetingBothCoverageAndVolumeShift);
        Assert.Equal(0, noDirectionalSignal.NonzeroQualifiedDirectionalWindows);
    }

    [Fact]
    public void AdjacentOppositePaidPriceSignsAreReversalsNotPredictions()
    {
        var windows = new[] {
            Window(1, 2, sourceWeightedPrice: 1000,
                targetWeightedPrice: 2000),
            Window(2, 3, sourceWeightedPrice: 2000,
                targetWeightedPrice: 1000)
        };
        var fails = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(windows,
                Policy(maximumReversals: 0, minimumNonzero: 1));
        Assert.Equal(1, fails.AdjacentQualifiedNonzeroReversals);
        Assert.Equal(
            CanonicalFoodRobustnessDiagnostic.ExcessiveObservedReversals,
            fails.Diagnostic);
        var passes = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(windows,
                Policy(maximumReversals: 1, minimumNonzero: 1));
        Assert.True(passes.WithinDeclaredCriteria);
        Assert.InRange(passes.MaximumAbsoluteQualifiedDeltaPermille, 1, 100);
    }

    [Fact]
    public void InsufficientCoverageAndObservedZeroBreakReversalContinuity()
    {
        var windows = new[] {
            Window(1, 2, sourceWeightedPrice: 1000,
                targetWeightedPrice: 2000),
            Window(2, 3, common: 10, sourceWeightedPrice: 2000,
                targetWeightedPrice: 1000),
            Window(3, 4, sourceWeightedPrice: 2000,
                targetWeightedPrice: 1000)
        };
        // Public pure gate is intentionally bounded to VS90's 2..7 windows.
        var result = CanonicalMultiCycleFoodRobustnessGate
            .EvaluateHypotheticalCriteria(windows,
                Policy(coverage: 800, maximumReversals: 0));
        Assert.Equal(0, result.AdjacentQualifiedNonzeroReversals);
        Assert.True(result.WithinDeclaredCriteria);
    }

    [Fact]
    public void NoncanonicalThresholdsAndUncertifiedGainsFailClosed()
    {
        var (f, history, _) = Build();
        foreach (var invalid in new[] {
            Policy(id: ""),
            Policy(id: "bad id"),
            Policy(gain: 0),
            Policy(gain: 251),
            Policy(coverage: -1),
            Policy(coverage: 1001),
            Policy(shift: -1),
            Policy(shift: 1001),
            Policy(minimumWindows: 0),
            Policy(minimumWindows: 3),
            Policy(minimumNonzero: 2),
            Policy(maximumReversals: -1),
            Policy(maximumReversals: 2)
        })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CanonicalMultiCycleFoodRobustnessGate
                    .FromVerifiedHistory(f.Session, history, new[] { invalid }));
        }
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalMultiCycleFoodRobustnessGate.FromVerifiedHistory(
                f.Session, history, new[] { Policy(gain: 37) }));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalMultiCycleFoodRobustnessGate.FromVerifiedHistory(
                f.Session, history, new[] { Policy(), Policy() }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalMultiCycleFoodRobustnessGate.FromVerifiedHistory(
                f.Session, history, Array.Empty<CanonicalFoodRobustnessScenario>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalMultiCycleFoodRobustnessGate.FromVerifiedHistory(
                f.Session, history, Enumerable.Range(1, 9)
                    .Select(x => Policy($"scenario-{x}"))));
    }

    [Fact]
    public void WorldAdvanceOrForeignSaveInvalidatesPriorRobustnessEvidence()
    {
        var (f, history, gate) = Build();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            gate.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalMultiCycleFoodRobustnessGate.FromVerifiedHistory(
                restored, history, gate.Scenarios));
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            gate.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            gate.VerifyIndependent());
    }

    [Fact]
    public void NetworkRevisionInvalidatesDiagnosticsWithoutWritingBack()
    {
        var (f, history, gate) = Build();
        f.Session.TransportNetwork.SetAccess(
            f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            gate.RequireCurrent(f.Session));
    }

    [Fact]
    public void RepeatedPolicyEvaluationIsDeterministicAndDoesNotChangeSave()
    {
        var (f, history, gate) = Build();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        for (var i = 0; i < 3; i++)
        {
            var replay = CanonicalMultiCycleFoodRobustnessGate.FromVerifiedHistory(
                f.Session, history, gate.Scenarios);
            Assert.Equal(gate.Observations, replay.Observations);
            Assert.Equal(gate.Scenarios, replay.Scenarios);
            replay.VerifyIndependent();
            Assert.Equal(save, f.Session.ExportSaveJson());
            Assert.Equal(hash, f.Session.CurrentHash);
        }
    }
}
