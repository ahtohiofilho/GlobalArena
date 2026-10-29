using System.Security.Cryptography;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class G3J3dMixedDurableV9CommitTests
{
    private const ulong D = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly MilitaryUnitId Unit = new(9UL);
    private static readonly EconomicPointId Site = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly byte[] Key = SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes("ga-fixture-j3c-test-key-20261029"));
    private static readonly EconomicPrimitiveCommodityMapping Mapping =
        new(Food, Wood, Stone);

    private sealed class HostAuthority : IMilitaryMoveOrderAuthority
    {
        public bool Approved { get; set; } = true;
        public int Checks { get; private set; }
        public bool IsAuthorized(
            MilitaryMoveCommand command, WorldState world,
            SimulationContext context)
        {
            Checks++;
            return Approved && command.IssuingCivilizationId == Owner &&
                command.MilitaryUnitId == Unit && command.Id.Turn == context.Turn;
        }
    }

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(2, 0)));

    private static EconomicEcologicalCheckpoint Initial(WorldGenerationResult generated)
    {
        var fields = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var cell = generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            fields.WoodyVegetation.GetRawValue(id) > 0L &&
            fields.ExposedStone.GetRawValue(id) > 0L);

        var world = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(Site, Owner, cell, 4UL, new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Forestry, 4UL)
                })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(Site, 4UL)
            }))
            .WithWarfare(new WarfareRuntimeState(new[]
            {
                new MilitaryUnitRuntimeState(Unit, Owner, new StrategicCellId(1UL))
            }))
            .WithTransportFacilities(new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(42UL,
                    new PhysicalTacticalTileId(
                        new GoldbergParameters(3, 0), new StrategicCellId(7UL)),
                    true, new[]
                    {
                        new TransportFacilityTransferCapability(1UL, 2UL, 1UL)
                    })
            }))
            .WithRestoredRevision(13UL);

        var policy = new EconomicNaturalCapitalPolicy(
            100UL * D, 100UL * D, 0L, 0L, 0L);
        return new EconomicEcologicalCheckpoint(
            generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, policy),
            EconomicMaterialReserveBook.Empty, policy, Mapping);
    }

    private static HistoricalMilitaryMoveAuthorizationSeal Seal(
        WorldGenerationResult generated, byte[]? key = null, string id = "host-v1") =>
        new(generated, id, key ?? Key);

    private static SimulationContext Context(ulong turn = 1UL,
        ulong seed = 73UL) =>
        new(new TurnNumber(turn), new SimulationSeed(seed));

    private static MilitaryMoveCommand Move(
        WorldGenerationResult generated, ulong turn = 1UL, ulong sequence = 1UL)
    {
        var index = generated.StrategicSurfaceGraph.GetNodeIndex(
            new StrategicCellId(1UL));
        var neighbor = generated.StrategicSurfaceGraph.GetNeighborIndexes(index)[0];
        return new MilitaryMoveCommand(
            new CommandId(new TurnNumber(turn), sequence), Owner, Unit,
            generated.StrategicSurfaceGraph.GetCellId(neighbor));
    }

    private static byte[] Issue(
        HistoricalMilitaryMoveAuthorizationSeal seal,
        EconomicEcologicalCheckpoint initial,
        WorldGenerationResult generated, HostAuthority authority) =>
        seal.Issue(initial, initial.Sha256Hex, Context(),
            new ISimulationCommand[] { Move(generated) }, authority);

    private static EconomicDemandProfile[] Demand() =>
        new[] { new EconomicDemandProfile(Site, Food, 500_000L) };

    private static EconomicMaterialOriginHoldOrder[] Holds() =>
        new[] { new EconomicMaterialOriginHoldOrder(
            Site, Wood, EconomicActivityKind.Forestry, D / 100UL) };

    [Fact]
    public void HistoricalSignatureVerifiesAfterLivePermissionIsRevoked()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var authority = new HostAuthority();
        var seal = Seal(generated);
        var signed = Issue(seal, initial, generated, authority);
        Assert.Equal(1, authority.Checks);

        authority.Approved = false; // old consent cannot be re-decided by today's ACL
        var result = seal.Verify(initial, initial.Sha256Hex, Context(), signed);
        Assert.Equal(1, authority.Checks);
        Assert.Equal(9U, new CanonicalWorldStateHasher().Compute(
            result.Resolution.ResultingWorldState).FormatVersion);
        Assert.Single(result.Resolution.EventLog.Entries);
        Assert.True(result.Resolution.EventLog.Entries[0].WasExecuted);
        Assert.Equal(signed, IssueAfterReapproval(seal, initial, generated, authority));
    }

    private static byte[] IssueAfterReapproval(
        HistoricalMilitaryMoveAuthorizationSeal seal,
        EconomicEcologicalCheckpoint initial, WorldGenerationResult generated,
        HostAuthority authority)
    {
        authority.Approved = true;
        return Issue(seal, initial, generated, authority);
    }

    [Fact]
    public void IssuerNeverSealsDeniedOrInvalidMilitaryOrders()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var denied = new HostAuthority { Approved = false };
        Assert.Throws<UnauthorizedAccessException>(() =>
            Issue(seal, initial, generated, denied));
        Assert.Equal(1, denied.Checks);

        var approved = new HostAuthority();
        Assert.Throws<InvalidOperationException>(() =>
            seal.Issue(initial, initial.Sha256Hex, Context(),
                new ISimulationCommand[] { Move(generated, turn: 2UL) },
                approved));
        Assert.Throws<ArgumentException>(() =>
            seal.Issue(initial, initial.Sha256Hex, Context(),
                Array.Empty<ISimulationCommand>(), approved));
    }

    [Fact]
    public void TrustedContextKeyAndWorldCannotBeSwapped()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var signed = Issue(seal, initial, generated, new HostAuthority());

        Assert.Throws<InvalidOperationException>(() =>
            seal.Verify(initial, initial.Sha256Hex, Context(seed: 74UL), signed));
        Assert.Throws<InvalidOperationException>(() =>
            seal.Verify(initial, initial.Sha256Hex, Context(turn: 2UL), signed));
        Assert.Throws<InvalidOperationException>(() =>
            seal.Verify(initial, new string('A', 64), Context(), signed));

        var badKey = SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("different-host-secret"));
        Assert.Throws<InvalidDataException>(() =>
            Seal(generated, badKey).Verify(
                initial, initial.Sha256Hex, Context(), signed));
        Assert.Throws<InvalidOperationException>(() =>
            Seal(generated, id: "retired-key-id").Verify(
                initial, initial.Sha256Hex, Context(), signed));
        Assert.Throws<InvalidOperationException>(() =>
            Seal(Generate(1UL)).Verify(
                initial, initial.Sha256Hex, Context(), signed));
    }

    [Fact]
    public void CertificateTamperingOrTruncationCannotSurviveRecomputedChecksum()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var signed = Issue(seal, initial, generated, new HostAuthority());
        var damaged = (byte[])signed.Clone();
        damaged[damaged.Length / 2] ^= 0x01;
        Assert.Throws<InvalidDataException>(() =>
            seal.Verify(initial, initial.Sha256Hex, Context(), damaged));
        Assert.Throws<InvalidDataException>(() =>
            seal.Verify(initial, initial.Sha256Hex, Context(), signed[..^1]));

        var rehashed = (byte[])signed.Clone();
        rehashed[12 + 4 + "host-v1".Length + 8 + 7] ^= 0x01;
        SHA256.HashData(rehashed.AsSpan(0, rehashed.Length - 32))
            .CopyTo(rehashed, rehashed.Length - 32);
        Assert.Throws<InvalidDataException>(() =>
            seal.Verify(initial, initial.Sha256Hex, Context(), rehashed));

        Assert.Throws<ArgumentException>(() =>
            Seal(generated, new byte[4]));
    }

    [Fact]
    public void MixedCandidateContainsActualMilitaryWorldAndEcologicalStocks()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var signed = Issue(seal, initial, generated, new HostAuthority());
        var projector = new HistoricalMilitaryEcologicalTurnProjector(
            generated, seal);
        var candidate = projector.Propose(initial, initial.Sha256Hex,
            Context(), signed, Demand(), Holds());

        var loaded = EconomicCompleteV9SaveBundleCodec.Decode(
            generated, candidate.CompleteV9Bundle);
        Assert.Equal(9U, loaded.WorldHash.FormatVersion);
        Assert.Equal(candidate.ProposedCheckpointSha256Hex, loaded.Sha256Hex);
        Assert.NotEqual(initial.WorldHash, loaded.WorldHash);
        Assert.NotEqual(initial.Sha256Hex, loaded.Sha256Hex);
        Assert.Equal(14UL, loaded.World.Revision);
        Assert.Equal(Move(generated).DestinationStrategicCellId,
            Assert.Single(loaded.World.Warfare.Units).StrategicCellId);
        Assert.Equal(new StrategicCellId(1UL),
            Assert.Single(initial.World.Warfare.Units).StrategicCellId);
        Assert.Equal(1UL, loaded.NaturalCapital.LastAppliedTurn);
        Assert.Equal(1UL, loaded.MaterialReserves.LastAppliedTurn(Owner));
        Assert.Equal(D / 100UL, loaded.MaterialReserves.RawAvailable(Site, Wood));
        Assert.Equal(0UL, initial.NaturalCapital.LastAppliedTurn);
        Assert.Empty(initial.MaterialReserves.Balances);
        var facility = Assert.Single(loaded.World.TransportFacilities.Facilities);
        Assert.Equal(42UL, facility.FacilityId);
        Assert.True(facility.CanTransfer(1UL, 2UL, 1UL));
        Assert.Equal(4UL, Assert.Single(loaded.World.Economy.EconomicPoints).Workforce);
        Assert.Equal(4UL, Assert.Single(loaded.World.Population.PointPopulations).Residents);
        Assert.Equal(candidate.CompleteV9Bundle,
            EconomicCompleteV9SaveBundleCodec.Encode(loaded));
    }

    [Fact]
    public void MixedCandidateIsDeterministicAndCannotAdvanceTwoTurns()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var signed = Issue(seal, initial, generated, new HostAuthority());
        var projector = new HistoricalMilitaryEcologicalTurnProjector(
            generated, seal);
        var one = projector.Propose(initial, initial.Sha256Hex,
            Context(), signed, Demand(), Holds());
        var two = projector.Propose(initial, initial.Sha256Hex,
            Context(), signed, Demand(), Holds());
        Assert.Equal(one.CompleteV9Bundle, two.CompleteV9Bundle);
        Assert.Equal(one.ProposedCheckpointSha256Hex, two.ProposedCheckpointSha256Hex);
        Assert.Throws<InvalidOperationException>(() =>
            projector.Propose(initial, initial.Sha256Hex,
                Context(turn: 2UL), signed, Demand(), Holds()));
        Assert.Throws<InvalidOperationException>(() =>
            projector.Propose(initial, new string('F', 64),
                Context(), signed, Demand(), Holds()));
    }

    [Fact]
    public void CertifiedCandidateCannotPublishOrInventADurableTurn()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var signed = Issue(seal, initial, generated, new HostAuthority());
        var root = Path.Combine(Path.GetTempPath(),
            "ga-j3c-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var host = EconomicCompleteV9DurableSaveHost.Create(
                root, generated, initial);
            var head = Path.Combine(root, "full-v9-head.gahead");
            var before = File.ReadAllBytes(head);
            var candidate = new HistoricalMilitaryEcologicalTurnProjector(
                generated, seal).Propose(initial, initial.Sha256Hex,
                Context(), signed, Demand(), Holds());
            Assert.NotEqual(initial.Sha256Hex, candidate.ProposedCheckpointSha256Hex);
            Assert.Equal(before, File.ReadAllBytes(head));
            Assert.Equal(initial.Sha256Hex, host.AcceptedSha256Hex);
            Assert.Equal(0UL, host.CommitSequence);
            Assert.Empty(Directory.GetFiles(root, "*.gajournal"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MixedTurnHasOneDurableHeadAndReplaysAfterRestart()
    {
        var generated = Generate();
        var previous = Initial(generated);
        var seal = Seal(generated);
        var certificate = Issue(seal, previous, generated, new HostAuthority());
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3d-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string accepted;
            string trustedRoot;
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, previous))
            {
                var result = host.CommitMixed(previous.Sha256Hex, Context(),
                    certificate, Demand(), Holds(), seal);
                accepted = result.ProposedCheckpointSha256Hex;
                trustedRoot = host.AcceptedJournalSha256Hex;
                Assert.Equal(1UL, host.CommitSequence);
                Assert.Equal(1UL, host.LastAppliedTurn);
                Assert.NotEqual(previous.WorldHash,
                    result.EconomicProjection.ProposedNext.WorldHash);
                Assert.Single(Directory.GetFiles(folder, "*.gamixed"));
                Assert.Empty(Directory.GetFiles(folder, "*.gajournal"));
                var loaded = EconomicCompleteV9SaveBundleCodec.Decode(
                    generated, host.CaptureCompleteSave());
                Assert.Equal(accepted, loaded.Sha256Hex);
                Assert.Equal(Move(generated).DestinationStrategicCellId,
                    Assert.Single(loaded.World.Warfare.Units).StrategicCellId);
                Assert.Equal(D / 100UL,
                    loaded.MaterialReserves.RawAvailable(Site, Wood));
                Assert.Equal(42UL,
                    Assert.Single(loaded.World.TransportFacilities.Facilities).FacilityId);
            }

            using (var host = EconomicCompleteV9DurableSaveHost.Restore(
                folder, generated, accepted,
                historicalVerifier: seal,
                independentlyTrustedJournalRoot: trustedRoot))
            {
                var next = host.CommitNext(accepted, 2UL, Demand(), Holds());
                accepted = next.ProposedNext.Sha256Hex;
                trustedRoot = host.AcceptedJournalSha256Hex;
                Assert.Equal(2UL, host.CommitSequence);
            }
            using var restored = EconomicCompleteV9DurableSaveHost.Restore(
                folder, generated, accepted,
                historicalVerifier: seal,
                independentlyTrustedJournalRoot: trustedRoot);
            Assert.Equal(2UL, restored.LastAppliedTurn);
            Assert.Equal(2UL, restored.CommitSequence);
            Assert.Single(Directory.GetFiles(folder, "*.gamixed"));
            Assert.Single(Directory.GetFiles(folder, "*.gajournal"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void MixedRestoreNeedsHistoricalKeyAndIndependentJournalRoot()
    {
        var generated = Generate();
        var previous = Initial(generated);
        var seal = Seal(generated);
        var certificate = Issue(seal, previous, generated, new HostAuthority());
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3d-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string accepted;
            string root;
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, previous))
            {
                accepted = host.CommitMixed(previous.Sha256Hex, Context(),
                    certificate, Demand(), Holds(), seal).ProposedCheckpointSha256Hex;
                root = host.AcceptedJournalSha256Hex;
            }
            Assert.Throws<InvalidOperationException>(() =>
                EconomicCompleteV9DurableSaveHost.Restore(folder, generated, accepted));
            Assert.Throws<InvalidOperationException>(() =>
                EconomicCompleteV9DurableSaveHost.Restore(
                    folder, generated, accepted, historicalVerifier: seal));
            Assert.Throws<InvalidOperationException>(() =>
                EconomicCompleteV9DurableSaveHost.Restore(
                    folder, generated, accepted,
                    historicalVerifier: seal,
                    independentlyTrustedJournalRoot: new string('A', 64)));
            Assert.Throws<InvalidDataException>(() =>
                EconomicCompleteV9DurableSaveHost.Restore(
                    folder, generated, accepted,
                    historicalVerifier: Seal(generated,
                        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                            "unrelated-host-key"))),
                    independentlyTrustedJournalRoot: root));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void CorruptedOrMissingMixedJournalFailsClosedDuringRecovery()
    {
        var generated = Generate();
        var previous = Initial(generated);
        var seal = Seal(generated);
        var certificate = Issue(seal, previous, generated, new HostAuthority());
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3d-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string accepted;
            string root;
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, previous))
            {
                accepted = host.CommitMixed(previous.Sha256Hex, Context(),
                    certificate, Demand(), Holds(), seal).ProposedCheckpointSha256Hex;
                root = host.AcceptedJournalSha256Hex;
            }
            var archive = new EconomicMixedV9TransitionArchive(folder);
            var path = archive.RecordPath(accepted);
            var bytes = File.ReadAllBytes(path);
            var damaged = (byte[])bytes.Clone();
            damaged[damaged.Length / 2] ^= 1;
            File.WriteAllBytes(path, damaged);
            Assert.Throws<InvalidDataException>(() =>
                EconomicCompleteV9DurableSaveHost.Restore(
                    folder, generated, accepted,
                    historicalVerifier: seal,
                    independentlyTrustedJournalRoot: root));
            File.Delete(path);
            Assert.Throws<InvalidDataException>(() =>
                EconomicCompleteV9DurableSaveHost.Restore(
                    folder, generated, accepted,
                    historicalVerifier: seal,
                    independentlyTrustedJournalRoot: root));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void StaleOrInvalidMixedTransitionNeverPublishesAnotherHead()
    {
        var generated = Generate();
        var previous = Initial(generated);
        var seal = Seal(generated);
        var certificate = Issue(seal, previous, generated, new HostAuthority());
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3d-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, previous);
            var head = Path.Combine(folder, "full-v9-head.gahead");
            var before = File.ReadAllBytes(head);
            Assert.Throws<InvalidOperationException>(() =>
                host.CommitMixed(previous.Sha256Hex,
                    Context(turn: 2UL), certificate, Demand(), Holds(), seal));
            Assert.Equal(before, File.ReadAllBytes(head));
            Assert.Throws<InvalidOperationException>(() =>
                host.CommitMixed(new string('F', 64),
                    Context(), certificate, Demand(), Holds(), seal));
            Assert.Equal(before, File.ReadAllBytes(head));
            var result = host.CommitMixed(previous.Sha256Hex, Context(),
                certificate, Demand(), Holds(), seal);
            Assert.Equal(1UL, host.CommitSequence);
            Assert.Throws<InvalidOperationException>(() =>
                host.CommitMixed(previous.Sha256Hex, Context(),
                    certificate, Demand(), Holds(), seal));
            Assert.Equal(result.ProposedCheckpointSha256Hex,
                host.AcceptedSha256Hex);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void UnpublishedMixedCandidateAndJournalAreNeverAccepted()
    {
        var generated = Generate();
        var previous = Initial(generated);
        var seal = Seal(generated);
        var certificate = Issue(seal, previous, generated, new HostAuthority());
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3d-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var candidate = new HistoricalMilitaryEcologicalTurnProjector(
                generated, seal).Propose(previous, previous.Sha256Hex,
                Context(), certificate, Demand(), Holds());
            var predicted = candidate.EconomicProjection.ProposedNext;
            var bundleArchive = new EconomicCompleteV9SaveBundleArchive(folder);
            var journalArchive = new EconomicMixedV9TransitionArchive(folder);
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, previous))
            {
                bundleArchive.Store(predicted);
                journalArchive.Store(new EconomicMixedV9TransitionRecord(
                    1UL, Context().Seed.Value, previous.Sha256Hex,
                    predicted.Sha256Hex, host.AcceptedJournalSha256Hex,
                    certificate, Demand(), Holds()));
                Assert.Equal(previous.Sha256Hex, host.AcceptedSha256Hex);
                Assert.Equal(0UL, host.CommitSequence);
            }
            using var restored = EconomicCompleteV9DurableSaveHost.Restore(
                folder, generated, previous.Sha256Hex);
            Assert.Equal(0UL, restored.LastAppliedTurn);
            var committed = restored.CommitMixed(previous.Sha256Hex,
                Context(), certificate, Demand(), Holds(), seal);
            Assert.Equal(predicted.Sha256Hex,
                committed.ProposedCheckpointSha256Hex);
            Assert.Equal(1UL, restored.CommitSequence);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
