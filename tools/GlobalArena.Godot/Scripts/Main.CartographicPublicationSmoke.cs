using Godot;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    // Graphical-only acceptance of the player's actual M window. Test
    // composited ROOT pixels, not the private route list or a draw counter.
    internal async Task AssertRenderedStrategicTrade(FoundationTacticalHumanAiTurnHost host, string directory)
    {
        var globe = _strategicGlobe ?? throw new InvalidOperationException("Missing strategic globe.");
        var map = _tacticalPreview ?? throw new InvalidOperationException("Missing tactical map.");
        if (_planetWindow is null || _planetWindow.Visible ||
            host.GetLastClosedEconomicTradeView(_humanCivilization).UsedEdges.Count == 0)
            throw new InvalidOperationException("Render acceptance requires a closed atlas and real freight.");
        var hash = host.StateHash;
        var focus = map.CenterGeography;
        var zoom = map.TacticalCameraState.Zoom;
        Directory.CreateDirectory(directory);
        async Task Frames()
        {
            for (int i = 0; i < 12; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        async Task PressM()
        {
            var viewport = GetViewport();
            viewport.PushInput(new InputEventKey { Keycode = Key.M, Pressed = true, Echo = false }, true);
            await Frames();
            viewport.PushInput(new InputEventKey { Keycode = Key.M, Pressed = false, Echo = false }, true);
            await Frames();
        }
        async Task<Image> Capture(string name)
        {
            await Frames();
            var image = GetViewport().GetTexture().GetImage();
            if (image is null || image.IsEmpty() ||
                image.SavePng(Path.Combine(directory, name + ".png")) != Error.Ok)
                throw new InvalidOperationException("Strategic composed screenshot failed.");
            image.Convert(Image.Format.Rgba8);
            return image;
        }
        await PressM();
        if (!_planetWindow.Visible || !globe.IsVisibleInTree())
            throw new InvalidOperationException("M did not expose the real atlas.");
        globe.FocusFirstPublishedTradeForRenderSmoke();
        globe.SetSphericalSettledTradeRoutesVisible(false);
        var off = await Capture("01_atlas_routes_off");
        globe.SetSphericalSettledTradeRoutesVisible(true);
        var on = await Capture("02_atlas_routes_on");
        globe.SetSphericalSettledTradeRoutesVisible(false);
        var offAgain = await Capture("03_atlas_routes_off_again");
        if (off.GetSize() != on.GetSize() || off.GetSize() != offAgain.GetSize())
            throw new InvalidOperationException("Atlas extent changed during route toggle.");
        byte[] a = off.GetData(), b = on.GetData(), c = offAgain.GetData();
        int black = 0, white = 0;
        for (int i = 0; i < a.Length; i += 4)
        {
            bool returned = Math.Abs(a[i] - c[i]) <= 2 && Math.Abs(a[i+1] - c[i+1]) <= 2 &&
                Math.Abs(a[i+2] - c[i+2]) <= 2;
            int delta = Math.Abs(a[i] - b[i]) + Math.Abs(a[i+1] - b[i+1]) + Math.Abs(a[i+2] - b[i+2]);
            if (!returned || delta < 45) continue;
            if (b[i] <= 32 && b[i+1] <= 32 && b[i+2] <= 32) black++;
            if (b[i] >= 220 && b[i+1] >= 220 && b[i+2] >= 220) white++;
        }
        if (black < 4 || white < 4)
            throw new InvalidOperationException($"Atlas has no reversible black/white trade pixels: black={black}, white={white}.");
        globe.SetSphericalSettledTradeRoutesVisible(true);
        globe.CenterOnGeography(focus);
        await PressM();
        if (_planetWindow.Visible || host.StateHash != hash ||
            focus.Dot(map.CenterGeography) < 0.99999f || Math.Abs(map.TacticalCameraState.Zoom - zoom) > 0.00001f)
            throw new InvalidOperationException("Atlas pixel test changed world or tactical navigation.");
        GD.Print($"GA_NEXT_R23110_ATLAS_PIXELS_PASS black={black} white={white} root_frame=true real_m=true reversible=true hash_unchanged=true");
    }

    internal void AssertCartographicPublication(FoundationTacticalHumanAiTurnHost host, ulong cycle)
    {
        var hash = host.StateHash;
        var view = host.GetLastClosedEconomicTradeView(_humanCivilization);
        var map = _tacticalPreview ?? throw new InvalidOperationException("Missing tactical map.");
        var globe = _strategicGlobe ?? throw new InvalidOperationException("Missing strategic globe.");
        ulong? expectedCycle = cycle == 0UL ? null : cycle;
        var expected = map.ExpectedResidentTradeEdges(view);
        if (map.PresentedEconomicCycleOrdinal != expectedCycle ||
            globe.PresentedSphericalTradeCycle != expectedCycle ||
            !map.DrawnTradeEdgeIds.SequenceEqual(expected) ||
            map.VisibleSettledTradeCrossings != expected.Count ||
            map.IsSettledTradeLayerVisible != (expected.Count > 0) ||
            globe.VisibleSphericalSettledTradeCrossings != view.UsedEdges.Count ||
            globe.HasSphericalTradeInk != (view.UsedEdges.Count > 0) ||
            host.StateHash != hash)
            throw new InvalidOperationException($"Cartographic publication mismatch at cycle {cycle}: host={view.UsedEdges.Count}, tactical={map.VisibleSettledTradeCrossings}, expected={expected.Count}, globe={globe.VisibleSphericalSettledTradeCrossings}");
        globe.AssertSphericalTradeGeometry(view);
        map.AssertSitePopulationPresentation();
        GD.Print($"GA_NEXT_R2311_TRADE_PUBLICATION cycle={cycle} authorized={view.UsedEdges.Count} resident={expected.Count} globe={globe.VisibleSphericalSettledTradeCrossings} hash_unchanged=true");
    }
}
