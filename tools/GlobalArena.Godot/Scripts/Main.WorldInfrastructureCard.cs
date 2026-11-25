using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>Infrastructure is an actual incident physical edge, not an
/// invented tile building. Funding and construction commands stay in the
/// existing connection dialog. Staged own funding is not active capital.</summary>
public partial class Main
{
    private Label? _worldInfrastructureContext, _worldInfrastructureDetail;
    private OptionButton? _worldInfrastructureSelector;
    private Button? _worldInfrastructureManage;
    private ProgressBar? _worldInfrastructureProgress;
    private ulong? _worldInfrastructureNode;
    private IReadOnlyList<FoundationLocalConnection> _worldInfrastructureLinks = Array.Empty<FoundationLocalConnection>();

    private ulong? ReadWorldContextNode()
    {
        if (_operationalCitySelected && _selectedCartographicSite.HasValue)
            return _selectedCartographicSite.Value;
        if (_operationalSelectedTile.HasValue) return _operationalSelectedTile.Value;
        return _tacticalHost?.Snapshot.Workers.Workers.FirstOrDefault(w =>
            w.Owner == _humanCivilization && w.Id.Value == _selectedLocalWorkerId)
            ?.TacticalTileId?.FineStrategicCellId.Value;
    }

    private void CreateWorldInfrastructureReadout(VBoxContainer body)
    {
        _worldInfrastructureContext = WorldLayerPlaceholder("");
        body.AddChild(_worldInfrastructureContext);
        _worldInfrastructureSelector = new OptionButton { Name = "GANextInfrastructureConnection",
            FitToLongestItem = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_worldInfrastructureSelector);
        _worldInfrastructureSelector.ItemSelected += _ => RefreshWorldInfrastructureDetail();
        _worldInfrastructureDetail = WorldLayerPlaceholder("");
        body.AddChild(_worldInfrastructureDetail);
        _worldInfrastructureProgress = new ProgressBar { MinValue = 0, MaxValue = 100,
            Step = 0, ShowPercentage = false, Visible = false };
        body.AddChild(_worldInfrastructureProgress);
        _worldInfrastructureManage = new Button { Name = "GANextInfrastructureManage", Text = "Manage connection" };
        _worldInfrastructureManage.Pressed += OpenWorldInfrastructureConnection;
        body.AddChild(_worldInfrastructureManage);
        RefreshWorldInfrastructure();
    }

    private FoundationLocalConnection? SelectedWorldInfrastructure =>
        _worldInfrastructureSelector is { Selected: >= 0 } selector &&
        selector.Selected < _worldInfrastructureLinks.Count
            ? _worldInfrastructureLinks[selector.Selected] : null;

