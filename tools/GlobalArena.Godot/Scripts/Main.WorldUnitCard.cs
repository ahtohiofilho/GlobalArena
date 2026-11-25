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
        _worldUnitBase = WorldLayerPlaceholder("");
        _worldUnitQueue = WorldLayerPlaceholder("");
        _worldUnitTravel = WorldLayerPlaceholder("");
        _worldUnitProgress = new ProgressBar { MinValue = 0, MaxValue = 100,
            Step = 0, ShowPercentage = false, Visible = false };
        foreach (var label in new[] { _worldUnitActivity, _worldUnitBase, _worldUnitQueue, _worldUnitTravel })
            _worldUnitReadout.AddChild(label);
        _worldUnitReadout.AddChild(_worldUnitProgress);
        ClearWorldUnitReadout();
    }

    private void ClearWorldUnitReadout()
    {
        if (_worldUnitReadout is not null) _worldUnitReadout.Visible = false;
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
        _worldUnitBase!.Text = "Base: " + DescribeWorkerBase(host, q) +
            (!Equals(q.BaseActivity, committed.BaseActivity) ? " | Pending submission" : " | Committed");
        bool changed = !q.Orders.SequenceEqual(committed.Orders) || q.ActiveOrderId != committed.ActiveOrderId;
        _worldUnitQueue!.Text = $"Orders: {q.Orders.Count}" +
            (changed ? " | Pending submission" : q.ActiveOrderId.HasValue ? " | Active" : " | Committed");
        // The existing planner computes this from authoritative EdgeTravels;
        // queued moves never appear as progress before the actual close.
        bool crossing = _persistentCrossingProgress?.Visible == true;
        _worldUnitTravel!.Visible = crossing;
        _worldUnitTravel.Text = crossing ? _persistentCrossingLabel!.Text : "";
        _worldUnitProgress!.Visible = crossing;
        _worldUnitProgress.Value = crossing ? _persistentCrossingProgress!.Value : 0;
    }

    private void AssertWorldUnitReadout(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId id)
    {
        var hash = host.StateHash;
        var q = ReadPersistentQueue(id);
        var committed = host.Snapshot.WorkerOrders.Queues.Single(x => x.Worker == id);
        var staged = !Equals(q.BaseActivity, committed.BaseActivity);
        if (_worldUnitReadout?.Visible != true ||
            _worldUnitActivity?.Text != "Current: " + _persistentWorkerSummary?.Text ||
            _worldUnitBase?.Text != "Base: " + DescribeWorkerBase(host, q) + (staged ? " | Pending submission" : " | Committed") ||
            _worldUnitQueue?.Text.StartsWith($"Orders: {q.Orders.Count}", StringComparison.Ordinal) != true ||
            _worldUnitProgress?.Visible != host.Snapshot.EdgeTravels.Pending.Any(p => p.Worker == id) ||
            _worldUnitCard?.IsAncestorOf(_localMoveButton!) != true ||
            _worldUnitCard?.IsAncestorOf(_localCancelQueuedButton!) != true || host.StateHash != hash)
            throw new InvalidOperationException("Unit card does not match selected private queue and committed activity.");
    }
}
