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
            AutowrapMode = TextServer.AutowrapMode.WordSmart
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
            Visible = false
        };
        column.AddChild(_cityLinkDetail);
        var details = new VBoxContainer();
        _cityLinkDetail.AddChild(details);
        var detailsHeader = new HBoxContainer();
        details.AddChild(detailsHeader);
        detailsHeader.AddChild(new Label
        {
            Text = "LIGAÇÃO | Detalhamento local",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        var hideDetails = new Button { Text = "FECHAR DETALHES" };
        hideDetails.Pressed += () => _cityLinkDetail.Visible = false;
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
        _cityLinkDetail.Visible = false;
        _cityWindowLayer.Visible = true;

        var host = _tacticalHost;
        var selected = _tacticalPreview?.SelectedCanonicalPhysicalTileId;
        if (host is null || selected is null ||
            _tacticalPreview?.IsCartographicSiteMode != true)
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

    private void ShowCityConnectionDetail(FoundationLocalConnection link)
    {
        if (_cityLinkDetail is null || _cityLinkDetailText is null)
            return;
        _cityLinkDetail.Visible = true;
        _cityLinkDetailText.Text =
            $"Nó vizinho: {link.Neighbor.FineStrategicCellId.Value}\n" +
            $"Custo físico unitário: {link.UnitPhysicalFreightMicro} micro\n" +
            $"Tarifa externa: {link.ExternalTariffMicro} micro\n" +
            $"Infraestrutura: {link.Ownership}\n" +
            $"Titular: {(link.InfrastructureOwner.HasValue
                ? link.InfrastructureOwner.Value.Value.ToString()
                : "sem titular")}\n" +
            $"Investimento acumulado: {link.InvestedMoneyMicro} micro";
    }
}
