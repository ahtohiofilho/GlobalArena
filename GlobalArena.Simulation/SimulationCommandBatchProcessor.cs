using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class SimulationCommandBatchProcessor
    : ISimulationCommandBatchProcessor
{
    private readonly ISimulationCommandValidator _commandValidator;
    private readonly ISimulationCommandProcessor _commandProcessor;

    public SimulationCommandBatchProcessor(
        ISimulationCommandValidator commandValidator,
        ISimulationCommandProcessor commandProcessor)
    {
        ArgumentNullException.ThrowIfNull(commandValidator);
        ArgumentNullException.ThrowIfNull(commandProcessor);

        _commandValidator = commandValidator;
        _commandProcessor = commandProcessor;
    }

    public IReadOnlyList<ISimulationEvent> BuildEvents(
        WorldState planningWorldState,
        IReadOnlyList<ISimulationCommand> commands,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(planningWorldState);
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0)
        {
            return Array.Empty<ISimulationEvent>();
        }

        foreach (var command in commands)
        {
            if (command is null)
            {
                throw new ArgumentException(
                    "Command collection cannot contain null.",
                    nameof(commands));
            }
        }

        var canonicalCommands = commands
            .OrderBy(command => command.Id.Turn.Value)
            .ThenBy(command => command.Id.Sequence)
            .ToArray();

        for (var index = 1;
             index < canonicalCommands.Length;
             index++)
        {
            if (canonicalCommands[index - 1].Id
                == canonicalCommands[index].Id)
            {
                throw new InvalidOperationException(
                    "Duplicate CommandId values are not supported.");
            }
        }

        var aggregatedEvents =
            new List<ISimulationEvent>();

        foreach (var command in canonicalCommands)
        {
            var isValid = _commandValidator.IsValid(
                planningWorldState,
                command,
                context);

            if (!isValid)
            {
                continue;
            }

            var commandEvents = _commandProcessor.Process(
                planningWorldState,
                command,
                context);

            ArgumentNullException.ThrowIfNull(commandEvents);

            aggregatedEvents.AddRange(
                commandEvents);
        }

        return Array.AsReadOnly(
            aggregatedEvents.ToArray());
    }
}
