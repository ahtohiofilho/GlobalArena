using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs86PriceHistorySidecarTests
{
    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static CanonicalPrimitiveSiteDiscoveryPolicy Policy() => new(
        new EconomicPrimitiveCommodityMapping(
            new CommodityId(7), new CommodityId(8), new CommodityId(9)),
        new[] {
            new CanonicalPrimitiveDemandRate(new CommodityId(7),
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(new CommodityId(8), 125_000),
            new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static (CanonicalPhysicalTacticalSession Session, string Sidecar)
        Build()
    {
        var (fixture, _, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var session = fixture.Session;
        // Seal the same-turn reallocation before taking the VS85 price
        // witness, and make the subsequent real cycle independently.
        Assert.NotNull(session.AdvanceMovementTurn());
        var sites = session.DiscoverPrimitiveEconomicSites(Policy());
        var freight = CanonicalSimultaneousFreightReference.FromSites(
            session, sites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        var rates = sites.Flows.SupplyFlows
            .Where(x => x.RawQuantity > 0)
            .Select(x => x.Activity).Distinct().Select(x =>
                new CanonicalWorkforceActivityRealizationPolicy(x, 500, 100));
        var before = CanonicalWorkforceRealizationProjection.FromSites(
            session, sites, freight, rates,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var feedback = CanonicalLaggedProductionResponse.FromFrozenProjection(
            session, sites, freight, before, 100,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var receipt = CanonicalCycleStampedPriceReceipt.Capture(
            session, feedback);
        Assert.NotNull(session.AdvanceMovementTurn());
        var nextSites = session.DiscoverPrimitiveEconomicSites(Policy());
        var nextFreight = CanonicalSimultaneousFreightReference.FromSites(
            session, nextSites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        var proof = receipt.ApplyAfterNextSettledCycle(
            session, nextSites, nextFreight,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var old = session.ExportSaveJson();
        var sidecar = CanonicalEconomicPriceHistorySidecar.Export(session, proof);
        Assert.Equal(old, session.ExportSaveJson());
        return (session, sidecar);
    }

    private static string ResignSidecar(
        string json, Func<CanonicalEconomicPriceHistoryPayload,
            CanonicalEconomicPriceHistoryPayload> mutate)
    {
        var envelope = JsonSerializer.Deserialize<
            CanonicalEconomicPriceHistoryEnvelope>(json)!;
        var payload = JsonSerializer.Deserialize<
            CanonicalEconomicPriceHistoryPayload>(envelope.PayloadJson)!;
        var rewritten = JsonSerializer.Serialize(mutate(payload));
        return JsonSerializer.Serialize(new CanonicalEconomicPriceHistoryEnvelope(
            envelope.Version, rewritten,
            Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(rewritten)))));
    }

    [Fact]
    public void VerifiedPriceObservationSidecarSurvivesCanonicalSaveRestorationReadOnly()
    {
        var (session, sidecar) = Build();
        var save = session.ExportSaveJson();
        var a = CanonicalEconomicPriceHistorySidecar.Inspect(session, sidecar);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var b = CanonicalEconomicPriceHistorySidecar.Inspect(restored, sidecar);
        Assert.Equal(a.Observations, b.Observations);
        Assert.Equal(a.SourceMovementTurn, b.SourceMovementTurn);
        Assert.Equal(a.TargetMovementTurn, b.TargetMovementTurn);
        Assert.True(b.HasValidCanonicalChronology);
        Assert.False(b.SidecarChecksumIsDigitalSignature);
        Assert.False(b.ConfirmsPricesWereActuallyPaid);
        Assert.False(b.IndependentlyProvesArchivedPriceQuotation);
        Assert.False(b.CanApplyProductionOrFreight);
        Assert.False(b.CanAuthorizeRealSettlement);
        Assert.False(b.ModifiesCanonicalSave);
        Assert.All(b.Observations, x => {
            Assert.True(BigInteger.Parse(x.PreviousLocallyAbsorbedRaw) > 0);
            Assert.True(BigInteger.Parse(x.HypotheticalPriceDenominator) > 0);
            Assert.InRange(x.NextUtilizationPermille, 0, 1000);
        });
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(save, restored.ExportSaveJson());
    }

    [Fact]
    public void SameLiveEvidenceExportsExactlyTheSameDeterministicSidecar()
    {
        var (session, sidecar) = Build();
        var fromJson = JsonSerializer.Deserialize<
            CanonicalEconomicPriceHistoryEnvelope>(sidecar)!;
        var payload = JsonSerializer.Deserialize<
            CanonicalEconomicPriceHistoryPayload>(fromJson.PayloadJson)!;
        Assert.Equal(1, payload.Version);
        Assert.Equal(1, payload.MaximumResponseStepPermille / 100);
        Assert.Equal(payload.SourceEconomicCycleCount + 1,
            payload.TargetEconomicCycleCount);
        Assert.Equal(payload.SourceMovementTurn + 1UL,
            payload.TargetMovementTurn);
        Assert.Equal(session.Events.Count, payload.TargetActionCount);
        Assert.NotEqual(payload.SourceWorldHashHex, payload.TargetWorldHashHex);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(fromJson.PayloadJson))),
            fromJson.PayloadSha256Hex);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            session.ExportSaveJson());
        var second = CanonicalEconomicPriceHistorySidecar.Inspect(
            restored, sidecar);
        Assert.Equal(payload.Signals, second.Observations);
    }

    [Fact]
    public void UnresignedByteMutationFailsChecksumAndCanonicalPayloadCheck()
    {
        var (session, sidecar) = Build();
        var changed = sidecar.Replace(
            "PreviousLocallyAbsorbedRaw", "PreviousLocallyAbsorbedRAW",
            StringComparison.Ordinal);
        Assert.NotEqual(sidecar, changed);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, changed));
        var envelope = JsonSerializer.Deserialize<
            CanonicalEconomicPriceHistoryEnvelope>(sidecar)!;
        var injected = JsonSerializer.Serialize(new
            CanonicalEconomicPriceHistoryEnvelope(
                envelope.Version,
                envelope.PayloadJson.Replace("\"Version\":1,",
                    "\"Version\":1,\"Extra\":2,", StringComparison.Ordinal),
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    envelope.PayloadJson.Replace("\"Version\":1,",
                        "\"Version\":1,\"Extra\":2,", StringComparison.Ordinal))))));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, injected));
    }

    [Fact]
    public void ResignedForgedRatePriceOrDuplicateActivityFailsMathematicalAudit()
    {
        var (session, sidecar) = Build();
        var alteredGain = ResignSidecar(sidecar, x =>
            x with { MaximumResponseStepPermille = 251 });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, alteredGain));
        var alteredPrice = ResignSidecar(sidecar, x => x with {
            Signals = x.Signals.Select((signal, i) => i == 0
                ? signal with { HypotheticalPriceDenominator = "0" }
                : signal).ToArray()
        });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, alteredPrice));
        var forgedDelta = ResignSidecar(sidecar, x => x with {
            Signals = x.Signals.Select((signal, i) => i == 0
                ? signal with { ResponseDeltaPermille = 999 }
                : signal).ToArray()
        });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, forgedDelta));
        var duplicate = ResignSidecar(sidecar, x => x with {
            Signals = new[] { x.Signals[0], x.Signals[0] }
        });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, duplicate));
    }

    [Fact]
    public void RehashedForgedChronologyAndSourceHashFailAgainstRealEventJournal()
    {
        var (session, sidecar) = Build();
        var forged = ResignSidecar(sidecar, x =>
            x with { SourceActionCount = x.SourceActionCount - 1 });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, forged));
        var forgedSource = ResignSidecar(sidecar, x =>
            x with { SourceWorldHashHex = new string('A', 64) });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, forgedSource));
        var forgedCycle = ResignSidecar(sidecar, x =>
            x with { SourceEconomicCycleCount = x.SourceEconomicCycleCount + 1 });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, forgedCycle));
    }

    [Fact]
    public void NextMovementAndForeignSaveInvalidateHistoricalObservationInspection()
    {
        var (session, sidecar) = Build();
        session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, sidecar));
        var (unrelated, _) = Build();
        unrelated.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(unrelated, sidecar));
    }

    [Fact]
    public void InvalidOversizedOrEmptySidecarCannotBeReadAsMarketAuthority()
    {
        var (session, sidecar) = Build();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session, "{}"));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(
                session, new string('x',
                    CanonicalEconomicPriceHistorySidecar.MaximumSidecarBytes + 1)));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalEconomicPriceHistorySidecar.Inspect(session,
                sidecar.Substring(0, sidecar.Length - 2)));
    }
}
