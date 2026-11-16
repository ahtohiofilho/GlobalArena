using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Reusable, presentation-only internal floating window.
/// Dragging moves its title bar; the lower-right grip resizes the Control.
/// Bounds, minimum dimensions and input ownership are enforced by Godot,
/// without spawning native OS windows or changing the gameplay camera.
/// </summary>
public partial class GaFloatingWindow : PanelContainer
{
    private bool _dragging;
    private bool _resizing;
    private Control? _titleBar;
    private Control? _resizeGrip;
    private Vector2 _pointerOrigin;
    private Vector2 _windowOrigin;
    private Vector2 _sizeOrigin;
    public Vector2 MinimumFloatingSize { get; set; } = new(400f, 320f);

    public void AttachTitleBar(Control titleBar)
    {
        _titleBar = titleBar;
        titleBar.MouseFilter = MouseFilterEnum.Stop;
    }

    public void AttachResizeGrip(Control grip)
    {
        _resizeGrip = grip;
        grip.MouseFilter = MouseFilterEnum.Stop;
    }

    // _Input runs BEFORE GUI focus routing. A Label, Button or focusable
    // Control nested in the title bar must never swallow our mouse-down.
    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree()) return;
        if (@event is InputEventMouseButton mouse &&
            mouse.ButtonIndex == MouseButton.Left)
        {
            if (!mouse.Pressed)
            {
                _dragging = false;
                _resizing = false;
                return;
            }

            var point = mouse.Position;
            var resize = _resizeGrip?.GetGlobalRect().HasPoint(point) == true;
            // Do not steal a click on the rightmost close button.
            var headerRect = _titleBar?.GetGlobalRect() ?? new Rect2();
            var title = _titleBar is not null &&
                headerRect.HasPoint(point) &&
                point.X < headerRect.End.X - 43f;
            if (!resize && !title) return;

            _dragging = title && !resize;
            _resizing = resize;
            _pointerOrigin = point;
            _windowOrigin = Position;
            _sizeOrigin = Size;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseMotion move ||
            (!_dragging && !_resizing))
            return;
        var delta = move.Position - _pointerOrigin;
        if (GetParent() is not Control bounds) return;
        if (_resizing)
        {
            var maxWidth = Mathf.Max(MinimumFloatingSize.X,
                bounds.Size.X - Position.X - 8f);
            var maxHeight = Mathf.Max(MinimumFloatingSize.Y,
                bounds.Size.Y - Position.Y - 8f);
            Size = new Vector2(
                Mathf.Clamp(_sizeOrigin.X + delta.X,
                    MinimumFloatingSize.X, maxWidth),
                Mathf.Clamp(_sizeOrigin.Y + delta.Y,
                    MinimumFloatingSize.Y, maxHeight));
        }
        else
        {
            var limitX = Mathf.Max(8f, bounds.Size.X - Size.X - 8f);
            var limitY = Mathf.Max(8f, bounds.Size.Y - Size.Y - 8f);
            Position = new Vector2(
                Mathf.Clamp(_windowOrigin.X + delta.X, 8f, limitX),
                Mathf.Clamp(_windowOrigin.Y + delta.Y, 8f, limitY));
        }
        GetViewport().SetInputAsHandled();
    }

    public void ClampIntoParent()
    {
        if (GetParent() is not Control bounds) return;
        Size = new Vector2(
            Mathf.Min(Size.X, Mathf.Max(MinimumFloatingSize.X, bounds.Size.X - 16f)),
            Mathf.Min(Size.Y, Mathf.Max(MinimumFloatingSize.Y, bounds.Size.Y - 16f)));
        Position = new Vector2(
            Mathf.Clamp(Position.X, 8f,
                Mathf.Max(8f, bounds.Size.X - Size.X - 8f)),
            Mathf.Clamp(Position.Y, 8f,
                Mathf.Max(8f, bounds.Size.Y - Size.Y - 8f)));
    }
}
