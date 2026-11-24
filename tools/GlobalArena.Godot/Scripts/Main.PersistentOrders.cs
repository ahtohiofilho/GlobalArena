using Godot;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

public partial class Main
{
    private VBoxContainer? _persistentQueuePanel;
    private ItemList? _persistentQueueList;
    private Button? _persistentUp, _persistentDown, _persistentRemove;
    private Button? _persistentInsert;
    private Label? _persistentWorkerSummary, _persistentCrossingLabel, _persistentEmpty;
    private ProgressBar? _persistentCrossingProgress;
    private ulong? _persistentSelectionWorker;
    private ulong? _persistentSelectedOrder;
    private int _persistentInsertionIndex = -1;

    private FoundationWorkerOrderQueue ReadPersistentQueue(PhysicalWorkerId worker) =>
        _tacticalHost!.GetHumanPersistentQueues(_humanCivilization).Single(q => q.Worker == worker);

    private void AppendPersistentOrder(PhysicalWorkerId worker, FoundationWorkerOrderKind kind,
        PhysicalTacticalTileId? destination = null, ulong? project = null, PhysicalWorkerOccupation? occupation = null)
    {
        var q = ReadPersistentQueue(worker);
        var index = _persistentInsertionIndex < 0 ? q.Orders.Count : Math.Clamp(_persistentInsertionIndex, q.ActiveOrderId.HasValue ? 1 : 0, q.Orders.Count);
        _tacticalHost!.QueueHumanPersistentEdit(_humanCivilization, new FoundationPersistentOrderEdit(
            _humanCivilization, worker, FoundationPersistentOrderEditKind.Insert, Index: index,
            OrderKind: kind, Destination: destination, ProjectId: project, Occupation: occupation));
        _persistentInsertionIndex = -1;
    }

    private bool RemovePersistentHead(PhysicalWorkerId worker)
    {
        var q = ReadPersistentQueue(worker);
        var order = q.Orders.FirstOrDefault();
        if (order is null) return false;
        RemovePersistentOrder(q, order.Id);
        return true;
    }

    private void RemovePersistentOrder(FoundationWorkerOrderQueue q, ulong id)
    {
        _tacticalHost!.QueueHumanPersistentEdit(_humanCivilization, new FoundationPersistentOrderEdit(
            _humanCivilization, q.Worker, q.ActiveOrderId == id ? FoundationPersistentOrderEditKind.Interrupt
                : FoundationPersistentOrderEditKind.Remove, OrderId: id));
    }

