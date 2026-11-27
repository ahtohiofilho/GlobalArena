using Godot;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>R229.2: read-only, own-city production from the latest replayed
/// temporal close. No forecast, market simulation or inferred prices.</summary>
public partial class Main
{
    private ScrollContainer? _cityProductionTab;
    private Label? _cityProductionStatus;
    private GridContainer? _cityProductionGoods, _cityProductionTrade;
    private VBoxContainer? _productivePanel, _productiveWorkers;
    private GridContainer? _productiveCapital;
    private OptionButton? _productiveActivity;
    private LineEdit? _productiveAmount;
    private Button? _productiveQueue, _productiveCancel;
    private Label? _productiveStatus, _productiveFeedback;
    private ProgressBar? _productiveProgress;
    private FoundationProductiveTarget? _productiveSelection;
    private readonly HashSet<PhysicalWorkerId> _productiveSelectedWorkers = new();

    private void CreateCityProductionTab(TabContainer tabs)
    {
        _cityProductionTab = new ScrollContainer { Name = "Production",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        tabs.AddChild(_cityProductionTab);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        _cityProductionTab.AddChild(body);
        _cityProductionStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_cityProductionStatus);
        CreateProductiveInvestmentControls(body);
        _cityProductionGoods = new GridContainer { Name = "GANextCityProductionGoods", Columns = 5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_cityProductionGoods);
        body.AddChild(new HSeparator());
        body.AddChild(new Label { Text = "SALES & PHYSICAL TRADE | LAST CLOSED CYCLE" });
        _cityProductionTrade = new GridContainer { Name = "GANextCityProductionTrade", Columns = 6,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_cityProductionTrade);
        body.AddChild(new Label { Text = "Goods in commodity units; prices and revenue in $. Rounded for display. Imports and exports cross tile boundaries. — = no observed local price.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
    }

    private static void ClearCityProductionGrid(GridContainer grid)
    {
        foreach (Node old in grid.GetChildren()) { grid.RemoveChild(old); old.QueueFree(); }
    }

    private static void AddCityProductionRow(GridContainer grid, IReadOnlyList<string> values, bool heading = false)
    {
        for (int i = 0; i < values.Count; i++)
        {
            var label = new Label { Text = values[i],
                HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                SizeFlagsHorizontal = SizeFlags.ExpandFill };
            if (heading) label.AddThemeColorOverride("font_color", GaVisualFoundation.Muted);
            grid.AddChild(label);
        }
    }

    private static Dictionary<CommodityId, string> CityCommodityNames(FoundationTacticalHumanAiTurnHost host,
        PhysicalTacticalTileId node)
    {
        var names = new Dictionary<CommodityId, string>();
        if (host.AgriculturalProduction is { } agriculture)
        {
            foreach (var crop in CropCatalog.All)
                names.Add(agriculture.Resolve(node, crop.Id).Commodity, crop.Id.ToString());
            if (agriculture.BasicResources is { } basic)
            {
                names.Add(basic.Wood, "Wood");
                names.Add(basic.Stone, "Stone");
            }
        }
        return names;
    }

    private static string CityCommodityName(Dictionary<CommodityId, string> names, CommodityId id) =>
        names.TryGetValue(id, out var name) ? name : $"Commodity #{id.Value}";

    private void RefreshCityProduction()
    {
        if (_cityProductionStatus is null || _cityProductionGoods is null || _cityProductionTrade is null ||
            _cityWindowLayer?.Visible != true || _cityWindowPanel?.Visible != true) return;
        RefreshProductiveInvestmentControls();
        ClearCityProductionGrid(_cityProductionGoods);
        ClearCityProductionGrid(_cityProductionTrade);
        var host = _tacticalHost;
        if (host is null || _cityConnectionSource is not { } source)
        {
            _cityProductionStatus.Text = "Select a city on the map.";
            return;
        }
        var node = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, new StrategicCellId(source));
        if (!host.Snapshot.Workers.Workers.Any(w => w.Owner == _humanCivilization && w.TacticalTileId == node))
        {
            _cityProductionStatus.Text = "Local production unavailable: no own workers at this node.";
            return;
        }
        var read = host.GetLastClosedLocalEconomics(_humanCivilization, node);
        if (read is null)
        {
            _cityProductionStatus.Text = "No closed local economic data available.";
            return;
        }
        _cityProductionStatus.Text = $"PRODUCTION | CYCLE {read.EconomicCycleOrdinal} | NODE {source}\nOwn civilization | Actual closed results";
        var names = CityCommodityNames(host, node);
        AddCityProductionRow(_cityProductionGoods, new[] { "Commodity", "Output", "Demand", "Received", "Local price" }, true);
        AddCityProductionRow(_cityProductionTrade, new[] { "Commodity", "Sold", "Exports", "Imports", "Gross", "Net" }, true);
        foreach (var row in read.Commodities)
        {
            string name = CityCommodityName(names, row.Commodity);
            AddCityProductionRow(_cityProductionGoods, new[] { name, FormatPhysicalMicroForDisplay(row.ProducedMicro),
                FormatPhysicalMicroForDisplay(row.DemandMicro), FormatPhysicalMicroForDisplay(row.ReceivedMicro),
                row.LocalPriceMicro is { } price ? FormatMoneyMicroForDisplay(price) : "—" });
            AddCityProductionRow(_cityProductionTrade, new[] { name, FormatPhysicalMicroForDisplay(row.SoldMicro),
                FormatPhysicalMicroForDisplay(row.ExportedMicro), FormatPhysicalMicroForDisplay(row.ImportedMicro),
                FormatExactMoneyForDisplay(row.GrossSalesScaled), FormatExactMoneyForDisplay(row.SellerNetScaled) });
        }
    }

    private void CreateProductiveInvestmentControls(VBoxContainer body)
    {
        _productivePanel = new VBoxContainer { Name = "GANextProductiveInvestment", Visible = false };
        body.AddChild(_productivePanel);
        _productivePanel.AddChild(new Label { Text = "PRODUCTIVE INFRASTRUCTURE" });
        _productiveCapital = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _productivePanel.AddChild(_productiveCapital);
        _productiveActivity = new OptionButton { Name = "GANextProductiveActivity" };
        foreach (var activity in Enum.GetValues<FoundationProductiveActivity>())
            _productiveActivity.AddItem(activity.ToString(), (int)activity);
        _productiveActivity.Select(0);
        _productiveActivity.ItemSelected += _ => RefreshProductiveInvestmentControls();
        _productivePanel.AddChild(_productiveActivity);
        _productiveAmount = new LineEdit { Name = "GANextProductiveAmount", Text = "0.10", MaxLength = 32 };
        _productivePanel.AddChild(new Label { Text = "Investment amount ($)" });
        _productivePanel.AddChild(_productiveAmount);
        _productiveAmount.TextChanged += _ => RefreshProductiveInvestmentControls();
        var actions = new HBoxContainer();
        _productivePanel.AddChild(actions);
        _productiveQueue = new Button { Name = "GANextProductiveQueue", Text = "QUEUE PRODUCTIVE INVESTMENT", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _productiveCancel = new Button { Name = "GANextProductiveCancel", Text = "CANCEL PENDING", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddChild(_productiveQueue);
        actions.AddChild(_productiveCancel);
        _productiveQueue.Pressed += () => ChangeProductiveInvestment(false);
        _productiveCancel.Pressed += () => ChangeProductiveInvestment(true);
        _productiveStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _productivePanel.AddChild(_productiveStatus);
        _productiveProgress = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = true, Visible = false };
        _productivePanel.AddChild(_productiveProgress);
        _productiveFeedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _productivePanel.AddChild(_productiveFeedback);
        _productiveWorkers = new VBoxContainer { Name = "GANextProductiveWorkers" };
        _productivePanel.AddChild(_productiveWorkers);
        _productivePanel.AddChild(new HSeparator());
    }

    private bool TryReadProductiveAmount(out ulong micro)
    {
        micro = 0UL;
        if (_productiveAmount is null || !decimal.TryParse(_productiveAmount.Text,
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0m || amount > ulong.MaxValue / 1_000_000m) return false;
        var scaled = amount * 1_000_000m;
        if (scaled != decimal.Truncate(scaled)) return false;
        micro = (ulong)scaled;
        return micro > 0UL;
    }

    private void RefreshProductiveInvestmentControls()
    {
        if (_productivePanel is null || _productiveCapital is null || _productiveWorkers is null ||
            _productiveActivity is null || _productiveAmount is null || _productiveQueue is null ||
            _productiveCancel is null || _productiveStatus is null || _productiveFeedback is null ||
            _productiveProgress is null) return;
        _productivePanel.Visible = false;
        _productiveQueue.Disabled = _productiveCancel.Disabled = true;
        ClearCityProductionGrid(_productiveCapital);
        foreach (Node child in _productiveWorkers.GetChildren())
        { _productiveWorkers.RemoveChild(child); child.QueueFree(); }
        var host = _tacticalHost;
        if (host?.ProductiveInfrastructureEnabled != true || !_interactiveEconomicPreviewActive ||
            _cityConnectionSource is not { } source || _productiveActivity.Selected < 0) return;
        var node = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, new StrategicCellId(source));
        IReadOnlyList<FoundationProductiveCapitalReading> capital;
        IReadOnlyList<FoundationProductiveConstructionProject> projects;
        try
        {
            capital = host.GetHumanProductiveCapital(_humanCivilization, node);
            projects = host.GetHumanProductiveProjects(_humanCivilization, node);
        }
        catch (InvalidOperationException) { _productiveSelection = null; _productiveSelectedWorkers.Clear(); return; }
        _productivePanel.Visible = true;
        var target = new FoundationProductiveTarget(node, (FoundationProductiveActivity)_productiveActivity.GetSelectedId());
        if (_productiveSelection != target)
        {
            _productiveSelection = target;
            _productiveSelectedWorkers.Clear();
        }
        AddCityProductionRow(_productiveCapital, new[] { "Activity", "Active ($)", "Pending ($)", "Multiplier" }, true);
        foreach (var row in capital)
        {
            var multiplier = Math.Sqrt(1.0 + (double)row.ActivatedCapitalMoneyMicro / row.ReferenceMoneyMicro);
            AddCityProductionRow(_productiveCapital, new[] { row.Target.Activity.ToString(),
                FormatMoneyMicroForDisplay(row.ActivatedCapitalMoneyMicro),
                FormatMoneyMicroForDisplay(row.PendingCapitalMoneyMicro),
                FormattableString.Invariant($"{multiplier:0.00}×") });
        }
        var plans = host.GetHumanPendingProductiveFunding(_humanCivilization);
        var pending = plans.SingleOrDefault(p => p.Target == target);
        var project = projects.LastOrDefault(p => p.Target == target && !p.IsActivated);
        bool ready = host.ReadyCivilizations.Contains(_humanCivilization);
        var reserved = plans.Aggregate(System.Numerics.BigInteger.Zero,
            (sum, p) => sum + GlobalArenaMoneyScale.FromMoneyMicro(p.MoneyMicro)) +
            host.GetHumanPersistentFunding(_humanCivilization).Aggregate(System.Numerics.BigInteger.Zero,
            (sum, p) => sum + GlobalArenaMoneyScale.FromMoneyMicro(p.MoneyMicro));
        var available = host.Snapshot.Treasury.Get(_humanCivilization).CashScaled - reserved;
        bool valid = TryReadProductiveAmount(out var amount);
        var active = capital.Single(r => r.Target == target);
        bool range = amount <= ulong.MaxValue - active.ActivatedCapitalMoneyMicro;
        bool affordable = valid && available >= GlobalArenaMoneyScale.FromMoneyMicro(amount);
        _productiveAmount.Editable = !ready && pending is null && project is null;
        _productiveAmount.Visible = project is null;
        _productiveQueue.Visible = pending is null && project is null;
        _productiveCancel.Visible = pending is not null;
        _productiveQueue.Disabled = ready || pending is not null || project is not null || !valid || !range || !affordable;
        _productiveCancel.Disabled = ready || pending is null;
        _productiveProgress.Visible = project is not null;
        _productiveProgress.Value = project is null ? 0 : 100.0 * project.CompletedWorkMicro / project.RequiredWorkMicro;
        _productiveStatus.Text = project is not null
            ? $"Project #{project.Id} | Construction pending / in progress | {FormatPhysicalMicroForDisplay(project.CompletedWorkMicro)} / {FormatPhysicalMicroForDisplay(project.RequiredWorkMicro)} work"
            : pending is not null ? $"Private funding queued: {FormatMoneyMicroForDisplay(pending.MoneyMicro)} | E to submit"
            : active.ActivatedCapitalMoneyMicro > 0UL ? "Active capital | Benefits from the next opening cycle after completion."
            : "No active capital for this activity.";
        _productiveFeedback.Text = ready ? "Turn already submitted." : project is not null ? "Assign or remove construction workers." :
            pending is not null ? "Funding is private until the turn closes." : !valid ? "Enter a positive amount (up to six decimals)." :
            !range ? "Capital exceeds the supported range." : !affordable ? "Insufficient unreserved treasury cash." :
            "Select workers and queue funding. Remote workers must travel to the site.";
        _productiveWorkers.AddChild(new Label { Text = "WORKFORCE" });
        foreach (var q in host.GetHumanPersistentQueues(_humanCivilization))
        {
            var worker = host.Snapshot.Workers.Workers.Single(w => w.Id == q.Worker);
            string label = $"Worker #{q.Worker.Value} | Node {worker.TacticalTileId!.Value.FineStrategicCellId.Value}" +
                (worker.TacticalTileId == node ? " | At site" : " | Travel required");
            if (project is not null)
            {
                var order = q.Orders.FirstOrDefault(o => o.Kind == FoundationWorkerOrderKind.BuildProductive && o.ProjectId == project.Id);
                var button = new Button { Text = label + (order is null ? " | Assign" : q.ActiveOrderId == order.Id ? " | Interrupt" : " | Remove"), Disabled = ready };
                button.Pressed += () =>
                {
                    try
                    {
                        if (order is null) host.QueueHumanPersistentEdit(_humanCivilization, new FoundationPersistentOrderEdit(
                            _humanCivilization, q.Worker, FoundationPersistentOrderEditKind.Insert, Index: q.Orders.Count,
                            OrderKind: FoundationWorkerOrderKind.BuildProductive, ProjectId: project.Id));
                        else RemovePersistentOrder(q, order.Id);
                        RefreshFineTacticalWorkerControls();
                    }
                    catch (Exception ex) { _productiveFeedback!.Text = "Command rejected: " + ex.Message; }
                };
                _productiveWorkers.AddChild(button);
            }
            else
            {
                var check = new CheckBox { Text = label, Disabled = ready || pending is not null,
                    ButtonPressed = pending is not null ? pending.Assignments.Any(a => a.Worker == q.Worker) : _productiveSelectedWorkers.Contains(q.Worker) };
                check.Toggled += value => { if (value) _productiveSelectedWorkers.Add(q.Worker); else _productiveSelectedWorkers.Remove(q.Worker); };
                _productiveWorkers.AddChild(check);
            }
        }
    }

    private void ChangeProductiveInvestment(bool cancel)
    {
        var host = _tacticalHost;
        if (host is null || _productiveSelection is not { } target || _productivePanel?.IsVisibleInTree() != true ||
            (cancel ? _productiveCancel?.Disabled != false : _productiveQueue?.Disabled != false)) return;
        try
        {
            if (cancel)
            {
                if (!host.CancelHumanProductiveFunding(_humanCivilization, target))
                    throw new InvalidOperationException("No private funding to cancel.");
            }
            else
            {
                if (!TryReadProductiveAmount(out var amount)) return;
                var queues = host.GetHumanPersistentQueues(_humanCivilization);
                host.QueueHumanProductiveFunding(_humanCivilization, target, amount,
                    queues.Where(q => _productiveSelectedWorkers.Contains(q.Worker))
                        .Select(q => new FoundationFundedWorkerInsertion(q.Worker, q.Orders.Count)));
                _productiveSelectedWorkers.Clear();
            }
            RefreshFineTacticalWorkerControls();
        }
        catch (Exception ex) { if (_productiveFeedback is not null) _productiveFeedback.Text = "Command rejected: " + ex.Message; }
    }

    private void ExerciseProductiveInvestmentSmoke(FoundationTacticalHumanAiTurnHost host)
    {
        if (!host.ProductiveInfrastructureEnabled || host.StateHash.FormatVersion != 31U)
            throw new InvalidOperationException("Missing explicit productive infrastructure deployment.");
        var worker = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization &&
            _r123FineGeography!.SurfaceAtIndex((int)w.TacticalTileId!.Value.FineStrategicCellId.Value - 1) == FoundationFineSurfaceKind.Land);
        var node = worker.TacticalTileId!.Value;
        FocusCityProductionSmokeSite(host, node);
        var tabs = FindChild("GANextCityTabs", true, false) as TabContainer
            ?? throw new InvalidOperationException("City tabs missing.");
        tabs.CurrentTab = 2;
        if (_productivePanel?.IsVisibleInTree() != true || _productiveCapital?.GetChildCount() != 16)
            throw new InvalidOperationException("Productive controls unavailable before economic close.");
        EndFineCollectiveTurn();
        if (host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != 1UL)
            throw new InvalidOperationException("Productive smoke could not close opening cycle.");
        FocusCityProductionSmokeSite(host, node);
        tabs.CurrentTab = 2;
        var target = new FoundationProductiveTarget(node, FoundationProductiveActivity.Agriculture);
        var before = host.StateHash;
        const ulong money = 10_000UL;
        _productiveAmount!.Text = GaDisplayNumbers.ExactMoneyInput(money);
        RefreshProductiveInvestmentControls();
        var check = _productiveWorkers!.GetChildren().OfType<CheckBox>()
            .Single(b => b.Text.StartsWith($"Worker #{worker.Id.Value} |", StringComparison.Ordinal));
        check.ButtonPressed = true;
        if (_productiveQueue?.Disabled != false) throw new InvalidOperationException("Affordable funding action unavailable.");
        _productiveQueue.EmitSignal(Button.SignalName.Pressed);
        var plan = host.GetHumanPendingProductiveFunding(_humanCivilization).Single();
        if (plan.Target != target || plan.MoneyMicro != money || plan.Assignments.Single().Worker != worker.Id ||
            host.StateHash != before || host.GetHumanProductiveProjects(_humanCivilization, node).Count != 0)
            throw new InvalidOperationException("Productive queue did not stage atomic private funding and workforce.");
        _productiveCancel!.EmitSignal(Button.SignalName.Pressed);
        if (host.GetHumanPendingProductiveFunding(_humanCivilization).Count != 0 || host.StateHash != before)
            throw new InvalidOperationException("Productive funding cancellation mutated authority.");
        _productiveQueue.EmitSignal(Button.SignalName.Pressed);
        if (host.GetHumanPendingProductiveFunding(_humanCivilization).Single().Assignments.Count != 0)
            throw new InvalidOperationException("Canceled workforce selection was not cleared.");
        EndFineCollectiveTurn();
        var pending = host.GetHumanProductiveProjects(_humanCivilization, node).Single();
        if (pending.IsActivated || pending.CompletedWorkMicro != 0UL || _productiveProgress?.IsVisibleInTree() != true)
            throw new InvalidOperationException("Funded project did not display actual pending progress.");
        var assign = _productiveWorkers.GetChildren().OfType<Button>()
            .Single(b => b.Text.StartsWith($"Worker #{worker.Id.Value} |", StringComparison.Ordinal) && b.Text.EndsWith(" | Assign", StringComparison.Ordinal));
        before = host.StateHash;
        assign.EmitSignal(Button.SignalName.Pressed);
        if (host.StateHash != before || !host.GetHumanPersistentQueues(_humanCivilization).Single(q => q.Worker == worker.Id)
            .Orders.Any(o => o.Kind == FoundationWorkerOrderKind.BuildProductive && o.ProjectId == pending.Id))
            throw new InvalidOperationException("Productive assignment did not reach the canonical typed queue.");
        EndFineCollectiveTurn();
        var active = host.GetHumanProductiveCapital(_humanCivilization, node).Single(c => c.Target == target);
        if (active.ActivatedCapitalMoneyMicro != money || active.PendingCapitalMoneyMicro != 0UL ||
            host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != 3UL || _productiveProgress?.Visible != false)
            throw new InvalidOperationException("Productive activation did not refresh the open City tab.");
        var foreign = host.Snapshot.Workers.Workers.First(w => w.Owner != _humanCivilization &&
            w.TacticalTileId is { } tile && !host.Snapshot.Workers.Workers.Any(own => own.Owner == _humanCivilization && own.TacticalTileId == tile));
        before = host.StateHash;
        FocusCityProductionSmokeSite(host, foreign.TacticalTileId!.Value);
        if (_productivePanel.Visible || host.StateHash != before)
            throw new InvalidOperationException("Foreign city exposed productive controls or mutated authority.");
        GD.Print("GA_NEXT_R235F2_PRODUCTIVE_UI_PASS real_controls=true private_funding=true cancellation=true assignment=true activation=true foreign_hidden=true");
    }

    private void FocusCityProductionSmokeSite(FoundationTacticalHumanAiTurnHost host, PhysicalTacticalTileId node)
    {
        var map = _tacticalPreview ?? throw new InvalidOperationException("Missing tactical map.");
        ulong tile = node.FineStrategicCellId.Value;
        if (!map.LocalSiteMarkerTileIds.Contains(tile))
        {
            var incidence = host.Incidence.TileIncidences[checked((int)tile - 1)];
            var focus = incidence.IncidentCoarseCellIds.OrderBy(id => id.Value).First();
            map.Open(focus.Value);
        }
        if (!map.LocalSiteMarkerTileIds.Contains(tile))
            throw new InvalidOperationException($"Cannot project actual city {tile} for production smoke.");
        OnCartographicLocalSiteSelected(tile);
        OpenSelectedCityWindow();
        if (_cityConnectionSource != tile)
            throw new InvalidOperationException($"City selection did not reach node {tile}.");
    }

    private void ExerciseCityProductionReadout(FoundationTacticalHumanAiTurnHost host, PhysicalTacticalTileId node, ulong cycle)
    {
        var before = host.StateHash;
        FocusCityProductionSmokeSite(host, node);
        var tabs = FindChild("GANextCityTabs", true, false) as TabContainer
            ?? throw new InvalidOperationException("City tabs missing.");
        tabs.CurrentTab = 2; // Preserve historical Connections index 1.
        if (_cityProductionTab?.IsVisibleInTree() != true || _cityProductionStatus is null ||
            _cityProductionGoods is null || _cityProductionTrade is null)
            throw new InvalidOperationException("Production tab is not visible.");
        if (cycle == 0UL)
        {
            if (!_cityProductionStatus.Text.Contains("No closed local economic data") ||
                _cityProductionGoods.GetChildCount() != 0 || _cityProductionTrade.GetChildCount() != 0)
                throw new InvalidOperationException("Production displayed invented opening values.");
        }
        else
        {
            var read = host.GetLastClosedLocalEconomics(_humanCivilization, node)
                ?? throw new InvalidOperationException("Missing authorized closed local data.");
            if (read.EconomicCycleOrdinal != cycle || read.Commodities.Count != 8 ||
                !_cityProductionStatus.Text.Contains($"CYCLE {cycle}") ||
                _cityProductionGoods.GetChildCount() != 45 || _cityProductionTrade.GetChildCount() != 54)
                throw new InvalidOperationException("Production tab catalog or cycle mismatch.");
            var names = CityCommodityNames(host, node);
            if (names.Count != 8 || !names.Values.Contains("Wood") || !names.Values.Contains("Stone"))
                throw new InvalidOperationException("Basic resources and crop names unavailable.");
            for (int i = 0; i < read.Commodities.Count; i++)
            {
                var row = read.Commodities[i];
                var goods = new[] { CityCommodityName(names, row.Commodity), FormatPhysicalMicroForDisplay(row.ProducedMicro),
                    FormatPhysicalMicroForDisplay(row.DemandMicro), FormatPhysicalMicroForDisplay(row.ReceivedMicro),
                    row.LocalPriceMicro is { } price ? FormatMoneyMicroForDisplay(price) : "—" };
                var trade = new[] { goods[0], FormatPhysicalMicroForDisplay(row.SoldMicro),
                    FormatPhysicalMicroForDisplay(row.ExportedMicro), FormatPhysicalMicroForDisplay(row.ImportedMicro),
                    FormatExactMoneyForDisplay(row.GrossSalesScaled), FormatExactMoneyForDisplay(row.SellerNetScaled) };
                for (int j = 0; j < goods.Length; j++)
                    if (((Label)_cityProductionGoods.GetChild((i + 1) * goods.Length + j)).Text != goods[j])
                        throw new InvalidOperationException("Rendered production cell differs from committed evidence.");
                for (int j = 0; j < trade.Length; j++)
                    if (((Label)_cityProductionTrade.GetChild((i + 1) * trade.Length + j)).Text != trade[j])
                        throw new InvalidOperationException("Rendered trade cell differs from committed evidence.");
            }
            // Do not display the same data for an exclusively foreign city.
            var foreign = host.Snapshot.Workers.Workers.FirstOrDefault(w => w.Owner != _humanCivilization &&
                w.TacticalTileId is { } tile && !host.Snapshot.Workers.Workers.Any(own =>
                    own.Owner == _humanCivilization && own.TacticalTileId == tile));
            if (foreign is null)
                throw new InvalidOperationException("Local economy privacy smoke requires an exclusively foreign occupied tile.");
            // A physical site beyond the current chart horizon has no projected
            // marker. Navigate first (same canonical incidence used by R134),
            // then use the ordinary site-selection handler, never assign the
            // private selected node directly or silently skip this assertion.
            var foreignNode = foreign.TacticalTileId!.Value;
            FocusCityProductionSmokeSite(host, foreignNode);
            if (_cityConnectionSource != foreignNode.FineStrategicCellId.Value ||
                _cityWindowTitle?.Text != $"CITY | NODE {foreignNode.FineStrategicCellId.Value}" ||
                _cityProductionStatus.Text != "Local production unavailable: no own workers at this node." ||
                _cityProductionGoods.GetChildCount() != 0 || _cityProductionTrade.GetChildCount() != 0)
                throw new InvalidOperationException("Foreign city identity or production privacy failed.");
            bool denied = false;
            try { _ = host.GetLastClosedLocalEconomics(_humanCivilization, foreignNode); }
            catch (InvalidOperationException) { denied = true; }
            if (!denied) throw new InvalidOperationException("Foreign node read boundary did not reject access.");
            FocusCityProductionSmokeSite(host, node);
            if (_cityConnectionSource != node.FineStrategicCellId.Value ||
                _cityWindowTitle?.Text != $"CITY | NODE {node.FineStrategicCellId.Value}" ||
                !_cityProductionStatus.Text.Contains($"CYCLE {cycle}") ||
                _cityProductionGoods.GetChildCount() != 45 || _cityProductionTrade.GetChildCount() != 54)
                throw new InvalidOperationException("Returning to own city did not restore authorized production.");
            GD.Print($"GA_NEXT_R22921_CITY_PRIVACY_PASS own={node.FineStrategicCellId.Value} foreign={foreignNode.FineStrategicCellId.Value} cycle={cycle}");
        }
        if (host.StateHash != before) throw new InvalidOperationException("City production changed canonical state.");
        _cityWindowLayer!.Visible = false;
    }
}
