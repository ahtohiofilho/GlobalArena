using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// Real GUI event test for an experimental tile→three jobs→individual menu.
/// It is intentionally distinct from the existing concentrated-workforce
/// smoke: no legacy mouse semantics or hashes are rewritten.
/// </summary>
public sealed class FoundationGodotTileHoverHypothesisSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;

    public FoundationGodotTileHoverHypothesisSmoke(
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
        for (var i = 0; i < 5; i++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task MouseAt(Vector2 position, bool click = false)
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventMouseMotion
        {
            Position = position,
            GlobalPosition = position,
            ButtonMask = (MouseButtonMask)0,
            Relative = Vector2.Zero
        }, inLocalCoords: true);
        await Frames();
        if (!click) return;
        viewport.PushInput(new InputEventMouseButton
        {
            Position = position,
            GlobalPosition = position,
            ButtonIndex = MouseButton.Left,
            ButtonMask = MouseButtonMask.Left,
            Pressed = true
        }, inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventMouseButton
        {
            Position = position,
            GlobalPosition = position,
            ButtonIndex = MouseButton.Left,
            ButtonMask = (MouseButtonMask)0,
            Pressed = false
        }, inLocalCoords: true);
        await Frames();
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

    private async Task Capture(string name)
    {
        var dir = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_TILE_HOVER_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        await Frames();
        var image = _main.GetViewport().GetTexture().GetImage();
        Must(image is not null && !image.IsEmpty(),
            "A real Godot framebuffer was not available.");
        var file = Path.Combine(dir!, name + ".png");
        Must(image!.SavePng(file) == Error.Ok &&
            new FileInfo(file).Length > 1500,
            "Failed to save actual tile hover hypothesis screenshot.");
        GD.Print($"GA_NEXT_TILE_HOVER_FRAME_PASS stage={name} " +
            $"width={image.GetWidth()} height={image.GetHeight()}");
    }

    public async Task RunAsync()
    {
        try
        {
            await Frames();
            var humans = _host.Snapshot.Workers.Workers
                .Where(w => w.Owner.Value == 1UL).ToArray();
            var home = humans[0].TacticalTileId!.Value;
            var originalHash = _host.StateHash;
            var camera = _map.TacticalCameraState;
            Must(_map.IsTileHoverHypothesisEnabled &&
                 _map.VisibleWorkerMarkerCount == 6 &&
                 _map.VisibleActivityGroupCount == 3 &&
                 humans.Length == 6 &&
                 humans.All(w => w.TacticalTileId == home),
                "Experimental hierarchy must begin with six people in three actual groups.");
            Must(_map.TryGetRenderedFineTilePixel(home, out var center),
                "Capital tile has no projected hit-test center.");
            await MouseAt(_map.GlobalPosition + center);
            Must(_map.IsTileHoverMenuVisible &&
                 _map.TileHoverMenuFineId == home.FineStrategicCellId.Value &&
                 _map.TileHoverExpandedOccupation is null &&
                 _host.StateHash == originalHash &&
                 _map.TacticalCameraState == camera,
                "Map hover did not open a read-only city tile overview.");
            var card = _map.FindChild(
                "GANextExperimentalTileHover", true, false) as PanelContainer;
            var title = _map.FindChild(
                "CityHoverPopulation", true, false) as Label;
            Must(card is not null && title?.Text.Contains("6 TRABALHADORES") == true,
                "Tile hover overview does not show the real population.");
            foreach (var role in new[] {
                PhysicalWorkerOccupation.Agriculture,
                PhysicalWorkerOccupation.Forestry,
                PhysicalWorkerOccupation.Mining })
            {
                var row = _map.FindChild(
                    $"CityHoverRole_{role}", true, false) as Button;
                Must(row?.Text.EndsWith("2") == true,
                    $"Actual role {role} missing or count not two.");
            }
            await Capture("01_tile_three_roles");

            var agriculturalRect = _map.TileHoverActivityRowRect(
                PhysicalWorkerOccupation.Agriculture);
            Must(agriculturalRect.HasValue,
                "Agriculture row not visible for hierarchical hover.");
            await MouseAt(agriculturalRect.Value.GetCenter());
            Must(_map.TileHoverExpandedOccupation ==
                    PhysicalWorkerOccupation.Agriculture &&
                 _map.TileHoverListedWorkerIds.SequenceEqual(new ulong[] {1UL, 4UL}) &&
                 _host.StateHash == originalHash,
                "Hovering the agriculture row did not reveal exactly worker #1 and #4.");
            await Capture("02_agriculture_individuals");

            var forestryRect = _map.TileHoverActivityRowRect(
                PhysicalWorkerOccupation.Forestry);
            Must(forestryRect.HasValue, "Forestry row disappeared.");
            await MouseAt(forestryRect.Value.GetCenter());
            Must(_map.TileHoverExpandedOccupation ==
                    PhysicalWorkerOccupation.Forestry &&
                 _map.TileHoverListedWorkerIds.SequenceEqual(new ulong[] {2UL, 5UL}),
                "Moving to forestry must list the two forestry individuals.");
            agriculturalRect = _map.TileHoverActivityRowRect(
                PhysicalWorkerOccupation.Agriculture);
            Must(agriculturalRect.HasValue, "Agriculture row disappeared.");
            await MouseAt(agriculturalRect.Value.GetCenter());
            var workerRow = _map.TileHoverWorkerRowRect(4UL);
            Must(workerRow.HasValue, "Individual worker #4 button is absent.");
            await MouseAt(workerRow.Value.GetCenter(), click: true);
            Must(_main.SelectedTacticalWorkerId == 4UL &&
                 _map.IsTileHoverMenuVisible &&
                 _map.IsTileHoverMenuPinned &&
                 _host.StateHash == originalHash &&
                 _map.TacticalCameraState == camera,
                "Clicking an individual must select #4 without gameplay or camera mutation.");
            GD.Print("GA_NEXT_TILE_HOVER_HIERARCHY_INPUT_PASS " +
                "overview=three_real_roles individual_lists=true " +
                "selected_worker=4 pinned=true hash_unchanged=true");
            await Capture("03_individual_selected");

            // Only the existing host is allowed to execute role changes.
            // The menu is strictly a selection-and-inspection interface.
            await KeyPress(Key.N);
            Must(_host.PendingOrderCount == 1 &&
                 _host.StateHash == originalHash &&
                 _host.Snapshot.Workers.Workers.Single(w =>
                    w.Id.Value == 4UL).Occupation ==
                        PhysicalWorkerOccupation.Agriculture,
                "Selection must not bypass the existing canonical order queue.");
            await KeyPress(Key.X);
            Must(_host.PendingOrderCount == 0 &&
                 _host.StateHash == originalHash,
                "X must cancel this role without a population update.");
            await KeyPress(Key.N);
            await KeyPress(Key.E);
            Must(_host.StateHash.FormatVersion == 11U &&
                 _host.Snapshot.Workers.Workers.Count == 12 &&
                 _host.Snapshot.Workers.Workers.Single(w =>
                    w.Id.Value == 4UL).Occupation ==
                        PhysicalWorkerOccupation.Mining &&
                 _host.Snapshot.Workers.Workers.Count(w =>
                    w.Owner.Value == 1UL && w.TacticalTileId == home) == 6 &&
                 _map.ActivityGroupMemberCount(
                    home.FineStrategicCellId.Value, 1UL,
                    PhysicalWorkerOccupation.Agriculture) == 1 &&
                 _map.ActivityGroupMemberCount(
                    home.FineStrategicCellId.Value, 1UL,
                    PhysicalWorkerOccupation.Mining) == 3,
                "A collective role update must preserve six people and refresh group counts.");
            var agriculture = _map.FindChild(
                "CityHoverRole_Agriculture", true, false) as Button;
            var mining = _map.FindChild(
                "CityHoverRole_Mining", true, false) as Button;
            Must(agriculture?.Text.EndsWith("1") == true &&
                 mining?.Text.EndsWith("3") == true,
                "Pinned menu did not update 2/2/2 to 1/2/3 after E.");
            await Capture("04_after_reallocation");

            await KeyPress(Key.Escape);
            Must(!_map.IsTileHoverMenuVisible &&
                 _main.SelectedTacticalWorkerId == 4UL,
                "Esc must close just the transient menu, preserving selection.");
            GD.Print("GA_NEXT_TILE_HOVER_HYPOTHESIS_E2E_PASS " +
                "tile_hover=true role_hover=true worker_click=true " +
                "pin=true X_cancel=true E_commit=true dynamic_counts=true " +
                "escape=true people=12 replay=v11");
            _main.GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr("GA_NEXT_TILE_HOVER_HYPOTHESIS_FAIL " + exception);
            _main.GetTree().Quit(89);
        }
    }
}
