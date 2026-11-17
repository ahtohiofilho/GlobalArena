using System;
using System.Globalization;
using GlobalArena.Simulation.Foundation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// A role slot that accepts only a real, locally controlled worker on the same
/// physical tile. Dropping requests the existing v11 order; it never changes a
/// census, tile, or occupation directly.
/// </summary>
public partial class GaWorkerBoardDropZone : PanelContainer
{
    public ulong TileId { get; set; }
    public ulong OwnerId { get; set; }
    public PhysicalWorkerOccupation Role { get; set; }
    public Func<ulong, bool>? CanAcceptWorker { get; set; }
    public Action<ulong, PhysicalWorkerOccupation>? OccupationRequested { get; set; }

    private bool TryReadWorker(Variant data, out ulong workerId)
    {
        workerId = 0UL;
        if (data.VariantType != Variant.Type.String) return false;
        var parts = data.AsString().Split(':');
        return parts.Length == 4 &&
            parts[0] == "GA-NEXT-WORKER" &&
            ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var tile) &&
            ulong.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var owner) &&
            ulong.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out workerId) &&
            tile == TileId && owner == OwnerId && workerId != 0UL;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        TryReadWorker(data, out var workerId) &&
        CanAcceptWorker?.Invoke(workerId) == true;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_CanDropData(atPosition, data) &&
            TryReadWorker(data, out var workerId))
            OccupationRequested?.Invoke(workerId, Role);
    }
}
