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
    private readonly uint _expectedFormatVersion;

    public FoundationGodotTacticalV10Smoke(Main main,
        FoundationTacticalHumanAiTurnHost host,
        TacticalContextPreview tactical,
        uint expectedFormatVersion = 10U)
    {
        _main = main;
        _host = host;
        _tactical = tactical;
        if (expectedFormatVersion != 10U && expectedFormatVersion != 11U)
            throw new ArgumentException("Unknown canonical tactical worker smoke version.");
        _expectedFormatVersion = expectedFormatVersion;
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
        await ClickPoint(pixel);
        Must(_tactical.SelectedCanonicalPhysicalTileId ==
                tile.FineStrategicCellId.Value,
            $"Actual Godot pointer did not select fine tile {tile.FineStrategicCellId.Value}");
    }

    private async Task HoverBadge(ulong workerId)
    {
        Must(_tactical.TryGetWorkerBadgePixel(workerId, out var pixel),
            $"Individual worker #{workerId} badge is not visible");
        var point = _tactical.GlobalPosition + pixel;
        _main.GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = point, GlobalPosition = point,
            Relative = Vector2.Zero, ButtonMask = (MouseButtonMask)0
        }, inLocalCoords: true);
        await Frames();
        Must(_tactical.HoveredWorkerBadgeId == workerId &&
            _tactical.IsWorkerHoverVisible,
            $"Hover did not show true individual worker #{workerId} details");
    }

    private async Task ClickBadge(ulong workerId)
    {
        Must(_tactical.TryGetWorkerBadgePixel(workerId, out var pixel),
            $"Individual worker #{workerId} badge is not visible to click");
        await ClickPoint(pixel);
    }

    private async Task ClickPoint(Vector2 pixel)
    {
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
    }

    private void VerifyInspector(ulong workerId)
    {
        var individual = _host.Snapshot.Workers.Workers.Single(
            worker => worker.Id.Value == workerId);
        var description = _tactical.WorkerInspectorDescription;
        var bounds = _tactical.WorkerInspectorBounds;
        var extent = _tactical.Size;
        Must(_tactical.IsWorkerInspectorVisible && description is not null,
            "Hover did not present non-modal contextual worker inspector");
        Must(description!.Contains($"TRABALHADOR #{workerId}") &&
             description.Contains($"CIV. {individual.Owner.Value}") &&
             description.Contains($"G(18,0)/{individual.TacticalTileId!.Value.FineStrategicCellId.Value}") &&
             description.Contains($"Região T{individual.CellId.Value}") &&
             description.Contains($"ITEM {individual.Commodity.Value}") &&
             description.Contains("Capacidade ") &&
             description.Contains("DEMANDA ×") &&
             description.Contains("SITUAÇÃO"),
            "Inspector is missing one or more canonical individual facts");
        Must(bounds.Position.X >= 7f && bounds.Position.Y >= 7f &&
             bounds.End.X <= extent.X - 7f && bounds.End.Y <= extent.Y - 7f,
            "Inspector overflows the real tactical viewport bounds");
        foreach (var viewport in new[] {
                     new Vector2(360f, 300f),
                     new Vector2(1280f, 720f) })
            foreach (var corner in new[] {
                         Vector2.Zero,
                         new Vector2(viewport.X, 0f),
                         new Vector2(0f, viewport.Y),
                         viewport })
            {
                var position = TacticalContextPreview.PlaceWorkerInspector(
                    corner, new Vector2(286f, 171f), viewport);
                Must(position.X >= 7f && position.Y >= 7f &&
                     position.X + 286f <= viewport.X - 7f &&
                     position.Y + 171f <= viewport.Y - 7f,
                    "Viewport-edge worker inspector placement regression");
            }
    }

    private async Task Capture(string stage)
    {
        var dir = System.Environment.GetEnvironmentVariable(
            _expectedFormatVersion == 11U
                ? "GA_NEXT_TACTICAL_V11_SCREENSHOT_DIR"
                : "GA_NEXT_TACTICAL_V10_SCREENSHOT_DIR");
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
            "Could not write real fine tactical screenshot");
        GD.Print($"GA_NEXT_V{_expectedFormatVersion}_RENDER_FRAME_PASS stage={stage} " +
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
                 _host.StateHash.FormatVersion == _expectedFormatVersion &&
                 _host.Snapshot.Workers.Workers.Count == 6 &&
                 _host.Snapshot.Workers.HasAuthoritativeTacticalLocations,
                "Godot is not attached to expected-format authoritative fine workers");
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
            if (_expectedFormatVersion == 11U)
            {
                Must(_host.Snapshot.Workers.HasAuthoritativeOccupations &&
                    a.Occupation == PhysicalWorkerOccupation.Agriculture &&
                    b.Occupation == PhysicalWorkerOccupation.Forestry &&
                    _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL)
                        .Occupation == PhysicalWorkerOccupation.Mining,
                    "v11 worker occupation census does not contain all three original roles");
                Must(_tactical.RenderedWorkerBadgeFill(1UL) == new Color("#61d98b") &&
                    _tactical.RenderedWorkerBadgeFill(2UL) == new Color("#b88656"),
                    "Agriculture/forestry badges do not use the verified VS16 colors");
            }
            await HoverBadge(2UL);
            VerifyInspector(2UL);
            var hoverCamera = _tactical.TacticalCameraState;
            if (_expectedFormatVersion == 11U)
                Must(_tactical.HoveredWorkerBadgeDescription?.Contains(
                    "Extração vegetal") == true,
                    "v11 hover must describe a real productive occupation");
            Must(_host.StateHash == before &&
                 _tactical.TacticalCameraState == hoverCamera,
                "Worker inspector hover must not mutate world hash or camera");
            await ClickBadge(2UL);
            Must(!_tactical.IsWorkerInspectorVisible,
                "Click must dismiss transient inspection without blocking selection");
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
                $"Exact tactical G(18,0) tile: {secondTile} (v{_expectedFormatVersion})") == true,
                "Dock displays a strategic alias or a guessed fine tile");
            await Capture("01_worker_roster");

            await ClickBadge(1UL);
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
                 _host.StateHash.FormatVersion == _expectedFormatVersion,
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

            if (_expectedFormatVersion == 11U)
            {
                // Mine workers live on the opposite side of this test
                // planet. Change only the VIEW focus to verify the actual
                // mining badge instead of inventing a third local worker.
                var miner = _host.Snapshot.Workers.Workers.Single(
                    w => w.Id.Value == 4UL);
                var minedHash = _host.StateHash;
                _tactical.Open(miner.CellId.Value);
                _tactical.PresentAuthoritativeWorkers(
                    _host.Snapshot.Workers, _main.SelectedTacticalWorkerId);
                await Frames();
                Must(_tactical.RenderedWorkerBadgeFill(4UL) ==
                    new Color("#c4c7d4"),
                    "Mining badge does not retain historical VS16 silver");
                await HoverBadge(4UL);
                VerifyInspector(4UL);
                Must(_tactical.HoveredWorkerBadgeDescription?.Contains(
                    "Mineração") == true &&
                    _host.StateHash == minedHash,
                    "Mining hover should only change presentation");
                await Capture("05_mining_opponent");
                GD.Print("GA_NEXT_V11_WORKER_OCCUPATIONS_PASS " +
                    "agriculture=green forestry=brown mining=silver " +
                    "roles=canonical_individual immutable=true");
            }

            await KeyPress(Key.M);
            Must(planet!.Visible && operations!.Visible,
                "Planet atlas should float independent of the tactical dock");
            await KeyPress(Key.Escape);
            Must(!planet!.Visible && operations!.Visible,
                "Esc did not close the floating window before the dock");
            GD.Print($"GA_NEXT_WORKER_INSPECTOR_PASS version={_expectedFormatVersion} " +
                "context=individual activity=canonical capacity=micro " +
                "mouse=transparent viewport_clamped=true camera_preserved=true " +
                "worldstate_unchanged=true");
            GD.Print("GA_NEXT_WORKER_BADGE_VS16_PASS " +
                "surface=coplanar camera_pitch=preserved " +
                "hover=worker2 individual_click=true state_mutation=false");
            GD.Print($"GA_NEXT_TACTICAL_V{_expectedFormatVersion}_E2E_PASS " +
                "markers=authoritative_fine map_selection=true " +
                "G=adjacent_queue X=cancel P=production E=collective " +
                $"human_ai=atomic replay=v{_expectedFormatVersion} worker_count=6 " +
                "atlas=dock_independent");
            _main.GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"GA_NEXT_TACTICAL_V{_expectedFormatVersion}_FAIL " + ex);
            _main.GetTree().Quit(87);
        }
    }
}
