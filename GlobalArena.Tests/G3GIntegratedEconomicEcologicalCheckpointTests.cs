using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-G checkpoint integrity and ecological/material balance projection.
/// The separate digest protects snapshot comparisons; no save/replay importer
/// or authoritative WorldState v10 is claimed by these tests.
/// </summary>
public sealed class G3GIntegratedEconomicEcologicalCheckpointTests
{
    private const ulong D = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId First = new(1UL);
    private static readonly CivilizationId Second = new(2UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping = new(Food, Wood, Stone);
    private static readonly EconomicEcologicalTurnCommitResolver Coordinator = new();

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static StrategicCellId ForestedCell(WorldGenerationResult generated)
    {
        var fields = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        return generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            fields.WoodyVegetation.GetRawValue(id) > 0L &&
            fields.ExposedStone.GetRawValue(id) > 0L);
    }

    private static WorldState World(WorldGenerationResult generated,
        StrategicCellId cell, bool twoCivilizations = false)
    {
        var points = new List<EconomicPointRuntimeState>
        {
            new(A, First, cell, 4UL, new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Forestry, 4UL)
            })
        };
        var census = new List<PopulationPointRuntimeState>
        {
            new(A, 4UL)
        };
        if (twoCivilizations)
        {
            points.Add(new EconomicPointRuntimeState(B, Second, cell, 4UL,
                new[] { new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Forestry, 4UL) }));
            census.Add(new PopulationPointRuntimeState(B, 4UL));
        }

        return WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(
                twoCivilizations ? new[] { First, Second } : new[] { First }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(points),
                new PopulationRuntimeState(census));
    }

    private static EconomicNaturalCapitalPolicy Policy(
        ulong woodScale = 100UL * D,
        long woodRecovery = 0L) => new(
            woodScale, 100UL * D, woodRecovery, 0L, 0L);

    private static EconomicEcologicalCheckpoint Initial(
        WorldGenerationResult generated, WorldState world,
        EconomicNaturalCapitalPolicy? policy = null,
        EconomicMaterialReserveBook? reserves = null) =>
        new(generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, policy ?? Policy()),
            reserves ?? EconomicMaterialReserveBook.Empty,
            policy ?? Policy(), Mapping);

    private static EconomicMaterialOriginHoldOrder Hold(EconomicPointId point) =>
        new(point, Wood, EconomicActivityKind.Forestry, D / 100UL);

    private static EconomicEcologicalTurnCommitResult Turn(
        WorldGenerationResult generated, EconomicEcologicalCheckpoint previous,
        ulong ordinal, params EconomicMaterialOriginHoldOrder[] holds) =>
        Coordinator.Propose(generated, previous, previous.Sha256Hex, ordinal,
            new[] { new EconomicDemandProfile(A, Food, 500_000L) }, holds);

    [Fact]
    public void RealizedForestHarvestIsCreditedOnceAndReducedFromOfferedOutput()
    {
        var generated = Generate();
        var cell = ForestedCell(generated);
        var world = World(generated, cell);
        var original = Initial(generated, world);
        var originalWorldHash = original.WorldHash;

        var proposed = Turn(generated, original, 1UL, Hold(A));
        var next = proposed.ProposedNext;
        var actualWood = Assert.Single(proposed.RealizedProductionAndDemand.SupplyFlows);
        var offeredWood = Assert.Single(proposed.MarketOfferAndDemand.SupplyFlows);

        Assert.Equal(actualWood.RawQuantity - D / 100UL, offeredWood.RawQuantity);
        Assert.Equal(D / 100UL, next.MaterialReserves.RawAvailable(A, Wood));
        Assert.Equal(1UL, next.MaterialReserves.LastAppliedTurn(First));
        Assert.Equal(1UL, next.NaturalCapital.LastAppliedTurn);
        Assert.True(next.NaturalCapital.At(cell).RawStandingWood <
            original.NaturalCapital.At(cell).RawStandingWood);

        Assert.Equal(originalWorldHash, next.WorldHash);
        Assert.NotEqual(original.Sha256Hex, next.Sha256Hex);
        Assert.Equal(0UL, original.MaterialReserves.RawAvailable(A, Wood));
        Assert.Equal(0UL, original.NaturalCapital.LastAppliedTurn);
        Assert.Equal(2UL * D, Assert.Single(
            proposed.RealizedProductionAndDemand.DemandFlows).RawQuantity);
    }

    [Fact]
    public void TwoCivilizationsMayHoldFromSamePhysicalHarvestWithoutDuplication()
    {
        var generated = Generate();
        var cell = ForestedCell(generated);
        var initial = Initial(generated, World(generated, cell, twoCivilizations: true));

        var proposed = Turn(generated, initial, 1UL, Hold(B), Hold(A));
        var next = proposed.ProposedNext;
        Assert.Equal(D / 100UL, next.MaterialReserves.RawAvailable(A, Wood));
        Assert.Equal(D / 100UL, next.MaterialReserves.RawAvailable(B, Wood));
        Assert.Equal(1UL, next.MaterialReserves.LastAppliedTurn(First));
        Assert.Equal(1UL, next.MaterialReserves.LastAppliedTurn(Second));

        var realizedTotal = proposed.RealizedProductionAndDemand.SupplyFlows
            .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity));
        var offeredTotal = proposed.MarketOfferAndDemand.SupplyFlows
            .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity));

        Assert.Equal(2UL * (D / 100UL), realizedTotal - offeredTotal);
        Assert.True(realizedTotal <= initial.NaturalCapital.At(cell).RawStandingWood);

        var reversed = Turn(generated, initial, 1UL, Hold(A), Hold(B));
        Assert.Equal(next.Sha256Hex, reversed.ProposedNext.Sha256Hex);
        Assert.Equal(next.MaterialReserves.Balances.Select(x => x.RawQuantity),
            reversed.ProposedNext.MaterialReserves.Balances.Select(x => x.RawQuantity));
    }

    [Fact]
    public void AnyMaterialOrEcologicalChangeChangesCommitmentButNotOldWorldHash()
    {
        var generated = Generate();
        var world = World(generated, ForestedCell(generated));
        var initial = Initial(generated, world);
        var otherReserves = new EconomicMaterialReserveBook(new[]
        {
            new EconomicMaterialReserveBalance(A, Wood, 1UL)
        }, Array.Empty<EconomicMaterialReserveTurnCursor>());
        var altered = Initial(generated, world, reserves: otherReserves);
        var anotherPolicy = Initial(generated, world, policy: Policy(woodRecovery: 1L));

        Assert.Equal(initial.WorldHash, altered.WorldHash);
        Assert.NotEqual(initial.Sha256Hex, altered.Sha256Hex);
        Assert.Equal(initial.WorldHash, anotherPolicy.WorldHash);
        Assert.NotEqual(initial.Sha256Hex, anotherPolicy.Sha256Hex);

        var firstTurn = Turn(generated, initial, 1UL);
        Assert.Equal(initial.WorldHash, firstTurn.ProposedNext.WorldHash);
        Assert.NotEqual(initial.Sha256Hex, firstTurn.ProposedNext.Sha256Hex);
    }

    [Fact]
    public void OldExpectedDigestAndRepeatedOrSkippedTurnsAreRejected()
    {
        var generated = Generate();
        var original = Initial(generated, World(generated, ForestedCell(generated)));
        var first = Turn(generated, original, 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            Coordinator.Propose(generated, first.ProposedNext,
                original.Sha256Hex, 2UL,
                Array.Empty<EconomicDemandProfile>(),
                Array.Empty<EconomicMaterialOriginHoldOrder>()));
        Assert.Throws<InvalidOperationException>(() =>
            Turn(generated, first.ProposedNext, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            Turn(generated, first.ProposedNext, 3UL));
        var second = Turn(generated, first.ProposedNext, 2UL);
        Assert.Equal(2UL, second.ProposedNext.NaturalCapital.LastAppliedTurn);
    }

    [Fact]
    public void OverholdingPhysicalOutputAndUnknownPointsFailWithoutMutatingInputs()
    {
        var generated = Generate();
        var initial = Initial(generated, World(generated, ForestedCell(generated)));
        var originalHash = initial.Sha256Hex;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Turn(generated, initial, 1UL,
                new EconomicMaterialOriginHoldOrder(
                    A, Wood, EconomicActivityKind.Forestry, ulong.MaxValue)));
        Assert.Throws<InvalidOperationException>(() =>
            Turn(generated, initial, 1UL,
                new EconomicMaterialOriginHoldOrder(
                    new EconomicPointId(99UL), Wood,
                    EconomicActivityKind.Forestry, D / 100UL)));
        Assert.Equal(originalHash, initial.Sha256Hex);
        Assert.Equal(0UL, initial.NaturalCapital.LastAppliedTurn);
        Assert.Empty(initial.MaterialReserves.Balances);
    }

    [Fact]
    public void EquivalentSnapshotsAndRowOrderingProduceSameDigest()
    {
        var generated = Generate();
        var world = World(generated, ForestedCell(generated), twoCivilizations: true);
        var policy = Policy();
        var a = Initial(generated, world);
        var reorderedNatural = new EconomicNaturalCapitalBook(
            0UL, a.NaturalCapital.DerivationVersion,
            a.NaturalCapital.Cells.Reverse());
        var b = new EconomicEcologicalCheckpoint(generated, world, reorderedNatural,
            EconomicMaterialReserveBook.Empty, policy, Mapping);

        Assert.Equal(a.Sha256Hex, b.Sha256Hex);
        Assert.Equal(EconomicEcologicalCheckpoint.CommitmentVersion, 1U);
        Assert.Equal(64, a.Sha256Hex.Length);
    }

    [Fact]
    public void CheckpointRejectsUnknownReservesAndFutureCursors()
    {
        var generated = Generate();
        var world = World(generated, ForestedCell(generated));
        var wrongPoint = new EconomicMaterialReserveBook(new[]
        {
            new EconomicMaterialReserveBalance(new EconomicPointId(99UL), Wood, 1UL)
        }, Array.Empty<EconomicMaterialReserveTurnCursor>());

        Assert.Throws<InvalidOperationException>(() =>
            Initial(generated, world, reserves: wrongPoint));

        var futureCursor = new EconomicMaterialReserveBook(
            Array.Empty<EconomicMaterialReserveBalance>(),
            new[] { new EconomicMaterialReserveTurnCursor(First, 1UL) });
        Assert.Throws<InvalidOperationException>(() =>
            Initial(generated, world, reserves: futureCursor));

        var otherWorld = Generate(1UL);
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicEcologicalCheckpoint(otherWorld, world,
                EconomicNaturalCapitalInitializer.Initialize(generated, Policy()),
                EconomicMaterialReserveBook.Empty, Policy(), Mapping));
    }
}
