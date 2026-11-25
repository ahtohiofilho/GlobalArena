using Godot;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>Fixed planet footer: immutable signed surface statistics and
/// existing geographic handoff, never a second order-entry handler.</summary>
public partial class Main
{
    private Label? _worldPlanetFacts;
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
        _worldPlanetFacts = WorldLayerPlaceholder("");
        _worldPlanetFacts.Name = "GANextPlanetFacts";
        _worldPlanetFacts.CustomMinimumSize = new Vector2(112, 0);
        overview.AddChild(_worldPlanetFacts);
        // Geography is immutable for this workspace. Read once, not on every
        // frame, pan, or economic close. These are tile shares, not area shares.
        if (_r123FineGeography is { } fine && fine.FineElevationRaw.Count > 0)
        {
            int total = fine.FineElevationRaw.Count;
            int ice = Enumerable.Range(0, total).Count(i => fine.SurfaceAtIndex(i) == FoundationFineSurfaceKind.FirmSeaIce);
            int land = fine.LandTileCount;
            int water = total - land - ice;
            _worldPlanetFacts.Text = $"Surface tiles: {total}\nLand: {PlanetTileShare(land, total)}\nWater: {PlanetTileShare(water, total)}\nSea ice: {PlanetTileShare(ice, total)}";
            _worldPlanetFacts.TooltipText = "Share of signed fine tiles, not physical surface area or habitability.";
        }
        else _worldPlanetFacts.Text = "Surface data unavailable.";
        _worldPlanetAtlas = new Button { Name = "GANextPlanetAtlas", Text = "M | PLANET MAP" };
        _worldPlanetAtlas.Pressed += TogglePlanetWindow;
        planet.AddChild(_worldPlanetAtlas);
        civilization.Reparent(planet);
    }

    private void ExerciseWorldPlanet(FoundationTacticalHumanAiTurnHost host)
    {
        var fine = _r123FineGeography ?? throw new InvalidOperationException("Missing signed geography.");
        var facts = _worldPlanetFacts ?? throw new InvalidOperationException("Missing planet facts.");
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
        string expected = $"Surface tiles: {total}\nLand: {PlanetTileShare(land, total)}\nWater: {PlanetTileShare(water, total)}\nSea ice: {PlanetTileShare(ice, total)}";
        if (land + water + ice != total || facts.Text != expected ||
            _worldPlanetCard?.IsAncestorOf(atlas) != true || _worldLayerScroll?.IsAncestorOf(inset) != false)
            throw new InvalidOperationException("Planet footer does not match signed surface or fixed layout.");
        if (window.Visible) throw new InvalidOperationException("Atlas unexpectedly open at startup.");
        atlas.EmitSignal(Button.SignalName.Pressed);
        if (!window.Visible || camera.Dot(globe.CenterGeography) < 0.99999f)
            throw new InvalidOperationException("Planet card did not open the existing atlas at tactical focus.");
        atlas.EmitSignal(Button.SignalName.Pressed);
        if (window.Visible || camera.Dot(tactical.CenterGeography) < 0.99999f ||
            host.StateHash != hash || !host.GetHumanPersistentQueues(_humanCivilization).SequenceEqual(queues))
            throw new InvalidOperationException("Planet card navigation changed state or failed geographic handoff.");
        GD.Print("GA_NEXT_R2306_PLANET_PASS signed_tile_shares=true fixed_footer=true atlas_handoff=true read_only=true");
    }
}
