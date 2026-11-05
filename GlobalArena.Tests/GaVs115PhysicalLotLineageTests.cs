using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS115: tracked vintage and custody are deterministic physical-quantity
/// bookkeeping, not magically authenticated identity in a fungible stock.
/// </summary>
public sealed class GaVs115PhysicalLotLineageTests
{
    private static CanonicalPhysicalTacticalSession Legacy() =>
        CanonicalPhysicalTacticalSession.Start();

    private static CanonicalPhysicalTacticalSession Cash() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static CanonicalPhysicalTacticalSession ClosedLegacy()
    {
        var session = Legacy();
        for (var i = 0; i < 5; i++)
            Assert.Null(session.AdvanceMovementTurn());
        Assert.NotNull(session.AdvanceMovementTurn());
        return session;
    }

    private static void VerifyConservation(
        CanonicalPhysicalTacticalSession session,
        CanonicalPhysicalLotCycleWitness witness)
    {
        witness.VerifyIndependent();
        var state = session.State;
        Assert.Equal(session.CurrentHash.HexDigest, witness.ClosingWorldHashHex);
        Assert.Equal((ulong)state.Cycles.Count, witness.CycleOrdinal);
        var stockTotals = witness.ClosingLots.Where(x =>
                x.Custody == CanonicalPhysicalLotCustodyKind.LocalStock)
            .GroupBy(x => (x.CustodyId, x.Commodity))
            .ToDictionary(x => x.Key,
                x => x.Aggregate(BigInteger.Zero, (s, v) => s + v.RawQuantity));
        var actualStocks = state.Stocks.Where(x => x.RawQuantity > 0UL)
            .ToDictionary(x => (
                CustodyId: x.TileId.FineStrategicCellId.Value,
                Commodity: x.Commodity.Value), x => new BigInteger(x.RawQuantity));
        Assert.Equal(actualStocks.Count, stockTotals.Count);
        foreach (var (key, value) in actualStocks)
            Assert.Equal(value, stockTotals[key]);

        var cargo = witness.ClosingLots.Where(x =>
                x.Custody == CanonicalPhysicalLotCustodyKind.ExpeditionCargo)
            .GroupBy(x => x.CustodyId)
            .ToDictionary(x => x.Key,
                x => x.Aggregate(BigInteger.Zero, (sum, row) => sum + row.RawQuantity));
        var realCargo = (state.TravelFoodLedgers ??
                Array.Empty<PhysicalJourneyFoodLedger>())
            .Where(x => x.FoodRemainingRaw > 0UL)
            .ToDictionary(x => x.OrderId, x => new BigInteger(x.FoodRemainingRaw));
        Assert.Equal(realCargo.Count, cargo.Count);
        foreach (var (id, quantity) in realCargo)
            Assert.Equal(quantity, cargo[id]);

        Assert.Equal(new BigInteger(state.Cycles[^1].FoodConsumedRaw),
            witness.FinalUseAttributions.Aggregate(BigInteger.Zero,
                (sum, row) => sum + row.RawQuantity));
    }

