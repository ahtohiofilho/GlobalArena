using System.Numerics;
using GlobalArena.Economy;

namespace GlobalArena.Tests;

public sealed class GaVs112PhysicalFinalOutputPreflightTests
{
    private static PhysicalProductionClaim Origin(
        ulong id, uint commodity, ulong amount, ulong source = 10UL,
        ulong cycle = 2UL) => new(id, cycle, source, commodity, amount);

    private static PhysicalDispositionClaim Flow(
        ulong id, ulong origin, uint commodity, ulong amount,
        PhysicalOutputDispositionRole kind, ulong destination = 10UL,
        ulong cycle = 2UL) => new(id, cycle, origin, destination, commodity, amount, kind);

    private static PhysicalCommodityUnitScale[] Scales() =>
        [new(2U, 100UL), new(1U, 10UL)];

    [Fact]
    public void ConservesProducedMaterialAndExcludesIntermediateAndRetention()
    {
        var preflight = PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 10,
            [Origin(101, 1, 100), Origin(102, 2, 250, source: 20)],
            [
                Flow(1, 101, 1, 20, PhysicalOutputDispositionRole.FinalLocalUse),
                Flow(2, 101, 1, 30, PhysicalOutputDispositionRole.FinalRemoteUse, 21),
                Flow(3, 101, 1, 40, PhysicalOutputDispositionRole.IntermediateInput, 21),
                Flow(4, 101, 1, 10, PhysicalOutputDispositionRole.ExplicitRetention),
                Flow(5, 102, 2, 250, PhysicalOutputDispositionRole.FinalRemoteUse, 10)
            ], Scales());

