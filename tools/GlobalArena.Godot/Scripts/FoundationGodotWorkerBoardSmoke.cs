using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Runs in a REAL Godot Mono scene. Verifies board UI entry, native drag/drop
/// callbacks, authority gating, pending-only count projection, click alternate,
/// X cancel, E commit and the original click-only menu's Escape behavior.
/// The Godot drag callbacks are driven directly for deterministic coverage;
/// mouse gesture ergonomics still require human Windows visual homologation.
/// </summary>
public sealed class FoundationGodotWorkerBoardSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;

    public FoundationGodotWorkerBoardSmoke(
        Main main, FoundationTacticalHumanAiTurnHost host, TacticalContextPreview map)
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
        for (var i = 0; i < 6; i++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Click(Vector2 position)
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventMouseMotion
        {
            Position = position, GlobalPosition = position,
            ButtonMask = (MouseButtonMask)0, Relative = Vector2.Zero
        }, inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left,
            Pressed = true
        }, inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, ButtonMask = (MouseButtonMask)0,
            Pressed = false
        }, inLocalCoords: true);
        await Frames();
    }

    private async Task KeyPress(Key code)
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventKey { Keycode = code, Pressed = true },
            inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventKey { Keycode = code, Pressed = false },
            inLocalCoords: true);
        await Frames();
    }

    private async Task Capture(string name)
    {
        var dir = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_WORKER_BOARD_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        await Frames();
        var image = _main.GetViewport().GetTexture().GetImage();
        Must(image is not null && !image.IsEmpty(),
            "Real Godot rendered frame not available.");
        var file = Path.Combine(dir!, name + ".png");
        Must(image!.SavePng(file) == Error.Ok &&
            new FileInfo(file).Length > 1500,
            "Failed to capture real Godot worker board.");
        GD.Print($"GA_NEXT_WORKER_BOARD_FRAME_PASS stage={name}");
    }

    public async Task RunAsync()
    {
        try
        {
            await Frames();
            var humans = _host.Snapshot.Workers.Workers
                .Where(worker => worker.Owner.Value == 1UL).ToArray();
            var tile = humans[0].TacticalTileId!.Value;
            var hash = _host.StateHash;
            Must(humans.Length == 6 &&
                 humans.All(w => w.TacticalTileId == tile) &&
                 _map.IsTileClickHypothesisEnabled,
                "Board test requires the accepted six-worker physical tile fixture.");
            Must(_map.TryGetRenderedFineTilePixel(tile, out var pixel),
                "Worker tile is not visible for a real click.");
            await Click(_map.GlobalPosition + pixel);
            Must(_map.IsTileClickMenuVisible && !_map.IsWorkerBoardVisible,
                "Old click-only tile menu must remain the entry point.");
            var organizer = _map.FindChild(
                "WorkerBoardOpenButton", true, false) as Button;
            Must(organizer is not null && organizer.IsVisibleInTree(),
                "Explicit worker organizer button was not attached to tile menu.");
            await Click(organizer!.GetGlobalRect().GetCenter());
            Must(_map.IsWorkerBoardVisible && _map.IsTileClickMenuVisible &&
                 _host.StateHash == hash,
                "Opening a board must preserve click menu and authoritative hash.");
            foreach (var role in new[] {
                PhysicalWorkerOccupation.Agriculture,
                PhysicalWorkerOccupation.Forestry,
                PhysicalWorkerOccupation.Mining })
                Must(_map.WorkerBoardActualCount(role) == 2 &&
                     _map.WorkerBoardProjectedCount(role) == 2,
                    "Board must contain exactly two real people in each role.");
            var allChips = _map.FindChildren(
                "WorkerBoardChip_*", "", true, false);
            Must(allChips.Count == 6 &&
                 allChips.Cast<GaWorkerBoardDragChip>()
                     .Select(chip => chip.WorkerId).Distinct().Count() == 6,
                "Board duplicated or invented physical workers.");
            GD.Print("GA_NEXT_WORKER_BOARD_SLOTS_PASS " +
                "roles=3 members=6 actual=2/2/2 hash_unchanged=true");
            await Capture("01_worker_board");

            var chipRect = _map.WorkerBoardChipRect(4UL);
            Must(chipRect.HasValue, "Worker #4 chip not visible.");
            await Click(chipRect.GetValueOrDefault().GetCenter());
            Must(_map.WorkerBoardSelectedId == 4UL &&
                 _main.SelectedTacticalWorkerId == 4UL &&
                 _host.StateHash == hash,
                "Click selection must delegate to the existing individual picker.");
            await Capture("02_worker_selected");

            var chip = _map.FindChild(
                "WorkerBoardChip_4", true, false) as GaWorkerBoardDragChip;
            var mining = _map.FindChild(
                "WorkerBoardZone_Mining", true, false) as GaWorkerBoardDropZone;
            Must(chip is not null && mining is not null,
                "Native chip/drop-zone classes are missing.");
            var payload = chip!._GetDragData(Vector2.Zero);
            Must(payload.VariantType == Variant.Type.String &&
                 mining!._CanDropData(Vector2.Zero, payload) &&
                 !mining._CanDropData(Vector2.Zero,
                     Variant.From("GA-NEXT-WORKER:999:2:4")) &&
                 !mining._CanDropData(Vector2.Zero,
                     Variant.From("GA-NEXT-WORKER:999:1:999")),
                "Drag identity/owner/tile acceptance must be scoped to this board.");
            mining!._DropData(Vector2.Zero, payload);
            await Frames();
            Must(_host.PendingOrderCount == 1 &&
                 _host.StateHash == hash &&
                 _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL)
                     .Occupation == PhysicalWorkerOccupation.Agriculture &&
                 _map.WorkerBoardActualCount(PhysicalWorkerOccupation.Agriculture) == 2 &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Agriculture) == 1 &&
                 _map.WorkerBoardActualCount(PhysicalWorkerOccupation.Mining) == 2 &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Mining) == 3,
                "Drop must only enqueue a real v11 order and project 2/2/2 → 1/2/3.");
            GD.Print("GA_NEXT_WORKER_BOARD_PENDING_PASS " +
                "worker=4 role=Mining actual=2/2/2 projected=1/2/3 " +
                "world_hash_unchanged=true");
            await Capture("03_pending_intent");

            // R110: selected worker cancellation is visible and targets
            // only its real host-queued order. Never touches settled census.
            Must(_map.CanCancelSelectedWorkerBoardOrder,
                "Selected own pending worker must expose cancel affordance.");
            var cancelRect = _map.WorkerBoardCancelSelectedRect;
            Must(cancelRect.HasValue,
                "Worker-board targeted cancel button must be visible.");
            await Click(cancelRect.GetValueOrDefault().GetCenter());
            Must(_host.PendingOrderCount == 0 &&
                 _host.StateHash == hash &&
                 !_map.CanCancelSelectedWorkerBoardOrder &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Mining) == 2 &&
                 _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL)
                     .Occupation == PhysicalWorkerOccupation.Agriculture,
                "Board cancel must remove ONLY queued intent before the E turn.");
            GD.Print("GA_NEXT_R110_WORKER_BOARD_CANCEL_PASS " +
                "worker=4 targeted=true authoritative_host=true " +
                "pending=0 actual_census_unchanged=true hash_unchanged=true");
            mining._DropData(Vector2.Zero, payload);
            await Frames();
            Must(_host.PendingOrderCount == 1 &&
                 _map.CanCancelSelectedWorkerBoardOrder &&
                 _host.StateHash == hash,
                "After targeted cancellation the same worker can queue again.");

            // R111: two independent real-worker orders may be queued.
            // Cancel either one directly from its row without changing
            // selection, and verify the other remains authoritative.
            var secondChip = _map.FindChild(
                "WorkerBoardChip_5", true, false) as GaWorkerBoardDragChip;
            Must(secondChip is not null,
                "Second real worker must remain visible in own tile board.");
            var secondPayload = secondChip!._GetDragData(Vector2.Zero);
            Must(mining._CanDropData(Vector2.Zero, secondPayload),
                "Second worker must be assignable to the mining zone.");
            mining._DropData(Vector2.Zero, secondPayload);
            await Frames();
            Must(_host.PendingOrderCount == 2 &&
                 _host.StateHash == hash &&
                 _map.WorkerBoardSelectedId == 4UL &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Mining) == 4,
                "Two independent pending orders must project without mutating world.");
            var cancelFourth = _map.WorkerBoardCancelPendingRect(4UL);
            Must(cancelFourth.HasValue,
                "Queued worker #4 needs a visible per-row cancel button.");
            await Click(cancelFourth.GetValueOrDefault().GetCenter());
            Must(_host.PendingOrderCount == 1 &&
                 _host.StateHash == hash &&
                 !_map.WorkerBoardCancelPendingRect(4UL).HasValue &&
                 _map.WorkerBoardCancelPendingRect(5UL).HasValue &&
                 _map.WorkerBoardSelectedId == 4UL &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Agriculture) == 2 &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Forestry) == 1 &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Mining) == 3,
                "Canceling worker #4 must preserve worker #5's pending intent.");
            var cancelFifth = _map.WorkerBoardCancelPendingRect(5UL);
            Must(cancelFifth.HasValue,
                "Worker #5 order must remain individually cancelable.");
            await Click(cancelFifth.GetValueOrDefault().GetCenter());
            Must(_host.PendingOrderCount == 0 &&
                 _host.StateHash == hash &&
                 _map.WorkerBoardSelectedId == 4UL &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Agriculture) == 2 &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Forestry) == 2 &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Mining) == 2,
                "Canceling both orders must restore the original projection.");
            GD.Print("GA_NEXT_R111_MULTI_ORDER_CANCEL_PASS " +
                "two_workers=true independent_cancel=true " +
                "selection_unchanged=true host_authority=true hash_unchanged=true");
            mining._DropData(Vector2.Zero, payload);
            await Frames();
            Must(_host.PendingOrderCount == 1 &&
                 _host.StateHash == hash,
                "Board must permit a new order after selective cancellations.");

            await KeyPress(Key.X);
            Must(_host.PendingOrderCount == 0 &&
                 _host.StateHash == hash &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Mining) == 2,
                "X must discard the queued board intention.");
            var transferRect = _map.WorkerBoardTransferRect(
                PhysicalWorkerOccupation.Mining);
            Must(transferRect.HasValue, "Click/keyboard role alternative disappeared.");
            await Click(transferRect.GetValueOrDefault().GetCenter());
            Must(_host.PendingOrderCount == 1 &&
                 _host.StateHash == hash,
                "ASSIGN SELECTED must use the same host queue as drop and keyboard N.");
            await KeyPress(Key.E);
            Must(_host.PendingOrderCount == 0 &&
                 _host.Snapshot.Workers.Workers.Count(w =>
                     w.Owner.Value == 1UL && w.TacticalTileId == tile) == 6 &&
                 _host.Snapshot.Workers.Workers.Count == 12 &&
                 _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL)
                     .Occupation == PhysicalWorkerOccupation.Mining &&
                 _map.WorkerBoardActualCount(PhysicalWorkerOccupation.Agriculture) == 1 &&
                 _map.WorkerBoardActualCount(PhysicalWorkerOccupation.Mining) == 3 &&
                 _map.WorkerBoardProjectedCount(PhysicalWorkerOccupation.Mining) == 3,
                "E must commit authoritative 1/2/3 with original population and journal.");
            GD.Print("GA_NEXT_WORKER_BOARD_COMMIT_PASS " +
                "worker=4 occupation=Mining people=12 real=1/2/3 " +
                "undo=X click_alternative=true replay=v11");
            await Capture("04_after_turn");

            await KeyPress(Key.Escape);
            Must(!_map.IsWorkerBoardVisible && _map.IsTileClickMenuVisible &&
                 _main.SelectedTacticalWorkerId == 4UL,
                "First Esc must close organizer but keep old click menu and selection.");
            await KeyPress(Key.Escape);
            Must(!_map.IsTileClickMenuVisible && _main.SelectedTacticalWorkerId == 4UL,
                "Second Esc must close click menu without clearing worker selection.");
            GD.Print("GA_NEXT_WORKER_BOARD_E2E_PASS " +
                "drag_drop_callback=true click_alternative=true " +
                "host_authority=true no_world_mutation_before_E=true " +
                "X_cancel=true E_commit=true close_stable=true");
            _main.GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PrintErr("GA_NEXT_WORKER_BOARD_FAIL " + error);
            _main.GetTree().Quit(91);
        }
    }
}
