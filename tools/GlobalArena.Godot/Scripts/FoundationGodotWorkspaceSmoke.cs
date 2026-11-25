using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>
/// Real Godot startup/keyboard/turn smoke for the new tactical-first shell.
/// It deliberately does NOT assert physical worker movement on G(18,0),
/// which awaits a separately versioned authoritative worker-location model.
/// The PR #361 strategic-order smoke remains a distinct regression contract.
/// </summary>
internal sealed class FoundationGodotWorkspaceSmoke
{
    private readonly Main _main;
    private readonly FoundationHumanAiLocalPreview _game;

    public FoundationGodotWorkspaceSmoke(
        Main main, FoundationHumanAiLocalPreview game)
    {
        _main = main;
        _game = game;
    }

    public async Task RunAsync()
    {
        try
        {
            for (var frame = 0; frame < 6; frame++)
                await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
            var tactical = _main.FindChild(
                "GANextTacticalViewport", recursive: true, owned: false)
                as TacticalContextPreview;
            var globe = _main.FindChild(
                "GANextPlanetGlobe", recursive: true, owned: false)
                as GlobePreview;
            var planet = _main.GetNode<Control>("GANextPlanetWindow");
            var operations = _main.FindChild(
                "GANextOperationsWindow", recursive: true, owned: false) as Control;
            Must(operations is not null,
                "operational dock missing from responsive HBox layout");
            var panelsButton = _main.FindChild(
                "GANextPanelsButton", recursive: true, owned: false) as Button;
            Must(panelsButton is not null,
                "panels toggle button missing from tactical header");
            var cityButton = _main.FindChild(
                "GANextCityButton", recursive: true, owned: false) as Button;
            var cityWindow = _main.FindChild(
                "GANextCityWindowLayer", recursive: true, owned: false)
                as Control;
            var cityTabs = _main.FindChild(
                "GANextCityTabs", recursive: true, owned: false)
                as TabContainer;
            Must(cityButton is not null && cityWindow is not null &&
                !cityWindow.Visible && cityTabs is not null &&
                cityTabs.GetTabCount() == 4 &&
                cityTabs.GetTabTitle(0) == "Overview" &&
                cityTabs.GetTabTitle(1) == "Connections" &&
                cityTabs.GetTabTitle(2) == "Production" &&
                cityTabs.GetTabTitle(3) == "Workers",
                "city window must have overview, immediate links, production and workers " +
                "tabs and remain closed in the clean map startup");

            // R182 English-first shell: no fabricated population or GDP,
            // no exposed panel before C and no impact on physical world.
            var civilizationButton = _main.FindChild(
                "GANextCivilizationButton", true, false) as Button;
            var civilizationWindow = _main.FindChild(
                "GANextCivilizationWindowLayer", true, false) as CenterContainer;
            var civilizationTabs = _main.FindChild(
                "GANextCivilizationTabs", true, false) as TabContainer;
            Must(civilizationButton is not null &&
                civilizationButton.Text == "C | CIVILIZATION" &&
                civilizationWindow is not null && !civilizationWindow.Visible &&
                civilizationTabs is not null &&
                civilizationTabs.GetTabCount() == 2 &&
                civilizationTabs.GetTabTitle(0) == "Overview" &&
                civilizationTabs.GetTabTitle(1) == "Economy",
                "R182 must provide two English civilization tabs, closed on map startup");

            var canvas = _main.FindChild(
                "GANextWideTacticalCanvas", recursive: true, owned: false)
                as PanelContainer;
            var atlasPanel = _main.GetNode<Control>(
                "GANextPlanetWindow/GANextPlanetPanel");
            var operationsPanel = operations!.FindChild(
                "GANextOperationsPanel", recursive: true, owned: false) as Control;
            Must(operationsPanel is not null, "operations dock panel missing");
            var roster = _main.FindChild(
                "GANextOperationalRoster", recursive: true, owned: false)
                as GaOperationalRoster;
            Must(roster is not null, "canonical individual worker roster missing");
            var atlas = atlasPanel as GaFloatingWindow;
            Must(atlas is not null,
                "planetary atlas must be a reusable floating window");
            Must(atlas!.FindChild("GANextPlanetResizeGrip",
                    recursive: true, owned: false) is Button,
                "planetary atlas missing resize grip");
            Must(atlas.FindChild("GAWindowDragHandle",
                    recursive: true, owned: false) is Control,
                "planetary atlas missing move bar");
            Must(_main.Theme is not null, "GA design system Theme missing");
            Must(canvas is not null && canvas.Size.X > canvas.Size.Y * 1.18f,
                "tactical viewport is constrained to an obsolete 1:1 square");
            Must(tactical is not null && tactical.Size.X > tactical.Size.Y * 1.18f,
                "tactical surface did not inherit landscape viewport size");
            Must(canvas!.GetThemeStylebox("panel") is StyleBoxFlat,
                "map framing must use centralized visual foundation style");
            Must(((PanelContainer)atlasPanel).GetThemeStylebox("panel") is StyleBoxFlat,
                "planet window missing shared design-system frame");
            Must(((PanelContainer)operationsPanel!).GetThemeStylebox("panel") is StyleBoxFlat,
                "operations window missing shared design-system frame");
            GD.Print($"GA_VISUAL_FOUNDATION_01_GATE rectangle={canvas.Size.X:F0}x{canvas.Size.Y:F0} " +
                "theme=true windows=2 production_style=true");
            Must(tactical is not null && tactical.Visible,
                "the local player did not enter the tactical viewport");
            Must(tactical!.CanonicalPhysicalIncidenceMap is not null,
                "no canonical G(18,0) physical incidence was opened");
            // Enable tile edges as in the user's visual report and assert
            // against actual Godot vertex arrays, not a screenshot impression.
            Must(tactical.HasSurfaceAlignedTacticalGrid(),
                "tactical grid vertices drift from the physical tile surface");
            tactical.SetEdgesVisible(true);
            GD.Print("GA_NEXT_TACTICAL_GRID_SURFACE_PASS shared_vertices=true " +
                "height_offset=0 camera_parallax=false");
            Must(globe is not null && !globe.Visible,
                "planet was shown permanently instead of as a window");
            Must(!planet.Visible && !operations.Visible,
                "temporary windows should start closed");
            Must(_game.PendingOrderCount == 0 &&
                 _game.Snapshot.Revision == 0UL,
                "tactical startup changed gameplay state");
            var hash = _game.StateHash;
            var fullMapWidth = canvas.Size.X;
            var initialCamera = tactical.TacticalCameraState;
            var initialRenderSize = tactical.RenderViewportSize;
            Must(operations.GetParent() == canvas.GetParent(),
                "operational dock must be a direct layout sibling of the map");
            Must(roster!.DisplayedWorkerCount == _game.ControllableWorkers.Count &&
                roster.DisplayedWorkerCount == 3,
                "roster did not enumerate the exact authoritative human census");
            Must(!roster.HasAuthoritativeFinePositions,
                "v9 has no authoritative tactical worker location to draw");
            var selectedOtherId = _game.ControllableWorkers[1].Id.Value;
            var otherRow = roster.FindChild(
                $"GANextWorkerRow_{selectedOtherId}",
                recursive: true, owned: false) as Button;
            Must(otherRow is not null, "worker #2 identity row missing");
            otherRow!.EmitSignal(Button.SignalName.Pressed);
            Must(roster.SelectedWorkerId == selectedOtherId &&
                _game.StateHash == hash && _game.PendingOrderCount == 0,
                "individual worker selection unexpectedly changed gameplay");
            var chosenDetail = roster.FindChild(
                "GANextWorkerDetails", recursive: true, owned: false) as Label;
            Must(chosenDetail?.Text.Contains("Exact tactical G(18,0) tile: unavailable (v9)")
                == true, "roster fabricated a fine worker location");
            GD.Print($"GA_NEXT_WORKFORCE_ROSTER_PASS count={roster.DisplayedWorkerCount} " +
                $"selected={roster.SelectedWorkerId} authoritative_ids=true " +
                "physical_tile_unavailable=true queued_orders=0 state_mutation=false");
            await CaptureRenderedViewIfRequested("01_tactical_start");

            // Actual city window controls, not a private geometry setter.
            // This shell-only startup has no selected city; the same floating
            // panel hosts all four tabs in the real cartographic preview.
            var cityPanel = _main.FindChild("GANextCityWindow", true, false) as GaFloatingWindow;
            var cityHandle = _main.FindChild("GANextCityDragHandle", true, false) as Control;
            var cityGrip = _main.FindChild("GANextCityResizeGrip", true, false) as Control;
            Must(cityPanel is not null && cityHandle is not null && cityGrip is not null,
                "city floating window or its move/resize controls missing");
            cityButton!.EmitSignal(Button.SignalName.Pressed);
            await WaitFrame();
            Must(cityPanel!.IsVisibleInTree(), "city window did not open");
            cityTabs!.CurrentTab = 2;
            await WaitFrame();
            var cityStart = cityPanel.Position;
            var cityDrag = cityHandle!.GlobalPosition + cityHandle.Size * new Vector2(0.35f, 0.5f);
            await DragPointer(cityDrag, cityDrag + new Vector2(42f, 22f));
            Must((cityPanel.Position - cityStart).Length() > 8f, "real pointer drag did not move city");
            var citySize = cityPanel.Size;
            var cityResize = cityGrip!.GlobalPosition + cityGrip.Size * 0.5f;
            await DragPointer(cityResize, cityResize + new Vector2(40f, 32f));
            Must(cityPanel.Size.X > citySize.X + 8f && cityPanel.Size.Y > citySize.Y + 8f,
                "real pointer resize did not expand city");
            var movedCity = cityPanel.Position;
            var expandedCity = cityPanel.Size;
            Must(movedCity.X >= 0f && movedCity.Y >= 0f &&
                 movedCity.X + expandedCity.X <= cityWindow!.Size.X + 1f &&
                 movedCity.Y + expandedCity.Y <= cityWindow.Size.Y + 1f,
                "floating city escaped viewport");
            await Press(Key.Escape);
            Must(!cityWindow!.Visible, "Escape did not close city");
            cityButton.EmitSignal(Button.SignalName.Pressed);
            await WaitFrame();
            Must(cityPanel.Position.DistanceTo(movedCity) < 1f &&
                cityPanel.Size.DistanceTo(expandedCity) < 1f && cityTabs.CurrentTab == 2,
                "city geometry or selected tab reset on reopening");
            await Press(Key.Escape);
            Must(_game.StateHash == hash && tactical.TacticalCameraState == initialCamera &&
                _game.PendingOrderCount == 0, "city geometry changed gameplay or camera");
            GD.Print("GA_NEXT_R2295_CITY_FLOATING_PASS pointer=true drag=true resize=true bounded=true reopen_preserves_geometry=true");

            // Drive Godot's input pipeline, NOT a direct _UnhandledKeyInput
            // method call. Tab is normally consumed by focus_next and Esc by
            // ui_cancel; the old test bypassed that engine-level behavior.
            panelsButton!.GrabFocus();
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
            Must(panelsButton.HasFocus(),
                "unable to place native Godot keyboard focus on controls");
            await Press(Key.M);
            Must(planet.Visible && globe!.Visible && tactical.Visible,
                "M did not open a floating planetary navigation window");
            Must(_game.StateHash == hash, "M mutated the authoritative hash");
            await CaptureRenderedViewIfRequested("02_planet_map");
            // Input.ParseInputEvent exercises actual Godot title-bar hit
            // testing and the resize grip, not window-private state setters.
            var titleBar = atlas.FindChild(
                "GAWindowDragHandle", recursive: true, owned: false) as Control;
            var grip = atlas.FindChild(
                "GANextPlanetResizeGrip", recursive: true, owned: false) as Control;
            Must(titleBar is not null && grip is not null,
                "floating atlas lacks drag or resize controls");
            var beforeMove = atlas.Position;
            var dragFrom = titleBar!.GlobalPosition + new Vector2(
                titleBar.Size.X * 0.6f, 12f);
            GD.Print($"GA_FLOATING_E2E_GEOMETRY atlas_pos={atlas.Position} " +
                $"atlas_size={atlas.Size} viewport={planet.Size} " +
                $"title_rect={titleBar.GetGlobalRect()} drag_from={dragFrom}");
            await DragPointer(dragFrom, dragFrom + new Vector2(42f, 22f));
            Must((atlas.Position - beforeMove).Length() > 8f,
                "real pointer drag did not move the planetary window");
            var beforeResize = atlas.Size;
            var resizeFrom = grip!.GlobalPosition + grip.Size * 0.5f;
            await DragPointer(resizeFrom, resizeFrom + new Vector2(40f, 32f));
            Must(atlas.Size.X > beforeResize.X + 8f ||
                 atlas.Size.Y > beforeResize.Y + 8f,
                "real pointer resize did not change planetary window bounds");
            Must(atlas.Position.X >= 0f && atlas.Position.Y >= 0f &&
                 atlas.Position.X + atlas.Size.X <= planet.Size.X + 1f &&
                 atlas.Position.Y + atlas.Size.Y <= planet.Size.Y + 1f,
                "drag/resize allowed the planetary atlas outside the viewport");
            Must(_game.StateHash == hash && tactical.TacticalCameraState == initialCamera,
                "secondary window pointer interaction changed gameplay or map focus");
            GD.Print("GA_NEXT_FLOATING_WINDOW_PASS pointer=godot_viewport_pipeline " +
                "drag=true resize=true bounded=true map_camera_stable=true");

            await Press(Key.Escape);
            Must(!planet.Visible && !globe!.Visible && tactical.Visible,
                "ESC was consumed before closing the planetary window");
            await Press(Key.M);
            Must(planet.Visible, "M did not reopen planet after ESC");
            await Press(Key.M);
            Must(!planet.Visible && !globe!.Visible && tactical.Visible,
                "M did not restore the uninterrupted tactical viewport");

            panelsButton.GrabFocus();
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
            await Press(Key.Tab);
            Must(operations.Visible && !planet.Visible,
                "TAB was consumed by focus navigation before toggling panels");
            await WaitFrame();
            var dockedMapWidth = canvas.Size.X;
            Must(dockedMapWidth < fullMapWidth - 280f,
                "TAB did not reserve actual width in the main map layout");
            Must(canvas.Size.X >= 360f && tactical.Size.X >= 360f,
                "TAB squeezed the tactical canvas below its minimum width");
            Must(operations.GlobalPosition.X >= canvas.GlobalPosition.X +
                     canvas.Size.X - 2f,
                "operational dock overlays rather than follows the map");
            Must(tactical.TacticalCameraState == initialCamera,
                "dock reflow changed geographical camera focus, yaw or zoom");
            Must(tactical.RenderViewportSize.X < initialRenderSize.X,
                "tactical SubViewport did not shrink with the available map width");
            Must(_game.StateHash == hash, "TAB dock changed authoritative state");
            GD.Print($"GA_NEXT_LAYOUT_REFLOW_PASS map_full={fullMapWidth:F0} " +
                $"map_docked={dockedMapWidth:F0} " +
                "camera_target_zoom_yaw_preserved=true geometry_reflow=true");
            await CaptureRenderedViewIfRequested("03_operations_panel");

            // Regression #381: the roster, selected tile, worker orders and
            // feedback must share ONE bounded outer scroll viewport. Merely
            // scrolling the old inner HUD leaves lower controls unreachable.
            var bodyScroll = operations.FindChild(
                "GANextOperationsBodyScroll", recursive: true, owned: false)
                as ScrollContainer ?? throw new InvalidOperationException("Operations scroll missing.");
            var body = operations.FindChild(
                "GANextOperationsBody", recursive: true, owned: false) as Control
                ?? throw new InvalidOperationException("Operations content missing.");
            var feedback = operations.FindChild(
                "GANextOrderFeedback", recursive: true, owned: false) as Label
                ?? throw new InvalidOperationException("Order feedback missing.");
            var quickEnd = _main.FindChild(
                "GANextEndTurnButton", recursive: true, owned: false) as Button
                ?? throw new InvalidOperationException("Pinned E missing.");
            Must(bodyScroll is not null && body is not null &&
                 feedback is not null && quickEnd is not null,
                "Operations dock has no full-height scroll, bottom order feedback or pinned E.");
            Must(bodyScroll!.IsAncestorOf(roster!) &&
                 bodyScroll.IsAncestorOf(feedback!) &&
                 body!.GetParent() == bodyScroll &&
                 roster!.GetParent() == body,
                "Roster, tile details and orders must share the same outer scrolling body.");
            Must(bodyScroll.Size.Y > 110f &&
                 bodyScroll.GetGlobalRect().End.Y <=
                    operations.GetGlobalRect().End.Y + 2f &&
                 operations.GetGlobalRect().End.Y <=
                    _main.GetGlobalRect().End.Y + 2f &&
                 bodyScroll.GetVScrollBar().MaxValue >
                    bodyScroll.GetVScrollBar().Page + 30f,
                "Dock is not height-bounded or its content does not scroll.");
            var beforeScrollHash = _game.StateHash;
            bodyScroll.ScrollVertical = (int)Math.Ceiling(
                bodyScroll.GetVScrollBar().MaxValue);
            for (int frame = 0; frame < 5; frame++) await WaitFrame();
            var scrollRect = bodyScroll.GetGlobalRect();
            var feedbackRect = feedback!.GetGlobalRect();
            Must(feedbackRect.Position.Y >= scrollRect.Position.Y - 2f &&
                 feedbackRect.End.Y <= scrollRect.End.Y + 2f,
                "The last worker-order feedback is clipped even at the end of the scroll.");
            Must(quickEnd!.IsVisibleInTree() &&
                 quickEnd.GetGlobalRect().End.Y <
                    bodyScroll.GetGlobalRect().Position.Y + 2f &&
                 beforeScrollHash == _game.StateHash &&
                 tactical.TacticalCameraState == initialCamera,
                "Scrolling hid the pinned E action or modified gameplay/camera.");
            GD.Print("GA_NEXT_OPERATIONS_DOCK_SCROLL_PASS " +
                "one_bounded_body=true roster_tile_orders=true bottom_accessible=true " +
                "end_turn_pinned=true hash_unchanged=true");
            await CaptureRenderedViewIfRequested("03_operations_bottom");

            // Also test a real smaller native viewport, not only scrolling a
            // large, fixed-size dock in the default screenshot.
            if (DisplayServer.GetName() == "headless")
            {
                // Headless Godot has no native window-manager resize contract.
                // Its bounded-scroll check above still runs; the actual
                // compact-window gate runs with a real Xvfb display below.
                GD.Print("GA_NEXT_OPERATIONS_DOCK_COMPACT_SKIP_HEADLESS " +
                    "reason=native_window_unavailable full_scroll_tested=true");
            }
            else
            {
            var nativeWindow = _main.GetWindow();
            var originalWindowSize = nativeWindow.Size;
            var originalWindowMode = nativeWindow.Mode;
            var originalContentSize = nativeWindow.ContentScaleSize;
            var originalRootHeight = _main.Size.Y;
            // Headless display servers can expose a different native window
            // size from the Godot logical viewport (content scale). Always
            // shrink the available native height, without assuming 720 px.
            var compactHeight = Math.Min(Math.Max(originalWindowSize.Y - 80, 400), 560);
            Must(compactHeight < originalWindowSize.Y,
                "Workspace smoke needs a smaller native window size.");
            GD.Print($"GA_NEXT_DOCK_RESIZE_PRE native={originalWindowSize} " +
                $"logical_height={originalRootHeight:F0} target_native_height={compactHeight}");
            nativeWindow.Mode = Window.ModeEnum.Windowed;
            // Project uses canvas_items stretch and retains its 1280x720
            // logical base on native resize. For the compact *layout* gate,
            // also lower the content base height. The production default is
            // restored immediately after the test; no setting is persisted.
            nativeWindow.ContentScaleSize = new Vector2I(
                originalContentSize.X, compactHeight);
            nativeWindow.Size = new Vector2I(originalWindowSize.X, compactHeight);
            for (int frame = 0; frame < 6; frame++) await WaitFrame();
            Must(_main.Size.Y < originalRootHeight - 20f &&
                 operations.Visible && bodyScroll.Size.Y > 100f &&
                 operations.GetGlobalRect().End.Y <=
                    _main.GetGlobalRect().End.Y + 2f &&
                 bodyScroll.GetVScrollBar().MaxValue >
                    bodyScroll.GetVScrollBar().Page,
                "The operational panel overflowed a reduced-height window.");
            bodyScroll.ScrollVertical = (int)Math.Ceiling(
                bodyScroll.GetVScrollBar().MaxValue);
            for (int frame = 0; frame < 5; frame++) await WaitFrame();
            scrollRect = bodyScroll.GetGlobalRect();
            feedbackRect = feedback.GetGlobalRect();
            Must(feedbackRect.Position.Y >= scrollRect.Position.Y - 2f &&
                 feedbackRect.End.Y <= scrollRect.End.Y + 2f &&
                 _game.StateHash == hash,
                "The bottom of the order panel cannot be reached in a compact window.");
            GD.Print("GA_NEXT_OPERATIONS_DOCK_COMPACT_PASS " +
                $"height={compactHeight} bounded=true bottom_accessible=true");
            await CaptureRenderedViewIfRequested("03_operations_compact");
            nativeWindow.ContentScaleSize = originalContentSize;
            nativeWindow.Size = originalWindowSize;
            nativeWindow.Mode = originalWindowMode;
            for (int frame = 0; frame < 6; frame++) await WaitFrame();
            }
            bodyScroll.ScrollVertical = 0;
            await WaitFrame();
            await Press(Key.M);
            Must(operations.Visible && planet.Visible,
                "opening a secondary window must not hide the operational dock");
            await Press(Key.Escape);
            Must(!planet.Visible && operations.Visible,
                "ESC must close floating window before operational dock");
            await Press(Key.Escape);
            Must(!operations.Visible && tactical.Visible,
                "ESC was consumed before closing the operational dock");
            await WaitFrame();
            Must(Mathf.Abs(canvas.Size.X - fullMapWidth) < 3f &&
                 tactical.TacticalCameraState == initialCamera,
                "closing the dock did not restore map width/camera state");
            await Press(Key.Tab);
            Must(operations.Visible, "TAB did not reopen operational dock");
            await Press(Key.Tab);
            Must(!operations.Visible, "TAB did not close operational dock");
            await WaitFrame();
            Must(Mathf.Abs(canvas.Size.X - fullMapWidth) < 3f,
                "TAB did not restore all available canvas width");

            // G cannot submit an old strategic destination while in tactical
            // mode. No claim of authoritative physical-tile movement is made.
            await Press(Key.G);
            Must(_game.PendingOrderCount == 0 &&
                 _game.Snapshot.Revision == 0UL && _game.StateHash == hash,
                "tactical G illegally queued a coarse Goldberg worker order");

            await Press(Key.E);
            Must(_game.NextTurn.Value == 2UL &&
                 _game.Snapshot.Revision == 1UL &&
                 _game.LastAiEventCount == 1 &&
                 _game.Snapshot.Workers.Workers.Count == 6 &&
                 _game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL)
                     .Activity == PhysicalWorkerActivity.Producing,
                "E did not close the existing authoritative human/AI turn");
            Must(_game.LastTurnReceipt is { ReplayMatched: true },
                "tactical-first turn did not replay identically");
            Must(roster.SelectedWorkerId == selectedOtherId &&
                 roster.DisplayedWorkerCount == _game.ControllableWorkers.Count,
                "roster selection/identity changed after human+AI commit");
            var heading = roster.FindChild("GANextRosterScroll", true, false);
            Must(heading is not null && _game.Snapshot.Revision == 1UL,
                "roster refresh after v9 commit unavailable");
            await CaptureRenderedViewIfRequested("04_after_ai_turn");
            GD.Print("GA_NEXT_LAYOUT_DOCK_E2E_PASS " +
                "tab_reserves_width=true focus_preserved=true " +
                "atlas_independent=true esc_floating_first=true");
            GD.Print("GA_NEXT_SHORTCUT_ROUTING_PASS " +
                "input_pipeline=godot_parsed tab=focus_safe escape=both_windows " +
                "M=reentrant TAB=reentrant E=authoritative committed_once=true");
            GD.Print("GA_NEXT_TACTICAL_WORKSPACE_PASS " +
                "entry=tactical planet=M panels=TAB close=ESC " +
                "g_tactical_locked=true human_ai_turn=true " +
                "ai_events=1 replay=true hash=v9");
            _main.GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PrintErr("GA_NEXT_TACTICAL_WORKSPACE_FAIL " + error);
            _main.GetTree().Quit(83);
        }
    }

    // Linux Xvfb/GL graphical gate: images come from the actual Godot
    // framebuffer, not a synthetic mock. Headless test skips this explicitly.
    private async Task CaptureRenderedViewIfRequested(string stage)
    {
        var directory = System.Environment.GetEnvironmentVariable("GA_NEXT_WORKSPACE_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        for (var i = 0; i < 4; i++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
        var frame = _main.GetViewport().GetTexture().GetImage();
        Must(frame is not null && !frame.IsEmpty(),
            "no Godot framebuffer for tactical workspace");
        Must(frame!.GetWidth() >= 640 && frame.GetHeight() >= 480,
            "unexpected tactical frame size");
        var palette = new System.Collections.Generic.HashSet<string>();
        for (var y = 0; y < frame.GetHeight(); y += Math.Max(1, frame.GetHeight() / 16))
            for (var x = 0; x < frame.GetWidth(); x += Math.Max(1, frame.GetWidth() / 16))
            {
                var sample = frame.GetPixel(x, y);
                palette.Add($"{(int)(sample.R * 255)}:{(int)(sample.G * 255)}:" +
                    $"{(int)(sample.B * 255)}");
            }
        Must(palette.Count >= 5, "tactical frame is blank or nearly uniform");
        var filename = Path.Combine(directory, stage + ".png");
        Must(frame.SavePng(filename) == Error.Ok, "Godot PNG write failed");
        GD.Print($"GA_NEXT_RENDER_FRAME_PASS stage={stage} " +
            $"dimensions={frame.GetWidth()}x{frame.GetHeight()} palette={palette.Count}");
    }

    private async Task WaitFrame()
    {
        for (var i = 0; i < 3; i++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task DragPointer(Vector2 from, Vector2 to)
    {
        _main.GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = from, GlobalPosition = from,
            Relative = Vector2.Zero, ButtonMask = (MouseButtonMask)0
        }, inLocalCoords: true);
        await WaitFrame();
        _main.GetViewport().PushInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true,
            Position = from, GlobalPosition = from,
            ButtonMask = MouseButtonMask.Left
        }, inLocalCoords: true);
        await WaitFrame();
        _main.GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = to, GlobalPosition = to,
            Relative = to - from, ButtonMask = MouseButtonMask.Left
        }, inLocalCoords: true);
        await WaitFrame();
        _main.GetViewport().PushInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = false,
            Position = to, GlobalPosition = to,
            ButtonMask = (MouseButtonMask)0
        }, inLocalCoords: true);
        await WaitFrame();
    }

    private async Task Press(Key code)
    {
        // The real SceneTree receives these events exactly like user input,
        // allowing focus navigation/accept_event to affect the outcome.
        global::Godot.Input.ParseInputEvent(new InputEventKey
        {
            Keycode = code, Pressed = true, Echo = false
        });
        await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
        global::Godot.Input.ParseInputEvent(new InputEventKey
        {
            Keycode = code, Pressed = false, Echo = false
        });
        await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Must(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
