using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// A draggable visual token for one real worker. Its payload is an identity,
/// not a mutable worker record or an instruction to the simulation.
/// </summary>
public partial class GaWorkerBoardDragChip : Button
{
    public ulong WorkerId { get; set; }
    public ulong TileId { get; set; }
    public ulong OwnerId { get; set; }
    public bool Draggable { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!Draggable) return default;
        SetDragPreview(new Label
        {
            Text = Text,
            CustomMinimumSize = new Vector2(130f, 30f),
            MouseFilter = MouseFilterEnum.Ignore
        });
        return Variant.From($"GA-NEXT-WORKER:{TileId}:{OwnerId}:{WorkerId}");
    }
}
