using Godot;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
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
        _cityProductionGoods = new GridContainer { Name = "GANextCityProductionGoods", Columns = 5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_cityProductionGoods);
        body.AddChild(new HSeparator());
        body.AddChild(new Label { Text = "SALES & PHYSICAL TRADE | LAST CLOSED CYCLE" });
        _cityProductionTrade = new GridContainer { Name = "GANextCityProductionTrade", Columns = 6,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_cityProductionTrade);
        body.AddChild(new Label { Text = "Goods in commodity units; prices and revenue in money units. Imports and exports cross tile boundaries. — = no observed local price.",
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
            AddCityProductionRow(_cityProductionGoods, new[] { name, FormatMoneyMicroForDisplay(row.ProducedMicro),
                FormatMoneyMicroForDisplay(row.DemandMicro), FormatMoneyMicroForDisplay(row.ReceivedMicro),
                row.LocalPriceMicro is { } price ? FormatMoneyMicroForDisplay(price) : "—" });
            AddCityProductionRow(_cityProductionTrade, new[] { name, FormatMoneyMicroForDisplay(row.SoldMicro),
                FormatMoneyMicroForDisplay(row.ExportedMicro), FormatMoneyMicroForDisplay(row.ImportedMicro),
                FormatExactMoneyForDisplay(row.GrossSalesScaled), FormatExactMoneyForDisplay(row.SellerNetScaled) });
        }
    }

    private void ExerciseCityProductionReadout(FoundationTacticalHumanAiTurnHost host, PhysicalTacticalTileId node, ulong cycle)
    {
        var before = host.StateHash;
        OnCartographicLocalSiteSelected(node.FineStrategicCellId.Value);
        OpenSelectedCityWindow();
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
                var goods = new[] { CityCommodityName(names, row.Commodity), FormatMoneyMicroForDisplay(row.ProducedMicro),
                    FormatMoneyMicroForDisplay(row.DemandMicro), FormatMoneyMicroForDisplay(row.ReceivedMicro),
                    row.LocalPriceMicro is { } price ? FormatMoneyMicroForDisplay(price) : "—" };
                var trade = new[] { goods[0], FormatMoneyMicroForDisplay(row.SoldMicro),
                    FormatMoneyMicroForDisplay(row.ExportedMicro), FormatMoneyMicroForDisplay(row.ImportedMicro),
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
            if (foreign is not null)
            {
                OnCartographicLocalSiteSelected(foreign.TacticalTileId!.Value.FineStrategicCellId.Value);
                OpenSelectedCityWindow();
                if (_cityProductionGoods.GetChildCount() != 0 || _cityProductionTrade.GetChildCount() != 0)
                    throw new InvalidOperationException("Foreign production leaked into city tab.");
                OnCartographicLocalSiteSelected(node.FineStrategicCellId.Value);
                OpenSelectedCityWindow();
            }
        }
        if (host.StateHash != before) throw new InvalidOperationException("City production changed canonical state.");
        _cityWindowLayer!.Visible = false;
    }
}
