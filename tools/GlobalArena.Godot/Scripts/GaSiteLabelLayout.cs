using Godot;

namespace GlobalArena.Godot;

// Screen-space labels only. Never suppress or move the physical population dot.
internal static class GaSiteLabelLayout
{
    internal readonly record struct Site(ulong Id, int Population, Rect2 Dot, Vector2 TextSize);
    internal static Dictionary<ulong, Rect2> Place(Vector2 viewport, IReadOnlyList<Site> sites)
    {
        var result = new Dictionary<ulong, Rect2>();
        var bounds = new Rect2(Vector2.Zero, viewport);
        foreach (var site in sites.OrderByDescending(s => s.Population).ThenBy(s => s.Id))
        {
            var d = site.Dot;
            var size = site.TextSize;
            var c = d.Position + d.Size * 0.5f;
            const float gap = 4f;
            float left = d.Position.X - gap - size.X, right = d.End.X + gap;
            float top = d.Position.Y - gap - size.Y, bottom = d.End.Y + gap;
            float middleX = c.X - size.X * 0.5f, middleY = c.Y - size.Y * 0.5f;
            Vector2[] candidates =
            {
                new(right, middleY), new(left, middleY),
                new(middleX, top), new(middleX, bottom),
                new(right, top), new(left, top),
                new(right, bottom), new(left, bottom)
            };
            foreach (var position in candidates)
            {
                var rect = new Rect2(position, size);
                if (!bounds.Encloses(rect) ||
                    sites.Any(s => rect.Intersects(s.Dot.Grow(2f))) ||
                    result.Values.Any(r => rect.Intersects(r.Grow(2f))))
                    continue;
                result.Add(site.Id, rect);
                break;
            }
        }
        return result;
    }

    internal static void ExerciseContract()
    {
        var sites = new Site[] {
            new(3, 2, new Rect2(140, 60, 6, 6), new Vector2(70, 18)),
            new(2, 4, new Rect2(96, 60, 8, 8), new Vector2(70, 18)),
            new(1, 20, new Rect2(78, 60, 12, 12), new Vector2(80, 22))
        };
        var viewport = new Vector2(260, 150);
        var layout = Place(viewport, sites);
        var reverse = Place(viewport, sites.Reverse().ToArray());
        if (layout.Count != 3 || layout.Any(p => !reverse.TryGetValue(p.Key, out var r) || r != p.Value) ||
            layout.Values.Any(r => sites.Any(s => r.Intersects(s.Dot.Grow(2f)))) ||
            layout.Values.SelectMany((a, i) => layout.Values.Skip(i+1).Select(b => (a,b))).Any(p => p.a.Intersects(p.b.Grow(2f))) ||
            Place(new Vector2(12,12), sites).Count != 0)
            throw new InvalidOperationException("Site name layout contract failed.");
    }
}
