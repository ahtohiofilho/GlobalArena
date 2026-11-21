using Godot;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R129: only read-side cartographic site selection. A site is temporarily
/// a real inhabited fine tile, NOT a city/settlement WorldState record.
/// Production and Trade tabs are intentionally deferred to their own gate.
/// </summary>
public partial class Main
{
    private Label? _localSiteSelectionQa;
    private ulong? _selectedCartographicSite;

    private void CreateLocalSiteSelectionQaControls(VBoxContainer parent)
    {
        if (_tacticalPreview?.IsCartographicSiteMode != true) return;
        _localSiteSelectionQa = new Label
        {
            Name = "GANextR129CartographicLocalSiteSelection",
            Text = "LOCAL SITE | Select a visible site name on the map",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _localSiteSelectionQa.AddThemeFontSizeOverride("font_size", 13);
        parent.AddChild(_localSiteSelectionQa);
    }

    private void OnCartographicLocalSiteSelected(ulong physicalTileId)
    {
        if (_tacticalPreview?.IsCartographicSiteMode != true ||
            _tacticalHost is null ||
            !_tacticalPreview.LocalSiteMarkerTileIds.Contains(physicalTileId))
            return;
        _selectedCartographicSite = physicalTileId;
        RefreshLocalSiteSelectionQa();
    }

    private void RefreshLocalSiteSelectionQa()
    {
        if (_localSiteSelectionQa is null ||
            _tacticalHost is null ||
            !_selectedCartographicSite.HasValue)
            return;
        ulong tile = _selectedCartographicSite.Value;
        var residents = _tacticalHost.Snapshot.Workers.Workers
            .Where(w => w.TacticalTileId?.FineStrategicCellId.Value == tile)
            .ToArray();
        _localSiteSelectionQa.Text =
            $"LOCAL SITE | Site {tile:D4} | " +
            $"{residents.Length} real residents | " +
            "Prototype only; administrative Production / Trade tabs pending";
        GD.Print($"GA_NEXT_R129_SITE_CONTEXT tile={tile} " +
            $"workers={residents.Length} mutation=false");
    }
}
