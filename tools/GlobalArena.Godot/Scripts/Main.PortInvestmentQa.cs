using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R123 manual visual QA only. The port money is NOT simulated or minted
/// by the client: pressing the button submits a real civilization investment
/// to the existing trusted host. E is the sole journal commit boundary.
/// </summary>
public partial class Main
{
    private const ulong PortQaSpendMoneyMicro = 100_000UL;
    private FoundationFineFirstPhysicalGeography? _r123FineGeography;
    private OptionButton? _portQaSelector;
    private OptionButton? _r143CrossingFilter;
    private FoundationFineTransportCrossingKind _r143SelectedCrossing =
        FoundationFineTransportCrossingKind.Port;
    private Button? _portQaInvest;
    private Button? _portQaCancel;
    private Label? _portQaDetails;
    private Label? _portQaFeedback;
    private readonly System.Collections.Generic.List<PhysicalTacticalEdgeId>
        _portQaChoices = new();
    private PhysicalTacticalEdgeId? _portQaSelected;
    private readonly Dictionary<PhysicalTacticalEdgeId,
        (ulong BeforeFreight, ulong BeforeCargo)> _r144QueuedCapital = new();
    private OptionButton? _r144PendingSelector;
    private readonly List<PhysicalTacticalEdgeId> _r144PendingChoices = new();
    private string? _portQaLastSettledAudit;
    private bool _portQaUserSelected;
    private ulong _portQaCargoBefore;

