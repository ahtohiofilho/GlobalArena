using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-B3: candidate founding in unowned adjacent strategic land.
/// No authoritative reserve persistence or fine-tile colonization is implied.
/// </summary>
public sealed class G3B3NeolithicSiteFoundingTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly CivilizationId Other = new(2UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CommodityId Stone = new(2U);
    private readonly EconomicNeolithicSiteFoundingResolver _founding = new();
    private readonly CanonicalWorldStateHasher _hasher = new();

    private static (WorldGenerationResult Generated, WorldState State,
        StrategicCellId Source, StrategicCellId Destination) Fixture()
    {
        var generated = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));

        StrategicCellId source = default, destination = default;
        foreach (var cell in generated.StrategicTopology.Cells)
        {
            if (generated.StrategicPhysicalFields.LandWater.GetKind(cell.Id) !=
                StrategicLandWaterKind.Land)
                continue;
            foreach (var adjacent in cell.AdjacentCellIds)
            {
                if (generated.StrategicPhysicalFields.LandWater.GetKind(adjacent) !=
                    StrategicLandWaterKind.Land)
                    continue;
                source = cell.Id;
                destination = adjacent;
                break;
            }
            if (source.IsValid)
                break;
        }
        if (!source.IsValid)
            throw new InvalidOperationException("G3-B3 requires a generated pair of adjacent land cells.");

        var world = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner, Other }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[]
            {
                new StrategicTerritoryControlEntry(source, Owner)
            }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, Owner, source, 6UL, new[]
                {
                    new EconomicActivityWorkforceAllocation(EconomicActivityKind.Agriculture, 3UL),
                    new EconomicActivityWorkforceAllocation(EconomicActivityKind.Mining, 1UL)
                })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 10UL)
            }));
        return (generated, world, source, destination);
    }

    private static EconomicMaterialReserveBook Materials(
        ulong wood = 5UL * U, ulong stone = 4UL * U) => new(
        new[]
        {
            new EconomicMaterialReserveBalance(A, Wood, wood),
            new EconomicMaterialReserveBalance(A, Stone, stone)
        }, new[] { new EconomicMaterialReserveTurnCursor(Owner, 3UL) });

    private static EconomicAdjacentSiteRelocationOrder Moving(
        StrategicCellId target) => new(
        A, B, target, 4UL, 3UL,
        new[]
        {
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Agriculture, 2UL),
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Mining, 1UL)
        });

    private static EconomicNeolithicFoundationRecipe Recipe() =>
        new(1UL, Wood, Stone, 2UL * U, 3UL * U);

    [Fact]
    public void FoundingProposesTerritoryEconomyAndPopulationWithExactlyFundedMaterials()
    {
        var (generated, original, source, destination) = Fixture();
        var reserves = Materials();
        var before = _hasher.Compute(original);

        var proposed = _founding.Propose(generated, original, Owner, before,
            reserves, Moving(destination), Recipe());
        var next = proposed.ProposedWorld;

        Assert.True(next.Territory.TryGetController(destination, out var controller));
        Assert.Equal(Owner, controller);
        Assert.True(next.Territory.TryGetController(source, out controller));
        Assert.Equal(Owner, controller);
        Assert.False(original.Territory.TryGetController(destination, out _));

        Assert.Equal(2, next.Economy.EconomicPoints.Count);
        Assert.Equal(10UL, next.Population.PointPopulations.Aggregate(
            0UL, (sum, resident) => sum + resident.Residents));
        Assert.Equal(6UL, next.Economy.EconomicPoints.Aggregate(
            0UL, (sum, point) => sum + point.Workforce));

        Assert.Equal(6UL, Assert.Single(next.Population.PointPopulations,
            p => p.EconomicPointId == A).Residents);
        Assert.Equal(4UL, Assert.Single(next.Population.PointPopulations,
            p => p.EconomicPointId == B).Residents);
        Assert.Equal(3UL, Assert.Single(next.Economy.EconomicPoints,
            p => p.Id == A).Workforce);
        Assert.Equal(3UL, Assert.Single(next.Economy.EconomicPoints,
            p => p.Id == B).Workforce);

        Assert.Equal(3UL * U, proposed.ProposedReserves.RawAvailable(A, Wood));
        Assert.Equal(U, proposed.ProposedReserves.RawAvailable(A, Stone));
        Assert.Equal(3UL, proposed.ProposedReserves.LastAppliedTurn(Owner));
        Assert.Equal(5UL * U, reserves.RawAvailable(A, Wood));
        Assert.Equal(4UL * U, reserves.RawAvailable(A, Stone));

        Assert.Equal(before, proposed.PriorWorldHash);
        Assert.NotEqual(before, proposed.ProposedWorldHash);
        Assert.Equal(proposed.ProposedWorldHash, _hasher.Compute(next));
        Assert.Equal(before, _hasher.Compute(original));
        Assert.Same(original.Civilizations, next.Civilizations);
        Assert.Same(original.Diplomacy, next.Diplomacy);
        Assert.Same(original.Warfare, next.Warfare);
    }

    [Fact]
    public void IncompleteWoodOrStoneCannotFoundAndInputsStayUntouched()
    {
        var (generated, world, _, target) = Fixture();
        var hash = _hasher.Compute(world);
        var reserves = Materials(wood: U, stone: 4UL * U);

        Assert.Throws<InvalidOperationException>(() =>
            _founding.Propose(generated, world, Owner, hash,
                reserves, Moving(target), Recipe()));
        Assert.Equal(hash, _hasher.Compute(world));
        Assert.False(world.Territory.TryGetController(target, out _));
        Assert.Equal(U, reserves.RawAvailable(A, Wood));
        Assert.Equal(4UL * U, reserves.RawAvailable(A, Stone));

        var insufficientStone = Materials(wood: 5UL * U, stone: U);
        Assert.Throws<InvalidOperationException>(() =>
            _founding.Propose(generated, world, Owner, hash,
                insufficientStone, Moving(target), Recipe()));
    }

    [Fact]
    public void ExistingForeignTerritoryAndAnotherPointCannotBeClaimed()
    {
        var (generated, world, _, target) = Fixture();
        var foreign = world.WithTerritory(new StrategicTerritoryRuntimeState(
            world.Territory.Controls.Append(
                new StrategicTerritoryControlEntry(target, Other))));
        Assert.Throws<InvalidOperationException>(() =>
            _founding.Propose(generated, foreign, Owner, _hasher.Compute(foreign),
                Materials(), Moving(target), Recipe()));

        var alreadyOccupied = world.WithEconomyAndPopulation(
            new EconomyRuntimeState(world.Economy.EconomicPoints.Append(
                new EconomicPointRuntimeState(new EconomicPointId(3UL),
                    Other, target, 0UL))),
            new PopulationRuntimeState(world.Population.PointPopulations.Append(
                new PopulationPointRuntimeState(new EconomicPointId(3UL), 0UL))));
        Assert.Throws<InvalidOperationException>(() =>
            _founding.Propose(generated, alreadyOccupied, Owner,
                _hasher.Compute(alreadyOccupied), Materials(),
                Moving(target), Recipe()));
    }

    [Fact]
    public void StaleHashAndWrongWorldSignatureFailBeforeFounding()
    {
        var (generated, world, _, target) = Fixture();
        var modified = world.AdvanceRevision();
        Assert.Throws<InvalidOperationException>(() =>
            _founding.Propose(generated, modified, Owner,
                _hasher.Compute(world), Materials(), Moving(target), Recipe()));

        var otherGenerated = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(1UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            _founding.Propose(otherGenerated, world, Owner, _hasher.Compute(world),
                Materials(), Moving(target), Recipe()));
    }

    [Fact]
    public void FoundingCannotSpendMaterialStoredOnlyAtAnotherPoint()
    {
        var (generated, world, _, target) = Fixture();
        var elsewhere = new EconomicMaterialReserveBook(new[]
        {
            new EconomicMaterialReserveBalance(new EconomicPointId(4UL), Wood, 20UL * U),
            new EconomicMaterialReserveBalance(new EconomicPointId(4UL), Stone, 20UL * U)
        }, Array.Empty<EconomicMaterialReserveTurnCursor>());

        Assert.Throws<InvalidOperationException>(() =>
            _founding.Propose(generated, world, Owner,
                _hasher.Compute(world), elsewhere, Moving(target), Recipe()));
    }

    [Fact]
    public void FoundingRecipeRequiresTwoDistinctNonzeroMaterials()
    {
        Assert.Throws<ArgumentException>(() =>
            new EconomicNeolithicFoundationRecipe(1UL, Wood, Wood, U, U));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicNeolithicFoundationRecipe(1UL, Wood, Stone, 0UL, U));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicNeolithicFoundationRecipe(1UL, Wood, Stone, U, 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicNeolithicFoundationRecipe(0UL, Wood, Stone, U, U));
    }

    [Fact]
    public void CannotFoundIfMovingWorkersOrResidentsWouldBeCreated()
    {
        var (generated, world, _, target) = Fixture();
        var invalidMove = new EconomicAdjacentSiteRelocationOrder(
            A, B, target, 11UL, 3UL,
            Array.Empty<EconomicActivityWorkforceAllocation>());
        Assert.Throws<InvalidOperationException>(() =>
            _founding.Propose(generated, world, Owner,
                _hasher.Compute(world), Materials(), invalidMove, Recipe()));
        Assert.Single(world.Economy.EconomicPoints);
        Assert.Single(world.Population.PointPopulations);
    }
}
