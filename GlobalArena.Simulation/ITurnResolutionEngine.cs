using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A headless turn-resolution engine. The current TurnResolver is one implementation;
/// future phased resolvers may process periodic strategic economy and simultaneous
/// orders without depending on command-count or on Godot presentation.
/// Implementations must return a complete authoritative resolution result.
/// </summary>
public interface ITurnResolutionEngine
{
    TurnResolutionResult Resolve(TurnResolutionInput input);
}
