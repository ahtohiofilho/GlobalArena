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
    private Button? _portQaInvest;
    private Label? _portQaDetails;
    private Label? _portQaFeedback;
    private readonly System.Collections.Generic.List<PhysicalTacticalEdgeId>
        _portQaChoices = new();
    private PhysicalTacticalEdgeId? _portQaSelected;
    private PhysicalTacticalEdgeId? _portQaQueued;
    private ulong _portQaOldFreight;

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
        var title = new Label { Text = "PORT INVESTMENT | EXPERIMENTAL QA" };
        title.AddThemeFontSizeOverride("font_size", 13);
        section.AddChild(title);
        section.AddChild(new Label
        {
            Text = "Close E once to earn real cash. Select an actual port " +
                "edge, queue 0.100000 monetary investment, then E to commit. " +
                "Only your treasury pays; no workers or goods are consumed.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
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
            Text = "QUEUE PORT INVESTMENT | 0.100000",
            Disabled = true
        };
        _portQaInvest.Pressed += QueuePortInvestmentQa;
        section.AddChild(_portQaInvest);
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
            _portQaDetails is null || _portQaFeedback is null ||
            _tacticalHost is null || !_interactiveEconomicPreviewActive)
            return;
        var host = _tacticalHost;
        var snapshot = host.Snapshot;
        var fine = _r123FineGeography;
        if (fine is null) return;

        var incidence = host.Incidence;
        var byId = snapshot.Transport.Edges.ToDictionary(x => x.Edge);
        var navigable = new System.Collections.Generic.List<
            (PhysicalTacticalEdgeId Id, long Depth, bool Used)>();
        var authorized = host.GetLastClosedEconomicTradeView(_humanCivilization);
        var used = authorized.UsedEdges.Select(x => x.Edge).ToHashSet();
        foreach (var edge in incidence.FineTopology.Edges)
        {
            int a = checked((int)edge.IncidentCellIds[0].Value - 1);
            int b = checked((int)edge.IncidentCellIds[1].Value - 1);
            if (fine.CrossingAtIndices(a, b) !=
                FoundationFineTransportCrossingKind.Port) continue;
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
            navigable.Add((id, depth, used.Contains(id)));
        }

        // Display real used port edges first; if none, offer a few real
        // financeable access edges. Never fabricate a port or a tile.
        var selected = navigable.Where(p => p.Used)
            .OrderBy(p => p.Id.FineStrategicEdgeId.Value).Take(8).ToList();
        if (selected.Count == 0)
            selected = navigable.OrderBy(p => p.Id.FineStrategicEdgeId.Value)
                .Take(8).ToList();
        if (_portQaSelected.HasValue &&
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
                $"PORT EDGE {p.Id.FineStrategicEdgeId.Value} | " +
                $"depth {p.Depth} | {(p.Used ? "USED" : "AVAILABLE")}");
        }
        if (_portQaChoices.Count == 0)
        {
            _portQaInvest.Disabled = true;
            _portQaDetails.Text = "No financeable navigable port exists.";
            return;
        }
        if (!_portQaSelected.HasValue ||
            !_portQaChoices.Contains(_portQaSelected.Value))
            _portQaSelected = _portQaChoices[0];
        int index = _portQaChoices.IndexOf(_portQaSelected.Value);
        _portQaSelector.Select(index);
        var current = byId[_portQaSelected.Value];
        ulong effective = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(current);
        var investment = current.CommittedInvestmentMoneyMicro ?? 0UL;
        _portQaDetails.Text =
            $"Natural unit freight {current.FreePhysicalCostMicroPerWholeCargoUnit} micro" +
            $" | effective {effective} micro" +
            $" | port construction basis {current.NaturalConstructionCostMoneyMicro} micro" +
            $" | already invested {investment} micro";
        bool affordable = snapshot.Treasury.Get(_humanCivilization).CashScaled >=
            GlobalArenaMoneyScale.FromMoneyMicro(PortQaSpendMoneyMicro);
        bool ownedByAnother = current.OwnershipKind ==
                PhysicalTransportOwnershipKind.UnownedInfrastructure ||
            (current.OwnershipKind == PhysicalTransportOwnershipKind.Owned &&
             current.InfrastructureOwner != _humanCivilization);
        bool ready = host.ReadyCivilizations.Contains(_humanCivilization);
        _portQaInvest.Disabled = !affordable || ownedByAnother ||
            ready || _portQaQueued.HasValue;
        if (_portQaQueued.HasValue)
            _portQaFeedback.Text = "Investment queued in the real host; " +
                "press E to commit and audit the ledger.";
        else if (!affordable)
            _portQaFeedback.Text = "Close one real economic turn (E) " +
                "to earn enough treasury cash before investing.";
        else if (ownedByAnother)
            _portQaFeedback.Text = "Selected port belongs to another owner.";
        else
            _portQaFeedback.Text = "Port is financeable. Queue investment, then E.";
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
            _portQaOldFreight = FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(edge);
            host.QueueHumanMonetaryInvestment(_humanCivilization,
                _portQaSelected.Value, PortQaSpendMoneyMicro);
            _portQaQueued = _portQaSelected;
            _portQaFeedback.Text = "0.100000 queued (NOT YET SPENT). Press E.";
            GD.Print("GA_NEXT_R123_PORT_INVESTMENT_QUEUED " +
                $"edge={_portQaSelected.Value.FineStrategicEdgeId.Value} " +
                $"money_micro={PortQaSpendMoneyMicro} " +
                $"old_effective_freight_micro={_portQaOldFreight}");
        }
        catch (System.Exception ex)
        {
            _portQaFeedback.Text = "Investment rejected: " + ex.Message;
            GD.PrintErr("GA_NEXT_R123_PORT_INVESTMENT_REJECTED " + ex);
        }
        RefreshPortInvestmentQaControls();
    }

    private void AuditPortInvestmentQa(
        FoundationTacticalCollectiveReceipt receipt)
    {
        if (!_portQaQueued.HasValue || _tacticalHost is null ||
            _portQaFeedback is null) return;
        var committed = receipt.InfrastructureInvestments.FirstOrDefault(x =>
            x.Edge == _portQaQueued.Value &&
            x.Investor == _humanCivilization);
        if (committed is null)
            throw new InvalidOperationException(
                "Queued port investment absent from closed real journal.");
        var after = _tacticalHost.Snapshot.Transport.Edges.Single(x =>
            x.Edge == _portQaQueued.Value);
        ulong newCost = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(after);
        if (newCost > _portQaOldFreight ||
            committed.MoneyMicro != PortQaSpendMoneyMicro)
            throw new InvalidOperationException(
                "R123 authoritative port capital must reduce, not raise freight.");
        _portQaFeedback.Text =
            $"REAL INVESTMENT COMMITTED | edge {_portQaQueued.Value.FineStrategicEdgeId.Value}" +
            $" | spent 0.100000 | unit freight {_portQaOldFreight} -> {newCost}" +
            $" | replay {(receipt.ReplayMatched ? "OK" : "FAIL")}";
        GD.Print("GA_NEXT_R123_PORT_INVESTMENT_SETTLED " +
            $"edge={_portQaQueued.Value.FineStrategicEdgeId.Value} " +
            $"money_micro={committed.MoneyMicro} before={_portQaOldFreight} " +
            $"after={newCost} replay={receipt.ReplayMatched}");
        _portQaQueued = null;
    }
}
