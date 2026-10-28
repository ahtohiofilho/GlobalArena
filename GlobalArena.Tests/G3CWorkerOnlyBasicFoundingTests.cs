using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-C worker-only primitive settlement. Workers are both producers and
/// consumers; the legacy v8 census mirrors workers only for compatibility.
/// No material requirement is imposed on basic occupation.
/// </summary>
public sealed class G3CWorkerOnlyBasicFoundingTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CivilizationId Owner = new(1UL);
    private readonly EconomicNeolithicSiteFoundingResolver _founding = new();
    private readonly CanonicalWorldStateHasher _hash = new();

    private static (WorldGenerationResult Generated, WorldState State,
        StrategicCellId Destination) Fixture(ulong legacyResidents = 8UL)
    {
        var generated = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(0UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));
        StrategicCellId source = default;
        StrategicCellId destination = default;
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
            throw new InvalidOperationException("Need generated adjacent land cells.");

        var world = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[]
            {
                new StrategicTerritoryControlEntry(source, Owner)
            }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, Owner, source, 8UL, new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture, 3UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Forestry, 3UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Mining, 2UL)
                })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, legacyResidents)
            }));
        return (generated, world, destination);
    }

    private static EconomicPrimitiveWorkerMoveOrder Move(StrategicCellId target) =>
        new(A, B, target, 3UL, new[]
        {
            new EconomicActivityWorkforceAllocation(
                EconomicActivityKind.Agriculture, 1UL),
            new EconomicActivityWorkforceAllocation(
                EconomicActivityKind.Forestry, 2UL)
        });

    [Fact]
    public void BasicFoundingUsesNoMaterialsAndConservesWorkersAndDemand()
    {
        var (generated, world, destination) = Fixture();
        var before = _hash.Compute(world);
        var stock = new EconomicMaterialReserveBook(new[]
        {
            new EconomicMaterialReserveBalance(A, Wood, 2UL * U)
        }, Array.Empty<EconomicMaterialReserveTurnCursor>());

        var founded = _founding.ProposeBasic(
            generated, world, Owner, before, stock, Move(destination));
        var next = founded.ProposedWorld;

        Assert.Same(stock, founded.ProposedReserves);
        Assert.Equal(2UL * U, founded.ProposedReserves.RawAvailable(A, Wood));
        Assert.True(next.Territory.TryGetController(destination, out var controller));
        Assert.Equal(Owner, controller);
        Assert.False(world.Territory.TryGetController(destination, out _));

        Assert.Equal(5UL, Assert.Single(next.Economy.EconomicPoints, x => x.Id == A).Workforce);
        Assert.Equal(3UL, Assert.Single(next.Economy.EconomicPoints, x => x.Id == B).Workforce);
        Assert.Equal(5UL, Assert.Single(next.Population.PointPopulations,
            x => x.EconomicPointId == A).Residents);
        Assert.Equal(3UL, Assert.Single(next.Population.PointPopulations,
            x => x.EconomicPointId == B).Residents);
        Assert.Equal(8UL, next.Economy.EconomicPoints.Aggregate(
            0UL, (sum, point) => sum + point.Workforce));
        Assert.Equal(before, _hash.Compute(world));
        Assert.NotEqual(before, _hash.Compute(next));

        // All workers continue to cause demand independent of activity.
        var demandOnly = new EconomicPrimitiveWorkerEconomyResolver().Resolve(
            generated, next, Array.Empty<EconomicProductionPotential>(),
            new[]
            {
                new EconomicDemandProfile(A, Food, 500_000L),
                new EconomicDemandProfile(B, Food, 500_000L)
            });
        Assert.Equal(2_500_000UL, Assert.Single(demandOnly.DemandFlows,
            x => x.EconomicPointId == A).RawQuantity);
        Assert.Equal(1_500_000UL, Assert.Single(demandOnly.DemandFlows,
            x => x.EconomicPointId == B).RawQuantity);
    }

    [Fact]
    public void FoundingWithEmptyReserveSucceedsWithoutImplicitStoneOrWood()
    {
        var (generated, world, destination) = Fixture();
        var proposal = _founding.ProposeBasic(
            generated, world, Owner, _hash.Compute(world),
            EconomicMaterialReserveBook.Empty, Move(destination));

        Assert.Same(EconomicMaterialReserveBook.Empty, proposal.ProposedReserves);
        Assert.True(proposal.ProposedWorld.Territory.TryGetController(
            destination, out var owner));
        Assert.Equal(Owner, owner);
    }

    [Fact]
    public void BasicFoundingRejectsNonMirroredLegacyPopulation()
    {
        var (generated, world, destination) = Fixture(legacyResidents: 10UL);
        Assert.Throws<InvalidOperationException>(() =>
            _founding.ProposeBasic(generated, world, Owner, _hash.Compute(world),
                EconomicMaterialReserveBook.Empty, Move(destination)));
    }

    [Fact]
    public void BasicFoundingRejectsInsufficientWorkersAndForeignTerritory()
    {
        var (generated, world, destination) = Fixture();
        var excessive = new EconomicPrimitiveWorkerMoveOrder(
            A, B, destination, 9UL,
            Array.Empty<EconomicActivityWorkforceAllocation>());

        Assert.Throws<InvalidOperationException>(() =>
            _founding.ProposeBasic(generated, world, Owner, _hash.Compute(world),
                EconomicMaterialReserveBook.Empty, excessive));

        var alreadyOwned = world.WithTerritory(
            new StrategicTerritoryRuntimeState(world.Territory.Controls.Append(
                new StrategicTerritoryControlEntry(destination, Owner))));
        Assert.Throws<InvalidOperationException>(() =>
            _founding.ProposeBasic(generated, alreadyOwned, Owner,
                _hash.Compute(alreadyOwned),
                EconomicMaterialReserveBook.Empty, Move(destination)));
    }

    [Fact]
    public void BasicFoundingRejectsStaleWorldAndInvalidMoveAllocations()
    {
        var (generated, world, destination) = Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            _founding.ProposeBasic(generated, world.AdvanceRevision(), Owner,
                _hash.Compute(world), EconomicMaterialReserveBook.Empty,
                Move(destination)));

        var wrongAllocation = new EconomicPrimitiveWorkerMoveOrder(
            A, B, destination, 3UL,
            new[] { new EconomicActivityWorkforceAllocation(
                EconomicActivityKind.Mining, 3UL) });
        Assert.Throws<InvalidOperationException>(() =>
            _founding.ProposeBasic(generated, world, Owner, _hash.Compute(world),
                EconomicMaterialReserveBook.Empty, wrongAllocation));
    }
}
