using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01F: the exact Godot V20 initialization must be
/// able to opt in without mutating the currently playable production session.
/// </summary>
public sealed class GaEconMarketDefault01fGodotIntegrationTests
{
    private static CanonicalPhysicalTacticalSession GodotInitial() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    [Fact]
    public void RealGodotModeClosesAtTurnOneAndOnlyIsolatedLabGetsOffers()
    {
        var playable = GodotInitial();
        var originalSave = playable.ExportSaveJson();
        var originalHash = playable.CurrentHash;
        var timeline = CanonicalAutomaticMarketOfferTimeline.StartOptIn(playable);
        Assert.Equal(0UL, timeline.MovementTurn);
        Assert.Equal(originalSave, playable.ExportSaveJson());

        var step = timeline.AdvanceMovementTurn();
        Assert.True(step.ClosedEconomicCycle);
        Assert.Equal(1UL, step.MovementTurn);
        Assert.Equal(1, step.ClosedCycleCount);
        Assert.True(step.NewlyCreatedOffers > 0);
        Assert.True(step.NewlyOfferedRaw > 0);
        Assert.False(step.ProvesExecutedCommercialSales);
        Assert.False(step.AuthorizesMonetaryRevenue);
        Assert.Equal(0UL, playable.State.MovementTurn);
        Assert.Empty(playable.PhysicalStockCommitments);
        Assert.Equal(originalHash, playable.CurrentHash);
        Assert.Equal(originalSave, playable.ExportSaveJson());

        var staged = timeline.CaptureCanonicalSnapshot();
        var physical = CanonicalDefaultMarketPoolPreflight.FromCurrent(staged);
        Assert.All(physical.Rows, x => Assert.Equal(
            System.Numerics.BigInteger.Zero, x.UncommittedLocalRaw));
        Assert.True(physical.EligibleByDefaultRaw > 0);
        Assert.Equal(staged.State.Cycles.Count, timeline.ClosedEconomicCycles);
        Assert.Equal(PhysicalFoodMarketMode.LocalCashSettlement,
            staged.State.MarketMode);
        Assert.True(staged.PhysicalStockCommitments.All(x =>
            x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer));
    }

    [Fact]
    public void GodotLabSaveRoundTripsWithoutChangingLegacySavesOrHudState()
    {
        var playable = GodotInitial();
        var productionSave = playable.ExportSaveJson();
        var lab = CanonicalAutomaticMarketOfferTimeline.StartOptIn(playable);
        lab.AdvanceMovementTurn();

        var checkpoint = lab.ExportCheckpointJson();
        var reopened = CanonicalAutomaticMarketOfferTimeline
            .RestoreCheckpointJson(checkpoint);
        Assert.Equal(checkpoint, reopened.ExportCheckpointJson());
        Assert.Equal(lab.WorldHashHex, reopened.WorldHashHex);
        Assert.Equal(1UL, reopened.MovementTurn);
        Assert.Equal(productionSave, playable.ExportSaveJson());

        var a = lab.AdvanceMovementTurn();
        var b = reopened.AdvanceMovementTurn();
        Assert.Equal(a, b);
        Assert.Equal(lab.ExportCheckpointJson(),
            reopened.ExportCheckpointJson());
        Assert.Equal(productionSave, playable.ExportSaveJson());
        Assert.False(reopened.ExecutesAutomaticSales);
        Assert.False(reopened.ReplacesLegacyFoodConsumption);
    }
}
