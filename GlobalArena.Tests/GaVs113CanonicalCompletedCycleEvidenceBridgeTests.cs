using System.Numerics;
using GlobalArena.Economy;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs113CanonicalCompletedCycleEvidenceBridgeTests
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

    private static PhysicalFinalOutputPreflight Candidate(
        ulong cycle, ulong population, ulong grossProduced)
    {
        // An intentionally arbitrary commodity label demonstrates why
        // aggregate equality alone does NOT authenticate commodity origin.
        return PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(
            cycle, population,
            [new PhysicalProductionClaim(1, cycle, 10, 1U, grossProduced)],
            [new PhysicalDispositionClaim(1, cycle, 1, 10, 1U,
                grossProduced, PhysicalOutputDispositionRole.ExplicitRetention)],
            [new PhysicalCommodityUnitScale(1U, 1UL)]);
    }

    [Fact]
    public void RejectsUnclosedCycleAndSameTurnNonTickMutation()
    {
        var session = Legacy();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalCompletedCycleEvidenceBridge.Capture(session));
        for (var i = 0; i < 5; i++)
            session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalCompletedCycleEvidenceBridge.Capture(session));
        Assert.NotNull(session.AdvanceMovementTurn());
        session.ReassignWorkers(session.InitialTile,
            EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 1UL);
        Assert.Equal("reassign", session.Events[^1].Kind);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalCompletedCycleEvidenceBridge.Capture(session));
    }

    [Fact]
    public void RealCycleTickCountersAndWorldHashAreReadOnlyAndIndependentlyVerified()
    {
        var session = ClosedLegacy();
        var beforeHash = session.CurrentHash;
        var beforeSave = session.ExportSaveJson();
        var bridge = CanonicalCompletedCycleEvidenceBridge.Capture(session);
        var cycle = session.State.Cycles[^1];

        Assert.True(bridge.ProvesActualCycleClosure);
        Assert.True(bridge.ProvesActualAggregateProductionAndFoodUse);
        Assert.False(bridge.AuthenticatesPerCommodityProduction);
        Assert.False(bridge.AuthenticatesFinalDestinations);
        Assert.False(bridge.CanAuthorizePurchasingPower);
        Assert.False(bridge.WritesToCanonicalSave);
        Assert.False(bridge.ExecutesMarketOrProduction);
        Assert.Equal(1UL, bridge.Facts.CycleOrdinal);
        Assert.Equal(6UL, bridge.Facts.ClosingMovementTurn);
        Assert.Equal(session.State.TotalWorkers,
            bridge.Facts.ResidentAndTravelingPopulation);
        Assert.Equal(cycle.ProducedRaw, bridge.Facts.ActuallyProducedRaw);
        Assert.Equal(cycle.FoodConsumedRaw,
            bridge.Facts.ActuallyConsumedFoodRaw);
        Assert.Equal(cycle.FoodUnmetRaw,
            bridge.Facts.ActuallyUnmetFoodRaw);
        Assert.Equal(beforeHash.HexDigest,
            bridge.Facts.ClosingWorldHashHex);
        bridge.VerifyIndependent();
        Assert.Equal(beforeHash, session.CurrentHash);
        Assert.Equal(beforeSave, session.ExportSaveJson());
    }

    [Fact]
    public void ExactAggregateAgreementIsNecessaryButNeverProofOfCommodityFinalOutput()
    {
        var session = ClosedLegacy();
        var bridge = CanonicalCompletedCycleEvidenceBridge.Capture(session);
        Assert.True(bridge.Facts.ActuallyProducedRaw > 0UL);
        var alleged = Candidate(bridge.Facts.CycleOrdinal,
            bridge.Facts.ResidentAndTravelingPopulation,
            bridge.Facts.ActuallyProducedRaw);
        var review = bridge.ReviewCandidate(session, alleged);

        Assert.True(review.AgreesWithCanonicalAggregate);
        Assert.Equal(new BigInteger(bridge.Facts.ActuallyProducedRaw),
            review.CandidateGrossProducedRaw);
        Assert.False(review.HasCanonicalProducerReceipts);
        Assert.False(review.HasCanonicalFinalDestinationReceipts);
        Assert.False(review.CanAuthorizePurchasingPower);
    }

    [Fact]
    public void WrongCyclePopulationOrAggregateFailsClosed()
    {
        var session = ClosedLegacy();
        var bridge = CanonicalCompletedCycleEvidenceBridge.Capture(session);
        var facts = bridge.Facts;
        Assert.Throws<InvalidOperationException>(() =>
            bridge.ReviewCandidate(session, Candidate(
                facts.CycleOrdinal + 1UL,
                facts.ResidentAndTravelingPopulation,
                facts.ActuallyProducedRaw)));
        Assert.Throws<InvalidOperationException>(() =>
            bridge.ReviewCandidate(session, Candidate(
                facts.CycleOrdinal,
                facts.ResidentAndTravelingPopulation + 1UL,
                facts.ActuallyProducedRaw)));
        Assert.Throws<InvalidOperationException>(() =>
            bridge.ReviewCandidate(session, Candidate(
                facts.CycleOrdinal,
                facts.ResidentAndTravelingPopulation,
                facts.ActuallyProducedRaw + 1UL)));
        Assert.Throws<InvalidOperationException>(() =>
            bridge.ReviewCandidate(session, Candidate(
                facts.CycleOrdinal,
                facts.ResidentAndTravelingPopulation,
                facts.ActuallyProducedRaw - 1UL)));
    }

    [Fact]
    public void StaleSnapshotAndForeignSessionCannotReassignAuthority()
    {
        var session = ClosedLegacy();
        var bridge = CanonicalCompletedCycleEvidenceBridge.Capture(session);
        var save = session.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);

        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(reopened));
        var reconstructed = CanonicalCompletedCycleEvidenceBridge.Capture(reopened);
        Assert.Equal(bridge.Facts, reconstructed.Facts);
        Assert.Equal(save, reopened.ExportSaveJson());

        session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(session));
        Assert.Throws<InvalidOperationException>(() =>
            bridge.VerifyIndependent());
    }

    [Fact]
    public void WorkerReassignmentAfterCaptureInvalidatesPhysicalEvidence()
    {
        var session = ClosedLegacy();
        var bridge = CanonicalCompletedCycleEvidenceBridge.Capture(session);
        session.ReassignWorkers(session.InitialTile,
            EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(session));
    }

    [Fact]
    public void ActualExecutedFoodReceiptsProveFoodUseButNotNewFoodOrigin()
    {
        var session = Cash();
        Assert.NotNull(session.AdvanceMovementTurn());
        var bridge = CanonicalCompletedCycleEvidenceBridge.Capture(session);
        var executed = CanonicalExecutedLocalFoodPriceWitness
            .FromLatestRealSettlement(session);
        var save = session.ExportSaveJson();
        var review = bridge.ReviewExecutedLocalFoodUse(session, executed);

        Assert.Equal(1UL, review.CanonicalCycleOrdinal);
        Assert.Equal(executed.TotalActualLocalFoodRaw,
            review.ActuallyDeliveredLocalFoodRaw);
        Assert.Equal(bridge.Facts.ActuallyConsumedFoodRaw -
            executed.TotalActualLocalFoodRaw, review.OtherConsumedFoodRaw);
        Assert.Equal(executed.HasExecutedPriceObservation,
            review.HasExecutedLocalFoodReceipts);
        Assert.False(review.ProvesSameCycleFoodProductionOrigin);
        Assert.False(review.CanAuthorizePurchasingPower);
        Assert.Equal(save, session.ExportSaveJson());
    }

    [Fact]
    public void DifferentPeriodExecutedFoodWitnessCannotBeReused()
    {
        var session = Cash();
        Assert.NotNull(session.AdvanceMovementTurn());
        var oldWitness = CanonicalExecutedLocalFoodPriceWitness
            .FromLatestRealSettlement(session);
        Assert.NotNull(session.AdvanceMovementTurn());
        var newBridge = CanonicalCompletedCycleEvidenceBridge.Capture(session);
        Assert.Throws<InvalidOperationException>(() =>
            newBridge.ReviewExecutedLocalFoodUse(session, oldWitness));
    }
}
