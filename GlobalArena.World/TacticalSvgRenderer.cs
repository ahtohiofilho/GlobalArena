using System.Globalization;
using System.Text;

namespace GlobalArena.World;

/// <summary>Produces a real standalone SVG image from canonical tactical draw polygons.</summary>
public static class TacticalSvgRenderer
{
    public static string Render(TacticalViewportDrawData data, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
            throw new ArgumentOutOfRangeException(nameof(width), "Both dimensions must be between 1 and 16384.");
        var svg = new StringBuilder();
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"")
           .Append(width.ToString(CultureInfo.InvariantCulture))
           .Append("\" height=\"").Append(height.ToString(CultureInfo.InvariantCulture))
           .Append("\" viewBox=\"0 0 ").Append(width.ToString(CultureInfo.InvariantCulture))
           .Append(' ').Append(height.ToString(CultureInfo.InvariantCulture)).Append("\">\n")
           .Append("<rect width=\"100%\" height=\"100%\" fill=\"#0c1723\"/>\n");
        foreach (var polygon in data.Polygons)
        {
            if (polygon.Vertices.Count < 3)
                throw new ArgumentException("A visible polygon must have at least three vertices.", nameof(data));
            svg.Append("<polygon data-tile-id=\"")
               .Append(polygon.TileId.FineStrategicCellId.Value.ToString(CultureInfo.InvariantCulture))
               .Append("\" points=\"");
            foreach (var point in polygon.Vertices)
            {
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) ||
                    point.X < -1.000000001d || point.X > 1.000000001d ||
                    point.Y < -1.000000001d || point.Y > 1.000000001d)
                    throw new ArgumentException("Projected polygon coordinates must be finite and normalized.", nameof(data));
                var px = (point.X + 1d) * width * 0.5d;
                var py = (1d - point.Y) * height * 0.5d;
                svg.Append(px.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                   .Append(py.ToString("F4", CultureInfo.InvariantCulture)).Append(' ');
            }
            svg.Append("\" fill=\"#547c91\" stroke=\"#bfd3dd\" stroke-width=\"0.8\" stroke-linejoin=\"round\"/>\n");
        }
        return svg.Append("</svg>\n").ToString();
    }
}
