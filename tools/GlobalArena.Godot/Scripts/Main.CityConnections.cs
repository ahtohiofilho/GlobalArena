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
    private ulong? _connectionDestination;

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
            Text = "CIDADE | Selecione um nó",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        header.AddChild(_cityWindowTitle);
        var close = new Button { Text = "FECHAR" };
        close.Pressed += () => overlay.Visible = false;
        header.AddChild(close);

        var tabs = new TabContainer
        {
            Name = "GANextCityTabs",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddChild(tabs);
        var overview = new MarginContainer { Name = "Visão geral" };
        tabs.AddChild(overview);
        _cityOverview = new Label
        {
            Text = "Selecione um nó físico no mapa.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Top,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        overview.AddChild(_cityOverview);

        var links = new ScrollContainer { Name = "Ligações" };
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
            CustomMinimumSize = new Vector2(575f, 350f),
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
        _cityLinkDetailText = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        details.AddChild(_cityLinkDetailText);
        details.AddChild(new Label { Text = "Investment amount (money units; decimal point)" });
        _connectionAmount = new LineEdit { Text = "0.100000", MaxLength = 32 };
        _connectionAmount.TextChanged += _ => RefreshConnectionInvestmentControls();
        details.AddChild(_connectionAmount);
        _connectionProjection = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        details.AddChild(_connectionProjection);
        _connectionInvest = new Button
        {
            Text = "QUEUE INVESTMENT",
            Disabled = true
        };
        _connectionInvest.Pressed += () => ChangeConnectionInvestment(false);
        details.AddChild(_connectionInvest);
        _connectionCancel = new Button { Text = "CANCEL PENDING INVESTMENT", Disabled = true };
        _connectionCancel.Pressed += () => ChangeConnectionInvestment(true);
        details.AddChild(_connectionCancel);
        _connectionFeedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        details.AddChild(_connectionFeedback);
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
            _cityWindowTitle.Text = "CIDADE | Selecione um assentamento";
            _cityOverview.Text = "Abra o mapa cartográfico e selecione uma cidade.";
            _cityLinksList.AddChild(new Label
            {
                Text = "As ligações locais aparecem após a seleção da cidade."
            });
            return;
        }

        var node = new PhysicalTacticalTileId(
            host.Incidence.Refinement.FineParameters,
            new StrategicCellId(selected.Value));
        var rows = FoundationLocalConnectionsProjector.Read(
            host.Snapshot, host.Incidence, node);
        _cityConnectionSource = selected.Value;
        _cityWindowTitle.Text = $"CIDADE | NÓ {selected.Value}";
        _cityOverview.Text =
            $"Nó físico #{selected.Value} da malha tática.\n" +
            $"{rows.Count} ligações diretas. Sem trajetos comerciais agregados.";
        foreach (var link in rows)
        {
            var displayed = link;
            var button = new Button
            {
                Text = $"Nó {link.Neighbor.FineStrategicCellId.Value}   " +
                    $"| frete físico {link.UnitPhysicalFreightMicro} micro/carga",
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
            $"Unit physical freight: {link.UnitPhysicalFreightMicro} micro\n" +
            $"External tariff: {link.ExternalTariffMicro} micro\n" +
            $"Infrastructure: {link.Ownership}\n" +
            $"Owner: {(link.InfrastructureOwner.HasValue
                ? link.InfrastructureOwner.Value.Value.ToString()
                : "Unowned")}\n" +
            $"Cumulative investment: {link.InvestedMoneyMicro} micro\n" +
            ReadConnectionWorkforce(_cityConnectionSource.Value,
                link.Neighbor.FineStrategicCellId.Value) + "\n\n" +
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
        var projectedAmount = pendingPlan?.MoneyMicro ?? amount;
        bool withinRange = projectedAmount <= ulong.MaxValue - physical.CommittedInvestmentMoneyMicro.GetValueOrDefault();
        bool affordable = valid && available >= GlobalArenaMoneyScale.FromMoneyMicro(amount);
        if (_connectionAmount is not null) _connectionAmount.Editable = !ready && !pending;
        if (_connectionProjection is not null && financeable && (pending || valid) && withinRange)
        {
            var before = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(physical);
            var after = FoundationPhysicalAuthoritativeTransportProjector.ProjectUnitFreightCost(physical, projectedAmount);
            _connectionProjection.Text =
                $"Physical freight per cargo unit: {FormatMoneyMicroForDisplay(before)} -> {FormatMoneyMicroForDisplay(after)}\n" +
                $"Reduction: {FormatMoneyMicroForDisplay(before - after)} | Conditional on settlement; not a revenue forecast.";
        }
        _connectionInvest.Disabled = ready || pending || !financeable || !affordable || !withinRange;
        _connectionCancel.Disabled = ready || !pending;
        _connectionFeedback.Text = ready ? "Turn already submitted." :
            pending ? "Investment pending. Press E to commit the turn." :
            !financeable ? "This connection cannot be financed by your civilization." :
            !valid ? "Enter a positive amount with at most six decimal places." :
            !withinRange ? "Capital exceeds the canonical money range." :
            !affordable ? "Insufficient unreserved treasury cash." :
            "Queue investment; cash is debited only on turn settlement.";
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
                _r144QueuedCapital[id] = (freight, cargo, amount);
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
                $"Your pending investment: {plan.MoneyMicro} micro (not yet spent)");
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
            $"Your workers at to node: {to}\n" +
            "Physical presence only; construction eligibility not evaluated.";
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
            tabs.GetTabTitle(0) != "Visão geral" ||
            tabs.GetTabTitle(1) != "Ligações" ||
            _cityLinksList?.GetChildCount() != rows.Count ||
            _cityWindowTitle?.Text != $"CIDADE | NÓ {site}" ||
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
                $"Your workers at to node: {toCount}\n", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("R186 connection workforce differs from signed worker locations.");
        GD.Print("GA_NEXT_R186_CONNECTION_WORKFORCE_PASS physical_presence=true read_only=true");
        var realEdge = _tacticalHost.Incidence.FineTopology.Edges.Single(e =>
            e.IncidentCellIds.Any(c => c.Value == site) &&
            e.IncidentCellIds.Any(c => c.Value == destination));
        var realId = new PhysicalTacticalEdgeId(
            _tacticalHost.Incidence.Refinement.FineParameters, realEdge.Id);
        var pending = _tacticalHost.GetHumanMonetaryInvestments(_humanCivilization)
            .SingleOrDefault(p => p.Edge == realId);
        var expectedPending = pending is null ? "Your pending investment: None" :
            $"Your pending investment: {pending.MoneyMicro} micro (not yet spent)";
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
            _cityWindowTitle?.Text != $"CIDADE | NÓ {site}" ||
            _tacticalHost.StateHash != before)
            throw new InvalidOperationException(
                "R180 city tabs did not show exactly real adjacent links.");
        _cityWindowLayer.Visible = false;
        GD.Print($"GA_NEXT_R180_CITY_LINKS_PASS site={site} " +
            $"direct_edges={expected.Count} real_transport=true " +
            "no_fake_routes=true no_economy_mutation=true");
    }
}
