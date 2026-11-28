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
    public static readonly Color Background = GaGraphiteTokens.Base;
    public static readonly Color Surface = GaGraphiteTokens.Panel;
    public static readonly Color Raised = GaGraphiteTokens.Raised;
    public static readonly Color Header = GaGraphiteTokens.Raised;
    public static readonly Color Border = GaGraphiteTokens.Border;
    public static readonly Color Text = GaGraphiteTokens.PrimaryText;
    public static readonly Color Muted = GaGraphiteTokens.SecondaryText;
    public static readonly Color Accent = GaGraphiteTokens.Focus;
    public static readonly Color AccentDim = GaGraphiteTokens.SubtleBorder;
    public static readonly Color Warm = GaGraphiteTokens.Attention;
    // Existing logical font metrics stay stable until shell migration.
    public const int ApplicationFontSize = 19;
    public const int WindowTitleFontSize = 15;
    public const int TextFontSize = 12;
    public const int ControlFontSize = 12;
    public const int TableFontSize = 11;
    public const int CaptionFontSize = 10;
    internal const int HudIdentityFontSize = 13;
    internal const int HudSectionFontSize = 11;
    internal const int HudMetricFontSize = 12;
    internal const int HudFeedbackFontSize = 11;
    internal static readonly Color HudIdentityText = GaGraphiteTokens.PrimaryText;
    internal static readonly Color HudMetricText = GaGraphiteTokens.SecondaryText;

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

    // Floating surfaces share the HUD's material family but retain their
    // original padding, border width, corner radius and drag geometry.
    public static StyleBoxFlat WindowSurface()
    {
        var box = SurfaceBox(GaHudVisualTokens.Footer, GaHudVisualTokens.Rim,
            radius: 8, borderWidth: 1, innerPadding: 8);
        box.ShadowColor = GaHudVisualTokens.Shadow;
        box.ShadowSize = GaHudVisualTokens.FooterShadow;
        box.ShadowOffset = new Vector2(0, 2);
        return box;
    }

    internal static StyleBoxFlat WindowHeaderSurface() =>
        GaHudEffects.Surface(GaHudPlane.Object, 7);

    internal static void StyleWindowClose(Button button)
    {
        // The standard text button has four pixels of padding; retain
        // that metric instead of the smaller icon-action padding.
        var normal = SurfaceBox(GaHudVisualTokens.Action, GaHudVisualTokens.Rim, 5, 1, 4);
        normal.ShadowColor = GaHudVisualTokens.Shadow;
        normal.ShadowSize = GaHudVisualTokens.ContactShadow;
        normal.ShadowOffset = new Vector2(0, 1);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", HudFlat(GaHudVisualTokens.Hover, GaHudVisualTokens.Focus, 5, 4, 1));
        button.AddThemeStyleboxOverride("pressed", HudFlat(GaHudVisualTokens.Pressed, GaHudVisualTokens.Focus, 5, 4, 1));
        button.AddThemeStyleboxOverride("disabled", HudFlat(GaHudVisualTokens.Disabled, GaHudVisualTokens.RecessRim, 5, 4, 1));
        button.AddThemeStyleboxOverride("focus", HudFlat(Colors.Transparent, GaHudVisualTokens.Focus, 5, 0, 1));
    }

    /// <summary>Dense widget cards only; floating management windows retain normal padding.</summary>
    internal static StyleBoxFlat HudCardSurface() =>
        GaHudEffects.Surface(GaHudPlane.Footer, 5);

    // Local operational cards have two nested surfaces. Avoid paying for
    // the standard window padding twice; the object and action sizes stay intact.
    internal static StyleBoxFlat HudOperationalCardSurface() =>
        GaHudEffects.Surface(GaHudPlane.Structure, 0);

    // A section is a group of widgets, not another outlined form.
    internal static StyleBoxFlat HudFlat(Color fill, Color border, int radius, int padding, int line = 0)
    {
        var box = SurfaceBox(fill, border, radius, line, padding);
        box.ShadowSize = 0;
        return box;
    }

    internal static StyleBoxFlat HudDockSurface() =>
        GaHudEffects.Surface(GaHudPlane.Atmosphere, 8);

    internal static StyleBoxFlat HudPortraitSurface() =>
        GaHudEffects.Surface(GaHudPlane.Portrait, 2);

    internal static void StyleHudAction(Button button)
    {
        GaHudEffects.StyleAction(button);
    }

    internal static void StyleHudSectionHeader(Button header)
    {
        header.AddThemeStyleboxOverride("normal", HudFlat(Background, Background, 0, 0));
        header.AddThemeStyleboxOverride("hover", HudFlat(Surface, Surface, 3, 0));
        header.AddThemeStyleboxOverride("pressed", HudFlat(Raised, Raised, 3, 0));
        header.AddThemeStyleboxOverride("focus", HudFlat(Colors.Transparent, Accent, 3, 0, 1));
        header.AddThemeColorOverride("font_color", Muted);
        header.AddThemeFontSizeOverride("font_size", HudSectionFontSize);
    }

    // Explicit information roles on native controls; no new fonts, nodes,
    // layout passes or runtime polling. The identity is the primary readout.
    internal static void StyleHudIdentity(Control label)
    {
        label.AddThemeFontSizeOverride("font_size", HudIdentityFontSize);
        label.AddThemeColorOverride("font_color", HudIdentityText);
    }

    internal static void StyleHudMetric(Label label)
    {
        label.AddThemeFontSizeOverride("font_size", HudMetricFontSize);
        label.AddThemeColorOverride("font_color", HudMetricText);
    }

    internal static void StyleHudSectionTitle(Label label)
    {
        label.AddThemeFontSizeOverride("font_size", HudSectionFontSize);
        label.AddThemeColorOverride("font_color", Muted);
    }

    internal static void StyleHudFeedback(Label label)
    {
        label.AddThemeFontSizeOverride("font_size", HudFeedbackFontSize);
        label.AddThemeColorOverride("font_color", Warm);
    }

    internal static StyleBoxFlat HudObjectSurface() =>
        GaHudEffects.Surface(GaHudPlane.Object, 3);

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
        result.SetColor("font_disabled_color", "Button", GaGraphiteTokens.DisabledText);
        result.SetColor("font_focus_color", "Button", Text);
        result.SetFontSize("font_size", "Button", ControlFontSize);
        result.SetStylebox("normal", "Button",
            SurfaceBox(Raised, Border, radius: 5, borderWidth: 1, innerPadding: 4));
        result.SetStylebox("hover", "Button",
            SurfaceBox(GaGraphiteTokens.Hover, Border, radius: 5, borderWidth: 1, innerPadding: 4));
        result.SetStylebox("pressed", "Button",
            SurfaceBox(GaGraphiteTokens.Pressed, Border, radius: 5, borderWidth: 1, innerPadding: 4));
        result.SetStylebox("hover_pressed", "Button", result.GetStylebox("pressed", "Button"));
        result.SetStylebox("disabled", "Button",
            SurfaceBox(GaGraphiteTokens.DisabledSurface, AccentDim, radius: 5,
                borderWidth: 1, innerPadding: 4));
        result.SetStylebox("focus", "Button", HudFlat(Colors.Transparent,
            GaGraphiteTokens.Focus, 5, 0, 2));
        // Persistent selection is distinct from transient press.
        result.SetTypeVariation("GraphiteSelectedButton", "Button");
        result.SetStylebox("normal", "GraphiteSelectedButton",
            HudFlat(GaGraphiteTokens.SelectedSurface, GaGraphiteTokens.Attention, 5, 4, 1));
        result.SetStylebox("hover", "GraphiteSelectedButton",
            HudFlat(GaGraphiteTokens.Hover, GaGraphiteTokens.Attention, 5, 4, 1));
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

    // Exercise real Godot theme lookup on a native control. No simulation access.
    internal static void ExerciseGraphiteContract(Theme theme)
    {
        var button = new Button { Theme = theme, Text = "Assign" };
        try
        {
            StyleBoxFlat State(string name) =>
                button.GetThemeStylebox(name) as StyleBoxFlat ??
                throw new InvalidOperationException("Missing Graphite style: " + name);
            var normal = State("normal");
            var hover = State("hover");
            var pressed = State("pressed");
            var disabled = State("disabled");
            var focus = State("focus");
            if (normal.BgColor != GaGraphiteTokens.Raised ||
                hover.BgColor != GaGraphiteTokens.Hover ||
                pressed.BgColor != GaGraphiteTokens.Pressed ||
                disabled.BgColor != GaGraphiteTokens.DisabledSurface ||
                focus.BgColor != Colors.Transparent ||
                focus.BorderColor != GaGraphiteTokens.Focus ||
                focus.BorderWidthTop != 2 ||
                button.GetThemeColor("font_color") != GaGraphiteTokens.PrimaryText ||
                button.GetThemeColor("font_disabled_color") != GaGraphiteTokens.DisabledText ||
                normal.GetMinimumSize() != hover.GetMinimumSize() ||
                normal.GetMinimumSize() != pressed.GetMinimumSize() ||
                normal.GetMinimumSize() != disabled.GetMinimumSize())
                throw new InvalidOperationException("Graphite theme state contract failed.");
            button.ThemeTypeVariation = "GraphiteSelectedButton";
            if (State("normal").BorderColor != GaGraphiteTokens.Attention ||
                State("normal").BgColor != GaGraphiteTokens.SelectedSurface ||
                State("hover").BorderColor != GaGraphiteTokens.Attention ||
                State("normal").GetMinimumSize() != normal.GetMinimumSize() ||
                State("focus").BorderColor != GaGraphiteTokens.Focus)
                throw new InvalidOperationException("Graphite persistent selection contract failed.");
            GD.Print("GA_NEXT_R2371A_GRAPHITE_PASS theme=true states=6 stable_metrics=true");
        }
        finally { button.Free(); }
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
        frame.AddThemeStyleboxOverride("panel", WindowHeaderSurface());
        GaHudEffects.AttachSurface(frame, GaHudPlane.Object);
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
            Text = "✕", TooltipText = "Close window (Esc)",
            CustomMinimumSize = new Vector2(29f, 29f)
        };
        StyleWindowClose(exit);
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
