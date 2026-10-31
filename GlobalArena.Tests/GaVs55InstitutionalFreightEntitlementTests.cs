using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs55InstitutionalFreightEntitlementTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private const string QuoteUnit = "VS40-NOMINAL-FREIGHT";
    private static readonly EconomicPointId Buyer = new(101);
    private static readonly EconomicPointId BuyerTwo = new(102);
    private static readonly EconomicPointId Source = new(201);
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Stone = new(8);
    private static readonly GoldbergParameters Fine = new(18, 0);

    private static readonly ExperimentalTransportInstitution Logistics =
        new("LOGISTICS-A", ExperimentalTransportInstitutionKind.CommercialOperator);
    private static readonly ExperimentalTransportInstitution Community =
        new("COMMUNITY-B", ExperimentalTransportInstitutionKind.Community);
    private static readonly ExperimentalTransportInstitution Public =
        new("PUBLIC-C", ExperimentalTransportInstitutionKind.PublicAuthority);

    private static FoodTransportCostFraction F(long n, long d = 1) => new(n, d);

    private static void Equal(long n, long d, FoodTransportCostFraction actual) =>
        Assert.Equal(0, actual.CompareTo(F(n, d)));

    private sealed class Authority(bool allow) :
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

    private sealed class Fixture
    {
        public CanonicalPhysicalTacticalSession Session { get; } =
            CanonicalPhysicalTacticalSession.Start(
                20261012UL, new GoldbergParameters(3, 0), Fine,
                PhysicalEconomicCadenceMode.AdaptiveThirty,
                PhysicalTacticalGeographyMode.Canonical01C,
                PhysicalFoodMarketMode.LocalCashSettlement,
                PhysicalWorkerMovementMode.TimedAdjacent,
                PhysicalCivilizationIdentityMode.BornWithPopulation);

        public EconomicProductionDemandResult Flows { get; }
        public FoodCommodityCatalog Catalog { get; }
        public ExperimentalAbstractPurchasingProjection Purchasing { get; }
        public ExperimentalAbstractNominalDeliveredQuote[] Quotes { get; }
        public ExperimentalAbstractPriceFreightCalibrationPolicy Policy { get; }
        public ExperimentalSimultaneousFreightLane[] Lanes { get; }
        public ExperimentalAbstractPhysicalFreightDelivery[] Deliveries { get; }
        public ExperimentalSharedFreightCongestionPolicy Congestion { get; } =
            new(1, 1_000_000);

        public Fixture(bool twoBuyers = false, FoodTransportCostFraction? scale = null)
        {
            if (twoBuyers)
            {
                var target = Session.AdjacentWorkable(Session.InitialTile)[0];
                Session.OrderWorkerTransit(Session.InitialTile, target,
                    EconomicActivityKind.Agriculture,
                    EconomicActivityKind.Forestry, 2);
                Session.AdvanceMovementTurn();
                Session.AdvanceMovementTurn();
            }
            var sources = twoBuyers
                ? new[] {
                    new EconomicSupplyFlow(Source, Food,
                        EconomicActivityKind.Agriculture, 2 * Unit)
                }
                : new[] {
                    new EconomicSupplyFlow(Source, Food,
                        EconomicActivityKind.Agriculture, Unit),
                    new EconomicSupplyFlow(Source, Stone,
                        EconomicActivityKind.Mining, Unit)
                };
            var demands = twoBuyers
                ? new[] {
                    new EconomicDemandFlow(Buyer, Food, Unit),
                    new EconomicDemandFlow(BuyerTwo, Food, Unit)
                }
                : new[] {
                    new EconomicDemandFlow(Buyer, Food, Unit),
                    new EconomicDemandFlow(Buyer, Stone, Unit)
                };
            Flows = new EconomicProductionDemandResult(sources, demands);
            Catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(Food, "food", 1),
                new FoodCommodityDefinition(Stone, "stone", 1)
            });
            ExperimentalSharedFreightOption Service(ulong serviceId,
                EconomicPointId buyer) =>
                ExperimentalSharedFreightOption.Synthetic(
                    serviceId, new StrategicEconomicRouteKey(Source, buyer),
                    new ExperimentalFreightTechnologyPricing(
                        101, 7, 55, ExperimentalLandInfrastructureKind.StoneRoad,
                        1, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
                    1_000_000, new[] {
                        new ExperimentalSharedFreightCorridorKey(
                            new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(1)),
                            101, 7)
                    });
            var first = Service(17, Buyer);
            var second = Service(18, twoBuyers ? BuyerTwo : Buyer);
            Lanes = new[] {
                new ExperimentalSimultaneousFreightLane(
                    sources[0], demands[0], new[] { first }),
                new ExperimentalSimultaneousFreightLane(
                    twoBuyers ? sources[0] : sources[1],
                    demands[1], new[] { second })
            };
            var anchors = new List<ExperimentalAbstractBuyerAnchor> {
                new(Buyer, Session.InitialTile, F(1))
            };
            if (twoBuyers)
                anchors.Add(new ExperimentalAbstractBuyerAnchor(
                    BuyerTwo, Session.AdjacentWorkable(Session.InitialTile)[0], F(1)));
            Purchasing = new ExperimentalAbstractPurchasingPowerProjector()
                .Project(Session, Flows, anchors,
                    new[] {
                        new ExperimentalAbstractProducerAnchor(Source, Session.InitialTile)
                    }, Lanes,
                    demands.Select(x => new ExperimentalBuyerCommodityPrice(
                        x.EconomicPointId, x.CommodityId, F(1))),
                    new ExperimentalAbstractPurchasingPolicy(
                        "VS55-PURCHASING", 1, F(5), F(5, 8)));

            Quotes = demands.Select(d =>
                new ExperimentalAbstractNominalDeliveredQuote(
                    d.EconomicPointId, d.CommodityId,
                    F(1), F(1), QuoteUnit)).ToArray();
            Policy = new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "VS55-CALIBRATION", 1, "VS55-PURCHASING", 1,
                QuoteUnit, scale ?? F(1));
            Deliveries = new[] {
                new ExperimentalAbstractPhysicalFreightDelivery(
                    1, Source, EconomicActivityKind.Agriculture,
                    Buyer, Food, Unit, first),
                new ExperimentalAbstractPhysicalFreightDelivery(
                    2, Source,
                    twoBuyers ? EconomicActivityKind.Agriculture :
                        EconomicActivityKind.Mining,
                    twoBuyers ? BuyerTwo : Buyer,
                    twoBuyers ? Food : Stone,
                    Unit, second)
            };
        }

        public ExperimentalTransportServiceEntitlement[] Rights() =>
            new[] {
                new ExperimentalTransportServiceEntitlement(
                    Source, Buyer, 17, Logistics,
                    new[] {
                        new ExperimentalTransportFreightRevenueShare(
                            Community, F(3, 4)),
                        new ExperimentalTransportFreightRevenueShare(
                            Public, F(1, 4))
                    }),
                new ExperimentalTransportServiceEntitlement(
                    Source, Deliveries[1].Buyer, 18, Logistics,
                    new[] {
                        new ExperimentalTransportFreightRevenueShare(
                            Community, F(1))
                    })
            };

        public ExperimentalTransportInstitutionalRegistry Registry(
            IEnumerable<ExperimentalTransportServiceEntitlement>? rights = null,
            string? hash = null,
            ulong revision = 7,
            string purchasingId = "VS55-PURCHASING",
            ulong purchasingVersion = 1,
            string calibrationId = "VS55-CALIBRATION",
            ulong calibrationVersion = 1,
            string nominalUnit = QuoteUnit) =>
            new("VS55-REGISTRY", 1, hash ?? Session.CurrentHash,
                revision, purchasingId, purchasingVersion,
                calibrationId, calibrationVersion, nominalUnit,
                rights ?? Rights());

        public ExperimentalTransportInstitutionalProjection Project(
            ExperimentalTransportInstitutionalRegistry? registry = null,
            Authority? authority = null,
            ExperimentalAbstractPhysicalFreightDelivery[]? deliveries = null,
            ExperimentalSimultaneousFreightLane[]? lanes = null) =>
            new ExperimentalTransportInstitutionalFreightProjector().Project(
                Session, Purchasing, Flows, Catalog, Quotes, Policy,
                QuoteUnit, lanes ?? Lanes, deliveries ?? Deliveries,
                Congestion, registry ?? Registry(), authority ?? new Authority(true));
    }

    [Fact]
    public void ExplicitOperatorAndBeneficiariesReconcileEveryFreightUnitWithoutInventedWallets()
    {
        var f = new Fixture();
        var beforeSave = f.Session.ExportSaveJson();
        var beforeHash = f.Session.CurrentHash;
        var cashBefore = f.Session.FoodCash!.At(f.Session.InitialTile)
            .HouseholdMicrocredits;
        var authority = new Authority(true);
        var result = f.Project(authority: authority);

        Assert.Equal(1, authority.Calls);
        Assert.Equal(2, result.Lots.Count);
        Assert.Equal(2, result.InstitutionalReceivables.Count);
        Assert.All(result.Lots, x => Assert.Equal(Logistics, x.Operator));
        Equal(10, 1, result.AbstractBuyerFreightCharges);
        Equal(10, 1, result.AbstractInstitutionalFreightReceivables);
        Equal(5, 1, result.Lots[0].AbstractFreightDebit);
        Equal(15, 4, result.Lots[0].Beneficiaries.Single(
            x => x.Beneficiary == Community).AbstractFreightRevenue);
        Equal(5, 4, result.Lots[0].Beneficiaries.Single(
            x => x.Beneficiary == Public).AbstractFreightRevenue);
        Equal(35, 4, result.InstitutionalReceivables.Single(
            x => x.Institution == Community).AbstractFreightReceivable);
        Equal(5, 4, result.InstitutionalReceivables.Single(
            x => x.Institution == Public).AbstractFreightReceivable);
        Assert.DoesNotContain(result.InstitutionalReceivables,
            x => x.Institution == Logistics);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.PhysicalReconciliation.CanAuthorizeRealSettlement);
        Assert.Equal(beforeHash, f.Session.CurrentHash);
        Assert.Equal(beforeSave, f.Session.ExportSaveJson());
        Assert.Equal(cashBefore, f.Session.FoodCash.At(f.Session.InitialTile)
            .HouseholdMicrocredits);
        Assert.Empty(f.Session.Events);
    }

    [Fact]
    public void DifferentLocalBuyersRetainSeparateFreightChargesButOneSharedInstitutionalBeneficiary()
    {
        var f = new Fixture(twoBuyers: true);
        var result = f.Project();
        Assert.Equal(2, result.PhysicalReconciliation.Buyers.Count);
        Assert.Equal(2, result.Lots.Select(x => x.Buyer).Distinct().Count());
        Equal(5, 1, result.Lots[0].AbstractFreightDebit);
        Equal(5, 1, result.Lots[1].AbstractFreightDebit);
        Equal(10, 1, result.AbstractInstitutionalFreightReceivables);
        Equal(105, 4, result.PhysicalReconciliation.Buyers[0]
            .SpendableAbstractPurchasing);
        Equal(35, 4, result.PhysicalReconciliation.Buyers[1]
            .SpendableAbstractPurchasing);
    }

    [Fact]
    public void ExactFractionalConversionPreservesBeneficiarySharesWithoutRounding()
    {
        var f = new Fixture(scale: F(3, 2));
        var result = f.Project();
        Equal(15, 1, result.AbstractBuyerFreightCharges);
        Equal(15, 1, result.AbstractInstitutionalFreightReceivables);
        Equal(45, 8, result.Lots[0].Beneficiaries.Single(
            x => x.Beneficiary == Community).AbstractFreightRevenue);
        Equal(15, 8, result.Lots[0].Beneficiaries.Single(
            x => x.Beneficiary == Public).AbstractFreightRevenue);
        Assert.False(result.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void EmptyDeliveryNeverCreatesRevenueNorSettlementPermission()
    {
        var f = new Fixture();
        var result = f.Project(deliveries: []);
        Assert.Empty(result.Lots);
        Assert.Empty(result.InstitutionalReceivables);
        Equal(0, 1, result.AbstractBuyerFreightCharges);
        Equal(0, 1, result.AbstractInstitutionalFreightReceivables);
        Assert.False(result.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void MissingDuplicatedExtraAndContradictoryRightsFailClosed()
    {
        var f = new Fixture();
        var baseRights = f.Rights();
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(baseRights.Take(1))));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(baseRights.Concat(baseRights))));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(baseRights.Append(
                baseRights[0] with { ServiceId = 999 }))));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(baseRights.Select(x =>
                x.ServiceId == 17 ? x with { Source = new EconomicPointId(998) } : x))));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(baseRights.Select(x =>
                x.ServiceId == 17 ? x with {
                    RevenueShares = new[] {
                        new ExperimentalTransportFreightRevenueShare(
                            Community, F(1, 2))
                    }
                } : x))));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(baseRights.Select(x =>
                x.ServiceId == 17 ? x with {
                    RevenueShares = new[] {
                        new ExperimentalTransportFreightRevenueShare(
                            Community, F(3, 4)),
                        new ExperimentalTransportFreightRevenueShare(
                            Community, F(1, 4))
                    }
                } : x))));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(baseRights.Select(x =>
                x.ServiceId == 17 ? x with {
                    RevenueShares = new[] {
                        new ExperimentalTransportFreightRevenueShare(
                            new ExperimentalTransportInstitution("COMMUNITY-B",
                                ExperimentalTransportInstitutionKind.PublicAuthority),
                            F(1))
                    }
                } : x))));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(baseRights.Select(x =>
                x.ServiceId == 17 ? x with {
                    Operator = new ExperimentalTransportInstitution("",
                        ExperimentalTransportInstitutionKind.CommercialOperator)
                } : x))));
    }

    [Fact]
    public void StaleWorldRevisionPolicyAndUntrustedRegistryAreRejected()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(hash: "FOREIGN-WORLD-HASH")));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(purchasingId: "OTHER")));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(purchasingVersion: 2)));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(calibrationId: "OTHER")));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(calibrationVersion: 2)));
        Assert.Throws<ArgumentException>(() => f.Project(
            registry: f.Registry(nominalUnit: "OTHER")));
        Assert.Throws<ArgumentException>(() => f.Registry(revision: 0));

        var rejected = new Authority(false);
        Assert.Throws<InvalidOperationException>(() =>
            f.Project(authority: rejected));
        Assert.Equal(1, rejected.Calls);
    }

    [Fact]
    public void RegistryDefensivelyCopiesSharesAndDoesNotAcceptRepricedServiceObject()
    {
        var f = new Fixture();
        var mutableShares = new[] {
            new ExperimentalTransportFreightRevenueShare(Community, F(1))
        };
        var rights = f.Rights();
        rights[1] = rights[1] with { RevenueShares = mutableShares };
        var registry = f.Registry(rights);
        mutableShares[0] =
            new ExperimentalTransportFreightRevenueShare(Public, F(1));
        var result = f.Project(registry);
        Equal(35, 4, result.InstitutionalReceivables.Single(
            x => x.Institution == Community).AbstractFreightReceivable);

        var first = f.Deliveries[0];
        var replacement = ExperimentalSharedFreightOption.Synthetic(
            first.Service.ServiceId,
            first.Service.RouteKey,
            new ExperimentalFreightTechnologyPricing(
                101, 7, 55, ExperimentalLandInfrastructureKind.StoneRoad,
                7, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
            1_000_000, first.Service.Corridors);
        Assert.Throws<ArgumentException>(() => f.Project(
            deliveries: new[] {
                first with { Service = replacement }, f.Deliveries[1]
            }));
    }

    [Fact]
    public void ReorderedLotsAndInstitutionalRightsProduceSameEconomicValues()
    {
        var f = new Fixture();
        var first = f.Project();
        var reordered = f.Project(
            registry: f.Registry(f.Rights().Reverse()),
            deliveries: f.Deliveries.Reverse().ToArray());
        Assert.Equal(first.Lots.Select(x => (x.LotId, x.ServiceId)),
            reordered.Lots.Select(x => (x.LotId, x.ServiceId)));
        for (var i = 0; i < first.InstitutionalReceivables.Count; i++)
        {
            Assert.Equal(first.InstitutionalReceivables[i].Institution,
                reordered.InstitutionalReceivables[i].Institution);
            Assert.Equal(0,
                first.InstitutionalReceivables[i].AbstractFreightReceivable
                    .CompareTo(reordered.InstitutionalReceivables[i]
                        .AbstractFreightReceivable));
        }
        Assert.False(reordered.CanAuthorizeRealSettlement);
    }
}
