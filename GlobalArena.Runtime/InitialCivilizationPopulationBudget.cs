namespace GlobalArena.Runtime;

/// <summary>
/// Caller-supplied total resident population for one materialized civilization.
/// This is an initial census input, not a population generation or growth rule.
/// </summary>
public sealed class InitialCivilizationPopulationBudget
{
    public CivilizationId CivilizationId { get; }

    public ulong Residents { get; }

    public InitialCivilizationPopulationBudget(
        CivilizationId civilizationId,
        ulong residents)
    {
        if (!civilizationId.IsValid)
            throw new ArgumentException(
                "Initial population budget requires a valid civilization identity.",
                nameof(civilizationId));

        CivilizationId = civilizationId;
        Residents = residents;
    }
}
