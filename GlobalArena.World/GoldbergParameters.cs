namespace GlobalArena.World;

public readonly record struct GoldbergParameters
{
    public int M { get; }

    public int N { get; }

    public ulong TriangulationNumber { get; }

    public ulong StrategicCellCount { get; }

    public ulong StrategicEdgeCount { get; }

    public ulong StrategicVertexCount { get; }

    public ulong PentagonCount { get; }

    public ulong HexagonCount { get; }

    public GoldbergParameters(
        int m,
        int n)
    {
        if (m < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(m),
                "Goldberg parameter m cannot be negative.");
        }

        if (n < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(n),
                "Goldberg parameter n cannot be negative.");
        }

        if (m == 0 && n == 0)
        {
            throw new ArgumentException(
                "Goldberg parameters m and n cannot both be zero.");
        }

        var mValue = (ulong)m;
        var nValue = (ulong)n;

        var triangulationNumber = checked(
            checked(mValue * mValue)
            + checked(mValue * nValue)
            + checked(nValue * nValue));

        M = m;
        N = n;
        TriangulationNumber = triangulationNumber;
        StrategicCellCount = checked(
            checked(10UL * triangulationNumber)
            + 2UL);
        StrategicEdgeCount = checked(
            30UL * triangulationNumber);
        StrategicVertexCount = checked(
            20UL * triangulationNumber);
        PentagonCount = 12UL;
        HexagonCount = checked(
            10UL * checked(triangulationNumber - 1UL));
    }
}
