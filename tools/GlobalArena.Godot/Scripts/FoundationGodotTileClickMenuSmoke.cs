using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// Real GUI event test for an experimental click-only tile→three jobs→individual menu.
/// It is intentionally distinct from the existing concentrated-workforce
/// smoke: no legacy mouse semantics or hashes are rewritten.
/// </summary>
public sealed class FoundationGodotTileClickMenuSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;

    public FoundationGodotTileClickMenuSmoke(
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
            "GA_NEXT_TILE_CLICK_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        await Frames();
        var image = _main.GetViewport().GetTexture().GetImage();
        Must(image is not null && !image.IsEmpty(),
            "A real Godot framebuffer was not available.");
        var file = Path.Combine(dir!, name + ".png");
        Must(image!.SavePng(file) == Error.Ok &&
            new FileInfo(file).Length > 1500,
            "Failed to save actual tile click hypothesis screenshot.");
        GD.Print($"GA_NEXT_TILE_CLICK_FRAME_PASS stage={name} " +
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
            Must(_map.IsTileClickHypothesisEnabled &&
                 _map.VisibleWorkerMarkerCount == 6 &&
                 _map.VisibleActivityGroupCount == 3 &&
                 humans.Length == 6 &&
                 humans.All(w => w.TacticalTileId == home),
                "Experimental hierarchy must begin with six people in three actual groups.");
            Must(_map.TryGetRenderedFineTilePixel(home, out var center),
                "Capital tile has no projected hit-test center.");
            // Mouse motion alone MUST NOT open anything, including
            // hovering over an existing worker disc.
            await MouseAt(_map.GlobalPosition + center);
            Must(!_map.IsTileClickMenuVisible,
                "Pointer motion unexpectedly opened a click-only city menu.");
            Must(!_map.IsWorkerInspectorVisible,
                "Pointer movement must not open the old worker hover tooltip.");
            await MouseAt(_map.GlobalPosition + center, click: true);
            Must(_map.IsTileClickMenuVisible &&
                 _map.TileClickMenuFineId == home.FineStrategicCellId.Value &&
                 _map.TileClickExpandedOccupation is null &&
                 _host.StateHash == originalHash &&
                 _map.TacticalCameraState == camera,
                "Map click did not open a read-only city tile overview.");
            var card = _map.FindChild(
                "GANextExperimentalTileClick", true, false) as PanelContainer;
            var title = _map.FindChild(
                "CityClickPopulation", true, false) as Label;
            Must(card is not null && title?.Text.Contains("6 TRABALHADORES") == true,
                "Tile click overview does not show the real population.");
            var ownerStripe = _map.FindChild(
                "CityClickCivilizationIdentityStripe", true, false) as ColorRect;
            Must(_map.AreWorkerCountDigitsScreenUpright() &&
                 _map.RenderedWorkerCivilizationColor(1UL) ==
                     GaCivilizationVisualIdentity.ColorFor(1UL) &&
                 ownerStripe?.Color == GaCivilizationVisualIdentity.ColorFor(1UL) &&
                 title?.Text.Contains("RED") == true,
                "Worker count orientation / owner marker and city identity must agree.");
            foreach (var role in new[] {
                PhysicalWorkerOccupation.Agriculture,
                PhysicalWorkerOccupation.Forestry,
                PhysicalWorkerOccupation.Mining })
            {
                var row = _map.FindChild(
                    $"CityClickRole_{role}", true, false) as Button;
                Must(row?.Text.EndsWith("2") == true,
                    $"Actual role {role} missing or count not two.");
            }
            // This reproduces the user's blocker: moving off the tile
            // into the menu or elsewhere must never dismiss the card.
            await MouseAt(_map.GlobalPosition + new Vector2(22f, 22f));
            Must(_map.IsTileClickMenuVisible &&
                 _map.TileClickMenuFineId == home.FineStrategicCellId.Value,
                "Menu closed when mouse left its origin tile.");
            await Capture("01_tile_three_roles");

            var agriculturalRect = _map.TileClickActivityRowRect(
                PhysicalWorkerOccupation.Agriculture);
            Must(agriculturalRect.HasValue,
                "Agriculture row not visible for click-only menu.");
            await MouseAt(agriculturalRect.GetValueOrDefault().GetCenter());
            Must(_map.TileClickExpandedOccupation is null &&
                 _map.IsTileClickMenuVisible,
                "Hover above a profession must not open or close a click-only menu.");
            await MouseAt(agriculturalRect.GetValueOrDefault().GetCenter(), click: true);
            Must(_map.TileClickExpandedOccupation ==
                    PhysicalWorkerOccupation.Agriculture &&
                 _map.TileClickListedWorkerIds.SequenceEqual(new ulong[] {1UL, 4UL}) &&
                 _host.StateHash == originalHash,
                "Clicking the agriculture row did not reveal exactly worker #1 and #4.");
            await Capture("02_agriculture_individuals");

            var forestryRect = _map.TileClickActivityRowRect(
                PhysicalWorkerOccupation.Forestry);
            Must(forestryRect.HasValue, "Forestry row disappeared.");
            await MouseAt(forestryRect.GetValueOrDefault().GetCenter());
            Must(_map.TileClickExpandedOccupation ==
                    PhysicalWorkerOccupation.Agriculture &&
                 _map.IsTileClickMenuVisible,
                "Mouse motion over another profession must not switch lists.");
            await MouseAt(forestryRect.GetValueOrDefault().GetCenter(), click: true);
            Must(_map.TileClickExpandedOccupation ==
                    PhysicalWorkerOccupation.Forestry &&
                 _map.TileClickListedWorkerIds.SequenceEqual(new ulong[] {2UL, 5UL}),
                "Moving to forestry must list the two forestry individuals.");
            agriculturalRect = _map.TileClickActivityRowRect(
                PhysicalWorkerOccupation.Agriculture);
            Must(agriculturalRect.HasValue, "Agriculture row disappeared.");
            await MouseAt(agriculturalRect.GetValueOrDefault().GetCenter(), click: true);
            var workerRow = _map.TileClickWorkerRowRect(4UL);
            Must(workerRow.HasValue, "Individual worker #4 button is absent.");
            await MouseAt(workerRow.GetValueOrDefault().GetCenter(), click: true);
            Must(_main.SelectedTacticalWorkerId == 4UL &&
                 _map.IsTileClickMenuVisible &&
                 _host.StateHash == originalHash &&
                 _map.TacticalCameraState == camera,
                "Clicking an individual must select #4 without closing menu or mutating gameplay.");
            GD.Print("GA_NEXT_TILE_CLICK_HIERARCHY_INPUT_PASS " +
                "overview=three_real_roles individual_lists=true " +
                "selected_worker=4 persistent=true hash_unchanged=true no_hover=true");
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
                "CityClickRole_Agriculture", true, false) as Button;
            var mining = _map.FindChild(
                "CityClickRole_Mining", true, false) as Button;
            Must(agriculture?.Text.EndsWith("1") == true &&
                 mining?.Text.EndsWith("3") == true,
                "Pinned menu did not update 2/2/2 to 1/2/3 after E.");
            await Capture("04_after_reallocation");

            await KeyPress(Key.Escape);
            Must(!_map.IsTileClickMenuVisible &&
                 _main.SelectedTacticalWorkerId == 4UL,
                "Esc must close just the transient menu, preserving selection.");
            GD.Print("GA_NEXT_TILE_CLICK_HYPOTHESIS_E2E_PASS " +
                "tile_click=true role_click=true worker_click=true " +
                "persistent_click=true no_hover=true X_cancel=true E_commit=true dynamic_counts=true " +
                "escape=true people=12 replay=v11");
            _main.GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr("GA_NEXT_TILE_CLICK_HYPOTHESIS_FAIL " + exception);
            _main.GetTree().Quit(89);
        }
    }
}
