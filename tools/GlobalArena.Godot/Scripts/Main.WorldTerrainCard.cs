using Godot;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>Read-only signed fine geography and configured potential.
/// Climate and elevation are dimensionless indices, not physical degrees/metres.
/// No inferred biome, movement rate, market or actual production.</summary>
public partial class Main
{
    private Label? _worldTerrainFacts, _worldTerrainPotential;
    private VBoxContainer? _terrainWidgets;
    private Label? _terrainSurface;
    private TextureRect? _terrainSurfaceIcon, _terrainFootState, _terrainShipState;
    private readonly Dictionary<string, Label> _terrainMetricValues = new();
    private readonly Dictionary<string, (HBoxContainer Row, Label Value, ProgressBar Bar)> _terrainYieldWidgets = new();

    private void CreateTerrainWidgets(VBoxContainer body)
    {
        var widgets = new VBoxContainer { Name = "GANextTerrainWidgets", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _terrainWidgets = widgets;
        body.AddChild(widgets);
        var surface = GaHudWidgets.MetricRow("terrain", "", "Signed surface type", out var surfaceValue);
        _terrainSurface = surfaceValue;
        _terrainSurfaceIcon = (TextureRect)surface.GetChild(0);
        widgets.AddChild(surface);
        var fields = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        widgets.AddChild(fields);
        foreach (var (key, meaning) in new[] { ("temperature", "Temperature index"), ("moisture", "Moisture index"),
            ("elevation", "Elevation index (sea level = 0)"), ("slope", "Logical slope index") })
        {
            var row = GaHudWidgets.MetricRow(key, "", meaning, out var value);
            fields.AddChild(row);
            _terrainMetricValues.Add(key, value);
        }
        var access = new HBoxContainer();
        widgets.AddChild(access);
        foreach (var (key, meaning) in new[] { ("foot", "Traversal on foot"), ("ship", "Navigation by ship") })
        {
            var row = new HBoxContainer { TooltipText = meaning, MouseFilter = MouseFilterEnum.Pass,
                SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(GaHudWidgets.MetricIcon(key));
            var state = GaHudWidgets.MetricIcon("cancel");
            row.AddChild(state);
            access.AddChild(row);
            if (key == "foot") _terrainFootState = state; else _terrainShipState = state;
        }
        widgets.AddChild(new HSeparator());
        var yields = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        widgets.AddChild(yields);
        foreach (var (key, icon) in new[] { ("Maize", "maize"), ("Sorghum", "sorghum"), ("Wheat", "wheat"),
            ("Rice", "rice"), ("Soybean", "soybean"), ("Barley", "barley"), ("Wood", "wood"), ("Stone", "stone") })
        {
            var cell = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 1);
            yields.AddChild(cell);
            var row = GaHudWidgets.MetricRow(icon, "", key, out var value);
            cell.AddChild(row);
            var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
            GaHudWidgets.CompactProgress(bar);
            cell.AddChild(bar);
            _terrainYieldWidgets.Add(key, (row, value, bar));
        }
    }

    private static void SetTerrainAccess(TextureRect state, bool allowed, string meaning)
    {
        state.Texture = GaHudWidgets.Icon(allowed ? "check" : "cancel");
        state.Modulate = allowed ? GaVisualFoundation.Accent : GaVisualFoundation.Muted;
        if (state.GetParent() is Control row) row.TooltipText = meaning + (allowed ? ": Yes" : ": No");
    }

    private void SetTerrainYields(IReadOnlyDictionary<string, ulong> values)
    {
        // One relative scale for the whole selected tile; never imply a
        // percentage of maximum global yield. Numeric values are absolute.
        double max = values.Count == 0 ? 1d : Math.Max(1d, values.Values.Max());
        foreach (var (key, widget) in _terrainYieldWidgets)
        {
            bool available = values.TryGetValue(key, out var amount);
            widget.Value.Text = available ? GaDisplayNumbers.PhysicalMicro(amount) : "—";
            widget.Bar.MaxValue = max;
            widget.Bar.Value = available ? amount : 0d;
            string tooltip = available
                ? $"{key}: {widget.Value.Text} units / full worker-turn. Bar relative to highest local potential; not actual production."
                : $"{key}: potential unavailable.";
            widget.Row.TooltipText = widget.Bar.TooltipText = tooltip;
        }
    }

    private void CreateWorldTerrainReadout(VBoxContainer body)
    {
        _worldTerrainFacts = WorldLayerPlaceholder("");
        _worldTerrainFacts.Name = "GANextTerrainFacts";
        body.AddChild(_worldTerrainFacts);
        _worldTerrainPotential = WorldLayerPlaceholder("");
        _worldTerrainPotential.Name = "GANextTerrainPotential";
        body.AddChild(_worldTerrainPotential);
        _worldTerrainFacts.Visible = _worldTerrainPotential.Visible = false;
        CreateTerrainWidgets(body);
        RefreshWorldTerrain();
    }

    private void RefreshWorldTerrain()
    {
        if (_worldTerrainFacts is null || _worldTerrainPotential is null) return;
        _worldTerrainPotential.Text = "";
        if (_terrainWidgets is null) return;
        _terrainWidgets.Visible = false;
        if (_hudTerrainSummary is not null)
        {
            _hudTerrainSummary.Visible = true;
            _hudTerrainSummary.TooltipText = "Select terrain on the map";
            _terrainSurface!.Text = "—";
            _terrainSurfaceIcon!.Texture = GaHudWidgets.Icon("terrain");
            if (_terrainFootState!.GetParent() is Control foot) foot.Visible = false;
            if (_terrainShipState!.GetParent() is Control ship) ship.Visible = false;
        }
        SetTerrainYields(new Dictionary<string, ulong>());
        var host = _tacticalHost;
        var fine = _r123FineGeography;
        var source = ReadWorldContextNode();
        if (host is null || fine is null || !source.HasValue ||
            source.Value == 0UL || source.Value > (ulong)fine.FineElevationRaw.Count)
        {
            _worldTerrainFacts.Text = "Select a tile or worker.";
            return;
        }
        int index = checked((int)source.Value - 1);
        var node = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, new StrategicCellId(source.Value));
        var surface = fine.SurfaceAtIndex(index);
        string surfaceName = surface switch
        {
            FoundationFineSurfaceKind.Land => "Land",
            FoundationFineSurfaceKind.OpenWater => "Open water",
            FoundationFineSurfaceKind.FirmSeaIce => "Firm sea ice",
            _ => "Unknown surface"
        };
        var adjacent = host.Incidence.FineTopology.Cells[index].AdjacentCellIds
            .Select(id => checked((int)fine.FineElevationRaw[checked((int)id.Value - 1)])).ToArray();
        int slope = AgriculturalLogicalSlope.Estimate(checked((int)fine.FineElevationRaw[index]), adjacent);
        _worldTerrainFacts.Text = $"{surfaceName} | Node {source.Value}\n" +
            $"Temperature index: {GaDisplayNumbers.PhysicalMicro(fine.FineTemperatureRaw[index])}\n" +
            $"Moisture index: {GaDisplayNumbers.PhysicalMicro(fine.FineMoistureRaw[index])}\n" +
            $"Elevation index: {GaDisplayNumbers.PhysicalMicro(fine.FineElevationRaw[index] - fine.SeaLevelRaw)} (sea level = 0)\n" +
            $"Slope index: {GaDisplayNumbers.PhysicalMicro(slope)}\n" +
            $"On foot: {(fine.CanTraverseOnFoot(index) ? "Yes" : "No")} | Ship: {(fine.CanNavigateByShip(index) ? "Yes" : "No")}";
        _terrainWidgets.Visible = true;
        if (_hudTerrainSummary is not null)
        {
            _hudTerrainSummary.Visible = true;
            _hudTerrainSummary.TooltipText = _worldTerrainFacts.Text;
            if (_terrainFootState!.GetParent() is Control foot) foot.Visible = true;
            if (_terrainShipState!.GetParent() is Control ship) ship.Visible = true;
        }
        _terrainSurface!.Text = surfaceName;
        _terrainSurfaceIcon!.Texture = GaHudWidgets.Icon(surface switch
        {
            FoundationFineSurfaceKind.OpenWater => "water",
            FoundationFineSurfaceKind.FirmSeaIce => "ice",
            _ => "terrain"
        });
        _terrainMetricValues["temperature"].Text = GaDisplayNumbers.PhysicalMicro(fine.FineTemperatureRaw[index]);
        _terrainMetricValues["moisture"].Text = GaDisplayNumbers.PhysicalMicro(fine.FineMoistureRaw[index]);
        _terrainMetricValues["elevation"].Text = GaDisplayNumbers.PhysicalMicro(fine.FineElevationRaw[index] - fine.SeaLevelRaw);
        _terrainMetricValues["slope"].Text = GaDisplayNumbers.PhysicalMicro(slope);
        SetTerrainAccess(_terrainFootState!, fine.CanTraverseOnFoot(index), "Traversal on foot");
        SetTerrainAccess(_terrainShipState!, fine.CanNavigateByShip(index), "Navigation by ship");
        if (host.AgriculturalProduction is not { } agriculture)
        {
            _worldTerrainPotential.Text = "Production potential unavailable.";
            return;
        }
        var values = CropCatalog.All.ToDictionary(c => c.Id.ToString(), c => agriculture.Resolve(node, c.Id).WholeTurnOutput);
        if (agriculture.BasicResources is { } basic)
        {
            values.Add("Wood", surface == FoundationFineSurfaceKind.Land ? basic.WoodWholeTurnOutput : 0UL);
            values.Add("Stone", surface == FoundationFineSurfaceKind.Land ? basic.StoneWholeTurnOutput : 0UL);
        }
        SetTerrainYields(values);
        _worldTerrainPotential.Text = "Potential output / full worker-turn (not actual production)\n" +
            string.Join("\n", values.Select(kv => $"{kv.Key}: {GaDisplayNumbers.PhysicalMicro(kv.Value)}"));
    }