    [Fact]
    public void NoLotWitnessBeforeExecutedSettlement()
    {
        var session = Legacy();
        Assert.Throws<InvalidOperationException>(() =>
            session.CaptureLatestPhysicalLotWitness());
        for (var i = 0; i < 5; i++)
            session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            session.CaptureLatestPhysicalLotWitness());
        Assert.NotNull(session.AdvanceMovementTurn());
        var witness = session.CaptureLatestPhysicalLotWitness();
        Assert.Equal(1UL, witness.CycleOrdinal);
        Assert.Equal(6UL, witness.ClosingMovementTurn);
        VerifyConservation(session, witness);
    }

    [Fact]
    public void RealStockLotsPreserveOriginalCycleAcrossLaterSettlements()
    {
        var session = ClosedLegacy();
        var original = session.CaptureLatestPhysicalLotWitness();
        Assert.False(original.PhysicalOriginIdentityIsDirectlyAuthenticated);
        Assert.True(original.UsesDeterministicFifoAccountingConvention);
        Assert.True(original.TracksStockAndExpeditionCustodyAcrossCycles);
        Assert.True(original.ReconstructedByCanonicalActionReplay);
        Assert.False(original.CanAuthorizeRealPurchasingPower);
        Assert.False(original.WritesNewCanonicalSaveFields);
        var earlierStock = original.ClosingLots
            .Where(x => x.Custody == CanonicalPhysicalLotCustodyKind.LocalStock)
            .ToArray();

        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        var next = session.CaptureLatestPhysicalLotWitness();
        Assert.Equal(2UL, next.CycleOrdinal);
        Assert.NotEqual(original.ClosingWorldHashHex, next.ClosingWorldHashHex);
        VerifyConservation(session, next);
        Assert.All(next.ClosingLots.Where(x =>
            x.OriginKind != CanonicalPhysicalLotOriginKind.UnattributedOpeningStock),
            row => Assert.InRange(row.OriginCycleOrdinal, 1UL, 2UL));

        // Durable preexisting stock is not stamped as output of cycle 2.
        foreach (var commodity in earlierStock.Where(x => x.Commodity != 1U))
            if (commodity.RawQuantity != 0UL)
                Assert.Contains(next.ClosingLots, row =>
                    row.OriginCycleOrdinal == commodity.OriginCycleOrdinal &&
                    row.OriginFineTile == commodity.OriginFineTile &&
                    row.Commodity == commodity.Commodity);
        Assert.Throws<InvalidOperationException>(() => original.VerifyIndependent());
    }

    [Fact]
    public void FinalUseAttributionsReconcileRealPhysicalConsumptionWithoutAuthorizingN()
    {
        var session = ClosedLegacy();
        var book = session.CaptureLatestPhysicalLotWitness();
        var physical = session.CaptureLatestExecutedPhysicalCycleProvenance();
        Assert.Equal(new BigInteger(physical.ActuallyConsumedRaw),
            book.FinalUseAttributions.Aggregate(BigInteger.Zero,
                (a, r) => a + r.RawQuantity));
        Assert.All(book.FinalUseAttributions, r =>
        {
            Assert.Equal(book.CycleOrdinal, r.ConsumingCycleOrdinal);
            Assert.Equal(1U, r.Commodity);
            Assert.Equal(CanonicalPhysicalFinalUseChannel.ResidentLocal, r.UseChannel);
            Assert.Equal(0UL, r.JourneyOrderId);
            Assert.True(r.RawQuantity > 0UL);
        });
        Assert.False(book.PhysicalOriginIdentityIsDirectlyAuthenticated);
        Assert.False(book.CanAuthorizeRealPurchasingPower);
        VerifyConservation(session, book);
    }

    [Fact]
    public void OrdinaryTicksAndWorkerReassignmentInvalidateOldCycleProof()
    {
        var session = ClosedLegacy();
        var witness = session.CaptureLatestPhysicalLotWitness();
        Assert.Null(session.AdvanceMovementTurn());
        Assert.Throws<InvalidOperationException>(() =>
            witness.RequireCurrent(session));
        Assert.Throws<InvalidOperationException>(() =>
            session.CaptureLatestPhysicalLotWitness());

        var again = ClosedLegacy();
        var old = again.CaptureLatestPhysicalLotWitness();
        again.ReassignWorkers(again.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        Assert.Throws<InvalidOperationException>(() => old.VerifyIndependent());
    }

    [Fact]
    public void LegacySaveReplayRecreatesExactlyTheSameLotBook()
    {
        var original = ClosedLegacy();
        for (var i = 0; i < 12; i++) original.AdvanceMovementTurn();
        var before = original.CaptureLatestPhysicalLotWitness();
        var save = original.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var after = restored.CaptureLatestPhysicalLotWitness();
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(before.ClosingWorldHashHex, after.ClosingWorldHashHex);
        Assert.Equal(before.ClosingLots.ToArray(), after.ClosingLots.ToArray());
        Assert.Equal(before.FinalUseAttributions.ToArray(),
            after.FinalUseAttributions.ToArray());
        Assert.Throws<InvalidOperationException>(() =>
            before.RequireCurrent(restored));
        VerifyConservation(restored, after);
    }

    [Fact]
    public void CashFoodSaveReplayPreservesFifoLotsWithDifferentOldAndNewVintages()
    {
        var session = Cash();
        for (var i = 0; i < 3; i++)
            Assert.NotNull(session.AdvanceMovementTurn());
        var before = session.CaptureLatestPhysicalLotWitness();
        var save = session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var after = restored.CaptureLatestPhysicalLotWitness();
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(before.ClosingLots.ToArray(), after.ClosingLots.ToArray());
        Assert.Equal(before.FinalUseAttributions.ToArray(),
            after.FinalUseAttributions.ToArray());
        Assert.False(after.CanAuthorizeRealPurchasingPower);
        VerifyConservation(restored, after);
    }

    private static CanonicalPhysicalTacticalSession WithExpedition(
        out PhysicalLongDistanceTransit journey)
    {
        var session = Cash();
        var start = session.InitialTile;
        session.ReassignWorkers(start, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 2UL);
        session.ReassignWorkers(start, EconomicActivityKind.Mining,
            EconomicActivityKind.Agriculture, 2UL);
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        var food = session.StockAt(start, new CommodityId(1U));
        Assert.True(food > 0UL);
        var neighbors = session.AdjacentWorkable(start).ToHashSet();
        var target = neighbors.SelectMany(session.AdjacentWorkable)
            .Where(x => x != start && !neighbors.Contains(x))
            .Distinct().OrderBy(x => x.FineStrategicCellId.Value).First();
        journey = session.OrderProvisionedLongDistanceTransit(
            start, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL, Math.Min(food, 2_000_000UL));
        return session;
    }

    [Fact]
    public void ProvisionedExpeditionMovesExistingFoodLotsWithoutMintingProduction()
    {
        var session = WithExpedition(out var journey);
        var source = session.InitialTile.FineStrategicCellId.Value;
        var purchased = session.State.TravelFoodLedgers!.Single(
            x => x.OrderId == journey.OrderId).FoodPurchasedRaw;
        Assert.True(purchased > 0UL);

        // The next tick is a completed economic cycle in the early game.
        var real = session.AdvanceMovementTurn();
        Assert.NotNull(real);
        var witness = session.CaptureLatestPhysicalLotWitness();
        VerifyConservation(session, witness);
        var carried = witness.ClosingLots.Where(x =>
            x.Custody == CanonicalPhysicalLotCustodyKind.ExpeditionCargo &&
            x.CustodyId == journey.OrderId).ToArray();
        var consumed = witness.FinalUseAttributions.Where(x =>
            x.UseChannel == CanonicalPhysicalFinalUseChannel.ExpeditionRations &&
            x.JourneyOrderId == journey.OrderId).Aggregate(0UL, (total, x) => checked(total + x.RawQuantity));
        var remaining = session.State.TravelFoodLedgers!.Single(
            x => x.OrderId == journey.OrderId).FoodRemainingRaw;
        Assert.Equal(new BigInteger(purchased),
            carried.Aggregate(BigInteger.Zero, (a, x) => a + x.RawQuantity) +
            consumed);
        Assert.All(carried, x => Assert.True(x.OriginCycleOrdinal <= 6UL));
        Assert.All(witness.FinalUseAttributions.Where(x =>
            x.JourneyOrderId == journey.OrderId), x =>
            Assert.Equal(1U, x.Commodity));
        Assert.Equal(source, journey.Source.FineStrategicCellId.Value);
        Assert.Equal(new BigInteger(remaining),
            carried.Aggregate(BigInteger.Zero, (a, x) => a + x.RawQuantity));
    }

    [Fact]
    public void ProvisionedExpeditionSurvivesSaveReplayWithCargoVintage()
    {
        var session = WithExpedition(out _);
        Assert.NotNull(session.AdvanceMovementTurn());
        var proof = session.CaptureLatestPhysicalLotWitness();
        var save = session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var replay = restored.CaptureLatestPhysicalLotWitness();
        Assert.Equal(proof.ClosingLots.ToArray(), replay.ClosingLots.ToArray());
        Assert.Equal(proof.FinalUseAttributions.ToArray(),
            replay.FinalUseAttributions.ToArray());
        Assert.Equal(save, restored.ExportSaveJson());
        VerifyConservation(restored, replay);
    }

    [Fact]
    public void LotBookDoesNotChangeCanonicalWorldHashOrSavePayload()
    {
        var session = ClosedLegacy();
        var expectedHash = session.CurrentHash;
        var expectedSave = session.ExportSaveJson();
        var first = session.CaptureLatestPhysicalLotWitness();
        for (var i = 0; i < 3; i++)
        {
            var again = session.CaptureLatestPhysicalLotWitness();
            Assert.Equal(first.ClosingLots.ToArray(), again.ClosingLots.ToArray());
        }
        Assert.Equal(expectedHash, session.CurrentHash);
        Assert.Equal(expectedSave, session.ExportSaveJson());
    }
}
