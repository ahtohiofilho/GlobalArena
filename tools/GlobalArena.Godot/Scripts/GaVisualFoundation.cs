using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA Visual Foundation 01: presentation-only design tokens and reusable
/// Godot theme primitives. All styles are generated from a single palette;
/// this class never reads or mutates the authoritative simulation state.
/// Native Control/Theme keeps styling portable and avoids external asset
/// licensing or a bespoke overlay renderer.
/// </summary>
public static class GaVisualFoundation
{
    public static readonly Color Background = new("#101010");
    public static readonly Color Surface = new("#1b1b1b");
    public static readonly Color Raised = new("#292929");
    public static readonly Color Header = new("#303030");
    public static readonly Color Border = new("#454545");
    public static readonly Color Text = new("#dedede");
    public static readonly Color Muted = new("#ababab");
    public static readonly Color Accent = new("#bcbcbc");
    public static readonly Color AccentDim = new("#454545");
    public static readonly Color Warm = new("#c7af82");
    // Compact first-class typography scale. Art palette remains provisional.
    public const int ApplicationFontSize = 19;
    public const int WindowTitleFontSize = 15;
    public const int TextFontSize = 12;
    public const int ControlFontSize = 12;
    public const int TableFontSize = 11;
    public const int CaptionFontSize = 10;

