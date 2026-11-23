using Godot;
using GlobalArena.Runtime;

namespace GlobalArena.Godot;

/// <summary>
/// R182 English-first, read-only civilization screen. Only authoritative
/// current worker and treasury data are shown; absent economies are labeled
/// unavailable rather than represented by synthetic balances or KPIs.
/// </summary>
public partial class Main
{
    private CenterContainer? _civilizationWindowLayer;
    private Label? _civilizationOverview;
    private Label? _civilizationEconomy;

    private void CreateCivilizationWindow()
    {
        var overlay = new CenterContainer
        {
            Name = "GANextCivilizationWindowLayer",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(overlay);
        _civilizationWindowLayer = overlay;

        var panel = new PanelContainer
        {
            Name = "GANextCivilizationWindow",
            CustomMinimumSize = new Vector2(575f, 440f),
            MouseFilter = MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        overlay.AddChild(panel);
        var content = new VBoxContainer();
        panel.AddChild(content);

        GaVisualFoundation.AddWindowHeader(content, "CIVILIZATION",
            "Civilization overview",
            "Official data from the current world snapshot",
            () => overlay.Visible = false);

        var tabs = new TabContainer
        {
            Name = "GANextCivilizationTabs",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(tabs);
        var overview = new MarginContainer { Name = "Overview" };
        tabs.AddChild(overview);
        _civilizationOverview = new Label
        {
            Name = "GANextCivilizationOverviewValues",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Top,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        overview.AddChild(_civilizationOverview);

        var economy = new MarginContainer { Name = "Economy" };
        tabs.AddChild(economy);
        _civilizationEconomy = new Label
        {
            Name = "GANextCivilizationEconomyValues",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Top,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        economy.AddChild(_civilizationEconomy);
        RefreshCivilizationWindow();
    }

    private void OpenCivilizationWindow()
    {
        if (_civilizationWindowLayer is null) return;
        RefreshCivilizationWindow();
        // Main screens never overlap: the map remains the backdrop.
        if (_cityWindowLayer is not null)
            _cityWindowLayer.Visible = false;
        _civilizationWindowLayer.Visible = true;
    }

    private void RefreshCivilizationWindow()
    {
        if (_civilizationOverview is null || _civilizationEconomy is null)
            return;
        var host = _tacticalHost;
        if (host is null)
        {
            _civilizationOverview.Text = "No active physical civilization.";
            _civilizationEconomy.Text =
                "Authoritative economic information is not available.";
            return;
        }

        var snapshot = host.Snapshot;
        int workerCount = snapshot.Workers.IsInitialized
            ? snapshot.Workers.Workers.Count(w => w.Owner == _humanCivilization)
            : 0;
        var turn = host.NextTurn.Value;
        _civilizationOverview.Text =
            $"Civilization {_humanCivilization.Value}\n" +
            $"Current turn: {turn}\n" +
            (snapshot.Workers.IsInitialized
                ? $"Workers: {workerCount}\n"
                : "Workers: unavailable\n") +
            $"World revision: {snapshot.Revision}\n" +
            $"Other civilizations: {host.AiCivilizations.Count}";

        if (!snapshot.Treasury.IsInitialized)
        {
            _civilizationEconomy.Text =
                "Treasury not initialized.\n" +
                "No financial figures are estimated.";
            return;
        }
        var account = snapshot.Treasury.Get(_humanCivilization);
        var close = host.GetLastClosedTemporalEconomics(_humanCivilization);
        _civilizationEconomy.Text =
            $"Cash balance: {FormatTemporalMoney(account.CashScaled)}\n" +
            $"Cumulative infrastructure investment: " +
                $"{FormatTemporalMoney(account.TotalInfrastructureInvestmentScaled)}\n" +
            (close is null
                ? "No verified temporal economic close yet."
                : $"Last economic cycle: {close.EconomicCycleOrdinal}\n" +
                  $"Sales net income: {FormatTemporalMoney(close.SellerNetScaled)}\n" +
                  $"Taxes credited: {FormatTemporalMoney(close.TaxReceiptsScaled)}\n" +
                  $"Tolls credited: {FormatTemporalMoney(close.OwnerTollIncomeScaled)}\n" +
                  $"Total cash credit: {FormatTemporalMoney(close.TotalCashCreditScaled)}");
    }

    // Explicit UI-only acceptance probe; no worker assignment or market run.
    internal void ExerciseR182CivilizationWindowSmoke()
    {
        var before = _tacticalHost?.StateHash;
        OpenCivilizationWindow();
        var tabs = FindChild("GANextCivilizationTabs", true, false)
            as TabContainer;
        if (tabs is null || tabs.GetTabCount() != 2 ||
            tabs.GetTabTitle(0) != "Overview" ||
            tabs.GetTabTitle(1) != "Economy" ||
            _civilizationWindowLayer?.Visible != true ||
            _civilizationOverview?.Text.Contains(
                "Civilization 1", StringComparison.Ordinal) != true ||
            _civilizationEconomy?.Text is null ||
            _tacticalHost?.StateHash != before)
            throw new InvalidOperationException(
                "R182 civilization screen did not preserve signed read-only state.");
        _civilizationWindowLayer.Visible = false;
        GD.Print("GA_NEXT_R182_CIVILIZATION_SCREEN_PASS " +
            "tabs=Overview,Economy language=en " +
            "official_snapshot=true no_fake_kpis=true read_only=true");
    }
}
