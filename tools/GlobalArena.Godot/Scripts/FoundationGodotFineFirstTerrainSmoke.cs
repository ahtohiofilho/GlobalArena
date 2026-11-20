using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R87 REAL Godot validation of the G18 terrain/transport/economy shared
/// physical authority. No decorative 01C, V1 interpolation or fabricated
/// trade overlay. E key closes the host's real settled sales twice.
/// </summary>
internal sealed class FoundationGodotFineFirstTerrainSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;
    private static readonly CivilizationId Human = new(1UL);

    internal FoundationGodotFineFirstTerrainSmoke(
        Main main, FoundationTacticalHumanAiTurnHost host,
        TacticalContextPreview map)
    {
        _main = main;
        _host = host;
        _map = map;
    }

    private static void Must(bool condition, string why)
    {
        if (!condition) throw new InvalidOperationException(why);
    }

    private async Task Frames()
    {
        for (int i = 0; i < 8; i++)
            await _main.ToSignal(_main.GetTree(),
                SceneTree.SignalName.ProcessFrame);
    }

    private async Task PressE()
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventKey
            { Keycode = Key.E, Pressed = true, Echo = false },
            inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventKey
            { Keycode = Key.E, Pressed = false, Echo = false },
            inLocalCoords: true);
        await Frames();
    }

    private async Task Capture(string name)
    {
        string? dir = Environment.GetEnvironmentVariable(
            "GA_NEXT_R87_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        await Frames();
        var shot = _main.GetViewport().GetTexture().GetImage();
        Must(shot is not null && !shot.IsEmpty(),
            "G18 Godot screenshot must contain actual rendered pixels.");
        var target = Path.Combine(dir, name + ".png");
        Must(shot!.SavePng(target) == Error.Ok &&
            new FileInfo(target).Length > 1500,
            "G18 Godot screenshot was not written.");
        GD.Print($"GA_NEXT_R87_G18_FRAME_PASS stage={name} " +
            $"width={shot.GetWidth()} height={shot.GetHeight()}");
    }

    internal async Task RunAsync()
    {
        try
        {
            await Frames();
            var fine = _map.AuthoritativeFineEconomy ??
                throw new InvalidOperationException(
                    "The visual tile mesh is not bound to the G18 physical world.");
            Must(_host.Snapshot.WorldBinding?.IsFineFirstWorld == true &&
                _main.ActiveTacticalHost == _host &&
                _map.CanonicalPhysicalIncidenceMap is { } displayedIncidence &&
                displayedIncidence.Refinement.FineParameters ==
                    _host.Incidence.Refinement.FineParameters &&
                displayedIncidence.Refinement.CoarseParameters ==
                    _host.Incidence.Refinement.CoarseParameters &&
                displayedIncidence.TileIncidences.Count ==
                    _host.Incidence.TileIncidences.Count,
                "Godot must use the authoritative G18 host and incidence.");

            var incidence = _host.Incidence;
            int count = incidence.TileIncidences.Count;
            Must(fine.FineElevationRaw.Count == count,
                "Rendered G18 field must cover every tactical cell.");
            int land = 0, water = 0;
            for (int i = 0; i < count; i++)
            {
                long rendered = _map.PresentedFineElevation((ulong)i + 1UL);
                long physical = fine.FineElevationRaw[i];
                Must(rendered == physical,
                    "The rendered elevation differs from physical freight input.");
                if (physical > fine.SeaLevelRaw) land++;
                else water++;
            }
            Must(land == fine.LandTileCount && land > 0 && water > 0,
                "Every terrain tile must use one G18 sea level.");

            int seaEdges = 0, landEdges = 0, coastEdges = 0;
            var records = _host.Snapshot.Transport.Edges.ToDictionary(
                e => e.Edge.FineStrategicEdgeId);
            foreach (var edge in incidence.FineTopology.Edges)
            {
                int left = checked((int)edge.IncidentCellIds[0].Value - 1);
                int right = checked((int)edge.IncidentCellIds[1].Value - 1);
                bool a = fine.FineElevationRaw[left] > fine.SeaLevelRaw;
                bool b = fine.FineElevationRaw[right] > fine.SeaLevelRaw;
                var transport = records[edge.Id];
                bool unbounded = transport.CargoMicroAtReferenceLoad ==
                    FoundationPhysicalEndogenousCongestionFeedback
                        .UnboundedNaturalCargoCapacity;
                Must(unbounded == (!a && !b),
                    "A real sea crossing in the map disagrees with " +
                    "the economic transport capacity.");
                if (a && b) landEdges++;
                else if (a || b) coastEdges++;
                else seaEdges++;
            }
            Must(seaEdges > 0 && landEdges > 0 && coastEdges > 0,
                "G18 must contain actual sea, land and port crossings.");
            Must(_map.FineVisualDetailTileCount == 0,
                "R81 decorative microdetail must never be active in G18.");
            GD.Print($"GA_NEXT_R87_G18_TERRAIN_FREIGHT_PASS " +
                $"tiles={count} land={land} sea={water} " +
                $"land_edges={landEdges} coast_edges={coastEdges} " +
                $"sea_edges={seaEdges} source=signed_g18 exact_match=true");

            var initialHash = _host.StateHash;
            var camera = _map.TacticalCameraState;
            await Capture("01_g18_terrain_before_trade");
            await PressE();
            var first = _host.GetLastClosedEconomicTradeView(Human);
            Must(_host.StateHash != initialHash &&
                _host.NextTurn.Value == 2UL &&
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 1UL &&
                _host.Snapshot.Treasury.Get(Human).CashScaled.Sign > 0 &&
                first.EconomicCycleOrdinal == 1UL &&
                first.Availability ==
                    FoundationEconomicTradeOverlayAvailability.AuthorizedSubset &&
                _map.HasSettledTradeData &&
                _map.PresentedEconomicCycleOrdinal == 1UL &&
                _map.TacticalCameraState == camera,
                "One E-key must close an actual G18 economic sale and " +
                "publish its private participant-visible freight overlay.");
            await Capture("02_g18_after_first_trade");
            var firstHash = _host.StateHash;
            await PressE();
            var second = _host.GetLastClosedEconomicTradeView(Human);
            Must(_host.StateHash != firstHash &&
                _host.NextTurn.Value == 3UL &&
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 2UL &&
                second.EconomicCycleOrdinal == 2UL &&
                second.PublishedHash == _host.StateHash &&
                _map.PresentedEconomicCycleOrdinal == 2UL &&
                _map.TacticalCameraState == camera,
                "Second E-key must settle and publish exactly one new " +
                "G18 economic cycle without changing the map.");
            await Capture("03_g18_after_second_trade");
            GD.Print("GA_NEXT_R87_G18_GODOT_ACCEPTANCE_PASS " +
                "cycles=2 replay=true real_economy=true " +
                "terrain_and_freight=same_signed_g18 manual_control=true");
            _main.GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError($"GA_NEXT_R87_G18_GODOT_ACCEPTANCE_FAIL {e}");
            _main.GetTree().Quit(1);
        }
    }
}
