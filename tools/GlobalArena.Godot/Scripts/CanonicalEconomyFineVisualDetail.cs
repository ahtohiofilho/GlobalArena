using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R81 presentation-only multiscale fine-terrain detail. Reuses the actual
/// 01C generator's G(18,0) three-band terrain signal, but NEVER its sea
/// level, biomes or physical ownership. The economic V1 height and sea-level
/// remain authoritative for EVERY land/water decision. This is only a
/// temporary visual overlay, NOT new simulated elevation or an authoritative
/// G(18,0) height field. Future physical worldgen must reconcile its own
/// higher-resolution geography before adopting such terrain for economics.
/// </summary>
internal sealed class CanonicalEconomyFineVisualDetail
{
    private readonly long[] _elevations;
    internal int FineTileCount => _elevations.Length;

    private CanonicalEconomyFineVisualDetail(long[] elevations) =>
        _elevations = elevations;

    internal long Elevation(ulong fineTileId)
    {
        if (fineTileId == 0UL || fineTileId > (ulong)_elevations.Length)
            throw new ArgumentOutOfRangeException(nameof(fineTileId));
        return _elevations[checked((int)fineTileId - 1)];
    }

    internal static CanonicalEconomyFineVisualDetail Create(
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        long minimum, long maximum)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        if (incidence.Refinement.CoarseParameters !=
                world.Request.StrategicParameters ||
            minimum > maximum ||
            incidence.TileIncidences.Count != incidence.FineTopology.Cells.Count)
            throw new InvalidOperationException(
                "Visual detail cannot use unrelated world or physical topology.");

        var geometry = StrategicSphericalGeometryGenerator.Generate(
            incidence.FineTopology);
        // The exact original 01C three-band generator. No second coastline:
        // take only variation around its own global mean.
        var source = TacticalIndependentScaleGenerator.GenerateWeighted(
            geometry, world.Request.Seed.Value, 0.5, 2.0, 3.0);
        if (source.Count != incidence.TileIncidences.Count)
            throw new InvalidOperationException(
                "Multiscale source must cover exactly the real fine tiles.");

        double mean = source.Average(x => (double)x);
        long sea = world.StrategicPhysicalFields.SeaLevel.RawValue;
        double amplitude = (maximum - (double)minimum) * 0.16 / 1_000_000.0;
        var economic = world.StrategicPhysicalFields.Elevation;
        var fine = new long[source.Count];
        for (int i = 0; i < fine.Length; i++)
        {
            var incidenceRow = incidence.TileIncidences[i];
            long sum = 0L;
            foreach (var coarse in incidenceRow.IncidentCoarseCellIds)
                sum = checked(sum + economic.GetRawValue(coarse));
            long original = sum / incidenceRow.IncidentCoarseCellIds.Count;
            long detail = checked((long)Math.Round(
                (source[i] - mean) * amplitude,
                MidpointRounding.AwayFromZero));
            long candidate = checked(original + detail);
            // Never alter the canonical economic land/water classification.
            // Colors are constrained to that sign, including at sea level.
            candidate = original > sea
                ? Math.Max(sea + 1L, candidate)
                : Math.Min(sea, candidate);
            fine[i] = Math.Clamp(candidate, minimum, maximum);
        }
        return new CanonicalEconomyFineVisualDetail(fine);
    }
}
