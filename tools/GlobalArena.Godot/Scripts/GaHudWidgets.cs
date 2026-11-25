using Godot;

namespace GlobalArena.Godot;

/// <summary>Shared original line pictograms and compact HUD controls.
/// Textures are cached; no external fonts, emoji or external art assets.
/// Keep semantic actions in callers, not in these presentation primitives.</summary>
internal static class GaHudWidgets
{
    private static readonly Dictionary<string, Texture2D> Icons = new();

    internal static Texture2D Icon(string key)
    {
        if (Icons.TryGetValue(key, out var cached)) return cached;
        string path = key switch
        {
            "unit" => "M12 3a3 3 0 1 0 0 6a3 3 0 1 0 0-6 M5 21v-3a7 7 0 0 1 14 0v3",
            "move" => "M12 2v20 M2 12h20 M8 6l4-4 4 4 M8 18l4 4 4-4 M6 8l-4 4 4 4 M18 8l4 4-4 4",
            "cancel" => "M6 6l12 12 M18 6L6 18",
            "connection" => "M12 12L4 5 M12 12l8-7 M12 12v9 M2 5a2 2 0 1 0 4 0a2 2 0 1 0-4 0 M18 5a2 2 0 1 0 4 0a2 2 0 1 0-4 0 M10 21a2 2 0 1 0 4 0a2 2 0 1 0-4 0 M10 12a2 2 0 1 0 4 0a2 2 0 1 0-4 0",
            "terrain" => "M2 20l7-13 4 7 4-10 5 16Z",
            "globe" => "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20 M2 12h20 M12 2c-7 6-7 14 0 20 M12 2c7 6 7 14 0 20",
            "city" => "M3 21V9h6v12 M9 21V3h6v18 M15 21V7h6v14 M2 21h20 M11 7h2 M11 11h2 M5 13h2 M17 11h2",
            "civilization" => "M12 2l9 5v6c0 5-4 8-9 9-5-1-9-4-9-9V7Z M8 12l3 3 5-6",
            "auto" => "M20 9a8 8 0 0 0-14-4L3 8 M3 3v5h5 M4 15a8 8 0 0 0 14 4l3-3 M21 21v-5h-5",
            "queue" => "M8 5h13 M8 12h13 M8 19h13 M3 5h1 M3 12h1 M3 19h1",
            "pending" => "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20 M12 6v6l4 2",
            "check" => "M4 12l5 5L20 6",
            "active" => "M8 4l12 8-12 8Z",
            "details" => "M4 3h16v18H4Z M8 8h8 M8 12h8 M8 16h5",
            "wood" => "M12 2L5 11h4l-5 7h16l-5-7h4Z M12 18v4",
            "stone" => "M2 17l4-10 9-4 7 9-3 8H6Z M6 7l6 6 3-10 M12 13l7 7",
            "crop" => "M12 22V3 M12 8C4 8 4 3 4 3c6 0 8 5 8 5 M12 14c8 0 8-5 8-5-6 0-8 5-8 5 M12 19c-8 0-8-5-8-5 6 0 8 5 8 5",
            "edge" => "M5 12h14 M5 9v6 M19 9v6",
            "capital" => "M12 2v20 M17 6H9a3 3 0 0 0 0 6h6a3 3 0 0 1 0 6H7",
            "freight" => "M3 5h18v14H3Z M3 10h18 M10 5v14",
            "build" => "M3 21l9-9 M10 4l4-2 8 8-4 4-3-3-2 2-5-5Z",
            "down" => "M5 9l7 7 7-7",
            "right" => "M9 5l7 7-7 7",
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        var image = new Image();
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'><path d='{path}' fill='none' stroke='#e8f1f4' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round'/></svg>";
        if (image.LoadSvgFromString(svg) != Error.Ok)
            throw new InvalidOperationException($"HUD pictogram failed: {key}");
        var texture = ImageTexture.CreateFromImage(image);
        Icons.Add(key, texture);
        return texture;
    }

    internal static void IconButton(Button button, string icon, string tooltip)
    {
        button.Text = "";
        button.Icon = Icon(icon);
        button.IconAlignment = HorizontalAlignment.Center;
        button.TooltipText = tooltip;
        button.CustomMinimumSize = new Vector2(36, 30);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    internal static void CompactProgress(ProgressBar progress)
    {
        progress.CustomMinimumSize = new Vector2(0, 8);
        progress.ShowPercentage = false;
    }

    internal static TextureRect MetricIcon(string icon) => new()
    {
        Texture = Icon(icon), CustomMinimumSize = new Vector2(18,18),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    internal static HBoxContainer MetricRow(string icon, string value, string tooltip, out Label label)
    {
        var row = new HBoxContainer { TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(MetricIcon(icon));
        label = new Label { Text = value, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(label);
        return row;
    }
}
