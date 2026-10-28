namespace GlobalArena.Simulation;

/// <summary>
/// Persistent, caller-assigned identity of one shared PHYSICAL resource used
/// by aggregate freight. A resource can represent a built segment, handling
/// interface, transfer facility or other cost-bearing physical component.
/// It is deliberately NOT a StrategicEdgeId and does not imply graph access,
/// technology, route direction, construction or settlement.
/// </summary>
public readonly record struct EconomicPhysicalResourceId
{
    private readonly ulong _value;

    public bool IsValid => _value != 0UL;

    public ulong Value => IsValid
        ? _value
        : throw new InvalidOperationException(
            "Default physical resource ID is not a valid identity.");

    public EconomicPhysicalResourceId(ulong value)
    {
        if (value == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(value), "Physical resource identity must be positive.");
        _value = value;
    }
}
