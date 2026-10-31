using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs22EconomicCycleProgressionTests
{
    private static CanonicalPhysicalTacticalSession Start(bool born = true) =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            born ? PhysicalCivilizationIdentityMode.BornWithPopulation
                : PhysicalCivilizationIdentityMode.LegacyUnattributed);

    [Fact]
    public void ReadOnlyPreviewUsesTheExistingCadenceAcrossThirtyTurnBoundary()
    {
        var game = Start();
        var hash = game.CurrentHash;
        var save = game.ExportSaveJson();
        var first = PhysicalEconomicCycleProgression.Preview(game);
        Assert.Equal(0UL, first.CurrentMovementTurn);
        Assert.Equal(1UL, first.SettlementTurn);
        Assert.Equal(1UL, first.MovementTurnsToAdvance);
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Empty(game.Events);

        for (var i = 0; i < 30; i++)
            Assert.NotNull(game.AdvanceMovementTurn());
        var boundaryHash = game.CurrentHash;
        var boundarySave = game.ExportSaveJson();
        var next = PhysicalEconomicCycleProgression.Preview(game);
        Assert.Equal(30UL, next.CurrentMovementTurn);
        Assert.Equal(32UL, next.SettlementTurn);
        Assert.Equal(2UL, next.MovementTurnsToAdvance);
        Assert.Equal(boundaryHash, game.CurrentHash);
        Assert.Equal(boundarySave, game.ExportSaveJson());

        Assert.Null(game.AdvanceMovementTurn()); // Turn 31 is not a settlement.
        var afterOneTick = PhysicalEconomicCycleProgression.Preview(game);
        Assert.Equal(31UL, afterOneTick.CurrentMovementTurn);
        Assert.Equal(32UL, afterOneTick.SettlementTurn);
        Assert.Equal(1UL, afterOneTick.MovementTurnsToAdvance);
        var cycle = PhysicalEconomicCycleProgression.AdvanceToNext(game);
        Assert.Equal(32UL, cycle.MovementTurn);
        Assert.Equal(32UL, game.State.MovementTurn);
        Assert.Equal(31, game.State.Cycles.Count);
    }

    [Fact]
    public void AdvanceToNextEqualsOrdinaryTicksWithMovementAndLocalReallocation()
    {
        var game = Start();
        var control = Start();
        foreach (var session in new[] { game, control })
        {
            var from = session.InitialTile;
            session.ReassignWorkers(from, EconomicActivityKind.Forestry,
                EconomicActivityKind.Agriculture, 1UL);
            session.OrderWorkerTransit(from, session.AdjacentWorkable(from)[0],
                EconomicActivityKind.Agriculture, EconomicActivityKind.Mining, 2UL);
        }

        for (var iteration = 0; iteration < 4; iteration++)
        {
            var plan = PhysicalEconomicCycleProgression.Preview(game);
            var settled = PhysicalEconomicCycleProgression.AdvanceToNext(game);
            PhysicalEconomicCycle? manualCycle = null;
            for (ulong i = 0UL; i < plan.MovementTurnsToAdvance; i++)
                manualCycle = control.AdvanceMovementTurn();
            Assert.Equal(manualCycle, settled);
            Assert.Equal(game.CurrentHash, control.CurrentHash);
            Assert.Equal(game.ExportSaveJson(), control.ExportSaveJson());
        }

        var populated = EmergentSettlementProjection.FromSession(game);
        Assert.Equal(2, populated.Sites.Count);
        Assert.Equal(8UL, populated.TotalResidentWorkers);
        Assert.Equal(0UL, populated.TotalTravellers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        var payload = game.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(payload);
        Assert.Equal(14U, game.CurrentHash.FormatVersion);
        Assert.Equal(payload, reopened.ExportSaveJson());
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(PhysicalEconomicCycleProgression.Preview(game),
            PhysicalEconomicCycleProgression.Preview(reopened));
    }

    [Fact]
    public void SixTurnLegacyCadenceAndV13SaveRemainUnmodified()
    {
        var legacy = CanonicalPhysicalTacticalSession.Start();
        var initial = PhysicalEconomicCycleProgression.Preview(legacy);
        Assert.Equal(6UL, initial.MovementTurnsToAdvance);
        Assert.Equal(6UL, initial.SettlementTurn);
        var first = PhysicalEconomicCycleProgression.AdvanceToNext(legacy);
        Assert.Equal(6UL, first.MovementTurn);
        Assert.Equal(6UL, legacy.State.MovementTurn);
        Assert.Equal(6, legacy.Events.Count);
        Assert.Equal(9U, legacy.CurrentHash.FormatVersion);

        var version13 = Start(born: false);
        var cycle = PhysicalEconomicCycleProgression.AdvanceToNext(version13);
        Assert.Equal(1UL, cycle.MovementTurn);
        Assert.Equal(13U, version13.CurrentHash.FormatVersion);
        var save = version13.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(version13.CurrentHash, reopened.CurrentHash);
        Assert.Null(Assert.Single(
            EmergentSettlementProjection.FromSession(reopened).Sites).Community);
    }
}
