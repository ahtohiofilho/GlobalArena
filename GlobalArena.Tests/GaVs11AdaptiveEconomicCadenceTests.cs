using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs11AdaptiveEconomicCadenceTests
{
    private static CanonicalPhysicalTacticalSession NewAdaptive() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty);

    [Fact]
    public void AdaptiveIntervalsHaveExactThirtyTurnBoundaries()
    {
        var policy = PhysicalEconomicCadenceMode.AdaptiveThirty;
        Assert.Equal(1UL, PhysicalEconomicCadence.IntervalForTurn(1UL, policy));
        Assert.Equal(1UL, PhysicalEconomicCadence.IntervalForTurn(30UL, policy));
        Assert.Equal(2UL, PhysicalEconomicCadence.IntervalForTurn(31UL, policy));
        Assert.Equal(2UL, PhysicalEconomicCadence.IntervalForTurn(60UL, policy));
        Assert.Equal(3UL, PhysicalEconomicCadence.IntervalForTurn(61UL, policy));
        Assert.Equal(3UL, PhysicalEconomicCadence.IntervalForTurn(90UL, policy));
        Assert.Equal(4UL, PhysicalEconomicCadence.IntervalForTurn(91UL, policy));
        Assert.Equal(31UL, PhysicalEconomicCadence.IntervalForTurn(901UL, policy));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalEconomicCadence.IntervalForTurn(0UL, policy));
    }

    [Fact]
    public void ProgressiveScheduleSettlesEveryTurnThenEverySecondThenEveryThird()
    {
        var game = NewAdaptive();
        Assert.Equal(PhysicalEconomicCadenceMode.AdaptiveThirty, game.CadenceMode);
        Assert.Equal(10U, game.CurrentHash.FormatVersion);
        Assert.Equal(1UL, game.NextEconomicInterval);

        for (var turn = 1; turn <= 90; turn++)
        {
            var cycle = game.AdvanceMovementTurn();
            var expected = turn <= 30 || (turn >= 32 && turn <= 60 && turn % 2 == 0) ||
                (turn >= 63 && turn <= 90 && turn % 3 == 0);
            Assert.Equal(expected, cycle is not null);
            if (expected) Assert.Equal((ulong)turn, cycle!.MovementTurn);
            if (turn == 30) Assert.Equal(30, game.State.Cycles.Count);
            if (turn == 31) Assert.Equal(30, game.State.Cycles.Count);
            if (turn == 60) Assert.Equal(45, game.State.Cycles.Count);
            if (turn == 90) Assert.Equal(55, game.State.Cycles.Count);
        }
        Assert.Equal(91UL, game.Game.Snapshot.Revision);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(4UL, game.NextEconomicInterval);
        Assert.Equal(10U, new CanonicalWorldStateHasher().Compute(
            game.Game.Snapshot).FormatVersion);
    }

    [Fact]
    public void CadenceContinuesAfterIntervalsExceedThirtyTurns()
    {
        var policy = PhysicalEconomicCadenceMode.AdaptiveThirty;
        ulong last = 0UL;
        var due = new List<ulong>();
        for (ulong turn = 1UL; turn <= 1200UL; turn++)
        {
            if (PhysicalEconomicCadence.IsSettlementTurn(turn, last, policy))
            {
                due.Add(turn);
                last = turn;
            }
        }
        Assert.Contains(due, x => x > 930UL);
        Assert.True(due[^1] > 1100UL);
        Assert.Equal(1UL, due[0]);
        Assert.Equal(30UL, due[29]);
        Assert.Equal(32UL, due[30]);
    }

    [Fact]
    public void AdaptiveV10SaveReopensAndContinuesWithIdenticalHash()
    {
        var game = NewAdaptive();
        var origin = game.InitialTile;
        var target = game.AdjacentWorkable(origin)[0];
        game.MoveWorkers(origin, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);
        for (var t = 0; t < 67; t++) game.AdvanceMovementTurn();
        var save = game.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!;
        Assert.Equal(CanonicalPhysicalTacticalSession.AdaptiveSaveFormat, envelope.Version);
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(game.Events, reopened.Events);
        Assert.Equal(PhysicalEconomicCadenceMode.AdaptiveThirty, reopened.CadenceMode);
        Assert.Equal(67UL, reopened.State.MovementTurn);
        for (var t = 0; t < 24; t++)
        {
            var a = game.AdvanceMovementTurn();
            var b = reopened.AdvanceMovementTurn();
            Assert.Equal(a, b);
        }
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(game.ExportSaveJson(), reopened.ExportSaveJson());
    }

    [Fact]
    public void LegacyV9SavesAndTheirSixTurnCadenceStayUntouched()
    {
        var legacy = CanonicalPhysicalTacticalSession.Start();
        Assert.Equal(PhysicalEconomicCadenceMode.LegacyFixedSix, legacy.CadenceMode);
        Assert.Equal(9U, legacy.CurrentHash.FormatVersion);
        for (var turn = 1; turn <= 12; turn++)
        {
            var cycle = legacy.AdvanceMovementTurn();
            Assert.Equal(turn % 6 == 0, cycle is not null);
        }
        var payload = legacy.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(payload)!;
        Assert.Equal(9, envelope.Version);
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(payload);
        Assert.Equal(PhysicalEconomicCadenceMode.LegacyFixedSix, reopened.CadenceMode);
        Assert.Equal(6UL, reopened.NextEconomicInterval);
        Assert.Equal(payload, reopened.ExportSaveJson());
        Assert.Equal(legacy.CurrentHash, reopened.CurrentHash);
    }

    [Fact]
    public void WrongAdaptiveCycleTimelineIsRejectedRatherThanReinterpreted()
    {
        var game = NewAdaptive();
        var initial = game.State;
        Assert.Throws<ArgumentException>(() => new PhysicalTacticalEconomyRuntimeState(
            initial.CoarseWorldBinding, initial.CoarseParameters,
            initial.FineParameters, 1UL, initial.WorkerRevision,
            initial.Allocations, initial.Stocks,
            Array.Empty<PhysicalEconomicCycle>(),
            PhysicalEconomicCadenceMode.AdaptiveThirty));
        Assert.Throws<ArgumentException>(() => new PhysicalTacticalEconomyRuntimeState(
            initial.CoarseWorldBinding, initial.CoarseParameters,
            initial.FineParameters, 31UL, initial.WorkerRevision,
            initial.Allocations, initial.Stocks,
            Enumerable.Range(1, 31).Select(i =>
                new PhysicalEconomicCycle((ulong)i, 0UL, 0UL, 0UL)),
            PhysicalEconomicCadenceMode.AdaptiveThirty));
    }
}