    private void RefreshWorldInfrastructure()
    {
        if (_worldInfrastructureSelector is not { } selector || _worldInfrastructureContext is null) return;
        var host = _tacticalHost;
        var source = ReadWorldContextNode();
        var oldEdge = _worldInfrastructureNode == source ? SelectedWorldInfrastructure?.Edge : null;
        _worldInfrastructureNode = source;
        _worldInfrastructureLinks = host is not null && source.HasValue
            ? FoundationLocalConnectionsProjector.Read(host.Snapshot, host.Incidence,
                new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, new StrategicCellId(source.Value)))
            : Array.Empty<FoundationLocalConnection>();
        selector.Clear();
        int selected = 0;
        for (int i = 0; i < _worldInfrastructureLinks.Count; i++)
        {
            var link = _worldInfrastructureLinks[i];
            selector.AddItem($"To node {link.Neighbor.FineStrategicCellId.Value}");
            if (oldEdge.HasValue && link.Edge == oldEdge.Value) selected = i;
        }
        selector.Disabled = _worldInfrastructureLinks.Count == 0;
        if (!selector.Disabled) selector.Select(selected);
        int improved = _worldInfrastructureLinks.Count(l => l.InvestedMoneyMicro > 0UL);
        _worldInfrastructureContext.Text = source.HasValue
            ? $"Node {source.Value} | {_worldInfrastructureLinks.Count} connections\nImproved: {improved}"
            : "Select a tile or worker.";
        RefreshWorldInfrastructureDetail();
    }

    private void RefreshWorldInfrastructureDetail()
    {
        if (_worldInfrastructureDetail is null || _worldInfrastructureProgress is null ||
            _worldInfrastructureManage is null) return;
        _worldInfrastructureProgress.Visible = false;
        _worldInfrastructureProgress.Value = 0;
        var host = _tacticalHost;
        var link = SelectedWorldInfrastructure;
        _worldInfrastructureManage.Disabled = host is null || link is null;
        if (host is null || link is null)
        {
            _worldInfrastructureDetail.Text = "No connection selected.";
            return;
        }
        string kind = "Connection";
        if (_r123FineGeography is { } fine && _worldInfrastructureNode is { } source)
        {
            kind = fine.CrossingAtIndices(checked((int)source - 1),
                checked((int)link.Neighbor.FineStrategicCellId.Value - 1)) switch
            {
                FoundationFineTransportCrossingKind.Ground => "Land connection",
                FoundationFineTransportCrossingKind.Port => "Coastal connection",
                _ => "Sea connection"
            };
        }
        var pending = host.GetHumanPersistentFunding(_humanCivilization).SingleOrDefault(p => p.Edge == link.Edge);
        var project = host.Snapshot.Constructions.Projects.LastOrDefault(p =>
            p.Edge == link.Edge && !p.IsActivated && p.Investor == _humanCivilization);
        string status = link.InvestedMoneyMicro > 0UL ? "Active infrastructure" : "No active improvement";
        string detail = $"{kind} | {status}\nCapital: {GaDisplayNumbers.MoneyMicro(link.InvestedMoneyMicro)}\n" +
            $"Freight: {GaDisplayNumbers.MoneyMicro(link.UnitPhysicalFreightMicro)}/cargo";
        if (pending is not null)
            detail += $"\nPending funding: {GaDisplayNumbers.MoneyMicro(pending.MoneyMicro)} | E to submit";
        if (project is not null)
        {
            detail += $"\n{(project.IsComplete ? "Awaiting activation" : "Construction project")} | Funded: {GaDisplayNumbers.MoneyMicro(project.FundedMoneyMicro)}\n" +
                $"Work: {GaDisplayNumbers.PhysicalMicro(project.CompletedWorkMicro)} / {GaDisplayNumbers.PhysicalMicro(project.RequiredWorkMicro)}";
            _worldInfrastructureProgress.Visible = true;
            _worldInfrastructureProgress.Value = 100.0 * project.CompletedWorkMicro / project.RequiredWorkMicro;
        }
        _worldInfrastructureDetail.Text = detail;
    }

    private void OpenWorldInfrastructureConnection()
    {
        if (ReadWorldContextNode() != _worldInfrastructureNode) { RefreshWorldInfrastructure(); return; }
        if (SelectedWorldInfrastructure is { } link) OnR227MapConnectionRequested(link.Edge);
    }

    private void ExerciseWorldInfrastructure(FoundationTacticalHumanAiTurnHost host, PhysicalTacticalTileId node, bool fund)
    {
        var hash = host.StateHash;
        var source = node.FineStrategicCellId.Value;
        RefreshWorldInfrastructure();
        var expected = FoundationLocalConnectionsProjector.Read(host.Snapshot, host.Incidence, node);
        if (_worldInfrastructureNode != source || !_worldInfrastructureLinks.SequenceEqual(expected) ||
            _worldInfrastructureSelector?.ItemCount != expected.Count ||
            _worldInfrastructureContext?.Text.Contains($"Node {source} | {expected.Count} connections") != true)
            throw new InvalidOperationException("Infrastructure does not reflect selected physical node.");
        var selector = _worldInfrastructureSelector!;
        var manage = _worldInfrastructureManage ?? throw new InvalidOperationException("Infrastructure manage missing.");
        int index = expected.ToList().FindIndex(l => host.Snapshot.Transport.Edges.Any(p =>
            p.Edge == l.Edge && p.NaturalConstructionCostMoneyMicro is > 0UL &&
            p.CargoMicroAtReferenceLoad != ulong.MaxValue &&
            p.OwnershipKind != PhysicalTransportOwnershipKind.UnownedInfrastructure &&
            (p.OwnershipKind != PhysicalTransportOwnershipKind.Owned || p.InfrastructureOwner == _humanCivilization)));
        if (index < 0) throw new InvalidOperationException("Smoke requires a financeable incident connection.");
        selector.Select(index);
        selector.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
        var link = expected[index];
        if (_worldInfrastructureDetail?.Text.Contains($"Capital: {GaDisplayNumbers.MoneyMicro(link.InvestedMoneyMicro)}") != true ||
            _worldInfrastructureDetail.Text.Contains($"Freight: {GaDisplayNumbers.MoneyMicro(link.UnitPhysicalFreightMicro)}/cargo") != true)
            throw new InvalidOperationException("Infrastructure capital or freight differs from canonical link.");
        manage.EmitSignal(Button.SignalName.Pressed);
        if (_cityLinkDetail?.IsVisibleInTree() != true || !_cityConnectionSource.HasValue ||
            !_connectionDestination.HasValue || ResolveConnectionEdge(_cityConnectionSource.Value, _connectionDestination.Value) != link.Edge ||
            host.StateHash != hash)
            throw new InvalidOperationException("Infrastructure did not open the canonical connection dialog.");
        if (fund)
        {
            _connectionAmount!.Text = "0.10";
            RefreshConnectionInvestmentControls();
            if (_connectionInvest?.Disabled != false) throw new InvalidOperationException("Smoke funding unavailable after close.");
            _connectionInvest.EmitSignal(Button.SignalName.Pressed);
            if (!host.GetHumanPersistentFunding(_humanCivilization).Any(p => p.Edge == link.Edge && p.MoneyMicro == 100_000UL) ||
                _worldInfrastructureDetail?.Text.Contains("Pending funding: $0.10 | E to submit") != true ||
                _worldInfrastructureProgress?.Visible == true || host.StateHash != hash)
                throw new InvalidOperationException("Staged funding was not displayed privately or became active.");
            if (_connectionCancel?.Disabled != false) throw new InvalidOperationException("Funding cancellation unavailable.");
            _connectionCancel.EmitSignal(Button.SignalName.Pressed);
            if (host.GetHumanPersistentFunding(_humanCivilization).Any(p => p.Edge == link.Edge) ||
                _worldInfrastructureDetail?.Text.Contains("Pending funding:") != false || host.StateHash != hash)
                throw new InvalidOperationException("Cancelled funding remained in Infrastructure.");
        }
        _r227CloseConnection!.EmitSignal(Button.SignalName.Pressed);
        if (_cityWindowLayer?.Visible == true || host.StateHash != hash)
            throw new InvalidOperationException("Infrastructure dialog close changed world or failed to return to map.");
    }
}
