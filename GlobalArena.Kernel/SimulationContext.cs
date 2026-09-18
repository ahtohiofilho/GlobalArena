namespace GlobalArena.Kernel;

public readonly record struct SimulationContext(
    TurnNumber Turn,
    SimulationSeed Seed);
