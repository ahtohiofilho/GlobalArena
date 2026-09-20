namespace GlobalArena.World;

public sealed class SharedBorderElement
{
    public SharedBorderElementId Id { get; }

    public SharedBorderElement(
        SharedBorderElementId id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Shared border element ID must be valid.",
                nameof(id));
        }

        Id = id;
    }
}