    private void ExerciseWorldTerrain(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId worker, PhysicalTacticalTileId land)
    {
        var hash = host.StateHash;
        SetWidgetSectionExpanded("Terrain", true);
        var queues = host.GetHumanPersistentQueues(_humanCivilization).ToArray();
        var fine = _r123FineGeography ?? throw new InvalidOperationException("Missing fine geography.");
        var agriculture = host.AgriculturalProduction ?? throw new InvalidOperationException("Missing crop policy.");
        var facts = _worldTerrainFacts ?? throw new InvalidOperationException("Missing terrain facts.");
        var potential = _worldTerrainPotential ?? throw new InvalidOperationException("Missing terrain potential.");
        int index = checked((int)land.FineStrategicCellId.Value - 1);
        if (!facts.Text.StartsWith($"Land | Node {land.FineStrategicCellId.Value}", StringComparison.Ordinal) ||
            !facts.Text.Contains($"Temperature index: {GaDisplayNumbers.PhysicalMicro(fine.FineTemperatureRaw[index])}") ||
            !facts.Text.Contains($"Moisture index: {GaDisplayNumbers.PhysicalMicro(fine.FineMoistureRaw[index])}") ||
            !facts.Text.Contains($"Elevation index: {GaDisplayNumbers.PhysicalMicro(fine.FineElevationRaw[index] - fine.SeaLevelRaw)}") ||
            !facts.Text.Contains("On foot: Yes | Ship: No"))
            throw new InvalidOperationException("Land terrain does not match signed fields.");
        foreach (var crop in CropCatalog.All)
            if (!potential.Text.Split('\n').Contains($"{crop.Id}: {GaDisplayNumbers.PhysicalMicro(agriculture.Resolve(land, crop.Id).WholeTurnOutput)}"))
                throw new InvalidOperationException("Crop potential differs from configured policy.");
        if (_terrainWidgets?.IsVisibleInTree() != true || facts.IsVisibleInTree() || potential.IsVisibleInTree() ||
            _terrainSurface?.Text != "Land" || _terrainFootState?.Texture != GaHudWidgets.Icon("check") ||
            _terrainShipState?.Texture != GaHudWidgets.Icon("cancel") ||
            _terrainMetricValues["temperature"].Text != GaDisplayNumbers.PhysicalMicro(fine.FineTemperatureRaw[index]) ||
            _terrainMetricValues["moisture"].Text != GaDisplayNumbers.PhysicalMicro(fine.FineMoistureRaw[index]) ||
            _terrainMetricValues["elevation"].Text != GaDisplayNumbers.PhysicalMicro(fine.FineElevationRaw[index] - fine.SeaLevelRaw))
            throw new InvalidOperationException("Terrain visible metric contract failed.");
        foreach (var crop in CropCatalog.All)
        {
            var widget = _terrainYieldWidgets[crop.Id.ToString()];
            ulong amount = agriculture.Resolve(land, crop.Id).WholeTurnOutput;
            if (widget.Value.Text != GaDisplayNumbers.PhysicalMicro(amount) ||
                widget.Bar.Value != (double)amount || !widget.Bar.IsVisibleInTree() ||
                widget.Bar.MaxValue != _terrainYieldWidgets["Maize"].Bar.MaxValue)
                throw new InvalidOperationException("Terrain crop widget diverged from configured yield.");
        }
        var waterIndex = Enumerable.Range(0, fine.FineElevationRaw.Count)
            .First(i => fine.SurfaceAtIndex(i) == FoundationFineSurfaceKind.OpenWater);
        var water = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters,
            new StrategicCellId((ulong)waterIndex + 1UL));
        // Dispatch the same physical-tile selection callback; no move order.
        OnTacticalPhysicalTileClicked(water);
        if (!facts.Text.StartsWith($"Open water | Node {water.FineStrategicCellId.Value}", StringComparison.Ordinal) ||
            !facts.Text.Contains("On foot: No | Ship: Yes") ||
            CropCatalog.All.Any(c => !potential.Text.Split('\n').Contains($"{c.Id}: 0")) ||
            !potential.Text.Split('\n').Contains("Wood: 0") || !potential.Text.Split('\n').Contains("Stone: 0"))
            throw new InvalidOperationException("Water selection retained land potential or traversal.");
        if (_terrainSurface?.Text != "Open water" || _terrainSurfaceIcon?.Texture != GaHudWidgets.Icon("water") ||
            _terrainFootState?.Texture != GaHudWidgets.Icon("cancel") || _terrainShipState?.Texture != GaHudWidgets.Icon("check") ||
            _terrainYieldWidgets.Values.Any(w => w.Value.Text != "0" || w.Bar.Value != 0d))
            throw new InvalidOperationException("Water terrain widget retained land output or access.");
        OnOperationalWorkerSelected(worker.Value);
        if (!facts.Text.StartsWith($"Land | Node {land.FineStrategicCellId.Value}", StringComparison.Ordinal) ||
            host.StateHash != hash || !host.GetHumanPersistentQueues(_humanCivilization).SequenceEqual(queues))
            throw new InvalidOperationException("Terrain selection changed plans or failed to restore worker context.");
        SetWidgetSectionExpanded("Terrain", false);
        GD.Print("GA_NEXT_R23061D_TERRAIN_WIDGET_PASS signed_values=true shared_local_scale=true land_water=true read_only=true");
        GD.Print("GA_NEXT_R2305_TERRAIN_PASS signed_fields=true crop_policy=true land_water=true read_only=true");
    }
}
