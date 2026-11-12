// Non-authoritative, observational microbenchmark: numerator-to-binary64 only.
// Does not benchmark full equilibrium clearing, routing or game-frame performance.
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;

internal static class Program
{
    private const long Denominator = 1_000_000_000_000L;
    private sealed record Sample(long TariffMicro, int SelfMicro);
    private static long _sink;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 4 || args[0] != "--input" || args[2] != "--output")
                throw new ArgumentException("Usage: --input ratio-input.json --output result.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(args[1]));
            var samples = doc.RootElement.GetProperty("cases").EnumerateArray()
                .Select(x => new Sample(x.GetProperty("tariffMicro").GetInt64(),
                                        x.GetProperty("selfMicro").GetInt32()))
                .ToArray();
            if (samples.Length != 270) throw new ArgumentException("Expected 270 official samples");
            var numerators = samples.Select(x => (BigInteger)x.TariffMicro * (1_000_000 - x.SelfMicro)).ToArray();
            var expectedBits = doc.RootElement.GetProperty("cases").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
            if (expectedBits.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Blank sample ID");

            // In-process Release measurements, amortizing process startup and JSON I/O.
            // Both methods share precomputed exact numerators; no allocations for inputs.
            const int iterations = 1500;
            const int rounds = 7;
            static double Exact(BigInteger n) => ExactRatioBinary64.Convert(n, Denominator);
            static double Legacy(BigInteger n) => (double)((decimal)n / 1_000_000_000_000m);
            long checksum = 0;
            for (int i = 0; i < numerators.Length; i++)
            {
                // Verify all inputs can be processed before benchmarking.
                checksum ^= BitConverter.DoubleToInt64Bits(Exact(numerators[i]));
                checksum ^= BitConverter.DoubleToInt64Bits(Legacy(numerators[i]));
            }

            static double Run(BigInteger[] data, int count, Func<BigInteger, double> conversion, ref long observed)
            {
                long accum = 0;
                var timer = Stopwatch.StartNew();
                for (int k=0; k<count; k++)
                    for (int i=0; i<data.Length; i++)
                        accum ^= BitConverter.DoubleToInt64Bits(conversion(data[i]));
                timer.Stop();
                observed ^= accum;
                return timer.Elapsed.TotalMilliseconds;
            }
            // Warm both JIT paths separately; avoid including compilation in results.
            _ = Run(numerators, 6, Exact, ref checksum);
            _ = Run(numerators, 6, Legacy, ref checksum);

            var exactTimes = new double[rounds];
            var legacyTimes = new double[rounds];
            for (int i=0; i<rounds; i++)
            {
                // Alternate the order to reduce drift/order bias.
                if ((i & 1) == 0)
                {
                    exactTimes[i] = Run(numerators, iterations, Exact, ref checksum);
                    legacyTimes[i] = Run(numerators, iterations, Legacy, ref checksum);
                }
                else
                {
                    legacyTimes[i] = Run(numerators, iterations, Legacy, ref checksum);
                    exactTimes[i] = Run(numerators, iterations, Exact, ref checksum);
                }
            }
            System.Threading.Interlocked.Exchange(ref _sink, checksum);
            static double Median(double[] v) => v.OrderBy(x => x).ElementAt(v.Length / 2);
            double exactMedian = Median(exactTimes), legacyMedian = Median(legacyTimes);
            var result = new
            {
                gate="Gate09-R19", type="observational-only", sampleCount=numerators.Length,
                iterationsPerRound=iterations, rounds,
                totalConversionsPerMethodPerRound=numerators.Length * iterations,
                exactMs=exactTimes, legacyMs=legacyTimes,
                exactMedianMs=exactMedian, legacyMedianMs=legacyMedian,
                overheadRatioVsLegacy=exactMedian / legacyMedian,
                checksum=System.Threading.Interlocked.Read(ref _sink), runtime=Environment.Version.ToString(),
                architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                os=System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                important="Legacy decimal path is NOT bit-equivalent; benchmark comparison is performance only."
            };
            File.WriteAllText(args[3], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine($"R19_BENCHMARK_OBSERVATIONAL exactMedianMs={exactMedian:F3} legacyMedianMs={legacyMedian:F3} ratio={exactMedian/legacyMedian:F3} samples={numerators.Length}");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("R19_BENCHMARK_FAILED " + e); return 1; }
    }
}