        Assert.Equal(2UL, preflight.ClaimedCompletedCycle);
        Assert.Equal(10UL, preflight.ClaimedPopulation);
        Assert.Equal<RealFinalOutputRow>(
            new[] { new RealFinalOutputRow(1U, 5m), new RealFinalOutputRow(2U, 2.5m) },
            preflight.CandidateFinalOutput);
        Assert.Equal(new BigInteger(40), preflight.Commodities[0].IntermediateRaw);
        Assert.Equal(new BigInteger(10), preflight.Commodities[0].RetainedRaw);
        Assert.False(preflight.HasCanonicalLedgerAttestation);
        Assert.False(preflight.AuthenticatesBaseUnitPrices);
        Assert.False(preflight.CanAuthorizePurchasingPower);
        Assert.False(preflight.ChangesPhysicalState);
    }

    [Fact]
    public void UnorderedInputsAndModifiedCallerArraysCannotMutateOutput()
    {
        var produced = new[] { Origin(2, 2, 250, 20), Origin(1, 1, 100) };
        var distributed = new[] {
            Flow(2, 2, 2, 250, PhysicalOutputDispositionRole.FinalRemoteUse, 10),
            Flow(1, 1, 1, 100, PhysicalOutputDispositionRole.FinalLocalUse)
        };
        var preflight = PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(
            2, 12, produced, distributed, Scales());
        produced[0] = Origin(55, 2, 1);
        distributed[0] = Flow(44, 1, 1, 5, PhysicalOutputDispositionRole.FinalLocalUse);
        Assert.Equal(1U, preflight.CandidateFinalOutput[0].Commodity);
        Assert.Equal(10m, preflight.CandidateFinalOutput[0].Quantity);
        Assert.Equal(2U, preflight.CandidateFinalOutput[1].Commodity);
        Assert.Equal(2.5m, preflight.CandidateFinalOutput[1].Quantity);
    }

    [Fact]
    public void DuplicateReceiptIdentityOrExcessDispositionsAreRejected()
    {
        var origins = new[] { Origin(1, 1, 10) };
        Assert.Throws<ArgumentException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3, origins,
                [Flow(9, 1, 1, 5, PhysicalOutputDispositionRole.FinalLocalUse),
                 Flow(9, 1, 1, 5, PhysicalOutputDispositionRole.FinalLocalUse)], Scales()));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3, origins,
                [Flow(7, 1, 1, 10, PhysicalOutputDispositionRole.FinalLocalUse),
                 Flow(8, 1, 1, 1, PhysicalOutputDispositionRole.FinalLocalUse)], Scales()));
        Assert.Throws<ArgumentException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3,
                [Origin(1, 1, 10), Origin(1, 1, 20)],
                [Flow(1, 1, 1, 10, PhysicalOutputDispositionRole.FinalLocalUse)], Scales()));
    }

    [Fact]
    public void MissingDestinationIsNotHiddenStock()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3,
                [Origin(1, 1, 10)],
                [Flow(1, 1, 1, 9, PhysicalOutputDispositionRole.FinalLocalUse)], Scales()));
    }

    [Fact]
    public void UnknownOriginCommodityOrDestinationClassificationIsRejected()
    {
        var origin = new[] { Origin(1, 1, 10) };
        var invalid = new[] {
            Flow(1, 2, 1, 10, PhysicalOutputDispositionRole.FinalLocalUse),
            Flow(1, 1, 2, 10, PhysicalOutputDispositionRole.FinalLocalUse),
            Flow(1, 1, 1, 10, PhysicalOutputDispositionRole.FinalRemoteUse),
            Flow(1, 1, 1, 10, PhysicalOutputDispositionRole.FinalLocalUse, 20),
            Flow(1, 1, 1, 10, PhysicalOutputDispositionRole.ExplicitRetention, 20),
            Flow(1, 1, 1, 10, (PhysicalOutputDispositionRole)99),
            Flow(1, 1, 1, 10, PhysicalOutputDispositionRole.FinalLocalUse, cycle: 3),
            Flow(1, 1, 1, 0, PhysicalOutputDispositionRole.FinalLocalUse)
        };
        foreach (var claim in invalid)
            Assert.Throws<ArgumentException>(() =>
                PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3, origin,
                    [claim], Scales()));
    }

    [Fact]
    public void CrossCycleAndInvalidScalingOrPopulationFailClosed()
    {
        Assert.Throws<ArgumentException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3,
                [Origin(1, 1, 10, cycle: 1)], [], Scales()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 0, [], [], Scales()));
        Assert.Throws<ArgumentException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3,
                [Origin(1, 3, 10)], [], Scales()));
        Assert.Throws<ArgumentException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3, [], [],
                [new PhysicalCommodityUnitScale(1, 1), new PhysicalCommodityUnitScale(1, 2)]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 3, [], [],
                [new PhysicalCommodityUnitScale(1, 0)]));
    }

    [Fact]
    public void NoPhysicalFinalOutputIsZeroObservationNotFabricatedN()
    {
        var preflight = PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 10,
            [Origin(1, 1, 30)],
            [Flow(1, 1, 1, 30, PhysicalOutputDispositionRole.IntermediateInput)],
            Scales());
        Assert.All(preflight.CandidateFinalOutput, row => Assert.Equal(0m, row.Quantity));
        Assert.False(preflight.CanAuthorizePurchasingPower);
        var untrustedObservation = RealCompletedCycleObservation.FromPhysicalObservations(
            preflight.ClaimedCompletedCycle, preflight.ClaimedPopulation,
            preflight.CandidateFinalOutput);
        Assert.Throws<InvalidOperationException>(() =>
            RealPurchasingPowerDerivation.FreezeBase(untrustedObservation,
                [new FrozenBaseUnitValuation(1, 1m), new FrozenBaseUnitValuation(2, 1m)]));
    }

    [Fact]
    public void ScalesArePerCommodityAndNoImplicitPriceWeightsAreUsed()
    {
        var result = PhysicalFinalOutputEvidenceAdapter.ReconcileClaims(2, 100_000_000,
            [Origin(1, 1, 150), Origin(2, 2, 400, 20)],
            [Flow(1, 1, 1, 150, PhysicalOutputDispositionRole.FinalLocalUse),
             Flow(2, 2, 2, 400, PhysicalOutputDispositionRole.FinalLocalUse, 20)],
            Scales());
        Assert.Equal(15m, result.CandidateFinalOutput[0].Quantity);
        Assert.Equal(4m, result.CandidateFinalOutput[1].Quantity);
    }
}
