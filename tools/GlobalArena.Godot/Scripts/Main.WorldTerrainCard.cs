using Godot;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>Read-only signed fine geography and configured potential.
/// Climate and elevation are dimensionless indices, not physical degrees/metres.
/// No inferred biome, movement rate, market or actual production.</summary>
public partial class Main
{
    private Label? _worldTerrainFacts, _worldTerrainPotential;

    private void CreateWorldTerrainReadout(VBoxContainer body)
    {
        _worldTerrainFacts = WorldLayerPlaceholder("");
        _worldTerrainFacts.Name = "GANextTerrainFacts";
        body.AddChild(_worldTerrainFacts);
        _worldTerrainPotential = WorldLayerPlaceholder("");
        _worldTerrainPotential.Name = "GANextTerrainPotential";
        body.AddChild(_worldTerrainPotential);
        RefreshWorldTerrain();
    }

    private void RefreshWorldTerrain()
    {
        if (_worldTerrainFacts is null || _worldTerrainPotential is null) return;
        _worldTerrainPotential.Text = "";
        var host = _tacticalHost;
        var fine = _r123FineGeography;
        var source = ReadWorldContextNode();
        if (host is null || fine is null || !source.HasValue ||
            source.Value == 0UL || source.Value > (ulong)fine.FineElevationRaw.Count)
        {
            _worldTerrainFacts.Text = "Select a tile or worker.";
            return;
        }
        int index = checked((int)source.Value - 1);
        var node = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, new StrategicCellId(source.Value));
        var surface = fine.SurfaceAtIndex(index);
        string surfaceName = surface switch
        {
            FoundationFineSurfaceKind.Land => "Land",
            FoundationFineSurfaceKind.OpenWater => "Open water",
            FoundationFineSurfaceKind.FirmSeaIce => "Firm sea ice",
            _ => "Unknown surface"
        };
        var adjacent = host.Incidence.FineTopology.Cells[index].AdjacentCellIds
            .Select(id => checked((int)fine.FineElevationRaw[checked((int)id.Value - 1)])).ToArray();
        int slope = AgriculturalLogicalSlope.Estimate(checked((int)fine.FineElevationRaw[index]), adjacent);
        _worldTerrainFacts.Text = $"{surfaceName} | Node {source.Value}\n" +
            $"Temperature index: {GaDisplayNumbers.PhysicalMicro(fine.FineTemperatureRaw[index])}\n" +
            $"Moisture index: {GaDisplayNumbers.PhysicalMicro(fine.FineMoistureRaw[index])}\n" +
            $"Elevation index: {GaDisplayNumbers.PhysicalMicro(fine.FineElevationRaw[index] - fine.SeaLevelRaw)} (sea level = 0)\n" +
            $"Slope index: {GaDisplayNumbers.PhysicalMicro(slope)}\n" +
            $"On foot: {(fine.CanTraverseOnFoot(index) ? "Yes" : "No")} | Ship: {(fine.CanNavigateByShip(index) ? "Yes" : "No")}";
        if (host.AgriculturalProduction is not { } agriculture)
        {
            _worldTerrainPotential.Text = "Production potential unavailable.";
            return;
        }
        var rows = CropCatalog.All.Select(c => $"{c.Id}: {GaDisplayNumbers.PhysicalMicro(agriculture.Resolve(node, c.Id).WholeTurnOutput)}").ToList();
        if (agriculture.BasicResources is { } basic)
        {
            rows.Add($"Wood: {GaDisplayNumbers.PhysicalMicro(surface == FoundationFineSurfaceKind.Land ? basic.WoodWholeTurnOutput : 0UL)}");
            rows.Add($"Stone: {GaDisplayNumbers.PhysicalMicro(surface == FoundationFineSurfaceKind.Land ? basic.StoneWholeTurnOutput : 0UL)}");
        }
        _worldTerrainPotential.Text = "Potential output / full worker-turn (not actual production)\n" + string.Join("\n", rows);
    }

    private void ExerciseWorldTerrain(FoundationTacticalHumanAiTurnHost host, PhysicalWorkerId worker, PhysicalTacticalTileId land)
    {
        var hash = host.StateHash;
        SetWidgetSectionExpanded("Terrain", true);
        var queues = host.GetHumanPersistentQueues(_humanCivilization).ToArray();
        var fine = _r123FineGeography ?? throw new InvalidOperationException("Missing fine geography.");
        var agriculture = host.AgriculturalProduction ?? throw new InvalidOperationException("Missing crop policy.");
        var facts = _worldTerrainFacts ?? throw new InvalidOperationException("Missing terrain facts.");
        var potential = _worldTerrainPotential ?? throw new InvalidOperationException("Missing terrain potential.");
        int index = checked((int)land.FineStrategicCellId.Value - 1);
        if (!facts.Text.StartsWith($"Land | Node {land.FineStrategicCellId.Value}", StringComparison.Ordinal) ||
            !facts.Text.Contains($"Temperature index: {GaDisplayNumbers.PhysicalMicro(fine.FineTemperatureRaw[index])}") ||
            !facts.Text.Contains($"Moisture index: {GaDisplayNumbers.PhysicalMicro(fine.FineMoistureRaw[index])}") ||
            !facts.Text.Contains($"Elevation index: {GaDisplayNumbers.PhysicalMicro(fine.FineElevationRaw[index] - fine.SeaLevelRaw)}") ||
            !facts.Text.Contains("On foot: Yes | Ship: No"))
            throw new InvalidOperationException("Land terrain does not match signed fields.");
        foreach (var crop in CropCatalog.All)
            if (!potential.Text.Split('\n').Contains($"{crop.Id}: {GaDisplayNumbers.PhysicalMicro(agriculture.Resolve(land, crop.Id).WholeTurnOutput)}"))
                throw new InvalidOperationException("Crop potential differs from configured policy.");
        var waterIndex = Enumerable.Range(0, fine.FineElevationRaw.Count)
            .First(i => fine.SurfaceAtIndex(i) == FoundationFineSurfaceKind.OpenWater);
        var water = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters,
            new StrategicCellId((ulong)waterIndex + 1UL));
        // Dispatch the same physical-tile selection callback; no move order.
        OnTacticalPhysicalTileClicked(water);
        if (!facts.Text.StartsWith($"Open water | Node {water.FineStrategicCellId.Value}", StringComparison.Ordinal) ||
            !facts.Text.Contains("On foot: No | Ship: Yes") ||
            CropCatalog.All.Any(c => !potential.Text.Split('\n').Contains($"{c.Id}: 0")) ||
            !potential.Text.Split('\n').Contains("Wood: 0") || !potential.Text.Split('\n').Contains("Stone: 0"))
            throw new InvalidOperationException("Water selection retained land potential or traversal.");
        OnOperationalWorkerSelected(worker.Value);
        if (!facts.Text.StartsWith($"Land | Node {land.FineStrategicCellId.Value}", StringComparison.Ordinal) ||
            host.StateHash != hash || !host.GetHumanPersistentQueues(_humanCivilization).SequenceEqual(queues))
            throw new InvalidOperationException("Terrain selection changed plans or failed to restore worker context.");
        SetWidgetSectionExpanded("Terrain", false);
        GD.Print("GA_NEXT_R2305_TERRAIN_PASS signed_fields=true crop_policy=true land_water=true read_only=true");
    }
}
