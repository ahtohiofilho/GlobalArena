using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoodStrategicEdgeCongestionEvaluatorTests
{
    private static (WorldGenerationResult World, WorldState State) Fixture()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civilizations = RuntimeCivilizationMaterializer.Materialize(world, 2);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
            civilizations);
        var source = civilizations.Records[0];
        var destination = civilizations.Records[1];

        var state = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL), source.Id,
                        source.StartCellId!.Value, 2UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 2UL)
                        }),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL), destination.Id,
                        destination.StartCellId!.Value, 0UL),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(3UL), destination.Id,
                        destination.StartCellId!.Value, 0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(new EconomicPointId(2UL), 10UL),
                    new PopulationPointRuntimeState(new EconomicPointId(3UL), 10UL)
                }));
        return (world, state);
    }

    private static FoodCommodityCatalog Catalog() =>
        new(new[]
        {
            new FoodCommodityDefinition(new CommodityId(8U), "roots", 2_000UL),
            new FoodCommodityDefinition(new CommodityId(7U), "grain", 1_000UL)
        });

    private static EconomicTradeAllocation Trade(
        ulong source = 1UL, ulong destination = 2UL,
        uint food = 7U, ulong quantity = 1_000_000UL) =>
        new(new EconomicPointId(source),
            EconomicActivityKind.Agriculture,
            new EconomicPointId(destination),
            new CommodityId(food), quantity, 900_000UL, 100_000UL);

    private static StrategicEconomicRoute Route(
        WorldGenerationResult world, WorldState state,
        ulong from = 1UL, ulong to = 2UL)
    {
        var resolved = new StrategicEconomicRouteResolver(
            world.StrategicSurfaceGraph).ResolveDetailed(
                state.Economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(from), new EconomicPointId(to)),
                StrategicEdgeAccessSnapshot.AllOpen);
        Assert.True(resolved.IsReachable);
        return resolved.Route!;
    }

    private static FoodStrategicEdgeCongestionProfile[] Profiles(
        StrategicEconomicRoute route, ulong characteristic = 2_500UL,
        ulong baseCost = 100UL, ulong congestionCost = 100UL) =>
        route.EdgePath.Select(edge =>
            new FoodStrategicEdgeCongestionProfile(
                edge, characteristic, baseCost, congestionCost))
            .ToArray();

    private static FoodStrategicEdgeCongestionResult Evaluate(
        WorldGenerationResult world, WorldState state,
        EconomicMarketAllocationResult market,
        IEnumerable<FoodStrategicEdgeCongestionProfile> profiles,
        StrategicEdgeAccessSnapshot? edgeAccess = null,
        FoodCommodityCatalog? catalog = null) =>
        new FoodStrategicEdgeCongestionEvaluator().Evaluate(
            world, state,
            edgeAccess ?? StrategicEdgeAccessSnapshot.AllOpen,
            market, catalog ?? Catalog(), profiles);

    [Fact]
    public void ContinuousQuadraticMarginalCostHasNoLoadCutoff()
    {
        var profile = new FoodStrategicEdgeCongestionProfile(
            new StrategicEdgeId(1UL), 1_000UL, 2UL, 10UL);
        var d = (BigInteger)StrategicScalarField.Denominator;

        var zero = profile.EvaluateMarginalCostPerGram(BigInteger.Zero);
        var half = profile.EvaluateMarginalCostPerGram(500 * d);
        var reference = profile.EvaluateMarginalCostPerGram(1_000 * d);
        var doubleReference = profile.EvaluateMarginalCostPerGram(2_000 * d);
        var thousandfold = profile.EvaluateMarginalCostPerGram(1_000_000 * d);

        Assert.Equal(new BigInteger(2), zero.Numerator);
        Assert.Equal(BigInteger.One, zero.Denominator);
        Assert.Equal(new BigInteger(9), half.Numerator);
        Assert.Equal(new BigInteger(2), half.Denominator);
        Assert.Equal(new BigInteger(12), reference.Numerator);
        Assert.Equal(new BigInteger(42), doubleReference.Numerator);
        Assert.Equal(new BigInteger(10_000_002), thousandfold.Numerator);
        Assert.True(zero.CompareTo(half) < 0);
        Assert.True(half.CompareTo(reference) < 0);
        Assert.True(reference.CompareTo(doubleReference) < 0);
        Assert.True(doubleReference.CompareTo(thousandfold) < 0);
    }

    [Fact]
    public void CrossingCharacteristicScaleDoesNotTriggerAnyDiscretePenalty()
    {
        var profile = new FoodStrategicEdgeCongestionProfile(
            new StrategicEdgeId(1UL), 1_000UL, 100UL, 100UL);
        var reference = (BigInteger)1_000 * StrategicScalarField.Denominator;

        var before = profile.EvaluateMarginalCostPerGram(reference - 1);
        var atReference = profile.EvaluateMarginalCostPerGram(reference);
        var after = profile.EvaluateMarginalCostPerGram(reference + 1);

        Assert.Equal(new BigInteger(200), atReference.Numerator);
        Assert.Equal(BigInteger.One, atReference.Denominator);
        Assert.True(before.CompareTo(atReference) < 0);
        Assert.True(atReference.CompareTo(after) < 0);

        var increaseJustAfter = new FoodTransportCostFraction(
            after.Numerator * atReference.Denominator
                - atReference.Numerator * after.Denominator,
            after.Denominator * atReference.Denominator);

        // One microgram beyond the reference load causes only the local
        // smooth-curve increment, not a discontinuous congestion penalty.
        Assert.True(increaseJustAfter.CompareTo(
            new FoodTransportCostFraction(1, 1_000)) < 0);
    }

    [Fact]
    public void TotalCostIsConvexIntegralNotAClippedThresholdPenalty()
    {
        var profile = new FoodStrategicEdgeCongestionProfile(
            new StrategicEdgeId(1UL), 1_000UL, 2UL, 10UL);
        var d = (BigInteger)StrategicScalarField.Denominator;
        var first = profile.EvaluateTotalCost(1_000 * d);
        var second = profile.EvaluateTotalCost(2_000 * d);

        Assert.Equal(new BigInteger(16_000), first.Numerator);
        Assert.Equal(new BigInteger(3), first.Denominator);
        Assert.Equal(new BigInteger(92_000), second.Numerator);
        Assert.Equal(new BigInteger(3), second.Denominator);
        Assert.True(second.CompareTo(first) > 0);
        Assert.Equal(BigInteger.Zero,
            profile.EvaluateTotalCost(BigInteger.Zero).Numerator);
    }

    [Fact]
    public void CostsRemainWellDefinedFarBeyondCharacteristicThroughput()
    {
        var profile = new FoodStrategicEdgeCongestionProfile(
            new StrategicEdgeId(1UL), 1UL, 0UL, 1UL);
        var huge = BigInteger.Pow(new BigInteger(10), 36);

        var marginal = profile.EvaluateMarginalCostPerGram(huge);
        var total = profile.EvaluateTotalCost(huge);

        Assert.True(marginal.Numerator > ulong.MaxValue);
        Assert.True(total.Numerator > ulong.MaxValue);
        Assert.True(marginal.Denominator > 0);
        Assert.True(total.Denominator > 0);
        Assert.True(marginal.CompareTo(
            profile.EvaluateMarginalCostPerGram(huge - 1)) > 0);
    }

    [Fact]
    public void SharedEdgeAcrossTwoFoodsRaisesMarginalCostSmoothly()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        Assert.NotEmpty(route.EdgePath);

        var grain = Evaluate(world, state,
            new EconomicMarketAllocationResult(new[] { Trade(food: 7U) }),
            Profiles(route));
        var together = Evaluate(world, state,
            new EconomicMarketAllocationResult(new[]
            {
                Trade(destination: 3UL, food: 8U),
                Trade(food: 7U)
            }), Profiles(route));

        Assert.Equal(route.HopCount, together.UsedEdges.Count);
        foreach (var edge in together.UsedEdges)
        {
            Assert.Equal(new BigInteger(3_000_000_000UL), edge.LoadMicrograms);
            Assert.Equal(new BigInteger(244), edge.MarginalCostPerGram.Numerator);
            Assert.Equal(BigInteger.One, edge.MarginalCostPerGram.Denominator);
            Assert.True(edge.MarginalCostPerGram.CompareTo(
                grain.UsedEdges.Single(x =>
                    x.StrategicEdgeId == edge.StrategicEdgeId)
                    .MarginalCostPerGram) > 0);
        }
    }

    [Fact]
    public void DifferentCharacteristicScalesChangeCostSmoothlyWithoutHardMaximum()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var market = new EconomicMarketAllocationResult(new[] { Trade() });

        var easier = Evaluate(world, state, market,
            Profiles(route, characteristic: 10_000UL));
        var harder = Evaluate(world, state, market,
            Profiles(route, characteristic: 500UL));

        Assert.All(easier.UsedEdges, edge =>
        {
            var other = harder.UsedEdges.Single(x =>
                x.StrategicEdgeId == edge.StrategicEdgeId);
            Assert.True(other.MarginalCostPerGram
                .CompareTo(edge.MarginalCostPerGram) > 0);
        });
    }

    [Fact]
    public void ActualResidentMarketAllocatesFoodIndependentlyButEvaluatorAggregatesMass()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var catalog = Catalog();
        var profiles = FoodResidentDemandProfileMaterializer.Materialize(
            state, catalog, new[]
            {
                new FoodResidentConsumptionRequirement(
                    new EconomicPointId(2UL), new CommodityId(7U), 500UL),
                new FoodResidentConsumptionRequirement(
                    new EconomicPointId(3UL), new CommodityId(8U), 1_000UL)
            });

        var phase = new EconomicResidentFoodMarketTurnPhase(
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 1_000_000L),
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(8U),
                    EconomicActivityKind.Agriculture, 1_000_000L)
            },
            profiles,
            new[]
            {
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL), new EconomicPointId(2UL)),
                    route.HopCount, 100_000UL, 1_000_000UL, 0UL),
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL), new EconomicPointId(3UL)),
                    route.HopCount, 100_000UL, 1_000_000UL, 0UL)
            },
            new EconomicMarketAllocationPolicy(
                900_000UL, 900_000UL, 1, 1),
            catalog);

        var hash = new CanonicalWorldStateHasher().Compute(state);
        var result = phase.Resolve(
            new TurnResolutionInput(
                state, Array.Empty<ISimulationCommand>(),
                new SimulationContext(
                    new TurnNumber(1UL), new SimulationSeed(5UL))), state);
        var selected = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            result.Events[1]).Allocation;

        Assert.Equal(new[] { 7U, 8U },
            selected.Allocations.Select(x => x.CommodityId.Value));
        var evaluation = Evaluate(world, state, selected, Profiles(route));
        Assert.All(evaluation.UsedEdges, edge =>
            Assert.Equal(new BigInteger(244),
                edge.MarginalCostPerGram.Numerator));

        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
        Assert.Same(state, result.ResultingWorldState);
    }

    [Fact]
    public void MissingProfileForUsedEdgeFailsInsteadOfAssumingFreeTransport()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        Assert.Throws<InvalidOperationException>(() =>
            Evaluate(world, state,
                new EconomicMarketAllocationResult(new[] { Trade() }),
                Profiles(route).Skip(1)));
    }

    [Fact]
    public void MissingFoodMassDefinitionCannotMakeSharedCostAppearLow()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        Assert.Throws<InvalidOperationException>(() =>
            Evaluate(world, state,
                new EconomicMarketAllocationResult(new[]
                {
                    Trade(food: 7U),
                    Trade(destination: 3UL, food: 42U)
                }), Profiles(route)));
    }

    [Fact]
    public void ClosedNetworkDoesNotPermitStaleMarketTradeRoute()
    {
        var (world, state) = Fixture();
        var closed = new StrategicEdgeAccessSnapshot(
            world.StrategicTopology.Edges.Select(edge => edge.Id));
        Assert.Throws<InvalidOperationException>(() =>
            Evaluate(world, state,
                new EconomicMarketAllocationResult(new[] { Trade() }),
                Profiles(Route(world, state)), edgeAccess: closed));
    }

    [Fact]
    public void InvalidDuplicateAndUnknownEdgeProfilesAreRejected()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var profile = Profiles(route)[0];
        var market = new EconomicMarketAllocationResult(new[] { Trade() });

        Assert.Throws<ArgumentException>(() =>
            Evaluate(world, state, market, new[] { profile, profile }));
        Assert.Throws<ArgumentException>(() =>
            Evaluate(world, state, market,
                new FoodStrategicEdgeCongestionProfile[] { null! }));

        var outside = new StrategicEdgeId(
            world.StrategicTopology.Edges.Max(edge => edge.Id.Value) + 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            Evaluate(world, state, market,
                new[] { new FoodStrategicEdgeCongestionProfile(
                    outside, 10UL, 0UL, 1UL) }));
    }

    [Fact]
    public void UnboundAndMismatchedGeneratedWorldCannotBeEvaluated()
    {
        var (world, state) = Fixture();
        var market = new EconomicMarketAllocationResult(new[] { Trade() });
        var profiles = Profiles(Route(world, state));

        Assert.Throws<InvalidOperationException>(() =>
            Evaluate(world, WorldState.CreateInitial(), market, profiles));

        var other = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(9UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            Evaluate(other, state, market, profiles));
    }

    [Fact]
    public void SameCellTradeAndEmptyMarketRequireNoIntercellEdgeCost()
    {
        var (world, state) = Fixture();
        Assert.Equal(0, Route(world, state, 2UL, 3UL).HopCount);

        var local = Evaluate(world, state,
            new EconomicMarketAllocationResult(new[]
            {
                Trade(source: 2UL, destination: 3UL)
            }), Array.Empty<FoodStrategicEdgeCongestionProfile>());
        Assert.Empty(local.UsedEdges);

        var empty = Evaluate(world, state,
            new EconomicMarketAllocationResult(
                Array.Empty<EconomicTradeAllocation>()),
            Array.Empty<FoodStrategicEdgeCongestionProfile>());
        Assert.Empty(empty.UsedEdges);
    }

    [Fact]
    public void FractionalGramQuantitiesAreAccountedForExactly()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var result = Evaluate(world, state,
            new EconomicMarketAllocationResult(new[]
            {
                Trade(quantity: 1UL)
            }), Profiles(route));

        Assert.All(result.UsedEdges, row =>
            Assert.Equal(new BigInteger(1_000), row.LoadMicrograms));
    }

    [Fact]
    public void CongestionOutputDoesNotDependOnProfileEnumerationOrder()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var market = new EconomicMarketAllocationResult(new[]
        {
            Trade(destination: 3UL, food: 8U),
            Trade(food: 7U)
        });
        var profiles = Profiles(route);
        var hash = new CanonicalWorldStateHasher().Compute(state);
        var first = Evaluate(world, state, market, profiles);
        var second = Evaluate(world, state, market, profiles.Reverse());

        Assert.Equal(
            first.UsedEdges.Select(x =>
                (x.StrategicEdgeId, x.LoadMicrograms,
                    x.MarginalCostPerGram.Numerator,
                    x.MarginalCostPerGram.Denominator,
                    x.TotalCost.Numerator, x.TotalCost.Denominator)),
            second.UsedEdges.Select(x =>
                (x.StrategicEdgeId, x.LoadMicrograms,
                    x.MarginalCostPerGram.Numerator,
                    x.MarginalCostPerGram.Denominator,
                    x.TotalCost.Numerator, x.TotalCost.Denominator)));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoodStrategicEdgeCongestionEntry>)first.UsedEdges).Clear());
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
    }

    [Fact]
    public void InvalidCurveInputsAndNegativeMassFailFast()
    {
        Assert.Throws<ArgumentException>(() =>
            new FoodStrategicEdgeCongestionProfile(
                default, 10UL, 1UL, 2UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoodStrategicEdgeCongestionProfile(
                new StrategicEdgeId(1UL), 0UL, 1UL, 2UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoodStrategicEdgeCongestionProfile(
                new StrategicEdgeId(1UL), 10UL, 1UL, 0UL));

        var profile = new FoodStrategicEdgeCongestionProfile(
            new StrategicEdgeId(1UL), 10UL, 1UL, 2UL);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            profile.EvaluateMarginalCostPerGram(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            profile.EvaluateTotalCost(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoodStrategicEdgeCongestionEntry(profile, -1));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodStrategicEdgeCongestionEntry(null!, BigInteger.Zero));
    }

    [Fact]
    public void RationalCostComparisonAndReductionAreExact()
    {
        var low = new FoodTransportCostFraction(6, 4);
        var high = new FoodTransportCostFraction(10, 4);
        Assert.Equal(new BigInteger(3), low.Numerator);
        Assert.Equal(new BigInteger(2), low.Denominator);
        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.Equal(1, low.CompareTo(null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoodTransportCostFraction(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoodTransportCostFraction(1, 0));
    }

    [Fact]
    public void DuplicateAndNullResultsAreRejected()
    {
        var profile = new FoodStrategicEdgeCongestionProfile(
            new StrategicEdgeId(1UL), 10UL, 1UL, 1UL);
        var row = new FoodStrategicEdgeCongestionEntry(profile, 10);
        Assert.Throws<ArgumentException>(() =>
            new FoodStrategicEdgeCongestionResult(new[] { row, row }));
        Assert.Throws<ArgumentException>(() =>
            new FoodStrategicEdgeCongestionResult(
                new FoodStrategicEdgeCongestionEntry[] { null! }));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodStrategicEdgeCongestionResult(null!));
    }

    [Fact]
    public void AllInputsToEvaluatorAreRequired()
    {
        var (world, state) = Fixture();
        var evaluator = new FoodStrategicEdgeCongestionEvaluator();
        var market = new EconomicMarketAllocationResult(
            Array.Empty<EconomicTradeAllocation>());
        var profiles = Array.Empty<FoodStrategicEdgeCongestionProfile>();

        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(null!, state, StrategicEdgeAccessSnapshot.AllOpen,
                market, Catalog(), profiles));
        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(world, null!, StrategicEdgeAccessSnapshot.AllOpen,
                market, Catalog(), profiles));
        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(world, state, null!, market, Catalog(), profiles));
        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(world, state, StrategicEdgeAccessSnapshot.AllOpen,
                null!, Catalog(), profiles));
        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(world, state, StrategicEdgeAccessSnapshot.AllOpen,
                market, null!, profiles));
        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(world, state, StrategicEdgeAccessSnapshot.AllOpen,
                market, Catalog(), null!));
    }
}
