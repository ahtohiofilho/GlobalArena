using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.Runtime;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Optional, reversible visual hypothesis. A three-column worker board on the
/// currently clicked PHYSICAL tile. Tokens remain in their real occupations
/// until the host publishes a new census after E. Drag/drop is only another
/// route to the EXISTING authorized v11 occupation order.
/// </summary>
public partial class TacticalContextPreview
{
    private static readonly PhysicalWorkerOccupation[] BoardRoles =
    {
        PhysicalWorkerOccupation.Agriculture,
        PhysicalWorkerOccupation.Forestry,
        PhysicalWorkerOccupation.Mining
    };

    private PanelContainer? _workerBoard;
    private Label? _workerBoardHeading;
    private Label? _workerBoardStatus;
    private Button? _workerBoardCancelSelected;
    private ulong? _workerBoardSelectedId;
    private bool _workerBoardAcceptingOrders = true;
    private readonly Dictionary<ulong, PhysicalWorkerOccupation> _workerBoardPending = new();
    private readonly Dictionary<PhysicalWorkerOccupation, GaWorkerBoardDropZone>
        _workerBoardZones = new();
    private readonly Dictionary<PhysicalWorkerOccupation, VBoxContainer>
        _workerBoardChips = new();
    private readonly Dictionary<PhysicalWorkerOccupation, Label>
        _workerBoardCounts = new();
    private readonly Dictionary<PhysicalWorkerOccupation, Button>
        _workerBoardTransferButtons = new();

    public event Action<ulong, PhysicalWorkerOccupation>? WorkerBoardOccupationRequested;
    public event Action<ulong>? WorkerBoardCancelSelectedOrderRequested;

    public bool CanCancelSelectedWorkerBoardOrder =>
        IsWorkerBoardVisible && _workerBoardSelectedId is { } id &&
        _tileClickOwnerId == _tileClickHuman.Value &&
        _workerBoardAcceptingOrders && _workerBoardPending.ContainsKey(id);

    public Rect2? WorkerBoardCancelSelectedRect =>
        _workerBoardCancelSelected?.IsVisibleInTree() == true
            ? _workerBoardCancelSelected.GetGlobalRect() : null;

    public bool IsWorkerBoardVisible => _workerBoard?.Visible == true;
    public ulong? WorkerBoardSelectedId => _workerBoardSelectedId;
    public Rect2? WorkerBoardChipRect(ulong workerId)
    {
        var chip = _workerBoard?.FindChild(
            $"WorkerBoardChip_{workerId}", true, false) as Control;
        return chip is not null && chip.IsVisibleInTree()
            ? chip.GetGlobalRect() : null;
    }
    public Rect2? WorkerBoardDropRect(PhysicalWorkerOccupation role) =>
        _workerBoardZones.TryGetValue(role, out var zone) && zone.IsVisibleInTree()
            ? zone.GetGlobalRect() : null;
    public Rect2? WorkerBoardTransferRect(PhysicalWorkerOccupation role) =>
        _workerBoardTransferButtons.TryGetValue(role, out var button) &&
        button.IsVisibleInTree() ? button.GetGlobalRect() : null;

    public int WorkerBoardActualCount(PhysicalWorkerOccupation role) =>
        BoardPeople().Count(worker => worker.Occupation == role);

    public int WorkerBoardProjectedCount(PhysicalWorkerOccupation role) =>
        BoardPeople().Count(worker =>
            (_workerBoardPending.TryGetValue(worker.Id.Value, out var queued)
                ? queued : worker.Occupation) == role);

    public void PresentPendingWorkerBoardOrders(
        IEnumerable<(ulong WorkerId, PhysicalWorkerOccupation Role)> orders,
        bool acceptingOrders)
    {
        _workerBoardPending.Clear();
        foreach (var (workerId, role) in orders)
            _workerBoardPending[workerId] = role;
        _workerBoardAcceptingOrders = acceptingOrders;
        if (IsWorkerBoardVisible) RebuildWorkerBoard();
    }

    private PhysicalWorkerRuntimeRecord[] BoardPeople()
    {
        if (!_tileClickFineId.HasValue || !_tileClickOwnerId.HasValue)
            return Array.Empty<PhysicalWorkerRuntimeRecord>();
        return _displayWorkers.Where(w =>
            w.TacticalTileId?.FineStrategicCellId.Value == _tileClickFineId.Value &&
            w.Owner.Value == _tileClickOwnerId.Value &&
            w.Occupation.HasValue)
            .OrderBy(w => w.Id.Value).ToArray();
    }

    private bool CanBoardPlaceWorker(ulong workerId, PhysicalWorkerOccupation target)
    {
        return _tileClickOwnerId == _tileClickHuman.Value &&
            _workerBoardAcceptingOrders &&
            BoardPeople().Any(worker => worker.Id.Value == workerId &&
                worker.Occupation != target);
    }

