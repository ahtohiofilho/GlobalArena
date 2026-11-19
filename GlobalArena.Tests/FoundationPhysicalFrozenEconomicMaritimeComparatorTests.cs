using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// R73: exhaustive economic quote is the reference, NOT the old R55
/// experimental cost curve. The same seller/commodity/frozen world must
/// yield the same globally preferred mode and exact pair whenever sea wins.
/// </summary>
public sealed class FoundationPhysicalFrozenEconomicMaritimeComparatorTests
{
    private readonly ITestOutputHelper _output;
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Commodity = new(1U);
    private static readonly FoundationCommodityCargoConversionPolicy Cargo =
        new("r73-whole-cargo", new[]
        {
            new FoundationCommodityCargoConversion(Commodity, 1_000_000UL)
        });

    public FoundationPhysicalFrozenEconomicMaritimeComparatorTests(
        ITestOutputHelper output) => _output = output;

    private static (FoundationTacticalHumanAiTurnHost Host, WorldState Snapshot,
        PhysicalTacticalTileId From, PhysicalTacticalTileId To,
        PhysicalTacticalEdgeId FundedEdge) Build(int m)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(m, 0)),
            workersPerCivilization: 3);
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(
                    1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(
                    5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(
                    25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var state = host.Snapshot.DeployInitialPhysicalTransport(
                seed.Transport, host.Session.GeneratedWorld, host.Incidence)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(new[]
                {
                    (Red, 10_000_000_000_000UL),
                    (Blue, 10_000_000_000_000UL)
                }));
        var fields = host.Session.GeneratedWorld.StrategicPhysicalFields;
        var isLand = host.Incidence.TileIncidences.Select(tile =>
        {
            long sum = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                sum = checked(sum + fields.Elevation.GetRawValue(coarse));
            return sum / tile.IncidentCoarseCellIds.Count >
                fields.SeaLevel.RawValue;
        }).ToArray();
        var coast = host.Incidence.FineTopology.Edges
            .Where(e => isLand[checked((int)e.IncidentCellIds[0].Value - 1)] !=
                isLand[checked((int)e.IncidentCellIds[1].Value - 1)])
            .ToArray();
        Assert.True(coast.Length > 5);
        ulong a = m == 3 ? 16UL : 0UL;
        ulong b = m == 3 ? 27UL : 0UL;
        if (m == 2)
        {
            var landCoast = coast.Select(e =>
                isLand[checked((int)e.IncidentCellIds[0].Value - 1)]
                    ? e.IncidentCellIds[0].Value
                    : e.IncidentCellIds[1].Value).Distinct()
                .OrderBy(x => x).ToArray();
            a = landCoast[0];
            b = landCoast[^1];
        }
        var fine = host.Incidence.Refinement.FineParameters;
        Assert.True(isLand[checked((int)a - 1)]);
        Assert.True(isLand[checked((int)b - 1)]);
        Assert.NotEqual(a, b);
        return (host, state,
            new PhysicalTacticalTileId(fine, new StrategicCellId(a)),
            new PhysicalTacticalTileId(fine, new StrategicCellId(b)),
            new PhysicalTacticalEdgeId(fine, coast[0].Id));
    }

    private static FoundationPhysicalFrozenEconomicMaritimeComparator.Result
        Compare(
            FoundationTacticalHumanAiTurnHost host, WorldState state,
            PhysicalTacticalTileId from, PhysicalTacticalTileId to,
            CivilizationId seller, bool prune)
        => FoundationPhysicalFrozenEconomicMaritimeComparator.Compare(
            state, host.Session.GeneratedWorld, host.Incidence,
            Cargo, Commodity, 2_000_000UL, seller,
            from, to, 1UL, prune);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ProofBoundPreservesWinnerVersusExhaustiveFrozenEconomy(int m)
    {
        var (host, state, from, to, financeEdge) = Build(m);
        var frozenHash = host.StateHash;
        var initial = host.Snapshot;
        // We deliberately test the authoritative INVESTMENT model:
        // CAPEX improves capacity; no R55 operational cost substitution.
        var record = state.Transport.Edges.Single(e => e.Edge == financeEdge);
        WorldState financed = state;
        if (m == 3)
        {
            // The R63 research scenario established that developed
            // coastal logistics can actually WIN for markets 16 -> 27.
            // Finance ALL real coast crossings with money (not materials),
            // but retain AUTHORITATIVE physical costs/capacity policy.
            var fields = host.Session.GeneratedWorld.StrategicPhysicalFields;
            var isLand = host.Incidence.TileIncidences.Select(tile =>
            {
                long sum = 0;
                foreach (var coarse in tile.IncidentCoarseCellIds)
                    sum = checked(sum +
                        fields.Elevation.GetRawValue(coarse));
                return sum / tile.IncidentCoarseCellIds.Count >
                    fields.SeaLevel.RawValue;
            }).ToArray();
            var coastalIds = host.Incidence.FineTopology.Edges
                .Where(edge =>
                    isLand[checked((int)edge.IncidentCellIds[0].Value - 1)] !=
                    isLand[checked((int)edge.IncidentCellIds[1].Value - 1)])
                .Select(edge => new PhysicalTacticalEdgeId(
                    host.Incidence.Refinement.FineParameters, edge.Id))
                .ToHashSet();
            foreach (var coast in state.Transport.Edges.Where(
                row => coastalIds.Contains(row.Edge)))
                financed = financed.RecordMonetaryInfrastructureInvestment(
                    Red, coast.Edge,
                    checked(coast.NaturalConstructionCostMoneyMicro!.Value *
                        256UL));
        }
        else
            financed = state.RecordMonetaryInfrastructureInvestment(
                Red, financeEdge,
                checked(record.NaturalConstructionCostMoneyMicro!.Value * 32UL));
        int comparisons = 0;
        foreach (var (label, inventory) in new[]
        {
            (Name: "natural", State: state),
            (Name: "financed", State: financed)
        })
        foreach (var seller in new[] { Red, Blue })
        {
            var sw = Stopwatch.StartNew();
            var reference = Compare(host, inventory, from, to, seller,
                prune: false);
            sw.Stop();
            var oracleMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            var optimized = Compare(host, inventory, from, to, seller,
                prune: true);
            sw.Stop();
            Assert.Equal(reference.LandOnlyRateMicro,
                optimized.LandOnlyRateMicro);
            Assert.Equal(reference.SeaPreferred, optimized.SeaPreferred);
            // The best losing maritime ALTERNATIVE may have been pruned;
            // when sea wins globally, its exact quote and REAL terminals
            // must be retained without approximate tie-breaking.
            if (m == 3 && label == "financed")
                Assert.True(reference.SeaPreferred,
                    "R73 must cover a real maritime GLOBAL winner, not " +
                    "only inexpensive terrestrial routes.");
            if (reference.SeaPreferred)
            {
                Assert.Equal(reference.BestSeaRateMicro,
                    optimized.BestSeaRateMicro);
                Assert.Equal(reference.EmbarkEdge, optimized.EmbarkEdge);
                Assert.Equal(reference.DisembarkEdge,
                    optimized.DisembarkEdge);
            }
            Assert.Equal(reference.PotentialPairs,
                optimized.PotentialPairs);
            Assert.True(optimized.ExcludedPairs >= 0);
            Assert.True(optimized.QuotedPairs <= reference.QuotedPairs);
            Assert.True(reference.QuotedPairs > 0);
            Assert.Equal(reference, Compare(host, inventory,
                from, to, seller, prune: false));
            comparisons++;
            _output.WriteLine(
                $"GA_R73_ECONOMIC_AB coarse={m},0 mode={label} " +
                $"seller={seller.Value} land_rate={reference.LandOnlyRateMicro} " +
                $"sea_preferred={reference.SeaPreferred} " +
                $"potential_pairs={reference.PotentialPairs} " +
                $"bound_excluded={optimized.ExcludedPairs} " +
                $"oracle_quotes={reference.QuotedPairs} " +
                $"bounded_quotes={optimized.QuotedPairs} " +
                $"oracle_ms={oracleMs:F3} bound_ms={sw.Elapsed.TotalMilliseconds:F3} " +
                $"same_global_winner=true");
        }
        Assert.Equal(4, comparisons);
        Assert.Same(initial, host.Snapshot);
        Assert.Equal(frozenHash, host.StateHash);
    }
}
