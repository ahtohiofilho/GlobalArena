namespace GlobalArena.Runtime;

public sealed class BaselineDiplomacyRelationEntry
{
    public CivilizationId First { get; }

    public CivilizationId Second { get; }

    public BaselineDiplomacyRelationKind Relation { get; }

    public BaselineDiplomacyRelationEntry(
        CivilizationId first,
        CivilizationId second,
        BaselineDiplomacyRelationKind relation)
    {
        if (!first.IsValid)
        {
            throw new ArgumentException(
                "Diplomacy relation requires a valid first civilization identity.",
                nameof(first));
        }

        if (!second.IsValid)
        {
            throw new ArgumentException(
                "Diplomacy relation requires a valid second civilization identity.",
                nameof(second));
        }

        if (first == second)
        {
            throw new ArgumentException(
                "Diplomacy relation cannot persist a self relation.",
                nameof(second));
        }

        if (relation == BaselineDiplomacyRelationKind.Neutral)
        {
            throw new ArgumentException(
                "Neutral diplomacy is implicit and cannot be persisted as an override.",
                nameof(relation));
        }

        if (relation != BaselineDiplomacyRelationKind.Enemy
            && relation != BaselineDiplomacyRelationKind.Ally)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relation),
                "Diplomacy relation override must be Enemy or Ally.");
        }

        if (first.Value < second.Value)
        {
            First = first;
            Second = second;
        }
        else
        {
            First = second;
            Second = first;
        }

        Relation = relation;
    }
}
