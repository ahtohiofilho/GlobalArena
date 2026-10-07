using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class MilitaryMoveEventExecutor
    : ISimulationEventExecutor
{
    public WorldState Execute(
        WorldState worldState,
        ISimulationEvent simulationEvent,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(
            worldState);

        ArgumentNullException.ThrowIfNull(
            simulationEvent);

        if (simulationEvent is not MilitaryMoveEvent militaryMove)
        {
            throw new ArgumentException(
                "Military move executor supports MilitaryMoveEvent only.",
                nameof(simulationEvent));
        }

        var matchedUnitCount = 0;

        var updatedUnits =
            worldState
                .Warfare
                .Units
                .Select(
                    unit =>
                    {
                        if (unit.Id
                            != militaryMove.MilitaryUnitId)
                        {
                            return unit;
                        }

                        matchedUnitCount++;

                        if (unit.Owner
                            != militaryMove.IssuingCivilizationId)
                        {
                            throw new InvalidOperationException(
                                "Military move event owner no longer matches authoritative unit owner.");
                        }

                        if (unit.StrategicCellId
                            != militaryMove.ExpectedSourceStrategicCellId)
                        {
                            throw new InvalidOperationException(
                                "Military move event expected source no longer matches authoritative unit location.");
                        }

                        return new MilitaryUnitRuntimeState(
                            unit.Id,
                            unit.Owner,
                            militaryMove.DestinationStrategicCellId);
                    })
                .ToArray();

        if (matchedUnitCount != 1)
        {
            throw new InvalidOperationException(
                "Military move event must reference exactly one authoritative unit.");
        }

        var updatedWarfare =
            new WarfareRuntimeState(
                updatedUnits);

        return worldState
            .WithWarfare(
                updatedWarfare)
            .AdvanceRevision();
    }
}