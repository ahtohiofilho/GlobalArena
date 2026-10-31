using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs43SimultaneousFreightReferenceTests
{
    private const ulong Quantum = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CommodityId FoodId = new(7);
    private static readonly GoldbergParameters Fine = new(18, 0);
    private static readonly ExperimentalSharedFreightCongestionPolicy Shared =
        new(10, 1_000_000);
    private static readonly ExperimentalFreightTechnologyPricing Trial = new(
        101, 7, 43, ExperimentalLandInfrastructureKind.StoneRoad,
        1, 1, 1, 0, 1_000_000, 1_000, 1_000_000);

    private sealed class FlatValue(ulong perWholeUnit) :
        IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(
            ulong alreadyAllocatedRawQuantity) => new(perWholeUnit, 1);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(
            ulong allocatedRawQuantity) =>
            new((BigInteger)perWholeUnit * allocatedRawQuantity, Quantum);
    }

    private static ExperimentalSharedFreightCorridorKey Corridor(ulong edge) =>
        new(new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(edge)), 101, 7);

    private static ExperimentalSharedFreightOption Option(
        EconomicPointId source, EconomicPointId destination,
        ulong service, long geographicNumerator, ulong edge) =>
        ExperimentalSharedFreightOption.Synthetic(
            service, new StrategicEconomicRouteKey(source, destination),
            Trial, geographicNumerator, new[] { Corridor(edge) });

    private static (EconomicProductionDemandResult Flows,
        FoodCommodityCatalog Catalog,
        EconomicTurnCommodityValueOffer[] Values,
        EconomicTurnSourceReservation[] Reservations,
        ExperimentalSimultaneousFreightLane[] Lanes) Inputs(
            ulong demandRaw = 2 * Quantum, ulong reserve = 0,
            ulong buyerValue = 40, ulong sourceRaw = Quantum)
    {
        var s1 = new EconomicSupplyFlow(
            new EconomicPointId(1), FoodId, EconomicActivityKind.Agriculture,
            sourceRaw);
        var s2 = new EconomicSupplyFlow(
            new EconomicPointId(2), FoodId, EconomicActivityKind.Agriculture,
            sourceRaw);
        var d = new EconomicDemandFlow(
            new EconomicPointId(3), FoodId, demandRaw);
        var lanes = new[]
        {
            new ExperimentalSimultaneousFreightLane(s1, d, new[] {
                Option(s1.EconomicPointId, d.EconomicPointId, 10, 1_000_000, 1),
                Option(s1.EconomicPointId, d.EconomicPointId, 11, 2_000_000, 2)
            }),
            new ExperimentalSimultaneousFreightLane(s2, d, new[] {
                Option(s2.EconomicPointId, d.EconomicPointId, 20, 1_000_000, 1),
                Option(s2.EconomicPointId, d.EconomicPointId, 21, 2_000_000, 3)
            })
        };
        return (new EconomicProductionDemandResult(new[] { s1, s2 }, new[] { d }),
            new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(FoodId, "food", 1) }),
            new[] { new EconomicTurnCommodityValueOffer(
                d.EconomicPointId, FoodId, new FlatValue(buyerValue)) },
            new[] {
                new EconomicTurnSourceReservation(
                    s1.EconomicPointId, FoodId, s1.Activity, reserve),
                new EconomicTurnSourceReservation(
                    s2.EconomicPointId, FoodId, s2.Activity, reserve)
            }, lanes);
    }

    private static ExperimentalSimultaneousFreightComparison Run(
        (EconomicProductionDemandResult Flows, FoodCommodityCatalog Catalog,
            EconomicTurnCommodityValueOffer[] Values,
            EconomicTurnSourceReservation[] Reservations,
            ExperimentalSimultaneousFreightLane[] Lanes) input,
        int exact = 100) =>
        new ExperimentalSimultaneousFreightReference().Compare(
            input.Flows, input.Catalog, input.Values, input.Reservations,
            input.Lanes, Shared, Quantum,
            maxLocalPasses: 5, maxExactConfigurations: exact);

    [Fact]
    public void VariableVolumesAndCorridorSharingFindBetterJointAllocation()
    {
        var comparison = Run(Inputs());
        Assert.Equal(new BigInteger(9), comparison.PossibleConfigurations);
        Assert.Equal(2, comparison.Independent.Trades.Count);
        Assert.Equal(2, comparison.Coordinated.Trades.Count);
        Assert.Equal(2, comparison.Exact!.Trades.Count);
        Assert.True(comparison.Coordinated.NetEconomicValue.CompareTo(
            comparison.Independent.NetEconomicValue) > 0);
        Assert.Equal(0, comparison.Coordinated.NetEconomicValue.CompareTo(
            comparison.Exact.NetEconomicValue));
        Assert.Equal(1, comparison.Exact.CorridorLoads[Corridor(1)] / 1_000_000);
        Assert.Equal(1, comparison.Exact.CorridorLoads.Count -
            (comparison.Exact.CorridorLoads.ContainsKey(Corridor(1)) ? 1 : 0));
        Assert.Equal(0, comparison.Exact.NetEconomicValue.CompareTo(
            new ExperimentalSignedEconomicValue(57, 1)));
    }

    [Fact]
    public void SharedDestinationDemandCannotBeOversubscribed()
    {
        var comparison = Run(Inputs(demandRaw: Quantum));
        Assert.NotNull(comparison.Exact);
        Assert.Single(comparison.Exact!.Trades);
        Assert.Equal(Quantum,
            comparison.Exact.Trades.Aggregate(0UL, (total, trade) => checked(total + trade.RawQuantity)));
        Assert.Equal(0, comparison.Exact.NetEconomicValue.CompareTo(
            new ExperimentalSignedEconomicValue(29, 1)));
        Assert.True(comparison.Coordinated.NetEconomicValue.CompareTo(
            comparison.Exact.NetEconomicValue) <= 0);
    }

    [Fact]
    public void BothSourcesRemainUnusedWhenNoTradeCanBeProfitable()
    {
        var comparison = Run(Inputs(reserve: 60));
        Assert.Empty(comparison.Exact!.Trades);
        Assert.Empty(comparison.Coordinated.Trades);
        Assert.Equal(0, comparison.Exact.NetEconomicValue.Numerator.Sign);
        Assert.Equal(0, comparison.Exact.SharedFreightCost.Numerator.Sign);
    }

    [Fact]
    public void TwoUnitsFromSingleProducerArePossibleAndRawQuantityIsNotFixed()
    {
        var input = Inputs(sourceRaw: 2 * Quantum);
        var comparison = Run(input, exact: 100);
        Assert.True(comparison.PossibleConfigurations > 9);
        Assert.NotNull(comparison.Exact);
        Assert.Equal(2 * Quantum, comparison.Exact!.Trades.Aggregate(0UL, (total, trade) => checked(total + trade.RawQuantity)));
        Assert.All(comparison.Exact.Trades,
            x => Assert.True(x.RawQuantity > 0UL));
        Assert.True(comparison.Exact.CorridorLoads.Values.All(x => x > 0));
    }

    [Fact]
    public void CorrectlyUsesPhysicalCommodityMassInsteadOfMovementPoints()
    {
        var input = Inputs();
        var heavier = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(FoodId, "food", 2) });
        var normal = Run(input);
        var heavy = new ExperimentalSimultaneousFreightReference().Compare(
            input.Flows, heavier, input.Values, input.Reservations, input.Lanes,
            Shared, Quantum, maxExactConfigurations: 100);
        Assert.True(heavy.Exact!.NetEconomicValue.CompareTo(
            normal.Exact!.NetEconomicValue) < 0);
        Assert.True(heavy.Exact.Trades.Count <= 2);
    }

    [Fact]
    public void ReorderedInputReturnsIdenticalExactAndCoordinatedTranscripts()
    {
        var input = Inputs();
        var original = Run(input);
        var reordered = new ExperimentalSimultaneousFreightReference().Compare(
            input.Flows, input.Catalog, input.Values.Reverse(),
            input.Reservations.Reverse(), input.Lanes.Reverse(), Shared,
            Quantum, maxLocalPasses: 5, maxExactConfigurations: 100);
        Assert.Equal(original.PossibleConfigurations, reordered.PossibleConfigurations);
        Assert.Equal(original.Coordinated.Trades.ToArray(),
            reordered.Coordinated.Trades.ToArray());
        Assert.Equal(original.Exact!.Trades.ToArray(), reordered.Exact!.Trades.ToArray());
        Assert.Equal(original.Exact.NetEconomicValue,
            reordered.Exact.NetEconomicValue);
    }

    [Fact]
    public void ExactOracleIsExplicitlyAbsentWhenPortfolioIsTooLarge()
    {
        var comparison = Run(Inputs(), exact: 2);
        Assert.Null(comparison.Exact);
        Assert.True(comparison.CandidateEvaluations > 0);
        Assert.True(comparison.Coordinated.NetEconomicValue.CompareTo(
            comparison.Independent.NetEconomicValue) >= 0);
    }

    [Fact]
    public void OneProducerCannotSupplyTwoDestinationsBeyondItsOwnPhysicalStock()
    {
        var source = new EconomicSupplyFlow(
            new EconomicPointId(1), FoodId, EconomicActivityKind.Agriculture,
            Quantum);
        var d1 = new EconomicDemandFlow(new EconomicPointId(2), FoodId, Quantum);
        var d2 = new EconomicDemandFlow(new EconomicPointId(3), FoodId, Quantum);
        var flows = new EconomicProductionDemandResult(
            new[] { source }, new[] { d1, d2 });
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(FoodId, "food", 1) });
        var value = new[] {
            new EconomicTurnCommodityValueOffer(d1.EconomicPointId,
                FoodId, new FlatValue(40)),
            new EconomicTurnCommodityValueOffer(d2.EconomicPointId,
                FoodId, new FlatValue(40))
        };
        var reserve = new[] { new EconomicTurnSourceReservation(
            source.EconomicPointId, FoodId, source.Activity, 0) };
        var candidates = new[] {
            new ExperimentalSimultaneousFreightLane(source, d1, new[] {
                Option(source.EconomicPointId, d1.EconomicPointId,
                    10, 1_000_000, 1) }),
            new ExperimentalSimultaneousFreightLane(source, d2, new[] {
                Option(source.EconomicPointId, d2.EconomicPointId,
                    11, 1_000_000, 2) })
        };
        var result = new ExperimentalSimultaneousFreightReference().Compare(
            flows, catalog, value, reserve, candidates, corridorPolicy: Shared,
            rawQuantityQuantum: Quantum, maxExactConfigurations: 20);
        Assert.NotNull(result.Exact);
        Assert.Single(result.Exact!.Trades);
        Assert.Equal(Quantum, result.Exact.Trades[0].RawQuantity);
        Assert.Equal(0, result.Exact.NetEconomicValue.CompareTo(
            new ExperimentalSignedEconomicValue(29, 1)));
    }

    private sealed class FallingIntegratedValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(
            ulong alreadyAllocatedRawQuantity)
        {
            var remaining = (BigInteger)25 * Quantum -
                (BigInteger)20 * alreadyAllocatedRawQuantity;
            return new FoodTransportCostFraction(
                BigInteger.Max(0, remaining), Quantum);
        }

        public FoodTransportCostFraction EvaluateIntegratedRawValue(
            ulong allocatedRawQuantity)
        {
            var q = (BigInteger)allocatedRawQuantity;
            return new FoodTransportCostFraction(
                25 * q * Quantum - 10 * q * q,
                (BigInteger)Quantum * Quantum);
        }
    }

    [Fact]
    public void IntegratedDestinationValueIsEvaluatedOnceAcrossCompetingSources()
    {
        var input = Inputs();
        var values = new[] { new EconomicTurnCommodityValueOffer(
            new EconomicPointId(3), FoodId, new FallingIntegratedValue()) };
        var result = new ExperimentalSimultaneousFreightReference().Compare(
            input.Flows, input.Catalog, values, input.Reservations,
            input.Lanes, new ExperimentalSharedFreightCongestionPolicy(
                0, 1_000_000), Quantum, maxExactConfigurations: 100);
        Assert.NotNull(result.Exact);
        Assert.Single(result.Exact!.Trades);
        Assert.Equal(Quantum, result.Exact.Trades[0].RawQuantity);
        Assert.Equal(0, result.Exact.IntegratedBuyerValue.CompareTo(
            new FoodTransportCostFraction(15, 1)));
        Assert.Equal(0, result.Exact.NetEconomicValue.CompareTo(
            new ExperimentalSignedEconomicValue(14, 1)));
    }

    [Fact]
    public void MissingEconomicInputsAndInvalidLanesFailClosed()
    {
        var input = Inputs();
        var solver = new ExperimentalSimultaneousFreightReference();
        Assert.Throws<ArgumentException>(() => solver.Compare(
            input.Flows, input.Catalog, Array.Empty<EconomicTurnCommodityValueOffer>(),
            input.Reservations, input.Lanes, Shared, Quantum));
        Assert.Throws<ArgumentException>(() => solver.Compare(
            input.Flows, input.Catalog, input.Values, input.Reservations,
            input.Lanes.Append(input.Lanes[0]), Shared, Quantum));
        Assert.Throws<ArgumentOutOfRangeException>(() => solver.Compare(
            input.Flows, input.Catalog, input.Values, input.Reservations,
            input.Lanes, Shared, 0));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalSimultaneousFreightLane(
                input.Lanes[0].Supply, input.Lanes[0].Demand,
                new[] { input.Lanes[0].Services[0],
                    input.Lanes[0].Services[0] }));
    }
}
