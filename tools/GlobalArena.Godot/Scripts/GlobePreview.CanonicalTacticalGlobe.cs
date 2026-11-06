using System;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-UI-VISUAL-02: the global globe is a real spherical G(18,0)
/// PHYSICAL tile presentation, colored by the same canonical terrain
/// source and palette as the local tactical viewer. The G(3,0)
/// strategic geometry is retained invisibly for the existing focus picker.
/// No political borders, edge lines, invented biome samples or state mutation.
/// </summary>
public partial class GlobePreview
{
    private MeshInstance3D? _canonicalTacticalSurface;
    private int _canonicalTacticalCellCount;
    private WorldGenerationResult? _canonicalPhysicalWorld;
    private WorldGenerationV2PhysicalComposition? _canonicalPhysicalTerrain;
    private CanonicalPhysicalVisualLayer _physicalVisualLayer =
        CanonicalPhysicalVisualLayer.Biomes;

    public CanonicalPhysicalVisualLayer PhysicalVisualLayer => _physicalVisualLayer;

    /// <summary>Switch only the paint. Preserve camera and real G18 picking.</summary>
    public void SetPhysicalVisualLayer(CanonicalPhysicalVisualLayer layer)
    {
        if (layer is not (CanonicalPhysicalVisualLayer.Biomes or
            CanonicalPhysicalVisualLayer.Relief))
            throw new ArgumentOutOfRangeException(nameof(layer));
        if (_physicalVisualLayer == layer) return;
        _physicalVisualLayer = layer;
        if (_canonicalPhysicalWorld is null) return;

        var oldPicked = _pickedLabel?.Text;
        var oldDetails = _detailsLabel?.Text;
        BindCanonicalTacticalSurface(
            _canonicalPhysicalWorld, _canonicalPhysicalTerrain);
        if (_selectedCellId.HasValue)
        {
            if (_pickedLabel is not null) _pickedLabel.Text = oldPicked ?? "";
            if (_detailsLabel is not null) _detailsLabel.Text = oldDetails ?? "";
        }
        GD.Print($"GA_UI_PHYSICAL_LAYER_GLOBE switched={layer} " +
            $"tiles={_canonicalTacticalCellCount} camera_preserved=true " +
            "canonical_state_unchanged=true");
    }

    public bool HasBoundCanonicalTacticalSurface =>
        _canonicalTacticalSurface?.Mesh is not null;

    public int CanonicalTacticalCellCount => _canonicalTacticalCellCount;

