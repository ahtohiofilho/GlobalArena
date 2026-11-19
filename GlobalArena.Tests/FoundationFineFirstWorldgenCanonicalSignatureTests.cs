using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationFineFirstWorldgenCanonicalSignatureTests
{
    private static FoundationTacticalHumanAiTurnHost Host(ulong seed = 20261012UL) =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationFineFirstPhysicalGeography Geography(
        FoundationTacticalHumanAiTurnHost host) =>
        FoundationFineFirstPhysicalGeography.Generate(
            host.Session.GeneratedWorld, host.Incidence);

    [Fact]
    public void SignedTacticalGeographyIsDeterministicAndDistinctFromLegacyWorld()
    {
        var host = Host();
        var fine = Geography(host);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            host.Session.GeneratedWorld, host.Incidence, fine);
        var replay = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            host.Session.GeneratedWorld, host.Incidence, Geography(host));
        var legacy = WorldGenerationCanonicalSignature.Compute(
            host.Session.GeneratedWorld);

        Assert.Equal(1, signature.FormatVersion);
        Assert.Equal(64, signature.Sha256Hex.Length);
        Assert.Matches("^[0-9a-f]{64}$", signature.Sha256Hex);
        Assert.Equal(signature, replay);
        Assert.Equal(legacy, signature.LegacyStrategicSignature);
        Assert.NotEqual(legacy.Sha256Hex, signature.Sha256Hex);
        Assert.Equal(fine.FineElevationRaw.Count, signature.FineTileCount);
        FoundationFineFirstWorldgenCanonicalSignature.RequireExact(
            signature, host.Session.GeneratedWorld, host.Incidence, fine);

        var foreign = Host(20261013UL);
        var other = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            foreign.Session.GeneratedWorld, foreign.Incidence,
            Geography(foreign));
        Assert.NotEqual(signature.Sha256Hex, other.Sha256Hex);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFineFirstWorldgenCanonicalSignature.RequireExact(
                other, host.Session.GeneratedWorld, host.Incidence, fine));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFineFirstWorldgenCanonicalSignature.RequireExact(
                signature with { FormatVersion = 999 },
                host.Session.GeneratedWorld, host.Incidence, fine));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFineFirstWorldgenCanonicalSignature.RequireExact(
                signature with { Sha256Hex = legacy.Sha256Hex },
                host.Session.GeneratedWorld, host.Incidence, fine));
    }

    [Fact]
    public void MigrationAdmissionDoesNotMutateOldHashesAndRejectsLiveEconomy()
    {
        var host = Host();
        var original = host.Snapshot;
        var oldHash = host.StateHash;
        var fields = Geography(host);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            host.Session.GeneratedWorld, host.Incidence, fields);
        var admitted = FoundationFineFirstMigrationAdmission.InspectBeforeMigration(
            original, host.Session.GeneratedWorld, host.Incidence,
            fields, signature);
        Assert.Equal(original.Revision, admitted.ExistingRevision);
        Assert.Equal(fields.FineElevationRaw.Count, admitted.FineTileCount);
        Assert.Equal(original.WorldBinding, admitted.ExistingLegacyBinding);
        Assert.Same(original, host.Snapshot);
        Assert.Equal(oldHash, host.StateHash);

        var policy = new FoundationNaturalTransportSeedingPolicy(
            new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
            new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
            new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
            10_000UL);
        var deployed = original.DeployInitialPhysicalTransport(
            FoundationPhysicalNaturalTransportSeeder.Create(
                host.Session.GeneratedWorld, host.Incidence, policy).Transport,
            host.Session.GeneratedWorld, host.Incidence);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFineFirstMigrationAdmission.InspectBeforeMigration(
                deployed, host.Session.GeneratedWorld, host.Incidence,
                fields, signature));
        var foreign = Host(20261013UL);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFineFirstMigrationAdmission.InspectBeforeMigration(
                original, foreign.Session.GeneratedWorld, foreign.Incidence,
                Geography(foreign),
                FoundationFineFirstWorldgenCanonicalSignature.Compute(
                    foreign.Session.GeneratedWorld, foreign.Incidence,
                    Geography(foreign))));
        Assert.Same(original, host.Snapshot);
        Assert.Equal(oldHash, host.StateHash);
    }
}
