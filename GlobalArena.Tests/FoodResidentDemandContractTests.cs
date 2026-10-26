using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class FoodResidentDemandContractTests
{
    private static FoodCommodityCatalog Catalog() =>
        new(new[]
        {
            new FoodCommodityDefinition(new CommodityId(9U), "legumes"),
            new FoodCommodityDefinition(new CommodityId(7U), "grain")
        });

    private static WorldState State(
        ulong firstResidents = 2UL,
        ulong secondResidents = 10UL) =>
        WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(
                new[] { new CivilizationId(1UL), new CivilizationId(2UL) }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL),
                        2UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 2UL)
                        }),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL),
                        new CivilizationId(2UL),
                        new StrategicCellId(2UL), 0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(
                        new EconomicPointId(1UL), firstResidents),
                    new PopulationPointRuntimeState(
                        new EconomicPointId(2UL), secondResidents)
                }));

    private static FoodResidentConsumptionRequirement Need(
        ulong point = 2UL, uint food = 7U, long rate = 500_000L) =>
        new(new EconomicPointId(point), new CommodityId(food), rate);

    [Fact]
    public void FoodDefinitionsRequireExplicitStableIdsAndCanonicalOrder()
    {
        var list = new List<FoodCommodityDefinition>
        {
            new(new CommodityId(9U), "legumes"),
            new(new CommodityId(7U), "grain")
        };
        var catalog = new FoodCommodityCatalog(list);
        list.Clear();

        Assert.Equal(new[] { 7U, 9U },
            catalog.Commodities.Select(x => x.CommodityId.Value));
        Assert.Equal("grain", catalog.Commodities[0].StableKey);
        Assert.True(catalog.Contains(new CommodityId(9U)));
        Assert.False(catalog.Contains(new CommodityId(100U)));
        Assert.False(catalog.Contains(default));
        Assert.Equal(1_000_000UL, FoodCommodityDefinition.RawQuantityPerKilogram);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoodCommodityDefinition>)catalog.Commodities)
                .Add(new FoodCommodityDefinition(new CommodityId(11U), "fruit")));
    }

    [Fact]
    public void FoodIdentitiesDoNotRequireAnyReservedMagicCommodityId()
    {
        var catalog = new FoodCommodityCatalog(new[]
        {
            new FoodCommodityDefinition(new CommodityId(4294967295U), "roots")
        });
        Assert.True(catalog.Contains(new CommodityId(uint.MaxValue)));
        Assert.False(catalog.Contains(new CommodityId(1U)));
    }

    [Fact]
    public void DuplicateFoodIdOrStableKeyFailsClosed()
    {
        Assert.Throws<ArgumentException>(() =>
            new FoodCommodityCatalog(new[]
            {
                new FoodCommodityDefinition(new CommodityId(7U), "grain"),
                new FoodCommodityDefinition(new CommodityId(7U), "rice")
            }));
        Assert.Throws<ArgumentException>(() =>
            new FoodCommodityCatalog(new[]
            {
                new FoodCommodityDefinition(new CommodityId(7U), "grain"),
                new FoodCommodityDefinition(new CommodityId(9U), "grain")
            }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Grain")]
    [InlineData("grão")]
    [InlineData("grain bread")]
    public void StableKeyRejectsNoncanonicalSpellings(string key)
    {
        Assert.Throws<ArgumentException>(() =>
            new FoodCommodityDefinition(new CommodityId(7U), key));
    }

    [Fact]
    public void InvalidIdsAndNullCollectionsFailFast()
    {
        Assert.Throws<ArgumentException>(() =>
            new FoodCommodityDefinition(default, "grain"));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodCommodityDefinition(new CommodityId(1U), null!));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodCommodityCatalog(null!));
        Assert.Throws<ArgumentException>(() =>
            new FoodCommodityCatalog(new FoodCommodityDefinition[] { null! }));
        Assert.Throws<ArgumentException>(() =>
            new FoodResidentConsumptionRequirement(default,
                new CommodityId(7U), 500_000L));
        Assert.Throws<ArgumentException>(() =>
            new FoodResidentConsumptionRequirement(new EconomicPointId(1UL),
                default, 500_000L));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1_000_001L)]
    public void FoodRequirementsRespectExistingNormalizedRateContract(long rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Need(rate: rate));
    }

    [Fact]
    public void DifferentFoodCommoditiesRetainIndependentPointAndMassRates()
    {
        var source = new List<FoodResidentConsumptionRequirement>
        {
            Need(point: 2UL, food: 9U, rate: 100_000L),
            Need(point: 1UL, food: 7U, rate: 750_000L),
            Need(point: 2UL, food: 7U, rate: 500_000L)
        };
        var rows = FoodResidentDemandProfileMaterializer.Materialize(
            State(), Catalog(), source);
        source.Clear();

        Assert.Equal(new[] { (1UL, 7U, 750_000L),
                             (2UL, 7U, 500_000L),
                             (2UL, 9U, 100_000L) },
            rows.Select(row => (
                row.EconomicPointId.Value,
                row.CommodityId.Value,
                row.RawDemandRatePerResident)));

        var demands = new EconomicResidentDemandResolver().Resolve(State(), rows);
        Assert.Equal(new[] { 1_500_000UL, 5_000_000UL, 1_000_000UL },
            demands.Select(flow => flow.RawQuantity));

        Assert.Equal(5UL, demands[1].RawQuantity /
            FoodCommodityDefinition.RawQuantityPerKilogram);
    }

    [Fact]
    public void AllDemandRatesComeFromExplicitInputsNotTheCatalog()
    {
        var empty = FoodResidentDemandProfileMaterializer.Materialize(
            State(), Catalog(), Array.Empty<FoodResidentConsumptionRequirement>());
        Assert.Empty(empty);

        var catalog = Catalog();
        Assert.Empty(new FoodResidentDemandResolver().Resolve(State(), empty));
        Assert.Equal(2, catalog.Commodities.Count);
    }

    [Fact]
    public void MissingCensusCannotBecomeZeroDemandByDefault()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FoodResidentDemandProfileMaterializer.Materialize(
                WorldState.CreateInitial(),
                Catalog(),
                Array.Empty<FoodResidentConsumptionRequirement>()));
    }

    [Fact]
    public void UnregisteredFoodOrUnknownEconomicPointFails()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FoodResidentDemandProfileMaterializer.Materialize(
                State(), Catalog(), new[] { Need(food: 15U) }));
        Assert.Throws<InvalidOperationException>(() =>
            FoodResidentDemandProfileMaterializer.Materialize(
                State(), Catalog(), new[] { Need(point: 999UL) }));
    }

    [Fact]
    public void DuplicateNeedDoesNotDoubleCountDemand()
    {
        Assert.Throws<ArgumentException>(() =>
            FoodResidentDemandProfileMaterializer.Materialize(
                State(), Catalog(), new[] { Need(), Need() }));
    }

    [Fact]
    public void InputOrderDoesNotChangeCanonicalFoodDemand()
    {
        var requirements = new[]
        {
            Need(point: 2UL, food: 9U, rate: 100_000L),
            Need(point: 2UL, food: 7U, rate: 500_000L),
            Need(point: 1UL, food: 7U, rate: 300_000L)
        };
        var original = FoodResidentDemandProfileMaterializer.Materialize(
            State(), Catalog(), requirements);
        var reverse = FoodResidentDemandProfileMaterializer.Materialize(
            State(), Catalog(), requirements.Reverse());
        Assert.Equal(original.Select(p =>
            (p.EconomicPointId, p.CommodityId, p.RawDemandRatePerResident)),
            reverse.Select(p =>
            (p.EconomicPointId, p.CommodityId, p.RawDemandRatePerResident)));
    }

    [Fact]
    public void ResidentHeadcountChangesDemandWithoutChangingPhysicalUnit()
    {
        var profile = FoodResidentDemandProfileMaterializer.Materialize(
            State(), Catalog(), new[] { Need() });
        var resolver = new EconomicResidentDemandResolver();
        var small = resolver.Resolve(State(secondResidents: 4UL), profile);
        var larger = resolver.Resolve(State(secondResidents: 10UL), profile);

        Assert.Equal(2_000_000UL, small.Single().RawQuantity);
        Assert.Equal(5_000_000UL, larger.Single().RawQuantity);
        Assert.Equal(500_000L, profile.Single().RawDemandRatePerResident);
    }

    [Fact]
    public void ExplicitFoodProfilesFeedTheExistingResidentMarketTurnUnchanged()
    {
        var state = State();
        var foodProfiles = FoodResidentDemandProfileMaterializer.Materialize(
            state, Catalog(), new[] { Need() });
        var phase = new EconomicResidentMarketTurnPhase(
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 1_000_000L)
            },
            foodProfiles,
            new[]
            {
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL), new EconomicPointId(2UL)),
                    1, 100_000UL, 1_000_000UL, 0UL)
            },
            new EconomicMarketAllocationPolicy(
                900_000UL, 900_000UL, 1, 1));

        var hash = new CanonicalWorldStateHasher().Compute(state);
        var resolution = phase.Resolve(
            new TurnResolutionInput(
                state, Array.Empty<ISimulationCommand>(),
                new SimulationContext(new TurnNumber(1UL),
                    new SimulationSeed(1UL))), state);

        Assert.Equal(2, resolution.Events.Count);
        var flow = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            resolution.Events[0]).Flows;
        var market = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            resolution.Events[1]).Allocation;

        Assert.Equal(2_000_000UL, flow.SupplyFlows.Single().RawQuantity);
        Assert.Equal(5_000_000UL, flow.DemandFlows.Single().RawQuantity);
        Assert.Equal(1_000_000UL, market.TotalRawAllocatedQuantity);
        Assert.Equal(new CommodityId(7U), market.Allocations.Single().CommodityId);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
    }

    [Fact]
    public void BadMaterializerInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FoodResidentDemandProfileMaterializer.Materialize(
                null!, Catalog(), Array.Empty<FoodResidentConsumptionRequirement>()));
        Assert.Throws<ArgumentNullException>(() =>
            FoodResidentDemandProfileMaterializer.Materialize(
                State(), null!, Array.Empty<FoodResidentConsumptionRequirement>()));
        Assert.Throws<ArgumentNullException>(() =>
            FoodResidentDemandProfileMaterializer.Materialize(
                State(), Catalog(), null!));
        Assert.Throws<ArgumentException>(() =>
            FoodResidentDemandProfileMaterializer.Materialize(
                State(), Catalog(),
                new FoodResidentConsumptionRequirement[] { null! }));
    }
}
