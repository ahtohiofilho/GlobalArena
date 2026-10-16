using System;
using System.Collections.Generic;
using System.Linq;

namespace GlobalArena.World;

/// <summary>
/// Experimental whole-planet quartile boundaries. Classifies the already
/// generated moisture field; never changes moisture or geographic locations.
/// Thresholds belong to the whole physical planet, not to tactical patches.
/// Equal values are never split across categories.
/// </summary>
public static class ExperimentalPlanetMoistureQuartiles
{
    public readonly record struct Thresholds(long Q25, long Q50, long Q75);

    public static Thresholds Calculate(IReadOnlyList<long> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Count < 4) throw new ArgumentException("At least four planet samples are required.", nameof(raw));
        var ordered = raw.ToArray();
        foreach (var value in ordered)
            if (value < 0 || value > ExperimentalRandomMoisturePatchGenerator.Scale)
                throw new ArgumentOutOfRangeException(nameof(raw));
        Array.Sort(ordered);
        return new Thresholds(
            ordered[(int)Math.Ceiling(ordered.Length * 0.25) - 1],
            ordered[(int)Math.Ceiling(ordered.Length * 0.50) - 1],
            ordered[(int)Math.Ceiling(ordered.Length * 0.75) - 1]);
    }

    public static ExperimentalRandomMoisturePatchGenerator.MoistureBand Classify(long value, Thresholds t)
    {
        if (value < 0 || value > ExperimentalRandomMoisturePatchGenerator.Scale)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (t.Q25 > t.Q50 || t.Q50 > t.Q75)
            throw new ArgumentException("Quartile thresholds must be ascending.", nameof(t));
        if (value <= t.Q25) return ExperimentalRandomMoisturePatchGenerator.MoistureBand.Arid;
        if (value <= t.Q50) return ExperimentalRandomMoisturePatchGenerator.MoistureBand.SemiArid;
        if (value <= t.Q75) return ExperimentalRandomMoisturePatchGenerator.MoistureBand.Fertile;
        return ExperimentalRandomMoisturePatchGenerator.MoistureBand.Humid;
    }
}