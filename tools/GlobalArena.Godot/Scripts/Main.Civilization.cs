using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

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
        if (_tacticalHost?.PurchasedDemographyEnabled == true) CreatePurchasedDemographyTab(tabs);
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
        RefreshVisiblePurchasedDemography();
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

    private TabContainer? _demographicTabs;
    private LineEdit? _demographicQuantity;
    private Label? _demographicSummary, _demographicStatus;
    private Button? _demographicQuoteButton, _demographicQueueButton, _demographicCancelButton;

    private void CreatePurchasedDemographyTab(TabContainer tabs)
    {
        _demographicTabs = tabs;
        var panel = new VBoxContainer { Name = "Population" };
        tabs.AddChild(panel);
        _demographicSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_demographicSummary);
        panel.AddChild(new Label { Text = "Number of new workers" });
        _demographicQuantity = new LineEdit { Text = "1", Name = "GANextDemographicQuantity", MaxLength = 19 };
        panel.AddChild(_demographicQuantity);
        _demographicStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _demographicQuoteButton = new Button { Text = "CHECK PURCHASE", Name = "GANextDemographicQuote" };
        _demographicQueueButton = new Button { Text = "QUEUE PURCHASE", Name = "GANextDemographicQueue", Disabled = true };
        _demographicCancelButton = new Button { Text = "CANCEL PENDING PURCHASE", Name = "GANextDemographicCancel", Disabled = true };
        panel.AddChild(_demographicQuoteButton);
        panel.AddChild(_demographicQueueButton);
        panel.AddChild(_demographicCancelButton);
        panel.AddChild(_demographicStatus);
        _demographicQuantity.TextChanged += _ =>
        {
            _demographicQueueButton.Disabled = true;
            _demographicStatus.Text = "Check the updated quantity before queuing.";
        };
        _demographicQuoteButton.Pressed += () => RefreshPurchasedDemographyQuote();
        _demographicQueueButton.Pressed += () => ChangePurchasedDemographyPlan(false);
        _demographicCancelButton.Pressed += () => ChangePurchasedDemographyPlan(true);
        tabs.TabChanged += _ => RefreshVisiblePurchasedDemography();
    }

    private void RefreshVisiblePurchasedDemography(string? message = null)
    {
        if (_civilizationWindowLayer?.Visible == true && _demographicTabs?.CurrentTab == 2)
            RefreshPurchasedDemographyQuote(message);
    }

    // Only explicit checks, tab entry, plan edits and turn closure project
    // the calendar; never poll from the frame loop or pointer hover.
    private void RefreshPurchasedDemographyQuote(string? message = null)
    {
        if (_demographicQuantity is null || _demographicSummary is null ||
            _demographicStatus is null || _demographicQueueButton is null ||
            _demographicCancelButton is null || _tacticalHost is not { } host) return;
        var ledger = host.Snapshot.PurchasedDemography.Get(_humanCivilization);
        var account = host.Snapshot.Treasury.Get(_humanCivilization);
        var born = host.GetHumanLastPurchasedNewborns(_humanCivilization);
        _demographicSummary.Text =
            $"Workers: {host.Snapshot.Workers.Workers.Count(w => w.Owner == _humanCivilization)}\n" +
            $"Purchased to date: {ledger.PurchasedWorkers} | Last births: {born.Count}\n" +
            $"Total spent: {GaDisplayNumbers.MoneyScaled(account.TotalDemographicPurchaseDebitsScaled)}";
        _demographicQueueButton.Disabled = true;
        bool ready = host.ReadyCivilizations.Contains(_humanCivilization);
        try
        {
            bool valid = ulong.TryParse(_demographicQuantity.Text,
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var quantity)
                && quantity > 0UL;
            // Zero quote retrieves private pending status even with invalid input.
            var quote = host.GetHumanDemographicQuote(_humanCivilization, valid ? quantity : 0UL);
            _demographicCancelButton.Disabled = ready || quote.PendingQuantity == 0UL;
            _demographicSummary.Text += $"\nAvailable cash before sales: {GaDisplayNumbers.MoneyScaled(quote.AvailableCashScaled)}\n" +
                $"Eligible workers: {quote.EligibleParents} | Pending purchase: {quote.PendingQuantity}";
            _demographicQueueButton.Disabled = ready || !valid || !quote.CanQueue;
            _demographicQueueButton.Text = quote.PendingQuantity == 0UL ? "QUEUE PURCHASE" : "REPLACE PENDING PURCHASE";
            _demographicStatus.Text = (valid
                ? $"Total price: ${GaDisplayNumbers.ExactMoneyInput(quote.MoneyMicro)}\n" +
                    (quote.CanQueue ? "Purchase available. E to submit after queuing." :
                        "Not enough available cash or eligible workers.")
                : "Enter a positive whole number.") +
                (quote.PendingQuantity > 0UL ? "\nPending purchase is private until turn closure." : "") +
                (message is null ? "" : "\n" + message);
        }
        catch (Exception ex)
        {
            // A bad/overflowing quantity must not disable cancellation of a valid pending plan.
            try { _demographicCancelButton.Disabled = ready || host.GetHumanDemographicQuote(_humanCivilization, 0UL).PendingQuantity == 0UL; }
            catch { _demographicCancelButton.Disabled = ready; }
            _demographicStatus.Text = "Purchase quote unavailable: " + ex.Message +
                (message is null ? "" : "\n" + message);
        }
    }

    private void ChangePurchasedDemographyPlan(bool cancel)
    {
        if (_tacticalHost is not { } host || _demographicQuantity is null) return;
        string message;
        try
        {
            if (cancel)
            {
                host.CancelHumanDemographicPurchase(_humanCivilization);
                message = "Pending purchase cancelled.";
            }
            else
            {
                if (!ulong.TryParse(_demographicQuantity.Text, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var quantity))
                    throw new InvalidOperationException("Enter a positive whole number.");
                host.QueueHumanDemographicPurchase(_humanCivilization, quantity);
                message = "Purchase queued privately. E to submit.";
            }
        }
        catch (Exception ex) { message = "Purchase rejected: " + ex.Message; }
        RefreshFineTacticalWorkerControls();
        RefreshPurchasedDemographyQuote(message);
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
