using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>R229.4: the same participant-scoped published crossing volumes
/// as the map. Undirected cargo, not complete routes or historical charges.</summary>
public partial class Main
{
    private Label? _cityTradeStatus, _connectionTradeDetail;
    private readonly Dictionary<PhysicalTacticalEdgeId, Button> _cityTradeButtons = new();

    private static string CityTradeHeading(FoundationLastClosedEconomicTradeView view) =>
        view.Availability == FoundationEconomicTradeOverlayAvailability.NoClosedCycle
            ? "TRADE | No closed economic cycle"
            : $"TRADE | CYCLE {view.EconomicCycleOrdinal} | Authorized cargo ({view.CargoUnitCode})";

    private static string ConnectionCargoText(FoundationLastClosedEconomicTradeView view,
        FoundationPhysicalTradeEdgeLoad? load) =>
        view.Availability == FoundationEconomicTradeOverlayAvailability.NoClosedCycle
            ? "Cargo: — (no closed cycle)"
            : load is null ? "Cargo: 0 (no own trade)" :
                $"Cargo: {FormatPhysicalMicroForDisplay(load.TotalCargoMicro)}";

    private void RefreshCityConnectionTrade()
    {
        var host = _tacticalHost;
        if (host is null || _cityTradeStatus is null || _cityConnectionSource is not { } source ||
            _cityWindowLayer?.Visible != true || _cityWindowPanel?.Visible != true ||
            _cityTradeButtons.Count == 0) return;
        var node = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, new StrategicCellId(source));
        var view = host.GetLastClosedEconomicTradeView(_humanCivilization);
        var loads = view.UsedEdges.ToDictionary(e => e.Edge);
        var links = FoundationLocalConnectionsProjector.Read(host.Snapshot, host.Incidence, node);
        _cityTradeStatus.Text = CityTradeHeading(view) +
            "\nCurrent freight is a quote, not the last cycle's charge. Cargo covers both directions and only transactions involving your civilization.";
        foreach (var link in links)
        {
            if (!_cityTradeButtons.TryGetValue(link.Edge, out var button)) continue;
            button.Text = $"Node {link.Neighbor.FineStrategicCellId.Value} | freight {FormatMoneyMicroForDisplay(link.UnitPhysicalFreightMicro)}/cargo\n" +
                ConnectionCargoText(view, loads.GetValueOrDefault(link.Edge));
        }
    }

    private void RefreshConnectionTradeDetail(FoundationLocalConnection link)
    {
        var host = _tacticalHost;
        if (host is null || _connectionTradeDetail is null) return;
        var view = host.GetLastClosedEconomicTradeView(_humanCivilization);
        var load = view.UsedEdges.SingleOrDefault(e => e.Edge == link.Edge);
        var text = CityTradeHeading(view) + "\n" + ConnectionCargoText(view, load);
        if (load is not null)
        {
            var names = CityCommodityNames(host, link.Neighbor);
            text += "\nCargo by commodity (both directions):\n" +
                string.Join("\n", load.ByCommodity.Select(row =>
                    $"{CityCommodityName(names, row.Commodity)}: {FormatPhysicalMicroForDisplay(row.CargoMicro)}"));
        }
        _connectionTradeDetail.Text = text +
            "\nOnly your civilization's trade is shown. Historical freight charges are not available here.";
    }

    private void ExerciseCityConnectionTradeReadout(FoundationTacticalHumanAiTurnHost host, PhysicalTacticalTileId node, ulong cycle)
    {
        var hash = host.StateHash;
        FocusCityProductionSmokeSite(host, node);
        var tabs = FindChild("GANextCityTabs", true, false) as TabContainer
            ?? throw new InvalidOperationException("Missing city tabs.");
        tabs.CurrentTab = 1;
        var view = host.GetLastClosedEconomicTradeView(_humanCivilization);
        if ((view.EconomicCycleOrdinal ?? 0UL) != cycle ||
            _cityTradeStatus?.Text.StartsWith(CityTradeHeading(view), StringComparison.Ordinal) != true)
            throw new InvalidOperationException("City trade cycle mismatch.");
        var links = FoundationLocalConnectionsProjector.Read(host.Snapshot, host.Incidence, node);
        if (_cityTradeButtons.Count != links.Count)
            throw new InvalidOperationException("Trade buttons must match real adjacent edges.");
        foreach (var link in links)
        {
            var load = view.UsedEdges.SingleOrDefault(e => e.Edge == link.Edge);
            var expected = ConnectionCargoText(view, load);
            var button = _cityTradeButtons[link.Edge];
            if (!button.Text.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException("Trade list cargo differs from authorized evidence.");
            button.EmitSignal(Button.SignalName.Pressed);
            if (_cityLinkDetail?.IsVisibleInTree() != true ||
                _connectionDestination != link.Neighbor.FineStrategicCellId.Value ||
                _connectionTradeDetail?.Text.Contains(expected, StringComparison.Ordinal) != true)
                throw new InvalidOperationException("Connection detail cargo differs from authorized evidence.");
            if (load is not null)
            {
                var names = CityCommodityNames(host, link.Neighbor);
                foreach (var row in load.ByCommodity)
                    if (!_connectionTradeDetail.Text.Contains(
                        $"{CityCommodityName(names, row.Commodity)}: {FormatPhysicalMicroForDisplay(row.CargoMicro)}", StringComparison.Ordinal))
                        throw new InvalidOperationException("Commodity cargo breakdown missing.");
            }
            _r227CloseConnection!.EmitSignal(Button.SignalName.Pressed);
        }
        if (host.StateHash != hash) throw new InvalidOperationException("Connection trade presentation mutated world.");
        _cityWindowLayer!.Visible = false;
    }
}
