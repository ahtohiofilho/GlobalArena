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
            var canvas = _main.FindChild(
                "GANextWideTacticalCanvas", recursive: true, owned: false)
                as PanelContainer;
            var atlasPanel = _main.GetNode<Control>(
                "GANextPlanetWindow/GANextPlanetPanel");
            var operationsPanel = operations!.FindChild(
                "GANextOperationsPanel", recursive: true, owned: false) as Control;
            Must(operationsPanel is not null, "operations dock panel missing");
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
            await CaptureRenderedViewIfRequested("01_tactical_start");

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
            GD.Print("GA_NEXT_FLOATING_WINDOW_PASS pointer=godot_parsed " +
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
        global::Godot.Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = from, GlobalPosition = from,
            Relative = Vector2.Zero, ButtonMask = MouseButtonMask.None
        });
        await WaitFrame();
        global::Godot.Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true,
            Position = from, GlobalPosition = from,
            ButtonMask = MouseButtonMask.Left
        });
        await WaitFrame();
        global::Godot.Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = to, GlobalPosition = to,
            Relative = to - from, ButtonMask = MouseButtonMask.Left
        });
        await WaitFrame();
        global::Godot.Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = false,
            Position = to, GlobalPosition = to,
            ButtonMask = MouseButtonMask.None
        });
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
