using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M41CRuntimeWorldBindingInvariantTests
{
    [Fact]
    public void BindingFactoryRejectsNullGeneratedWorld()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeWorldBinding.FromGeneratedWorld(
                null!));
    }

    [Fact]
    public void BindingCapturesCanonicalWorldIdentity()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var binding =
            RuntimeWorldBinding.FromGeneratedWorld(
                generatedWorld);

        Assert.Equal(
            WorldGenerationSignature.CurrentFormatVersion,
            binding.WorldSignatureFormatVersion);

        Assert.Equal(
            KnownWorldSignature,
            binding.WorldSignatureSha256Hex);

        Assert.Equal(
            42UL,
            binding.StrategicCellCount);
    }

    [Fact]
    public void BindingRecognizesCanonicalStrategicCellRange()
    {
        var binding =
            RuntimeWorldBinding.FromGeneratedWorld(
                GenerateWorld(
                    0UL));

        Assert.True(
            binding.Contains(
                new StrategicCellId(
                    1UL)));

        Assert.True(
            binding.Contains(
                new StrategicCellId(
                    42UL)));

        Assert.False(
            binding.Contains(
                new StrategicCellId(
                    43UL)));

        Assert.False(
            binding.Contains(
                default));
    }

    [Fact]
    public void BindToWorldPreservesExistingRuntimeSnapshots()
    {
        var state =
            CreateValidUnboundState();

        var bound =
            state.BindToWorld(
                GenerateWorld(
                    0UL));

        Assert.True(
            bound.IsWorldBound);

        Assert.Same(
            state.Civilizations,
            bound.Civilizations);

        Assert.Same(
            state.Economy,
            bound.Economy);

        Assert.Same(
            state.Warfare,
            bound.Warfare);
    }

    [Fact]
    public void BindingToSameWorldIsIdempotent()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var state =
            WorldState.CreateBound(
                generatedWorld);

        Assert.Same(
            state,
            state.BindToWorld(
                generatedWorld));
    }

    [Fact]
    public void RebindingToDifferentWorldIsRejected()
    {
        var state =
            WorldState.CreateBound(
                GenerateWorld(
                    0UL));

        Assert.Throws<InvalidOperationException>(
            () => state.BindToWorld(
                GenerateWorld(
                    1UL)));
    }

    [Fact]
    public void EconomyOwnerMustExistInCivilizationRoster()
    {
        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        new EconomicPointRuntimeState(
                            new EconomicPointId(1UL),
                            new CivilizationId(2UL),
                            new StrategicCellId(1UL),
                            0UL)
                    })));
    }

    [Fact]
    public void WarfareOwnerMustExistInCivilizationRoster()
    {
        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithWarfare(
                new WarfareRuntimeState(
                    new[]
                    {
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(1UL),
                            new CivilizationId(2UL),
                            new StrategicCellId(1UL))
                    })));
    }

    [Fact]
    public void BoundStateRejectsUnitOutsideGeneratedWorld()
    {
        var state =
            WorldState.CreateBound(
                    GenerateWorld(
                        0UL))
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithWarfare(
                new WarfareRuntimeState(
                    new[]
                    {
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(1UL),
                            new CivilizationId(1UL),
                            new StrategicCellId(43UL))
                    })));
    }

    [Fact]
    public void CivilizationRosterCannotDropEconomyOwner()
    {
        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL),
                            new CivilizationId(2UL)
                        }))
                .WithEconomy(
                    new EconomyRuntimeState(
                        new[]
                        {
                            new EconomicPointRuntimeState(
                                new EconomicPointId(1UL),
                                new CivilizationId(2UL),
                                new StrategicCellId(1UL),
                                0UL)
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithCivilizations(
                new CivilizationRuntimeState(
                    new[]
                    {
                        new CivilizationId(1UL)
                    })));
    }

    [Fact]
    public void CivilizationRosterCannotDropWarfareOwner()
    {
        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL),
                            new CivilizationId(2UL)
                        }))
                .WithWarfare(
                    new WarfareRuntimeState(
                        new[]
                        {
                            new MilitaryUnitRuntimeState(
                                new MilitaryUnitId(1UL),
                                new CivilizationId(2UL),
                                new StrategicCellId(1UL))
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithCivilizations(
                new CivilizationRuntimeState(
                    new[]
                    {
                        new CivilizationId(1UL)
                    })));
    }

    [Fact]
    public void ValidBoundCrossDomainStateIsAccepted()
    {
        var state =
            WorldState.CreateBound(
                    GenerateWorld(
                        0UL))
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL),
                            new CivilizationId(2UL)
                        }))
                .WithEconomy(
                    new EconomyRuntimeState(
                        new[]
                        {
                            new EconomicPointRuntimeState(
                                new EconomicPointId(1UL),
                                new CivilizationId(1UL),
                                new StrategicCellId(1UL),
                                1UL,
                                new[]
                                {
                                    new EconomicActivityWorkforceAllocation(
                                        EconomicActivityKind.Agriculture,
                                        1UL)
                                })
                        }))
                .WithWarfare(
                    new WarfareRuntimeState(
                        new[]
                        {
                            new MilitaryUnitRuntimeState(
                                new MilitaryUnitId(1UL),
                                new CivilizationId(2UL),
                                new StrategicCellId(42UL))
                        }))
                .AdvanceRevision();

        Assert.True(
            state.IsWorldBound);

        Assert.Equal(
            1UL,
            state.Revision);

        Assert.Equal(
            KnownWorldSignature,
            state
                .WorldBinding!
                .WorldSignatureSha256Hex);

        Assert.Single(
            state.Economy.EconomicPoints);

        Assert.Single(
            state.Warfare.Units);
    }

    private static WorldState CreateValidUnboundState()
    {
        return WorldState.CreateInitial()
            .WithCivilizations(
                new CivilizationRuntimeState(
                    new[]
                    {
                        new CivilizationId(1UL),
                        new CivilizationId(2UL)
                    }))
            .WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        new EconomicPointRuntimeState(
                            new EconomicPointId(1UL),
                            new CivilizationId(1UL),
                            new StrategicCellId(2UL),
                            1UL)
                    }))
            .WithWarfare(
                new WarfareRuntimeState(
                    new[]
                    {
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(1UL),
                            new CivilizationId(2UL),
                            new StrategicCellId(2UL))
                    }));
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

    private const string KnownWorldSignature =
        "fa9677da59098b4eccc4a3911299520ad"
        + "0549780b4470b2fae96f607da774c52";
}