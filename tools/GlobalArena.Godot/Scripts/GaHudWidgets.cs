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

    internal static HBoxContainer MetricRow(string icon, string value, string tooltip, out Label label)
    {
        var row = new HBoxContainer { TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(new TextureRect { Texture = Icon(icon), CustomMinimumSize = new Vector2(18,18),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore });
        label = new Label { Text = value, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(label);
        return row;
    }
}
