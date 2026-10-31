using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs57SessionTransportAuthorityTests
{
    internal static GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Create(ulong through = 100)
    {
        var f = new GaVs56BuiltNetworkInstitutionalRightsTests.Fixture();
        var n = f.Session.TransportNetwork;
        n.Construct(f.Asset, f.Grant().InfrastructureOwner);
        n.SetEntitlement(f.Registry().Entitlements.Single());
        n.IssueGrant(f.Grant(from: 3, through: through));
        return f;
    }

    internal static CanonicalTransportProof Capture(GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        CanonicalPhysicalTacticalSession? session = null)
    {
        var r = f.Registry();
        return (session ?? f.Session).TransportNetwork.Capture(new[] { f.Evidence() },
            r.PurchasingPolicyId, r.PurchasingPolicyVersion, r.CalibrationId,
            r.CalibrationVersion, r.NominalQuoteUnitId);
    }

    private static ExperimentalBuiltFreightRightsProjection Run(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f, CanonicalTransportProof? proof = null,
        CanonicalPhysicalTacticalSession? session = null,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote>? quotes = null) =>
        new CanonicalTransportFreightProjector().Project(session ?? f.Session,
            f.Purchasing, f.Flows, f.Catalog, quotes ?? f.Quotes, f.Policy,
            f.Registry().NominalQuoteUnitId, f.Lanes, f.Deliveries, f.Congestion,
            proof ?? Capture(f), f.Anchors);

    [Fact]
    public void SessionAuthorityReconcilesExistingEconomyWithoutSettlementOrMutation()
    {
        var f = Create();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        var digest = f.Session.TransportNetwork.StateDigest;
        var result = Run(f);
        Assert.Equal(3UL, result.NetworkRevision);
        Assert.Single(result.ValidatedServiceRights);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.InstitutionalProjection.CanAuthorizeRealSettlement);
        Assert.Equal(0, result.InstitutionalProjection.AbstractBuyerFreightCharges.CompareTo(
            result.InstitutionalProjection.AbstractInstitutionalFreightReceivables));
        Assert.Equal(hash, f.Session.CurrentHash);
        Assert.Equal(digest, f.Session.TransportNetwork.StateDigest);
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void ProofCannotBeConstructedByConsumersAndEmptySessionCannotAttest()
    {
        Assert.Empty(typeof(CanonicalTransportProof).GetConstructors());
        var f = new GaVs56BuiltNetworkInstitutionalRightsTests.Fixture();
        Assert.Throws<InvalidOperationException>(() => Capture(f));
    }

    [Fact]
    public void IdenticalWorldInAnotherSessionCannotBorrowProof()
    {
        var f = Create();
        var other = Create();
        Assert.Equal(f.Session.CurrentHash, other.Session.CurrentHash);
        Assert.Equal(f.Session.TransportNetwork.StateDigest, other.Session.TransportNetwork.StateDigest);
        Assert.Throws<InvalidOperationException>(() => Run(other, Capture(f)));
    }

    [Fact]
    public void PhysicalTurnInvalidatesProofEvenIfNetworkRevisionIsUnchanged()
    {
        var f = Create();
        var proof = Capture(f);
        f.Session.AdvanceMovementTurn();
        Assert.Equal(3UL, f.Session.TransportNetwork.Revision);
        Assert.Throws<InvalidOperationException>(() => Run(f, proof));
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("inactive")]
    [InlineData("revoked")]
    [InlineData("owner")]
    [InlineData("entitlement")]
    public void EveryAuthoritativeChangeInvalidatesOldProof(string change)
    {
        var f = Create();
        var n = f.Session.TransportNetwork;
        var proof = Capture(f);
        switch (change)
        {
            case "closed": n.SetAccess(f.Asset.GeographicEdgeId, false); break;
            case "inactive": n.SetOperational(f.Asset.AssetId, false); break;
            case "revoked": n.RevokeGrant(f.Asset.AssetId, f.Grant().AuthorizedOperator); break;
            case "owner": n.TransferOwnership(f.Asset.AssetId, f.Grant().AuthorizedOperator); break;
            case "entitlement": n.SetEntitlement(f.Registry().Entitlements.Single()); break;
        }
        Assert.Equal(4UL, n.Revision);
        Assert.Throws<InvalidOperationException>(() => Run(f, proof));
        if (change == "closed") Assert.Throws<InvalidOperationException>(() => Run(f));
        if (change == "inactive") Assert.Throws<InvalidOperationException>(() => Run(f));
        if (change is "revoked" or "owner") Assert.Throws<KeyNotFoundException>(() => Capture(f));
    }

    [Fact]
    public void InclusiveValidityAndFutureGrantAreCheckedAgainstCurrentRevision()
    {
        var f = Create(through: 3);
        Run(f); // exact inclusive upper bound
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() => Run(f));
        var future = new GaVs56BuiltNetworkInstitutionalRightsTests.Fixture();
        var n = future.Session.TransportNetwork;
        n.Construct(future.Asset, future.Grant().InfrastructureOwner);
        n.SetEntitlement(future.Registry().Entitlements.Single());
        n.IssueGrant(future.Grant(from: 4, through: 6));
        Assert.Throws<InvalidOperationException>(() => Run(future));
        n.SetAccess(future.Asset.GeographicEdgeId, true);
        Run(future); // exact inclusive lower bound
    }

    [Fact]
    public void RejectedCommandsDoNotAdvanceRevisionOrMutateSave()
    {
        var f = Create();
        var n = f.Session.TransportNetwork;
        var save = f.Session.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() => n.Construct(f.Asset, f.Grant().InfrastructureOwner));
        Assert.Throws<ArgumentException>(() => n.IssueGrant(f.Grant(from: 4, owner: f.Grant().AuthorizedOperator)));
        Assert.Throws<ArgumentException>(() => n.IssueGrant(f.Grant(from: 4)));
        var right = f.Registry().Entitlements.Single();
        Assert.Throws<ArgumentException>(() => n.SetEntitlement(right with { RevenueShares = [] }));
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void ForeignTopologyAndWaterConstructionCannotCreateSessionAssets()
    {
        var f = Create();
        var save = f.Session.ExportSaveJson();
        var foreign = new EconomicTransportBuiltAsset(99, 101,
            new PhysicalTacticalEdgeId(new GoldbergParameters(3, 0), new StrategicEdgeId(1)), 7);
        Assert.Throws<ArgumentException>(() => f.Session.TransportNetwork.Construct(
            foreign, f.Grant().InfrastructureOwner));
        var water = f.Session.FineWorld.StrategicTopology.Edges.First(x =>
            x.IncidentCellIds.Any(c => !f.Session.IsPhysicalWorkableLand(c)));
        var wet = new EconomicTransportBuiltAsset(99, 101,
            new PhysicalTacticalEdgeId(f.Session.InitialTile.FineGoldbergParameters, water.Id), 7);
        Assert.Throws<InvalidOperationException>(() => f.Session.TransportNetwork.Construct(
            wet, f.Grant().InfrastructureOwner));
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void RevenueInputCollectionsAreCopiedAndNetworkChangesHaveSeparateDigest()
    {
        var f = Create();
        var n = f.Session.TransportNetwork;
        var right = f.Registry().Entitlements.Single();
        var shares = right.RevenueShares.ToArray();
        n.SetEntitlement(right with { RevenueShares = shares });
        var proof = Capture(f);
        shares[0] = shares[0] with { Beneficiary = right.Operator };
        Assert.Equal(right.RevenueShares.Single().Beneficiary,
            Run(f, proof).InstitutionalProjection.Lots.Single().Beneficiaries.Single().Beneficiary);
        var hash = f.Session.CurrentHash;
        var digest = n.StateDigest;
        n.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Equal(hash, f.Session.CurrentHash);
        Assert.NotEqual(digest, n.StateDigest);
    }

    [Fact]
    public void TransportSaveUsesNewEnvelopeAndCannotDowngradeSilently()
    {
        var f = Create();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(f.Session.ExportSaveJson())!;
        Assert.Equal(CanonicalPhysicalTacticalSession.TransportAuthorityEnvelopeFormat, envelope.Version);
        var payload = JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(envelope.PayloadJson)!;
        Assert.Throws<InvalidOperationException>(() => CanonicalPhysicalTacticalSession.RestoreSaveJson(
            JsonSerializer.Serialize(envelope with { Version = payload.Version })));
        var legacy = new GaVs56BuiltNetworkInstitutionalRightsTests.Fixture().Session;
        var oldSave = legacy.ExportSaveJson();
        _ = legacy.TransportNetwork;
        Assert.Equal(oldSave, legacy.ExportSaveJson());
        Assert.Equal(oldSave, CanonicalPhysicalTacticalSession.RestoreSaveJson(oldSave).ExportSaveJson());
    }

    [Fact]
    public void OwnershipTransferRequiresNewGrantFromRecordedOwner()
    {
        var f = Create();
        var n = f.Session.TransportNetwork;
        var nextOwner = f.Registry().Entitlements.Single().RevenueShares.Single().Beneficiary;
        n.TransferOwnership(f.Asset.AssetId, nextOwner);
        Assert.Throws<ArgumentException>(() => n.IssueGrant(f.Grant(from: 5)));
        n.IssueGrant(f.Grant(owner: nextOwner, from: 5, through: 100, id: "NEW-OWNER-GRANT"));
        Assert.Equal("NEW-OWNER-GRANT", Run(f).ValidatedServiceRights.Single().AssetGrantIds.Single());
    }

    [Fact]
    public void LazyInputMutationCannotRevalidateStaleProof()
    {
        var f = Create();
        var proof = Capture(f);
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> Mutate()
        {
            f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
            foreach (var quote in f.Quotes) yield return quote;
        }
        Assert.Throws<InvalidOperationException>(() => Run(f, proof, quotes: Mutate()));
    }

    [Fact]
    public void SaveReplaysInterleavedCommandsAndRequiresFreshProofAfterRestore()
    {
        var f = Create();
        var proof = Capture(f);
        f.Session.AdvanceMovementTurn();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        f.Session.AdvanceMovementTurn();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        var save = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(f.Session.TransportNetwork.StateDigest, restored.TransportNetwork.StateDigest);
        Assert.Throws<InvalidOperationException>(() => Run(f, proof, restored));
        Assert.Equal(restored.CurrentHash, Capture(f, restored).Network.WorldStateHash);
    }

    [Fact]
    public void RestoredSessionRunsTheFullAuditWithReissuedProof()
    {
        var f = Create();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(f.Session.ExportSaveJson());
        Assert.False(Run(f, Capture(f, restored), restored).CanAuthorizeRealSettlement);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("physical")]
    [InlineData("owner")]
    [InlineData("ordering")]
    public void RehashedSaveCannotBypassCommandReplay(string tampering)
    {
        var f = Create();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(f.Session.ExportSaveJson())!;
        var payload = JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(envelope.PayloadJson)!;
        var commands = payload.TransportCommands!.ToArray();
        commands[2] = tampering switch
        {
            "revision" => commands[2] with { PreviousRevision = 999 },
            "physical" => commands[2] with { PhysicalHash = new string('0', 64) },
            "ordering" => commands[2] with { PhysicalActionCount = 0 },
            _ => commands[2] with { Grant = commands[2].Grant! with {
                InfrastructureOwner = f.Grant().AuthorizedOperator } }
        };
        var json = JsonSerializer.Serialize(payload with { TransportCommands = commands });
        var forged = JsonSerializer.Serialize(envelope with { PayloadJson = json,
            PayloadSha256Hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))) });
        Assert.ThrowsAny<Exception>(() => CanonicalPhysicalTacticalSession.RestoreSaveJson(forged));
    }
}
