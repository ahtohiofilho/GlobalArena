using Godot;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>R229.3: own physical residents of the selected node, not the
/// civilization-wide roster. The existing private worker planner is reused.</summary>
public partial class Main
{
    private TabContainer? _cityWorkersTabs;
    private ScrollContainer? _cityWorkersTab;
    private Label? _cityWorkersStatus;
    private VBoxContainer? _cityWorkersList;
    private ulong? _cityWorkerOrigin;
    private FoundationTacticalHumanAiTurnHost? _cityWorkerCropHost;
    private ulong _cityWorkerCropTurn;
    private readonly Dictionary<PhysicalWorkerId, FoundationPersistentWorkerSlice> _cityWorkerCropEvidence = new();

    // Last actual interval, not an order or preference. Keep only own workers.
    private void RecordCityWorkerCropEvidence(FoundationTacticalHumanAiTurnHost host, FoundationTacticalCollectiveReceipt receipt)
    {
        _cityWorkerCropEvidence.Clear();
        _cityWorkerCropHost = null;
        if (!receipt.ReplayMatched || receipt.Hash != host.StateHash || receipt.PersistentTimeline is not { } timeline) return;
        var own = host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization).Select(w => w.Id).ToHashSet();
        foreach (var group in timeline.Slices.Where(s => own.Contains(s.Worker) && s.DurationMicro > 0).GroupBy(s => s.Worker))
        {
            var last = group.OrderBy(s => s.StartMicro).Last();
            if (last.Activity == PhysicalWorkerActivity.Producing && last.Occupation == PhysicalWorkerOccupation.Agriculture && last.Crop.HasValue)
                _cityWorkerCropEvidence.Add(group.Key, last);
        }
        _cityWorkerCropHost = host;
        _cityWorkerCropTurn = host.NextTurn.Value;
    }

    private string? CityWorkerClosedCrop(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerRuntimeRecord worker)
    {
        if (!ReferenceEquals(_cityWorkerCropHost, host) || _cityWorkerCropTurn != host.NextTurn.Value ||
            worker.Owner != _humanCivilization || worker.Activity != PhysicalWorkerActivity.Producing ||
            worker.Occupation != PhysicalWorkerOccupation.Agriculture ||
            !_cityWorkerCropEvidence.TryGetValue(worker.Id, out var slice) || slice.Node != worker.TacticalTileId ||
            slice.Crop is not { } crop || !CropCatalog.All.Any(c => c.Id == (CropId)crop)) return null;
        return ((CropId)crop).ToString();
    }

    private void CreateCityWorkersTab(TabContainer tabs)
    {
        _cityWorkersTabs = tabs;
        _cityWorkersTab = new ScrollContainer { Name = "Workers",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        tabs.AddChild(_cityWorkersTab);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _cityWorkersTab.AddChild(body);
        _cityWorkersStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_cityWorkersStatus);
        _cityWorkersList = new VBoxContainer { Name = "GANextCityWorkersList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_cityWorkersList);
    }

    private void RefreshCityWorkers()
    {
        if (_cityWorkersStatus is null || _cityWorkersList is null ||
            _cityWindowLayer?.Visible != true || _cityWindowPanel?.Visible != true) return;
        foreach (Node child in _cityWorkersList.GetChildren())
        { _cityWorkersList.RemoveChild(child); child.QueueFree(); }
        var host = _tacticalHost;
        if (host is null || _cityConnectionSource is not { } source)
        {
            _cityWorkersStatus.Text = "Select a city on the map.";
            return;
        }
        var node = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, new StrategicCellId(source));
        var workers = host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization &&
            w.TacticalTileId == node).OrderBy(w => w.Id.Value).ToArray();
        _cityWorkersStatus.Text = workers.Length == 0 ? "No own workers at this node." :
            $"WORKERS | NODE {source} | {workers.Length} own workers\nSelect a worker for detailed management.";
        var queues = host.PersistentOrdersEnabled
            ? host.GetHumanPersistentQueues(_humanCivilization).ToDictionary(q => q.Worker) : null;
        foreach (var worker in workers)
        {
            var q = queues?.GetValueOrDefault(worker.Id);
            string activity = worker.Activity == PhysicalWorkerActivity.Producing
                ? (worker.Occupation switch {
                    PhysicalWorkerOccupation.Agriculture => $"Producing | {CityWorkerClosedCrop(host, worker) ?? "Agriculture"}",
                    PhysicalWorkerOccupation.Forestry when host.ExperimentalLocalEconomyEnabled => "Producing wood",
                    PhysicalWorkerOccupation.Mining when host.ExperimentalLocalEconomyEnabled => "Producing stone",
                    _ => $"Producing | {worker.Occupation}" })
                : worker.Activity.ToString();
            string preference = q is null ? "Not configured" : DescribeWorkerBase(host, q);
            string orders = q is null ? "Detailed planning unavailable" : $"Orders: {q.Orders.Count} | Base: {preference}";
            var button = new Button { Name = $"GANextCityWorker{worker.Id.Value}",
                Text = $"Worker #{worker.Id.Value} | {activity}\n{orders}",
                Alignment = HorizontalAlignment.Left, Disabled = !host.PersistentOrdersEnabled };
            var id = worker.Id;
            button.Pressed += () => OpenCityWorker(source, id);
            _cityWorkersList.AddChild(button);
        }
    }

    private void OpenCityWorker(ulong source, PhysicalWorkerId worker)
    {
        var host = _tacticalHost;
        if (host is null || !host.PersistentOrdersEnabled ||
            _cityWindowLayer?.Visible != true || _cityWindowPanel?.Visible != true ||
            _cityConnectionSource != source ||
            !host.Snapshot.Workers.Workers.Any(w => w.Id == worker && w.Owner == _humanCivilization &&
                w.TacticalTileId?.FineStrategicCellId.Value == source)) return;
        if (_r224Window?.Visible == true) CloseR224WorkerWindow();
        _cityWorkerOrigin = source;
        _cityWindowLayer.Visible = false;
        OnOperationalWorkerSelected(worker.Value);
        OpenR224WorkerWindow();
        if (_r224Window?.Visible != true) ReturnFromCityWorker();
    }

    private void ReturnFromCityWorker()
    {
        if (_cityWorkerOrigin is not { } source) return;
        _cityWorkerOrigin = null;
        // Do not reopen a stale city when the map has moved to another site.
        if (_selectedCartographicSite != source ||
            _tacticalPreview?.LocalSiteMarkerTileIds.Contains(source) != true) return;
        OpenSelectedCityWindow();
        if (_cityWorkersTabs is not null) _cityWorkersTabs.CurrentTab = 3;
    }

    private void ExerciseOpenCityWorker(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId worker,
        PhysicalTacticalTileId node)
    {
        var hash = host.StateHash;
        FocusCityProductionSmokeSite(host, node);
        _cityWorkersTabs!.CurrentTab = 3;
        AssertCityWorkerList(host, node);
        if (_r224Open?.IsVisibleInTree() == true)
            throw new InvalidOperationException("Permanent worker launcher is visible in local economy mode.");
        var button = _cityWorkersList!.GetChildren().OfType<Button>().Single(x => x.Name.ToString() == $"GANextCityWorker{worker.Value}");
        button.EmitSignal(Button.SignalName.Pressed);
        if (_r224Window?.Visible != true || _cityWindowLayer?.Visible == true ||
            _r224Identity?.Text != $"Worker #{worker.Value} | Node {node.FineStrategicCellId.Value}" ||
            host.StateHash != hash)
            throw new InvalidOperationException("City worker did not open the correct private planner.");
    }

    private void AssertCityWorkerList(FoundationTacticalHumanAiTurnHost host, PhysicalTacticalTileId node)
    {
        var expected = host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization &&
            w.TacticalTileId == node).OrderBy(w => w.Id.Value)
            .Select(w => $"GANextCityWorker{w.Id.Value}").ToArray();
        var shown = _cityWorkersList!.GetChildren().OfType<Button>().Select(b => b.Name.ToString()).ToArray();
        if (_cityWorkersTab?.IsVisibleInTree() != true || !shown.SequenceEqual(expected))
            throw new InvalidOperationException("Worker list does not match own physical residents.");
    }

    private int ExerciseCityWorkerClosedCropRows(FoundationTacticalHumanAiTurnHost host, FoundationTacticalCollectiveReceipt receipt)
    {
        var hash = host.StateHash;
        var timeline = receipt.PersistentTimeline ?? throw new InvalidOperationException("Missing crop evidence timeline.");
        int count = 0;
        foreach (var worker in host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization &&
            w.Activity == PhysicalWorkerActivity.Producing && w.Occupation == PhysicalWorkerOccupation.Agriculture && w.TacticalTileId.HasValue))
        {
            var last = timeline.Slices.Where(s => s.Worker == worker.Id && s.DurationMicro > 0).OrderBy(s => s.StartMicro).LastOrDefault();
            if (last is null || last.Node != worker.TacticalTileId || last.Activity != PhysicalWorkerActivity.Producing ||
                last.Occupation != PhysicalWorkerOccupation.Agriculture || last.Crop is not { } crop) continue;
            FocusCityProductionSmokeSite(host, worker.TacticalTileId!.Value);
            _cityWorkersTabs!.CurrentTab = 3;
            AssertCityWorkerList(host, worker.TacticalTileId.Value);
            var row = _cityWorkersList!.GetChildren().OfType<Button>().Single(b => b.Name.ToString() == $"GANextCityWorker{worker.Id.Value}");
            if (!row.Text.StartsWith($"Worker #{worker.Id.Value} | Producing | {(CropId)crop}\n", StringComparison.Ordinal))
                throw new InvalidOperationException("Worker crop row differs from committed calendar.");
            _cityWindowLayer!.Visible = false;
            count++;
        }
        if (hash != host.StateHash) throw new InvalidOperationException("Worker crop UI changed canonical state.");
        return count;
    }

    private void ExerciseCityWorkersPrivacy(FoundationTacticalHumanAiTurnHost host, PhysicalTacticalTileId node)
    {
        var hash = host.StateHash;
        var foreign = host.Snapshot.Workers.Workers.First(w => w.Owner != _humanCivilization &&
            w.TacticalTileId is { } tile && !host.Snapshot.Workers.Workers.Any(own =>
                own.Owner == _humanCivilization && own.TacticalTileId == tile));
        FocusCityProductionSmokeSite(host, foreign.TacticalTileId!.Value);
        _cityWorkersTabs!.CurrentTab = 3;
        AssertCityWorkerList(host, foreign.TacticalTileId.Value);
        if (_cityWorkersStatus?.Text != "No own workers at this node.")
            throw new InvalidOperationException("Foreign worker list not empty.");
        OpenCityWorker(foreign.TacticalTileId.Value.FineStrategicCellId.Value, foreign.Id);
        if (_r224Window?.Visible == true) throw new InvalidOperationException("Foreign worker planner opened.");
        var own = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization && w.TacticalTileId == node);
        ExerciseOpenCityWorker(host, own.Id, node);
        CloseR224WorkerWindow();
        AssertCityWorkerList(host, node);
        if (_cityWindowLayer?.Visible != true || _cityWorkersTabs.CurrentTab != 3 ||
            _r224Window?.Visible == true || host.StateHash != hash)
            throw new InvalidOperationException("Worker close did not restore city list without mutation.");
        _cityWindowLayer.Visible = false;
    }
}
