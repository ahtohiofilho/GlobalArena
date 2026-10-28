using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicFineServicePhysicalResourceAdapterTests
{
    private const ulong One = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly GoldbergParameters Fine = new(4, 0);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId E = new(2UL);
    private static readonly CommodityId Grain = new(7U);
    private static readonly EconomicPhysicalResourceId FirstResource = new(101UL);
    private static readonly EconomicPhysicalResourceId SecondResource = new(102UL);
    private static readonly EconomicPhysicalResourceId TerminalResource = new(103UL);

    private sealed class FlatFreight : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms) =>
            new(new BigInteger(10), BigInteger.One);
    }

    private sealed class FlatCommodityValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(
            ulong allocatedRawQuantity) =>
            new(new BigInteger(600_000), BigInteger.One);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(
            ulong rawQuantity) =>
            new((BigInteger)600_000 * rawQuantity, One);
    }

    private sealed class Fixture
    {
        public StrategicSurfaceGraph Graph { get; }
        public EconomicTransportBuiltAsset[] Assets { get; }
        public EconomicTransportServiceOffer[] Offers { get; }
        public Dictionary<EconomicPointId, PhysicalTacticalTileId> Anchors { get; }
        public EconomicProductionDemandResult Flows { get; }
        public PhysicalTacticalTileId Junction { get; }
        public PhysicalTacticalTileId Origin { get; }

        public Fixture(bool technologyChange)
        {
            Graph = new StrategicSurfaceGraph(
                GoldbergStrategicTopologyGenerator.Generate(Fine));
            var first = Graph.StrategicTopology.Edges[0];
            var start = first.IncidentCellIds[0];
            var middle = first.IncidentCellIds[1];
            var second = Graph.StrategicTopology.Edges.First(e =>
                e.Id != first.Id &&
                e.IncidentCellIds.Contains(middle) &&
                !e.IncidentCellIds.Contains(start));
            var destination = second.IncidentCellIds.First(c => c != middle);

            Origin = new PhysicalTacticalTileId(Fine, start);
            Junction = new PhysicalTacticalTileId(Fine, middle);
            Anchors = new()
            {
                [A] = Origin,
                [E] = new PhysicalTacticalTileId(Fine, destination)
            };
            Assets = new[]
            {
                new EconomicTransportBuiltAsset(
                    10UL, 10UL, new PhysicalTacticalEdgeId(Fine, first.Id), 3UL),
                new EconomicTransportBuiltAsset(
                    20UL, technologyChange ? 20UL : 10UL,
                    new PhysicalTacticalEdgeId(Fine, second.Id), 3UL)
            };
            Offers = new[]
            {
                new EconomicTransportServiceOffer(
                    50UL, new StrategicEconomicRouteKey(A, E),
                    new[] { 10UL, 20UL },
                    technologyChange ? new[] { 0 } : Array.Empty<int>(),
                    BigInteger.Zero, new FlatFreight())
            };
            Flows = new EconomicProductionDemandResult(
                new[] { new EconomicSupplyFlow(
                    A, Grain, EconomicActivityKind.Agriculture, 3 * One) },
                new[] { new EconomicDemandFlow(E, Grain, 3 * One) });
        }

        public EconomicPhysicalAssetResourceBinding[] AssetBindings() => new[]
        {
            new EconomicPhysicalAssetResourceBinding(10UL, FirstResource),
            new EconomicPhysicalAssetResourceBinding(20UL, SecondResource)
        };

        public EconomicPhysicalTransferResourceBinding[] TransferBindings() => new[]
        {
            new EconomicPhysicalTransferResourceBinding(
                50UL, 0, TerminalResource, Junction)
        };

        public IReadOnlyList<EconomicReclearingQuotedRoute> Resolve(
            IEnumerable<EconomicTransportBuiltAsset>? assets = null,
            IEnumerable<EconomicTransportServiceOffer>? offers = null,
            IEnumerable<EconomicPhysicalAssetResourceBinding>? assetBindings = null,
            IEnumerable<EconomicPhysicalTransferResourceBinding>? transfers = null,
            StrategicEdgeAccessSnapshot? access = null) =>
            new EconomicFineServicePhysicalResourceAdapter().Resolve(
                1UL, Graph, Anchors, Flows,
                access ?? StrategicEdgeAccessSnapshot.AllOpen,
                assets ?? Assets, offers ?? Offers,
                assetBindings ?? AssetBindings(),
                transfers ?? TransferBindings());
    }

    [Fact]
    public void BindsTwoBuiltLegsAndExactlyOneTrueTransferWithoutInventedGeographicEdge()
    {
        var f = new Fixture(technologyChange: true);
        var route = Assert.Single(f.Resolve());
        Assert.Equal(new[] { FirstResource, TerminalResource, SecondResource },
            route.PhysicalResources);
        Assert.Equal(new StrategicEconomicRouteKey(A, E), route.Quote.RouteKey);
        Assert.Equal(1UL, route.Quote.TurnOrdinal);
        Assert.True(route.PhysicalResources.All(x => x.IsValid));
    }

    [Fact]
    public void SameTechnologyPassageNeverCreatesTransferCost()
    {
        var f = new Fixture(technologyChange: false);
        var route = Assert.Single(f.Resolve(
            transfers: Array.Empty<EconomicPhysicalTransferResourceBinding>()));
        Assert.Equal(new[] { FirstResource, SecondResource }, route.PhysicalResources);
        Assert.DoesNotContain(TerminalResource, route.PhysicalResources);
    }

    [Fact]
    public void TechnologyChangeWithoutDeclaredTerminalFailsClosed()
    {
        var f = new Fixture(technologyChange: true);
        Assert.Throws<InvalidOperationException>(() => f.Resolve(
            transfers: Array.Empty<EconomicPhysicalTransferResourceBinding>()));
    }

    [Fact]
    public void InventedTransferOnUnchangedTechnologyFailsClosed()
    {
        var f = new Fixture(technologyChange: false);
        Assert.Throws<InvalidOperationException>(() => f.Resolve());
    }

    [Fact]
    public void WrongJunctionTileCannotMasqueradeAsARealTransfer()
    {
        var f = new Fixture(technologyChange: true);
        var wrong = new[]
        {
            new EconomicPhysicalTransferResourceBinding(
                50UL, 0, TerminalResource, f.Origin)
        };
        Assert.Throws<InvalidOperationException>(() => f.Resolve(transfers: wrong));
    }

    [Fact]
    public void TwoAssetsCannotClaimTheSamePhysicalResource()
    {
        var f = new Fixture(technologyChange: true);
        var aliases = new[]
        {
            new EconomicPhysicalAssetResourceBinding(10UL, FirstResource),
            new EconomicPhysicalAssetResourceBinding(20UL, FirstResource)
        };
        Assert.Throws<ArgumentException>(() => f.Resolve(assetBindings: aliases));
    }

    [Fact]
    public void TransferFacilityCannotAliasAThroughputAsset()
    {
        var f = new Fixture(technologyChange: true);
        var aliases = new[]
        {
            new EconomicPhysicalTransferResourceBinding(
                50UL, 0, FirstResource, f.Junction)
        };
        Assert.Throws<ArgumentException>(() => f.Resolve(transfers: aliases));
    }

    [Fact]
    public void BlockedOrUnavailableAssetCannotProduceAValidatedCommercialRoute()
    {
        var f = new Fixture(technologyChange: true);
        var blocked = new StrategicEdgeAccessSnapshot(
            new[] { f.Assets[0].GeographicEdgeId });
        Assert.Empty(f.Resolve(
            access: blocked,
            transfers: Array.Empty<EconomicPhysicalTransferResourceBinding>()));

        var unavailable = new EconomicTransportBuiltAsset(
            10UL, 10UL, f.Assets[0].FineGeographicEdgeId!.Value,
            3UL, isOperational: false);
        Assert.Empty(f.Resolve(
            assets: new[] { unavailable, f.Assets[1] },
            transfers: Array.Empty<EconomicPhysicalTransferResourceBinding>()));
    }

    [Fact]
    public void IncompleteOrForgedAssetPathIsRejectedByExistingGeographyResolver()
    {
        var f = new Fixture(technologyChange: true);
        Assert.Throws<ArgumentException>(() => f.Resolve(
            assets: f.Assets.Take(1)));
        var fake = new EconomicTransportServiceOffer(
            50UL, new StrategicEconomicRouteKey(A, E),
            new[] { 20UL, 10UL }, new[] { 0 },
            BigInteger.Zero, new FlatFreight());
        Assert.Throws<ArgumentException>(() => f.Resolve(offers: new[] { fake }));
    }

    [Fact]
    public void ReorderedInputsProduceIdenticalCertifiedResourceIncidence()
    {
        var f = new Fixture(technologyChange: true);
        var a = Assert.Single(f.Resolve());
        var b = Assert.Single(f.Resolve(
            assets: f.Assets.Reverse(),
            assetBindings: f.AssetBindings().Reverse(),
            transfers: f.TransferBindings().Reverse()));
        Assert.Equal(a.PhysicalResources, b.PhysicalResources);
        Assert.Equal(0, a.Quote.RawUnitCostPerGram.CompareTo(b.Quote.RawUnitCostPerGram));
    }

    [Fact]
    public void ValidatedServiceCanBeReclearedAsAHypothesisWithoutWorldMutation()
    {
        var f = new Fixture(technologyChange: true);
        var verified = f.Resolve();
        var world = WorldState.CreateInitial();
        var beforeHash = new CanonicalWorldStateHasher().Compute(world);
        var profiles = new[]
        {
            new EconomicDensityResourceCostProfile(
                FirstResource, 400, 50, 2000, 15),
            new EconomicDensityResourceCostProfile(
                SecondResource, 100, 20, 400, 20),
            new EconomicDensityResourceCostProfile(
                TerminalResource, 60, 15, 300, 15)
        };
        var value = new[]
        {
            new EconomicTurnCommodityValueOffer(E, Grain, new FlatCommodityValue())
        };
        var reserve = new[]
        {
            new EconomicTurnSourceReservation(
                A, Grain, EconomicActivityKind.Agriculture, 10_000UL)
        };
        var comparison = new EconomicReferenceScenarioReclearingProbe().Evaluate(
            1UL, f.Flows, new FoodCommodityCatalog(new[]
            {
                new FoodCommodityDefinition(Grain, "grain", 1_000UL)
            }),
            profiles, verified, verified, value, reserve,
            new EconomicCongestionMarketSearchPolicy(One, 10));
        Assert.Equal(new BigInteger(3 * One),
            comparison.Baseline.RawTradedQuantity);
        Assert.Equal(comparison.Baseline.RawTradedQuantity,
            comparison.Proposed.RawTradedQuantity);
        foreach (var resource in new[]
        {
            FirstResource, SecondResource, TerminalResource
        })
            Assert.Equal(new BigInteger(3_000_000_000),
                comparison.Baseline.ResourceLoadsMicrograms[resource]);
        Assert.Equal(EconomicDensityCostDeltaClassification.Indeterminate,
            comparison.RealCostSign);
        Assert.Equal(beforeHash, new CanonicalWorldStateHasher().Compute(world));
    }
    private static EconomicFineTransferFacilityEvidence Facility(
        Fixture f, bool operational = true,
        ulong fromTechnology = 10UL, ulong toTechnology = 20UL,
        ulong version = 3UL,
        PhysicalTacticalTileId? tile = null,
        ulong facilityId = 900UL,
        EconomicPhysicalResourceId? resource = null) =>
        new(facilityId, resource ?? TerminalResource,
            tile ?? f.Junction, fromTechnology, toTechnology,
            version, operational);

    private static IReadOnlyList<EconomicReclearingQuotedRoute> Authorized(
        Fixture f,
        IEnumerable<EconomicFineTransferFacilityEvidence>? evidence = null,
        IEnumerable<EconomicTransportServiceOffer>? offers = null,
        IEnumerable<EconomicPhysicalTransferResourceBinding>? transfers = null,
        ulong turn = 1UL,
        ulong snapshotTurn = 1UL) =>
        new EconomicFineAuthorizedServicePhysicalResourceAdapter().Resolve(
            turn, f.Graph, f.Anchors, f.Flows,
            StrategicEdgeAccessSnapshot.AllOpen, f.Assets,
            offers ?? f.Offers, f.AssetBindings(),
            transfers ?? f.TransferBindings(),
            new EconomicFineTransferFacilityAuthorizationSnapshot(
                snapshotTurn, evidence ?? new[] { Facility(f) }));

    [Fact]
    public void OperationalFacilityAtCorrectJunctionAuthorizesExactDirectedTransfer()
    {
        var f = new Fixture(technologyChange: true);
        var result = Assert.Single(Authorized(f));
        Assert.Equal(new[] { FirstResource, TerminalResource, SecondResource },
            result.PhysicalResources);
        Assert.Equal(1UL, result.Quote.TurnOrdinal);
    }

    [Fact]
    public void MissingOrNonOperationalFacilityCannotAuthorizeCrossTechnologyService()
    {
        var f = new Fixture(technologyChange: true);
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, evidence: Array.Empty<EconomicFineTransferFacilityEvidence>()));
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, evidence: new[] { Facility(f, operational: false) }));
    }

    [Fact]
    public void WrongLocationOrResourceCannotImpersonateTheInstalledFacility()
    {
        var f = new Fixture(technologyChange: true);
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, evidence: new[] { Facility(f, tile: f.Origin) }));
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, evidence: new[]
            {
                Facility(f, resource: new EconomicPhysicalResourceId(9000UL))
            }));
    }

    [Fact]
    public void PermissionIsDirectionalAndVersionBoundWithNoImplicitReciprocity()
    {
        var f = new Fixture(technologyChange: true);
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, evidence: new[]
            {
                Facility(f, fromTechnology: 20UL, toTechnology: 10UL)
            }));
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, evidence: new[] { Facility(f, version: 4UL) }));
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, evidence: new[]
            {
                Facility(f, fromTechnology: 10UL, toTechnology: 30UL)
            }));
    }

    [Fact]
    public void TransferEvidenceMustBeFromThisTurnSnapshot()
    {
        var f = new Fixture(technologyChange: true);
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, snapshotTurn: 2UL));
    }

    [Fact]
    public void FacilityIdsResourcesAndDirectedCapabilitiesMustNotAliasOrDuplicate()
    {
        var f = new Fixture(technologyChange: true);
        Assert.Throws<ArgumentException>(() =>
            new EconomicFineTransferFacilityAuthorizationSnapshot(
                1UL, new[] { Facility(f), Facility(f) }));
        Assert.Throws<ArgumentException>(() =>
            new EconomicFineTransferFacilityAuthorizationSnapshot(
                1UL, new[]
                {
                    Facility(f),
                    Facility(f, facilityId: 901UL)
                }));
        Assert.Throws<ArgumentException>(() =>
            new EconomicFineTransferFacilityAuthorizationSnapshot(
                1UL, new[]
                {
                    Facility(f),
                    Facility(f, resource: new EconomicPhysicalResourceId(104UL))
                }));
    }

    private sealed class CountedFreight : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Calls { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Calls++;
            return new FoodTransportCostFraction(new BigInteger(10), BigInteger.One);
        }
    }

    [Fact]
    public void FacilityCannotBeSimultaneouslyOperationalAndClosedInOneSnapshot()
    {
        var f = new Fixture(technologyChange: true);
        Assert.Throws<ArgumentException>(() =>
            new EconomicFineTransferFacilityAuthorizationSnapshot(
                1UL, new[]
                {
                    Facility(f, operational: true),
                    Facility(f, operational: false,
                        fromTechnology: 20UL, toTechnology: 10UL)
                }));
    }

    [Fact]
    public void MissingBuiltAssetReferenceIsRejectedBeforeFreightPricing()
    {
        var f = new Fixture(technologyChange: true);
        var counter = new CountedFreight();
        var forged = new EconomicTransportServiceOffer(
            50UL, new StrategicEconomicRouteKey(A, E),
            new[] { 10UL, 999UL }, new[] { 0 },
            BigInteger.Zero, counter);
        Assert.Throws<ArgumentException>(() =>
            Authorized(f, offers: new[] { forged }));
        Assert.Equal(0, counter.Calls);
    }

    [Fact]
    public void MissingFacilityBindingRejectsBeforeAnyCommercialQuoteIsEvaluated()
    {
        var f = new Fixture(technologyChange: true);
        var counter = new CountedFreight();
        var offer = new EconomicTransportServiceOffer(
            50UL, new StrategicEconomicRouteKey(A, E),
            new[] { 10UL, 20UL }, new[] { 0 },
            BigInteger.Zero, counter);
        Assert.Throws<InvalidOperationException>(() =>
            Authorized(f, offers: new[] { offer },
                transfers: Array.Empty<EconomicPhysicalTransferResourceBinding>()));
        Assert.Equal(0, counter.Calls);
    }

    [Fact]
    public void SameTechnologyTransitRequiresNoTransferFacilityOrTransferItem()
    {
        var f = new Fixture(technologyChange: false);
        var result = Assert.Single(Authorized(f,
            evidence: Array.Empty<EconomicFineTransferFacilityEvidence>(),
            transfers: Array.Empty<EconomicPhysicalTransferResourceBinding>()));
        Assert.Equal(new[] { FirstResource, SecondResource }, result.PhysicalResources);
    }

    [Fact]
    public void AuthorizationSnapshotIsReadOnlyAndDoesNotMutateWorld()
    {
        var f = new Fixture(technologyChange: true);
        var world = WorldState.CreateInitial();
        var before = new CanonicalWorldStateHasher().Compute(world);
        var result = Authorized(f);
        Assert.Single(result);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }

}
