namespace GlobalArena.Runtime;

public readonly record struct CommodityId
{
    private readonly uint _value;

    public uint Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default CommodityId is not a valid identity.");
            }

            return _value;
        }
    }

    public bool IsValid => _value != 0U;

    public CommodityId(
        uint value)
    {
        if (value == 0U)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Commodity ID must be greater than zero.");
        }

        _value = value;
    }
}