    private void CreatePersistentWorkerControls(VBoxContainer parent)
    {
        _persistentQueuePanel = new VBoxContainer { Name = "GANextPersistentQueue", Visible = false };
        parent.AddChild(_persistentQueuePanel);
        _persistentWorkerSummary = new Label { Name = "GANextWorkerContext", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _persistentQueuePanel.AddChild(_persistentWorkerSummary);
        _persistentCrossingLabel = new Label { Name = "GANextCrossingDetails", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _persistentQueuePanel.AddChild(_persistentCrossingLabel);
        _persistentCrossingProgress = new ProgressBar { Name = "GANextCrossingProgress", MinValue = 0, MaxValue = 100, Step = 0, ShowPercentage = false, Visible = false };
        _persistentQueuePanel.AddChild(_persistentCrossingProgress);
        _persistentQueuePanel.AddChild(new Label { Text = "ORDER QUEUE" });
        _persistentEmpty = new Label { Text = "No orders" };
        _persistentQueuePanel.AddChild(_persistentEmpty);
        _persistentQueueList = new ItemList { CustomMinimumSize = new Vector2(0, 92), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _persistentQueueList.ItemSelected += index =>
        {
            if (_selectedLocalWorkerId is not { } worker) return;
            var q = ReadPersistentQueue(new PhysicalWorkerId(worker));
            if (index >= 0 && index < q.Orders.Count)
                _persistentSelectedOrder = q.Orders[(int)index].Id;
            RefreshPersistentQueueButtons(q);
        };
        _persistentQueuePanel.AddChild(_persistentQueueList);
        var row = new HBoxContainer();
        _persistentQueuePanel.AddChild(row);
        _persistentUp = new Button { Text = "Up" };
        _persistentDown = new Button { Text = "Down" };
        _persistentRemove = new Button { Text = "Remove" };
        row.AddChild(_persistentUp); row.AddChild(_persistentDown); row.AddChild(_persistentRemove);
        _persistentUp.Pressed += () => EditPersistentSelected(-1);
        _persistentDown.Pressed += () => EditPersistentSelected(1);
        _persistentRemove.Pressed += () => EditPersistentSelected(0);
        var insert = _persistentInsert = new Button { Text = "Insert before selected", Disabled = true };
        insert.Pressed += () =>
        {
            if (insert.Disabled || _selectedLocalWorkerId is not { } worker) return;
            var q = ReadPersistentQueue(new PhysicalWorkerId(worker));
            _persistentInsertionIndex = q.Orders.ToList().FindIndex(o => o.Id == _persistentSelectedOrder);
            if (_localOrderStatus is not null) _localOrderStatus.Text = "Choose the next order to insert.";
        };
        _persistentQueuePanel.AddChild(insert);
    }

    private void EditPersistentSelected(int delta)
    {
        try
        {
            if (_selectedLocalWorkerId is not { } worker || _persistentSelectedOrder is not { } id) return;
            var q = ReadPersistentQueue(new PhysicalWorkerId(worker));
            int i = q.Orders.ToList().FindIndex(o => o.Id == id);
            if (i < 0) return;
            if (delta == 0) RemovePersistentOrder(q, id);
            else _tacticalHost!.QueueHumanPersistentEdit(_humanCivilization, new FoundationPersistentOrderEdit(
                _humanCivilization, q.Worker, FoundationPersistentOrderEditKind.Reorder, OrderId: id, Index: i + delta));
        }
        catch (Exception ex) { if (_localOrderStatus is not null) _localOrderStatus.Text = "Order rejected: " + ex.Message; }
        RefreshFineTacticalWorkerControls();
    }

    private void RefreshPersistentQueueButtons(FoundationWorkerOrderQueue q)
    {
        if (_persistentUp is null || _persistentDown is null || _persistentRemove is null) return;
        int i = q.Orders.ToList().FindIndex(o => o.Id == _persistentSelectedOrder);
        bool ready = _tacticalHost!.ReadyCivilizations.Contains(_humanCivilization);
        bool active = i >= 0 && q.ActiveOrderId == q.Orders[i].Id;
        _persistentUp.Disabled = ready || active || i <= (q.ActiveOrderId.HasValue ? 1 : 0);
        _persistentDown.Disabled = ready || active || i < 0 || i >= q.Orders.Count - 1;
        _persistentRemove.Disabled = ready || i < 0;
        _persistentRemove.Text = active ? "Interrupt" : "Remove";
        if (_persistentInsert is not null) _persistentInsert.Disabled = ready || active || i < 0;
    }

    private void RefreshPersistentWorkerControls()
    {
        if (_persistentQueuePanel is null || _persistentQueueList is null) return;
        var host = _tacticalHost;
        _persistentQueuePanel.Visible = host?.PersistentOrdersEnabled == true;
        if (!_persistentQueuePanel.Visible || host is null || _selectedLocalWorkerId is not { } worker) return;
        var q = ReadPersistentQueue(new PhysicalWorkerId(worker));
        bool ready = host.ReadyCivilizations.Contains(_humanCivilization);
        if (_persistentSelectionWorker != worker)
        {
            _persistentSelectionWorker = worker;
            _persistentSelectedOrder = null;
            _persistentInsertionIndex = -1;
        }
        RefreshPersistentWorkerSummary(host, q.Worker);
        _persistentQueueList.Clear();
        _persistentQueueList.Visible = q.Orders.Count > 0;
        if (_persistentEmpty is not null) _persistentEmpty.Visible = q.Orders.Count == 0;
        foreach (var order in q.Orders)
        {
            string name = order.Kind switch
            {
                FoundationWorkerOrderKind.Move => $"Move to {order.Destination!.Value.FineStrategicCellId.Value}",
                FoundationWorkerOrderKind.Build => $"Build #{order.ProjectId}",
                _ => order.Occupation.ToString() ?? "Occupation"
            };
            _persistentQueueList.AddItem((q.ActiveOrderId == order.Id ? "Active | " : "") + name);
        }
        int selected = q.Orders.ToList().FindIndex(o => o.Id == _persistentSelectedOrder);
        if (selected >= 0) _persistentQueueList.Select(selected);
        else { _persistentSelectedOrder = null; _persistentInsertionIndex = -1; }
        RefreshPersistentQueueButtons(q);
        if (_localPendingLabel is not null)
        {
            var committed = host.Snapshot.WorkerOrders.Queues.Single(x => x.Worker == q.Worker);
            bool changed = !committed.Orders.SequenceEqual(q.Orders) || committed.ActiveOrderId != q.ActiveOrderId;
            _localPendingLabel.Text = changed ? "Pending changes | E to submit" :
                q.Orders.Count == 0 ? "No scheduled orders" : $"{q.Orders.Count} scheduled | {(q.ActiveOrderId.HasValue ? "Active" : "Waiting")}";
        }
        if (_localMoveButton is not null)
        {
            _localMoveButton.Text = "G | Move to selected node";
            _localMoveButton.Disabled = ready || _tacticalPreview?.SelectedCanonicalPhysicalTileId is null ||
                _tacticalPreview.SelectedCanonicalPhysicalTileId == host.Snapshot.Workers.Workers.Single(w => w.Id == q.Worker).TacticalTileId!.Value.FineStrategicCellId.Value;
        }
        if (_localProduceButton is not null) _localProduceButton.Disabled = ready;
        if (_localCancelQueuedButton is not null)
        {
            _localCancelQueuedButton.Text = "X | Remove / interrupt head";
            _localCancelQueuedButton.Disabled = ready || q.Orders.Count == 0;
        }
        foreach (var b in new[] { _localAgricultureButton, _localForestryButton, _localMiningButton, _r149LandWorkButton, _r149PortWorkButton })
            if (b is not null) b.Disabled = ready;
        if (_fineMovementDecision is not null) _fineMovementDecision.Text = "Select a destination on the map. Orders run in sequence.";
    }

    private void RefreshPersistentWorkerSummary(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId id)
    {
        var person = host.Snapshot.Workers.Workers.Single(w => w.Id == id);
        var committed = host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == id);
        var active = committed.Orders.FirstOrDefault(o => o.Id == committed.ActiveOrderId);
        var travel = host.Snapshot.EdgeTravels.Pending.SingleOrDefault(p => p.Worker == id);
        string activity = travel is not null ? "Traveling" :
            person.Activity == PhysicalWorkerActivity.Producing ? person.Occupation.ToString() ?? "Producing" :
            active?.Kind == FoundationWorkerOrderKind.Build ? "Construction order active" :
            person.Occupation is PhysicalWorkerOccupation.LandTransport or PhysicalWorkerOccupation.PortOperations ? person.Occupation.ToString()! :
            person.Activity == PhysicalWorkerActivity.Other ? "Idle" : person.Activity.ToString();
        string target = active?.Kind == FoundationWorkerOrderKind.Move ? $" | Target {active.Destination!.Value.FineStrategicCellId.Value}" :
            active?.Kind == FoundationWorkerOrderKind.Build ? $" | Build #{active.ProjectId}" : "";
        if (_persistentWorkerSummary is not null)
            _persistentWorkerSummary.Text = $"{activity} | Node {person.TacticalTileId!.Value.FineStrategicCellId.Value}{target}";
        if (_persistentCrossingLabel is null || _persistentCrossingProgress is null) return;
        _persistentCrossingLabel.Visible = _persistentCrossingProgress.Visible = travel is not null;
        if (travel is null)
        {
            _persistentCrossingLabel.Text = "";
            _persistentCrossingProgress.Value = 0;
            return;
        }
        var percent = 100m * (travel.OriginalDurationMicro - travel.RemainingMicro) / travel.OriginalDurationMicro;
        _persistentCrossingProgress.Value = (double)percent;
        _persistentCrossingLabel.Text = FormattableString.Invariant(
            $"Crossing {travel.Source.FineStrategicCellId.Value} → {travel.Destination.FineStrategicCellId.Value} | {percent:F1}%\nRemaining: {travel.RemainingMicro / 1_000_000m:0.######} turns");
    }

    internal async Task ExerciseR213PersistentInterfaceSmoke()
    {
        try
        {
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var host = _tacticalHost ?? throw new InvalidOperationException("Missing host.");
            if (!host.PersistentOrdersEnabled) throw new InvalidOperationException("Missing persistent preview.");
            var before = host.StateHash;
            var worker = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization);
            OnOperationalWorkerSelected(worker.Id.Value);
            if (_persistentQueueList!.Visible || _persistentInsert!.Disabled != true ||
                _persistentCrossingProgress!.Visible || _persistentCrossingLabel!.Visible)
                throw new InvalidOperationException("Empty queue or stationary controls are incorrect.");
            _localAgricultureButton!.EmitSignal(Button.SignalName.Pressed);
            _localMiningButton!.EmitSignal(Button.SignalName.Pressed);
            var q = ReadPersistentQueue(worker.Id);
            if (q.Orders.Count != 2 || _persistentQueueList!.ItemCount != 2 || host.StateHash != before)
                throw new InvalidOperationException("Queue controls did not stage two real orders.");
            _persistentQueueList.EmitSignal(ItemList.SignalName.ItemSelected, 1L);
            if (_persistentInsert!.Disabled) throw new InvalidOperationException("Valid insertion unavailable.");
            var other = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization && w.Id != worker.Id);
            OnOperationalWorkerSelected(other.Id.Value);
            if (!_persistentInsert.Disabled || _persistentSelectedOrder.HasValue)
                throw new InvalidOperationException("Selection leaked between workers.");
            OnOperationalWorkerSelected(worker.Id.Value);
            _persistentQueueList.EmitSignal(ItemList.SignalName.ItemSelected, 1L);
            _persistentUp!.EmitSignal(Button.SignalName.Pressed);
            if (ReadPersistentQueue(worker.Id).Orders[0].Occupation != PhysicalWorkerOccupation.Mining)
                throw new InvalidOperationException("Reorder signal failed.");
            _persistentRemove!.EmitSignal(Button.SignalName.Pressed);
            if (ReadPersistentQueue(worker.Id).Orders.Count != 1)
                throw new InvalidOperationException("Remove signal failed.");
            host.ClearHumanPersistentPlan(_humanCivilization);
            RefreshFineTacticalWorkerControls();
            // Earn real treasury funds through the ordinary E action.
            EndFineCollectiveTurn();
            if (host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != 1)
                throw new InvalidOperationException("Initial economic turn did not close.");
            var site = _tacticalPreview!.LocalSiteMarkerTileIds.First();
            OnCartographicLocalSiteSelected(site);
            OpenSelectedCityWindow();
            var tile = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, new StrategicCellId(site));
            var links = FoundationLocalConnectionsProjector.Read(host.Snapshot, host.Incidence, tile);
            var link = links.First(l => {
                var edge = ResolveConnectionEdge(site, l.Neighbor.FineStrategicCellId.Value);
                var physical = host.Snapshot.Transport.Edges.Single(e => e.Edge == edge);
                return physical.NaturalConstructionCostMoneyMicro is > 0UL && physical.CargoMicroAtReferenceLoad != ulong.MaxValue;
            });
            ShowCityConnectionDetail(link);
            _connectionAmount!.Text = "0.100000";
            RefreshConnectionInvestmentControls();
            var check = _persistentFundingWorkers!.GetChildren().OfType<CheckBox>().First();
            check.ButtonPressed = true;
            var fundedBefore = host.StateHash;
            if (_connectionInvest!.Disabled) throw new InvalidOperationException("Funding control unavailable.");
            _connectionInvest.EmitSignal(Button.SignalName.Pressed);
            var funding = host.GetHumanPersistentFunding(_humanCivilization).Single();
            if (funding.Assignments.Count != 1 || funding.MoneyMicro != 100_000UL || host.StateHash != fundedBefore)
                throw new InvalidOperationException("Funding and workforce were not staged atomically.");
            _connectionCancel!.EmitSignal(Button.SignalName.Pressed);
            if (host.GetHumanPersistentFunding(_humanCivilization).Count != 0 || host.StateHash != fundedBefore)
                throw new InvalidOperationException("Funding cancel signal failed.");
            // Start a genuine natural ground crossing from the same signed geography.
            _cityWindowLayer!.Visible = false;
            OnOperationalWorkerSelected(worker.Id.Value);
            var source = host.Snapshot.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId!.Value;
            var fine = _r123FineGeography!;
            var destination = host.Incidence.FineTopology.Cells[(int)source.FineStrategicCellId.Value - 1]
                .AdjacentCellIds.First(c => fine.CrossingAtIndices((int)source.FineStrategicCellId.Value - 1,
                    (int)c.Value - 1) == FoundationFineTransportCrossingKind.Ground);
            AppendPersistentOrder(worker.Id, FoundationWorkerOrderKind.Move,
                destination: new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, destination));
            RefreshFineTacticalWorkerControls();
            if (_persistentCrossingProgress!.Visible || _localPendingLabel!.Text != "Pending changes | E to submit")
                throw new InvalidOperationException("Staged journey presented as committed crossing.");
            EndFineCollectiveTurn();
            var travel = host.Snapshot.EdgeTravels.Pending.Single(p => p.Worker == worker.Id);
            var expected = 100m * (travel.OriginalDurationMicro - travel.RemainingMicro) / travel.OriginalDurationMicro;
            var travelHash = host.StateHash;
            if (!_persistentCrossingProgress.Visible || !_persistentCrossingLabel!.Visible ||
                Math.Abs(_persistentCrossingProgress.Value - (double)expected) > 0.00001 ||
                !_persistentCrossingLabel.Text.Contains(FormattableString.Invariant($"Remaining: {travel.RemainingMicro / 1_000_000m:0.######} turns")) ||
                !_persistentWorkerSummary!.Text.StartsWith("Traveling", StringComparison.Ordinal))
                throw new InvalidOperationException($"Crossing readout mismatch: expected={expected}, bar={_persistentCrossingProgress.Value}, visible={_persistentCrossingProgress.Visible}, label={_persistentCrossingLabel?.Text}, summary={_persistentWorkerSummary?.Text}");
            OnOperationalWorkerSelected(other.Id.Value);
            if (_persistentCrossingProgress.Visible) throw new InvalidOperationException("Crossing leaked to stationary worker.");
            OnOperationalWorkerSelected(worker.Id.Value);
            if (!_persistentCrossingProgress.Visible || host.StateHash != travelHash)
                throw new InvalidOperationException("Context selection mutated state.");
            EndFineCollectiveTurn();
            if (host.Snapshot.EdgeTravels.Pending.Any(p => p.Worker == worker.Id) ||
                _persistentCrossingProgress.Visible || _persistentCrossingLabel!.Visible ||
                ReadPersistentQueue(worker.Id).Orders.Count != 0)
                throw new InvalidOperationException("Completed crossing retained stale UI.");
            GD.Print("GA_NEXT_R216_WORKER_CONTEXT_PASS real_crossing=true exact_progress=true selection_isolated=true");
            GD.Print("GA_NEXT_R213_PERSISTENT_UI_PASS real_signals=true queue_edit=true funding_assignment=true committed_state_unchanged=true");
            GetTree().Quit(0);
        }
        catch (Exception ex) { GD.PrintErr("GA_NEXT_R213_PERSISTENT_UI_FAIL " + ex); GetTree().Quit(213); }
    }
}
