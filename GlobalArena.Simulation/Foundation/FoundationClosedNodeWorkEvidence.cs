using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Server-only evidence, not a boarding authorization. Coverage is
/// elapsed time; work is additive worker-time at current equal effort.
/// Eligibility is supplied by the future diplomatic/title policy.</summary>
internal sealed record FoundationClosedNodeWorkEvidence(
    uint CoverageMicro, ulong OwnWorkMicro, ulong EligibleWorkMicro)
{
    internal const uint WholeTurnMicro = 1_000_000U;
    internal bool HasFullCoverage => CoverageMicro == WholeTurnMicro;
    internal ulong ForeignWorkMicro => EligibleWorkMicro - OwnWorkMicro;
}

internal static class FoundationClosedNodeWorkProjector
{
    internal static FoundationClosedNodeWorkEvidence Read(
        IReadOnlyList<FoundationPersistentWorkerSlice> slices,
        IReadOnlyDictionary<PhysicalWorkerId, CivilizationId> owners,
        PhysicalTacticalTileId node, CivilizationId requester,
        IReadOnlySet<CivilizationId> eligibleOwners)
    {
        ArgumentNullException.ThrowIfNull(slices);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(eligibleOwners);
        if (!node.IsValid || !requester.IsValid || !eligibleOwners.Contains(requester))
            throw new ArgumentException("Valid node and own eligibility required.");
        return Read(Capture(slices, owners), node, requester, eligibleOwners);
    }

    internal static FoundationPriorNodeWork[] Capture(
        IReadOnlyList<FoundationPersistentWorkerSlice> slices,
        IReadOnlyDictionary<PhysicalWorkerId, CivilizationId> owners)
    {
        return slices.Where(IsEconomicWork).Select(slice =>
        {
            if (!owners.TryGetValue(slice.Worker, out var owner))
                throw new InvalidOperationException("Economic work has no worker owner.");
            return new FoundationPriorNodeWork(slice.Node, owner, slice.StartMicro, slice.DurationMicro);
        }).ToArray();
    }

    internal static FoundationClosedNodeWorkEvidence Read(
        IReadOnlyList<FoundationPriorNodeWork> work, PhysicalTacticalTileId node,
        CivilizationId requester, IReadOnlySet<CivilizationId> eligibleOwners)
    {
        ulong total = 0, own = 0;
        var intervals = new List<(uint Start, uint End)>();
        foreach (var slice in work)
        {
            if (slice.Node != node || !eligibleOwners.Contains(slice.Owner)) continue;
            if (slice.DurationMicro == 0 || slice.StartMicro >= FoundationClosedNodeWorkEvidence.WholeTurnMicro ||
                slice.DurationMicro > FoundationClosedNodeWorkEvidence.WholeTurnMicro - slice.StartMicro)
                throw new InvalidOperationException("Invalid closed work interval.");
            total = checked(total + slice.DurationMicro);
            if (slice.Owner == requester) own = checked(own + slice.DurationMicro);
            intervals.Add((slice.StartMicro, slice.StartMicro + slice.DurationMicro));
        }
        uint coverage = 0, start = 0, end = 0;
        foreach (var interval in intervals.OrderBy(i => i.Start).ThenBy(i => i.End))
        {
            if (interval.Start > end)
            {
                coverage += end - start;
                start = interval.Start;
                end = interval.End;
            }
            else end = Math.Max(end, interval.End);
        }
        coverage += end - start;
        return new(coverage, own, total);
    }

    private static bool IsEconomicWork(FoundationPersistentWorkerSlice slice)
    {
        if (slice.Activity is PhysicalWorkerActivity.Traveling or PhysicalWorkerActivity.Military)
            return false;
        return slice.ProjectId.HasValue ||
            (slice.Activity == PhysicalWorkerActivity.Producing &&
                slice.Occupation is PhysicalWorkerOccupation.Agriculture or PhysicalWorkerOccupation.Forestry or PhysicalWorkerOccupation.Mining) ||
            (slice.Activity == PhysicalWorkerActivity.Other &&
                slice.Occupation is PhysicalWorkerOccupation.LandTransport or PhysicalWorkerOccupation.PortOperations);
    }
}
