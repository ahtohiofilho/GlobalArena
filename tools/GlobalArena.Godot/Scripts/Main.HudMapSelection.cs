using Godot;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

public partial class Main
{
    private Button? _hudTerrainCity;
    private bool HudMoveArmed => _tacticalPreview?.ArmedRightClickMove == true;

    private void SetHudMoveArmed(bool armed)
    {
        if (!LayeredWorldDock || _tacticalPreview is null) return;
        _tacticalPreview.ArmedRightClickMove = armed;
        RefreshHudMoveVisual();
    }

    private void RefreshHudMoveVisual()
    {
        if (!LayeredWorldDock || _localMoveButton is null) return;
        _localMoveButton.ToggleMode = true;
        _localMoveButton.SetPressedNoSignal(HudMoveArmed);
        _localMoveButton.TooltipText = HudMoveArmed
            ? "Right-click destination on map. Esc cancels."
            : "Move (G): arm, then right-click destination on map.";
        if (_hudTerrainCity is not null)
            _hudTerrainCity.Disabled = !_selectedCartographicSite.HasValue;
    }

    private void AssertNeutralHudStart(FoundationTacticalHumanAiTurnHost host)
    {
        var hash = host.StateHash;
        var queues = host.GetHumanPersistentQueues(_humanCivilization).ToArray();
        RefreshFineTacticalWorkerControls();
        GD.Print($"GA_NEXT_R230632_NEUTRAL worker={_selectedLocalWorkerId} context={ReadWorldContextNode()} edge={_hudMapEdge} site={_selectedCartographicSite} armed={HudMoveArmed} move_disabled={_localMoveButton?.Disabled} details_disabled={_worldUnitDetails?.Disabled} infra_disabled={_worldInfrastructureManage?.Disabled} city_disabled={_hudTerrainCity?.Disabled} selector_visible={_localWorkerSelector?.IsVisibleInTree()} readout_visible={_worldUnitReadout?.IsVisibleInTree()} terrain={_terrainSurface?.Text} hash_same={host.StateHash == hash}");
        if (_selectedLocalWorkerId.HasValue || ReadWorldContextNode().HasValue ||
            _hudMapEdge.HasValue || SelectedWorldInfrastructure is not null ||
            _selectedCartographicSite.HasValue || HudMoveArmed ||
            _localMoveButton?.Disabled != true || _worldUnitDetails?.Disabled != true ||
            _worldInfrastructureManage?.Disabled != true || _hudTerrainCity?.Disabled != true ||
            _localWorkerSelector?.IsVisibleInTree() != false ||
            _worldUnitReadout?.IsVisibleInTree() == true || _terrainSurface?.Text != "—" ||
            host.StateHash != hash || !host.GetHumanPersistentQueues(_humanCivilization).SequenceEqual(queues))
            throw new InvalidOperationException("HUD startup must be neutral and read-only.");
    }

    private void ExerciseHudArmedMove(FoundationTacticalHumanAiTurnHost host)
    {
        var worker = host.Snapshot.Workers.Workers.First(w => w.Owner == _humanCivilization &&
            w.TacticalTileId.HasValue && _r123FineGeography!.SurfaceAtIndex((int)w.TacticalTileId.Value.FineStrategicCellId.Value - 1) == FoundationFineSurfaceKind.Land);
        OnCanonicalWorkerBadgeClicked(worker.Id.Value);
        var source = worker.TacticalTileId!.Value;
        var target = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters,
            host.Incidence.FineTopology.Cells[(int)source.FineStrategicCellId.Value - 1].AdjacentCellIds.First(c =>
                _r123FineGeography!.CrossingAtIndices((int)source.FineStrategicCellId.Value - 1,
                    (int)c.Value - 1) == FoundationFineTransportCrossingKind.Ground));
        var hash = host.StateHash;
        var count = ReadPersistentQueue(worker.Id).Orders.Count;
        var button = _localMoveButton!;
        if (button.Disabled) throw new InvalidOperationException("Selected worker cannot arm movement.");
        button.EmitSignal(Button.SignalName.Pressed);
        if (!HudMoveArmed || !button.ButtonPressed || ReadPersistentQueue(worker.Id).Orders.Count != count)
            throw new InvalidOperationException("Arming movement must not create an order.");
        SetHudMoveArmed(false);
        if (button.ButtonPressed || ReadPersistentQueue(worker.Id).Orders.Count != count)
            throw new InvalidOperationException("Disarming movement created an order.");
        button.EmitSignal(Button.SignalName.Pressed);
        var camera = _tacticalPreview!.TacticalCameraState;
        _tacticalPreview.ExerciseHudRightClickMove(target);
        var q = ReadPersistentQueue(worker.Id);
        if (HudMoveArmed || button.ButtonPressed || q.Orders.Count != count + 1 ||
            !q.Orders.Any(o => o.Kind == FoundationWorkerOrderKind.Move && o.Destination == target) ||
            host.StateHash != hash || _tacticalPreview.TacticalCameraState != camera)
            throw new InvalidOperationException("Armed right-click did not queue exactly one move without recentering.");
        GD.Print("GA_NEXT_R23063_MAP_SELECTION_PASS neutral_start=true map_edge=true armed_right_click=true one_order=true hash_unchanged=true");
    }
}
