using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed record FoundationConstructionProject(
    ulong Id, PhysicalTacticalEdgeId Edge, CivilizationId Investor,
    ulong FundedMoneyMicro, ulong RequiredWorkMicro, ulong CompletedWorkMicro, bool IsActivated = false)
{
    public ulong RemainingWorkMicro => RequiredWorkMicro - CompletedWorkMicro;
    public bool IsComplete => CompletedWorkMicro == RequiredWorkMicro;
}

/// <summary>Opt-in snapshot ledger. Recording a project is not a treasury
/// debit or infrastructure activation; those require a later event executor.</summary>
public sealed class FoundationConstructionRuntimeState
{
    public static FoundationConstructionRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<FoundationConstructionProject>());
    public bool IsInitialized { get; }
    public IReadOnlyList<FoundationConstructionProject> Projects { get; }

    public FoundationConstructionRuntimeState(IEnumerable<FoundationConstructionProject> projects)
        : this(true, (projects ?? throw new ArgumentNullException(nameof(projects))).ToArray()) { }

    private FoundationConstructionRuntimeState(bool initialized, FoundationConstructionProject[] projects)
    {
        if (projects.Any(p => p is null || p.Id == 0UL || !p.Edge.IsValid ||
            !p.Investor.IsValid || p.FundedMoneyMicro == 0UL ||
            p.RequiredWorkMicro == 0UL || p.CompletedWorkMicro > p.RequiredWorkMicro ||
            (p.IsActivated && !p.IsComplete)))
            throw new ArgumentException("Invalid construction project.", nameof(projects));
        if (projects.Select(p => p.Id).Distinct().Count() != projects.Length)
            throw new ArgumentException("Duplicate project ID.", nameof(projects));
        if (projects.Where(p => !p.IsActivated).GroupBy(p => p.Edge).Any(g => g.Count() > 1))
            throw new ArgumentException("Only one unactivated project per physical edge.", nameof(projects));
        IsInitialized = initialized;
        Projects = Array.AsReadOnly(projects.OrderBy(p => p.Id).ToArray());
    }
}
