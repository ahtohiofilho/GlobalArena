using System.Globalization;
using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Godot;

/// <summary>R230.1 English UI presentation only. No formatted value feeds
/// the authoritative simulation. Raw edit fields use a separate exact format.</summary>
internal static class GaDisplayNumbers
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const ulong MicroPerUnit = 1_000_000UL;

    internal static string PhysicalMicro(BigInteger micro) => Rounded(micro, MicroPerUnit, 3, false);
    internal static string MoneyMicro(BigInteger micro) => Rounded(micro, MicroPerUnit, 2, true);
    internal static string MoneyScaled(BigInteger scaled) =>
        Rounded(scaled, GlobalArenaMoneyScale.ExactScaledPerUnit, 2, true);

    internal static string ExactMoneyInput(ulong micro) =>
        (micro / 1_000_000m).ToString("0.######", Invariant);

    private static string Rounded(BigInteger amount, BigInteger units, int decimals, bool money)
    {
        var factor = BigInteger.Pow(10, decimals);
        var magnitude = BigInteger.Abs(amount);
        var rounded = BigInteger.DivRem(magnitude * factor, units, out var remainder);
        // Exact half-away-from-zero rounding, no double precision loss.
        if (remainder * 2 >= units) rounded++;
        var sign = amount.Sign < 0 ? "-" : "";
        var currency = money ? "$" : "";
        var whole = BigInteger.DivRem(rounded, factor, out var fraction);
        var digits = fraction.ToString(Invariant).PadLeft(decimals, '0');
        if (!money) digits = digits.TrimEnd('0');
        // Keep tiny nonzero values distinct from actual zero.
        if (rounded.IsZero && !amount.IsZero)
            return sign + "<" + currency + "0." + new string('0', decimals - 1) + "1";
        return sign + currency + whole.ToString(Invariant) +
            (digits.Length == 0 ? "" : "." + digits);
    }

    internal static void ExerciseContract()
    {
        void Must(string actual, string expected)
        {
            if (actual != expected)
                throw new InvalidOperationException($"R230.1 numeric format: {actual} != {expected}");
        }
        Must(PhysicalMicro(1_806_736UL), "1.807");
        Must(PhysicalMicro(3_000_000UL), "3");
        Must(PhysicalMicro(500_001UL), "0.5");
        Must(PhysicalMicro(0UL), "0");
        Must(PhysicalMicro(1UL), "<0.001");
        Must(MoneyMicro(24_234_260UL), "$24.23");
        Must(MoneyMicro(950_456UL), "$0.95");
        Must(MoneyMicro(0UL), "$0.00");
        Must(MoneyMicro(1UL), "<$0.01");
        Must(MoneyMicro(5_000UL), "$0.01");
        Must(MoneyMicro(-5_000), "-$0.01");
        Must(MoneyScaled(GlobalArenaMoneyScale.ExactScaledPerUnit * 24 +
            GlobalArenaMoneyScale.ExactScaledPerUnit * 23426 / 100000), "$24.23");
        Must(ExactMoneyInput(123_456UL), "0.123456");
        GDMarker();
    }

    private static void GDMarker() => Godot.GD.Print("GA_NEXT_R2301_NUMERIC_FORMAT_PASS physical=3 money=2 exact_input=true");
}
