using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M42EOwnershipLinkIntegrationTests
{
    [Fact]
    public void RepresentativeIntegratedBoundStateIsAccepted()
    {
        var state =
            CreateRepresentativeBoundState();

        Assert.True(
            state.IsWorldBound);

        Assert.Equal(
            3,
            state.Civilizations.Records.Count);

        Assert.Equal(
            3,
            state.Territory.Controls.Count);

        Assert.Equal(
            2,
            state.Diplomacy.Relations.Count);

        Assert.Equal(
            2,
            state.Economy.StrategicStocks.Count);

        Assert.Equal(
            3,
            state.Warfare.Units.Count);
    }

    [Fact]
    public void RepeatedMaterializationProducesSameCivilizationStarts()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var first =
            RuntimeCivilizationMaterializer.Materialize(
                generatedWorld,
                3);

        var second =
            RuntimeCivilizationMaterializer.Materialize(
                generatedWorld,
                3);

        Assert.Equal(
            first.Records.Count,
            second.Records.Count);

        for (var index = 0;
             index < first.Records.Count;
             index++)
        {
            Assert.Equal(
                first.Records[index].Id,
                second.Records[index].Id);

            Assert.Equal(
                first.Records[index].StartCellId,
                second.Records[index].StartCellId);
        }
    }

    [Fact]
    public void MaterializedStartsAreUniqueStrategicLand()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var civilizations =
            RuntimeCivilizationMaterializer.Materialize(
                generatedWorld,
                3);

        var starts =
            new HashSet<StrategicCellId>();

        foreach (var civilization in
            civilizations.Records)
        {
            Assert.True(
                civilization.StartCellId.HasValue);

            var start =
                civilization.StartCellId.Value;

            Assert.True(
                starts.Add(
                    start));

            Assert.Equal(
                StrategicLandWaterKind.Land,
                generatedWorld
                    .StrategicPhysicalFields
                    .LandWater
                    .GetKind(
                        start));
        }
    }

    [Fact]
    public void InitialTerritoryControlsExactlyEveryMaterializedStart()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var civilizations =
            RuntimeCivilizationMaterializer.Materialize(
                generatedWorld,
                3);

        var territory =
            RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
                civilizations);

        Assert.Equal(
            civilizations.Records.Count,
            territory.Controls.Count);

        foreach (var civilization in
            civilizations.Records)
        {
            Assert.True(
                civilization.StartCellId.HasValue);

            Assert.True(
                territory.TryGetController(
                    civilization.StartCellId.Value,
                    out var controller));

            Assert.Equal(
                civilization.Id,
                controller);
        }
    }

    [Fact]
    public void StrategicCellOutsideInitialStartsRemainsUnowned()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var civilizations =
            RuntimeCivilizationMaterializer.Materialize(
                generatedWorld,
                3);

        var territory =
            RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
                civilizations);

        var starts =
            civilizations
                .Records
                .Select(
                    civilization =>
                        civilization.StartCellId!.Value)
                .ToHashSet();

        var unowned =
            Enumerable
                .Range(
                    1,
                    generatedWorld.StrategicSurfaceGraph.NodeCount)
                .Select(
                    value =>
                        new StrategicCellId(
                            checked(
                                (ulong)value)))
                .First(
                    cell =>
                        !starts.Contains(
                            cell));

        Assert.False(
            territory.TryGetController(
                unowned,
                out _));
    }

    [Fact]
    public void EmptyDiplomacyDefaultsNeutralAcrossMaterializedCivilizations()
    {
        var civilizations =
            RuntimeCivilizationMaterializer.Materialize(
                GenerateWorld(
                    0UL),
                3);

        for (var firstIndex = 0;
             firstIndex < civilizations.Civilizations.Count;
             firstIndex++)
        {
            for (var secondIndex = firstIndex + 1;
                 secondIndex < civilizations.Civilizations.Count;
                 secondIndex++)
            {
                Assert.Equal(
                    BaselineDiplomacyRelationKind.Neutral,
                    BaselineDiplomacyRuntimeState.Empty.GetRelation(
                        civilizations.Civilizations[firstIndex],
                        civilizations.Civilizations[secondIndex]));
            }
        }
    }

    [Fact]
    public void EnemyAndAllyOverridesAreSymmetricInIntegratedState()
    {
        var state =
            CreateRepresentativeBoundState();

        Assert.Equal(
            BaselineDiplomacyRelationKind.Enemy,
            state.Diplomacy.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(2UL)));

        Assert.Equal(
            BaselineDiplomacyRelationKind.Enemy,
            state.Diplomacy.GetRelation(
                new CivilizationId(2UL),
                new CivilizationId(1UL)));

        Assert.Equal(
            BaselineDiplomacyRelationKind.Ally,
            state.Diplomacy.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(3UL)));

        Assert.Equal(
            BaselineDiplomacyRelationKind.Ally,
            state.Diplomacy.GetRelation(
                new CivilizationId(3UL),
                new CivilizationId(1UL)));
    }

    [Fact]
    public void EconomyOwnersResolveToMaterializedCivilizations()
    {
        var state =
            CreateRepresentativeBoundState();

        var civilizationIds =
            state.Civilizations.Civilizations.ToHashSet();

        Assert.All(
            state.Economy.StrategicStocks,
            stock =>
                Assert.Contains(
                    stock.Owner,
                    civilizationIds));
    }

    [Fact]
    public void WarfareOwnersResolveToMaterializedCivilizations()
    {
        var state =
            CreateRepresentativeBoundState();

        var civilizationIds =
            state.Civilizations.Civilizations.ToHashSet();

        Assert.All(
            state.Warfare.Units,
            unit =>
                Assert.Contains(
                    unit.Owner,
                    civilizationIds));
    }

    [Fact]
    public void IntegratedStateRejectsUnknownEconomyOwner()
    {
        var state =
            CreateRepresentativeBoundState();

        Assert.Throws<InvalidOperationException>(
            () => state.WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        new StrategicStockEntry(
                            new CivilizationId(4UL),
                            new CommodityId(1U),
                            10L)
                    })));
    }

    [Fact]
    public void IntegratedStateRejectsUnknownWarfareOwner()
    {
        var state =
            CreateRepresentativeBoundState();

        Assert.Throws<InvalidOperationException>(
            () => state.WithWarfare(
                new WarfareRuntimeState(
                    new[]
                    {
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(10UL),
                            new CivilizationId(4UL),
                            new StrategicCellId(1UL))
                    })));
    }

    [Fact]
    public void BoundIntegratedStateRejectsUnitOutsideGeneratedWorld()
    {
        var state =
            CreateRepresentativeBoundState();

        Assert.Throws<InvalidOperationException>(
            () => state.WithWarfare(
                new WarfareRuntimeState(
                    new[]
                    {
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(10UL),
                            new CivilizationId(1UL),
                            new StrategicCellId(43UL))
                    })));
    }

    [Fact]
    public void AdvanceRevisionPreservesAllIntegratedSnapshots()
    {
        var state =
            CreateRepresentativeBoundState();

        var advanced =
            state.AdvanceRevision();

        Assert.Same(
            state.Civilizations,
            advanced.Civilizations);

        Assert.Same(
            state.Territory,
            advanced.Territory);

        Assert.Same(
            state.Diplomacy,
            advanced.Diplomacy);

        Assert.Same(
            state.Economy,
            advanced.Economy);

        Assert.Same(
            state.Warfare,
            advanced.Warfare);

        Assert.Equal(
            state.Revision + 1UL,
            advanced.Revision);
    }

    [Fact]
    public void EquivalentInputOrderingProducesSameIntegratedHash()
    {
        var first =
            CreateManualIntegratedState(
                reverseInput: false);

        var second =
            CreateManualIntegratedState(
                reverseInput: true);

        var hasher =
            new CanonicalWorldStateHasher();

        Assert.Equal(
            hasher.Compute(
                first),
            hasher.Compute(
                second));
    }

    [Fact]
    public void IntegratedStateHasKnownCanonicalHash()
    {
        var hash =
            new CanonicalWorldStateHasher()
                .Compute(
                    CreateManualIntegratedState(
                        reverseInput: true));

        Assert.Equal(
            KnownIntegratedDigest,
            hash.HexDigest);
    }

    [Fact]
    public void DiplomacyMutationPreservesOwnershipSnapshots()
    {
        var state =
            CreateRepresentativeBoundState();

        var changed =
            state.WithDiplomacy(
                state.Diplomacy.WithRelation(
                    new CivilizationId(2UL),
                    new CivilizationId(3UL),
                    BaselineDiplomacyRelationKind.Enemy));

        Assert.Same(
            state.Civilizations,
            changed.Civilizations);

        Assert.Same(
            state.Territory,
            changed.Territory);

        Assert.Same(
            state.Economy,
            changed.Economy);

        Assert.Same(
            state.Warfare,
            changed.Warfare);

        Assert.Equal(
            BaselineDiplomacyRelationKind.Enemy,
            changed.Diplomacy.GetRelation(
                new CivilizationId(3UL),
                new CivilizationId(2UL)));
    }

    [Fact]
    public void TerritoryDiplomacyEconomyAndWarfareCoexistWithoutDirectCoupling()
    {
        var state =
            CreateRepresentativeBoundState();

        foreach (var control in
            state.Territory.Controls)
        {
            Assert.Contains(
                control.Controller,
                state.Civilizations.Civilizations);
        }

        foreach (var relation in
            state.Diplomacy.Relations)
        {
            Assert.Contains(
                relation.First,
                state.Civilizations.Civilizations);

            Assert.Contains(
                relation.Second,
                state.Civilizations.Civilizations);
        }

        foreach (var stock in
            state.Economy.StrategicStocks)
        {
            Assert.Contains(
                stock.Owner,
                state.Civilizations.Civilizations);
        }

        foreach (var unit in
            state.Warfare.Units)
        {
            Assert.Contains(
                unit.Owner,
                state.Civilizations.Civilizations);
        }
    }

    [Fact]
    public void RepresentativeM42ExitScenarioIsDeterministic()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var first =
            hasher.Compute(
                CreateRepresentativeBoundState());

        var second =
            hasher.Compute(
                CreateRepresentativeBoundState());

        Assert.Equal(
            first,
            second);
    }

    private static WorldState CreateRepresentativeBoundState()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var civilizations =
            RuntimeCivilizationMaterializer.Materialize(
                generatedWorld,
                3);

        var territory =
            RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
                civilizations);

        var diplomacy =
            BaselineDiplomacyRuntimeState.Empty
                .WithRelation(
                    new CivilizationId(1UL),
                    new CivilizationId(2UL),
                    BaselineDiplomacyRelationKind.Enemy)
                .WithRelation(
                    new CivilizationId(1UL),
                    new CivilizationId(3UL),
                    BaselineDiplomacyRelationKind.Ally);

        var firstStart =
            civilizations.Records[0].StartCellId!.Value;

        var secondStart =
            civilizations.Records[1].StartCellId!.Value;

        var thirdStart =
            civilizations.Records[2].StartCellId!.Value;

        return WorldState.CreateBound(
                generatedWorld)
            .WithCivilizations(
                civilizations)
            .WithTerritory(
                territory)
            .WithDiplomacy(
                diplomacy)
            .WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        new StrategicStockEntry(
                            new CivilizationId(2UL),
                            new CommodityId(1U),
                            30L),
                        new StrategicStockEntry(
                            new CivilizationId(1UL),
                            new CommodityId(1U),
                            25L)
                    }))
            .WithWarfare(
                new WarfareRuntimeState(
                    new[]
                    {
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(5UL),
                            new CivilizationId(3UL),
                            thirdStart),
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(2UL),
                            new CivilizationId(2UL),
                            secondStart),
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(1UL),
                            new CivilizationId(1UL),
                            firstStart)
                    }));
    }

    private static WorldState CreateManualIntegratedState(
        bool reverseInput)
    {
        CivilizationRuntimeRecord[] civilizations =
        {
            new(
                new CivilizationId(1UL),
                new StrategicCellId(3UL)),
            new(
                new CivilizationId(2UL),
                new StrategicCellId(10UL))
        };

        StrategicTerritoryControlEntry[] territory =
        {
            new(
                new StrategicCellId(3UL),
                new CivilizationId(1UL)),
            new(
                new StrategicCellId(10UL),
                new CivilizationId(2UL))
        };

        StrategicStockEntry[] stocks =
        {
            new(
                new CivilizationId(1UL),
                new CommodityId(1U),
                25L),
            new(
                new CivilizationId(2UL),
                new CommodityId(1U),
                30L)
        };

        MilitaryUnitRuntimeState[] units =
        {
            new(
                new MilitaryUnitId(2UL),
                new CivilizationId(1UL),
                new StrategicCellId(3UL)),
            new(
                new MilitaryUnitId(5UL),
                new CivilizationId(2UL),
                new StrategicCellId(10UL))
        };

        if (reverseInput)
        {
            Array.Reverse(
                civilizations);

            Array.Reverse(
                territory);

            Array.Reverse(
                stocks);

            Array.Reverse(
                units);
        }

        return WorldState.CreateInitial()
            .WithCivilizations(
                new CivilizationRuntimeState(
                    civilizations))
            .WithTerritory(
                new StrategicTerritoryRuntimeState(
                    territory))
            .WithDiplomacy(
                BaselineDiplomacyRuntimeState.Empty.WithRelation(
                    new CivilizationId(2UL),
                    new CivilizationId(1UL),
                    BaselineDiplomacyRelationKind.Enemy))
            .WithEconomy(
                new EconomyRuntimeState(
                    stocks))
            .WithWarfare(
                new WarfareRuntimeState(
                    units));
    }

    private static WorldGenerationResult GenerateWorld(
        ulong seed)
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        return generator.Generate(
            new WorldGenerationRequest(
                new WorldSeed(
                    seed),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    0)));
    }

    private const string KnownIntegratedDigest =
        "ACF5314F2E481B07A2E7DB5E88D189DD"
        + "68EE05666A9EDE62B2012BE5AAEC53D5";
}
