namespace GlobalArena.Runtime;

public sealed class BaselineDiplomacyRuntimeState
{
    private static readonly BaselineDiplomacyRuntimeState EmptyInstance =
        new(
            Array.Empty<BaselineDiplomacyRelationEntry>());

    private readonly BaselineDiplomacyRelationEntry[] _relations;

    private readonly IReadOnlyList<BaselineDiplomacyRelationEntry> _readOnlyRelations;

    private readonly Dictionary<
        (CivilizationId First, CivilizationId Second),
        BaselineDiplomacyRelationKind> _relationsByPair;

    public static BaselineDiplomacyRuntimeState Empty =>
        EmptyInstance;

    public IReadOnlyList<BaselineDiplomacyRelationEntry> Relations =>
        _readOnlyRelations;

    public BaselineDiplomacyRuntimeState(
        IEnumerable<BaselineDiplomacyRelationEntry> relations)
    {
        ArgumentNullException.ThrowIfNull(
            relations);

        var canonical =
            relations.ToArray();

        if (canonical.Any(
            relation =>
                relation is null))
        {
            throw new ArgumentException(
                "Diplomacy relation state cannot contain null entries.",
                nameof(relations));
        }

        canonical =
            canonical
                .OrderBy(
                    relation =>
                        relation.First.Value)
                .ThenBy(
                    relation =>
                        relation.Second.Value)
                .ToArray();

        for (var index = 1;
             index < canonical.Length;
             index++)
        {
            var previous =
                canonical[index - 1];

            var current =
                canonical[index];

            if (previous.First == current.First
                && previous.Second == current.Second)
            {
                throw new ArgumentException(
                    "Diplomacy relation state cannot contain duplicate civilization pairs.",
                    nameof(relations));
            }
        }

        _relations =
            canonical;

        _readOnlyRelations =
            Array.AsReadOnly(
                _relations);

        _relationsByPair =
            canonical.ToDictionary(
                relation =>
                    (
                        relation.First,
                        relation.Second
                    ),
                relation =>
                    relation.Relation);
    }

    public BaselineDiplomacyRelationKind GetRelation(
        CivilizationId first,
        CivilizationId second)
    {
        ValidateCivilization(
            first,
            nameof(first));

        ValidateCivilization(
            second,
            nameof(second));

        if (first == second)
        {
            return BaselineDiplomacyRelationKind.Neutral;
        }

        var pair =
            CanonicalizePair(
                first,
                second);

        if (_relationsByPair.TryGetValue(
            pair,
            out var relation))
        {
            return relation;
        }

        return BaselineDiplomacyRelationKind.Neutral;
    }

    public BaselineDiplomacyRuntimeState WithRelation(
        CivilizationId first,
        CivilizationId second,
        BaselineDiplomacyRelationKind relation)
    {
        ValidateCivilization(
            first,
            nameof(first));

        ValidateCivilization(
            second,
            nameof(second));

        if (first == second)
        {
            throw new ArgumentException(
                "Diplomacy mutation cannot target a self relation.",
                nameof(second));
        }

        if (relation != BaselineDiplomacyRelationKind.Neutral
            && relation != BaselineDiplomacyRelationKind.Enemy
            && relation != BaselineDiplomacyRelationKind.Ally)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relation),
                "Diplomacy relation must be Neutral, Enemy or Ally.");
        }

        var pair =
            CanonicalizePair(
                first,
                second);

        var updated =
            _relations
                .Where(
                    existing =>
                        existing.First != pair.First
                        || existing.Second != pair.Second)
                .ToList();

        if (relation != BaselineDiplomacyRelationKind.Neutral)
        {
            updated.Add(
                new BaselineDiplomacyRelationEntry(
                    pair.First,
                    pair.Second,
                    relation));
        }

        if (updated.Count == 0)
        {
            return Empty;
        }

        return new BaselineDiplomacyRuntimeState(
            updated);
    }

    private static (
        CivilizationId First,
        CivilizationId Second
    ) CanonicalizePair(
        CivilizationId first,
        CivilizationId second)
    {
        if (first.Value < second.Value)
        {
            return (
                first,
                second
            );
        }

        return (
            second,
            first
        );
    }

    private static void ValidateCivilization(
        CivilizationId civilization,
        string parameterName)
    {
        if (!civilization.IsValid)
        {
            throw new ArgumentException(
                "Diplomacy lookup requires a valid civilization identity.",
                parameterName);
        }
    }
}
