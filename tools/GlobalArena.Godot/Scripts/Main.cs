using Godot;
using System;

namespace GlobalArena.Godot;

// Production UI root. World geometry and simulation remain owned by headless projects.
public partial class Main : Control
{
    private Label? _status;

    public override void _Ready()
    {
        GD.Print("GA_PROD_MAIN_READY_R6");
        var backdrop = new ColorRect { Color = new Color("#0a1725") };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var shell = new MarginContainer();
        shell.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shell.AddThemeConstantOverride("margin_left", 64);
        shell.AddThemeConstantOverride("margin_right", 64);
        shell.AddThemeConstantOverride("margin_top", 56);
        shell.AddThemeConstantOverride("margin_bottom", 56);
        AddChild(shell);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 24);
        shell.AddChild(layout);

        var title = new Label { Text = "GLOBAL ARENA" };
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", new Color("#d9e9f4"));
        layout.AddChild(title);

        var subtitle = new Label { Text = "Planetary strategy  |  Production foundation" };
        subtitle.AddThemeFontSizeOverride("font_size", 19);
        subtitle.AddThemeColorOverride("font_color", new Color("#7eabc4"));
        layout.AddChild(subtitle);

        var description = new Label
        {
            Text = "The production client is now connected to the independent GlobalArena.World project.\nThe navigable globe, interaction and turn loop are subsequent production checkpoints."
        };
        description.AddThemeFontSizeOverride("font_size", 16);
        layout.AddChild(description);

        var button = new Button { Text = "SYSTEM STATUS", CustomMinimumSize = new Vector2(250, 48) };
        button.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        button.Pressed += ShowStatus;
        layout.AddChild(button);

        _status = new Label { Text = "GA-PROD-01.B | Godot client bootstrap" };
        _status.AddThemeColorOverride("font_color", new Color("#90a9bc"));
        layout.AddChild(_status);
        AddChild(new GlobePreview());
    }

    private void ShowStatus()
    {
        if (_status is null) return;
        _status.Text = $"Godot {Engine.GetVersionInfo()["string"]} | .NET production shell | World assembly referenced";
    }
}