using GlobalArena.Economy;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-CANON-DIRECTIVES-01: future product rules must not be silently
/// replaced by V9/V30 compatibility policies after later increments.
/// </summary>
public sealed class GaCanonDirectives01PolicyAuditTests
{
    private static CanonicalPhysicalTacticalSession CurrentGodotModes() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    [Fact]
    public void HistoricalCadenceIsSupportedButNotTheFutureProductTarget()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        var before = session.CurrentHash;
        var save = session.ExportSaveJson();

        var report = GaCanonicalProductDirectionAudit.Inspect(session);
        var cadence = report.Findings.Single(x =>
            x.DirectiveId == "GA-DIR-CADENCE-MOBILITY");
        Assert.Equal(GaProductDirectionReadiness.DesignRequiresNewVersionedRule,
            cadence.Readiness);
        Assert.Contains("LegacyFixedSix", cadence.Observed);
        Assert.Contains("mobility", cadence.Target,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("30", cadence.Target);
        Assert.False(cadence.CanAuthorizeTargetGameplay);
        Assert.False(report.CanPromoteAsTargetCanonical);
        Assert.Throws<InvalidOperationException>(report.RequireTargetCanonical);
        Assert.Equal(before, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
    }

    [Fact]
    public void ActiveGodotModesAreExplicitlyLabelledLegacyNotApprovedTargets()
    {
        var session = CurrentGodotModes();
        var snapshot = session.ExportSaveJson();
        var hash = session.CurrentHash;

        var report = GaCanonicalProductDirectionAudit.Inspect(session);
        Assert.Equal("GA-CANON-DIRECTIVES-01/2026-11-06",
            GaCanonicalProductDirectionAudit.PolicyVersion);
        Assert.Equal(5, report.Findings.Count);
        Assert.Equal(5, report.Findings.Select(x => x.DirectiveId)
            .Distinct().Count());
        Assert.All(report.Findings, x =>
        {
            Assert.NotEqual(GaProductDirectionReadiness.TargetActive, x.Readiness);
            Assert.False(x.CanAuthorizeTargetGameplay);
            Assert.False(string.IsNullOrWhiteSpace(x.Target));
            Assert.False(string.IsNullOrWhiteSpace(x.Observed));
        });
        Assert.Contains("AdaptiveThirty", report.Findings.Single(x =>
            x.DirectiveId == "GA-DIR-CADENCE-MOBILITY").Observed);
        Assert.Contains("two turns", report.Findings.Single(x =>
            x.DirectiveId == "GA-DIR-MOVEMENT-SCALAR").Observed);
        Assert.Contains("LocalCashSettlement", report.Findings.Single(x =>
            x.DirectiveId == "GA-DIR-ECON-ABSTRACT").Observed);
        Assert.Equal(GaProductDirectionReadiness.FuturePhysicalEvidenceMissing,
            report.Findings.Single(x =>
                x.DirectiveId == "GA-DIR-NUTR-SIX").Readiness);
        Assert.Equal(hash.HexDigest, report.ObservedWorldHashHex);
        Assert.Equal(0UL, report.ObservedMovementTurn);
        Assert.False(report.CanPromoteAsTargetCanonical);
        Assert.Throws<InvalidOperationException>(report.RequireTargetCanonical);
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(snapshot, session.ExportSaveJson());
    }

    [Fact]
    public void NewAbstractPriceDemandDoesNotInheritFoodWalletOrConsumptionQuota()
    {
        // All curve values in this test are purely examples, not a game rule.
        var quote = PopulationPriceKernel.Quote(
            8UL, PhysicalOffer.FromAvailable(1000m),
            AbstractPurchasingPowerIndex.FromRelativeCapacity(2m),
            _ => 3m);

        Assert.Equal(8UL, quote.Population);
        Assert.Equal(2m, quote.RelativePurchasingPower);
        Assert.Equal(16m, quote.EffectiveDemand);
        Assert.Equal(1000m, quote.Offered);
        Assert.Equal(62.5m, quote.SupplyDemandRatio);
        Assert.Equal(3m, quote.Price);
        Assert.Equal(48m, quote.ReferenceRevenue);
        Assert.False(quote.IsExecutedSettlement);
        // D = 16 does NOT physically cap offered S = 1000.
        Assert.True(quote.Offered > quote.EffectiveDemand);

        var references = typeof(PopulationPriceKernel).Assembly
            .GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(references, name => name is not null &&
            name.StartsWith("GlobalArena.Runtime", StringComparison.Ordinal));
    }

    [Fact]
    public void PreservedHistoricalSaveReplaysWithoutEverGainingTargetCertification()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        var save = session.ExportSaveJson();
        var before = GaCanonicalProductDirectionAudit.Inspect(session);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var after = GaCanonicalProductDirectionAudit.Inspect(restored);

        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(session.CurrentHash, restored.CurrentHash);
        Assert.Equal(before.ObservedWorldHashHex, after.ObservedWorldHashHex);
        Assert.Equal(before.ObservedMovementTurn, after.ObservedMovementTurn);
        Assert.Equal(before.Findings.ToArray(), after.Findings.ToArray());
        Assert.False(after.CanPromoteAsTargetCanonical);
        Assert.Throws<InvalidOperationException>(after.RequireTargetCanonical);
    }

    [Fact]
    public void LateCycleCandidateCannotBePromotedByAnOldLabel()
    {
        var session = CurrentGodotModes();
        var first = GaCanonicalProductDirectionAudit.Inspect(session);
        Assert.NotNull(session.AdvanceMovementTurn());
        var next = GaCanonicalProductDirectionAudit.Inspect(session);

        Assert.NotEqual(first.ObservedWorldHashHex, next.ObservedWorldHashHex);
        Assert.Equal(first.Findings.ToArray(), next.Findings.ToArray());
        Assert.False(next.CanPromoteAsTargetCanonical);
        var denial = Assert.Throws<InvalidOperationException>(
            next.RequireTargetCanonical);
        Assert.Contains("GA-DIR-CADENCE-MOBILITY", denial.Message);
        Assert.Contains("GA-DIR-ECON-ABSTRACT", denial.Message);
        Assert.Contains("GA-DIR-NUTR-SIX", denial.Message);
    }
}
