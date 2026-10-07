using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M45BMilitaryMoveCommandValidationTests
{
    [Fact]
    public void CommandCapturesIdentityAndOrderFields()
    {
        var id =
            CreateCommandId();

        var issuer =
            new CivilizationId(
                1UL);

        var unitId =
            new MilitaryUnitId(
                7UL);

        var destination =
            new StrategicCellId(
                2UL);

        var command =
            new MilitaryMoveCommand(
                id,
                issuer,
                unitId,
                destination);

        Assert.Equal(
            id,
            command.Id);

        Assert.Equal(
            issuer,
            command.IssuingCivilizationId);

        Assert.Equal(
            unitId,
            command.MilitaryUnitId);

        Assert.Equal(
            destination,
            command.DestinationStrategicCellId);
    }

    [Fact]
    public void CommandRejectsDefaultCommandId()
    {
        Assert.Throws<ArgumentException>(
            () => new MilitaryMoveCommand(
                default,
                new CivilizationId(
                    1UL),
                new MilitaryUnitId(
                    1UL),
                new StrategicCellId(
                    2UL)));
    }

    [Fact]
    public void CommandRejectsInvalidIssuer()
    {
        Assert.Throws<ArgumentException>(
            () => new MilitaryMoveCommand(
                CreateCommandId(),
                default,
                new MilitaryUnitId(
                    1UL),
                new StrategicCellId(
                    2UL)));
    }

    [Fact]
    public void CommandRejectsInvalidUnitId()
    {
        Assert.Throws<ArgumentException>(
            () => new MilitaryMoveCommand(
                CreateCommandId(),
                new CivilizationId(
                    1UL),
                default,
                new StrategicCellId(
                    2UL)));
    }

    [Fact]
    public void CommandRejectsInvalidDestination()
    {
        Assert.Throws<ArgumentException>(
            () => new MilitaryMoveCommand(
                CreateCommandId(),
                new CivilizationId(
                    1UL),
                new MilitaryUnitId(
                    1UL),
                default));
    }

    [Fact]
    public void ValidatorRejectsUnsupportedCommandType()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var state =
            CreateBoundState(
                generatedWorld,
                Array.Empty<MilitaryUnitRuntimeState>());

        Assert.False(
            validator.IsValid(
                state,
                new OtherCommand(
                    CreateCommandId()),
                CreateContext()));
    }

    [Fact]
    public void ValidatorRejectsUnboundWorldState()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetDirectNeighbor(
                generatedWorld,
                source);

        var state =
            CreateUnboundState(
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        source)
                });

        Assert.False(
            validator.IsValid(
                state,
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destination),
                CreateContext()));
    }

    [Fact]
    public void ValidatorRejectsWorldBoundToDifferentGeneratedWorld()
    {
        var expectedWorld =
            GenerateWorld(
                0UL);

        var differentWorld =
            GenerateWorld(
                1UL);

        var validator =
            new MilitaryMoveCommandValidator(
                expectedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetDirectNeighbor(
                expectedWorld,
                source);

        var state =
            CreateBoundState(
                differentWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        source)
                });

        Assert.False(
            validator.IsValid(
                state,
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destination),
                CreateContext()));
    }

    [Fact]
    public void ValidatorRejectsCommandFromDifferentTurn()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetDirectNeighbor(
                generatedWorld,
                source);

        var state =
            CreateBoundState(
                generatedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        source)
                });

        var command =
            new MilitaryMoveCommand(
                new CommandId(
                    new TurnNumber(
                        2UL),
                    1UL),
                new CivilizationId(
                    1UL),
                new MilitaryUnitId(
                    1UL),
                destination);

        Assert.False(
            validator.IsValid(
                state,
                command,
                CreateContext()));
    }

    [Fact]
    public void ValidatorRejectsUnknownUnit()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetDirectNeighbor(
                generatedWorld,
                source);

        var state =
            CreateBoundState(
                generatedWorld,
                Array.Empty<MilitaryUnitRuntimeState>());

        Assert.False(
            validator.IsValid(
                state,
                CreateMoveCommand(
                    1UL,
                    99UL,
                    destination),
                CreateContext()));
    }

    [Fact]
    public void ValidatorRejectsIssuerOwnerMismatch()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetDirectNeighbor(
                generatedWorld,
                source);

        var state =
            CreateBoundState(
                generatedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        2UL,
                        source)
                });

        Assert.False(
            validator.IsValid(
                state,
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destination),
                CreateContext()));
    }

    [Fact]
    public void ValidatorRejectsDestinationOutsideBoundWorld()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var state =
            CreateBoundState(
                generatedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        source)
                });

        var foreignDestination =
            new StrategicCellId(
                checked(
                    generatedWorld
                        .Request
                        .StrategicParameters
                        .StrategicCellCount
                    + 1UL));

        Assert.False(
            validator.IsValid(
                state,
                CreateMoveCommand(
                    1UL,
                    1UL,
                    foreignDestination),
                CreateContext()));
    }

    [Fact]
    public void ValidatorRejectsNonNeighborDestination()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetNonNeighbor(
                generatedWorld,
                source);

        var state =
            CreateBoundState(
                generatedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        source)
                });

        Assert.False(
            validator.IsValid(
                state,
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destination),
                CreateContext()));
    }

    [Fact]
    public void ValidatorAcceptsDirectNeighborDestination()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetDirectNeighbor(
                generatedWorld,
                source);

        var state =
            CreateBoundState(
                generatedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        source)
                });

        Assert.True(
            validator.IsValid(
                state,
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destination),
                CreateContext()));
    }

    [Fact]
    public void ValidatorAllowsColocationAtDestination()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetDirectNeighbor(
                generatedWorld,
                source);

        var state =
            CreateBoundState(
                generatedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        source),
                    CreateUnit(
                        2UL,
                        2UL,
                        destination)
                });

        Assert.True(
            validator.IsValid(
                state,
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destination),
                CreateContext()));
    }

    private static CommandId CreateCommandId()
    {
        return new CommandId(
            new TurnNumber(
                1UL),
            1UL);
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new TurnNumber(
                1UL),
            new SimulationSeed(
                123UL));
    }

    private static MilitaryMoveCommand CreateMoveCommand(
        ulong issuerId,
        ulong unitId,
        StrategicCellId destination)
    {
        return new MilitaryMoveCommand(
            CreateCommandId(),
            new CivilizationId(
                issuerId),
            new MilitaryUnitId(
                unitId),
            destination);
    }

    private static MilitaryUnitRuntimeState CreateUnit(
        ulong unitId,
        ulong ownerId,
        StrategicCellId cellId)
    {
        return new MilitaryUnitRuntimeState(
            new MilitaryUnitId(
                unitId),
            new CivilizationId(
                ownerId),
            cellId);
    }

    private static WorldState CreateBoundState(
        WorldGenerationResult generatedWorld,
        IEnumerable<MilitaryUnitRuntimeState> units)
    {
        return WorldState
            .CreateBound(
                generatedWorld)
            .WithCivilizations(
                CreateCivilizations())
            .WithWarfare(
                new WarfareRuntimeState(
                    units));
    }

    private static WorldState CreateUnboundState(
        IEnumerable<MilitaryUnitRuntimeState> units)
    {
        return WorldState
            .CreateInitial()
            .WithCivilizations(
                CreateCivilizations())
            .WithWarfare(
                new WarfareRuntimeState(
                    units));
    }

    private static CivilizationRuntimeState CreateCivilizations()
    {
        return new CivilizationRuntimeState(
            new[]
            {
                new CivilizationId(
                    1UL),
                new CivilizationId(
                    2UL)
            });
    }

    private static StrategicCellId GetDirectNeighbor(
        WorldGenerationResult generatedWorld,
        StrategicCellId source)
    {
        return generatedWorld
            .StrategicTopology
            .Cells[
                checked(
                    (int)source.Value
                    - 1)]
            .AdjacentCellIds[0];
    }

    private static StrategicCellId GetNonNeighbor(
        WorldGenerationResult generatedWorld,
        StrategicCellId source)
    {
        var sourceCell =
            generatedWorld
                .StrategicTopology
                .Cells[
                    checked(
                        (int)source.Value
                        - 1)];

        var neighbors =
            sourceCell
                .AdjacentCellIds
                .ToHashSet();

        return generatedWorld
            .StrategicTopology
            .Cells
            .Select(
                cell =>
                    cell.Id)
            .First(
                cellId =>
                    cellId != source
                    && !neighbors.Contains(
                        cellId));
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

    private sealed record OtherCommand(
        CommandId Id)
        : ISimulationCommand;
}