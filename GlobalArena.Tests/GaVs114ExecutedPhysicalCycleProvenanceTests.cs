using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-REBASE-05: provenance originates in actual tick execution and
/// survives canonical save replay without introducing a new save field.
/// No invented producer->consumer join, income, N or household money.
/// </summary>
public sealed class GaVs114ExecutedPhysicalCycleProvenanceTests
{
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
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var turn = 0; turn < 5; turn++)
            Assert.Null(session.AdvanceMovementTurn());
        Assert.NotNull(session.AdvanceMovementTurn());
        return session;
    }

    [Fact]
    public void NoProvenanceExistsUntilAnActuallyCompletedEconomicTick()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        Assert.Throws<InvalidOperationException>(() =>
            session.CaptureLatestExecutedPhysicalCycleProvenance());
        session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            session.CaptureLatestExecutedPhysicalCycleProvenance());
        for (var i = 0; i < 5; i++) session.AdvanceMovementTurn();
        var proof = session.CaptureLatestExecutedPhysicalCycleProvenance();
        Assert.Equal(1UL, proof.CycleOrdinal);
        Assert.Equal(6UL, proof.ClosingMovementTurn);
    }

    [Fact]
    public void ActualWorkerOriginsCommoditiesAndFinalUseMatchCanonicalCounters()
    {
        var session = ClosedLegacy();
        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();
        var proof = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var cycle = session.State.Cycles[^1];
        Assert.True(proof.ProvesActualProductionBySourceAndCommodity);
        Assert.True(proof.ProvesActualFinalUseAtDestination);
        Assert.False(proof.ProvesWhichProducerSuppliedAConsumedUnit);
        Assert.False(proof.AuthenticatesIntermediateInputsOrCrossCycleInventoryVintage);
        Assert.False(proof.CanAuthorizeRealPurchasingPower);
        Assert.False(proof.IsNewCanonicalSaveField);
        Assert.True(proof.IsReconstructedOnCanonicalSaveReplay);
        Assert.False(proof.CreatesProductionOrMoney);
        Assert.Equal(hash.HexDigest, proof.ClosingWorldHashHex);
        Assert.Equal(cycle.ProducedRaw, proof.ActuallyProducedRaw);
        Assert.Equal(cycle.FoodConsumedRaw, proof.ActuallyConsumedRaw);
        Assert.Equal(cycle.FoodUnmetRaw, proof.ActuallyUnmetFoodRaw);
        Assert.Equal(session.State.TotalWorkers, proof.TotalRealPopulation);
        Assert.Equal(new BigInteger(cycle.ProducedRaw),
            proof.Production.Aggregate(BigInteger.Zero,
                (total, row) => total + row.ProducedRaw));
        Assert.Equal(new BigInteger(cycle.FoodConsumedRaw),
            proof.FinalUse.Aggregate(BigInteger.Zero,
                (total, row) => total + row.ConsumedRaw));
        Assert.All(proof.Production, row =>
        {
            Assert.True(row.SourceFineTile > 0UL);
            Assert.True(row.Commodity is 1U or 2U or 3U);
            Assert.True(row.ProducedRaw > 0UL);
            Assert.Equal(CanonicalPhysicalProductionChannel.AssignedWorker, row.Channel);
            Assert.Equal(0UL, row.JourneyOrderId);
        });
        Assert.All(proof.FinalUse, row =>
        {
            Assert.Equal(1U, row.Commodity);
            Assert.Equal(CanonicalPhysicalFinalUseChannel.ResidentLocal, row.Channel);
            Assert.Equal(0UL, row.JourneyOrderId);
        });
        proof.VerifyIndependent();
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
    }

    [Fact]
    public void LocalStockConservationIsExplicitPerCommodityAndSite()
    {
        var session = ClosedLegacy();
        var proof = session.CaptureLatestExecutedPhysicalCycleProvenance();
        Assert.NotEmpty(proof.LocalStock);
        foreach (var stock in proof.LocalStock)
        {
            Assert.Equal(
                (BigInteger)stock.OpeningRaw + stock.WorkerProducedRaw - stock.ResidentConsumedRaw,
                new BigInteger(stock.ClosingRaw));
            Assert.Equal(
                session.State.Stocks.Single(s =>
                    s.TileId.FineStrategicCellId.Value == stock.FineTile &&
                    s.Commodity.Value == stock.Commodity).RawQuantity,
                stock.ClosingRaw);
        }
        Assert.Equal(new BigInteger(proof.ActuallyProducedRaw),
            proof.Production.Aggregate(BigInteger.Zero,
                (total, x) => total + x.ProducedRaw));
    }

    [Fact]
    public void CashFoodSettlementStillCannotIdentifyWhichProductionVintageWasConsumed()
    {
        var session = Cash();
        Assert.NotNull(session.AdvanceMovementTurn());
        var proof = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var actualFood = CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(session);
        var localUse = proof.FinalUse.Where(x =>
            x.Channel == CanonicalPhysicalFinalUseChannel.ResidentLocal)
            .Aggregate(0UL, (sum, x) => checked(sum + x.ConsumedRaw));
        Assert.Equal(actualFood.TotalActualLocalFoodRaw, localUse);
        Assert.Equal(proof.ActuallyConsumedRaw, localUse);
        Assert.False(proof.ProvesWhichProducerSuppliedAConsumedUnit);
        Assert.False(proof.CanAuthorizeRealPurchasingPower);
    }

    [Fact]
    public void SaveReplaysIdenticalSourceDestinationAndStocksWithoutSchemaMigration()
    {
        var session = ClosedLegacy();
        var before = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var payload = session.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(payload);
        var after = reopened.CaptureLatestExecutedPhysicalCycleProvenance();

        Assert.Equal(payload, reopened.ExportSaveJson());
        Assert.Equal(session.CurrentHash, reopened.CurrentHash);
        Assert.Equal(before.CycleOrdinal, after.CycleOrdinal);
        Assert.Equal(before.ClosingWorldHashHex, after.ClosingWorldHashHex);
        Assert.Equal(before.Production.ToArray(), after.Production.ToArray());
        Assert.Equal(before.FinalUse.ToArray(), after.FinalUse.ToArray());
        Assert.Equal(before.LocalStock.ToArray(), after.LocalStock.ToArray());
        after.VerifyIndependent();
        Assert.Throws<InvalidOperationException>(() => before.RequireCurrent(reopened));
    }

    [Fact]
    public void TwoSubsequentRealCyclesHaveDifferentWitnessesAndNoDuplicateProof()
    {
        var session = Cash();
        Assert.NotNull(session.AdvanceMovementTurn());
        var first = session.CaptureLatestExecutedPhysicalCycleProvenance();
        Assert.NotNull(session.AdvanceMovementTurn());
        Assert.Throws<InvalidOperationException>(() => first.RequireCurrent(session));
        var second = session.CaptureLatestExecutedPhysicalCycleProvenance();
        Assert.Equal(1UL, first.CycleOrdinal);
        Assert.Equal(2UL, second.CycleOrdinal);
        Assert.NotEqual(first.ClosingWorldHashHex, second.ClosingWorldHashHex);
        second.VerifyIndependent();
    }

    [Fact]
    public void PostSettlementWorkerChangeMakesEvidenceStale()
    {
        var session = ClosedLegacy();
        var receipt = session.CaptureLatestExecutedPhysicalCycleProvenance();
        session.ReassignWorkers(session.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        Assert.Throws<InvalidOperationException>(() => receipt.RequireCurrent(session));
        Assert.Throws<InvalidOperationException>(() =>
            session.CaptureLatestExecutedPhysicalCycleProvenance());
    }

    [Fact]
    public void OffCadenceMovementTurnInvalidatesOldReceipt()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        var proof = session.CaptureLatestExecutedPhysicalCycleProvenance();
        Assert.Null(session.AdvanceMovementTurn());
        Assert.Throws<InvalidOperationException>(() => proof.VerifyIndependent());
        Assert.Throws<InvalidOperationException>(() =>
            session.CaptureLatestExecutedPhysicalCycleProvenance());
    }

    [Fact]
    public void SaveReplayInCashModePreservesEconomicReceiptsAndProvenanceTogether()
    {
        var session = Cash();
        Assert.NotNull(session.AdvanceMovementTurn());
        var proof = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var food = CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(session);
        var save = session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var replay = restored.CaptureLatestExecutedPhysicalCycleProvenance();
        var foodReplay = CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(restored);
        Assert.Equal(proof.Production.ToArray(), replay.Production.ToArray());
        Assert.Equal(proof.FinalUse.ToArray(), replay.FinalUse.ToArray());
        Assert.Equal(food.TotalActualLocalFoodRaw, foodReplay.TotalActualLocalFoodRaw);
        Assert.Equal(save, restored.ExportSaveJson());
    }
}
