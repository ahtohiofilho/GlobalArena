using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// Actual G(18,0) Godot scene and GUI-input smoke: real human worker glyphs,
/// real fine-tile click, G/P/X/E, atomic v10 human+AI and hash replay.
/// Runs separately from the retained v9 scenario and workstation smokes.
/// </summary>
public sealed class FoundationGodotTacticalV10Smoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _tactical;
    private static readonly CivilizationId Human = new(1UL);

    public FoundationGodotTacticalV10Smoke(Main main,
        FoundationTacticalHumanAiTurnHost host,
        TacticalContextPreview tactical)
    {
        _main = main;
        _host = host;
        _tactical = tactical;
    }

    private static void Must(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private async Task Frames()
    {
        for (var i = 0; i < 4; i++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task KeyPress(Key key)
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventKey
        {
            Keycode = key, Pressed = true, Echo = false
        }, inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventKey
        {
            Keycode = key, Pressed = false, Echo = false
        }, inLocalCoords: true);
        await Frames();
    }

    private async Task ClickTile(PhysicalTacticalTileId tile)
    {
        Must(_tactical.TryGetRenderedFineTilePixel(tile, out var pixel),
            $"Fine tile {tile.FineStrategicCellId.Value} is not visible to click");
        var point = _tactical.GlobalPosition + pixel;
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventMouseMotion
        {
            Position = point, GlobalPosition = point,
            Relative = Vector2.Zero, ButtonMask = (MouseButtonMask)0
        }, inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = true,
            ButtonMask = MouseButtonMask.Left
        }, inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = false,
            ButtonMask = (MouseButtonMask)0
        }, inLocalCoords: true);
        await Frames();
        Must(_tactical.SelectedCanonicalPhysicalTileId ==
                tile.FineStrategicCellId.Value,
            $"Actual Godot pointer did not select fine tile {tile.FineStrategicCellId.Value}");
    }

    private async Task Capture(string stage)
    {
        var dir = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_TACTICAL_V10_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        await Frames();
        var image = _main.GetViewport().GetTexture().GetImage();
        Must(image is not null && !image.IsEmpty(), "Godot framebuffer unavailable");
        Must(image!.GetWidth() >= 640 && image.GetHeight() >= 480,
            "Godot renderer returned undersized tactical image");
        var file = Path.Combine(dir!, stage + ".png");
        var result = image.SavePng(file);
        Must(result == Error.Ok && new FileInfo(file).Length > 1500,
            "Could not write real v10 tactical screenshot");
        GD.Print($"GA_NEXT_V10_RENDER_FRAME_PASS stage={stage} " +
            $"width={image.GetWidth()} height={image.GetHeight()}");
    }

    public async Task RunAsync()
    {
        try
        {
            await Frames();
            var roster = _main.FindChild(
                "GANextOperationalRoster", true, false) as GaOperationalRoster;
            var operations = _main.FindChild(
                "GANextOperationsWindow", true, false) as Control;
            var planet = _main.FindChild(
                "GANextPlanetWindow", true, false) as Control;
            Must(roster is not null && operations is not null && planet is not null,
                "v10 UI missing operational dock or atlas");
            Must(_main.ActiveTacticalHost == _host &&
                 _host.StateHash.FormatVersion == 10U &&
                 _host.Snapshot.Workers.Workers.Count == 6 &&
                 _host.Snapshot.Workers.HasAuthoritativeTacticalLocations,
                "Godot is not attached to authoritative v10 physical workers");
            Must(_tactical.CanonicalPhysicalIncidenceMap is not null &&
                 _tactical.VisibleWorkerMarkerCount >= 2,
                "Real tactical viewport has no individually identified v10 markers");
            Must(_tactical.VisibleWorkerMarkerIds.Distinct().Count() ==
                 _tactical.VisibleWorkerMarkerCount,
                "Duplicated marker identity");

            var a = _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL);
            var b = _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 2UL);
            var source = a.TacticalTileId!.Value;
            Must(_tactical.TryGetWorkerMarkerPosition(1UL, out var markerTile,
                     out var center) &&
                 markerTile == source.FineStrategicCellId.Value &&
                 center.Y == 0f,
                "Worker #1 marker not attached to its exact flattened canonical tile");
            Must(_tactical.TryGetWorkerMarkerPosition(2UL, out var secondTile,
                     out _) &&
                 secondTile == b.TacticalTileId!.Value.FineStrategicCellId.Value,
                "Worker #2 marker missing or aliased");

            var before = _host.StateHash;
            await ClickTile(b.TacticalTileId!.Value);
            Must(_main.SelectedTacticalWorkerId == 2UL &&
                 _host.StateHash == before,
                "Clicking worker #2 must select its actual identity without mutation");
            await KeyPress(Key.Tab);
            Must(operations!.Visible && roster!.HasAuthoritativeFinePositions &&
                roster.SelectedWorkerId == 2UL,
                "Operational dock is not displaying the true v10 selected worker");
            var details = roster!.FindChild(
                "GANextWorkerDetails", true, false) as Label;
            Must(details?.Text.Contains(
                $"Exact tactical G(18,0) tile: {secondTile} (v10)") == true,
                "Dock displays a strategic alias or a guessed fine tile");
            await Capture("01_worker_roster");

            await ClickTile(source);
            Must(_main.SelectedTacticalWorkerId == 1UL,
                "Clicking own worker #1 did not change active selection");

            // Pick an actually rendered adjacent node that doesn't contain
            // another human worker. Its fine edge is checked by the host.
            var occupants = _host.Snapshot.Workers.Workers
                .Where(w => w.Owner == Human)
                .Select(w => w.TacticalTileId!.Value.FineStrategicCellId)
                .ToHashSet();
            var adjacentIds = _host.Incidence.FineTopology.Cells[
                checked((int)source.FineStrategicCellId.Value - 1)]
                .AdjacentCellIds;
            var destination = adjacentIds.Select(id =>
                new PhysicalTacticalTileId(
                    _host.Incidence.Refinement.FineParameters, id))
                .First(tile => !occupants.Contains(tile.FineStrategicCellId) &&
                    _tactical.TryGetRenderedFineTilePixel(tile, out _));
            await ClickTile(destination);
            Must(_main.SelectedTacticalWorkerId == 1UL, "Destination click changed worker");
            await KeyPress(Key.G);
            Must(_host.PendingOrderCount == 1 &&
                 _host.StateHash == before &&
                 _host.Snapshot.Revision == 0UL &&
                 a.TacticalTileId == source,
                "G moved or mutated worker before collective commit");
            await Capture("02_order_queued");
            await KeyPress(Key.X);
            Must(_host.PendingOrderCount == 0 && _host.StateHash == before,
                "X did not cancel private tactical intent");
            await KeyPress(Key.G);
            Must(_host.PendingOrderCount == 1 && _host.StateHash == before,
                "G failed to requeue valid fine movement after cancel");

            await KeyPress(Key.E);
            Must(_host.PendingOrderCount == 0 &&
                 _host.NextTurn.Value == 2UL &&
                 _host.Snapshot.Revision == 2UL &&
                 _host.StateHash.FormatVersion == 10U,
                "E did not publish a single real v10 human + AI collective turn");
            Must(_host.Snapshot.Workers.Workers.Single(w =>
                    w.Id.Value == 1UL).TacticalTileId == destination &&
                 _host.Snapshot.Workers.Workers.Single(w =>
                    w.Id.Value == 4UL).Activity == PhysicalWorkerActivity.Producing &&
                 _host.Snapshot.Workers.Workers.Count == 6 &&
                 _host.StateHash != before,
                "v10 collective commit did not move human and resume AI worker");
            Must(_tactical.TryGetWorkerMarkerPosition(1UL, out var afterTile,
                    out var afterCenter) &&
                 afterTile == destination.FineStrategicCellId.Value &&
                 afterCenter.Y == center.Y,
                "Worker marker did not follow authoritative fine position after E");
            await Capture("03_after_collective_turn");

            // The very same worker can resume activity on its actual fine
            // location on the next all-ready turn, with no duplicate demand.
            await ClickTile(destination);
            await KeyPress(Key.P);
            Must(_host.PendingOrderCount == 1 &&
                 _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .Activity == PhysicalWorkerActivity.Traveling,
                "P must queue production, not execute it immediately");
            await KeyPress(Key.E);
            Must(_host.NextTurn.Value == 3UL &&
                 _host.Snapshot.Revision == 3UL &&
                 _host.Snapshot.Workers.Workers.Single(w =>
                    w.Id.Value == 1UL).Activity == PhysicalWorkerActivity.Producing &&
                 _host.Snapshot.Workers.Workers.Single(w =>
                    w.Id.Value == 1UL).TacticalTileId == destination,
                "Second v10 collective turn changed physical tile or did not resume work");
            await Capture("04_after_production");

            await KeyPress(Key.M);
            Must(planet!.Visible && operations!.Visible,
                "Planet atlas should float independent of the tactical dock");
            await KeyPress(Key.Escape);
            Must(!planet!.Visible && operations!.Visible,
                "Esc did not close the floating window before the dock");
            GD.Print("GA_NEXT_TACTICAL_V10_E2E_PASS " +
                "markers=authoritative_fine map_selection=true " +
                "G=adjacent_queue X=cancel P=production E=collective " +
                "human_ai=atomic replay=v10 worker_count=6 " +
                "atlas=dock_independent");
            _main.GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr("GA_NEXT_TACTICAL_V10_FAIL " + ex);
            _main.GetTree().Quit(87);
        }
    }
}
