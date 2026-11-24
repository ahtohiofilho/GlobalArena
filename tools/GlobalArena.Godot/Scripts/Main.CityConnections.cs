using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R180 presentation-only city window. Tabs enumerate the selected physical
/// node and its immediate links, never synthetic long-distance trade routes.
/// </summary>
public partial class Main
{
    private CenterContainer? _cityWindowLayer;
    private PanelContainer? _cityWindowPanel;
    private ulong? _cityConnectionSource;
    private Label? _cityWindowTitle;
    private Label? _cityOverview;
    private VBoxContainer? _cityLinksList;
    private PanelContainer? _cityLinkDetail;
    private Label? _cityLinkDetailText;
    private Button? _connectionInvest;
    private LineEdit? _connectionAmount;
    private Label? _connectionProjection;
    private Button? _connectionCancel;
    private Label? _connectionFeedback;
    private VBoxContainer? _connectionFunding;
    private Label? _constructionWorkforceTitle;
    private ulong? _connectionDestination;
    private VBoxContainer? _constructionSection;
    private Label? _constructionStatus;
    private ProgressBar? _constructionProgress;
    private VBoxContainer? _constructionWorkers;

    private void CreateCityWindow()
    {
        var overlay = new CenterContainer
        {
            Name = "GANextCityWindowLayer",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
        _cityWindowLayer = overlay;

        var panel = new PanelContainer
        {
            Name = "GANextCityWindow",
            CustomMinimumSize = new Vector2(575f, 450f),
            MouseFilter = MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        overlay.AddChild(panel);
        _cityWindowPanel = panel;
        var column = new VBoxContainer();
        panel.AddChild(column);

        var header = new HBoxContainer();
        column.AddChild(header);
        _cityWindowTitle = new Label
        {
            Text = "CITY | Select a node",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        header.AddChild(_cityWindowTitle);
        var close = new Button { Text = "CLOSE" };
        close.Pressed += () => overlay.Visible = false;
        header.AddChild(close);

        var tabs = new TabContainer
        {
            Name = "GANextCityTabs",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddChild(tabs);
        var overview = new MarginContainer { Name = "Overview" };
        tabs.AddChild(overview);
        _cityOverview = new Label
        {
            Text = "Select a physical node on the map.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Top,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        overview.AddChild(_cityOverview);

        var links = new ScrollContainer { Name = "Connections" };
        tabs.AddChild(links);
        _cityLinksList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        links.AddChild(_cityLinksList);

        _cityLinkDetail = new PanelContainer
        {
            Name = "GANextConnectionDetail",
            Visible = false,
            CustomMinimumSize = new Vector2(440f, 410f),
            MouseFilter = MouseFilterEnum.Stop
        };
        _cityLinkDetail.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        overlay.AddChild(_cityLinkDetail);
        var details = new VBoxContainer();
        _cityLinkDetail.AddChild(details);
        var detailsHeader = new HBoxContainer();
        details.AddChild(detailsHeader);
        detailsHeader.AddChild(new Label
        {
            Text = "CONNECTION",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        var hideDetails = new Button { Text = "BACK TO CITY" };
        hideDetails.Pressed += CloseConnectionDetail;
        detailsHeader.AddChild(hideDetails);
        // A bounded scroll viewport prevents a large workforce from growing
        // the window beyond its normal footprint. Header stays accessible.
        var connectionScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            ClipContents = true
        };
        details.AddChild(connectionScroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        connectionScroll.AddChild(body);
        _cityLinkDetailText = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        body.AddChild(_cityLinkDetailText);
        _connectionFunding = new VBoxContainer();
        body.AddChild(_connectionFunding);
        _connectionFunding.AddChild(new Label { Text = "Investment amount" });
        _connectionAmount = new LineEdit { Text = "0.100000", MaxLength = 32 };
        _connectionAmount.TextChanged += _ => RefreshConnectionInvestmentControls();
        _connectionFunding.AddChild(_connectionAmount);
        _connectionProjection = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_connectionProjection);
        _connectionInvest = new Button
        {
            Text = "QUEUE INVESTMENT",
            Disabled = true
        };
        _connectionInvest.Pressed += () => ChangeConnectionInvestment(false);
        _connectionFunding.AddChild(_connectionInvest);
        _connectionCancel = new Button { Text = "CANCEL PENDING INVESTMENT", Disabled = true };
        _connectionCancel.Pressed += () => ChangeConnectionInvestment(true);
        _connectionFunding.AddChild(_connectionCancel);
        _connectionFeedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_connectionFeedback);
        _constructionSection = new VBoxContainer { Name = "GANextConstruction", Visible = false };
        body.AddChild(_constructionSection);
        _constructionSection.AddChild(new Label { Text = "CONSTRUCTION" });
        _constructionStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _constructionSection.AddChild(_constructionStatus);
        _constructionProgress = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = true };
        _constructionSection.AddChild(_constructionProgress);
        _constructionWorkforceTitle = new Label { Text = "WORKFORCE" };
        _constructionSection.AddChild(_constructionWorkforceTitle);
        _constructionWorkers = new VBoxContainer();
        _constructionSection.AddChild(_constructionWorkers);
    }

    private void OpenSelectedCityWindow()
    {
        if (_cityWindowLayer is null || _cityOverview is null ||
            _cityLinksList is null || _cityWindowTitle is null ||
            _cityLinkDetail is null) return;

        foreach (Node old in _cityLinksList.GetChildren())
        {
            _cityLinksList.RemoveChild(old);
            old.QueueFree();
        }
        CloseConnectionDetail();
        _cityConnectionSource = null;
        // Main management windows are mutually exclusive.
        if (_civilizationWindowLayer is not null)
            _civilizationWindowLayer.Visible = false;
        _cityWindowLayer.Visible = true;

        var host = _tacticalHost;
        var selected = _selectedCartographicSite;
        if (host is null || selected is null ||
            _tacticalPreview?.IsCartographicSiteMode != true ||
            !_tacticalPreview.LocalSiteMarkerTileIds.Contains(selected.Value))
        {
            _cityWindowTitle.Text = "CITY | Select a settlement";
            _cityOverview.Text = "Select a city on the map.";
            _cityLinksList.AddChild(new Label
            {
                Text = "Local connections appear after selecting a city."
            });
            return;
        }

        var node = new PhysicalTacticalTileId(
            host.Incidence.Refinement.FineParameters,
            new StrategicCellId(selected.Value));
        var rows = FoundationLocalConnectionsProjector.Read(
            host.Snapshot, host.Incidence, node);
        _cityConnectionSource = selected.Value;
        _cityWindowTitle.Text = $"CITY | NODE {selected.Value}";
        _cityOverview.Text =
            $"Physical node #{selected.Value} on the tactical map.\n" +
            $"{rows.Count} direct connections.";
        foreach (var link in rows)
        {
            var displayed = link;
            var button = new Button
            {
                Text = $"Node {link.Neighbor.FineStrategicCellId.Value}   " +
                    $"| physical freight {FormatMoneyMicroForDisplay(link.UnitPhysicalFreightMicro)}/cargo",
                Alignment = HorizontalAlignment.Left
            };
            button.Pressed += () => ShowCityConnectionDetail(displayed);
            _cityLinksList.AddChild(button);
        }
    }

    private void CloseConnectionDetail()
    {
        _connectionDestination = null;
        if (_cityLinkDetail is not null) _cityLinkDetail.Visible = false;
        if (_cityWindowPanel is not null) _cityWindowPanel.Visible = true;
    }

    private void ShowCityConnectionDetail(FoundationLocalConnection link)
    {
        if (_cityLinkDetail is null || _cityLinkDetailText is null ||
            _cityWindowPanel is null || !_cityConnectionSource.HasValue)
            return;
        _connectionDestination = link.Neighbor.FineStrategicCellId.Value;
        _cityWindowPanel.Visible = false;
        _cityLinkDetail.Visible = true;
        _cityLinkDetailText.Text =
            $"From node: {_cityConnectionSource.Value}\n" +
            $"To node: {link.Neighbor.FineStrategicCellId.Value}\n" +
            $"Current freight: {FormatMoneyMicroForDisplay(link.UnitPhysicalFreightMicro)}/cargo\n" +
            $"External tariff: {FormatMoneyMicroForDisplay(link.ExternalTariffMicro)}/cargo\n" +
            $"Owner: {(link.InfrastructureOwner.HasValue
                ? link.InfrastructureOwner.Value.Value.ToString()
                : "Unowned")}\n" +
            $"Active capital: {FormatMoneyMicroForDisplay(link.InvestedMoneyMicro)}\n" +
            ReadConnectionWorkforce(_cityConnectionSource.Value,
                link.Neighbor.FineStrategicCellId.Value) + "\n" +
            ReadConnectionInvestment(_cityConnectionSource.Value,
                link.Neighbor.FineStrategicCellId.Value);
        RefreshConnectionInvestmentControls();
    }

    private PhysicalTacticalEdgeId ResolveConnectionEdge(ulong source, ulong destination)
    {
        var host = _tacticalHost ?? throw new InvalidOperationException("Host unavailable.");
        var edge = host.Incidence.FineTopology.Edges.Single(e =>
            e.IncidentCellIds.Any(c => c.Value == source) &&
            e.IncidentCellIds.Any(c => c.Value == destination));
        return new PhysicalTacticalEdgeId(host.Incidence.Refinement.FineParameters, edge.Id);
    }

    private bool TryReadConnectionAmount(out ulong micro)
    {
        micro = 0UL;
        if (_connectionAmount is null ||
            !decimal.TryParse(_connectionAmount.Text,
                System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0m || amount > ulong.MaxValue / 1000000m)
            return false;
        var scaled = amount * 1000000m;
        if (scaled != decimal.Truncate(scaled)) return false;
        micro = (ulong)scaled;
        return micro > 0UL;
    }

    private void RefreshConnectionInvestmentControls()
    {
        if (_connectionInvest is null || _connectionCancel is null || _connectionFeedback is null)
            return;
        _connectionInvest.Disabled = _connectionCancel.Disabled = true;
        if (_connectionProjection is not null) _connectionProjection.Text = "Projection unavailable.";
        var host = _tacticalHost;
        if (!_interactiveEconomicPreviewActive || host is null ||
            !_cityConnectionSource.HasValue || !_connectionDestination.HasValue)
        {
            _connectionFeedback.Text = "Investment commands unavailable in this preview.";
            return;
        }
        var id = ResolveConnectionEdge(_cityConnectionSource.Value, _connectionDestination.Value);
        var plans = host.GetHumanMonetaryInvestments(_humanCivilization);
        var pendingPlan = plans.SingleOrDefault(p => p.Edge == id);
        bool pending = pendingPlan is not null;
        bool construction = host.Snapshot.Constructions.IsInitialized;
        var project = host.Snapshot.Constructions.Projects.LastOrDefault(p => p.Edge == id && !p.IsActivated);
        _connectionInvest.Text = construction ? "QUEUE CONSTRUCTION FUNDING" : "QUEUE INVESTMENT";
        _connectionCancel.Text = construction ? "CANCEL PENDING FUNDING" : "CANCEL PENDING INVESTMENT";
        bool ready = host.ReadyCivilizations.Contains(_humanCivilization);
        var physical = host.Snapshot.Transport.Edges.Single(e => e.Edge == id);
        bool financeable = physical.NaturalConstructionCostMoneyMicro is > 0UL &&
            physical.CargoMicroAtReferenceLoad != ulong.MaxValue &&
            physical.OwnershipKind != PhysicalTransportOwnershipKind.UnownedInfrastructure &&
            (physical.OwnershipKind != PhysicalTransportOwnershipKind.Owned ||
                physical.InfrastructureOwner == _humanCivilization);
        var reserved = plans.Aggregate(System.Numerics.BigInteger.Zero,
            (sum, p) => sum + GlobalArenaMoneyScale.FromMoneyMicro(p.MoneyMicro));
        var available = host.Snapshot.Treasury.Get(_humanCivilization).CashScaled - reserved;
        bool valid = TryReadConnectionAmount(out var amount);
        var projectedAmount = project?.FundedMoneyMicro ?? pendingPlan?.MoneyMicro ?? amount;
        bool withinRange = projectedAmount <= ulong.MaxValue - physical.CommittedInvestmentMoneyMicro.GetValueOrDefault();
        bool affordable = valid && available >= GlobalArenaMoneyScale.FromMoneyMicro(amount);
        if (_connectionFunding is not null) _connectionFunding.Visible = project is null;
        if (_connectionAmount is not null && pendingPlan is not null && construction)
        {
            // The authoritative reservation, not a stale value from another edge.
            _connectionAmount.Text = FormatMoneyMicroForDisplay(pendingPlan.MoneyMicro);
        }
        if (_connectionAmount is not null) _connectionAmount.Editable = !ready && !pending && project is null;
        if (_connectionProjection is not null && financeable && (project is not null || pending || valid) && withinRange)
        {
            var before = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(physical);
            var after = FoundationPhysicalAuthoritativeTransportProjector.ProjectUnitFreightCost(physical, projectedAmount);
            _connectionProjection.Text =
                $"Projected freight: {FormatMoneyMicroForDisplay(after)}/cargo\n" +
                $"Reduction: {FormatMoneyMicroForDisplay(before - after)} | {(construction ? "Next cycle after completion." : "After settlement.")}";
        }
        _connectionInvest.Disabled = ready || pending || project is not null || !financeable || !affordable || !withinRange;
        _connectionCancel.Disabled = ready || !pending;
        _connectionCancel.Visible = !construction || pending;
        _connectionInvest.Visible = !construction || !pending;
        _connectionFeedback.Text = ready ? "Turn already submitted." :
            project is not null ? (project.Investor == _humanCivilization ? "Assign workers for this turn." : "Project funded by another civilization.") :
            pending ? "Funding pending. Press E to commit the turn." :
            !financeable ? "This connection cannot be financed by your civilization." :
            !valid ? "Enter a positive amount with at most six decimal places." :
            !withinRange ? "Capital exceeds the canonical money range." :
            !affordable ? "Insufficient unreserved treasury cash." :
            "Queue funding; cash is debited only on turn settlement.";
        RefreshConstructionSection(id, ready);
    }

    private void RefreshConstructionSection(PhysicalTacticalEdgeId id, bool ready)
    {
        var host = _tacticalHost;
        if (_constructionSection is null || _constructionStatus is null || _constructionProgress is null || _constructionWorkers is null || host is null) return;
        foreach (Node child in _constructionWorkers.GetChildren()) { _constructionWorkers.RemoveChild(child); child.QueueFree(); }
        var project = host.Snapshot.Constructions.Projects.LastOrDefault(p => p.Edge == id && !p.IsActivated);
        _constructionSection.Visible = host.Snapshot.Constructions.IsInitialized && project is not null;
        _constructionProgress.Value = 0;
        _constructionStatus.Text = "";
        if (project is null || !_constructionSection.Visible) return;
        _constructionProgress.Value = 100.0 * project.CompletedWorkMicro / project.RequiredWorkMicro;
        _constructionStatus.Text =
            $"Project #{project.Id} | {(project.IsComplete ? "Awaiting activation" : "Under construction")}\nFunded capital: {FormatMoneyMicroForDisplay(project.FundedMoneyMicro)}\nWork: {FormatMoneyMicroForDisplay(project.CompletedWorkMicro)} / {FormatMoneyMicroForDisplay(project.RequiredWorkMicro)}";
        bool showWorkers = !project.IsComplete && project.Investor == _humanCivilization;
        if (_constructionWorkforceTitle is not null) _constructionWorkforceTitle.Visible = showWorkers;
        _constructionWorkers.Visible = showWorkers;
        if (!showWorkers) return;
        var plans = host.GetHumanConstructionOrders(_humanCivilization);
        var otherOrders = host.GetHumanOrders(_humanCivilization);
        var edge = host.Incidence.FineTopology.Edges.Single(e => e.Id == id.FineStrategicEdgeId);
        var fine = _r123FineGeography;
        if (fine is null) return;
        var kind = fine.CrossingAtIndices((int)edge.IncidentCellIds[0].Value - 1, (int)edge.IncidentCellIds[1].Value - 1);
        foreach (var worker in host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization && w.TacticalTileId.HasValue && edge.IncidentCellIds.Contains(w.TacticalTileId.Value.FineStrategicCellId)))
        {
            var plan = plans.SingleOrDefault(p => p.Worker == worker.Id);
            bool assigned = plan?.ProjectId == project.Id;
            bool eligible = worker.Activity == PhysicalWorkerActivity.Other && worker.Occupation == PhysicalWorkerOccupation.Unassigned &&
                !host.Snapshot.EdgeTravels.Pending.Any(p => p.Worker == worker.Id) && !otherOrders.Any(o => o.WorkerId == worker.Id) &&
                (kind == FoundationFineTransportCrossingKind.Ground || (kind == FoundationFineTransportCrossingKind.Port && fine.FineElevationRaw[(int)worker.TacticalTileId!.Value.FineStrategicCellId.Value - 1] > fine.SeaLevelRaw));
            var button = new Button { Text = $"Worker #{worker.Id.Value} | {(assigned ? "Cancel assignment" : eligible && plan is null ? "Assign" : "Unavailable")}", Disabled = ready || (!assigned && (!eligible || plan is not null)) };
            var workerId = worker.Id;
            button.Pressed += () => { try { if (assigned) host.CancelHumanConstructionWork(_humanCivilization, workerId); else host.QueueHumanConstructionWork(_humanCivilization, workerId, project.Id); RefreshOpenConnectionInvestment(); } catch (Exception ex) { if (_connectionFeedback is not null) _connectionFeedback.Text = "Command rejected: " + ex.Message; } };
            _constructionWorkers.AddChild(button);
        }
        if (_constructionWorkers.GetChildCount() == 0) _constructionWorkers.AddChild(new Label { Text = "No local workers." });
    }

    private void ChangeConnectionInvestment(bool cancel)
    {
        var host = _tacticalHost;
        if (host is null || !_interactiveEconomicPreviewActive ||
            !_cityConnectionSource.HasValue || !_connectionDestination.HasValue ||
            _cityLinkDetail?.IsVisibleInTree() != true) return;
        if (cancel ? _connectionCancel?.Disabled != false : _connectionInvest?.Disabled != false)
            return;
        var source = _cityConnectionSource.Value;
        var destination = _connectionDestination.Value;
        var id = ResolveConnectionEdge(source, destination);
        try
        {
            if (cancel)
            {
                if (!host.CancelHumanMonetaryInvestment(_humanCivilization, id))
                    throw new InvalidOperationException("No pending investment on this connection.");
                _r144QueuedCapital.Remove(id);
            }
            else
            {
                var edge = host.Snapshot.Transport.Edges.Single(e => e.Edge == id);
                var freight = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(edge);
                var cargo = host.GetLastClosedEconomicTradeView(_humanCivilization).UsedEdges
                    .FirstOrDefault(e => e.Edge == id)?.TotalCargoMicro ?? 0UL;
                if (!TryReadConnectionAmount(out var amount)) return;
                host.QueueHumanMonetaryInvestment(_humanCivilization, id, amount);
                if (!host.Snapshot.Constructions.IsInitialized) _r144QueuedCapital[id] = (freight, cargo, amount);
            }
            _portQaLastSettledAudit = null;
            RefreshPortInvestmentQaControls();
            RefreshOpenConnectionInvestment();
        }
        catch (Exception ex)
        {
            RefreshConnectionInvestmentControls();
            if (_connectionFeedback is not null)
                _connectionFeedback.Text = "Command rejected: " + ex.Message;
        }
    }

    private void RefreshOpenConnectionInvestment()
    {
        var host = _tacticalHost;
        if (host is null || _cityLinkDetail?.IsVisibleInTree() != true ||
            !_cityConnectionSource.HasValue || !_connectionDestination.HasValue) return;
        var source = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters,
            new StrategicCellId(_cityConnectionSource.Value));
        var link = FoundationLocalConnectionsProjector.Read(host.Snapshot, host.Incidence, source)
            .Single(r => r.Neighbor.FineStrategicCellId.Value == _connectionDestination.Value);
        ShowCityConnectionDetail(link);
    }

    // Read the same authoritative pending plan as the investment laboratory.
    // Resolve the undirected physical edge by its exact incident nodes.
    private string ReadConnectionInvestment(ulong source, ulong destination)
    {
        var host = _tacticalHost;
        if (host is null) return "Pending investment: unavailable";
        var edge = host.Incidence.FineTopology.Edges.Single(e =>
            e.IncidentCellIds.Any(c => c.Value == source) &&
            e.IncidentCellIds.Any(c => c.Value == destination));
        var id = new PhysicalTacticalEdgeId(
            host.Incidence.Refinement.FineParameters, edge.Id);
        var plan = host.GetHumanMonetaryInvestments(_humanCivilization)
            .SingleOrDefault(p => p.Edge == id);
        return $"Physical connection ID: {edge.Id.Value}\n" +
            (plan is null ? "Your pending investment: None" :
                $"Your pending investment: {FormatMoneyMicroForDisplay(plan.MoneyMicro)} (not yet spent)");
    }

    // Presence is not construction eligibility: workers can have other orders
    // or occupations. Do not authorize investment from this read-only count.
    private string ReadConnectionWorkforce(ulong source, ulong destination)
    {
        var workers = _tacticalHost?.Snapshot.Workers;
        if (workers is null || !workers.HasAuthoritativeTacticalLocations)
            return "Local workforce: unavailable";
        var own = workers.Workers.Where(w => w.Owner == _humanCivilization);
        var from = own.Count(w =>
            w.TacticalTileId.HasValue &&
            w.TacticalTileId.Value.FineStrategicCellId.Value == source);
        var to = own.Count(w =>
            w.TacticalTileId.HasValue &&
            w.TacticalTileId.Value.FineStrategicCellId.Value == destination);
        return $"Your workers at from node: {from}\n" +
            $"Your workers at to node: {to} (presence only)";
    }

    // R181: exact visual acceptance from the REAL cartographic site
    // selected in the rendered G18 world. QA never commits any turn.
    internal async Task ExerciseR181CityWindowVisualSmoke(ulong site)
    {
        if (_tacticalHost is null || _tacticalPreview is null ||
            !_tacticalPreview.LocalSiteMarkerTileIds.Contains(site))
            throw new InvalidOperationException(
                "R181 visual QA requires a currently focused actual inhabited site.");
        var beforeHash = _tacticalHost.StateHash;
        var viewport = GetViewport();
        var folder = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_R181_CITY_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(folder))
            throw new InvalidOperationException("R181 screenshot directory required.");
        System.IO.Directory.CreateDirectory(folder);
        async Task Frames()
        {
            for (int i = 0; i < 6; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        async Task Capture(string stage)
        {
            await Frames();
            if (_cityWindowLayer?.IsVisibleInTree() != true ||
                (_cityWindowTitle?.IsVisibleInTree() != true &&
                 _cityLinkDetail?.IsVisibleInTree() != true) ||
                _cityWindowLayer.GetGlobalRect().Intersects(
                    new Rect2(Vector2.Zero, viewport.GetVisibleRect().Size)) == false)
                throw new InvalidOperationException(
                    "R181 window exists in memory but is not actually displayed.");
            var image = viewport.GetTexture().GetImage();
            if (image is null || image.IsEmpty())
                throw new InvalidOperationException(
                    "No real rendered framebuffer for city QA.");
            var target = System.IO.Path.Combine(folder, stage + ".png");
            if (image.SavePng(target) != Error.Ok ||
                new System.IO.FileInfo(target).Length < 1500)
                throw new InvalidOperationException(
                    "R181 real city panel screenshot was not saved.");
            GD.Print($"GA_NEXT_R181_CITY_FRAME_PASS stage={stage} " +
                $"width={image.GetWidth()} height={image.GetHeight()}");
        }

        OnCartographicLocalSiteSelected(site);
        OpenSelectedCityWindow();
        var tabs = FindChild("GANextCityTabs", true, false) as TabContainer;
        var tile = new PhysicalTacticalTileId(
            _tacticalHost.Incidence.Refinement.FineParameters,
            new StrategicCellId(site));
        var rows = FoundationLocalConnectionsProjector.Read(
            _tacticalHost.Snapshot, _tacticalHost.Incidence, tile);
        if (tabs is null || tabs.GetTabCount() != 2 ||
            tabs.GetTabTitle(0) != "Overview" ||
            tabs.GetTabTitle(1) != "Connections" ||
            _cityLinksList?.GetChildCount() != rows.Count ||
            _cityWindowTitle?.Text != $"CITY | NODE {site}" ||
            _tacticalHost.StateHash != beforeHash)
            throw new InvalidOperationException(
                "R181 window tabs and incident-edges data do not match the physical world.");
        tabs.CurrentTab = 0;
        await Capture("01_city_overview");
        tabs.CurrentTab = 1;
        await Frames();
        if (_cityLinksList?.IsVisibleInTree() != true ||
            _cityLinksList.GetChildCount() != rows.Count ||
            rows.Count == 0)
            throw new InvalidOperationException(
                "R181 local links are hidden or not strictly incident edges.");
        await Capture("02_city_direct_links");
        ShowCityConnectionDetail(rows[0]);
        await Frames();
        if (_cityLinkDetail?.IsVisibleInTree() != true ||
            _cityLinkDetailText?.Text.Contains(
                $"To node: {rows[0].Neighbor.FineStrategicCellId.Value}",
                StringComparison.Ordinal) != true)
            throw new InvalidOperationException(
                "R181 connection detail did not display the real neighboring node.");
        if (_cityWindowPanel?.IsVisibleInTree() != false ||
            _cityLinkDetailText?.Text.Contains($"From node: {site}",
                StringComparison.Ordinal) != true)
            throw new InvalidOperationException("R183 connection endpoints or window exclusivity invalid.");
        var workforce = _tacticalHost.Snapshot.Workers.Workers
            .Where(w => w.Owner == _humanCivilization).ToArray();
        var destination = rows[0].Neighbor.FineStrategicCellId.Value;
        var fromCount = workforce.Count(w => w.TacticalTileId.HasValue &&
            w.TacticalTileId.Value.FineStrategicCellId.Value == site);
        var toCount = workforce.Count(w => w.TacticalTileId.HasValue &&
            w.TacticalTileId.Value.FineStrategicCellId.Value == destination);
        if (_cityLinkDetailText?.Text.Contains(
                $"Your workers at from node: {fromCount}\n", StringComparison.Ordinal) != true ||
            _cityLinkDetailText.Text.Contains(
                $"Your workers at to node: {toCount} (presence only)", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("R186 connection workforce differs from signed worker locations.");
        if (_constructionSection?.Visible != false ||
            _constructionProgress?.Value != 0)
            throw new InvalidOperationException("R202 no active construction must not show stale progress.");
        GD.Print("GA_NEXT_R202_EMPTY_CONSTRUCTION_PASS hidden=true");
        GD.Print("GA_NEXT_R186_CONNECTION_WORKFORCE_PASS physical_presence=true read_only=true");
        var realEdge = _tacticalHost.Incidence.FineTopology.Edges.Single(e =>
            e.IncidentCellIds.Any(c => c.Value == site) &&
            e.IncidentCellIds.Any(c => c.Value == destination));
        var realId = new PhysicalTacticalEdgeId(
            _tacticalHost.Incidence.Refinement.FineParameters, realEdge.Id);
        var pending = _tacticalHost.GetHumanMonetaryInvestments(_humanCivilization)
            .SingleOrDefault(p => p.Edge == realId);
        var expectedPending = pending is null ? "Your pending investment: None" :
            $"Your pending investment: {FormatMoneyMicroForDisplay(pending.MoneyMicro)} (not yet spent)";
        if (_cityLinkDetailText?.Text.Contains(
                $"Physical connection ID: {realEdge.Id.Value}\n", StringComparison.Ordinal) != true ||
            !_cityLinkDetailText.Text.EndsWith(expectedPending, StringComparison.Ordinal) ||
            ReadConnectionInvestment(site, destination) != ReadConnectionInvestment(destination, site))
            throw new InvalidOperationException("R187 connection plan or undirected identity mismatch.");
        GD.Print("GA_NEXT_R187_CONNECTION_PLAN_PASS authoritative=true undirected=true read_only=true");
        if (_connectionInvest is null || _connectionCancel is null ||
            !_connectionInvest.IsVisibleInTree() || !_connectionCancel.IsVisibleInTree())
            throw new InvalidOperationException("R188 connection investment controls missing.");
        if (_connectionAmount is null || _connectionProjection is null)
            throw new InvalidOperationException("R189 amount and projection controls missing.");
        _connectionAmount.Text = "0.123456";
        RefreshConnectionInvestmentControls();
        if (!TryReadConnectionAmount(out var customAmount) || customAmount != 123456UL)
            throw new InvalidOperationException("R189 exact decimal conversion failed.");
        var physicalEdge = _tacticalHost.Snapshot.Transport.Edges.Single(e => e.Edge == realId);
        if (physicalEdge.NaturalConstructionCostMoneyMicro is > 0UL &&
            physicalEdge.CargoMicroAtReferenceLoad != ulong.MaxValue)
        {
            var current = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(physicalEdge);
            var projected = FoundationPhysicalAuthoritativeTransportProjector.ProjectUnitFreightCost(physicalEdge, customAmount);
            if (projected > current ||
                FoundationPhysicalAuthoritativeTransportProjector.ProjectUnitFreightCost(physicalEdge, 0UL) != current)
                throw new InvalidOperationException("R189 projection violates canonical freight.");
        }
        _connectionAmount.Text = "0.0000001";
        RefreshConnectionInvestmentControls();
        if (TryReadConnectionAmount(out _) || !_connectionInvest.Disabled)
            throw new InvalidOperationException("R189 fractional micro accepted.");
        _connectionAmount.Text = "0.123456";
        RefreshConnectionInvestmentControls();
        GD.Print("GA_NEXT_R189_CONFIGURABLE_INVESTMENT_PASS exact_amount=true canonical_projection=true");
        var initialPlans = _tacticalHost.GetHumanMonetaryInvestments(_humanCivilization).ToArray();
        // Real UI signal on the current initial treasury; no fabricated funding.
        if (_connectionInvest.Disabled)
        {
            _connectionInvest.EmitSignal(Button.SignalName.Pressed);
            if (!_tacticalHost.GetHumanMonetaryInvestments(_humanCivilization).SequenceEqual(initialPlans))
                throw new InvalidOperationException("R188 disabled control altered the plan.");
        }
        else
        {
            _connectionInvest.EmitSignal(Button.SignalName.Pressed);
            if (!_tacticalHost.GetHumanMonetaryInvestments(_humanCivilization)
                    .Any(p => p.Edge == realId && p.MoneyMicro == customAmount) ||
                _connectionCancel.Disabled)
                throw new InvalidOperationException("R188 queue did not reach authoritative host.");
            _connectionCancel.EmitSignal(Button.SignalName.Pressed);
            if (!_tacticalHost.GetHumanMonetaryInvestments(_humanCivilization).SequenceEqual(initialPlans))
                throw new InvalidOperationException("R188 cancellation failed to restore plan.");
        }
        if (_tacticalHost.StateHash != beforeHash)
            throw new InvalidOperationException("R188 pending controls changed committed state.");
        GD.Print("GA_NEXT_R188_CONNECTION_CONTROLS_PASS real_signals=true committed_state_unchanged=true");
        await Capture("03_local_connection_detail");
        CloseConnectionDetail();
        if (_cityWindowPanel?.IsVisibleInTree() != true ||
            _cityLinkDetail?.Visible != false || tabs.CurrentTab != 1)
            throw new InvalidOperationException("R183 Back must restore the city Links tab.");
        GD.Print("GA_NEXT_R183_CONNECTION_WINDOW_PASS language=en read_only=true back_to_city=true");
        if (_tacticalHost.StateHash != beforeHash)
            throw new InvalidOperationException(
                "City presentation must not mutate economic or physical state.");
        _cityWindowLayer!.Visible = false;
        GD.Print($"GA_NEXT_R181_CITY_VISUAL_PASS site={site} " +
            $"direct_edges={rows.Count} frames=3 tabs=2 " +
            "map_background=true real_incidence=true no_economy_mutation=true");
    }

    // Execute the exact existing cartographic selection handler; this is
    // UI-only QA, not a worker, infrastructure or settlement mutation.
    internal void ExerciseR180CityWindowSmoke(ulong site)
    {
        if (_tacticalHost is null || _tacticalPreview is null ||
            !_tacticalPreview.LocalSiteMarkerTileIds.Contains(site))
            throw new InvalidOperationException(
                "R180 city QA requires a real visible inhabited site.");
        var before = _tacticalHost.StateHash;
        OnCartographicLocalSiteSelected(site);
        OpenSelectedCityWindow();
        var tile = new PhysicalTacticalTileId(
            _tacticalHost.Incidence.Refinement.FineParameters,
            new StrategicCellId(site));
        var expected = FoundationLocalConnectionsProjector.Read(
            _tacticalHost.Snapshot, _tacticalHost.Incidence, tile);
        if (_cityWindowLayer?.Visible != true ||
            _cityLinksList?.GetChildCount() != expected.Count ||
            _cityWindowTitle?.Text != $"CITY | NODE {site}" ||
            _tacticalHost.StateHash != before)
            throw new InvalidOperationException(
                "R180 city tabs did not show exactly real adjacent links.");
        _cityWindowLayer.Visible = false;
        GD.Print($"GA_NEXT_R180_CITY_LINKS_PASS site={site} " +
            $"direct_edges={expected.Count} real_transport=true " +
            "no_fake_routes=true no_economy_mutation=true");
    }
}
