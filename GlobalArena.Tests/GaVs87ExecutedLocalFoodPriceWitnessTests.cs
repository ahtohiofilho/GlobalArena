using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs87ExecutedLocalFoodPriceWitnessTests
{
    private static GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture() =>
        GaVs67IndependentFreightComponentsTests.SharedBuyer().Fixture;

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalExecutedLocalFoodPriceWitness Witness) Closed()
    {
        var f = Fixture();
        // The VS67 fixture reassigns a worker after the movement-2 closing
        // tick. Only a fresh executed cycle may become witness authority.
        Assert.NotNull(f.Session.AdvanceMovementTurn());
        return (f,
            CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(
                f.Session));
    }

    [Fact]
    public void ActualLocalFoodSalesHavePhysicalQuantityMicrocreditPaymentAndRealTickProof()
    {
        var (fixture, observed) = Closed();
        Assert.True(observed.IsFromCanonicalFoodCashState);
        Assert.True(observed.HasExecutedPriceObservation);
        Assert.True(observed.HasEvidenceOfRealCashFoodPurchase);
        Assert.NotEmpty(observed.ExecutedReceipts);
        Assert.Equal(1U, observed.ObservedCommodityId);
        Assert.Equal("GA-LEGACY-FOOD-CASH-MICROCREDITS",
            observed.ObservedNominalUnitId);
        Assert.False(observed.QuotesAbstractVs86Price);
        Assert.False(observed.EstablishesRealPricesForOtherCommodities);
        Assert.False(observed.ChangesEconomicSettlement);
        Assert.False(observed.CanAuthorizeFutureSettlement);
        var cycle = fixture.Session.State.Cycles[^1];
        Assert.Equal(cycle.MovementTurn,
            observed.CompletedEconomicCycleMovementTurn);
        Assert.True(observed.TotalActualLocalFoodRaw > 0);
        Assert.True(observed.TotalActualLocalFoodRaw <= cycle.FoodConsumedRaw);
        Assert.All(observed.ExecutedReceipts, x =>
        {
            Assert.Equal(cycle.MovementTurn, x.MovementTurn);
            Assert.True(x.ExecutedFoodRaw > 0UL);
            Assert.Equal(
                (BigInteger)x.ExecutedFoodRaw *
                x.ExecutedPriceMicrocreditsPerWholeUnit /
                PhysicalFoodCashMarketState.MonetaryScale,
                new BigInteger(x.ActuallyPaidMicrocredits));
        });
        Assert.Equal(
            observed.ExecutedReceipts.Aggregate(0UL,
                (sum, x) => checked(sum + x.ExecutedFoodRaw)),
            observed.TotalActualLocalFoodRaw);
        Assert.Equal(observed.ExecutedReceipts.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.ActuallyPaidMicrocredits),
            observed.TotalActualPaymentsMicrocredits);
        observed.VerifyIndependent();
    }

    [Fact]
    public void CanonicalRestoredSaveReconstructsExactHistoricalExecutedFoodReceipts()
    {
        var (f, old) = Closed();
        var save = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var replay = CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(
            restored);
        Assert.Equal(old.SourceCanonicalWorldHashHex,
            replay.SourceCanonicalWorldHashHex);
        Assert.Equal(old.ExecutedReceipts, replay.ExecutedReceipts);
        Assert.Equal(old.TotalActualLocalFoodRaw, replay.TotalActualLocalFoodRaw);
        Assert.Equal(old.TotalActualPaymentsMicrocredits,
            replay.TotalActualPaymentsMicrocredits);
        Assert.Throws<InvalidOperationException>(() =>
            old.RequireCurrent(restored));
        Assert.Equal(save, restored.ExportSaveJson());
    }

    [Fact]
    public void LastRealCycleProofCannotBeCapturedAfterSameTurnReassignment()
    {
        var f = Fixture();
        Assert.Equal(f.Session.State.MovementTurn,
            f.Session.State.Cycles[^1].MovementTurn);
        Assert.Equal("reassign", f.Session.Events[^1].Kind);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(
                f.Session));
    }

    [Fact]
    public void PhysicalMoveAfterCaptureInvalidatesOldExecutedPriceEvidence()
    {
        var (f, witness) = Closed();
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            witness.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            witness.VerifyIndependent());
    }

    [Fact]
    public void InstitutionalNetworkChangeInvalidatesOldExecutedPriceEvidence()
    {
        var (f, witness) = Closed();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            witness.RequireCurrent(f.Session));
    }

    [Fact]
    public void NonCashOrUnsettledModeNeverFabricatesPaidLocalPrice()
    {
        var legacy = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(
                legacy));
        legacy.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(
                legacy));
        var f = Fixture();
        // No new actual settlement after a worker reallocation.
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(
                f.Session));
    }

    [Fact]
    public void RepeatedReadOnlyCanonicalPriceEvidenceNeverMutatesWorldOrSave()
    {
        var (f, witness) = Closed();
        var before = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        for (var i = 0; i < 3; i++)
        {
            var other =
                CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(
                    f.Session);
            other.VerifyIndependent();
            Assert.Equal(witness.ExecutedReceipts, other.ExecutedReceipts);
            Assert.Equal(witness.TotalActualPaymentsMicrocredits,
                other.TotalActualPaymentsMicrocredits);
            Assert.Equal(hash, f.Session.CurrentHash);
            Assert.Equal(before, f.Session.ExportSaveJson());
        }
    }
}