    private static StyleBoxFlat SurfaceBox(
        Color background, Color border, int radius, int borderWidth,
        int innerPadding)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = innerPadding,
            ContentMarginRight = innerPadding,
            ContentMarginTop = innerPadding,
            ContentMarginBottom = innerPadding,
            ShadowColor = new Color(0f, 0f, 0f, 0.36f),
            ShadowSize = 5
        };
    }

    public static StyleBoxFlat WindowSurface() =>
        SurfaceBox(Surface, Border, radius: 8, borderWidth: 1, innerPadding: 8);

    /// <summary>Dense widget cards only; floating management windows retain normal padding.</summary>
    internal static StyleBoxFlat HudCardSurface() =>
        HudFlat(Surface, Surface, 4, 5);

    // Local operational cards have two nested surfaces. Avoid paying for
    // the standard window padding twice; the object and action sizes stay intact.
    internal static StyleBoxFlat HudOperationalCardSurface() =>
        HudFlat(Background, Background, 0, 0);

    // A section is a group of widgets, not another outlined form.
    internal static StyleBoxFlat HudFlat(Color fill, Color border, int radius, int padding, int line = 0)
    {
        var box = SurfaceBox(fill, border, radius, line, padding);
        box.ShadowSize = 0;
        return box;
    }

    internal static StyleBoxFlat HudDockSurface() =>
        HudFlat(Background, Background, 3, 8);

    internal static StyleBoxFlat HudPortraitSurface() =>
        HudFlat(new Color("#151515"), new Color("#393939"), 3, 2, 1);

    internal static void StyleHudAction(Button button)
    {
        button.AddThemeStyleboxOverride("normal", HudFlat(Raised, Border, 4, 3, 1));
        button.AddThemeStyleboxOverride("hover", HudFlat(new Color("#3a3a3a"), Accent, 4, 3, 1));
        button.AddThemeStyleboxOverride("pressed", HudFlat(new Color("#505050"), Accent, 4, 3, 1));
        button.AddThemeStyleboxOverride("disabled", HudFlat(new Color("#202020"), new Color("#303030"), 4, 3, 1));
        button.AddThemeStyleboxOverride("focus", HudFlat(Colors.Transparent, Accent, 4, 0, 1));
    }

    internal static void StyleHudSectionHeader(Button header)
    {
        header.AddThemeStyleboxOverride("normal", HudFlat(Background, Background, 0, 0));
        header.AddThemeStyleboxOverride("hover", HudFlat(Surface, Surface, 3, 0));
        header.AddThemeStyleboxOverride("pressed", HudFlat(Raised, Raised, 3, 0));
        header.AddThemeStyleboxOverride("focus", HudFlat(Colors.Transparent, Accent, 3, 0, 1));
        header.AddThemeColorOverride("font_color", Muted);
        header.AddThemeFontSizeOverride("font_size", CaptionFontSize);
    }

    internal static StyleBoxFlat HudObjectSurface() =>
        HudFlat(Surface, Border, 4, 3, 1);

    public static StyleBoxFlat MapFrame() =>
        SurfaceBox(Background, AccentDim, radius: 5, borderWidth: 1, innerPadding: 0);

    public static Theme BuildTheme()
    {
        var result = new Theme();
        result.SetColor("font_color", "Label", Text);
        result.SetColor("font_shadow_color", "Label", new Color(0f, 0f, 0f, 0.25f));
        result.SetFontSize("font_size", "Label", TextFontSize);
        result.SetColor("font_color", "Button", Text);
        result.SetColor("font_hover_color", "Button", Text);
        result.SetColor("font_pressed_color", "Button", Text);
        result.SetColor("font_disabled_color", "Button", Muted.Darkened(0.3f));
        result.SetFontSize("font_size", "Button", ControlFontSize);
        result.SetStylebox("normal", "Button",
            SurfaceBox(Raised, Border, radius: 5, borderWidth: 1, innerPadding: 4));
        result.SetStylebox("hover", "Button",
            SurfaceBox(new Color("#3a3a3a"), Accent, radius: 5, borderWidth: 1, innerPadding: 4));
        result.SetStylebox("pressed", "Button",
            SurfaceBox(AccentDim, Accent, radius: 5, borderWidth: 1, innerPadding: 4));
        result.SetStylebox("disabled", "Button",
            SurfaceBox(Surface, new Color("#303030"), radius: 5,
                borderWidth: 1, innerPadding: 4));
        result.SetStylebox("focus", "Button", new StyleBoxEmpty());
        result.SetStylebox("panel", "PanelContainer", WindowSurface());
        result.SetColor("font_color", "CheckBox", Text);
        result.SetColor("font_color", "OptionButton", Text);
        result.SetStylebox("normal", "OptionButton",
            SurfaceBox(Raised, Border, radius: 5, borderWidth: 1, innerPadding: 4));
        result.SetStylebox("hover", "OptionButton",
            SurfaceBox(new Color("#3a3a3a"), Accent, radius: 5,
                borderWidth: 1, innerPadding: 4));
        result.SetColor("font_color", "PopupMenu", Text);
        result.SetStylebox("panel", "PopupMenu",
            SurfaceBox(Raised, Border, radius: 5, borderWidth: 1, innerPadding: 5));
        return result;
    }

    public static Label Caption(string caption)
    {
        var label = new Label { Text = caption, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", CaptionFontSize);
        label.AddThemeColorOverride("font_color", Accent);
        return label;
    }

    public static PanelContainer AddWindowHeader(
        VBoxContainer parent, string eyebrow, string title,
        string detail, System.Action close)
    {
        var frame = new PanelContainer
        {
            Name = "GAWindowDragHandle",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        frame.AddThemeStyleboxOverride("panel",
            SurfaceBox(Header, AccentDim, radius: 5, borderWidth: 1,
                innerPadding: 7));
        parent.AddChild(frame);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        frame.AddChild(row);
        var strip = new ColorRect
        {
            Color = Accent,
            CustomMinimumSize = new Vector2(3, 34),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(strip);
        var texts = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        texts.AddThemeConstantOverride("separation", 1);
        row.AddChild(texts);
        texts.AddChild(Caption(eyebrow));
        var main = new Label { Text = title };
        main.AddThemeFontSizeOverride("font_size", WindowTitleFontSize);
        main.MouseFilter = Control.MouseFilterEnum.Ignore;
        main.AddThemeColorOverride("font_color", Text);
        texts.AddChild(main);
        if (!string.IsNullOrEmpty(detail))
        {
            var info = new Label { Text = detail };
            info.AddThemeFontSizeOverride("font_size", CaptionFontSize);
            info.MouseFilter = Control.MouseFilterEnum.Ignore;
            info.AddThemeColorOverride("font_color", Muted);
            texts.AddChild(info);
        }
        var exit = new Button
        {
            Text = "✕", TooltipText = "Fechar janela (Esc)",
            CustomMinimumSize = new Vector2(29f, 29f)
        };
        exit.Pressed += close;
        row.AddChild(exit);
        return frame;
    }

    /// <summary>Compact reusable, read-only data tile; caller supplies
    /// an authoritative value and refreshes its label after each commit.</summary>
    public static PanelContainer CreateMetric(
        string caption, string initialValue, out Label value)
    {
        var card = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride("panel",
            SurfaceBox(Raised, AccentDim, radius: 4, borderWidth: 1,
                innerPadding: 7));
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 2);
        card.AddChild(stack);
        stack.AddChild(Caption(caption));
        value = new Label { Text = initialValue };
        value.AddThemeFontSizeOverride("font_size", 14);
        value.AddThemeColorOverride("font_color", Text);
        stack.AddChild(value);
        return card;
    }

    public static void StylePrimaryAction(Button button)
    {
        button.AddThemeStyleboxOverride("normal",
            SurfaceBox(AccentDim, Accent, radius: 5,
                borderWidth: 1, innerPadding: 4));
        button.AddThemeStyleboxOverride("hover",
            SurfaceBox(new Color("#515151"), Accent, radius: 5,
                borderWidth: 1, innerPadding: 4));
        button.AddThemeColorOverride("font_color", Text);
    }
}
