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
        if (_cityLinkDetail is not null) _cityLinkDetail.Visible = false;
        if (_cityWindowPanel is not null) _cityWindowPanel.Visible = true;
    }

    private void ShowCityConnectionDetail(FoundationLocalConnection link)
    {
        if (_cityLinkDetail is null || _cityLinkDetailText is null ||
            _cityWindowPanel is null || !_cityConnectionSource.HasValue)
            return;
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
            $"Cumulative investment: {link.InvestedMoneyMicro} micro";
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
