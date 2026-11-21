using GlobalArena.World;

// R118: research-only fine-first batymetry diagnosis. Does NOT seed costs,
// alter market policy, invent meter scales or change signed world hashes.
internal static class FoundationCrossingDepthReport
{
    private static long Quantile(long[] sorted, int numerator, int denominator)
    {
        if (sorted.Length == 0) return 0L;
        int index = checked((int)((long)(sorted.Length - 1) *
            numerator / denominator));
        return sorted[index];
    }

    private static string Format(long value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static void Run()
    {
        // Two seeds share the same G18 topology. A second seed detects
        // accidental overfitting of a shallow-water proposal to one map.
        foreach (ulong seed in new[] { 20261012UL, 20261013UL })
        {
            var coarse = new GoldbergParameters(3, 0);
            var fineScale = new GoldbergParameters(18, 0);
            var incidence = PhysicalTacticalIncidenceMapper.Materialize(
                new GoldbergScaledRefinement(coarse, fineScale));
            var world = new DeterministicWorldGenerator().Generate(
                new WorldGenerationRequest(new WorldSeed(seed),
                    WorldGenerationVersion.Initial, coarse));
            var geography = FoundationFineFirstPhysicalGeography.Generate(
                world, incidence);
            long sea = geography.SeaLevelRaw;
            var depth = geography.FineElevationRaw
                .Select(h => Math.Max(0L, sea - h)).ToArray();
            var seaDepths = Enumerable.Range(0, depth.Length)
                .Where(i => geography.FineElevationRaw[i] <= sea)
                .Select(i => depth[i]).OrderBy(d => d).ToArray();
            long p25 = Quantile(seaDepths, 1, 4);
            long p50 = Quantile(seaDepths, 1, 2);
            long p75 = Quantile(seaDepths, 3, 4);

            var groundDelta = new List<long>();
            var ports = new List<long>();
            var seaEdges = new List<long>();
            foreach (var edge in incidence.FineTopology.Edges)
            {
                int first = checked((int)edge.IncidentCellIds[0].Value - 1);
                int second = checked((int)edge.IncidentCellIds[1].Value - 1);
                switch (geography.CrossingAtIndices(first, second))
                {
                    case FoundationFineTransportCrossingKind.Ground:
                        groundDelta.Add(Math.Abs(
                            geography.FineElevationRaw[first] -
                            geography.FineElevationRaw[second]));
                        break;
                    case FoundationFineTransportCrossingKind.Port:
                        ports.Add(geography.FineElevationRaw[first] <= sea
                            ? depth[first] : depth[second]);
                        break;
                    case FoundationFineTransportCrossingKind.OpenSea:
                        seaEdges.Add(Math.Min(depth[first], depth[second]));
                        break;
                    case FoundationFineTransportCrossingKind.BlockedIceSeaBoundary:
                        break; // Not a navigable port, no invented cost.
                    default:
                        throw new InvalidOperationException("Unknown crossing mode.");
                }
            }

            var portSorted = ports.OrderBy(x => x).ToArray();
            var seaSorted = seaEdges.OrderBy(x => x).ToArray();
            var groundSorted = groundDelta.OrderBy(x => x).ToArray();
            if (ports.Count == 0 || seaEdges.Count == 0 ||
                groundDelta.Count == 0 || seaDepths.Length == 0)
                throw new InvalidOperationException(
                    "The diagnostic scenario must contain all navigable classes.");
            long shallowCutoff = p25;
            long moderateCutoff = p50;
            Console.WriteLine(
                $"GA_R118_FINE_DEPTH seed={seed} tiles={depth.Length} " +
                $"land={geography.LandTileCount} sea={seaDepths.Length} " +
                $"sea_level_raw={Format(sea)} depths_are_relative_not_meters=true");
            Console.WriteLine(
                $"GA_R118_SEA_TILE_DEPTH seed={seed} " +
                $"p25={Format(p25)} p50={Format(p50)} p75={Format(p75)} " +
                $"max={Format(seaDepths[^1])}");
            Console.WriteLine(
                $"GA_R118_TT seed={seed} edges={groundDelta.Count} " +
                $"height_delta_p50={Format(Quantile(groundSorted, 1, 2))} " +
                $"height_delta_p75={Format(Quantile(groundSorted, 3, 4))}");
            Console.WriteLine(
                $"GA_R118_TM seed={seed} edges={ports.Count} " +
                $"depth_p25={Format(Quantile(portSorted, 1, 4))} " +
                $"depth_p50={Format(Quantile(portSorted, 1, 2))} " +
                $"depth_p75={Format(Quantile(portSorted, 3, 4))} " +
                $"shallow_at_sea_p25={ports.Count(d => d <= shallowCutoff)} " +
                $"shallow_at_sea_p50={ports.Count(d => d <= moderateCutoff)}");
            Console.WriteLine(
                $"GA_R118_MM seed={seed} edges={seaEdges.Count} " +
                $"depth_p25={Format(Quantile(seaSorted, 1, 4))} " +
                $"depth_p50={Format(Quantile(seaSorted, 1, 2))} " +
                $"depth_p75={Format(Quantile(seaSorted, 3, 4))} " +
                $"shallow_at_sea_p25={seaEdges.Count(d => d <= shallowCutoff)} " +
                $"shallow_at_sea_p50={seaEdges.Count(d => d <= moderateCutoff)}");
        }
    }
}
