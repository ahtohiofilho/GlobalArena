using GlobalArena.Simulation;
using GlobalArena.Runtime;

namespace GlobalArena.Tests;

public sealed class GaVs60SessionTradePreviewTests
{
    internal static CanonicalTradeScenario Scenario(GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote>? quotes = null, ulong quantum = 500_000,
        string? unit = null) => new(f.Flows, f.Catalog,
            f.Purchasing.Buyers.Select(x => new ExperimentalAbstractBuyerAnchor(x.Buyer, x.Tile, x.ProductivityFactor)),
            f.Purchasing.Producers.Select(x => new ExperimentalAbstractProducerAnchor(x.Producer, x.Tile)),
            f.Flows.DemandFlows.Select(x => new ExperimentalBuyerCommodityPrice(x.EconomicPointId, x.CommodityId, new FoodTransportCostFraction(1, 1))),
            new ExperimentalAbstractPurchasingPolicy("VS56-PURCHASING", 1, new FoodTransportCostFraction(5, 1), new FoodTransportCostFraction(5, 8)),
            quotes ?? f.Quotes, f.Policy, unit ?? f.Registry().NominalQuoteUnitId,
            f.Lanes, new[] { f.Evidence() }, f.Congestion, quantum);

    [Fact]
    public void SingleSessionOperationMatchesIndependentlyAuditedSelectionWithoutMutation()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var save = f.Session.ExportSaveJson();
        var preview = f.Session.PreviewTrade(Scenario(f));
        var expected = GaVs58SimultaneousAuthorityTests.Run(f);
        Assert.Equal(expected.Deliveries, preview.VerifiedSelection.Deliveries);
        Assert.Equal(2UL, preview.Purchasing.Buyers.Single().ResidentWorkers);
        Assert.Equal(save, f.Session.ExportSaveJson());
        Assert.False(preview.CanAuthorizeRealSettlement);
        preview.RequireCurrent(f.Session);
        Assert.Empty(typeof(CanonicalTradePreview).GetConstructors());
    }

    [Fact]
    public void ReusedScenarioRebuildsPurchasingFromCurrentResidentWorkers()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var scenario = Scenario(f);
        var before = f.Session.PreviewTrade(scenario);
        var source = f.Purchasing.Producers.Single().Tile;
        var buyer = f.Purchasing.Buyers.Single().Tile;
        f.Session.OrderWorkerTransit(source, buyer, EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1);
        f.Session.AdvanceMovementTurn();
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() => before.RequireCurrent(f.Session));
        var after = f.Session.PreviewTrade(scenario);
        Assert.Equal(3UL, after.Purchasing.Buyers.Single().ResidentWorkers);
        Assert.True(after.Purchasing.TotalTemporaryPurchasingUnits.CompareTo(before.Purchasing.TotalTemporaryPurchasingUnits) > 0);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("inactive")]
    [InlineData("revoked")]
    public void InstitutionalChangesInvalidatePreviewAndPreventNewApproval(string change)
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var scenario = Scenario(f);
        var preview = f.Session.PreviewTrade(scenario);
        if (change == "closed") f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        if (change == "inactive") f.Session.TransportNetwork.SetOperational(f.Asset.AssetId, false);
        if (change == "revoked") f.Session.TransportNetwork.RevokeGrant(f.Asset.AssetId, f.Grant().AuthorizedOperator);
        var save = f.Session.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() => preview.RequireCurrent(f.Session));
        Assert.ThrowsAny<Exception>(() => f.Session.PreviewTrade(scenario));
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void RestoredSessionMustProduceNewPreview()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var scenario = Scenario(f);
        var preview = f.Session.PreviewTrade(scenario);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() => preview.RequireCurrent(restored));
        restored.PreviewTrade(scenario).RequireCurrent(restored);
    }

    [Fact]
    public void ScenarioCopiesPricesAndEnumeratesThemOnce()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var quotes = f.Quotes.ToArray();
        var enumerations = 0;
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> Once()
        {
            if (++enumerations > 1) throw new InvalidOperationException();
            foreach (var q in quotes) yield return q;
        }
        var scenario = Scenario(f, Once());
        quotes[0] = quotes[0] with { NominalCommodityUnitPrice = new FoodTransportCostFraction(1000, 1) };
        Assert.Single(f.Session.PreviewTrade(scenario).VerifiedSelection.Deliveries);
        Assert.Single(f.Session.PreviewTrade(scenario).VerifiedSelection.Deliveries);
        Assert.Empty(f.Session.PreviewTrade(Scenario(f, quotes)).VerifiedSelection.Deliveries);
        Assert.Equal(1, enumerations);
    }

    [Fact]
    public void UnitMismatchAndSearchOverflowFailWithoutPublishingOrMutating()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var save = f.Session.ExportSaveJson();
        Assert.Throws<ArgumentException>(() => f.Session.PreviewTrade(Scenario(f, unit: "FOREIGN")));
        Assert.Throws<ArgumentException>(() => f.Session.PreviewTrade(Scenario(f, quantum: 1)));
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void SessionWithoutCommittedNetworkCannotSelfCertifyOfferedAssets()
    {
        var f = new GaVs56BuiltNetworkInstitutionalRightsTests.Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Session.PreviewTrade(Scenario(f)));
    }
}
