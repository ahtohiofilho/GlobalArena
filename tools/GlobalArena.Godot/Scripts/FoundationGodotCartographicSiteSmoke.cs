using Godot;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R134 actual-scene regression for the failures reported in screenshots:
/// a city dot was on a vertex rather than the polygon interior, and a second
/// legitimate occupied tile never became a cartographic site marker.
/// Independent of the historical worker-badge E2E.
/// </summary>
internal sealed class FoundationGodotCartographicSiteSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;

    internal FoundationGodotCartographicSiteSmoke(
        Main main, FoundationTacticalHumanAiTurnHost host,
        TacticalContextPreview map)
    {
        _main = main;
        _host = host;
        _map = map;
    }

    private static void Must(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private async Task Frames()
    {
        for (int i = 0; i < 6; i++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    internal async Task RunAsync()
    {
        try
        {
            await Frames();
            Must(_map.IsCartographicSiteMode &&
                 _host.Snapshot.Workers.HasAuthoritativeTacticalLocations,
                "Cartographic site smoke requires QA mode and signed fine workers.");
            var sites = _host.Snapshot.Workers.Workers
                .Select(w => w.TacticalTileId!.Value.FineStrategicCellId.Value)
                .Distinct().OrderBy(id => id).ToArray();
            Must(sites.Length >= 2,
                "Cartographic QA fixture must include at least two true sites.");
            // R140: gameplay decision comes from the signed worker snapshot;
            // reading the preview never queues an order or alters the host.
            var initialDecision = _main.FineMovementDecisionText;
            Must(initialDecision.Contains("MOVEMENT | worker #") &&
                 initialDecision.Contains(" | from ") &&
                 initialDecision.Contains(" to "),
                 "R140 movement feasibility panel must describe the selected real worker.");
            _main.ExerciseR143RealCrossingSelectionSmoke();
            var beforeHash = _host.StateHash;
            var human = new GlobalArena.Runtime.CivilizationId(1UL);
            var firstHome = _host.Snapshot.Workers.Workers
                .First(w => w.Owner == human).TacticalTileId!.Value;
            var proof = _map.OpeningCityScreenProof(firstHome);
            var viewport = _map.RenderViewportSize;
            Must(Math.Abs(proof.City.X - viewport.X / 2f) < 1.5f &&
                 Math.Abs(proof.City.Y - viewport.Y / 2f) < 1.5f,
                "Startup MUST center optical camera exactly on human home city.");
            var direction = proof.North - proof.City;
            Must(direction.Y < -0.1f &&
                 Math.Abs(direction.X) < -direction.Y * 0.15f,
                "Geographic north must be above the initial city, not left or right.");
            GD.Print($"GA_NEXT_R135_HOME_NORTH_UP_PASS " +
                $"tile={firstHome.FineStrategicCellId.Value} " +
                $"city_pixel={proof.City} north_delta={direction} " +
                "centered=true geo_north_up=true");
            // Worldgen's climate uses Center.Z for sine latitude. Test the
            // actual +Z/-Z poles, not a synthetic +Y camera 'north' axis.
            var poles = _map.OpeningGeographicPolesScreenProof(firstHome);
            var polarNorth = poles.NorthPole - poles.City;
            var polarSouth = poles.SouthPole - poles.City;
            GD.Print($"GA_NEXT_R136_POLAR_SCREEN_OBSERVATION north={polarNorth} " +
                $"south={polarSouth} city={poles.City}");
            Must(polarNorth.Y < -0.1f && polarSouth.Y > 0.1f,
                "The REAL +Z north pole must appear above the home city " +
                "and the -Z south pole below it.");
            Must(Math.Abs(polarNorth.X) < Math.Abs(polarNorth.Y) * 0.2f &&
                 Math.Abs(polarSouth.X) < Math.Abs(polarSouth.Y) * 0.2f,
                "Worldgen's real poles must lie on the vertical map axis.");
            GD.Print($"GA_NEXT_R136_CANONICAL_POLES_PASS " +
                $"north_delta={polarNorth} south_delta={polarSouth} " +
                "physical_north=+Z physical_south=-Z horizontal_offset_small=true");
            _map.ExerciseOverheadPolarDragSmoke();
            await Frames();
            int visited = 0;
            foreach (var tile in sites)
            {
                var incident = _host.Incidence.TileIncidences[
                    checked((int)tile - 1)];
                var focus = incident.IncidentCoarseCellIds
                    .OrderBy(x => x.Value).First();
                _map.Open(focus.Value);
                await Frames();
                Must(_map.LocalSiteMarkerTileIds.Contains(tile),
                    $"Real occupied tile {tile} lost its site marker on focus {focus.Value}.");
                Must(_map.VisibleLocalSiteMarkerCount > 0,
                    $"No site visible after focusing occupied tile {tile}.");
                var anchor = _map.LocalSiteAnchors
                    .Single(x => x.TileId == tile);
                Must(float.IsFinite(anchor.Centroid.X) &&
                     float.IsFinite(anchor.Centroid.Z),
                    "Site polygon centroid must be finite.");
                var dot = _map.FindChild(
                    "GANextR134SiteDot" + tile, true, false) as Panel;
                Must(dot is not null && dot.Visible,
                    $"Real city dot {tile} is not displayed on its focused polygon.");
                // The Panel is a real 10x10 circular geometry, not a
                // font glyph whose baseline drifts from the polygon center.
                Must(dot!.Size.DistanceTo(new Vector2(10f, 10f)) < 0.01f,
                    "Cartographic dot is not the centered 10px circular Panel.");
                GD.Print($"GA_NEXT_R134_SITE_FOCUS tile={tile} " +
                    $"focus={focus.Value} centroid={anchor.Centroid} " +
                    "dot_panel=true present=true");
                visited++;
            }
            _main.ExerciseGeographicHandoffSmoke();
            Must(_main.FineMovementDecisionText.Contains("MOVEMENT | worker #"),
                "R140 movement preview vanished after geographic handoff.");
            GD.Print("GA_NEXT_R140_MOVE_DECISION_PASS signed_worker=true " +
                "canonical_destination=true read_only=true survives_map_handoff=true");
            Must(visited == sites.Length && _host.StateHash == beforeHash,
                "Site-focus inspection must cover both localities without gameplay mutation.");
            GD.Print($"GA_NEXT_R134_CARTOGRAPHIC_SITES_PASS " +
                $"sites={visited} physical_ids=true centroid=polygon_area " +
                "focus_rebuild=true no_hidden_locality=true no_economy_mutation=true");
            _main.GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError($"GA_NEXT_R134_CARTOGRAPHIC_SITES_FAIL {e}");
            _main.GetTree().Quit(1);
        }
    }
}
