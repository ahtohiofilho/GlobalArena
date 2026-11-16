using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Read-only individually keyed worker roster for the operational dock.
/// A v9 strategic CellId is a *region of presentation*, not a G(18,0)
/// tactical destination. The roster cannot fabricate a fine location,
/// spawn workers, queue orders or edit the authoritative WorldState.
/// </summary>
public readonly record struct GaWorkerRosterRow(
    ulong WorkerId,
    ulong CoarseRegionId,
    string Activity,
    ulong CommodityId,
    ulong OutputMicroPerTurn,
    long DemandMultiplierMicro,
    ulong? FineTileId = null,
    string? OccupationName = null);

public partial class GaOperationalRoster : VBoxContainer
{
    public event Action<ulong>? WorkerSelected;

    private readonly Dictionary<ulong, Button> _rowButtons = new();
    private VBoxContainer? _rows;
    private Label? _details;
    private Label? _heading;
    private ulong? _selectedId;

    public ulong? SelectedWorkerId => _selectedId;
    public int DisplayedWorkerCount => _rowButtons.Count;
    public bool HasAuthoritativeFinePositions { get; private set; }

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 4);
        _heading = GaVisualFoundation.Caption("LOCAL CIVILIZATION  /  WORKFORCE");
        AddChild(_heading);

        var listScroll = new ScrollContainer
        {
            Name = "GANextRosterScroll",
            CustomMinimumSize = new Vector2(0f, 108f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        AddChild(listScroll);
        _rows = new VBoxContainer
        {
            Name = "GANextRosterRows",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _rows.AddThemeConstantOverride("separation", 2);
        listScroll.AddChild(_rows);

        _details = new Label
        {
            Name = "GANextWorkerDetails",
            Text = "Select a worker to inspect its authoritative census record.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _details.AddThemeColorOverride("font_color", GaVisualFoundation.Muted);
        _details.AddThemeFontSizeOverride("font_size", GaVisualFoundation.TableFontSize);
        AddChild(_details);
    }

    public void Refresh(IReadOnlyList<GaWorkerRosterRow> workers,
        ulong? selectedWorkerId, ulong revision)
    {
        if (_rows is null || _details is null || _heading is null)
            return;
        // UI-only rebuild: each row retains its stable worker ID; the
        // selected ID is never interpreted as an array index or fine tile ID.
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
        _rowButtons.Clear();
        _selectedId = null;
        HasAuthoritativeFinePositions = workers.Count > 0 &&
            workers.All(w => w.FineTileId.HasValue && w.FineTileId.Value != 0UL);
        if (workers.Any(w => w.FineTileId.HasValue) && !HasAuthoritativeFinePositions)
            throw new InvalidOperationException("Mixed fine/strategic worker location roster.");
        _heading.Text = $"LOCAL WORKFORCE  /  {workers.Count} INDIVIDUALS  /  REV {revision}";

        foreach (var worker in workers)
        {
            var id = worker.WorkerId;
            if (_rowButtons.ContainsKey(id))
                throw new InvalidOperationException($"Duplicate physical worker #{id}");
            var button = new Button
            {
                Name = $"GANextWorkerRow_{id}",
                Text = worker.FineTileId.HasValue
                    ? worker.OccupationName is not null
                        ? $"#{id}  {worker.OccupationName} · {worker.Activity} · T{worker.FineTileId.Value}"
                        : $"#{id}   {worker.Activity}   ·   G(18,0)/{worker.FineTileId.Value}"
                    : $"#{id}   {worker.Activity}    ·    Region T{worker.CoarseRegionId}",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0f, 27f),
                TooltipText = worker.FineTileId.HasValue
                    ? $"Worker #{id} · canonical G(18,0)/{worker.FineTileId.Value}"
                    : $"Worker #{id} · G(3,0) region T{worker.CoarseRegionId} · " +
                      "Exact tactical cell is not stored in journal v9"
            };
            button.Pressed += () => WorkerSelected?.Invoke(id);
            _rows.AddChild(button);
            _rowButtons.Add(id, button);
        }

        var chosen = default(GaWorkerRosterRow?);
        foreach (var worker in workers)
            if (worker.WorkerId == selectedWorkerId)
            {
                chosen = worker;
                break;
            }
        if (!chosen.HasValue && workers.Count > 0) chosen = workers[0];
        if (!chosen.HasValue)
        {
            _details.Text = "No workers in this civilization.";
            return;
        }

        var selected = chosen.Value;
        _selectedId = selected.WorkerId;
        if (_rowButtons.TryGetValue(selected.WorkerId, out var active))
            active.AddThemeColorOverride("font_color", GaVisualFoundation.Accent);

        var output = (selected.OutputMicroPerTurn / 1_000_000m)
            .ToString("0.###", CultureInfo.InvariantCulture);
        var demand = (selected.DemandMultiplierMicro / 1_000_000m)
            .ToString("0.###", CultureInfo.InvariantCulture);
        _details.Text = $"SELECTED  #{selected.WorkerId}  ·  {selected.Activity}\n" +
            (selected.OccupationName is not null
                ? $"Atividade econômica: {selected.OccupationName}\n" : "") +
            $"Item {selected.CommodityId}   |   Capacity {output}/turn   |   Demand ×{demand}\n" +
            $"Known region: G(3,0) T{selected.CoarseRegionId}\n" +
            (selected.FineTileId.HasValue
                ? $"Exact tactical G(18,0) tile: {selected.FineTileId.Value} " +
                  $"(v{(selected.OccupationName is null ? 10 : 11)}).\n" +
                  "Click an adjacent hex, then G to queue a real fine-edge move."
                : "Exact tactical G(18,0) tile: unavailable (v9). Movement locked.");
    }
}