    private void InitializeWorkerBoard()
    {
        var board = new PanelContainer
        {
            Name = "GANextWorkforceBoard",
            Visible = false,
            ZIndex = 20,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(626f, 350f)
        };
        board.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        AddChild(board);
        _workerBoard = board;

        var root = new VBoxContainer
        {
            Name = "WorkerBoardContents",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        board.AddChild(root);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        root.AddChild(header);
        _workerBoardHeading = new Label
        {
            Name = "WorkerBoardHeading",
            Text = "WORKERS",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _workerBoardHeading.AddThemeFontSizeOverride("font_size", 15);
        header.AddChild(_workerBoardHeading);
        var back = new Button
        {
            Name = "WorkerBoardBack",
            Text = "BACK",
            FocusMode = FocusModeEnum.None
        };
        back.Pressed += HideWorkerBoard;
        header.AddChild(back);

        root.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore });
        var columns = new HBoxContainer
        {
            Name = "WorkerBoardColumns",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        columns.AddThemeConstantOverride("separation", 7);
        root.AddChild(columns);
        foreach (var role in BoardRoles)
        {
            var roleCopy = role;
            var zone = new GaWorkerBoardDropZone
            {
                Name = $"WorkerBoardZone_{role}",
                Role = role,
                MouseFilter = MouseFilterEnum.Stop,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(186f, 240f),
                CanAcceptWorker = workerId => CanBoardPlaceWorker(workerId, roleCopy),
                OccupationRequested = RequestWorkerBoardOccupation
            };
            zone.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
            columns.AddChild(zone);
            _workerBoardZones[role] = zone;

            var contents = new VBoxContainer
            {
                MouseFilter = MouseFilterEnum.Pass,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            contents.AddThemeConstantOverride("separation", 5);
            zone.AddChild(contents);
            var count = new Label
            {
                Name = $"WorkerBoardCount_{role}",
                MouseFilter = MouseFilterEnum.Ignore
            };
            count.AddThemeFontSizeOverride("font_size", 12);
            contents.AddChild(count);
            _workerBoardCounts[role] = count;

            var transfer = new Button
            {
                Name = $"WorkerBoardTransfer_{role}",
                Text = "ASSIGN SELECTED",
                FocusMode = FocusModeEnum.None,
                Disabled = true,
                MouseFilter = MouseFilterEnum.Stop
            };
            transfer.AddThemeFontSizeOverride("font_size", 10);
            transfer.Pressed += () =>
            {
                if (_workerBoardSelectedId.HasValue)
                    RequestWorkerBoardOccupation(_workerBoardSelectedId.Value, roleCopy);
            };
            contents.AddChild(transfer);
            _workerBoardTransferButtons[role] = transfer;

            var scroll = new ScrollContainer
            {
                Name = $"WorkerBoardScroll_{role}",
                MouseFilter = MouseFilterEnum.Pass,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(174f, 170f)
            };
            contents.AddChild(scroll);
            var chips = new VBoxContainer
            {
                MouseFilter = MouseFilterEnum.Pass,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            chips.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(chips);
            _workerBoardChips[role] = chips;
        }

        var cancelSelected = new Button
        {
            Name = "WorkerBoardCancelSelected",
            Text = "CANCEL SELECTED ORDER",
            FocusMode = FocusModeEnum.None,
            Disabled = true,
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin
        };
        cancelSelected.Pressed += () =>
        {
            if (!CanCancelSelectedWorkerBoardOrder ||
                !_workerBoardSelectedId.HasValue) return;
            WorkerBoardCancelSelectedOrderRequested?.Invoke(
                _workerBoardSelectedId.Value);
        };
        root.AddChild(cancelSelected);
        _workerBoardCancelSelected = cancelSelected;

        _workerBoardStatus = new Label
        {
            Name = "WorkerBoardStatus",
            Text = "DRAG A WORKER OR CLICK + ASSIGN. E CONFIRMS; X CANCELS.",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _workerBoardStatus.AddThemeFontSizeOverride("font_size", 10);
        _workerBoardStatus.AddThemeColorOverride("font_color", GaVisualFoundation.Accent);
        root.AddChild(_workerBoardStatus);
        board.Resized += PositionWorkerBoard;
        Resized += PositionWorkerBoard;
    }

    public void OpenWorkerBoard()
    {
        if (!IsTileClickMenuVisible || !_tileClickFineId.HasValue ||
            !_tileClickOwnerId.HasValue || _workerBoard is null)
            return;
        _workerBoardSelectedId = _selectedWorkerGlyphId.HasValue &&
            BoardPeople().Any(w => w.Id.Value == _selectedWorkerGlyphId.Value)
            ? _selectedWorkerGlyphId : null;
        _workerBoard.Visible = true;
        RebuildWorkerBoard();
        PositionWorkerBoard();
        GD.Print($"GA_NEXT_WORKER_BOARD_OPEN tile={_tileClickFineId} " +
            $"owner={_tileClickOwnerId} real_people={BoardPeople().Length} " +
            "three_slots=true read_only_census=true");
    }

    private void PositionWorkerBoard()
    {
        if (_workerBoard is null || !_workerBoard.Visible) return;
        var size = _workerBoard.Size;
        if (size.X <= 0f || size.Y <= 0f)
            size = _workerBoard.CustomMinimumSize;
        _workerBoard.Position = new Vector2(
            Mathf.Max(6f, (Size.X - size.X) / 2f),
            Mathf.Max(6f, (Size.Y - size.Y) / 2f));
    }

    private void HideWorkerBoard()
    {
        if (_workerBoard is not null) _workerBoard.Visible = false;
        _workerBoardSelectedId = null;
    }

    private void RefreshWorkerBoardAfterSnapshot()
    {
        if (IsWorkerBoardVisible) RebuildWorkerBoard();
    }

    private void RequestWorkerBoardOccupation(
        ulong workerId, PhysicalWorkerOccupation role)
    {
        if (!IsWorkerBoardVisible || !CanBoardPlaceWorker(workerId, role))
            return;
        WorkerBoardOccupationRequested?.Invoke(workerId, role);
        GD.Print($"GA_NEXT_WORKER_BOARD_INTENT tile={_tileClickFineId} " +
            $"worker={workerId} role={role} " +
            "source=ui_authorized_host_only no_census_mutation=true");
    }

    private void RebuildWorkerBoard()
    {
        if (!IsWorkerBoardVisible || !_tileClickFineId.HasValue ||
            !_tileClickOwnerId.HasValue || _workerBoardHeading is null ||
            _workerBoardStatus is null) return;
        var people = BoardPeople();
        if (people.Length == 0)
        {
            HideWorkerBoard();
            return;
        }
        var owner = _tileClickOwnerId.Value;
        var editable = owner == _tileClickHuman.Value && _workerBoardAcceptingOrders;
        if (_workerBoardSelectedId.HasValue &&
            !people.Any(w => w.Id.Value == _workerBoardSelectedId.Value))
            _workerBoardSelectedId = null;

        _workerBoardHeading.Text =
            $"WORKERS  ·  TILE {_tileClickFineId}  ·  CIV {owner} " +
            $"({GaCivilizationVisualIdentity.EnglishNameFor(owner)})";
        var queuedCount = people.Count(w => _workerBoardPending.ContainsKey(w.Id.Value));
        _workerBoardStatus.Text = !editable
            ? "READ ONLY: YOU CANNOT CHANGE THIS CIVILIZATION'S WORKERS."
            : queuedCount > 0
                ? $"{queuedCount} PENDING ORDER(S). COUNTS CHANGE ONLY AFTER E. " +
                  "SELECT ONE TO CANCEL ITS ORDER; X ALSO CANCELS."
                : "DRAG A WORKER OR CLICK + ASSIGN. E CONFIRMS; X CANCELS.";
        if (_workerBoardCancelSelected is not null)
            _workerBoardCancelSelected.Disabled =
                !CanCancelSelectedWorkerBoardOrder;

        foreach (var role in BoardRoles)
        {
            var group = people.Where(w => w.Occupation == role).ToArray();
            var projected = WorkerBoardProjectedCount(role);
            _workerBoardCounts[role].Text = projected == group.Length
                ? $"{role.ToString().ToUpperInvariant()}   {group.Length}"
                : $"{role.ToString().ToUpperInvariant()}   {group.Length} → {projected}*";
            var zone = _workerBoardZones[role];
            zone.TileId = _tileClickFineId.Value;
            zone.OwnerId = owner;
            var transfer = _workerBoardTransferButtons[role];
            transfer.Disabled = !_workerBoardSelectedId.HasValue ||
                !CanBoardPlaceWorker(_workerBoardSelectedId.Value, role);
            var container = _workerBoardChips[role];
            foreach (var existing in container.GetChildren())
            {
                container.RemoveChild(existing);
                existing.QueueFree();
            }
            foreach (var worker in group)
            {
                var workerCopy = worker;
                var selected = _workerBoardSelectedId == worker.Id.Value;
                var queued = _workerBoardPending.TryGetValue(worker.Id.Value,
                    out var pendingRole);
                var chip = new GaWorkerBoardDragChip
                {
                    Name = $"WorkerBoardChip_{worker.Id.Value}",
                    WorkerId = worker.Id.Value,
                    TileId = _tileClickFineId.Value,
                    OwnerId = owner,
                    Draggable = editable,
                    Text = $"♟  #{worker.Id.Value}" +
                        (selected ? "  [SELECTED]" : "") +
                        (queued ? $"  → {pendingRole}*" : ""),
                    TooltipText = $"Worker #{worker.Id.Value} · {worker.Activity} · " +
                        (queued ? $"PENDING {pendingRole}" : role.ToString()),
                    MouseFilter = MouseFilterEnum.Stop,
                    FocusMode = FocusModeEnum.None,
                    Disabled = !editable,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(170f, 32f)
                };
                chip.AddThemeFontSizeOverride("font_size", 11);
                chip.Pressed += () =>
                {
                    _workerBoardSelectedId = workerCopy.Id.Value;
                    CanonicalWorkerBadgeClicked?.Invoke(workerCopy.Id.Value);
                    if (IsWorkerBoardVisible) RebuildWorkerBoard();
                };
                container.AddChild(chip);
            }
        }
        PositionWorkerBoard();
    }
}
