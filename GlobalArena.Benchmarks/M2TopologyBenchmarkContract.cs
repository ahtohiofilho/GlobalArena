using GlobalArena.World;

namespace GlobalArena.Benchmarks;

public enum M2TopologyBenchmarkWorkloadKind
{
    Logical,
    Physical
}

public sealed record M2TopologyBenchmarkCase(
    string Id,
    M2TopologyBenchmarkWorkloadKind WorkloadKind,
    GoldbergParameters CoarseParameters,
    GoldbergParameters? FineParameters,
    bool BlocksM2Exit);

public static class M2TopologyBenchmarkContract
{
    public const int WarmupCount = 1;

    public const int MeasuredCount = 5;

    public const double MedianElapsedBudgetMilliseconds = 1000.0;

    public const double MaxElapsedBudgetMilliseconds = 2000.0;

    public const long MedianManagedAllocationBudgetBytes =
        192L * 1024L * 1024L;

    public const long MaxManagedAllocationBudgetBytes =
        256L * 1024L * 1024L;

    private static readonly M2TopologyBenchmarkCase[] CaseArray =
    {
        new(
            "class-i-logical-g15x0",
            M2TopologyBenchmarkWorkloadKind.Logical,
            new GoldbergParameters(
                15,
                0),
            null,
            true),

        new(
            "class-ii-logical-g7x7",
            M2TopologyBenchmarkWorkloadKind.Logical,
            new GoldbergParameters(
                7,
                7),
            null,
            true),

        new(
            "class-iii-logical-g14x1",
            M2TopologyBenchmarkWorkloadKind.Logical,
            new GoldbergParameters(
                14,
                1),
            null,
            true),

        new(
            "class-i-physical-g4x0-to-g24x0",
            M2TopologyBenchmarkWorkloadKind.Physical,
            new GoldbergParameters(
                4,
                0),
            new GoldbergParameters(
                24,
                0),
            true),

        new(
            "class-i-physical-stress-g16x0-to-g96x0",
            M2TopologyBenchmarkWorkloadKind.Physical,
            new GoldbergParameters(
                16,
                0),
            new GoldbergParameters(
                96,
                0),
            false)
    };

    public static IReadOnlyList<M2TopologyBenchmarkCase> Cases { get; } =
        Array.AsReadOnly(
            CaseArray);
}