    public void BindCanonicalTacticalSurface(
        WorldGenerationResult physicalWorld,
        WorldGenerationV2PhysicalComposition? canonicalTerrain)
    {
        ArgumentNullException.ThrowIfNull(physicalWorld);
        if (physicalWorld.Request.StrategicParameters !=
            new GoldbergParameters(18, 0))
            throw new ArgumentException(
                "The global physical globe must use the active G(18,0) tactical grid.",
                nameof(physicalWorld));
        var geometry = physicalWorld.StrategicSphericalGeometry;
        if (geometry.Cells.Count != physicalWorld.StrategicTopology.Cells.Count ||
            geometry.Cells.Count != 3242 ||
            (canonicalTerrain is not null &&
             (canonicalTerrain.Biomes.Count != geometry.Cells.Count ||
              canonicalTerrain.Geometry.Cells.Count != geometry.Cells.Count)))
            throw new ArgumentException(
                "Physical global terrain is not aligned with the active canonical tile geometry.",
                nameof(canonicalTerrain));
        if (_planet is null || _surfaceInstance is null ||
            _highResSurface is null)
            throw new InvalidOperationException(
                "The strategic camera and mesh must be initialized before binding actual tactical terrain.");

        _canonicalPhysicalWorld = physicalWorld;
        _canonicalPhysicalTerrain = canonicalTerrain;
        var minElevation = long.MaxValue;
        var maxElevation = long.MinValue;
        if (canonicalTerrain is not null)
        {
            foreach (var value in canonicalTerrain.Elevation)
            {
                if (value < minElevation) minElevation = value;
                if (value > maxElevation) maxElevation = value;
            }
        }
        var builder = new SurfaceTool();
        builder.Begin(Mesh.PrimitiveType.Triangles);
        var triangles = 0;
        var land = 0;
        foreach (var cell in geometry.Cells)
        {
            int index = checked((int)cell.CellId.Value - 1);
            if (index < 0 || index >= geometry.Cells.Count ||
                cell.BoundaryVertexIds.Count is not (5 or 6))
                throw new InvalidOperationException(
                    "Canonical G(18,0) polygon identities are invalid.");

            Color tint;
            if (canonicalTerrain is not null)
            {
                var physicalBiome = canonicalTerrain.Biomes[index];
                tint = CanonicalPhysicalTerrainPalette.FromCanonicalCell(
                    canonicalTerrain, index, _physicalVisualLayer,
                    minElevation, maxElevation);
                if (!physicalBiome.IsWater) land++;
            }
            else
            {
                var id = new StrategicCellId(cell.CellId.Value);
                // Historical V1 saves do not have a calibrated 01C
                // sea-level datum. Keep verified V1 biomes rather than
                // invent a numerical relief map from unrelated samples.
                tint = CanonicalPhysicalTerrainPalette.FromLegacyWorld(
                    physicalWorld, id);
                if (physicalWorld.StrategicPhysicalFields.LandWater.GetKind(id) !=
                    StrategicLandWaterKind.Water) land++;
            }

            var center = ToGodot(cell.Center);
            for (int i = 0; i < cell.BoundaryVertexIds.Count; i++)
            {
                var a = ToGodot(geometry.GetVertex(
                    cell.BoundaryVertexIds[i]).Position);
                var b = ToGodot(geometry.GetVertex(
                    cell.BoundaryVertexIds[(i + 1) %
                        cell.BoundaryVertexIds.Count]).Position);
                AddVertex(builder, center, tint);
                AddVertex(builder, a, tint);
                AddVertex(builder, b, tint);
                triangles++;
            }
        }

        var mesh = builder.Commit();
        builder.Dispose();
        if (mesh is null || triangles != 19440)
            throw new InvalidOperationException(
                $"Canonical tactical globe triangulation mismatch: {triangles}.");

        if (_canonicalTacticalSurface is null)
        {
            _canonicalTacticalSurface = new MeshInstance3D
            {
                Name = "GA_CanonicalPhysicalTacticalGlobe",
                MaterialOverride = _surfaceInstance.MaterialOverride
            };
            _planet.AddChild(_canonicalTacticalSurface);
        }
        var old = _canonicalTacticalSurface.Mesh;
        _canonicalTacticalSurface.Mesh = mesh;
        old?.Dispose();
        _canonicalTacticalCellCount = geometry.Cells.Count;

        // The coarse and experimental V1 preview meshes are diagnostic only.
        // They never cover or override the active physical globe.
        _surfaceInstance.Visible = false;
        _highResSurface.Visible = false;
        _canonicalTacticalSurface.Visible = true;
        _mapMode = 0;
        if (_mapLayerSelector is not null)
        {
            _mapLayerSelector.Select(0);
            _mapLayerSelector.Visible = false;
        }
        if (_mapLegend is not null)
            _mapLegend.Visible = false;
        if (_pickedLabel is not null)
            _pickedLabel.Text = "G(18,0) | malha tática global";
        if (_detailsLabel is not null)
            _detailsLabel.Text = _physicalVisualLayer ==
                CanonicalPhysicalVisualLayer.Relief && canonicalTerrain is not null
                ? "Relevo físico 01C | clique para escolher uma região"
                : "Terreno físico canônico | clique para escolher uma região";

        GD.Print($"GA_UI_GLOBAL_PHYSICAL_GLOBE_R1 bound=true grid=G(18,0) " +
            $"tiles={_canonicalTacticalCellCount} triangles={triangles} " +
            $"land={land} canonical01C={canonicalTerrain is not null} " +
            $"layer={_physicalVisualLayer} palette=shared_with_tactical overlays=false " +
            "strategic_selection=preserved mutation=false");
    }
}
