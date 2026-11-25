using System.Numerics;
using Godot;
using GlobalArena.Runtime;

namespace GlobalArena.Godot;

/// <summary>
/// Presentation ONLY. The existing economy QA fixture is activated by a
/// separate, explicit launcher flag. It is NOT production balancing.
/// No money, worker, price, market or route is created by this indicator.
/// </summary>
public partial class Main
{
    private bool _interactiveEconomicPreviewActive;
    private Label? _interactiveEconomicPreviewBanner;

    private void CreateInteractiveEconomyPreviewBanner(VBoxContainer root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _interactiveEconomicPreviewBanner = new Label
        {
            Name = "GANextEconomyPreviewBanner",
            Visible = _interactiveEconomicPreviewActive,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = "EXPERIMENTAL ECONOMY • QA VALUES, NOT FINAL BALANCE " +
                "• PRESS E TO SETTLE ACTUAL SALES • TRADES AFTER TURN 1"
        };
        _interactiveEconomicPreviewBanner.AddThemeColorOverride(
            "font_color", GaVisualFoundation.Warm);
        _interactiveEconomicPreviewBanner.AddThemeFontSizeOverride(
            "font_size", 12);
        root.AddChild(_interactiveEconomicPreviewBanner);
    }

    // R124: all monetary quantities on-screen use ONE abstract unit.
    // The kernel keeps its exact micro integer and 1e16 ledger scales.
    // Values here are only an invariant-culture presentation conversion.
    private static string FormatMoneyMicroForDisplay(ulong micro)
    {
        const ulong microPerUnit = 1_000_000UL;
        return (micro / microPerUnit).ToString(
            System.Globalization.CultureInfo.InvariantCulture) + "." +
            (micro % microPerUnit).ToString(
                System.Globalization.CultureInfo.InvariantCulture).PadLeft(6, '0');
    }

    private static string FormatExactMoneyForDisplay(BigInteger scaled)
    {
        if (scaled.Sign < 0)
            throw new InvalidOperationException(
                "An authoritative civilization treasury cannot be negative.");
        BigInteger whole = scaled / GlobalArenaMoneyScale.ExactScaledPerUnit;
        BigInteger fractionMicro =
            (scaled % GlobalArenaMoneyScale.ExactScaledPerUnit) /
            GlobalArenaMoneyScale.ExactScaledPerMoneyMicro;
        // The UI truncates beyond six displayed decimal places, but
        // the REAL 1e16-scale monetary ledger remains exact in WorldState.
        return whole.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "." + fractionMicro.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
                .PadLeft(6, '0');
    }

    private void RefreshInteractiveEconomyPreviewBanner()
    {
        if (!_interactiveEconomicPreviewActive ||
            _interactiveEconomicPreviewBanner is null ||
            _tacticalHost is null)
            return;

        var state = _tacticalHost.Snapshot;
        if (!state.Treasury.IsInitialized ||
            !state.Transport.HasMonetaryConstructionCosts)
            throw new InvalidOperationException(
                "Interactive QA must display ACTUAL v13+ treasury and transport.");
        var read = _tacticalHost.GetLastClosedEconomicTradeView(
            _humanCivilization);
        _interactiveEconomicPreviewBanner.Text =
            (_tacticalHost.ExperimentalLocalEconomyEnabled ? "EXPERIMENTAL LOCAL ECONOMY / NOT FINAL BALANCE  •  " : "EXPERIMENTAL / NOT FINAL BALANCE  •  ") +
            "CIV 1 CASH " + FormatExactMoneyForDisplay(
                state.Treasury.Get(_humanCivilization).CashScaled) +
            "  •  CLOSED CYCLES " +
            state.Treasury.LastSettledEconomicCycleOrdinal +
            "  •  USED TRADE EDGES " + read.UsedEdges.Count +
            "  •  E = CLOSE NEXT REAL ECONOMIC TURN";
    }
}
