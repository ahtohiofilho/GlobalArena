using Godot;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>Fixed planet footer: immutable signed surface statistics and
/// existing geographic handoff, never a second order-entry handler.</summary>
public partial class Main
{
    private VBoxContainer? _worldPlanetMetrics;
    private readonly Dictionary<string, Label> _planetMetricValues = new();
    private Button? _worldPlanetAtlas;

    private static string PlanetTileShare(int count, int total) =>
        FormattableString.Invariant($"{100m * count / total:0.#}%");

    private void CreateWorldPlanetReadout(VBoxContainer planet, GlobePreview globe, Button civilization)
    {
        var overview = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        overview.AddThemeConstantOverride("separation", 6);
        planet.AddChild(overview);
        if (globe.ReadSignedPhysicalSurface() is { } surface)
        {
            _auxiliaryPlanet = new GaAuxiliaryPlanet(surface.Mesh, surface.Material)
            {
                CustomMinimumSize = new Vector2(112, 112),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            overview.AddChild(_auxiliaryPlanet);
            RefreshAuxiliaryPlanet();
        }
        else overview.AddChild(WorldLayerPlaceholder("Planet preview unavailable."));
        var metrics = new VBoxContainer { Name = "GANextPlanetMetrics",
            CustomMinimumSize = new Vector2(90, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _worldPlanetMetrics = metrics;
        metrics.AddThemeConstantOverride("separation", 2);
        overview.AddChild(metrics);
        foreach (var (key, icon, meaning) in new[]
        {
            ("tiles", "tiles", "Total signed fine surface tiles"),
            ("land", "terrain", "Land — share of fine tiles"),
            ("water", "water", "Open water — share of fine tiles"),
            ("ice", "ice", "Firm sea ice — share of fine tiles")
        })
        {
            var row = GaHudWidgets.MetricRow(icon, "—", meaning, out var value);
            metrics.AddChild(row);
            _planetMetricValues.Add(key, value);
        }
        // Geography is immutable for this workspace. Read once, not on every
        // frame, pan, or economic close. These are tile shares, not area shares.
        if (_r123FineGeography is { } fine && fine.FineElevationRaw.Count > 0)
        {
            int total = fine.FineElevationRaw.Count;
            int ice = Enumerable.Range(0, total).Count(i => fine.SurfaceAtIndex(i) == FoundationFineSurfaceKind.FirmSeaIce);
            int land = fine.LandTileCount;
            int water = total - land - ice;
            _planetMetricValues["tiles"].Text = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _planetMetricValues["land"].Text = PlanetTileShare(land, total);
            _planetMetricValues["water"].Text = PlanetTileShare(water, total);
            _planetMetricValues["ice"].Text = PlanetTileShare(ice, total);
        }
        else metrics.TooltipText = "Surface data unavailable.";
        foreach (var key in new[] { "land", "water", "ice" })
            if (_planetMetricValues[key].GetParent() is Control row)
                row.TooltipText += " (not physical area or habitability).";
        _worldPlanetAtlas = new Button { Name = "GANextPlanetAtlas", Text = "M | PLANET MAP" };
        _worldPlanetAtlas.Pressed += TogglePlanetWindow;
        planet.AddChild(_worldPlanetAtlas);
        civilization.Reparent(planet);
    }

    private void ExerciseWorldPlanet(FoundationTacticalHumanAiTurnHost host)
    {
        var fine = _r123FineGeography ?? throw new InvalidOperationException("Missing signed geography.");
        var metrics = _worldPlanetMetrics ?? throw new InvalidOperationException("Missing planet metrics.");
        var atlas = _worldPlanetAtlas ?? throw new InvalidOperationException("Missing planet navigation.");
        var inset = _auxiliaryPlanet ?? throw new InvalidOperationException("Missing auxiliary globe.");
        var tactical = _tacticalPreview ?? throw new InvalidOperationException("Missing tactical map.");
        var globe = _strategicGlobe ?? throw new InvalidOperationException("Missing planet map.");
        var window = _planetWindow ?? throw new InvalidOperationException("Missing atlas window.");
        var hash = host.StateHash;
        var queues = host.GetHumanPersistentQueues(_humanCivilization).ToArray();
        var camera = tactical.CenterGeography;
        int total = fine.FineElevationRaw.Count;
        int land = Enumerable.Range(0, total).Count(i => fine.SurfaceAtIndex(i) == FoundationFineSurfaceKind.Land);
        int water = Enumerable.Range(0, total).Count(i => fine.SurfaceAtIndex(i) == FoundationFineSurfaceKind.OpenWater);
        int ice = Enumerable.Range(0, total).Count(i => fine.SurfaceAtIndex(i) == FoundationFineSurfaceKind.FirmSeaIce);
        var expected = new Dictionary<string, (string Value, string Icon)>
        {
            ["tiles"] = (total.ToString(System.Globalization.CultureInfo.InvariantCulture), "tiles"),
            ["land"] = (PlanetTileShare(land, total), "terrain"),
            ["water"] = (PlanetTileShare(water, total), "water"),
            ["ice"] = (PlanetTileShare(ice, total), "ice")
        };
        if (land + water + ice != total || _planetMetricValues.Count != expected.Count ||
            !metrics.IsVisibleInTree() || _worldPlanetCard?.IsAncestorOf(metrics) != true ||
            _worldLayerScroll?.IsAncestorOf(metrics) != false ||
            _worldPlanetCard?.IsAncestorOf(atlas) != true || _worldLayerScroll?.IsAncestorOf(inset) != false)
            throw new InvalidOperationException("Planet footer does not match signed surface or fixed layout.");
        foreach (var (key, entry) in expected)
        {
            var value = _planetMetricValues[key];
            if (!value.IsVisibleInTree() || value.Text != entry.Value ||
                value.GetParent() is not HBoxContainer row ||
                row.GetChild(0) is not TextureRect icon || icon.Texture != GaHudWidgets.Icon(entry.Icon) ||
                string.IsNullOrEmpty(row.TooltipText))
                throw new InvalidOperationException($"Planet widget differs from signed facts: {key}");
        }
        if (window.Visible) throw new InvalidOperationException("Atlas unexpectedly open at startup.");
        atlas.EmitSignal(Button.SignalName.Pressed);
        if (!window.Visible || camera.Dot(globe.CenterGeography) < 0.99999f)
            throw new InvalidOperationException("Planet card did not open the existing atlas at tactical focus.");
        atlas.EmitSignal(Button.SignalName.Pressed);
        if (window.Visible || camera.Dot(tactical.CenterGeography) < 0.99999f ||
            host.StateHash != hash || !host.GetHumanPersistentQueues(_humanCivilization).SequenceEqual(queues))
            throw new InvalidOperationException("Planet card navigation changed state or failed geographic handoff.");
        GD.Print("GA_NEXT_R23061E_PLANET_WIDGET_PASS four_existing_metrics=true icon_values=true fixed_footer=true atlas_handoff=true");
        GD.Print("GA_NEXT_R2306_PLANET_PASS signed_tile_shares=true fixed_footer=true atlas_handoff=true read_only=true");
    }
}
