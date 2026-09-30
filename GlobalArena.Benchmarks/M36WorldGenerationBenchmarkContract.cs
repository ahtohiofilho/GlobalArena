using GlobalArena.World;

namespace GlobalArena.Benchmarks;

public sealed record M36WorldGenerationBenchmarkCase(
    string Id,
    ulong Seed,
    GoldbergParameters Parameters,
    bool BlocksM3Exit);

public static class M36WorldGenerationBenchmarkContract
{
    public const int WarmupCount =
        1;

    public const int MeasuredCount =
        5;

    public const double MedianElapsedBudgetMilliseconds =
        200.0;

    public const double MaxElapsedBudgetMilliseconds =
        300.0;

    public const long MedianManagedAllocationBudgetBytes =
        64L * 1024L * 1024L;

    public const long MaxManagedAllocationBudgetBytes =
        96L * 1024L * 1024L;

    private static readonly M36WorldGenerationBenchmarkCase[] CaseArray =
    {
        new(
            "class-i-g15x0",
            3601UL,
            new GoldbergParameters(
                15,
                0),
            true),

        new(
            "class-ii-g7x7",
            3602UL,
            new GoldbergParameters(
                7,
                7),
            true),

        new(
            "class-iii-g14x1",
            3603UL,
            new GoldbergParameters(
                14,
                1),
            true),

        new(
            "class-i-stress-g24x0",
            3604UL,
            new GoldbergParameters(
                24,
                0),
            false)
    };

    public static IReadOnlyList<M36WorldGenerationBenchmarkCase> Cases { get; } =
        Array.AsReadOnly(
            CaseArray);
}
