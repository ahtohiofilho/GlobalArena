using Godot;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>R230.3: compact contextual readout for the selected own worker.
/// Keep the existing selector, Move / Cancel and detailed City/Workers
/// planner. Use the same private queue and committed crossing as that planner.</summary>
public partial class Main
{
    private VBoxContainer? _worldUnitReadout;
    private Label? _worldUnitActivity, _worldUnitBase, _worldUnitQueue, _worldUnitTravel;
    private ProgressBar? _worldUnitProgress;
    private HBoxContainer? _worldUnitBaseMetric, _worldUnitQueueMetric;
    private TextureRect? _worldUnitBaseIcon, _worldUnitBaseState, _worldUnitQueueState;
    private Button? _worldUnitDetails;

    private static string DescribeWorkerBase(FoundationTacticalHumanAiTurnHost host, FoundationWorkerOrderQueue q) =>
        q.BaseActivity?.Crop is { } crop ? $"Grow {(CropId)crop}" :
        q.BaseActivity?.Mode == FoundationWorkerBaseMode.Automatic ||
            (q.BaseActivity is null && host.DefaultAutomaticBaseEnabled)
            ? "Automatic (local)" : q.BaseActivity?.Occupation?.ToString() ?? "Not configured";

    private void CreateWorldUnitReadout(VBoxContainer unit)
    {
        _worldUnitReadout = new VBoxContainer { Name = "GANextUnitReadout",
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        unit.AddChild(_worldUnitReadout);
        // Before the order panel, after the existing identity and selector.
        unit.MoveChild(_worldUnitReadout, Math.Max(0, unit.GetChildCount() - 2));
        _worldUnitActivity = WorldLayerPlaceholder("");
        _worldUnitTravel = WorldLayerPlaceholder("");
        _worldUnitProgress = new ProgressBar { MinValue = 0, MaxValue = 100,
            Step = 0, ShowPercentage = false, Visible = false };
        _worldUnitActivity.Visible = false;
        _worldUnitReadout.AddChild(_worldUnitActivity);
        // Compact visible metrics; detailed strings remain in tooltips.
        _worldUnitBaseMetric = GaHudWidgets.MetricRow("auto", "", "Base activity", out var baseValue);
        _worldUnitBase = baseValue;
        _worldUnitBaseIcon = (TextureRect)_worldUnitBaseMetric.GetChild(0);
        _worldUnitBaseState = GaHudWidgets.MetricIcon("check");
        _worldUnitBaseMetric.AddChild(_worldUnitBaseState);
        _worldUnitQueueMetric = GaHudWidgets.MetricRow("queue", "", "Scheduled orders", out var queueValue);
        _worldUnitQueue = queueValue;
        _worldUnitQueueState = GaHudWidgets.MetricIcon("check");
        _worldUnitQueueMetric.AddChild(_worldUnitQueueState);
        var metrics = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        metrics.AddThemeConstantOverride("separation", 12);
        _worldUnitBaseMetric.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _worldUnitQueueMetric.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        metrics.AddChild(_worldUnitBaseMetric);
        metrics.AddChild(_worldUnitQueueMetric);
        _worldUnitReadout.AddChild(metrics);
        _worldUnitTravel.Visible = false;
        _worldUnitReadout.AddChild(_worldUnitTravel);
        _worldUnitReadout.AddChild(_worldUnitProgress);
        ClearWorldUnitReadout();
    }

    private void ClearWorldUnitReadout()
    {
        if (_worldUnitReadout is not null) _worldUnitReadout.Visible = false;
        if (_worldUnitDetails is not null) _worldUnitDetails.Disabled = true;
        if (_worldUnitTravel is not null) _worldUnitTravel.Text = "";
        if (_worldUnitProgress is not null) { _worldUnitProgress.Visible = false; _worldUnitProgress.Value = 0; }
    }

    private void RefreshWorldUnitReadout(FoundationTacticalHumanAiTurnHost host, FoundationWorkerOrderQueue q)
    {
        if (_worldUnitReadout is null) return;
        if (!host.Snapshot.Workers.Workers.Any(w => w.Id == q.Worker && w.Owner == _humanCivilization))
        { ClearWorldUnitReadout(); return; }
        var committed = host.Snapshot.WorkerOrders.Queues.Single(x => x.Worker == q.Worker);
        _worldUnitReadout.Visible = true;
        _worldUnitActivity!.Text = "Current: " + (_persistentWorkerSummary?.Text ?? "Unavailable");
        var description = DescribeWorkerBase(host, q);
        bool staged = !Equals(q.BaseActivity, committed.BaseActivity);
        _worldUnitBase!.Text = ShortUnitBase(description);
        _worldUnitBaseMetric!.TooltipText = "Base: " + description + (staged ? " | Pending submission (E)" : " | Committed");
        _worldUnitBaseIcon!.Texture = GaHudWidgets.Icon(UnitBaseIcon(q, description));
        _worldUnitBaseState!.Texture = GaHudWidgets.Icon(staged ? "pending" : "check");
        _worldUnitBaseState.Modulate = staged ? GaVisualFoundation.Warm : GaVisualFoundation.Muted;
        bool changed = !q.Orders.SequenceEqual(committed.Orders) || q.ActiveOrderId != committed.ActiveOrderId;
        _worldUnitQueue!.Text = q.Orders.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _worldUnitQueueMetric!.TooltipText = $"Orders: {q.Orders.Count}" +
            (changed ? " | Pending submission (E)" : q.ActiveOrderId.HasValue ? " | Active" : " | Committed");
        _worldUnitQueueState!.Texture = GaHudWidgets.Icon(changed ? "pending" : q.ActiveOrderId.HasValue ? "active" : "check");
        _worldUnitQueueState.Modulate = changed ? GaVisualFoundation.Warm : GaVisualFoundation.Muted;
        if (_worldUnitDetails is not null) _worldUnitDetails.Disabled = false;
        // The existing planner computes this from authoritative EdgeTravels;
        // queued moves never appear as progress before the actual close.
        bool crossing = _persistentCrossingProgress?.Visible == true;
        _worldUnitTravel!.Visible = false;
        _worldUnitTravel.Text = crossing ? _persistentCrossingLabel!.Text : "";
        _worldUnitProgress!.Visible = crossing;
        _worldUnitProgress.Value = crossing ? _persistentCrossingProgress!.Value : 0;
        _worldUnitProgress.TooltipText = _worldUnitTravel.Text;
    }

    private static string ShortUnitBase(string description) => description switch
    {
        "Automatic (local)" => "Auto",
        "Forestry" => "Wood",
        "Mining" => "Stone",
        "LandTransport" => "Land",
        "PortOperations" => "Port",
        "Not configured" => "—",
        _ => description.StartsWith("Grow ", StringComparison.Ordinal) ? description[5..] : description
    };

    private static string UnitBaseIcon(FoundationWorkerOrderQueue q, string description) =>
        description == "Automatic (local)" ? "auto" : q.BaseActivity?.Crop is not null ? "crop" :
        description == "Forestry" ? "wood" : description == "Mining" ? "stone" :
        description is "LandTransport" or "PortOperations" ? "connection" : "unit";

    private void AssertWorldUnitReadout(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId id)
    {
        var hash = host.StateHash;
        var q = ReadPersistentQueue(id);
        var committed = host.Snapshot.WorkerOrders.Queues.Single(x => x.Worker == id);
        var staged = !Equals(q.BaseActivity, committed.BaseActivity);
        if (_worldUnitReadout?.Visible != true ||
            _worldUnitActivity?.Text != "Current: " + _persistentWorkerSummary?.Text ||
            _worldUnitBase?.Text != ShortUnitBase(DescribeWorkerBase(host, q)) ||
            _worldUnitBaseMetric?.TooltipText != "Base: " + DescribeWorkerBase(host, q) + (staged ? " | Pending submission (E)" : " | Committed") ||
            _worldUnitBaseState?.Texture != GaHudWidgets.Icon(staged ? "pending" : "check") ||
            _worldUnitQueue?.Text != q.Orders.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
            _worldUnitQueueState?.Texture != GaHudWidgets.Icon(
                !q.Orders.SequenceEqual(committed.Orders) || q.ActiveOrderId != committed.ActiveOrderId
                    ? "pending" : q.ActiveOrderId.HasValue ? "active" : "check") ||
            _worldUnitBase?.IsVisibleInTree() != true || _worldUnitQueue?.IsVisibleInTree() != true ||
            _worldUnitActivity?.IsVisibleInTree() == true || _worldUnitDetails?.Disabled != false ||
            _worldUnitProgress?.Visible != host.Snapshot.EdgeTravels.Pending.Any(p => p.Worker == id) ||
            _worldUnitCard?.IsAncestorOf(_localMoveButton!) != true ||
            _worldUnitCard?.IsAncestorOf(_localCancelQueuedButton!) != true || host.StateHash != hash)
            throw new InvalidOperationException("Unit card does not match selected private queue and committed activity.");
    }
}
