namespace GlobalArena.Runtime;

/// <summary>
/// Explicitly supplied initial total workforce for one materialized civilization.
/// This is a workforce accounting input, not total population or a growth rule.
/// A zero budget is valid when a civilization starts with no workers.
/// </summary>
public sealed class InitialCivilizationWorkforceBudget
{
    public CivilizationId CivilizationId { get; }

    public ulong Workforce { get; }

    public InitialCivilizationWorkforceBudget(
        CivilizationId civilizationId,
        ulong workforce)
    {
        if (!civilizationId.IsValid)
            throw new ArgumentException(
                "Initial workforce budget requires a valid civilization.",
                nameof(civilizationId));

        CivilizationId = civilizationId;
        Workforce = workforce;
    }
}
