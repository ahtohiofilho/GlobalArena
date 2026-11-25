namespace GlobalArena.Godot;

/// <summary>Presentation-only cartographic calibration. No economic units
/// or population state are modified. Cargo is the audited common unit.</summary>
internal static class GaCartographicStyle
{
    internal const float TradeOutlinePixels = 1f;
    internal const double CargoReferenceMicro = 1_000_000d;
    internal const float TradeLogGain = 2f;
    internal const float MaximumTradeCorePixels = 12f;

    internal static int SiteBand(int population)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        if (population == 0) return 0;
        int band = 1;
        long threshold = 1;
        while (threshold < population) { threshold *= 2; band++; }
        return band;
    }

    internal static int SiteDiameter(int population)
    {
        int band = SiteBand(population);
        return band == 0 ? 0 : 6 + 2 * band;
    }

    internal static float TradeWidth(ulong cargoMicro) => cargoMicro == 0UL ? 0f :
        Math.Min(MaximumTradeCorePixels, 1f + TradeLogGain *
            (float)Math.Log2(1d + cargoMicro / CargoReferenceMicro));

    internal static void ExerciseContract()
    {
        int[] population = { 0, 1, 2, 3, 4, 5, 8, 9, 16, 17 };
        int[] expected = { 0, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
        for (int i = 0; i < population.Length; i++)
            if (SiteBand(population[i]) != expected[i])
                throw new InvalidOperationException("Population band boundary differs from user contract.");
        if (TradeWidth(0) != 0f || TradeWidth(1_000_000) >= TradeWidth(2_000_000) ||
            TradeWidth(2_000_000) >= TradeWidth(4_000_000) ||
            TradeWidth(ulong.MaxValue) > MaximumTradeCorePixels || TradeOutlinePixels != 1f)
            throw new InvalidOperationException("Cartographic trade calibration failed.");
    }
}
