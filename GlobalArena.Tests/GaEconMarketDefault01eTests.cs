using System.Numerics;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01eTests
{
    private static CanonicalAutomaticMarketOfferTimeline Fresh() =>
        CanonicalAutomaticMarketOfferTimeline.StartOptIn(
            CanonicalPhysicalTacticalSession.Start());

    [Fact]
    public void ExplicitOptInExecutesCustodyAtCycleBoundaryOnly()
    {
        var initial = CanonicalPhysicalTacticalSession.Start();
        var originalSave = initial.ExportSaveJson();
        var originalHash = initial.CurrentHash.HexDigest;
        var timeline = CanonicalAutomaticMarketOfferTimeline.StartOptIn(initial);
        Assert.True(timeline.IsExplicitOptIn);
        Assert.False(timeline.ExecutesAutomaticSales);
        Assert.False(timeline.ReplacesLegacyFoodConsumption);

        for (var i = 1; i <= 5; i++)
        {
            var tick = timeline.AdvanceMovementTurn();
            Assert.Equal((ulong)i, tick.MovementTurn);
            Assert.False(tick.ClosedEconomicCycle);
            Assert.False(tick.ProvesExecutedCommercialSales);
            Assert.Equal(0, tick.NewlyCreatedOffers);
            Assert.Equal(BigInteger.Zero, tick.NewlyOfferedRaw);
            Assert.Empty(timeline.CaptureCanonicalSnapshot().PhysicalStockCommitments);
        }

        var closing = timeline.AdvanceMovementTurn();
        var snapshot = timeline.CaptureCanonicalSnapshot();
        Assert.True(closing.ClosedEconomicCycle);
        Assert.True(closing.ProvesAutomaticMarketCustody);
        Assert.Equal(1, closing.ClosedCycleCount);
        Assert.Equal(6UL, closing.MovementTurn);
        Assert.True(closing.NewlyCreatedOffers > 0);
        Assert.True(closing.NewlyOfferedRaw > BigInteger.Zero);
        Assert.False(closing.ProvesExecutedCommercialSales);
        Assert.False(closing.AuthorizesMonetaryRevenue);
        Assert.Equal(closing.NewlyCreatedOffers,
            snapshot.PhysicalStockCommitments.Count);
        Assert.All(snapshot.PhysicalStockCommitments, x =>
            Assert.Equal(CanonicalPhysicalCommitmentKind.MarketOffer, x.Kind));
        Assert.All(CanonicalDefaultMarketPoolPreflight.FromCurrent(snapshot).Rows,
            x => Assert.Equal(BigInteger.Zero, x.UncommittedLocalRaw));
        snapshot.CapturePhysicalStockCommitmentWitness().VerifyIndependent();
        Assert.Equal(originalSave, initial.ExportSaveJson());
        Assert.Equal(originalHash, initial.CurrentHash.HexDigest);
    }

    [Fact]
    public void RepeatedCyclesAndOptInCheckpointRoundTripAreDeterministic()
    {
        var timeline = Fresh();
        for (var i = 0; i < 6; i++) timeline.AdvanceMovementTurn();
        var firstHash = timeline.WorldHashHex;
        var first = timeline.CaptureCanonicalSnapshot();
        var firstOffers = first.PhysicalStockCommitments.Count;
        Assert.True(firstOffers > 0);

        var json = timeline.ExportCheckpointJson();
        var replay = CanonicalAutomaticMarketOfferTimeline.RestoreCheckpointJson(json);
        Assert.Equal(json, replay.ExportCheckpointJson());
        Assert.Equal(timeline.WorldHashHex, replay.WorldHashHex);
        Assert.Equal(timeline.MovementTurn, replay.MovementTurn);

        for (var i = 0; i < 6; i++)
        {
            var a = timeline.AdvanceMovementTurn();
            var b = replay.AdvanceMovementTurn();
            Assert.Equal(a, b);
            Assert.Equal(timeline.WorldHashHex, replay.WorldHashHex);
        }
        Assert.Equal(12UL, timeline.MovementTurn);
        Assert.Equal(2, timeline.ClosedEconomicCycles);
        Assert.NotEqual(firstHash, timeline.WorldHashHex);
        var next = timeline.CaptureCanonicalSnapshot();
        Assert.True(next.PhysicalStockCommitments.Count >= firstOffers);
        Assert.Equal(timeline.ExportCheckpointJson(), replay.ExportCheckpointJson());
        Assert.False(timeline.ExecutesAutomaticSales);
        Assert.False(replay.ExecutesAutomaticSales);
    }

    [Fact]
    public void LegacyRawSaveOrWrongRulesetCannotReopenAsOptInCheckpoint()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalAutomaticMarketOfferTimeline.RestoreCheckpointJson(
                game.ExportSaveJson()));

        game.AdvanceMovementTurn();
        var changedSave = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalAutomaticMarketOfferTimeline.StartOptIn(game));
        Assert.Equal(changedSave, game.ExportSaveJson());

        var timeline = Fresh();
        var checkpoint = JsonSerializer.Deserialize<
            CanonicalAutomaticMarketOfferCheckpoint>(
                timeline.ExportCheckpointJson())!;
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalAutomaticMarketOfferTimeline.RestoreCheckpointJson(
                JsonSerializer.Serialize(checkpoint with
                {
                    RulesetId = "GA-LEGACY",
                })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalAutomaticMarketOfferTimeline.RestoreCheckpointJson(
                JsonSerializer.Serialize(checkpoint with
                {
                    RulesetVersion = 999,
                })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalAutomaticMarketOfferTimeline.RestoreCheckpointJson(
                JsonSerializer.Serialize(checkpoint with
                {
                    CanonicalWorldHashHex = "FAKE",
                })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalAutomaticMarketOfferTimeline.RestoreCheckpointJson(
                JsonSerializer.Serialize(checkpoint with
                {
                    CanonicalSaveJson = checkpoint.CanonicalSaveJson + " ",
                })));
    }

    [Fact]
    public void DetachedSnapshotsCannotMutateOwnedTimelineOrCheckpoint()
    {
        var timeline = Fresh();
        var beforeHash = timeline.WorldHashHex;
        var beforeJson = timeline.ExportCheckpointJson();
        var detached = timeline.CaptureCanonicalSnapshot();
        detached.AdvanceMovementTurn();
        Assert.Equal(0UL, timeline.MovementTurn);
        Assert.Equal(beforeHash, timeline.WorldHashHex);
        Assert.Equal(beforeJson, timeline.ExportCheckpointJson());

        var turn = timeline.AdvanceMovementTurn();
        Assert.Equal(1UL, turn.MovementTurn);
        Assert.Equal(0, turn.ClosedCycleCount);
        Assert.Equal(0, turn.NewlyCreatedOffers);
    }

    [Fact]
    public void FullTurnTransitionIsJournalledAndSaveableAfterEveryTick()
    {
        var timeline = Fresh();
        for (var i = 0; i < 13; i++)
        {
            var tick = timeline.AdvanceMovementTurn();
            Assert.Equal((ulong)(i + 1), tick.MovementTurn);
            var checkpoint = timeline.ExportCheckpointJson();
            var restored = CanonicalAutomaticMarketOfferTimeline
                .RestoreCheckpointJson(checkpoint);
            Assert.Equal(checkpoint, restored.ExportCheckpointJson());
            Assert.Equal(timeline.WorldHashHex, restored.WorldHashHex);
            Assert.Equal(timeline.ClosedEconomicCycles,
                restored.ClosedEconomicCycles);
            Assert.False(tick.ProvesExecutedCommercialSales);
        }
        Assert.Equal(13UL, timeline.MovementTurn);
        Assert.Equal(2, timeline.ClosedEconomicCycles);
    }
}
