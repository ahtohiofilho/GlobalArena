namespace GlobalArena.Runtime.PersistentOrders;

public enum FoundationWorkerBaseMode { Manual = 1, Automatic = 2 }

/// <summary>Persistent preference, not actual occupation or productive effort.</summary>
public sealed record FoundationWorkerBaseActivity
{
    public FoundationWorkerBaseMode Mode { get; }
    public PhysicalWorkerOccupation? Occupation { get; }
    public byte? Crop { get; }

    public FoundationWorkerBaseActivity(FoundationWorkerBaseMode mode,
        PhysicalWorkerOccupation? occupation = null, byte? crop = null)
    {
        if (mode == FoundationWorkerBaseMode.Automatic)
        {
            if (occupation.HasValue || crop.HasValue)
                throw new ArgumentException("Automatic base has no manual payload.");
        }
        else if (mode == FoundationWorkerBaseMode.Manual)
        {
            if (occupation is not (PhysicalWorkerOccupation.Agriculture or PhysicalWorkerOccupation.Forestry
                or PhysicalWorkerOccupation.Mining or PhysicalWorkerOccupation.LandTransport
                or PhysicalWorkerOccupation.PortOperations) ||
                (crop.HasValue && (occupation != PhysicalWorkerOccupation.Agriculture || crop.Value < 1 || crop.Value > 6)))
                throw new ArgumentException("Invalid manual base activity.");
        }
        else throw new ArgumentException("Unknown base activity mode.");
        Mode = mode;
        Occupation = occupation;
        Crop = crop;
    }
}
