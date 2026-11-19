using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationFineFirstWorldBindingTests
{
    private static FoundationTacticalHumanAiTurnHost Host(ulong seed = 20261012UL) =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);

    private static FoundationFineFirstPhysicalGeography Fine(
        FoundationTacticalHumanAiTurnHost h) =>
        FoundationFineFirstPhysicalGeography.Generate(
            h.Session.GeneratedWorld, h.Incidence);

    private static FoundationNaturalTransportSeedingPolicy Freight() =>
        new(new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
            new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
            new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
            10_000UL);

    [Fact]
    public void ExplicitFineWorldBindingChangesStateHashAndCanDeployFineTransport()
    {
        var host = Host();
        var world = host.Session.GeneratedWorld;
        var fine = Fine(host);
        var identity = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            world, host.Incidence, fine);
        var legacy = host.Snapshot;
        var previousHash = new CanonicalWorldStateHasher().Compute(legacy);
        Assert.False(legacy.WorldBinding!.IsFineFirstWorld);

        var promoted = legacy.PromoteFineFirstBeforeTransport(
            world, host.Incidence, fine, identity);
        Assert.True(promoted.WorldBinding!.IsFineFirstWorld);
        Assert.Equal(RuntimeWorldBinding.FineFirstSignatureFormatVersion,
            promoted.WorldBinding.WorldSignatureFormatVersion);
        Assert.Equal(identity.Sha256Hex,
            promoted.WorldBinding.WorldSignatureSha256Hex);
        Assert.Same(legacy.Workers, promoted.Workers);
        Assert.Equal(legacy.Revision, promoted.Revision);
        Assert.False(promoted.Transport.IsInitialized);
        Assert.NotEqual(previousHash,
            new CanonicalWorldStateHasher().Compute(promoted));

        var quote = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            world, host.Incidence, fine, Freight(),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        Assert.Equal(fine.LandTileCount, quote.LandTileCount);
        var deployed = promoted.DeployInitialFineFirstPhysicalTransport(
            quote.Transport, world, host.Incidence, fine, identity);
        Assert.True(deployed.Transport.IsInitialized);
        Assert.Equal(quote.Transport.Edges.ToArray(),
            deployed.Transport.Edges.ToArray());
        Assert.NotEqual(new CanonicalWorldStateHasher().Compute(promoted),
            new CanonicalWorldStateHasher().Compute(deployed));
        var replay = legacy.PromoteFineFirstBeforeTransport(
            world, host.Incidence, Fine(host), identity)
            .DeployInitialFineFirstPhysicalTransport(
                quote.Transport, world, host.Incidence, fine, identity);
        Assert.Equal(new CanonicalWorldStateHasher().Compute(deployed),
            new CanonicalWorldStateHasher().Compute(replay));
        Assert.Equal(previousHash,
            new CanonicalWorldStateHasher().Compute(legacy));
        Assert.Throws<InvalidOperationException>(() =>
            promoted.DeployInitialPhysicalTransport(
                quote.Transport, world, host.Incidence));
        Assert.Throws<InvalidOperationException>(() =>
            legacy.DeployInitialFineFirstPhysicalTransport(
                quote.Transport, world, host.Incidence, fine, identity));
    }

    [Fact]
    public void RefusesForeignOrActiveWorldsWithoutSilentlyRebindingThem()
    {
        var host = Host();
        var world = host.Session.GeneratedWorld;
        var fine = Fine(host);
        var identity = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            world, host.Incidence, fine);
        var legacy = host.Snapshot;

        var foreign = Host(20261013UL);
        var other = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            foreign.Session.GeneratedWorld, foreign.Incidence, Fine(foreign));
        Assert.Throws<InvalidOperationException>(() =>
            legacy.PromoteFineFirstBeforeTransport(
                world, host.Incidence, fine, other));
        Assert.Throws<InvalidOperationException>(() =>
            legacy.PromoteFineFirstBeforeTransport(
                foreign.Session.GeneratedWorld, foreign.Incidence,
                Fine(foreign), other));

        var previous = FoundationPhysicalNaturalTransportSeeder.Create(
            world, host.Incidence, Freight());
        var active = legacy.DeployInitialPhysicalTransport(
            previous.Transport, world, host.Incidence);
        Assert.Throws<InvalidOperationException>(() =>
            active.PromoteFineFirstBeforeTransport(
                world, host.Incidence, fine, identity));
        Assert.False(legacy.WorldBinding!.IsFineFirstWorld);
        Assert.False(legacy.Transport.IsInitialized);
    }
}
