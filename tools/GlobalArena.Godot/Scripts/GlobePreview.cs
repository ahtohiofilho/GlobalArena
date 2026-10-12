using System;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

// GA-PROD-02A: view-only projection of the authoritative spherical geometry.
// This class never creates a second Goldberg topology or changes core IDs.
public partial class GlobePreview : SubViewportContainer
{
    private Node3D? _planet;

    public override void _Ready()
    {
        Position = new Vector2(660, 135);
        Size = new Vector2(550, 490);
        Stretch = true;
        MouseFilter = MouseFilterEnum.Ignore;

        var viewport = new SubViewport
        {
            Size = new Vector2I(550, 490),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            OwnWorld3D = true,
            TransparentBg = true
        };
        AddChild(viewport);

        var world = new Node3D();
        viewport.AddChild(world);

        var request = new WorldGenerationRequest(
            new WorldSeed(20261012UL),
            WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0));
        var generation = new DeterministicWorldGenerator().Generate(request);
        var geometry = generation.StrategicSphericalGeometry;
        var topology = generation.StrategicTopology;

        if (geometry.Cells.Count != topology.Cells.Count
            || geometry.Vertices.Count != topology.Vertices.Count
            || geometry.Cells.Count != 92)
        {
            throw new InvalidOperationException("Authoritative globe geometry/count contract failed.");
        }

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var triangleCount = 0;

        foreach (var cell in geometry.Cells)
        {
            if (cell.BoundaryVertexIds.Count != 5 && cell.BoundaryVertexIds.Count != 6)
            {
                throw new InvalidOperationException("Unexpected Goldberg polygon degree.");
            }

            var center = ToGodot(cell.Center);
            var baseColor = cell.BoundaryVertexIds.Count == 5
                ? new Color(0.34f, 0.66f, 0.81f)
                : (cell.CellId.Value % 7UL < 3UL
                    ? new Color(0.15f, 0.51f, 0.65f)
                    : new Color(0.10f, 0.36f, 0.51f));

            for (var index = 0; index < cell.BoundaryVertexIds.Count; index++)
            {
                var first = ToGodot(geometry.GetVertex(cell.BoundaryVertexIds[index]).Position);
                var second = ToGodot(geometry.GetVertex(
                    cell.BoundaryVertexIds[(index + 1) % cell.BoundaryVertexIds.Count]).Position);

                // Slightly inset the polygon vertices to make canonical borders visible.
                const float inset = 0.965f;
                first = (center + (first - center) * inset).Normalized();
                second = (center + (second - center) * inset).Normalized();
                AddVertex(surface, center, baseColor);
                AddVertex(surface, first, baseColor);
                AddVertex(surface, second, baseColor);
                triangleCount++;
            }
        }

        var arrayMesh = surface.Commit();
        if (arrayMesh is null || triangleCount == 0)
        {
            throw new InvalidOperationException("No canonical globe triangles generated.");
        }

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _planet = new Node3D();
        world.AddChild(_planet);
        _planet.AddChild(new MeshInstance3D { Mesh = arrayMesh, MaterialOverride = material });

        var camera = new Camera3D
        {
            Position = new Vector3(0f, 0.05f, 3.15f),
            Current = true,
            Fov = 48f
        };
        world.AddChild(camera);
        camera.LookAt(Vector3.Zero, Vector3.Up);

        GD.Print($"GA_PROD_GLOBE_READY_02A cells={geometry.Cells.Count} vertices={geometry.Vertices.Count} triangles={triangleCount}");
    }

    public override void _Process(double delta)
    {
        if (_planet is not null)
        {
            _planet.RotateY((float)(delta * 0.12));
        }
    }

    private static void AddVertex(SurfaceTool surface, Vector3 position, Color color)
    {
        surface.SetColor(color);
        surface.AddVertex(position);
    }

    private static Vector3 ToGodot(SphericalPoint3 point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);
}