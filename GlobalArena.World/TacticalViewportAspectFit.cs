namespace GlobalArena.World;

/// <summary>Renderer-neutral orthographic camera bounds with the viewport aspect ratio applied.</summary>
public sealed class TacticalViewportAspectFit
{
    public double MinX { get; }
    public double MaxX { get; }
    public double MinY { get; }
    public double MaxY { get; }
    public double AspectRatio { get; }
    public int PhysicalTileCount { get; }

    private TacticalViewportAspectFit(double minX, double maxX,
        double minY, double maxY, double aspect, int tileCount)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        AspectRatio = aspect;
        PhysicalTileCount = tileCount;
    }

    public static TacticalViewportAspectFit Create(TacticalViewportFit fit,
        int pixelWidth, int pixelHeight)
    {
        ArgumentNullException.ThrowIfNull(fit);
        if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
        var aspect = (double)pixelWidth / pixelHeight;
        if (!double.IsFinite(aspect) || aspect <= 0d)
            throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        var centerX = fit.MinX + (fit.MaxX - fit.MinX) * 0.5d;
        var centerY = fit.MinY + (fit.MaxY - fit.MinY) * 0.5d;
        var halfX = (fit.MaxX - fit.MinX) * 0.5d;
        var halfY = (fit.MaxY - fit.MinY) * 0.5d;
        if (!double.IsFinite(halfX) || !double.IsFinite(halfY) || halfX <= 0d || halfY <= 0d)
            throw new ArgumentException("Source fit bounds are invalid.", nameof(fit));
        // Expand only the shorter screen dimension: no selected geometry is cut.
        halfY = Math.Max(halfY, halfX / aspect);
        halfX = halfY * aspect;
        if (!double.IsFinite(halfX) || !double.IsFinite(halfY) ||
            !double.IsFinite(centerX) || !double.IsFinite(centerY))
            throw new ArgumentException("Viewport fitting overflow.", nameof(fit));
        return new TacticalViewportAspectFit(centerX - halfX, centerX + halfX,
            centerY - halfY, centerY + halfY, aspect, fit.PhysicalTileCount);
    }
}