using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs56BuiltNetworkInstitutionalRightsTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private const ulong Revision = 7;
    private const string QuoteUnit = "GA-VS56-NOMINAL-LAB";
    private static readonly EconomicPointId Source = new(201);
    private static readonly EconomicPointId Buyer = new(101);
    private static readonly CommodityId Food = new(7);
    private static readonly ExperimentalTransportInstitution Owner =
        new("PUBLIC-ROAD-A", ExperimentalTransportInstitutionKind.PublicAuthority);
    private static readonly ExperimentalTransportInstitution Operator =
        new("COMMERCIAL-CARRIER-B", ExperimentalTransportInstitutionKind.CommercialOperator);
    private static readonly ExperimentalTransportInstitution Beneficiary =
        new("COMMUNITY-C", ExperimentalTransportInstitutionKind.Community);

    private static FoodTransportCostFraction F(long n, long d = 1) => new(n, d);

    internal sealed class NetworkAuthority(bool allow) :
        IExperimentalBuiltTransportNetworkAuthority
    {
        public int Calls { get; private set; }
        public bool AcceptReadOnlyBuiltNetwork(
            ExperimentalBuiltTransportNetworkWitness witness,
            StrategicEdgeAccessSnapshot access)
        {
            Calls++;
            return allow;
        }
    }

    internal sealed class RegistryAuthority(bool allow) :
        IExperimentalTransportInstitutionalRegistryAuthority
    {
        public int Calls { get; private set; }
        public bool AcceptReadOnlyRegistry(
            ExperimentalTransportInstitutionalRegistry registry)
        {
            Calls++;
            return allow;
        }
    }

    internal sealed class Fixture
    {
        public CanonicalPhysicalTacticalSession Session { get; } =
            CanonicalPhysicalTacticalSession.Start(
                20261012UL, new GoldbergParameters(3, 0),
                new GoldbergParameters(18, 0),
                PhysicalEconomicCadenceMode.AdaptiveThirty,
                PhysicalTacticalGeographyMode.Canonical01C,
                PhysicalFoodMarketMode.LocalCashSettlement,
                PhysicalWorkerMovementMode.TimedAdjacent,
                PhysicalCivilizationIdentityMode.BornWithPopulation);

        public EconomicTransportBuiltAsset Asset { get; }
        public ExperimentalSharedFreightOption Option { get; }
        public EconomicTransportServiceOffer Offer { get; }
        public Dictionary<EconomicPointId, PhysicalTacticalTileId> Anchors { get; }
        public EconomicProductionDemandResult Flows { get; }
        public FoodCommodityCatalog Catalog { get; }
        public ExperimentalAbstractPurchasingProjection Purchasing { get; }
        public ExperimentalAbstractNominalDeliveredQuote[] Quotes { get; }
        public ExperimentalAbstractPriceFreightCalibrationPolicy Policy { get; }
        public ExperimentalSimultaneousFreightLane[] Lanes { get; }
        public ExperimentalAbstractPhysicalFreightDelivery[] Deliveries { get; }
        public ExperimentalSharedFreightCongestionPolicy Congestion { get; } =
            new(1, 1_000_000);
        public StrategicEdgeAccessSnapshot Access { get; } =
            StrategicEdgeAccessSnapshot.AllOpen;

        public Fixture()
        {
            var sourceTile = Session.InitialTile;
            var buyerTile = Session.AdjacentWorkable(sourceTile)[0];
            Session.OrderWorkerTransit(sourceTile, buyerTile,
                EconomicActivityKind.Agriculture,
                EconomicActivityKind.Forestry, 2);
            Session.AdvanceMovementTurn();
            Session.AdvanceMovementTurn();

            var edge = Session.FineWorld.StrategicTopology.Edges.First(x =>
                x.IncidentCellIds.Contains(sourceTile.FineStrategicCellId) &&
                x.IncidentCellIds.Contains(buyerTile.FineStrategicCellId));
            Asset = new EconomicTransportBuiltAsset(
                31, 101,
                new PhysicalTacticalEdgeId(
                    Session.InitialTile.FineGoldbergParameters, edge.Id),
                7, true);
            Anchors = new() {
                [Source] = sourceTile,
                [Buyer] = buyerTile
            };
            var facts = new CanonicalLandEdgeFactsSnapshot(Session);
            var network = new ExperimentalLandInfrastructureSnapshot(
                Session, facts, new[] { Asset },
                new Dictionary<(ulong, ulong), ExperimentalLandInfrastructureKind> {
                    [(101UL, 7UL)] = ExperimentalLandInfrastructureKind.StoneRoad
                }, Access, networkRevision: Revision);
            var rate = new ExperimentalFreightTechnologyPricing(
                101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 1_000, 1_000_000);
            var intent = new EconomicFineTransportServiceIntent(
                17, new StrategicEconomicRouteKey(Source, Buyer),
                101, 7, 1_000_000);
            Offer = Assert.Single(
                new ExperimentalCostOptimalFreightItineraryDiscovery()
                    .Discover(Session, facts, network, Anchors, Access,
                        new[] { Asset }, new[] { intent }, new[] { rate }));
            Option = ExperimentalSharedFreightOption.FromBuiltOffer(
                Offer, Session, Anchors, new[] { Asset }, Access);
            Flows = new EconomicProductionDemandResult(
                new[] {
                    new EconomicSupplyFlow(
                        Source, Food, EconomicActivityKind.Agriculture, Unit)
                },
                new[] { new EconomicDemandFlow(Buyer, Food, Unit) });
            Catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(Food, "food", 1)
            });
            Lanes = new[] {
                new ExperimentalSimultaneousFreightLane(
                    Flows.SupplyFlows[0], Flows.DemandFlows[0],
                    new[] { Option })
            };
            Purchasing = new ExperimentalAbstractPurchasingPowerProjector()
                .Project(Session, Flows,
                    new[] { new ExperimentalAbstractBuyerAnchor(
                        Buyer, buyerTile, F(1)) },
                    new[] { new ExperimentalAbstractProducerAnchor(
                        Source, sourceTile) },
                    Lanes,
                    new[] { new ExperimentalBuyerCommodityPrice(Buyer, Food, F(1)) },
                    new ExperimentalAbstractPurchasingPolicy(
                        "VS56-PURCHASING", 1, F(5), F(5, 8)));
            Quotes = new[] {
                new ExperimentalAbstractNominalDeliveredQuote(
                    Buyer, Food, F(1), F(1), QuoteUnit)
            };
            Policy = new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "VS56-CALIBRATION", 1, "VS56-PURCHASING", 1, QuoteUnit, F(1));
            Deliveries = new[] {
                new ExperimentalAbstractPhysicalFreightDelivery(
                    1, Source, EconomicActivityKind.Agriculture,
                    Buyer, Food, Unit, Option)
            };
        }

        public ExperimentalBuiltFreightAssetGrant Grant(
            ExperimentalTransportInstitution? owner = null,
            ExperimentalTransportInstitution? authorized = null,
            ulong from = Revision, ulong through = Revision + 2,
            ulong assetId = 31, string id = "GRANT-ROAD-31") =>
            new(assetId, owner ?? Owner, authorized ?? Operator,
                id, from, through);

        public ExperimentalBuiltFreightServiceEvidence Evidence(
            ExperimentalSharedFreightOption? option = null) =>
            new(Offer, option ?? Option);

        public ExperimentalTransportInstitutionalRegistry Registry(
            ulong revision = Revision) =>
            new("VS56-REGISTRY", 1, Session.CurrentHash, revision,
                "VS56-PURCHASING", 1,
                "VS56-CALIBRATION", 1, QuoteUnit,
                new[] {
                    new ExperimentalTransportServiceEntitlement(
                        Source, Buyer, 17, Operator,
                        new[] { new ExperimentalTransportFreightRevenueShare(
                            Beneficiary, F(1)) })
                });

        public ExperimentalBuiltTransportNetworkWitness Witness(
            IEnumerable<EconomicTransportBuiltAsset>? assets = null,
            IEnumerable<ExperimentalBuiltFreightAssetGrant>? grants = null,
            IEnumerable<ExperimentalBuiltFreightServiceEvidence>? evidence = null,
            ulong revision = Revision,
            WorldStateHash? hash = null) =>
            new("VS56-BUILT-WITNESS", 1, hash ?? Session.CurrentHash,
                revision, assets ?? new[] { Asset },
                evidence ?? new[] { Evidence() },
                grants ?? new[] { Grant() });

        public ExperimentalBuiltFreightRightsProjection Run(
            ExperimentalBuiltTransportNetworkWitness? witness = null,
            ExperimentalTransportInstitutionalRegistry? registry = null,
            NetworkAuthority? networkAuthority = null,
            RegistryAuthority? registryAuthority = null,
            StrategicEdgeAccessSnapshot? access = null,
            IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId>? anchors = null,
            ExperimentalSimultaneousFreightLane[]? lanes = null) =>
            new ExperimentalBuiltFreightInstitutionalRightsProjector().Project(
                Session, Purchasing, Flows, Catalog, Quotes, Policy,
                QuoteUnit, lanes ?? Lanes, Deliveries, Congestion,
                registry ?? Registry(), registryAuthority ?? new RegistryAuthority(true),
                witness ?? Witness(), networkAuthority ?? new NetworkAuthority(true),
                anchors ?? Anchors, access ?? Access);
    }

    [Fact]
    public void RealConstructedSingleEdgeAndInstitutionalGrantReconcileWithoutMutatingWorld()
    {
        var f = new Fixture();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        var events = f.Session.Events.Count;
        var account = f.Session.FoodCash!.At(f.Session.InitialTile)
            .HouseholdMicrocredits;
        var physicalAuthority = new NetworkAuthority(true);
        var registryAuthority = new RegistryAuthority(true);
        var result = f.Run(networkAuthority: physicalAuthority,
            registryAuthority: registryAuthority);

        Assert.Equal(1, physicalAuthority.Calls);
        Assert.Equal(1, registryAuthority.Calls);
        Assert.Equal(Revision, result.NetworkRevision);
        Assert.Equal(hash, result.WorldStateHash);
        Assert.Equal(1, result.DistinctOperationalAssets);
        var right = Assert.Single(result.ValidatedServiceRights);
        Assert.Equal(Operator, right.Operator);
        Assert.Equal(new ulong[] { 31 }, right.BuiltAssetIds);
        Assert.Equal(new[] { "GRANT-ROAD-31" }, right.AssetGrantIds);
        var lot = Assert.Single(result.InstitutionalProjection.Lots);
        Assert.Equal(Beneficiary, Assert.Single(lot.Beneficiaries).Beneficiary);
        Assert.Equal(0, result.InstitutionalProjection.AbstractBuyerFreightCharges
            .CompareTo(result.InstitutionalProjection.AbstractInstitutionalFreightReceivables));
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.InstitutionalProjection.CanAuthorizeRealSettlement);
        Assert.Equal(hash, f.Session.CurrentHash);
        Assert.Equal(save, f.Session.ExportSaveJson());
        Assert.Equal(account, f.Session.FoodCash.At(f.Session.InitialTile)
            .HouseholdMicrocredits);
        Assert.Equal(events, f.Session.Events.Count);
    }

    [Fact]
    public void ClosedRoadOrInactiveAssetCannotValidateRealService()
    {
        var f = new Fixture();
        var closed = new StrategicEdgeAccessSnapshot(
            new[] { f.Asset.GeographicEdgeId });
        Assert.Throws<InvalidOperationException>(() => f.Run(access: closed));
        var inactive = new EconomicTransportBuiltAsset(
            31, 101, f.Asset.FineGeographicEdgeId!.Value, 7, false);
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(assets: new[] { inactive })));
    }

    [Fact]
    public void MissingExpiredPrematureForeignAndDuplicatedGrantsFailClosed()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Run(
            witness: f.Witness(grants: [])));
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(grants: new[] { f.Grant(from: 1, through: 6) })));
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(grants: new[] { f.Grant(from: 8, through: 9) })));
        Assert.Throws<InvalidOperationException>(() => f.Run(
            witness: f.Witness(grants: new[] { f.Grant(
                authorized: Beneficiary) })));
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(grants: new[] { f.Grant(), f.Grant() })));
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(grants: new[] { f.Grant(assetId: 999) })));
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(grants: new[] { f.Grant(), f.Grant(
                authorized: Beneficiary, owner: Operator, id: "OTHER-GRANT") })));
    }

    [Fact]
    public void WorldNetworkRevisionAndAnchorsMustMatchVS55AndVS52()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(revision: Revision + 1)));
        Assert.Throws<ArgumentException>(() => f.Run(
            registry: f.Registry(revision: Revision + 1)));
        var wrongHash = new WorldStateHash(
            f.Session.CurrentHash.FormatVersion,
            (f.Session.CurrentHash.HexDigest[0] == 'A' ? "B" : "A") +
                f.Session.CurrentHash.HexDigest[1..]);
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(hash: wrongHash)));
        Assert.Throws<ArgumentException>(() => f.Run(
            anchors: new Dictionary<EconomicPointId, PhysicalTacticalTileId> {
                [Source] = f.Anchors[Buyer],
                [Buyer] = f.Anchors[Source]
            }));
    }

    [Fact]
    public void FakeRepricedServiceAndDuplicatedOrMissingOfferEvidenceAreRejected()
    {
        var f = new Fixture();
        var forged = ExperimentalSharedFreightOption.Synthetic(
            17, f.Option.RouteKey,
            new ExperimentalFreightTechnologyPricing(
                101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
                17, 0, 0, 13, 1_000_000, 1_000, 1_000_000),
            f.Option.Pricing.GeographicBaseNumerator,
            f.Option.Corridors);
        Assert.Throws<InvalidOperationException>(() => f.Run(
            witness: f.Witness(evidence: new[] { f.Evidence(forged) }),
            lanes: new[] {
                new ExperimentalSimultaneousFreightLane(
                    f.Flows.SupplyFlows[0], f.Flows.DemandFlows[0],
                    new[] { forged })
            }));
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(evidence: [])));
        Assert.Throws<ArgumentException>(() => f.Run(
            witness: f.Witness(evidence: new[] {
                f.Evidence(), f.Evidence()
            })));
    }

    [Fact]
    public void HostNetworkAndInstitutionalAuthoritiesMustBothApprove()
    {
        var f = new Fixture();
        var rejectedNetwork = new NetworkAuthority(false);
        var registry = new RegistryAuthority(true);
        Assert.Throws<InvalidOperationException>(() => f.Run(
            networkAuthority: rejectedNetwork, registryAuthority: registry));
        Assert.Equal(1, rejectedNetwork.Calls);
        Assert.Equal(0, registry.Calls);

        var acceptedNetwork = new NetworkAuthority(true);
        var rejectedRegistry = new RegistryAuthority(false);
        Assert.Throws<InvalidOperationException>(() => f.Run(
            networkAuthority: acceptedNetwork, registryAuthority: rejectedRegistry));
        Assert.Equal(1, acceptedNetwork.Calls);
        Assert.Equal(1, rejectedRegistry.Calls);
    }

    [Fact]
    public void WitnessCopiesTopLevelGrantAndServiceCollections()
    {
        var f = new Fixture();
        var grants = new[] { f.Grant() };
        var offers = new[] { f.Evidence() };
        var witness = f.Witness(grants: grants, evidence: offers);
        grants[0] = f.Grant(authorized: Beneficiary);
        offers[0] = f.Evidence(null);
        var result = f.Run(witness: witness);
        Assert.Single(result.ValidatedServiceRights);
        Assert.False(result.CanAuthorizeRealSettlement);
    }
}
