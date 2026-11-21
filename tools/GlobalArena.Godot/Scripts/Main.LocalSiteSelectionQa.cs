using Godot;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>
/// R130 experimental read-only Local Center tabs. A site is still the
/// canonical fine tile occupied by real workers, not a new WorldState city.
/// Production shows workers actually assigned to this fine tile; Trade
/// shows ONLY authorized used crossings touching this tile in the last
/// CLOSED cycle. No local revenue or commodity allocation is fabricated.
/// </summary>
public partial class Main
{
    private Label? _localSiteSelectionQa;
    private PanelContainer? _localCenterQaPanel;
    private Label? _localCenterProductionQa;
    private Label? _localCenterTradeQa;
    private ulong? _selectedCartographicSite;

    private void CreateLocalSiteSelectionQaControls(VBoxContainer parent)
    {
        if (_tacticalPreview?.IsCartographicSiteMode != true) return;
        _localSiteSelectionQa = new Label
        {
            Name = "GANextR129CartographicLocalSiteSelection",
            Text = "LOCAL CENTER | Select a visible site name on the map",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _localSiteSelectionQa.AddThemeFontSizeOverride("font_size", 13);
        parent.AddChild(_localSiteSelectionQa);

        _localCenterQaPanel = new PanelContainer
        {
            Name = "GANextR130LocalCenterQa",
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        parent.AddChild(_localCenterQaPanel);
        var tabs = new TabContainer
        {
            Name = "GANextR130LocalCenterTabs",
            CustomMinimumSize = new Vector2(0f, 240f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _localCenterQaPanel.AddChild(tabs);
        var production = new VBoxContainer
        {
            Name = "Production",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tabs.AddChild(production);
        _localCenterProductionQa = new Label
        {
            Name = "GANextR130ProductionReadout",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "Select an occupied site to inspect production."
        };
        production.AddChild(_localCenterProductionQa);
        var trade = new VBoxContainer
        {
            Name = "Trade",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tabs.AddChild(trade);
        _localCenterTradeQa = new Label
        {
            Name = "GANextR130TradeReadout",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "Close an economic cycle to inspect authorized crossings."
        };
        trade.AddChild(_localCenterTradeQa);
    }

    private void OnCartographicLocalSiteSelected(ulong physicalTileId)
    {
        if (_tacticalPreview?.IsCartographicSiteMode != true ||
            _tacticalHost is null ||
            !_tacticalPreview.LocalSiteMarkerTileIds.Contains(physicalTileId))
            return;
        _selectedCartographicSite = physicalTileId;
        RefreshLocalSiteSelectionQa();
    }

    private void RefreshLocalSiteSelectionQa()
    {
        if (_localSiteSelectionQa is null ||
            _tacticalHost is null ||
            !_selectedCartographicSite.HasValue)
            return;
        ulong tile = _selectedCartographicSite.Value;
        var snapshot = _tacticalHost.Snapshot;
        var residents = snapshot.Workers.Workers
            .Where(w => w.TacticalTileId?.FineStrategicCellId.Value == tile)
            .ToArray();
        var own = residents.Where(w => w.Owner == _humanCivilization)
            .OrderBy(w => w.Id.Value).ToArray();
        _localSiteSelectionQa.Text =
            $"LOCAL CENTER | Site {tile:D4} | " +
            $"{residents.Length} physical workers | QA tile proxy";
        if (_localCenterQaPanel is null ||
            _localCenterProductionQa is null ||
            _localCenterTradeQa is null) return;
        _localCenterQaPanel.Visible = true;

        // Do not expose other civilizations' worker-level production
        // through the player's local administration screen.
        ulong output = 0UL;
        foreach (var worker in own.Where(w =>
            w.Activity == GlobalArena.Runtime.PhysicalWorkerActivity.Producing))
            output = checked(output + worker.OutputMicroPerTurn);
        string rows = own.Length == 0
            ? "No workers owned by this civilization at this site."
            : string.Join("\n", own.Select(w =>
                $"#{w.Id.Value} | {w.Activity} | " +
                $"occupation {(w.Occupation?.ToString() ?? "n/a")} | " +
                $"commodity {w.Commodity.Value} | " +
                $"output {w.OutputMicroPerTurn} micro/turn"));
        _localCenterProductionQa.Text =
            $"PRODUCTION | Site {tile:D4}\n" +
            $"Own workers: {own.Length} | own producing output: " +
            $"{output} micro/turn (current assignments, NOT settled sales)\n" +
            rows;

        var view = _tacticalHost.GetLastClosedEconomicTradeView(
            _humanCivilization);
        if (view.Availability ==
            FoundationEconomicTradeOverlayAvailability.NoClosedCycle)
        {
            _localCenterTradeQa.Text =
                "TRADE | No closed economic cycle yet. " +
                "No routes or sales are inferred.";
        }
        else
        {
            // The authorized view publishes used EDGES, not complete
            // origin-destination deliveries or site-specific sale prices.
            var adjacent = view.UsedEdges
                .Where(e => e.FirstTile.FineStrategicCellId.Value == tile ||
                            e.SecondTile.FineStrategicCellId.Value == tile)
                .OrderBy(e => e.Edge.FineStrategicEdgeId.Value)
                .ToArray();
            var transport = snapshot.Transport.Edges.ToDictionary(e => e.Edge);
            // These are CURRENT physical unit freight quotes from the
            // canonical infrastructure snapshot, not the historical price
            // charged during the previous settled cycle. A site's exact
            // money allocation requires a separate authorized market ledger.
            string CrossingReadout(FoundationPhysicalTradeEdgeLoad line)
            {
                if (!transport.TryGetValue(line.Edge, out var infrastructure))
                    throw new InvalidOperationException(
                        "Authorized used crossing is absent from current transport.");
                ulong currentUnitFreight =
                    FoundationPhysicalAuthoritativeTransportProjector
                        .EffectiveUnitFreightCost(infrastructure);
                return $"Edge {line.Edge.FineStrategicEdgeId.Value} | " +
                    $"cargo {line.TotalCargoMicro} micro (last cycle) | " +
                    $"current unit freight " +
                    $"{FormatMoneyMicroForDisplay(currentUnitFreight)} | " +
                    $"{line.FirstTile.FineStrategicCellId.Value}" +
                    $" - {line.SecondTile.FineStrategicCellId.Value}";
            }
            _localCenterTradeQa.Text = adjacent.Length == 0
                ? $"TRADE | Last closed cycle {view.EconomicCycleOrdinal}: " +
                  "no authorized used crossing incident to this site."
                : $"TRADE | Last closed cycle {view.EconomicCycleOrdinal}\n" +
                  $"Authorized used crossings: {adjacent.Length}\n" +
                  string.Join("\n", adjacent.Select(CrossingReadout)) +
                  "\nCurrent unit freight is a quote, NOT the money " +
                  "actually charged in the last closed cycle. " +
                  "These are used crossings, not full routes. " +
                  "Revenue, taxes, toll payments and exact historical " +
                  "freight by site are unavailable.";
        }
        GD.Print($"GA_NEXT_R130_LOCAL_CENTER_READ tile={tile} " +
            $"own_workers={own.Length} settled_cycle=" +
            $"{(view.EconomicCycleOrdinal?.ToString() ?? "none")} " +
            "read_only=true no_phantom_local_market=true");
    }
}
