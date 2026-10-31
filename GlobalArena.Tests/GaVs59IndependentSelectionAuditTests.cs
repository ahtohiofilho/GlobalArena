using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs59IndependentSelectionAuditTests
{
    private static CanonicalTransportSelection Verify(GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        IEnumerable<ExperimentalAbstractPhysicalFreightDelivery> lots, int count = 3,
        ulong quantum = 500_000, CanonicalTransportProof? proof = null) =>
        new CanonicalSimultaneousTransportAuditor().Verify(f.Session, f.Purchasing, f.Flows,
            f.Catalog, f.Quotes, f.Policy, f.Registry().NominalQuoteUnitId, f.Lanes,
            f.Congestion, proof ?? GaVs57SessionTransportAuthorityTests.Capture(f), f.Anchors,
            quantum, lots, count);

    [Fact]
    public void IndependentlyAcceptsCanonicalSelectionWithoutMutatingSave()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var selected = GaVs58SimultaneousAuthorityTests.Run(f);
        var save = f.Session.ExportSaveJson();
        var verified = Verify(f, selected.Deliveries);
        Assert.Equal(selected.Deliveries, verified.Deliveries);
        Assert.NotSame(selected.Audit, verified.Audit);
        Assert.False(verified.CanAuthorizeRealSettlement);
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(250000UL)]
    [InlineData(500000UL)]
    [InlineData(1500000UL)]
    public void RejectsFeasibleSuboptimalUnquantizedAndOversupplyProposals(ulong quantity)
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var lot = GaVs58SimultaneousAuthorityTests.Run(f).Deliveries.Single();
        var lots = quantity == 0 ? Array.Empty<ExperimentalAbstractPhysicalFreightDelivery>() : new[] { lot with { RawQuantity = quantity } };
        Assert.Throws<InvalidOperationException>(() => Verify(f, lots));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void RejectsFalseEnumerationCoverage(int count)
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        Assert.Throws<InvalidOperationException>(() => Verify(f, GaVs58SimultaneousAuthorityTests.Run(f).Deliveries, count));
    }

    [Fact]
    public void RejectsDuplicateOrRenumberedLots()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var lot = GaVs58SimultaneousAuthorityTests.Run(f).Deliveries.Single();
        Assert.Throws<InvalidOperationException>(() => Verify(f, new[] { lot, lot }));
        Assert.Throws<InvalidOperationException>(() => Verify(f, new[] { lot with { LotId = 99 } }));
    }

    [Fact]
    public void EmptyOptimumIsValidOnlyInItsDeclaredDomain()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        Assert.Empty(Verify(f, [], 1, 2_000_000).Deliveries);
        Assert.Throws<InvalidOperationException>(() => Verify(f, [], 3));
    }

    [Fact]
    public void RevocationInvalidatesAuditedSelectionAndProof()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var proof = GaVs57SessionTransportAuthorityTests.Capture(f);
        var lots = GaVs58SimultaneousAuthorityTests.Run(f).Deliveries;
        var audited = Verify(f, lots, proof: proof);
        f.Session.TransportNetwork.RevokeGrant(f.Asset.AssetId, f.Grant().AuthorizedOperator);
        Assert.Throws<InvalidOperationException>(() => audited.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() => Verify(f, lots, proof: proof));
    }
}
