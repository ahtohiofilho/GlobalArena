using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class RuntimeInitialEconomicPointMaterializerTests
{
    private static (WorldGenerationResult World,
        CivilizationRuntimeState Civilizations,
        StrategicTerritoryRuntimeState Territory) Fixture()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civilizations = RuntimeCivilizationMaterializer.Materialize(world, 3);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(civilizations);
        return (world, civilizations, territory);
    }

    private static EconomicPointRuntimeState Point(
        ulong id,
        CivilizationId owner,
        StrategicCellId cell,
        ulong workforce = 8UL) =>
        new(
            new EconomicPointId(id),
            owner,
            cell,
            workforce,
            new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 3UL)
            });

    [Fact]
    public void MaterializesExplicitEconomicPointsAtActualWorldgenCivilizationStarts()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        var second = civilizations.Records[1];
        var raw = new List<EconomicPointRuntimeState>
        {
            Point(2UL, second.Id, second.StartCellId!.Value),
            Point(1UL, first.Id, first.StartCellId!.Value)
        };

        var economy = RuntimeInitialEconomicPointMaterializer.Materialize(
            world, civilizations, territory, raw);
        raw.Clear();

        Assert.Equal(new[] { 1UL, 2UL },
            economy.EconomicPoints.Select(point => point.Id.Value));
        Assert.Equal(first.StartCellId!.Value,
            economy.EconomicPoints[0].StrategicCellId);
        Assert.Equal(second.StartCellId!.Value,
            economy.EconomicPoints[1].StrategicCellId);
        Assert.All(economy.EconomicPoints,
            point => Assert.Equal(8UL, point.Workforce));

        var bound = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomy(economy);
        Assert.Equal(2, bound.Economy.EconomicPoints.Count);
        Assert.True(bound.IsWorldBound);
    }

    [Fact]
    public void AllowsMultiplePointsOnOneControlledCellWithoutInventingExtraTerritory()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];

        var economy = RuntimeInitialEconomicPointMaterializer.Materialize(
            world, civilizations, territory,
            new[]
            {
                Point(3UL, first.Id, first.StartCellId!.Value),
                Point(1UL, first.Id, first.StartCellId!.Value)
            });

        Assert.Equal(new[] { 1UL, 3UL },
            economy.EconomicPoints.Select(point => point.Id.Value));
        Assert.Equal(3, territory.Controls.Count);
    }

    [Fact]
    public void UnknownOrUnmaterializedOwnerIsRejected()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialEconomicPointMaterializer.Materialize(
                world, civilizations, territory,
                new[] { Point(1UL, new CivilizationId(999UL), first.StartCellId!.Value) }));

        var identityOnly = new CivilizationRuntimeState(
            new[] { first.Id });

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialEconomicPointMaterializer.Materialize(
                world, identityOnly, StrategicTerritoryRuntimeState.Empty,
                new[] { Point(1UL, first.Id, first.StartCellId!.Value) }));
    }

    [Fact]
    public void RejectsPointsOutsideTheGeneratedWorld()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        var outside = new StrategicCellId(
            checked((ulong)world.StrategicSurfaceGraph.NodeCount + 1UL));

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialEconomicPointMaterializer.Materialize(
                world, civilizations, territory,
                new[] { Point(1UL, first.Id, outside) }));
    }

    [Fact]
    public void RejectsUnownedLandAndForeignTerritorialControl()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        var second = civilizations.Records[1];

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialEconomicPointMaterializer.Materialize(
                world, civilizations, territory,
                new[] { Point(1UL, first.Id, second.StartCellId!.Value) }));

        var unownedLand = Enumerable.Range(
                1, world.StrategicSurfaceGraph.NodeCount)
            .Select(value => new StrategicCellId((ulong)value))
            .First(cell => world.StrategicPhysicalFields.LandWater.GetKind(cell)
                == StrategicLandWaterKind.Land
                && !territory.TryGetController(cell, out _));

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialEconomicPointMaterializer.Materialize(
                world, civilizations, territory,
                new[] { Point(1UL, first.Id, unownedLand) }));
    }

    [Fact]
    public void RejectsDuplicatePointIdentities()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        var point = Point(1UL, first.Id, first.StartCellId!.Value);

        Assert.Throws<ArgumentException>(() =>
            RuntimeInitialEconomicPointMaterializer.Materialize(
                world, civilizations, territory,
                new[] { point, point }));
    }

    [Fact]
    public void EmptyInputDoesNotSynthesizePopulationOrSettlements()
    {
        var (world, civilizations, territory) = Fixture();
        var economy = RuntimeInitialEconomicPointMaterializer.Materialize(
            world, civilizations, territory,
            Array.Empty<EconomicPointRuntimeState>());
        Assert.Empty(economy.EconomicPoints);
    }

    [Fact]
    public void GeneratedWorldEconomicPointParticipatesInACommandlessTurn()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        var economy = RuntimeInitialEconomicPointMaterializer.Materialize(
            world, civilizations, territory,
            new[] { Point(1UL, first.Id, first.StartCellId!.Value) });

        var session = new CanonicalGameSession(world);
        session.AcceptSnapshot(session.Snapshot
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomy(economy));

        var original = session.Snapshot;
        var hash = new CanonicalWorldStateHasher().Compute(original);
        var phase = new EconomicProductionDemandTurnPhase(
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(1U),
                    EconomicActivityKind.Agriculture, 500_000L)
            },
            new[]
            {
                new EconomicDemandProfile(
                    new EconomicPointId(1UL), new CommodityId(1U),
                    250_000L)
            });

        var gate = new AuthoritativeTurnCommitGate(
            session,
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { phase })),
            new TurnNumber(1UL),
            1U);

        var result = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false),
            new TurnResolutionInput(
                session.Snapshot,
                Array.Empty<ISimulationCommand>(),
                new SimulationContext(new TurnNumber(1UL), new SimulationSeed(43UL)))));

        Assert.True(result.WasResolved);
        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            result.ResolutionResult!.Events.Single());
        Assert.Equal(1_500_000UL, evt.Flows.SupplyFlows.Single().RawQuantity);
        Assert.Equal(2_000_000UL, evt.Flows.DemandFlows.Single().RawQuantity);
        Assert.Same(original, session.Snapshot);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(session.Snapshot));
        Assert.Equal(new TurnNumber(2UL), gate.NextTurn);
    }
}
