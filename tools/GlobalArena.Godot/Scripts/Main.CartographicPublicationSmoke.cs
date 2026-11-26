using Godot;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
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
