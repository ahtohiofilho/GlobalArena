using Godot;
using GlobalArena.Simulation;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

public partial class Main
{
    private VBoxContainer? _persistentQueuePanel;
    private OptionButton? _cropSelector;
    private Label? _foodQuality;
    private Label? _operationalReadout;
    private CropId? SelectedCrop => _cropSelector is { Selected: >= 0 } ? (CropId)_cropSelector.GetSelectedId() : null;
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
            OrderKind: kind, Destination: destination, ProjectId: project, Occupation: occupation,
            Crop: occupation == PhysicalWorkerOccupation.Agriculture && _tacticalHost.AgriculturalProduction is not null
                ? (byte?)(SelectedCrop ?? throw new InvalidOperationException("Select a crop first.")) : null));
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
        _foodQuality = new Label { Name = "GANextFoodQuality", Visible = false, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _persistentQueuePanel.AddChild(_foodQuality);
        _operationalReadout = new Label { Name = "GANextOperationalReadout", Visible = false, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _persistentQueuePanel.AddChild(_operationalReadout);
        _cropSelector = new OptionButton { Name = "GANextCropSelector", Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _persistentQueuePanel.AddChild(_cropSelector);
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
        RefreshCropSelector(host, q.Worker, ready);
        RefreshFoodQuality(host, q.Worker);
        RefreshOperationalReadout(host, q.Worker);
        _persistentQueueList.Clear();
        _persistentQueueList.Visible = q.Orders.Count > 0;
        if (_persistentEmpty is not null) _persistentEmpty.Visible = q.Orders.Count == 0;
        foreach (var order in q.Orders)
        {
            string name = order.Kind switch
            {
                FoundationWorkerOrderKind.Move => $"Move to {order.Destination!.Value.FineStrategicCellId.Value}",
                FoundationWorkerOrderKind.Build => $"Build #{order.ProjectId}",
                _ => order.Crop is { } crop ? $"Grow {(CropId)crop}" : order.Occupation.ToString() ?? "Occupation"
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

    private void RefreshOperationalReadout(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId worker)
    {
        if (_operationalReadout is null) return;
        _operationalReadout.Visible = host.OperationalFreight is not null;
        if (!_operationalReadout.Visible) return;
        var node = host.Snapshot.Workers.Workers.Single(w => w.Id == worker).TacticalTileId!.Value;
        var quotes = host.ReadOperationalQuotes(_humanCivilization, node);
        _operationalReadout.Text = quotes is null ? "Local logistics: no closed cycle" :
            quotes.Count == 0 ? "Local logistics: no service last cycle" :
            FormattableString.Invariant($"Local logistics: {quotes.Count} worked links | Last cycle");
        _operationalReadout.TooltipText = quotes is null ? "" : string.Join("\n", quotes.Select(q =>
            FormattableString.Invariant($"Link {q.Edge.FineStrategicEdgeId.Value}: {q.OriginalPhysicalCostMicroPerWholeCargoUnit} → {q.ShadowPhysicalCostMicroPerWholeCargoUnit} micro freight")));
    }

    private void RefreshFoodQuality(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId worker)
    {
        if (_foodQuality is null) return;
        var agriculture = host.AgriculturalProduction;
        _foodQuality.Visible = agriculture is not null;
        if (agriculture is null) return;
        var intake = host.ReadWorkerFoodIntake(_humanCivilization, worker);
        if (intake is null)
        {
            _foodQuality.Text = "Food quality (experimental): unavailable";
            return;
        }
        var conversions = agriculture.Commodities.ToDictionary(c => c, _ => 0.000001);
        var result = FoundationSettledFoodIntake.Evaluate(worker, intake, conversions,
            new CanonicalNutritionCandidateParameters(0.5, 0.5), f => 2.0 * f / (1.0 + f));
        _foodQuality.Text = FormattableString.Invariant($"Food quality (experimental): {result.CandidateQuality!.Value:0.000}");
        _foodQuality.TooltipText = FormattableString.Invariant($"Last closed cycle {host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal} | Intake {result.FoodPerPerson:0.000} | Diversity {result.Diversity:0.000} | No productivity effect");
    }

    private void RefreshCropSelector(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId worker, bool ready)
    {
        if (_cropSelector is null) return;
        var policy = host.AgriculturalProduction;
        _cropSelector.Visible = policy is not null;
        if (policy is null) return;
        var previous = SelectedCrop;
        var node = host.Snapshot.Workers.Workers.Single(w => w.Id == worker).TacticalTileId!.Value;
        _cropSelector.Clear();
        foreach (var crop in CropCatalog.All)
        {
            var output = policy.Resolve(node, crop.Id).WholeTurnOutput;
            _cropSelector.AddItem(FormattableString.Invariant($"{crop.Id} | {output / 1_000_000m:0.###}/turn"), (int)crop.Id);
        }
        _cropSelector.Select(previous.HasValue ? (int)previous.Value - 1 : 0);
        _cropSelector.Disabled = ready;
        if (_localAgricultureButton is not null) _localAgricultureButton.Text = "A | Grow selected crop";
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
        if (active?.Crop is { } crop && travel is null) activity = $"Growing {(CropId)crop}";
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

    private void ExerciseCropPreview(FoundationTacticalHumanAiTurnHost host)
    {
        var before = host.StateHash;
        var worker = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization);
        OnOperationalWorkerSelected(worker.Id.Value);
        if (_cropSelector?.Visible != true || _cropSelector.ItemCount != 6)
            throw new InvalidOperationException("Six crop controls unavailable.");
        if (_foodQuality?.Visible != true || !_foodQuality.Text.Contains("unavailable"))
            throw new InvalidOperationException("Food quality must be unavailable before first close.");
        _cropSelector.Select(3);
        _localAgricultureButton!.EmitSignal(Button.SignalName.Pressed);
        var order = ReadPersistentQueue(worker.Id).Orders.Single();
        if (order.Crop != (byte)CropId.Rice || host.StateHash != before ||
            !_persistentQueueList!.GetItemText(0).Contains("Rice"))
            throw new InvalidOperationException("Crop selection did not stage real rice intent.");
        EndFineCollectiveTurn();
        var queue = host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id);
        if (queue.Orders.Single().Crop != (byte)CropId.Rice ||
            queue.ActiveOrderId != order.Id || host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != 1 ||
            !_persistentWorkerSummary!.Text.Contains("Rice"))
            throw new InvalidOperationException("Crop preview did not commit persistent rice production.");
        var committedHash = host.StateHash;
        var intake = host.ReadWorkerFoodIntake(_humanCivilization, worker.Id);
        if (intake is null || intake.Count != 6) throw new InvalidOperationException("Missing six settled foods.");
        var expected = FoundationSettledFoodIntake.Evaluate(worker.Id, intake,
            host.AgriculturalProduction!.Commodities.ToDictionary(c => c, _ => 0.000001),
            new CanonicalNutritionCandidateParameters(0.5, 0.5), f => 2.0 * f / (1.0 + f));
        if (_foodQuality!.Text != FormattableString.Invariant($"Food quality (experimental): {expected.CandidateQuality!.Value:0.000}"))
            throw new InvalidOperationException("Food quality readout did not use the committed intake.");
        var foreign = host.Snapshot.Workers.Workers.First(w => w.Owner != _humanCivilization);
        bool rejected = false;
        try { host.ReadWorkerFoodIntake(_humanCivilization, foreign.Id); }
        catch (InvalidOperationException) { rejected = true; }
        if (!rejected || host.StateHash != committedHash)
            throw new InvalidOperationException("Food read boundary leaked foreign data or mutated world.");
        if (host.OperationalFreight is not null)
        {
            var operatorWorker = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization &&
                w.Id != worker.Id && host.Incidence.FineTopology.Cells[(int)w.TacticalTileId!.Value.FineStrategicCellId.Value - 1]
                    .AdjacentCellIds.Any(c => _r123FineGeography!.CrossingAtIndices(
                        (int)w.TacticalTileId.Value.FineStrategicCellId.Value - 1, (int)c.Value - 1) == FoundationFineTransportCrossingKind.Ground));
            OnOperationalWorkerSelected(operatorWorker.Id.Value);
            _r149LandWorkButton!.EmitSignal(Button.SignalName.Pressed);
            if (ReadPersistentQueue(operatorWorker.Id).Orders.Single().Occupation != PhysicalWorkerOccupation.LandTransport ||
                host.StateHash != committedHash)
                throw new InvalidOperationException("Operational button did not stage a real order.");
            EndFineCollectiveTurn();
            var node = host.Snapshot.Workers.Workers.Single(w => w.Id == operatorWorker.Id).TacticalTileId!.Value;
            var quotes = host.ReadOperationalQuotes(_humanCivilization, node);
            if (quotes is null || quotes.Count == 0 || quotes.Aggregate(0UL, (sum, q) => checked(sum + q.OperationalServiceMicro)) != 1_000_000UL ||
                quotes.Any(q => q.ShadowPhysicalCostMicroPerWholeCargoUnit >= q.OriginalPhysicalCostMicroPerWholeCargoUnit) ||
                !_operationalReadout!.Text.Contains("worked links"))
                throw new InvalidOperationException("Operational service did not affect closed freight quotes.");
            GD.Print("GA_NEXT_R219B_OPERATIONAL_UI_PASS real_order=true service_conserved=true freight_reduced=true");
        }
        GD.Print("GA_NEXT_R219A3_FOOD_READOUT_PASS committed=true owner_scoped=true hash_unchanged=true");
        GD.Print("GA_NEXT_R219A1_CROP_PREVIEW_PASS six_choices=true real_order=true economic_close=true");
    }

    internal async Task ExerciseR213PersistentInterfaceSmoke()
    {
        try
        {
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var host = _tacticalHost ?? throw new InvalidOperationException("Missing host.");
            if (!host.PersistentOrdersEnabled) throw new InvalidOperationException("Missing persistent preview.");
            var before = host.StateHash;
            if (host.AgriculturalProduction is not null)
            {
                ExerciseCropPreview(host);
                GetTree().Quit(0);
                return;
            }
            var hoverWorker = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization);
            var hoverSite = hoverWorker.TacticalTileId!.Value.FineStrategicCellId.Value;
            if (!R218Available()) throw new InvalidOperationException("Site hover unavailable in persistent preview.");
            _r218Anchor = _tacticalPreview!.GetGlobalRect().GetCenter();
            RenderR218SiteHover(hoverSite);
            var hoverScroll = _r218Rows!.GetChildren().OfType<ScrollContainer>().Single();
            var hoverList = hoverScroll.GetChild<VBoxContainer>(0);
            var hoverButton = hoverList.GetChildren().OfType<HBoxContainer>()
                .SelectMany(r => r.GetChildren().OfType<Button>()).Single(b => b.Name.ToString() == $"R218Worker{hoverWorker.Id.Value}");
            if (!_r218Hud!.Visible || host.StateHash != before)
                throw new InvalidOperationException("Site HUD mutated canonical state.");
            hoverButton.EmitSignal(Button.SignalName.Pressed);
            if (_r218Hud.Visible || _selectedLocalWorkerId != hoverWorker.Id.Value || host.StateHash != before)
                throw new InvalidOperationException("HUD selection failed or mutated state.");
            GD.Print("GA_NEXT_R218_SITE_HUD_PASS canonical_roster=true real_button=true hash_unchanged=true");
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
            var tabs = FindChild("GANextCityTabs", true, false) as TabContainer
                ?? throw new InvalidOperationException("Missing city tabs.");
            tabs.CurrentTab = 1;
            var diagram = _cityConnectionDiagram ?? throw new InvalidOperationException("Missing connection diagram.");
            var diagramHash = host.StateHash;
            if (!diagram.IsVisibleInTree() || diagram.BoundaryCount != links.Count ||
                !diagram.EdgeIds.OrderBy(x => x).SequenceEqual(links.Select(l => l.Edge.FineStrategicEdgeId.Value).OrderBy(x => x)))
                throw new InvalidOperationException("Diagram does not match actual local topology.");
            foreach (var candidate in links)
            {
                diagram.EdgeButton(candidate.Edge.FineStrategicEdgeId.Value).EmitSignal(Button.SignalName.Pressed);
                if (_connectionDestination != candidate.Neighbor.FineStrategicCellId.Value ||
                    _cityLinkDetail?.IsVisibleInTree() != true || _cityWindowPanel?.Visible != false ||
                    host.StateHash != diagramHash)
                    throw new InvalidOperationException("Diagram button opened wrong physical connection.");
                CloseConnectionDetail();
                if (tabs.CurrentTab != 1 || !diagram.IsVisibleInTree())
                    throw new InvalidOperationException("Back did not restore connection diagram.");
            }
            // Exercise both actual Goldberg cell types, not synthetic polygons.
            foreach (int sides in new[] { 5, 6 })
            {
                var cell = host.Incidence.FineTopology.Cells.First(c => c.AdjacentCellIds.Count == sides);
                var geometry = _tacticalPreview.ReadLocalConnectionGeometry(cell.Id);
                if (geometry.Boundary.Length != sides || geometry.Links.Count != sides)
                    throw new InvalidOperationException("Canonical pentagon/hexagon geometry unavailable.");
                var expectedEdges = host.Incidence.FineTopology.Edges.Where(e => e.IncidentCellIds.Contains(cell.Id))
                    .Select(e => e.Id.Value).OrderBy(x => x);
                if (!geometry.Links.Keys.OrderBy(x => x).SequenceEqual(expectedEdges))
                    throw new InvalidOperationException("Diagram geometry includes nonincident edges.");
            }
            diagram.EdgeButton(link.Edge.FineStrategicEdgeId.Value).EmitSignal(Button.SignalName.Pressed);
            GD.Print("GA_NEXT_R220_CONNECTION_DIAGRAM_PASS canonical_geometry=true real_buttons=true hash_unchanged=true");
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
