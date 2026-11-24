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
        _persistentQueuePanel.AddChild(new Label { Text = "ORDER QUEUE" });
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
        var insert = new Button { Text = "Insert before selected" };
        insert.Pressed += () =>
        {
            if (_selectedLocalWorkerId is not { } worker) return;
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
    }

    private void RefreshPersistentWorkerControls()
    {
        if (_persistentQueuePanel is null || _persistentQueueList is null) return;
        var host = _tacticalHost;
        _persistentQueuePanel.Visible = host?.PersistentOrdersEnabled == true;
        if (!_persistentQueuePanel.Visible || host is null || _selectedLocalWorkerId is not { } worker) return;
        var q = ReadPersistentQueue(new PhysicalWorkerId(worker));
        bool ready = host.ReadyCivilizations.Contains(_humanCivilization);
        _persistentQueueList.Clear();
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
        RefreshPersistentQueueButtons(q);
        if (_localPendingLabel is not null) _localPendingLabel.Text = $"{q.Orders.Count} orders | E to submit";
        if (_localMoveButton is not null)
        {
            _localMoveButton.Text = "G | Move to selected node";
            _localMoveButton.Disabled = ready || _tacticalPreview?.SelectedCanonicalPhysicalTileId is null;
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
}
