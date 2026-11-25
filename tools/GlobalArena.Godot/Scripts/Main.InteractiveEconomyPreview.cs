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

    // Separate read-only presentation from exact command input.
    private static string FormatMoneyMicroForDisplay(ulong micro) => GaDisplayNumbers.MoneyMicro(micro);
    private static string FormatExactMoneyForDisplay(BigInteger scaled) => GaDisplayNumbers.MoneyScaled(scaled);
    private static string FormatPhysicalMicroForDisplay(ulong micro) => GaDisplayNumbers.PhysicalMicro(micro);

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