    private void CreatePortInvestmentQaControls(VBoxContainer parent)
    {
        // Only the deliberately selected economy laboratory exposes this UI.
        // The ordinary gameplay panel and existing smoke tests are unchanged.
        if (!_interactiveEconomicPreviewActive) return;

        var section = new VBoxContainer
        {
            Name = "GANextR123ExperimentalPortInvestment",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        parent.AddChild(section);
        var title = new Label { Text = "TRANSPORT INVESTMENT | EXPERIMENTAL QA" };
        title.AddThemeFontSizeOverride("font_size", 13);
        section.AddChild(title);
        section.AddChild(new Label
        {
            Text = "Choose a real land or coastal edge. Queue 0.100000 " +
                "capital, cancel before E or commit with E. Only your " +
                "civilization treasury pays; no workers or goods are consumed.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        _r143CrossingFilter = new OptionButton
        {
            Name = "GANextR143CrossingFilter",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _r143CrossingFilter.AddItem("COAST | land-sea port edges");
        _r143CrossingFilter.AddItem("LAND | ground-to-ground edges");
        _r143CrossingFilter.ItemSelected += slot =>
        {
            _r143SelectedCrossing = slot == 1
                ? FoundationFineTransportCrossingKind.Ground
                : FoundationFineTransportCrossingKind.Port;
            _portQaSelected = null;
            _portQaUserSelected = false;
            RefreshPortInvestmentQaControls();
        };
        section.AddChild(_r143CrossingFilter);
        _portQaSelector = new OptionButton
        {
            Name = "GANextR123PortSelector",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _portQaSelector.ItemSelected += index =>
        {
            int slot = checked((int)index);
            if (slot < 0 || slot >= _portQaChoices.Count) return;
            _portQaSelected = _portQaChoices[slot];
            _portQaUserSelected = true;
            RefreshPortInvestmentQaControls();
        };
        section.AddChild(_portQaSelector);
        _portQaDetails = new Label
        {
            Name = "GANextR123PortDetails",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        section.AddChild(_portQaDetails);
        _portQaInvest = new Button
        {
            Name = "GANextR123InvestPort",
            Text = "QUEUE EDGE INVESTMENT | " +
                FormatMoneyMicroForDisplay(PortQaSpendMoneyMicro),
            Disabled = true
        };
        _portQaInvest.Pressed += QueuePortInvestmentQa;
        section.AddChild(_portQaInvest);
        _r144PendingSelector = new OptionButton
        {
            Name = "GANextR144PendingEdgeSelector",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        section.AddChild(_r144PendingSelector);
        _portQaCancel = new Button
        {
            Name = "GANextR142CancelPortInvestment",
            Text = "CANCEL PENDING EDGE INVESTMENT",
            Disabled = true
        };
        _portQaCancel.Pressed += CancelPortInvestmentQa;
        section.AddChild(_portQaCancel);
        _portQaFeedback = new Label
        {
            Name = "GANextR123PortFeedback",
            Text = "Waiting for authoritative treasury...",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        section.AddChild(_portQaFeedback);
    }

    private void RefreshPortInvestmentQaControls()
    {
        if (_portQaSelector is null || _portQaInvest is null ||
            _portQaCancel is null || _r144PendingSelector is null ||
            _portQaDetails is null || _portQaFeedback is null ||
            _tacticalHost is null || !_interactiveEconomicPreviewActive)
            return;
        var host = _tacticalHost;
        var snapshot = host.Snapshot;
        var plans = host.GetHumanMonetaryInvestments(_humanCivilization);
        _r144PendingChoices.Clear();
        _r144PendingSelector.Clear();
        foreach (var plan in plans)
        {
            _r144PendingChoices.Add(plan.Edge);
            _r144PendingSelector.AddItem(
                $"PENDING EDGE {plan.Edge.FineStrategicEdgeId.Value} | " +
                FormatMoneyMicroForDisplay(plan.MoneyMicro));
        }
        if (plans.Count == 0)
            _r144PendingSelector.AddItem("NO PENDING INVESTMENTS");
        var fine = _r123FineGeography;
        if (fine is null) return;

        var incidence = host.Incidence;
        var byId = snapshot.Transport.Edges.ToDictionary(x => x.Edge);
        var navigable = new System.Collections.Generic.List<
            (PhysicalTacticalEdgeId Id, long Depth, ulong CargoMicro)>();
        var authorized = host.GetLastClosedEconomicTradeView(_humanCivilization);
        var used = authorized.UsedEdges.ToDictionary(
            x => x.Edge, x => x.TotalCargoMicro);
        foreach (var edge in incidence.FineTopology.Edges)
        {
            int a = checked((int)edge.IncidentCellIds[0].Value - 1);
            int b = checked((int)edge.IncidentCellIds[1].Value - 1);
            if (fine.CrossingAtIndices(a, b) != _r143SelectedCrossing)
                continue;
            var id = new PhysicalTacticalEdgeId(
                incidence.Refinement.FineParameters, edge.Id);
            if (!byId.TryGetValue(id, out var record) ||
                record.NaturalConstructionCostMoneyMicro is not > 0UL ||
                record.CargoMicroAtReferenceLoad == ulong.MaxValue)
                continue;
            long waterElevation = fine.FineElevationRaw[a] <= fine.SeaLevelRaw
                ? fine.FineElevationRaw[a] : fine.FineElevationRaw[b];
            long depth = System.Math.Max(0L,
                fine.SeaLevelRaw - waterElevation);
            // The existing authority still validates financeability, ownership,
            // available treasury and turn readiness on the command itself.
            navigable.Add((id, depth, used.GetValueOrDefault(id)));
        }

        // Display used actual edges first; fall back to signed real
        // crossing edges. No type-specific economic model is introduced.
        var selected = navigable.Where(p => p.CargoMicro > 0UL)
            .OrderByDescending(p => p.CargoMicro)
            .ThenBy(p => p.Id.FineStrategicEdgeId.Value).Take(8).ToList();
        int usedPortCount = navigable.Count(p => p.CargoMicro > 0UL);
        string crossingLabel = _r143SelectedCrossing ==
            FoundationFineTransportCrossingKind.Ground ? "LAND" : "COAST";
        if (selected.Count == 0)
            selected = navigable.OrderBy(p => p.Id.FineStrategicEdgeId.Value)
                .Take(8).ToList();
        if ((_portQaUserSelected || _r144QueuedCapital.Count > 0) &&
            _portQaSelected.HasValue &&
            navigable.Any(p => p.Id == _portQaSelected.Value) &&
            selected.All(p => p.Id != _portQaSelected.Value))
            selected.Add(navigable.Single(p => p.Id == _portQaSelected.Value));

        _portQaChoices.Clear();
        _portQaSelector.Clear();
        for (int i = 0; i < selected.Count; i++)
        {
            var p = selected[i];
            _portQaChoices.Add(p.Id);
            _portQaSelector.AddItem(
                $"{crossingLabel} EDGE {p.Id.FineStrategicEdgeId.Value} | " +
                (_r143SelectedCrossing == FoundationFineTransportCrossingKind.Port
                    ? $"depth {p.Depth} | " : "") +
                (p.CargoMicro > 0UL
                    ? $"USED ({p.CargoMicro} cargo micro)"
                    : "AVAILABLE"));
        }
        if (_portQaChoices.Count == 0)
        {
            _portQaInvest.Disabled = true;
            _portQaCancel.Disabled = plans.Count == 0;
            _portQaDetails.Text = $"No financeable {crossingLabel} edges exist.";
            return;
        }
        // After the first actual settlement, automatically focus a port
        // carrying real cargo, unless the user deliberately selected an
        // alternative or already queued a specific port order.
        if (!_portQaSelected.HasValue ||
            !_portQaChoices.Contains(_portQaSelected.Value) ||
            (!_portQaUserSelected && _r144QueuedCapital.Count == 0 &&
             usedPortCount > 0 &&
             !used.ContainsKey(_portQaSelected.Value)))
            _portQaSelected = _portQaChoices[0];
        int index = _portQaChoices.IndexOf(_portQaSelected.Value);
        _portQaSelector.Select(index);
        var current = byId[_portQaSelected.Value];
        ulong effective = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(current);
        var investment = current.CommittedInvestmentMoneyMicro ?? 0UL;
        ulong selectedCargo = used.GetValueOrDefault(_portQaSelected.Value);
        _portQaDetails.Text =
            $"Crossing {crossingLabel} | last-cycle used: {usedPortCount}" +
            $" | selected-edge cargo: {selectedCargo} micro | " +
            $"Natural unit freight {FormatMoneyMicroForDisplay(current.FreePhysicalCostMicroPerWholeCargoUnit)}" +
            $" | effective {FormatMoneyMicroForDisplay(effective)}" +
            $" | edge construction basis {FormatMoneyMicroForDisplay(current.NaturalConstructionCostMoneyMicro!.Value)}" +
            $" | accumulated capital {FormatMoneyMicroForDisplay(investment)}";
        var reserved = plans.Aggregate(System.Numerics.BigInteger.Zero,
            (total, plan) => total +
                GlobalArenaMoneyScale.FromMoneyMicro(plan.MoneyMicro));
        bool affordable = snapshot.Treasury.Get(_humanCivilization).CashScaled -
            reserved >= GlobalArenaMoneyScale.FromMoneyMicro(PortQaSpendMoneyMicro);
        bool alreadyQueued = plans.Any(plan => plan.Edge == _portQaSelected.Value);
        bool ownedByAnother = current.OwnershipKind ==
                PhysicalTransportOwnershipKind.UnownedInfrastructure ||
            (current.OwnershipKind == PhysicalTransportOwnershipKind.Owned &&
             current.InfrastructureOwner != _humanCivilization);
        bool ready = host.ReadyCivilizations.Contains(_humanCivilization);
        _portQaInvest.Disabled = !affordable || ownedByAnother ||
            ready || alreadyQueued;
        _portQaCancel.Disabled = ready || plans.Count == 0;
        _r144PendingSelector.Disabled = ready || plans.Count == 0;
        if (_r143CrossingFilter is not null)
            _r143CrossingFilter.Disabled = ready;
        if (plans.Count > 0)
            _portQaFeedback.Text = $"{plans.Count} real investment(s) pending | " +
                $"reserved {FormatMoneyMicroForDisplay(
                    plans.Aggregate(0UL, (sum, order) => checked(sum + order.MoneyMicro)))} | " +
                "choose another edge/type, cancel a listed order, or press E to commit.";
        else if (_portQaLastSettledAudit is not null)
            _portQaFeedback.Text = _portQaLastSettledAudit;
        else if (!affordable)
            _portQaFeedback.Text = "Close one real economic turn (E) " +
                "to earn enough treasury cash before investing.";
        else if (ownedByAnother)
            _portQaFeedback.Text = "Selected edge belongs to another owner.";
        else if (usedPortCount == 0)
            _portQaFeedback.Text = "No selected crossing type appears in " +
                "CIV 1 last settled trade routes. Available real edges can " +
                "be financed, but route improvement cannot be inferred.";
        else
            _portQaFeedback.Text = "Edge is financeable. Queue investment, then E.";
    }

    private void QueuePortInvestmentQa()
    {
        if (_tacticalHost is null || !_portQaSelected.HasValue ||
            _portQaFeedback is null) return;
        try
        {
            var host = _tacticalHost;
            var edge = host.Snapshot.Transport.Edges.Single(x =>
                x.Edge == _portQaSelected.Value);
            var beforeFreight = FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(edge);
            var beforeCargo = host.GetLastClosedEconomicTradeView(
                _humanCivilization).UsedEdges.FirstOrDefault(x =>
                    x.Edge == _portQaSelected.Value)?.TotalCargoMicro ?? 0UL;
            host.QueueHumanMonetaryInvestment(_humanCivilization,
                _portQaSelected.Value, PortQaSpendMoneyMicro);
            _portQaLastSettledAudit = null;
            _r144QueuedCapital.Add(_portQaSelected.Value,
                (beforeFreight, beforeCargo));
            _portQaFeedback.Text =
                $"{FormatMoneyMicroForDisplay(PortQaSpendMoneyMicro)} queued " +
                "(NOT YET SPENT). Press E.";
            GD.Print("GA_NEXT_R123_PORT_INVESTMENT_QUEUED " +
                $"edge={_portQaSelected.Value.FineStrategicEdgeId.Value} " +
                $"money_micro={PortQaSpendMoneyMicro} " +
                $"old_effective_freight_micro={beforeFreight}");
        }
        catch (System.Exception ex)
        {
            _portQaFeedback.Text = "Investment rejected: " + ex.Message;
            GD.PrintErr("GA_NEXT_R123_PORT_INVESTMENT_REJECTED " + ex);
        }
        RefreshPortInvestmentQaControls();
    }

    // R142: cancel a private monetary intention, never undo a committed
    // turn or credit cash from a fictitious reverse transaction.
    private void CancelPortInvestmentQa()
    {
        if (_tacticalHost is null || _r144PendingSelector is null ||
            _r144PendingChoices.Count == 0 || _portQaFeedback is null)
            return;
        int index = _r144PendingSelector.Selected;
        if (index < 0 || index >= _r144PendingChoices.Count) return;
        var edge = _r144PendingChoices[index];
        try
        {
            bool cancelled = _tacticalHost.CancelHumanMonetaryInvestment(
                _humanCivilization, edge);
            if (!cancelled)
                throw new InvalidOperationException(
                    "Authoritative host had no queued investment for this edge.");
            _r144QueuedCapital.Remove(edge);
            _portQaLastSettledAudit =
                "Pending edge investment cancelled before E; treasury and " +
                "capital unchanged. You may choose another edge.";
            GD.Print("GA_NEXT_R142_PORT_INVESTMENT_CANCELLED " +
                $"edge={edge.FineStrategicEdgeId.Value} " +
                "before_commit=true treasury_unchanged=true");
        }
        catch (System.Exception ex)
        {
            _portQaFeedback.Text = "Cancellation rejected: " + ex.Message;
            GD.PrintErr("GA_NEXT_R142_PORT_INVESTMENT_CANCEL_REJECTED " + ex);
            return;
        }
        RefreshPortInvestmentQaControls();
    }

    // Automated scene accessors expose presentation state, not mutable authority.
    internal bool R142PortCancelAvailable =>
        _portQaCancel is { Disabled: false };
    internal bool R142PortInvestmentQueued => _r144QueuedCapital.Count > 0;
    internal int R144PendingCapitalCount => _r144QueuedCapital.Count;
    internal string R143CrossingFilterSelected => _r143SelectedCrossing.ToString();

    // Verify the actual Godot selector against the signed crossing catalogue.
    // This intentionally never invokes a money command or closes a turn.
    internal void ExerciseR143RealCrossingSelectionSmoke()
    {
        if (!_interactiveEconomicPreviewActive) return;
        if (_r143CrossingFilter is null || _portQaSelector is null ||
            _tacticalHost is null || _r123FineGeography is null)
            throw new InvalidOperationException("R143 investment selector missing.");
        var original = _r143SelectedCrossing;
        var geography = _r123FineGeography;
        var incidence = _tacticalHost.Incidence;
        var before = _tacticalHost.StateHash;
        foreach (var crossing in new[] {
            FoundationFineTransportCrossingKind.Ground,
            FoundationFineTransportCrossingKind.Port })
        {
            _r143SelectedCrossing = crossing;
            _portQaSelected = null;
            RefreshPortInvestmentQaControls();
            if (_portQaChoices.Count == 0)
                throw new InvalidOperationException(
                    $"R143 no actual financeable {crossing} edges.");
            foreach (var edgeId in _portQaChoices)
            {
                var edge = incidence.FineTopology.Edges.Single(x =>
                    x.Id == edgeId.FineStrategicEdgeId);
                var a = checked((int)edge.IncidentCellIds[0].Value - 1);
                var b = checked((int)edge.IncidentCellIds[1].Value - 1);
                if (geography.CrossingAtIndices(a, b) != crossing)
                    throw new InvalidOperationException(
                        $"R143 selected {crossing} displays a foreign edge.");
            }
            if (_tacticalHost.StateHash != before)
                throw new InvalidOperationException("Selecting crossings mutated the game.");
        }
        _r143SelectedCrossing = original;
        _r143CrossingFilter.Select(original ==
            FoundationFineTransportCrossingKind.Ground ? 1 : 0);
        _portQaSelected = null;
        RefreshPortInvestmentQaControls();
        GD.Print("GA_NEXT_R143_CROSSING_SELECTOR_PASS " +
            "land=true coast=true real_edges_only=true state_unchanged=true");
    }

    private void AuditPortInvestmentQa(
        FoundationTacticalCollectiveReceipt receipt)
    {
        if (_r144QueuedCapital.Count == 0 || _tacticalHost is null ||
            _portQaFeedback is null) return;
        var audit = new List<string>();
        foreach (var (edge, previous) in _r144QueuedCapital.OrderBy(
            x => x.Key.FineStrategicEdgeId.Value))
        {
            var committed = receipt.InfrastructureInvestments.SingleOrDefault(x =>
                x.Edge == edge && x.Investor == _humanCivilization);
            if (committed is null)
                throw new InvalidOperationException(
                    "Queued investment absent from the real closed journal.");
            var after = _tacticalHost.Snapshot.Transport.Edges.Single(x =>
                x.Edge == edge);
            ulong newCost = FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(after);
            if (newCost > previous.BeforeFreight ||
                committed.MoneyMicro != PortQaSpendMoneyMicro)
                throw new InvalidOperationException(
                    "Canonical capital investment must not raise effective freight.");
            ulong cargoAfter = _tacticalHost.GetLastClosedEconomicTradeView(
                _humanCivilization).UsedEdges.FirstOrDefault(x =>
                    x.Edge == edge)?.TotalCargoMicro ?? 0UL;
            audit.Add($"edge {edge.FineStrategicEdgeId.Value}: " +
                $"spent {FormatMoneyMicroForDisplay(committed.MoneyMicro)}, " +
                $"freight {FormatMoneyMicroForDisplay(previous.BeforeFreight)} -> " +
                $"{FormatMoneyMicroForDisplay(newCost)}, " +
                $"cargo {previous.BeforeCargo} -> {cargoAfter}");
            GD.Print("GA_NEXT_R144_MULTI_EDGE_INVESTMENT_SETTLED " +
                $"edge={edge.FineStrategicEdgeId.Value} " +
                $"money_micro={committed.MoneyMicro} " +
                $"before={previous.BeforeFreight} after={newCost} " +
                $"replay={receipt.ReplayMatched}");
        }
        _portQaLastSettledAudit = $"REAL INVESTMENTS COMMITTED " +
            $"({_r144QueuedCapital.Count}) | " +
            string.Join(" | ", audit) +
            $" | replay {(receipt.ReplayMatched ? "OK" : "FAIL")}";
        _portQaFeedback.Text = _portQaLastSettledAudit;
        _r144QueuedCapital.Clear();
    }
}
