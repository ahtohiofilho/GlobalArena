using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs92ExecutedFoodSampleAuditTests
{
    private static CanonicalFoodSampleScenario Scenario(
        string name = "baseline", int tiles = 1, int coverage = 0,
        int maxLargest = 1000, int eligible = 1, int persistent = 0) =>
        new(name, tiles, coverage, maxLargest, eligible, persistent);

    private static CanonicalFoodSampleWindow Window(
        ulong sourceTurn, ulong targetTurn, int sourceTiles = 2,
        int targetTiles = 2, int matchedTiles = 2,
        long sourceFood = 100, long targetFood = 100,
        long matchedFood = 100, long largest = 60) =>
        new(sourceTurn, targetTurn, sourceTiles, targetTiles, matchedTiles,
            sourceFood, targetFood, matchedFood, largest);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalMultiCycleFoodRobustnessGate Gate91,
        CanonicalExecutedFoodSampleAudit Audit) Build()
    {
        var (f, _, g) = GaVs91MultiCycleFoodRobustnessGateTests.Build();
        var audit = CanonicalExecutedFoodSampleAudit.FromVerifiedRobustness(
            f.Session, g, new[] {
                Scenario(),
                Scenario("min-two-tiles", tiles: 2),
                Scenario("max-half-concentration", maxLargest: 500)
            });
        return (f, g, audit);
    }

    [Fact]
    public void CanonicalThreeCycleFoodSampleIsAuditedFromRealPaidTilesOnly()
    {
        var (f, gate, audit) = Build();
        var historic = gate.SourceHistory.Windows;
        Assert.Equal(2, audit.Windows.Count);
        Assert.Equal(3, audit.Assessments.Count);
        Assert.True(audit.MeasuresOnlyExecutedLocalFoodSample);
        Assert.False(audit.ProvesWholeEconomyRepresentativeness);
        Assert.False(audit.ProvesCompleteFoodDemandCoverage);
        Assert.False(audit.EstablishesPricesForOtherGoods);
        Assert.False(audit.ProvesCausalSupplyDemandEffects);
        Assert.False(audit.PromotesEconomicParameterAutomatically);
        Assert.False(audit.ChangesCanonicalWorldOrSave);
        Assert.False(audit.CanAuthorizeSettlement);
        Assert.False(audit.CreatesMoneyStocksOrCredit);
        Assert.Equal(historic.Select(x => x.CommonExecutedFoodRaw),
            audit.Windows.Select(x => x.MatchedCommonFoodRaw));
        Assert.Equal(historic.Select(x => x.MatchedTileCount),
            audit.Windows.Select(x => x.MatchedPositivePaymentTiles));
        foreach (var row in audit.Windows)
        {
            Assert.InRange(row.MatchedPositivePaymentTiles, 0,
                Math.Min(row.SourcePositivePaymentTiles,
                    row.TargetPositivePaymentTiles));
            Assert.True(row.LargestSingleTileCommonFoodRaw <=
                row.MatchedCommonFoodRaw);
            Assert.True(row.MatchedCommonFoodRaw <= row.SourceExecutedFoodRaw);
            Assert.True(row.MatchedCommonFoodRaw <= row.TargetExecutedFoodRaw);
        }
        Assert.True(audit.Assessments[0].WithinDeclaredCriteria);
        Assert.Equal(audit.Windows.Count,
            audit.Assessments[0].TotalWindows);
        audit.VerifyIndependent();
    }

    [Fact]
    public void TileConcentrationIsCalculatedFromPaidFoodAndMatchedPhysicalQuantity()
    {
        var (f, gate, audit) = Build();
        var receipts = f.Session.State.FoodCash!.Receipts;
        foreach (var w in audit.Windows)
        {
            var b = receipts.Where(x =>
                x.MovementTurn == w.SourceCycleMovementTurn)
                .ToDictionary(x => x.Tile.FineStrategicCellId.Value);
            var a = receipts.Where(x =>
                x.MovementTurn == w.TargetCycleMovementTurn)
                .ToDictionary(x => x.Tile.FineStrategicCellId.Value);
            var paired = new List<ulong>();
            foreach (var (tile, receipt) in b)
            {
                if (a.TryGetValue(tile, out var target) &&
                    receipt.PaidMicrocredits > 0 &&
                    target.PaidMicrocredits > 0 &&
                    receipt.PriceMicrocreditsPerUnit > 0 &&
                    target.PriceMicrocreditsPerUnit > 0)
                    paired.Add(Math.Min(receipt.FoodRaw, target.FoodRaw));
            }
            Assert.Equal(paired.Count, w.MatchedPositivePaymentTiles);
            Assert.Equal(paired.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x), w.MatchedCommonFoodRaw);
            Assert.Equal(paired.Count == 0 ? BigInteger.Zero :
                new BigInteger(paired.Max()),
                w.LargestSingleTileCommonFoodRaw);
        }
        audit.VerifyIndependent();
    }

    [Fact]
    public void PureCriteriaDistinguishNoSampleAndInadequateTileDiversity()
    {
        var absent = new[] {
            Window(1, 2, 0, 0, 0, 0, 0, 0, 0),
            Window(2, 3, 1, 1, 0, 100, 100, 0, 0)
        };
        var none = CanonicalExecutedFoodSampleAudit.EvaluateHypotheticalCriteria(
            absent, 0, Scenario());
        Assert.Equal(CanonicalFoodSampleDiagnostic.NoPairedPaidFoodPrices,
            none.Diagnostic);
        var narrow = new[] {
            Window(1, 2, 1, 1, 1, 100, 100, 100, 100),
            Window(2, 3)
        };
        var one = CanonicalExecutedFoodSampleAudit.EvaluateHypotheticalCriteria(
            narrow, 0, Scenario(tiles: 2));
        Assert.Equal(CanonicalFoodSampleDiagnostic.InsufficientMatchedTileDiversity,
            one.Diagnostic);
        Assert.Equal(1, one.PairedWindows);
    }

    [Fact]
    public void SourceAndTargetPhysicalCoverageRequireExactRationalThresholds()
    {
        var window = new[] {
            Window(1, 2, matchedFood: 50, largest: 30),
            Window(2, 3, matchedFood: 50, largest: 30)
        };
        var fail = CanonicalExecutedFoodSampleAudit.EvaluateHypotheticalCriteria(
            window, 2, Scenario(coverage: 501));
        Assert.Equal(CanonicalFoodSampleDiagnostic.InsufficientPairedPhysicalCoverage,
            fail.Diagnostic);
        var exact = CanonicalExecutedFoodSampleAudit.EvaluateHypotheticalCriteria(
            window, 2, Scenario(coverage: 500));
        Assert.True(exact.WithinDeclaredCriteria);
        var targetHeavy = new[] {
            Window(1, 2, targetFood: 200, matchedFood: 100, largest: 60),
            Window(2, 3, targetFood: 200, matchedFood: 100, largest: 60)
        };
        var no = CanonicalExecutedFoodSampleAudit.EvaluateHypotheticalCriteria(
            targetHeavy, 2, Scenario(coverage: 501));
        Assert.Equal(CanonicalFoodSampleDiagnostic.InsufficientPairedPhysicalCoverage,
            no.Diagnostic);
    }

    [Fact]
    public void LargestMatchedTilePreventsOverclaimingDiverseMarketSample()
    {
        var dominant = new[] {
            Window(1, 2, matchedFood: 100, largest: 90),
            Window(2, 3, matchedFood: 100, largest: 90)
        };
        var strict = CanonicalExecutedFoodSampleAudit.EvaluateHypotheticalCriteria(
            dominant, 2, Scenario(maxLargest: 899));
        Assert.Equal(CanonicalFoodSampleDiagnostic.ExcessiveSingleTileConcentration,
            strict.Diagnostic);
        var exact = CanonicalExecutedFoodSampleAudit.EvaluateHypotheticalCriteria(
            dominant, 2, Scenario(maxLargest: 900));
        Assert.True(exact.WithinDeclaredCriteria);
    }

    [Fact]
    public void InsufficientEligibleWindowsAndLongitudinalContinuityAreDistinct()
    {
        var windows = new[] {
            Window(1, 2, matchedFood: 100, largest: 60),
            Window(2, 3, matchedFood: 100, largest: 90)
        };
        var insufficient = CanonicalExecutedFoodSampleAudit
            .EvaluateHypotheticalCriteria(windows, 0,
                Scenario(maxLargest: 600, eligible: 2));
        Assert.Equal(CanonicalFoodSampleDiagnostic.InsufficientEligibleWindows,
            insufficient.Diagnostic);
        Assert.Equal(1, insufficient.FullyEligibleWindows);
        var interrupted = CanonicalExecutedFoodSampleAudit
            .EvaluateHypotheticalCriteria(windows, 0,
                Scenario(persistent: 1));
        Assert.Equal(CanonicalFoodSampleDiagnostic.InsufficientPersistentTiles,
            interrupted.Diagnostic);
        var all = CanonicalExecutedFoodSampleAudit
            .EvaluateHypotheticalCriteria(windows, 1,
                Scenario(persistent: 1));
        Assert.True(all.WithinDeclaredCriteria);
    }

    [Fact]
    public void InvalidThresholdsImpossibleSharesAndDuplicatePoliciesAreRejected()
    {
        var (f, gate, _) = Build();
        var malformed = new[] {
            Scenario(name: ""),
            Scenario(name: "bad space"),
            Scenario(tiles: 0),
            Scenario(tiles: 513),
            Scenario(coverage: -1),
            Scenario(coverage: 1001),
            Scenario(maxLargest: -1),
            Scenario(maxLargest: 1001),
            Scenario(eligible: 0),
            Scenario(eligible: 3),
            Scenario(persistent: -1),
            Scenario(persistent: 513)
        };
        foreach (var policy in malformed)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CanonicalExecutedFoodSampleAudit.FromVerifiedRobustness(
                    f.Session, gate, new[] { policy }));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedFoodSampleAudit.FromVerifiedRobustness(
                f.Session, gate, new[] { Scenario(), Scenario() }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalExecutedFoodSampleAudit.FromVerifiedRobustness(
                f.Session, gate, Array.Empty<CanonicalFoodSampleScenario>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalExecutedFoodSampleAudit.FromVerifiedRobustness(
                f.Session, gate, Enumerable.Range(1, 9)
                    .Select(x => Scenario($"policy-{x}"))));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedFoodSampleAudit.EvaluateHypotheticalCriteria(
                new[] {
                    Window(1, 2, matchedFood: 100, largest: 40),
                    Window(2, 3)
                }, 0, Scenario()));
    }

    [Fact]
    public void RestoredSaveAndNewTickCannotReuseLiveRepresentationProof()
    {
        var (f, gate, audit) = Build();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            audit.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedFoodSampleAudit.FromVerifiedRobustness(
                restored, gate, audit.Scenarios));
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            audit.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            audit.VerifyIndependent());
    }

    [Fact]
    public void NetworkRevisionInvalidatesSampleWithoutChangingWorldSave()
    {
        var (f, gate, audit) = Build();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            audit.RequireCurrent(f.Session));
    }

    [Fact]
    public void RepeatedAuditReconstructsSamePaidFoodRowsWithoutMutation()
    {
        var (f, gate, audit) = Build();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        for (var i = 0; i < 3; i++)
        {
            var replay = CanonicalExecutedFoodSampleAudit
                .FromVerifiedRobustness(f.Session, gate, audit.Scenarios);
            Assert.Equal(audit.Windows, replay.Windows);
            Assert.Equal(audit.Assessments, replay.Assessments);
            Assert.Equal(audit.PersistentMatchedTileCount,
                replay.PersistentMatchedTileCount);
            replay.VerifyIndependent();
            Assert.Equal(save, f.Session.ExportSaveJson());
            Assert.Equal(hash, f.Session.CurrentHash);
        }
    }
}
