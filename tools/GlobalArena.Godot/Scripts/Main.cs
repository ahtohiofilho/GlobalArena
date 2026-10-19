using Godot;

namespace GlobalArena.Godot;

// GA-UI-FOUNDATION-01: presentation shell owns layout; GlobePreview owns only globe behavior.
public partial class Main : Control
{
    private Label? _status;
    private Label? _tacticalBridgeStatus;
    private Button? _openTacticalContext;
    private ulong? _bridgeSelection;

    private void OnCanonicalTerritorySelectionChanged(ulong? selectedId)
    {
        _bridgeSelection = selectedId;
        if (_openTacticalContext is not null) _openTacticalContext.Disabled = !selectedId.HasValue;
        if (_tacticalBridgeStatus is not null)
            _tacticalBridgeStatus.Text = selectedId.HasValue
                ? $"Territory {selectedId.Value} selected | tactical context available"
                : "Select a canonical territory to enable tactical context";
    }

    private void OpenTacticalContext()
    {
        if (!_bridgeSelection.HasValue || _tacticalBridgeStatus is null) return;
        _tacticalBridgeStatus.Text = $"Tactical focus: canonical territory {_bridgeSelection.Value}. Tactical mesh viewport pending integration.";
        GD.Print($"GA_UI_BRIDGE_01_TACTICAL_FOCUS cell={_bridgeSelection.Value}");
    }

    public override void _Ready()
    {
        GD.Print("GA_PROD_MAIN_READY_R6");
        // Prioritize a maximized production window; preserve windowed resizing and avoid headless changes.
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Maximized);
        var backdrop = new ColorRect { Color = new Color("#0a1725"), MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var frame = new MarginContainer();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddThemeConstantOverride("margin_left", 20);
        frame.AddThemeConstantOverride("margin_right", 20);
        frame.AddThemeConstantOverride("margin_top", 16);
        frame.AddThemeConstantOverride("margin_bottom", 16);
        AddChild(frame);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        frame.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 20);
        root.AddChild(header);
        var title = new Label { Text = "GLOBAL ARENA" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color("#d9e9f4"));
        header.AddChild(title);
        var subtitle = new Label { Text = "Planetary strategy  |  Production foundation", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        subtitle.AddThemeColorOverride("font_color", new Color("#7eabc4"));
        header.AddChild(subtitle);
        var button = new Button { Text = "SYSTEM STATUS" };
        button.Pressed += ShowStatus;
        header.AddChild(button);

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        root.AddChild(content);

        var mapRegion = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddChild(mapRegion);
        var globe = new GlobePreview
        {
            CustomMinimumSize = new Vector2(360f, 300f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mapRegion.AddChild(globe);
        globe.CanonicalTerritorySelectionChanged += OnCanonicalTerritorySelectionChanged;
        GD.Print("GA_UI_VIEWPORT_READY_02 parent=map-region sizing=expand-fill");

        var rightPanel = new PanelContainer { CustomMinimumSize = new Vector2(390f, 0f), SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddChild(rightPanel);
        var rightMargin = new MarginContainer();
        rightMargin.AddThemeConstantOverride("margin_left", 12);
        rightMargin.AddThemeConstantOverride("margin_right", 12);
        rightMargin.AddThemeConstantOverride("margin_top", 12);
        rightMargin.AddThemeConstantOverride("margin_bottom", 12);
        rightPanel.AddChild(rightMargin);
        var rightLayout = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        rightLayout.AddThemeConstantOverride("separation", 8);
        rightMargin.AddChild(rightLayout);
        var panelTitle = new Label { Text = "TERRITORY & WORLD" };
        panelTitle.AddThemeFontSizeOverride("font_size", 17);
        rightLayout.AddChild(panelTitle);
        var tacticalBridgeTitle = new Label { Text = "TACTICAL CONTEXT" };
        tacticalBridgeTitle.AddThemeFontSizeOverride("font_size", 15);
        rightLayout.AddChild(tacticalBridgeTitle);
        _tacticalBridgeStatus = new Label { Text = "Select a canonical territory to enable tactical context" };
        _tacticalBridgeStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rightLayout.AddChild(_tacticalBridgeStatus);
        _openTacticalContext = new Button { Text = "OPEN TACTICAL CONTEXT", Disabled = true };
        _openTacticalContext.Pressed += OpenTacticalContext;
        rightLayout.AddChild(_openTacticalContext);
        var panelScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        rightLayout.AddChild(panelScroll);
        var hud = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hud.AddThemeConstantOverride("separation", 8);
        panelScroll.AddChild(hud);
        globe.TransferHud(hud);
        // Keep the calibration buttons visible even when the context scrolls.
        foreach (var child in hud.GetChildren())
        {
            if (child is HBoxContainer row)
            {
                bool calibrationRow = false;
                foreach (var entry in row.GetChildren())
                    if (entry is Button control && control.Text.StartsWith("LAND ")) calibrationRow = true;
                if (calibrationRow) row.Reparent(rightLayout);
            }
            else if (child is Button next && next.Text == "NEXT SEED")
                next.Reparent(rightLayout);
        }
        GD.Print("GA_UI_CALIBRATION_ACCESS_READY_01 fixed-controls=true");

        _status = new Label { Text = "GA-UI-FOUNDATION-01  |  Map and context areas separated" };
        _status.AddThemeColorOverride("font_color", new Color("#90a9bc"));
        root.AddChild(_status);
        GD.Print("GA_UI_FOUNDATION_READY_01 layout=map,sidebar,header,footer");
        GD.Print("GA_UI_FOUNDATION_RESIZE_READY_02 responsive=true");
    }

    private void ShowStatus()
    {
        if (_status is null) return;
        _status.Text = $"Godot {Engine.GetVersionInfo()["string"]} | .NET production shell | World assembly referenced";
    }
}
