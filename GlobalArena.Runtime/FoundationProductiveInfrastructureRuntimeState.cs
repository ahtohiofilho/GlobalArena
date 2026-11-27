using GlobalArena.World;

namespace GlobalArena.Runtime;

// Stable canonical activity codes, deliberately not commodity or crop IDs.
public enum FoundationProductiveActivity : uint
{
    Agriculture = 1U,
    Forestry = 2U,
    Mining = 3U
}

public readonly record struct FoundationProductiveTarget(
    PhysicalTacticalTileId Node, FoundationProductiveActivity Activity)
{
    public bool IsValid => Node.IsValid && Enum.IsDefined(Activity);
}

public sealed record FoundationProductiveReference(
    FoundationProductiveActivity Activity, ulong ConstructionMoneyMicro);

public sealed record FoundationProductiveConstructionProject(
    ulong Id, FoundationProductiveTarget Target, CivilizationId Investor,
    ulong FundedMoneyMicro, ulong RequiredWorkMicro, ulong CompletedWorkMicro,
    bool IsActivated = false)
{
    public bool IsComplete => CompletedWorkMicro == RequiredWorkMicro;
    public ulong RemainingWorkMicro => RequiredWorkMicro - CompletedWorkMicro;
}

public sealed record FoundationProductiveCapitalReading(
    FoundationProductiveTarget Target, CivilizationId? ReservedOwner,
    ulong ReferenceMoneyMicro, ulong ActivatedCapitalMoneyMicro,
    ulong PendingCapitalMoneyMicro);

/// <summary>
/// Immutable productive ledger foundation. Project IDs are local to this
/// ledger until a typed Build target is integrated. No world funding or
/// labor authority is implied by constructing a detached ledger.
/// </summary>
public sealed class FoundationProductiveInfrastructureRuntimeState
{
    public static FoundationProductiveInfrastructureRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<FoundationProductiveReference>(),
            Array.Empty<FoundationProductiveConstructionProject>());

    public bool IsInitialized { get; }
    public IReadOnlyList<FoundationProductiveReference> References { get; }
    public IReadOnlyList<FoundationProductiveConstructionProject> Projects { get; }

    private readonly IReadOnlyDictionary<FoundationProductiveActivity, ulong> _references;
    private readonly IReadOnlyDictionary<FoundationProductiveTarget, FoundationProductiveCapitalReading> _capital;

    public static FoundationProductiveInfrastructureRuntimeState CreateEmpty(
        IEnumerable<FoundationProductiveReference> references) =>
        new(true, (references ?? throw new ArgumentNullException(nameof(references))).ToArray(),
            Array.Empty<FoundationProductiveConstructionProject>());

    private FoundationProductiveInfrastructureRuntimeState(bool initialized,
        FoundationProductiveReference[] references, FoundationProductiveConstructionProject[] projects)
    {
        if (initialized && (references.Any(r => r is null || !Enum.IsDefined(r.Activity) ||
                r.ConstructionMoneyMicro == 0UL) ||
            references.Length != Enum.GetValues<FoundationProductiveActivity>().Length ||
            references.Select(r => r.Activity).Distinct().Count() != references.Length))
            throw new ArgumentException("Every productive activity needs one positive construction reference.");
        _references = references.ToDictionary(r => r.Activity, r => r.ConstructionMoneyMicro);
        if (projects.Any(p => p is null || p.Id == 0UL || !p.Target.IsValid || !p.Investor.IsValid ||
            !_references.ContainsKey(p.Target.Activity) || p.FundedMoneyMicro == 0UL ||
            p.RequiredWorkMicro == 0UL || p.CompletedWorkMicro > p.RequiredWorkMicro ||
            (p.IsActivated && !p.IsComplete)) ||
            projects.Select(p => p.Id).Distinct().Count() != projects.Length)
            throw new ArgumentException("Invalid productive construction history.");
        if (projects.Select(p => p.Target.Node.FineGoldbergParameters).Distinct().Count() > 1)
            throw new ArgumentException("Productive projects cannot mix fine worlds.");
        var capital = new Dictionary<FoundationProductiveTarget, FoundationProductiveCapitalReading>();
        foreach (var group in projects.GroupBy(p => p.Target))
        {
            if (group.Select(p => p.Investor).Distinct().Count() != 1 ||
                group.Count(p => !p.IsActivated) > 1)
                throw new InvalidOperationException("Productive target requires exclusive title and a single pending project.");
            ulong active = 0UL, pending = 0UL;
            foreach (var p in group)
                if (p.IsActivated) active = checked(active + p.FundedMoneyMicro);
                else pending = checked(pending + p.FundedMoneyMicro);
            _ = checked(active + pending); // never reserve unrepresentable capital
            capital.Add(group.Key, new(group.Key, group.First().Investor,
                _references[group.Key.Activity], active, pending));
        }
        _capital = capital;
        IsInitialized = initialized;
        References = Array.AsReadOnly(references.OrderBy(r => r.Activity).ToArray());
        Projects = Array.AsReadOnly(projects.OrderBy(p => p.Id).ToArray());
    }

    public FoundationProductiveCapitalReading Read(FoundationProductiveTarget target)
    {
        if (!IsInitialized || !target.IsValid || !_references.TryGetValue(target.Activity, out var reference))
            throw new InvalidOperationException("Productive reading requires a deployed activity reference and valid target.");
        return _capital.TryGetValue(target, out var reading) ? reading :
            new(target, null, reference, 0UL, 0UL);
    }

    // These are pure detached ledger operations. WorldState does not expose
    // them as gameplay commands; the later financial/temporal event gate
    // must independently authorize funding, work and commissioning.
    public FoundationProductiveInfrastructureRuntimeState Reserve(
        FoundationProductiveTarget target, CivilizationId investor, ulong moneyMicro, ulong requiredWorkMicro)
    {
        var reading = Read(target);
        if (!investor.IsValid || moneyMicro == 0UL || requiredWorkMicro == 0UL ||
            reading.PendingCapitalMoneyMicro != 0UL ||
            (reading.ReservedOwner.HasValue && reading.ReservedOwner.Value != investor))
            throw new InvalidOperationException("Productive reservation requires available own or natural target.");
        ulong id = Projects.Count == 0 ? 1UL : checked(Projects[^1].Id + 1UL);
        return Copy(Projects.Append(new(id, target, investor, moneyMicro, requiredWorkMicro, 0UL)));
    }

    public FoundationProductiveInfrastructureRuntimeState RecordWork(ulong projectId, ulong workMicro)
    {
        var project = Projects.SingleOrDefault(p => p.Id == projectId);
        if (project is null || project.IsActivated || workMicro == 0UL || workMicro > project.RemainingWorkMicro)
            throw new InvalidOperationException("Productive work must advance a pending project within its remaining effort.");
        return Copy(Projects.Select(p => p.Id == projectId
            ? p with { CompletedWorkMicro = checked(p.CompletedWorkMicro + workMicro) } : p));
    }

    public FoundationProductiveInfrastructureRuntimeState Activate(ulong projectId)
    {
        var project = Projects.SingleOrDefault(p => p.Id == projectId);
        if (project is null || project.IsActivated || !project.IsComplete)
            throw new InvalidOperationException("Only completed pending productive capital may activate.");
        return Copy(Projects.Select(p => p.Id == projectId ? p with { IsActivated = true } : p));
    }

    private FoundationProductiveInfrastructureRuntimeState Copy(
        IEnumerable<FoundationProductiveConstructionProject> projects) =>
        new(true, References.ToArray(), projects.ToArray());
}
