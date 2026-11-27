using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

internal enum FoundationNavalBoardingStatus
{
    Allowed,
    InvalidCoastalCrossing,
    UnknownConnection,
    HostileInfrastructure,
    UnownedInfrastructure,
    NoPriorCalendar,
    InsufficientCoverage
}

/// <summary>Authorization and exact paid fraction, not a freight quote or
/// movement command. No foreign workforce counts or provider revenue.</summary>
internal sealed record FoundationNavalBoardingDecision(
    FoundationNavalBoardingStatus Status, ulong PaidNumerator = 0, ulong PaidDenominator = 1)
{
    internal bool IsAllowed => Status == FoundationNavalBoardingStatus.Allowed;
}

internal static class FoundationNavalBoardingPolicy
{
    internal static FoundationNavalBoardingDecision Evaluate(
        WorldState world, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        CivilizationId requester, PhysicalTacticalTileId origin, PhysicalTacticalTileId water,
        Func<IReadOnlySet<CivilizationId>, FoundationClosedNodeWorkEvidence?> readPriorWork)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(readPriorWork);
        if (!world.Civilizations.Civilizations.Contains(requester))
            throw new ArgumentException("Unknown boarding civilization.", nameof(requester));
        var fine = incidence.Refinement.FineParameters;
        if (geography.FineParameters != fine || geography.CoarseParameters != incidence.Refinement.CoarseParameters ||
            !world.Transport.IsInitialized)
            throw new InvalidOperationException("Boarding needs signed physical transport.");
        if (!origin.IsValid || !water.IsValid || origin.FineGoldbergParameters != fine ||
            water.FineGoldbergParameters != fine || origin == water ||
            origin.FineStrategicCellId.Value > (ulong)incidence.FineTopology.Cells.Count ||
            water.FineStrategicCellId.Value > (ulong)incidence.FineTopology.Cells.Count)
            return new(FoundationNavalBoardingStatus.InvalidCoastalCrossing);
        int a = checked((int)origin.FineStrategicCellId.Value - 1);
        int b = checked((int)water.FineStrategicCellId.Value - 1);
        if (geography.SurfaceAtIndex(a) != FoundationFineSurfaceKind.Land ||
            !incidence.FineTopology.Cells[a].AdjacentCellIds.Contains(water.FineStrategicCellId) ||
            geography.CrossingAtIndices(a, b) != FoundationFineTransportCrossingKind.Port)
            return new(FoundationNavalBoardingStatus.InvalidCoastalCrossing);
        var geometric = incidence.FineTopology.Edges.Single(e =>
            e.IncidentCellIds.Contains(origin.FineStrategicCellId) &&
            e.IncidentCellIds.Contains(water.FineStrategicCellId));
        var edge = new PhysicalTacticalEdgeId(fine, geometric.Id);
        var row = world.Transport.Edges.Single(e => e.Edge == edge);
        if (row.OwnershipKind == PhysicalTransportOwnershipKind.Owned)
        {
            var owner = row.InfrastructureOwner!.Value;
            if (owner == requester) return new(FoundationNavalBoardingStatus.Allowed);
            return world.Diplomacy.GetRelation(requester, owner) == BaselineDiplomacyRelationKind.Enemy
                ? new(FoundationNavalBoardingStatus.HostileInfrastructure)
                : new(FoundationNavalBoardingStatus.Allowed, 1, 1);
        }
        if (row.OwnershipKind != PhysicalTransportOwnershipKind.NoInfrastructure)
            return new(FoundationNavalBoardingStatus.UnownedInfrastructure);

        // Funded but unactivated projects are not infrastructure. The
        // natural crossing still requires the prior actual economic calendar.
        var eligible = world.Civilizations.Civilizations.Where(c => c == requester ||
            world.Diplomacy.GetRelation(requester, c) != BaselineDiplomacyRelationKind.Enemy).ToHashSet();
        var evidence = readPriorWork(eligible);
        if (evidence is null) return new(FoundationNavalBoardingStatus.NoPriorCalendar);
        if (!evidence.HasFullCoverage || evidence.EligibleWorkMicro == 0)
            return new(FoundationNavalBoardingStatus.InsufficientCoverage);
        if (evidence.OwnWorkMicro > evidence.EligibleWorkMicro)
            throw new InvalidOperationException("Invalid work share.");
        return new(FoundationNavalBoardingStatus.Allowed,
            evidence.ForeignWorkMicro, evidence.EligibleWorkMicro);
    }
}
