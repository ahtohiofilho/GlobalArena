namespace GlobalArena.Foundation.Agriculture;

/// <summary>
/// Stable IDs for six initial food crops. They are not legacy commodity IDs.
/// Experimental per-crop market identities must be integrated separately.
/// </summary>
public enum CropId : byte
{
    Maize = 1,
    Sorghum = 2,
    Wheat = 3,
    Rice = 4,
    Soybean = 5,
    Barley = 6
}

/// <summary>
/// LAB-ONLY response parameters; not authoritative agricultural calibration.
/// All indices are fixed-point. Temperature is signed [-1e6,+1e6].
/// Moisture is normalized [0,1e6]. Tolerances are positive.
/// </summary>
public readonly record struct CropProfile(
    CropId Id,
    string StableKey,
    int IdealTemperature,
    int IdealMoisture,
    int TemperatureTolerance,
    int MoistureTolerance,
    int MaxSlopePenalty);

public static class CropCatalog
{
    public const int Count = 6;

    // Profile values are experimental. Names/identities are the canonical decision.
    private static readonly CropProfile[] Profiles =
    {
        new(CropId.Maize,   "milho",  350_000, 530_000, 1_200_000, 650_000, 190_000),
        new(CropId.Sorghum, "sorgo",  680_000, 150_000, 1_200_000, 650_000, 170_000),
        new(CropId.Wheat,   "trigo", -200_000, 470_000, 1_150_000, 650_000, 190_000),
        new(CropId.Rice,    "arroz",  650_000, 880_000, 1_250_000, 700_000, 290_000),
        new(CropId.Soybean, "soja",   90_000, 740_000, 1_180_000, 630_000, 200_000),
        new(CropId.Barley,  "cevada",-650_000, 250_000, 1_200_000, 680_000, 170_000)
    };

    public static IReadOnlyList<CropProfile> All { get; } =
        Array.AsReadOnly(Profiles);

    // Explicit identity mapping; never infer a stable ID from declaration order.
    public static int IndexOf(CropId id) => id switch
    {
        CropId.Maize => 0,
        CropId.Sorghum => 1,
        CropId.Wheat => 2,
        CropId.Rice => 3,
        CropId.Soybean => 4,
        CropId.Barley => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown crop")
    };

    public static CropProfile Get(CropId id) => Profiles[IndexOf(id)];
}
