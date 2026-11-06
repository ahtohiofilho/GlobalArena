using System;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-UI-LAYERS-01: one unobtrusive discovery point, one direct keyboard
/// shortcut and one shared view-only state for the globe and tactical map.
/// No permanent corner toolbar or duplicate world physics.
/// </summary>
public partial class Main
{
    private CanonicalPhysicalVisualLayer _activePhysicalVisualLayer =
        CanonicalPhysicalVisualLayer.Biomes;
    private MenuButton? _physicalLayerMenu;
    private Label? _physicalLayerIndicator;

    public int ActivePhysicalLayerId => (int)_activePhysicalVisualLayer;

    private void InitializePhysicalLayersMenu(PanelContainer mapRegion)
    {
        var overlay = new Control
        {
            Name = "GAQAVisualLayersOverlay",
            MouseFilter = MouseFilterEnum.Ignore
        };
        mapRegion.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _physicalLayerMenu = new MenuButton
        {
            Name = "GAQAPhysicalLayersMenu",
            Text = "CAMADAS · BIOMAS",
            TooltipText = "Camadas do mapa · pressione M para alternar Biomas/Relevo",
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.AddChild(_physicalLayerMenu);
        _physicalLayerMenu.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        _physicalLayerMenu.OffsetLeft = -158f;
        _physicalLayerMenu.OffsetTop = 9f;
        _physicalLayerMenu.OffsetRight = -9f;
        _physicalLayerMenu.OffsetBottom = 39f;

        var popup = _physicalLayerMenu.GetPopup();
        popup.AddRadioCheckItem("Biomas · padrão", 0);
        popup.AddRadioCheckItem("Relevo físico · 01C", 1);
        popup.IdPressed += id =>
            SetPhysicalVisualLayer((CanonicalPhysicalVisualLayer)id);

        _physicalLayerIndicator = new Label
        {
            Name = "GAQAPhysicalLayerIndicator",
            Text = "Biomas",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        overlay.AddChild(_physicalLayerIndicator);
        SyncPhysicalLayerMenu();
    }

    private void SyncPhysicalLayerMenu()
    {
        if (_physicalLayerMenu is null) return;
        var relief = _activePhysicalVisualLayer ==
            CanonicalPhysicalVisualLayer.Relief;
        _physicalLayerMenu.Text = relief ? "CAMADAS · RELEVO" : "CAMADAS · BIOMAS";
        if (_physicalLayerIndicator is not null)
            _physicalLayerIndicator.Text = relief ? "Relevo" : "Biomas";
        var popup = _physicalLayerMenu.GetPopup();
        for (var i = 0; i < popup.ItemCount; i++)
            popup.SetItemChecked(i,
                popup.GetItemId(i) == (int)_activePhysicalVisualLayer);
    }

    /// <summary>
    /// Presentation change applied to both map views, even when one is
    /// hidden. Do not serialize this choice into canonical world saves.
    /// </summary>
    public void SetPhysicalVisualLayer(CanonicalPhysicalVisualLayer layer)
    {
        if (layer is not (CanonicalPhysicalVisualLayer.Biomes or
            CanonicalPhysicalVisualLayer.Relief))
            throw new ArgumentOutOfRangeException(nameof(layer));
        if (layer == CanonicalPhysicalVisualLayer.Relief &&
            _canonicalPhysicalLab?.CanonicalTerrain is null)
        {
            GD.PushWarning(
                "GA-UI-LAYERS-01: canonical 01C relief unavailable on this historical save.");
            return;
        }
        if (_activePhysicalVisualLayer == layer) return;
        var beforeHash = _canonicalPhysicalLab?.CurrentHash;
        var beforeEvents = _canonicalPhysicalLab?.Events.Count;

        _strategicGlobe?.SetPhysicalVisualLayer(layer);
        _tacticalPreview?.SetPhysicalVisualLayer(layer);
        _activePhysicalVisualLayer = layer;
        SyncPhysicalLayerMenu();

        if (_canonicalPhysicalLab is not null &&
            (beforeHash != _canonicalPhysicalLab.CurrentHash ||
             beforeEvents != _canonicalPhysicalLab.Events.Count))
            throw new InvalidOperationException(
                "Changing presentation must never alter the canonical simulation.");
        GD.Print($"GA_UI_PHYSICAL_LAYERS_01 layer={layer} globe=tactical " +
            "geometry=G18 terrain=01C input=M view_only=true save_unchanged=true");
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo ||
            key.Keycode != Key.M || key.CtrlPressed || key.AltPressed ||
            key.MetaPressed)
            return;

        // An input field, dropdown or popup owns its own keystrokes.
        // Avoid changing the planet while editing a worker quantity.
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit or
            SpinBox or OptionButton or PopupMenu ||
            _physicalLayerMenu?.GetPopup().Visible == true)
            return;

        SetPhysicalVisualLayer(
            _activePhysicalVisualLayer == CanonicalPhysicalVisualLayer.Biomes
                ? CanonicalPhysicalVisualLayer.Relief
                : CanonicalPhysicalVisualLayer.Biomes);
        GetViewport().SetInputAsHandled();
    }
}
