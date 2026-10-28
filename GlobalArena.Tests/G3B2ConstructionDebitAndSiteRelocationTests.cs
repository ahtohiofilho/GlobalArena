using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-B2 reference: exact local reserve debit, plus conservation of residents
/// and workers when opening a new economic site on controlled adjacent land.
/// </summary>
public sealed class G3B2ConstructionDebitAndSiteRelocationTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly CivilizationId Stranger = new(2UL);
    private readonly EconomicConstructionMaterialDebitCandidateResolver _debit = new();
    private readonly EconomicAdjacentSiteRelocationResolver _relocation = new();

    private static WorldState UnboundEconomicWorld() =>
        WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner, Stranger }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, Owner, new StrategicCellId(1UL), 0UL),
                new EconomicPointRuntimeState(B, Owner, new StrategicCellId(2UL), 0UL)
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 0UL),
                new PopulationPointRuntimeState(B, 0UL)
            }));

    private static EconomicMaterialReserveBook StoredMaterials() => new(
        new[]
        {
            new EconomicMaterialReserveBalance(A, Stone, 6UL * U),
            new EconomicMaterialReserveBalance(A, Wood, 4UL * U),
            new EconomicMaterialReserveBalance(B, Stone, 7UL * U)
        }, new[]
        {
            new EconomicMaterialReserveTurnCursor(Owner, 3UL)
        });

    [Fact]
    public void DebitConsumesExactlyLocalInputsAndPreservesOtherPointsAndCursors()
    {
        var world = UnboundEconomicWorld();
        var beforeHash = new CanonicalWorldStateHasher().Compute(world);
        var original = StoredMaterials();
        var result = _debit.Debit(world, Owner, original, new[]
        {
            new EconomicConstructionMaterialRequirement(A, Stone, 3UL * U),
            new EconomicConstructionMaterialRequirement(A, Wood, 2UL * U)
        });

        Assert.Equal(3UL * U, result.RawAvailable(A, Stone));
        Assert.Equal(2UL * U, result.RawAvailable(A, Wood));
        Assert.Equal(7UL * U, result.RawAvailable(B, Stone));
        Assert.Equal(3UL, result.LastAppliedTurn(Owner));
        Assert.Equal(6UL * U, original.RawAvailable(A, Stone));
        Assert.Equal(beforeHash, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void UnaffordableMixedDebitIsAllOrNothing()
    {
        var world = UnboundEconomicWorld();
        var original = StoredMaterials();

        Assert.Throws<InvalidOperationException>(() => _debit.Debit(
            world, Owner, original, new[]
            {
                new EconomicConstructionMaterialRequirement(A, Stone, U),
                new EconomicConstructionMaterialRequirement(A, Wood, 5UL * U)
            }));
        Assert.Equal(6UL * U, original.RawAvailable(A, Stone));
        Assert.Equal(4UL * U, original.RawAvailable(A, Wood));
    }

    [Fact]
    public void DebitRemovesZeroBalancesAndCannotBorrowFromAnotherPoint()
    {
        var world = UnboundEconomicWorld();
        var book = _debit.Debit(world, Owner, StoredMaterials(), new[]
        {
            new EconomicConstructionMaterialRequirement(A, Stone, 6UL * U)
        });
        Assert.Equal(0UL, book.RawAvailable(A, Stone));
        Assert.DoesNotContain(book.Balances,
            row => row.EconomicPointId == A && row.CommodityId == Stone);
        Assert.Equal(7UL * U, book.RawAvailable(B, Stone));
        Assert.Throws<InvalidOperationException>(() => _debit.Debit(
            world, Owner, book, new[]
            {
                new EconomicConstructionMaterialRequirement(A, Stone, U)
            }));
    }

    [Fact]
    public void DebitOrderIsCanonicalAndInvalidRequirementsFailClosed()
    {
        var world = UnboundEconomicWorld();
        var first = new EconomicConstructionMaterialRequirement(A, Wood, U);
        var second = new EconomicConstructionMaterialRequirement(A, Stone, U);
        var original = StoredMaterials();
        var one = _debit.Debit(world, Owner, original, new[] { first, second });
        var two = _debit.Debit(world, Owner, original, new[] { second, first });

        Assert.Equal(one.Balances.Select(x => (x.EconomicPointId, x.CommodityId, x.RawQuantity)),
            two.Balances.Select(x => (x.EconomicPointId, x.CommodityId, x.RawQuantity)));
        Assert.Throws<ArgumentException>(() => _debit.Debit(
            world, Owner, original, Array.Empty<EconomicConstructionMaterialRequirement>()));
        Assert.Throws<ArgumentException>(() => _debit.Debit(
            world, Owner, original, new[] { first, first }));
    }

    // We select a real adjacent pair of land cells generated from a fixed seed,
    // rather than claiming arbitrary numeric IDs are adjacent or traversable.
    private static (WorldGenerationResult Generated, WorldState State,
        StrategicCellId SourceCell, StrategicCellId DestinationCell) SiteFixture()
    {
        var generated = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));

        StrategicCellId source = default, dest = default;
        foreach (var cell in generated.StrategicTopology.Cells)
        {
            if (generated.StrategicPhysicalFields.LandWater.GetKind(cell.Id)
                != StrategicLandWaterKind.Land)
                continue;
            foreach (var neighbor in cell.AdjacentCellIds)
            {
                if (generated.StrategicPhysicalFields.LandWater.GetKind(neighbor)
                    != StrategicLandWaterKind.Land)
                    continue;
                source = cell.Id;
                dest = neighbor;
                break;
            }
            if (source.IsValid)
                break;
        }
        if (!source.IsValid)
            throw new InvalidOperationException("G3-B2 fixture needs two adjacent land cells.");

        var state = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner, Stranger }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[]
            {
                new StrategicTerritoryControlEntry(source, Owner),
                new StrategicTerritoryControlEntry(dest, Owner)
            }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, Owner, source, 6UL, new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture, 3UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Mining, 1UL)
                })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 10UL)
            }));

        return (generated, state, source, dest);
    }

    private static EconomicAdjacentSiteRelocationOrder BasicMove(
        StrategicCellId destination) => new(
            A, B, destination, 4UL, 3UL,
            new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 2UL),
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Mining, 1UL)
            });

    [Fact]
    public void AdjacentSiteReallocationConservesResidentsAndWorkersAndChangesWorldHash()
    {
        var (generated, world, source, dest) = SiteFixture();
        var oldHash = new CanonicalWorldStateHasher().Compute(world);
        var next = _relocation.Apply(generated, world, Owner, BasicMove(dest));
        var sourceAfter = next.Economy.EconomicPoints.Single(p => p.Id == A);
        var destination = next.Economy.EconomicPoints.Single(p => p.Id == B);

        Assert.Equal(source, sourceAfter.StrategicCellId);
        Assert.Equal(dest, destination.StrategicCellId);
        Assert.Equal(6UL, next.Population.PointPopulations.Single(x => x.EconomicPointId == A).Residents);
        Assert.Equal(4UL, next.Population.PointPopulations.Single(x => x.EconomicPointId == B).Residents);
        Assert.Equal(3UL, sourceAfter.Workforce);
        Assert.Equal(3UL, destination.Workforce);
        Assert.Single(sourceAfter.ActivityAllocations);
        Assert.Equal(EconomicActivityKind.Agriculture, sourceAfter.ActivityAllocations[0].Kind);
        Assert.Equal(1UL, sourceAfter.ActivityAllocations[0].Workforce);
        Assert.Equal(2, destination.ActivityAllocations.Count);
        Assert.Equal(10UL, next.Population.PointPopulations.Aggregate(0UL, (s, p) => s + p.Residents));
        Assert.Equal(6UL, next.Economy.EconomicPoints.Aggregate(0UL, (s, p) => s + p.Workforce));
        Assert.Equal(oldHash, new CanonicalWorldStateHasher().Compute(world));
        Assert.NotEqual(oldHash, new CanonicalWorldStateHasher().Compute(next));
        Assert.Same(world.Territory, next.Territory);
    }

    [Fact]
    public void RelocationDoesNotClaimUnownedOrForeignStrategicTerritory()
    {
        var (generated, world, _, dest) = SiteFixture();
        var unowned = world.WithTerritory(new StrategicTerritoryRuntimeState(new[]
        {
            new StrategicTerritoryControlEntry(world.Economy.EconomicPoints.Single().StrategicCellId, Owner)
        }));
        Assert.Throws<InvalidOperationException>(() =>
            _relocation.Apply(generated, unowned, Owner, BasicMove(dest)));
        var foreign = world.WithTerritory(new StrategicTerritoryRuntimeState(new[]
        {
            new StrategicTerritoryControlEntry(world.Economy.EconomicPoints.Single().StrategicCellId, Owner),
            new StrategicTerritoryControlEntry(dest, Stranger)
        }));
        Assert.Throws<InvalidOperationException>(() =>
            _relocation.Apply(generated, foreign, Owner, BasicMove(dest)));
    }

    [Fact]
    public void RelocationRejectsInsufficientPeopleAndMismatchedActivity()
    {
        var (generated, world, _, dest) = SiteFixture();
        var tooMany = new EconomicAdjacentSiteRelocationOrder(
            A, B, dest, 11UL, 3UL, Array.Empty<EconomicActivityWorkforceAllocation>());
        Assert.Throws<InvalidOperationException>(() =>
            _relocation.Apply(generated, world, Owner, tooMany));

        var wrongActivity = new EconomicAdjacentSiteRelocationOrder(
            A, B, dest, 4UL, 3UL, new[]
            {
                new EconomicActivityWorkforceAllocation(EconomicActivityKind.Mining, 2UL)
            });
        Assert.Throws<InvalidOperationException>(() =>
            _relocation.Apply(generated, world, Owner, wrongActivity));

        var tooManyUnallocated = new EconomicAdjacentSiteRelocationOrder(
            A, B, dest, 4UL, 4UL, new[]
            {
                new EconomicActivityWorkforceAllocation(EconomicActivityKind.Agriculture, 1UL)
            });
        Assert.Throws<InvalidOperationException>(() =>
            _relocation.Apply(generated, world, Owner, tooManyUnallocated));
    }

    [Fact]
    public void RelocationRejectsDuplicatePointAndMismatchedWorldBinding()
    {
        var (generated, world, _, dest) = SiteFixture();
        Assert.Throws<ArgumentException>(() =>
            new EconomicAdjacentSiteRelocationOrder(
                A, A, dest, 4UL, 2UL,
                Array.Empty<EconomicActivityWorkforceAllocation>()));

        var differentWorld = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(1UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            _relocation.Apply(differentWorld, world, Owner, BasicMove(dest)));
    }
}
