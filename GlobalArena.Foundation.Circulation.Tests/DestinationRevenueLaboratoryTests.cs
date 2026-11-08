using System.Text.Json;
using GlobalArena.Foundation.Circulation;

namespace GlobalArena.Foundation.Circulation.Tests;

public sealed class DestinationRevenueLaboratoryTests
{
    private const long S = DestinationRevenueLaboratory.Scale;

    private static EdgeTariffReference Reference(long capitalRaw = S)
    {
        var neutral = NaturalEdgeEvaluator.Evaluate(
            new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Land, 0, 0, 0),
            NaturalEdgeReferenceProfiles.CandidateA);
        var invested = EdgeInvestmentLaboratory.Evaluate(neutral, capitalRaw, S);
        return EdgeTariffReferenceLaboratory.Evaluate(neutral, invested, 4);
    }

    private static TransientEdgeUse Edge(long id = 40, long owner = 2) =>
        new(id, Reference(), new[] { new EdgeCapitalHolding(owner, S) });

    private static TransientEdgeUse SharedEdge(long id = 40) =>
        new(id, Reference(), new[] {
            new EdgeCapitalHolding(1, 250_000),
            new EdgeCapitalHolding(2, 750_000)
        });

    private static TransientDestinationSale External(long id = 1, long destination = 20,
        long quantity = S, long seller = 1, params TransientEdgeUse[] edges) =>
        new(id, 10, destination, 5, seller, quantity,
            edges.Length == 0 ? new[] { Edge() } : edges);

    private static TransientDestinationSale Local(long id, long destination,
        long quantity, long seller = 1) =>
        new(id, destination, destination, 5, seller, quantity,
            Array.Empty<TransientEdgeUse>());

    private static DestinationMarketInput Market(long destination = 20, long demand = S,
        long quantity = S) => new(destination, 5, demand, quantity);

    [Fact]
    public void MarketPriceUsesTotalQForDestinationProductNotEachOrigin()
    {
        var projection = DestinationRevenueLaboratory.Settle(
            new[] { Market(20, demand: 4*S, quantity: 4*S) },
            new[] {
                Local(1, 20, S, seller: 1),
                Local(2, 20, 3*S, seller: 2)
            });

        Assert.Single(projection.Markets);
        Assert.Equal(S, projection.Markets[0].PriceRaw);
        Assert.Equal(4*S, projection.TotalGrossRaw);
        Assert.Equal(4*S, projection.TotalCreditsRaw);
        Assert.Equal(S, projection.Sales.Single(x=>x.FlowId == 1).GrossRevenueRaw);
        Assert.Equal(3*S, projection.Sales.Single(x=>x.FlowId == 2).GrossRevenueRaw);
        Assert.Equal(2, projection.CivilizationCredits.Count);
        Assert.Equal(S, projection.CivilizationCredits.Single(x=>x.CivilizationId == 1).CreditRaw);
        Assert.Equal(3*S, projection.CivilizationCredits.Single(x=>x.CivilizationId == 2).CreditRaw);
    }

    [Fact]
    public void F071IdenticalAggregatedEdgeVolumeCanCarryDifferentDestinationValue()
    {
        // X: Q=1,D=4 -> P_X=2; Y: Q=1,D=1 -> P_Y=1.
        // Both alternatives use the SAME edge with ONE unit of flow,
        // but the tariff revenue must reflect WHICH destination used it.
        var markets = new[] { Market(20, 4*S), Market(30, S) };
        var highPriceCrosses = DestinationRevenueLaboratory.Settle(markets,
            new[] { External(1, 20), Local(2, 30, S) });
        var lowPriceCrosses = DestinationRevenueLaboratory.Settle(markets,
            new[] { Local(1, 20, S), External(2, 30) });

        Assert.Equal(3*S, highPriceCrosses.TotalGrossRaw);
        Assert.Equal(highPriceCrosses.TotalGrossRaw, lowPriceCrosses.TotalGrossRaw);
        Assert.Equal(2*S, highPriceCrosses.Markets.Single(x=>x.DestinationNodeId == 20).PriceRaw);
        Assert.Equal(S, highPriceCrosses.Markets.Single(x=>x.DestinationNodeId == 30).PriceRaw);

        var highOwner = highPriceCrosses.CivilizationCredits.Single(
            x=>x.CivilizationId == 2).CreditRaw;
        var lowOwner = lowPriceCrosses.CivilizationCredits.Single(
            x=>x.CivilizationId == 2).CreditRaw;

        Assert.Equal(917_073, highOwner);
        Assert.Equal(458_536, lowOwner);
        Assert.True(highOwner > lowOwner);
        Assert.Equal(1, highPriceCrosses.Sales.Sum(x=>x.ExternalOwnerCredits.Count));
        Assert.Equal(1, lowPriceCrosses.Sales.Sum(x=>x.ExternalOwnerCredits.Count));
    }

    [Fact]
    public void F060SellerAndTwoIndependentEdgeOwnersReceiveGrossOnlyOnce()
    {
        var projection = DestinationRevenueLaboratory.Settle(
            new[] { Market(20, 4*S, 2*S) },
            new[] { External(1, 20, 2*S, 1, Edge(40, 2), Edge(41, 3)) });
        var result = Assert.Single(projection.Sales);

        Assert.Equal(1_414_213, result.DestinationPriceRaw); // floor(sqrt(2)*1e6)
        Assert.Equal(2_828_426, result.GrossRevenueRaw);
        Assert.Equal(2 * Reference().WeightRaw, result.ExternalWeightRaw);
        Assert.Equal(2, result.ExternalOwnerCredits.Count);
        Assert.All(result.ExternalOwnerCredits, x=>Assert.NotEqual(result.SellerCivilizationId, x.OwnerCivilizationId));
        Assert.Equal(result.GrossRevenueRaw,
            result.SellerNetRaw + result.ExternalOwnerCredits.Sum(x=>x.CreditRaw));
        Assert.Equal(projection.TotalGrossRaw, projection.TotalCreditsRaw);
        Assert.Equal(3, projection.CivilizationCredits.Count);
    }

    [Fact]
    public void CoownerPaysOnlyOtherCapitalShareAndSelfOwnerGetsNoTariff()
    {
        var projection = DestinationRevenueLaboratory.Settle(
            new[] { Market(20) }, new[] { External(1, 20, S, 1, SharedEdge()) });
        var sale = Assert.Single(projection.Sales);
        Assert.Equal(635_134, sale.ExternalWeightRaw);
        var credit = Assert.Single(sale.ExternalOwnerCredits);
        Assert.Equal(2, credit.OwnerCivilizationId);
        Assert.Equal(40, credit.StableEdgeId);
        Assert.Equal(S, sale.GrossRevenueRaw);
        Assert.Equal(S, sale.SellerNetRaw + credit.CreditRaw);
        Assert.DoesNotContain(sale.ExternalOwnerCredits, x=>x.OwnerCivilizationId == 1);
    }

    [Fact]
    public void FullyOwnedEdgeDoesNotChargeTheProducingCivilization()
    {
        var projection = DestinationRevenueLaboratory.Settle(
            new[] { Market(20) },
            new[] { External(1, 20, S, seller: 2, Edge(40, 2)) });
        var result = Assert.Single(projection.Sales);
        Assert.Equal(0, result.ExternalWeightRaw);
        Assert.Empty(result.ExternalOwnerCredits);
        Assert.Equal(S, result.SellerNetRaw);
        Assert.Single(projection.CivilizationCredits);
        Assert.Equal(2, projection.CivilizationCredits[0].CivilizationId);
    }

    [Fact]
    public void UnimprovedNaturalEdgeHasNoOwnersAndNoTariff()
    {
        var edge = new TransientEdgeUse(40, Reference(0), Array.Empty<EdgeCapitalHolding>());
        var projection = DestinationRevenueLaboratory.Settle(
            new[] { Market(20) }, new[] { External(1, 20, S, 1, edge) });
        var sale = Assert.Single(projection.Sales);
        Assert.Equal(0, sale.ExternalWeightRaw);
        Assert.Equal(S, sale.SellerNetRaw);
        Assert.Empty(sale.ExternalOwnerCredits);
    }

    [Fact]
    public void SaleLocalToItsOwnMarketRequiresZeroEdgesAndPaysNoTariff()
    {
        var projection = DestinationRevenueLaboratory.Settle(
            new[] { Market(20, demand: 4*S, quantity: S) },
            new[] { Local(1, 20, S) });
        var sale = Assert.Single(projection.Sales);
        Assert.Equal(2*S, sale.DestinationPriceRaw);
        Assert.Equal(2*S, sale.GrossRevenueRaw);
        Assert.Equal(2*S, sale.SellerNetRaw);
        Assert.Empty(sale.ExternalOwnerCredits);
    }

    [Fact]
    public void ExactMarketValueApportionmentUsesStableLotIdentityWhenRemaindersTie()
    {
        // Three equal one-microquantity sales; fixed-point price ~1.82574.
        // Market gross rounds to five micromoney units: 2+2+1.
        var markets = new[] { Market(20, demand: 10, quantity: 3) };
        var projection = DestinationRevenueLaboratory.Settle(markets,
            new[] { Local(9, 20, 1), Local(3, 20, 1), Local(5, 20, 1) });

        Assert.Equal(1_825_741, projection.Markets[0].PriceRaw);
        Assert.Equal(5, projection.TotalGrossRaw);
        Assert.Equal(2, projection.Sales.Single(x=>x.FlowId == 3).GrossRevenueRaw);
        Assert.Equal(2, projection.Sales.Single(x=>x.FlowId == 5).GrossRevenueRaw);
        Assert.Equal(1, projection.Sales.Single(x=>x.FlowId == 9).GrossRevenueRaw);
        Assert.Equal(5, projection.TotalCreditsRaw);
    }

    [Fact]
    public void TinySalesStillReconcileWithoutFabricatingAThirdPayment()
    {
        var projection = DestinationRevenueLaboratory.Settle(
            new[] { Market(20, demand: 10, quantity: 3) },
            new[] {
                External(3, 20, 1, 1, Edge(40, 2)),
                External(5, 20, 1, 1, Edge(40, 2)),
                External(9, 20, 1, 1, Edge(40, 2))
            });
        Assert.Equal(5, projection.TotalGrossRaw);
        Assert.Equal(5, projection.TotalCreditsRaw);
        Assert.Equal(3, projection.Sales.Count);
        Assert.All(projection.Sales, s=>
            Assert.Equal(s.GrossRevenueRaw, s.SellerNetRaw +
                s.ExternalOwnerCredits.Sum(x=>x.CreditRaw)));
    }

    [Fact]
    public void InputOrderDoesNotChangePerFlowOrPerCivilizationCredits()
    {
        var markets = new[] { Market(20, 4*S, 2*S), Market(30, S, S) };
        var saleA = External(4, 20, S, 1, Edge(40, 2), Edge(50, 3));
        var saleB = External(8, 20, S, 1, Edge(50, 3), Edge(40, 2));
        var saleC = Local(2, 30, S, 2);

        var first = DestinationRevenueLaboratory.Settle(markets, new[] { saleA, saleB, saleC });
        var reverse = DestinationRevenueLaboratory.Settle(
            markets.Reverse().ToArray(), new[] { saleC, saleB, saleA });

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(reverse));
        Assert.Equal(first.TotalGrossRaw, first.TotalCreditsRaw);
    }

    [Fact]
    public void OwnerHoldingEnumerationOrderDoesNotAffectRevenueOrTariff()
    {
        var a = SharedEdge();
        var b = a with { Holdings = a.Holdings.Reverse().ToArray() };
        var left = DestinationRevenueLaboratory.Settle(
            new[] { Market(20) }, new[] { External(1, 20, S, 4, a) });
        var right = DestinationRevenueLaboratory.Settle(
            new[] { Market(20) }, new[] { External(1, 20, S, 4, b) });
        Assert.Equal(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right));
        Assert.Equal(2, left.Sales[0].ExternalOwnerCredits.Count);
    }

    [Fact]
    public void OneCivilizationCanEarnTariffsOnSeveralEdgesAndProductionInOneBatch()
    {
        var projection = DestinationRevenueLaboratory.Settle(
            new[] { Market(20, 4*S, 2*S), Market(30, S, S) },
            new[] {
                External(1, 20, S, 1, Edge(40, 2), Edge(41, 2)),
                Local(2, 20, S, seller: 2),
                Local(3, 30, S, seller: 2)
            });
        var totalOwners = projection.Sales.SelectMany(s=>s.ExternalOwnerCredits)
            .Where(x=>x.OwnerCivilizationId == 2).Sum(x=>x.CreditRaw);
        var civilizationTwo = projection.CivilizationCredits.Single(x=>x.CivilizationId == 2).CreditRaw;
        Assert.True(totalOwners > 0);
        Assert.Equal(totalOwners + 2*S, civilizationTwo);
        Assert.Equal(projection.TotalGrossRaw, projection.TotalCreditsRaw);
    }

    [Fact]
    public void DuplicateFlowOrIncompleteDestinationQuantityIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(
                new[] { Market(20, S, 2*S) }, new[] { Local(1, 20, S) }));
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(
                new[] { Market(20, S, 2*S) }, new[] {
                    Local(1, 20, S), Local(1, 20, S)
                }));
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(
                new[] { Market(20), Market(20) }, new[] { Local(1, 20, S) }));
    }

    [Fact]
    public void MissingMarketsUnmatchedCommodityAndUnattributedNonlocalSaleAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(Array.Empty<DestinationMarketInput>(),
                new[] { Local(1, 20, S) }));
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(new[] { Market(20) },
                new[] { new TransientDestinationSale(1, 20, 20, 6, 1, S,
                    Array.Empty<TransientEdgeUse>()) }));
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(new[] { Market(20) },
                new[] { new TransientDestinationSale(1, 10, 20, 5, 1, S,
                    Array.Empty<TransientEdgeUse>()) }));
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(new[] { Market(20) },
                new[] { External(1, 20, S, 1), Local(2, 20, S) }));
    }

    [Fact]
    public void DuplicateEdgeOnSaleOrConflictingSnapshotAcrossSalesFails()
    {
        var edge = Edge(40, 2);
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(new[] { Market(20) },
                new[] { External(1, 20, S, 1, edge, edge) }));

        var changed = new TransientEdgeUse(40, Reference(S),
            new[] { new EdgeCapitalHolding(3, S) });
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(new[] { Market(20, S, 2*S) },
                new[] {
                    External(1, 20, S, 1, edge),
                    External(2, 20, S, 1, changed)
                }));
    }

    [Fact]
    public void InvalidOwnershipMustNotPrintMoney()
    {
        var edge = Edge(40, 2);
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(new[] { Market(20) },
                new[] { External(1, 20, S, 1,
                    edge with { Holdings = new[] { new EdgeCapitalHolding(2, S-1) } }) }));
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(new[] { Market(20) },
                new[] { External(1, 20, S, 1,
                    edge with { Holdings = new[] {
                        new EdgeCapitalHolding(2, S/2),
                        new EdgeCapitalHolding(2, S/2)
                    } }) }));
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Settle(new[] { Market(20) },
                new[] { External(1, 20, S, 1,
                    edge with { Holdings = new[] { new EdgeCapitalHolding(2, -S) } }) }));
    }

    [Fact]
    public void F043EmptyMarketsHaveNoPriceAndInvalidDemandsFail()
    {
        var empty = DestinationRevenueLaboratory.Quote(
            new DestinationMarketInput(20, 5, 0, 0));
        Assert.Equal(0, empty.PriceRaw);
        Assert.Equal(0, empty.GrossRevenueRaw);
        Assert.Throws<ArgumentException>(() =>
            DestinationRevenueLaboratory.Quote(new DestinationMarketInput(20, 5, 0, S)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DestinationRevenueLaboratory.Quote(new DestinationMarketInput(-1, 5, S, S)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DestinationRevenueLaboratory.Quote(new DestinationMarketInput(20, 5, S, -1)));

        var noSales = DestinationRevenueLaboratory.Settle(
            new[] { new DestinationMarketInput(20, 5, S, 0) },
            Array.Empty<TransientDestinationSale>());
        Assert.Equal(0, noSales.TotalCreditsRaw);
        Assert.Empty(noSales.Sales);
    }

    [Fact]
    public void PriceUnderflowFailsInsteadOfCreatingFreeGoods()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DestinationRevenueLaboratory.Quote(
                new DestinationMarketInput(20, 5, 1, long.MaxValue)));
    }

    [Fact]
    public void FixedPointPriceChecksumIsDeterministicAcrossPlatforms()
    {
        long sum = 0;
        for (int i = 1; i <= 4_096; i++)
        {
            var quote = DestinationRevenueLaboratory.Quote(
                new DestinationMarketInput(20, 5,
                    S + (i % 23) * 17_433L,
                    S + i * 113L));
            sum = checked(sum + quote.PriceRaw * 7 + quote.GrossRevenueRaw * 3);
        }
        Assert.Equal(43_143_934_645L, sum);
    }
}
