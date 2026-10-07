using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M45CMinimalStrategicMovementExecutionTests
{
    [Fact]
    public void EventCapturesMinimalMovementIntent()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var source =
            new StrategicCellId(
                1UL);

        var destination =
            GetDirectNeighbor(
                generatedWorld,
                source);

        var edge =
            GetConnectingEdge(
                generatedWorld,
                source,
                destination);

        var command =
            CreateMoveCommand(
                1UL,
                1UL,
                destination);

        var simulationEvent =
            new MilitaryMoveEvent(
                new EventId(
                    command.Id,
                    1UL),
                command.IssuingCivilizationId,
                command.MilitaryUnitId,
                source,
                destination,
                edge);

        Assert.Equal(
            new EventId(
                command.Id,
                1UL),
            simulationEvent.Id);

        Assert.Equal(
            new CivilizationId(
                1UL),
            simulationEvent.IssuingCivilizationId);

        Assert.Equal(
            new MilitaryUnitId(
                1UL),
            simulationEvent.MilitaryUnitId);

        Assert.Equal(
            source,
            simulationEvent.ExpectedSourceStrategicCellId);

        Assert.Equal(
            destination,
            simulationEvent.DestinationStrategicCellId);

        Assert.Equal(
            edge,
            simulationEvent.TraversedStrategicEdgeId);
    }

    [Fact]
    public void EventRejectsDefaultEventId()
    {
        Assert.Throws<ArgumentException>(
            () => new MilitaryMoveEvent(
                default,
                new CivilizationId(
                    1UL),
                new MilitaryUnitId(
                    1UL),
                new StrategicCellId(
                    1UL),
                new StrategicCellId(
                    2UL),
                new StrategicEdgeId(
                    1UL)));
    }

    [Fact]
    public void ProcessorBuildsSingleEventFromAuthoritativeSourceAndExactEdge()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

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
            CreateMoveCommand(
                1UL,
                1UL,
                destination);

        var processor =
            new MilitaryMoveCommandProcessor(
                generatedWorld);

        var simulationEvent =
            Assert.IsType<MilitaryMoveEvent>(
                Assert.Single(
                    processor.Process(
                        state,
                        command,
                        CreateContext())));

        Assert.Equal(
            new EventId(
                command.Id,
                1UL),
            simulationEvent.Id);

        Assert.Equal(
            source,
            simulationEvent.ExpectedSourceStrategicCellId);

        Assert.Equal(
            destination,
            simulationEvent.DestinationStrategicCellId);

        Assert.Equal(
            GetConnectingEdge(
                generatedWorld,
                source,
                destination),
            simulationEvent.TraversedStrategicEdgeId);

        Assert.Equal(
            0UL,
            state.Revision);

        Assert.Equal(
            source,
            Assert.Single(
                state.Warfare.Units)
                .StrategicCellId);
    }

    [Fact]
    public void ProcessorReturnsNoEventsForUnsupportedCommand()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var processor =
            new MilitaryMoveCommandProcessor(
                generatedWorld);

        var state =
            CreateBoundState(
                generatedWorld,
                Array.Empty<MilitaryUnitRuntimeState>());

        var events =
            processor.Process(
                state,
                new OtherCommand(
                    CreateCommandId()),
                CreateContext());

        Assert.Empty(
            events);
    }

    [Fact]
    public void ProcessorRejectsPlanningStateFromDifferentWorld()
    {
        var expectedWorld =
            GenerateWorld(
                0UL);

        var differentWorld =
            GenerateWorld(
                1UL);

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

        var processor =
            new MilitaryMoveCommandProcessor(
                expectedWorld);

        Assert.Throws<InvalidOperationException>(
            () => processor.Process(
                state,
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destination),
                CreateContext()));
    }

    [Fact]
    public void RevalidatorAcceptsCurrentMatchingMovementIntent()
    {
        var fixture =
            CreateFixture();

        Assert.True(
            fixture.Revalidator.CanExecute(
                fixture.State,
                fixture.Event,
                CreateContext()));
    }

    [Fact]
    public void RevalidatorRejectsEventFromDifferentTurn()
    {
        var fixture =
            CreateFixture();

        var differentContext =
            new SimulationContext(
                new TurnNumber(
                    2UL),
                new SimulationSeed(
                    123UL));

        Assert.False(
            fixture.Revalidator.CanExecute(
                fixture.State,
                fixture.Event,
                differentContext));
    }

    [Fact]
    public void RevalidatorRejectsWorldBoundToDifferentGeneratedWorld()
    {
        var fixture =
            CreateFixture();

        var differentWorld =
            GenerateWorld(
                1UL);

        var differentState =
            CreateBoundState(
                differentWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        fixture.Source)
                });

        Assert.False(
            fixture.Revalidator.CanExecute(
                differentState,
                fixture.Event,
                CreateContext()));
    }

    [Fact]
    public void RevalidatorRejectsUnknownUnit()
    {
        var fixture =
            CreateFixture();

        var state =
            CreateBoundState(
                fixture.GeneratedWorld,
                Array.Empty<MilitaryUnitRuntimeState>());

        Assert.False(
            fixture.Revalidator.CanExecute(
                state,
                fixture.Event,
                CreateContext()));
    }

    [Fact]
    public void RevalidatorRejectsOwnerMismatch()
    {
        var fixture =
            CreateFixture();

        var state =
            CreateBoundState(
                fixture.GeneratedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        2UL,
                        fixture.Source)
                });

        Assert.False(
            fixture.Revalidator.CanExecute(
                state,
                fixture.Event,
                CreateContext()));
    }

    [Fact]
    public void RevalidatorRejectsStaleExpectedSource()
    {
        var fixture =
            CreateFixture();

        var alternativeSource =
            GetDifferentCell(
                fixture.GeneratedWorld,
                fixture.Source,
                fixture.Destination);

        var state =
            CreateBoundState(
                fixture.GeneratedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        alternativeSource)
                });

        Assert.False(
            fixture.Revalidator.CanExecute(
                state,
                fixture.Event,
                CreateContext()));
    }

    [Fact]
    public void RevalidatorRejectsWrongTraversedEdge()
    {
        var fixture =
            CreateFixture();

        var wrongEdge =
            fixture
                .GeneratedWorld
                .StrategicTopology
                .Edges
                .Select(
                    edge =>
                        edge.Id)
                .First(
                    edgeId =>
                        edgeId
                        != fixture.Event.TraversedStrategicEdgeId);

        var forgedEvent =
            new MilitaryMoveEvent(
                fixture.Event.Id,
                fixture.Event.IssuingCivilizationId,
                fixture.Event.MilitaryUnitId,
                fixture.Event.ExpectedSourceStrategicCellId,
                fixture.Event.DestinationStrategicCellId,
                wrongEdge);

        Assert.False(
            fixture.Revalidator.CanExecute(
                fixture.State,
                forgedEvent,
                CreateContext()));
    }

    [Fact]
    public void RevalidatorAllowsDestinationColocation()
    {
        var fixture =
            CreateFixture();

        var state =
            CreateBoundState(
                fixture.GeneratedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        fixture.Source),
                    CreateUnit(
                        2UL,
                        2UL,
                        fixture.Destination)
                });

        Assert.True(
            fixture.Revalidator.CanExecute(
                state,
                fixture.Event,
                CreateContext()));
    }

    [Fact]
    public void ExecutorMovesOnlyTargetUnitAndAdvancesRevisionOnce()
    {
        var fixture =
            CreateFixture();

        var otherCell =
            GetDifferentCell(
                fixture.GeneratedWorld,
                fixture.Source,
                fixture.Destination);

        var state =
            CreateBoundState(
                fixture.GeneratedWorld,
                new[]
                {
                    CreateUnit(
                        1UL,
                        1UL,
                        fixture.Source),
                    CreateUnit(
                        2UL,
                        2UL,
                        otherCell)
                });

        var executor =
            new MilitaryMoveEventExecutor();

        var result =
            executor.Execute(
                state,
                fixture.Event,
                CreateContext());

        Assert.Equal(
            1UL,
            result.Revision);

        Assert.Equal(
            0UL,
            state.Revision);

        var moved =
            result
                .Warfare
                .Units
                .Single(
                    unit =>
                        unit.Id
                        == new MilitaryUnitId(
                            1UL));

        Assert.Equal(
            fixture.Destination,
            moved.StrategicCellId);

        var untouched =
            result
                .Warfare
                .Units
                .Single(
                    unit =>
                        unit.Id
                        == new MilitaryUnitId(
                            2UL));

        Assert.Equal(
            otherCell,
            untouched.StrategicCellId);

        Assert.Same(
            state.Civilizations,
            result.Civilizations);

        Assert.Same(
            state.Territory,
            result.Territory);

        Assert.Same(
            state.Diplomacy,
            result.Diplomacy);

        Assert.Same(
            state.Economy,
            result.Economy);
    }

    [Fact]
    public void CompletePipelineMovesUnitThroughRealContracts()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

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
            CreateMoveCommand(
                1UL,
                1UL,
                destination);

        var result =
            CreateResolver(
                generatedWorld)
            .Resolve(
                new TurnResolutionInput(
                    state,
                    new ISimulationCommand[]
                    {
                        command
                    },
                    CreateContext()));

        Assert.Equal(
            destination,
            Assert.Single(
                result.ResultingWorldState.Warfare.Units)
                .StrategicCellId);

        Assert.Equal(
            1UL,
            result.ResultingWorldState.Revision);

        var logEntry =
            Assert.Single(
                result.EventLog.Entries);

        Assert.True(
            logEntry.WasEligible);

        Assert.True(
            logEntry.WasExecuted);
    }

    [Fact]
    public void SameSourceCommandsExecuteOnlyFirstOrderedEventAndRejectLaterStaleEvent()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var source =
            new StrategicCellId(
                1UL);

        var destinations =
            GetTwoDirectNeighbors(
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

        var commands =
            new ISimulationCommand[]
            {
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destinations[0],
                    sequence: 1UL),
                CreateMoveCommand(
                    1UL,
                    1UL,
                    destinations[1],
                    sequence: 2UL)
            };

        var result =
            CreateResolver(
                generatedWorld)
            .Resolve(
                new TurnResolutionInput(
                    state,
                    commands,
                    CreateContext()));

        Assert.Equal(
            2,
            result.EventLog.Entries.Count);

        Assert.Single(
            result.EventLog.Entries,
            entry =>
                entry.WasExecuted);

        Assert.Single(
            result.EventLog.Entries,
            entry =>
                !entry.WasEligible
                && !entry.WasExecuted);

        Assert.Equal(
            1UL,
            result.ResultingWorldState.Revision);

        var executedEvent =
            Assert.IsType<MilitaryMoveEvent>(
                result
                    .EventLog
                    .Entries
                    .Single(
                        entry =>
                            entry.WasExecuted)
                    .Event);

        Assert.Equal(
            executedEvent.DestinationStrategicCellId,
            Assert.Single(
                result.ResultingWorldState.Warfare.Units)
                .StrategicCellId);
    }

    [Fact]
    public void PipelineAllowsColocationWithoutCreatingCombatSemantics()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

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

        var result =
            CreateResolver(
                generatedWorld)
            .Resolve(
                new TurnResolutionInput(
                    state,
                    new ISimulationCommand[]
                    {
                        CreateMoveCommand(
                            1UL,
                            1UL,
                            destination)
                    },
                    CreateContext()));

        Assert.Equal(
            2,
            result
                .ResultingWorldState
                .Warfare
                .Units
                .Count(
                    unit =>
                        unit.StrategicCellId
                        == destination));

        Assert.Equal(
            1UL,
            result.ResultingWorldState.Revision);
    }

    [Fact]
    public void EqualInputAndContextProduceDeterministicMovementResult()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var source =
            new StrategicCellId(
                1UL);

        var destinations =
            GetTwoDirectNeighbors(
                generatedWorld,
                source);

        var first =
            ResolveTwoCommands(
                generatedWorld,
                source,
                destinations);

        var second =
            ResolveTwoCommands(
                generatedWorld,
                source,
                destinations);

        Assert.Equal(
            Assert.Single(
                first.ResultingWorldState.Warfare.Units)
                .StrategicCellId,
            Assert.Single(
                second.ResultingWorldState.Warfare.Units)
                .StrategicCellId);

        Assert.Equal(
            first.ResultingWorldState.Revision,
            second.ResultingWorldState.Revision);

        Assert.Equal(
            first.Events.Select(
                simulationEvent =>
                    simulationEvent.Id),
            second.Events.Select(
                simulationEvent =>
                    simulationEvent.Id));

        Assert.Equal(
            first.EventLog.Entries.Select(
                entry =>
                    (
                        entry.EventId,
                        entry.WasEligible,
                        entry.WasExecuted)),
            second.EventLog.Entries.Select(
                entry =>
                    (
                        entry.EventId,
                        entry.WasEligible,
                        entry.WasExecuted)));
    }

    private static TurnResolutionResult ResolveTwoCommands(
        WorldGenerationResult generatedWorld,
        StrategicCellId source,
        IReadOnlyList<StrategicCellId> destinations)
    {
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

        return CreateResolver(
            generatedWorld)
            .Resolve(
                new TurnResolutionInput(
                    state,
                    new ISimulationCommand[]
                    {
                        CreateMoveCommand(
                            1UL,
                            1UL,
                            destinations[0],
                            sequence: 1UL),
                        CreateMoveCommand(
                            1UL,
                            1UL,
                            destinations[1],
                            sequence: 2UL)
                    },
                    CreateContext()));
    }

    private static TurnResolver CreateResolver(
        WorldGenerationResult generatedWorld)
    {
        return new TurnResolver(
            new MilitaryMoveCommandValidator(
                generatedWorld),
            new MilitaryMoveCommandProcessor(
                generatedWorld),
            new SeededSimulationEventOrderer(),
            new MilitaryMoveEventRevalidator(
                generatedWorld),
            new MilitaryMoveEventExecutor());
    }

    private static MovementFixture CreateFixture()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

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

        var processor =
            new MilitaryMoveCommandProcessor(
                generatedWorld);

        var command =
            CreateMoveCommand(
                1UL,
                1UL,
                destination);

        var simulationEvent =
            Assert.IsType<MilitaryMoveEvent>(
                Assert.Single(
                    processor.Process(
                        state,
                        command,
                        CreateContext())));

        return new MovementFixture(
            generatedWorld,
            state,
            source,
            destination,
            simulationEvent,
            new MilitaryMoveEventRevalidator(
                generatedWorld));
    }

    private static CommandId CreateCommandId(
        ulong sequence = 1UL)
    {
        return new CommandId(
            new TurnNumber(
                1UL),
            sequence);
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
        StrategicCellId destination,
        ulong sequence = 1UL)
    {
        return new MilitaryMoveCommand(
            CreateCommandId(
                sequence),
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

    private static IReadOnlyList<StrategicCellId> GetTwoDirectNeighbors(
        WorldGenerationResult generatedWorld,
        StrategicCellId source)
    {
        return generatedWorld
            .StrategicTopology
            .Cells[
                checked(
                    (int)source.Value
                    - 1)]
            .AdjacentCellIds
            .Take(
                2)
            .ToArray();
    }

    private static StrategicCellId GetDifferentCell(
        WorldGenerationResult generatedWorld,
        StrategicCellId first,
        StrategicCellId second)
    {
        return generatedWorld
            .StrategicTopology
            .Cells
            .Select(
                cell =>
                    cell.Id)
            .First(
                cellId =>
                    cellId != first
                    && cellId != second);
    }

    private static StrategicEdgeId GetConnectingEdge(
        WorldGenerationResult generatedWorld,
        StrategicCellId source,
        StrategicCellId destination)
    {
        var sourceCell =
            generatedWorld
                .StrategicTopology
                .Cells[
                    checked(
                        (int)source.Value
                        - 1)];

        foreach (var edgeId in sourceCell.IncidentEdgeIds)
        {
            var edge =
                generatedWorld
                    .StrategicTopology
                    .Edges[
                        checked(
                            (int)edgeId.Value
                            - 1)];

            if (edge.IncidentCellIds.Contains(
                destination))
            {
                return edge.Id;
            }
        }

        throw new InvalidOperationException(
            "Direct-neighbor test fixture has no connecting strategic edge.");
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

    private sealed record MovementFixture(
        WorldGenerationResult GeneratedWorld,
        WorldState State,
        StrategicCellId Source,
        StrategicCellId Destination,
        MilitaryMoveEvent Event,
        MilitaryMoveEventRevalidator Revalidator);
}