using Godot;

namespace GlobalArena.Godot;

// Presentation only. Shares the atlas mesh/material, never its mutable nodes.
public partial class GaAuxiliaryPlanet : SubViewportContainer
{
    private readonly SubViewport _view;
    private readonly Camera3D _camera;
    private readonly MeshInstance3D _surface;
    private Vector3 _center;
    private Vector3 _north;
    internal int OrientationUpdates { get; private set; }
    internal Mesh? SurfaceMesh => _surface.Mesh;
    internal Material? SurfaceMaterial => _surface.MaterialOverride;
    internal Vector3 CenterGeography => _camera.Position.Normalized();
    internal Vector3 NorthGeography => _camera.Transform.Basis.Y.Normalized();

    public GaAuxiliaryPlanet(Mesh mesh, Material? material)
    {
        Name = "GANextAuxiliaryPlanet";
        CustomMinimumSize = new Vector2(0, 160);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Stretch = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _view = new SubViewport {
            OwnWorld3D = true, TransparentBg = true,
            Size = new Vector2I(280,160),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
        };
        AddChild(_view);
        _surface = new MeshInstance3D { Mesh = mesh, MaterialOverride = material };
        _view.AddChild(_surface);
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 2.22f, Position = new Vector3(0,0,3), Current = true };
        _view.AddChild(_camera);
        VisibilityChanged += RefreshRendering;
        Resized += () => { if (IsVisibleInTree()) RequestFrame(); };
    }

    private void RefreshRendering()
    {
        if (IsVisibleInTree()) RequestFrame();
        else _view.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
    }

    private void RequestFrame() => _view.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

    internal void Synchronize(Vector3 center, Vector3 north)
    {
        if (!IsVisibleInTree() || !center.IsFinite() || !north.IsFinite() ||
            center.LengthSquared() < 0.5f) return;
        center = center.Normalized();
        north = (north - center * north.Dot(center)).Normalized();
        if (north.LengthSquared() < 0.5f) return;
        if (center.DistanceSquaredTo(_center) < 1e-12f &&
            north.DistanceSquaredTo(_north) < 1e-12f) return;
        _center = center;
        _north = north;
        _camera.Position = center * 3f;
        _camera.LookAt(Vector3.Zero, north);
        OrientationUpdates++;
        RequestFrame();
    }
}
