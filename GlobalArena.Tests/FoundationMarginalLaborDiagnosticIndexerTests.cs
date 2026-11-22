using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationMarginalLaborDiagnosticIndexerTests
{
    private static readonly GoldbergParameters Fine = new(18, 0);

    private static FoundationMarginalLaborObservation Transfer(
        ulong worker, ulong tile, long money, ulong owner = 1UL) =>
        new("world-seed:20261012/revision:8",
            FoundationMarginalLaborChangeKind.ExistingWorkerTransfer,
            new CivilizationId(owner),
            new PhysicalTacticalTileId(Fine, new StrategicCellId(tile)),
            PhysicalWorkerOccupation.LandTransport,
            new BigInteger(money), new PhysicalWorkerId(worker),
            new PhysicalTacticalTileId(Fine, new StrategicCellId(178UL)));

    [Fact]
    public void SortedDiagnosticsExposeExtremesWithCanonicalTiesIndependentOfInputOrder()
    {
        var observations = new[]
        {
            Transfer(4, 201, 10),
            Transfer(1, 178, -30),
            Transfer(3, 190, 90),
            Transfer(2, 180, -30),
            Transfer(5, 220, 0)
        };
        var a = FoundationMarginalLaborDiagnosticIndexer.Rank(observations, 2);
        var b = FoundationMarginalLaborDiagnosticIndexer.Rank(
            observations.Reverse().ToArray(), 2);

        Assert.Equal(5, a.EvaluatedCandidates);
        Assert.False(a.IsCommitted);
        Assert.Equal(new[] { 1UL, 2UL },
            a.LowestReturns.Select(x => x.ExistingWorkerId!.Value.Value));
        Assert.Equal(new[] { 3UL, 4UL },
            a.HighestOpportunities.Select(x => x.ExistingWorkerId!.Value.Value));
        Assert.Equal(a.LowestReturns, b.LowestReturns);
        Assert.Equal(a.HighestOpportunities, b.HighestOpportunities);
        Assert.Equal("world-seed:20261012/revision:8", a.BaselineIdentity);
    }

    [Fact]
    public void GrowthIsSeparateAndDoesNotWithdrawAnExistingWorker()
    {
        var additions = new[]
        {
            new FoundationMarginalLaborObservation("same-population-baseline",
                FoundationMarginalLaborChangeKind.NewWorkerAddition,
                new CivilizationId(1UL),
                new PhysicalTacticalTileId(Fine, new StrategicCellId(178UL)),
                PhysicalWorkerOccupation.Agriculture, new BigInteger(30)),
            new FoundationMarginalLaborObservation("same-population-baseline",
                FoundationMarginalLaborChangeKind.NewWorkerAddition,
                new CivilizationId(2UL),
                new PhysicalTacticalTileId(Fine, new StrategicCellId(251UL)),
                PhysicalWorkerOccupation.Mining, new BigInteger(40))
        };
        var result = FoundationMarginalLaborDiagnosticIndexer.Rank(additions, 10);
        Assert.Equal(FoundationMarginalLaborChangeKind.NewWorkerAddition,
            result.ChangeKind);
        Assert.Equal(2, result.HighestOpportunities.Count);
        Assert.Equal(2UL, result.HighestOpportunities[0].Owner.Value);
        Assert.All(result.HighestOpportunities,
            x => Assert.Null(x.ExistingWorkerId));

        var mixed = additions.Cast<FoundationMarginalLaborObservation>()
            .Append(Transfer(1, 190, 50)).ToArray();
        Assert.Throws<ArgumentException>(() =>
            FoundationMarginalLaborDiagnosticIndexer.Rank(mixed, 3));
        Assert.Throws<ArgumentException>(() =>
            FoundationMarginalLaborDiagnosticIndexer.Rank(
                new[] { additions[0] with
                { ExistingWorkerId = new PhysicalWorkerId(9UL) } }, 1));
    }

    [Fact]
    public void RejectsIncomparableBaselineDuplicatesAndNonexistentEvidence()
    {
        var transfer = Transfer(1, 178, -100);
        Assert.Throws<ArgumentException>(() =>
            FoundationMarginalLaborDiagnosticIndexer.Rank(
                new[] { transfer, transfer with
                { BaselineIdentity = "other-state" } }, 2));
        Assert.Throws<ArgumentException>(() =>
            FoundationMarginalLaborDiagnosticIndexer.Rank(
                new[] { transfer, transfer with { CashDeltaScaled = 300 } }, 2));
        Assert.Throws<ArgumentException>(() =>
            FoundationMarginalLaborDiagnosticIndexer.Rank(
                new[] { transfer with { SourceTile = null } }, 1));
        Assert.Throws<ArgumentException>(() =>
            FoundationMarginalLaborDiagnosticIndexer.Rank(
                Array.Empty<FoundationMarginalLaborObservation>(), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationMarginalLaborDiagnosticIndexer.Rank(new[] { transfer }, 0));
    }

    [Fact]
    public void ExistingCounterfactualNetDeltaCanBePresentedWithoutPretendingItIsAnEntry()
    {
        var comparison = new[]
        {
            Transfer(1, 178, -1_088_085_468_527L),
            Transfer(2, 251, -719_982_000_000L),
        };
        var result = FoundationMarginalLaborDiagnosticIndexer.Rank(comparison, 1);
        Assert.Equal(2UL,
            result.HighestOpportunities[0].ExistingWorkerId!.Value.Value);
        Assert.Equal(1UL,
            result.LowestReturns[0].ExistingWorkerId!.Value.Value);
        Assert.Equal(FoundationMarginalLaborChangeKind.ExistingWorkerTransfer,
            result.ChangeKind);
        // Ranking already EVALUATED deltas does not run a worker x city search.
    }
}
