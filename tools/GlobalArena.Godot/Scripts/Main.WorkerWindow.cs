using Godot;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.World;
using GlobalArena.Simulation;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    private Button? _r224Open, _r224Apply;
    private Control? _r224Window;
    private GaFloatingWindow? _r224Floating;
    private VBoxContainer? _r224Body;
    private Node? _r224QueueHome;
    private int _r224QueueIndex;
    private Label? _r224Identity, _r224BaseReadout, _r224Status;
    private OptionButton? _r224Activity;
    private ulong? _r224Worker;
    private VBoxContainer? _r225DetailParking;
    private readonly List<Control> _r225DetailControls = new();
    private bool R225CompactWorkerDock => CompactOperationalDock && _tacticalHost?.PersistentOrdersEnabled == true;

    private void ConfigureR225WorkerDock(VBoxContainer orderPanel)
    {
        if (!R225CompactWorkerDock || _r225DetailParking is not null) return;
        _r225DetailParking = new VBoxContainer { Name = "GANextWorkerDetailParking", Visible = false };
        orderPanel.AddChild(_r225DetailParking);
        foreach (var control in new Control?[] { _cropSelector, _localProduceButton,
            _localAgricultureButton, _localForestryButton, _localMiningButton,
            _r149LandWorkButton, _r149PortWorkButton, _persistentQueuePanel })
        {
            if (control is null) continue;
            _r225DetailControls.Add(control);
            control.Reparent(_r225DetailParking);
        }
        if (_r224Open is not null && _selectedUnitSlot is not null)
            _r224Open.Reparent(_selectedUnitSlot);
    }

    private void CreateR224WorkerWindowLauncher(VBoxContainer parent)
    {
        _r224Open = new Button { Name = "GANextOpenWorkerWindow", Text = "Open worker window", Visible = false };
        _r224Open.Pressed += OpenR224WorkerWindow;
        parent.AddChild(_r224Open);
    }

    private void OpenR224WorkerWindow()
    {
        if (_tacticalHost?.PersistentOrdersEnabled != true || _selectedLocalWorkerId is null ||
            _persistentQueuePanel is null) return;
        if (_r224Window is null)
        {
            _r224Window = new Control { Name = "GANextWorkerWindow", MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_r224Window);
            _r224Window.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _r224Floating = new GaFloatingWindow {
                Name = "GANextWorkerPanel", Position = new Vector2(32, 72),
                Size = new Vector2(400, 540), MinimumFloatingSize = new Vector2(310, 320),
                MouseFilter = MouseFilterEnum.Stop
            };
            _r224Window.AddChild(_r224Floating);
            _r224Floating.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
            var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            _r224Floating.AddChild(column);
            var title = GaVisualFoundation.AddWindowHeader(column, "GA / WORKER", "Worker planning",
                "Private planning | E to submit", CloseR224WorkerWindow);
            _r224Floating.AttachTitleBar(title);
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            column.AddChild(scroll);
            _r224Body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            scroll.AddChild(_r224Body);
            _r224Identity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _r224Body.AddChild(_r224Identity);
            _r224Body.AddChild(new Label { Text = "BASE ACTIVITY" });
            _r224BaseReadout = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _r224Body.AddChild(_r224BaseReadout);
            _r224Activity = new OptionButton { Name = "GANextBaseActivity", FitToLongestItem = false };
            _r224Body.AddChild(_r224Activity);
            _r224Apply = new Button { Name = "GANextApplyBaseActivity", Text = "Set manual base activity" };
            _r224Apply.Pressed += StageR224BaseActivity;
            _r224Body.AddChild(_r224Apply);
            _r224Body.AddChild(new Label { Text = "Automatic allocation: not available" });
            _r224Status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _r224Body.AddChild(_r224Status);
            _r224Body.AddChild(new HSeparator());
            _r224Body.AddChild(new Label { Text = "TASK QUEUE | Add tasks using map and dock controls" });
        }
        if (_r225DetailParking is not null)
        {
            foreach (var control in _r225DetailControls)
                if (control.GetParent() != _r224Body) control.Reparent(_r224Body!);
        }
        else if (_persistentQueuePanel.GetParent() != _r224Body)
        {
            _r224QueueHome = _persistentQueuePanel.GetParent();
            _r224QueueIndex = _persistentQueuePanel.GetIndex();
            _persistentQueuePanel.Reparent(_r224Body!);
        }
        _r224Window.Visible = true;
        _r224Floating!.ClampIntoParent();
        _r224Worker = null;
        RefreshR224WorkerWindow();
    }

    private void CloseR224WorkerWindow()
    {
        if (_r224Window is null) return;
        _r224Window.Visible = false;
        if (_r225DetailParking is not null)
        {
            foreach (var control in _r225DetailControls)
                if (control.GetParent() == _r224Body) control.Reparent(_r225DetailParking);
        }
        else if (_persistentQueuePanel is not null && _r224QueueHome is not null &&
            _persistentQueuePanel.GetParent() == _r224Body)
        {
            _persistentQueuePanel.Reparent(_r224QueueHome);
            _r224QueueHome.MoveChild(_persistentQueuePanel, _r224QueueIndex);
        }
        _r224Worker = null;
    }

    private void RefreshR224WorkerWindow()
    {
        var host = _tacticalHost;
        var worker = host?.Snapshot.Workers.Workers.FirstOrDefault(w =>
            w.Owner == _humanCivilization && w.Id.Value == _selectedLocalWorkerId);
        if (_r224Open is not null)
        {
            _r224Open.Visible = host?.PersistentOrdersEnabled == true;
            _r224Open.Disabled = worker is null;
        }
        if (_r224Window?.Visible != true) return;
        if (host is null || !host.PersistentOrdersEnabled || worker is null)
        {
            CloseR224WorkerWindow();
            return;
        }
        var q = ReadPersistentQueue(worker.Id);
        var committed = host.Snapshot.WorkerOrders.Queues.Single(x => x.Worker == worker.Id);
        _r224Identity!.Text = $"Worker #{worker.Id.Value} | Node {worker.TacticalTileId?.FineStrategicCellId.Value}";
        _r224BaseReadout!.Text = "Base: " + (q.BaseActivity?.Crop is { } crop ? $"Grow {(CropId)crop}" :
            q.BaseActivity?.Occupation?.ToString() ?? "Not configured") +
            (!Equals(q.BaseActivity, committed.BaseActivity) ? " | Pending submission" : " | Committed");
        if (_r224Worker != worker.Id.Value)
        {
            _r224Worker = worker.Id.Value;
            _r224Activity!.Clear();
            if (host.AgriculturalProduction is not null)
                foreach (var item in CropCatalog.All)
                    _r224Activity.AddItem($"Grow {item.Id}", (int)item.Id);
            _r224Activity.AddItem("Forestry", 101);
            _r224Activity.AddItem("Mining", 102);
            _r224Activity.AddItem("Land transport", 103);
            _r224Activity.AddItem("Port operations", 104);
            int selected = q.BaseActivity?.Crop is { } current ? current : q.BaseActivity?.Occupation switch
            {
                PhysicalWorkerOccupation.Forestry => 101,
                PhysicalWorkerOccupation.Mining => 102,
                PhysicalWorkerOccupation.LandTransport => 103,
                PhysicalWorkerOccupation.PortOperations => 104,
                _ => -1
            };
            for (int i = 0; i < _r224Activity.ItemCount; i++)
                if (_r224Activity.GetItemId(i) == selected) _r224Activity.Select(i);
            _r224Status!.Text = "";
        }
        bool ready = host.ReadyCivilizations.Contains(_humanCivilization);
        _r224Activity!.Disabled = _r224Apply!.Disabled = ready;
    }

    private void StageR224BaseActivity()
    {
        try
        {
            var host = _tacticalHost ?? throw new InvalidOperationException("No active host.");
            if (_r224Worker is not { } id || id != _selectedLocalWorkerId || _r224Activity!.Selected < 0) return;
            int selection = _r224Activity.GetSelectedId();
            var occupation = selection switch
            {
                101 => PhysicalWorkerOccupation.Forestry,
                102 => PhysicalWorkerOccupation.Mining,
                103 => PhysicalWorkerOccupation.LandTransport,
                104 => PhysicalWorkerOccupation.PortOperations,
                _ => PhysicalWorkerOccupation.Agriculture
            };
            host.QueueHumanBaseActivity(_humanCivilization, new PhysicalWorkerId(id),
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, occupation,
                    occupation == PhysicalWorkerOccupation.Agriculture ? (byte?)selection : null));
            _r224Status!.Text = "Base activity staged privately.";
            RefreshFineTacticalWorkerControls();
        }
        catch (Exception ex) { _r224Status!.Text = "Base activity rejected: " + ex.Message; }
    }

    private void ExerciseR224WorkerWindow(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId worker)
    {
        var before = host.StateHash;
        var home = _persistentQueuePanel!.GetParent();
        _r224Open!.EmitSignal(Button.SignalName.Pressed);
        if (_r224Window?.Visible != true || _persistentQueuePanel.GetParent() != _r224Body ||
            _r224Identity?.Text.Contains($"Worker #{worker.Value} |") != true)
            throw new InvalidOperationException("R224 worker window failed to open with existing queue.");
        for (int i = 0; i < _r224Activity!.ItemCount; i++)
            if (_r224Activity.GetItemId(i) == (int)CropId.Rice) _r224Activity.Select(i);
        _r224Apply!.EmitSignal(Button.SignalName.Pressed);
        if (ReadPersistentQueue(worker).BaseActivity?.Crop != (byte)CropId.Rice ||
            host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker).BaseActivity is not null ||
            host.StateHash != before || _r224BaseReadout?.Text.Contains("Pending submission") != true)
            throw new InvalidOperationException("R224 base command did not remain private.");
        var other = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization && w.Id != worker);
        OnOperationalWorkerSelected(other.Id.Value);
        if (_r224Identity?.Text.Contains($"Worker #{other.Id.Value} |") != true ||
            ReadPersistentQueue(other.Id).BaseActivity is not null || host.StateHash != before)
            throw new InvalidOperationException("R224 worker selection leaked base preference.");
        OnOperationalWorkerSelected(worker.Value);
        CloseR224WorkerWindow();
        if (_r224Window.Visible || _persistentQueuePanel.GetParent() != home || host.StateHash != before)
            throw new InvalidOperationException("R224 close did not restore existing queue.");
    }
}
