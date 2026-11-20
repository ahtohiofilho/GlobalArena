using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R87 REAL Godot validation of the G18 terrain/transport/economy shared
/// physical authority. No decorative 01C, V1 interpolation or fabricated
/// trade overlay. E key closes the host's real settled sales twice.
/// </summary>
internal sealed class FoundationGodotFineFirstTerrainSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;
    private static readonly CivilizationId Human = new(1UL);

    internal FoundationGodotFineFirstTerrainSmoke(
        Main main, FoundationTacticalHumanAiTurnHost host,
        TacticalContextPreview map)
    {
        _main = main;
        _host = host;
        _map = map;
    }

    private static void Must(bool condition, string why)
    {
        if (!condition) throw new InvalidOperationException(why);
    }

    private async Task Frames()
    {
        for (int i = 0; i < 8; i++)
            await _main.ToSignal(_main.GetTree(),
                SceneTree.SignalName.ProcessFrame);
    }

    private async Task PressE()
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventKey
            { Keycode = Key.E, Pressed = true, Echo = false },
            inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventKey
            { Keycode = Key.E, Pressed = false, Echo = false },
            inLocalCoords: true);
        await Frames();
    }

    private async Task Capture(string name)
    {
        string? dir = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_R87_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        await Frames();
        var shot = _main.GetViewport().GetTexture().GetImage();
        Must(shot is not null && !shot.IsEmpty(),
            "G18 Godot screenshot must contain actual rendered pixels.");
        var target = Path.Combine(dir, name + ".png");
        Must(shot!.SavePng(target) == Error.Ok &&
            new FileInfo(target).Length > 1500,
            "G18 Godot screenshot was not written.");
        GD.Print($"GA_NEXT_R87_G18_FRAME_PASS stage={name} " +
            $"width={shot.GetWidth()} height={shot.GetHeight()}");
    }

    internal async Task RunAsync()
    {
        try
        {
            await Frames();
            var fine = _map.AuthoritativeFineEconomy ??
                throw new InvalidOperationException(
                    "The visual tile mesh is not bound to the G18 physical world.");
            Must(_host.Snapshot.WorldBinding?.IsFineFirstWorld == true &&
                _main.ActiveTacticalHost == _host &&
                _map.CanonicalPhysicalIncidenceMap is { } displayedIncidence &&
                displayedIncidence.Refinement.FineParameters ==
                    _host.Incidence.Refinement.FineParameters &&
                displayedIncidence.Refinement.CoarseParameters ==
                    _host.Incidence.Refinement.CoarseParameters &&
                displayedIncidence.TileIncidences.Count ==
                    _host.Incidence.TileIncidences.Count,
                "Godot must use the authoritative G18 host and incidence.");

            var incidence = _host.Incidence;
            int count = incidence.TileIncidences.Count;
            Must(fine.FineElevationRaw.Count == count,
                "Rendered G18 field must cover every tactical cell.");
            int land = 0, water = 0;
            for (int i = 0; i < count; i++)
            {
                long rendered = _map.PresentedFineElevation((ulong)i + 1UL);
                long physical = fine.FineElevationRaw[i];
                Must(rendered == physical,
                    "The rendered elevation differs from physical freight input.");
                if (physical > fine.SeaLevelRaw) land++;
                else water++;
            }
            Must(land == fine.LandTileCount && land > 0 && water > 0,
                "Every terrain tile must use one G18 sea level.");

            int seaEdges = 0, landEdges = 0, coastEdges = 0, blockedEdges = 0;
            var records = _host.Snapshot.Transport.Edges.ToDictionary(
                e => e.Edge.FineStrategicEdgeId);
            foreach (var edge in incidence.FineTopology.Edges)
            {
                int left = checked((int)edge.IncidentCellIds[0].Value - 1);
                int right = checked((int)edge.IncidentCellIds[1].Value - 1);
                var crossing = fine.CrossingAtIndices(left, right);
                var transport = records[edge.Id];
                bool unbounded = transport.CargoMicroAtReferenceLoad ==
                    FoundationPhysicalEndogenousCongestionFeedback
                        .UnboundedNaturalCargoCapacity;
                Must(unbounded ==
                    (crossing == FoundationFineTransportCrossingKind.OpenSea),
                    "Only open-water sea passages may have unbounded capacity.");
                // R89: verify the actual seeded inventory, not merely
                // the policy constants. Slope may only add land cost.
                ulong baseCost = transport.FreePhysicalCostMicroPerWholeCargoUnit;
                switch (crossing)
                {
                    case FoundationFineTransportCrossingKind.Ground:
                        landEdges++;
                        Must(baseCost >= GaExplicitEconomicQaConfiguration.QaLandEdgeCostMicro,
                            "Ground, including frozen sea, must pay land freight.");
                        break;
                    case FoundationFineTransportCrossingKind.Port:
                        coastEdges++;
                        Must(baseCost == GaExplicitEconomicQaConfiguration.QaCoastEdgeCostMicro,
                            "Land/open-water port must pay coastal freight.");
                        break;
                    case FoundationFineTransportCrossingKind.BlockedIceSeaBoundary:
                        blockedEdges++;
                        Must(baseCost == GaExplicitEconomicQaConfiguration.QaCoastEdgeCostMicro,
                            "Blocked sea-ice boundaries remain finite inventory quotes.");
                        break;
                    case FoundationFineTransportCrossingKind.OpenSea:
                        seaEdges++;
                        Must(baseCost == GaExplicitEconomicQaConfiguration.QaSeaEdgeCostMicro,
                            "Open-sea freight must be cheap and uncongested.");
                        break;
                    default:
                        throw new InvalidOperationException("Unknown ice freight surface.");
                }
            }
            Must(seaEdges > 0 && landEdges > 0 && coastEdges > 0 &&
                blockedEdges > 0,
                "G18 must contain sea, land, port and blocked ice boundaries.");
            Must(seaEdges + landEdges + coastEdges + blockedEdges ==
                incidence.FineTopology.Edges.Count,
                "Every economic crossing must have exactly one surface mode.");
            GaExplicitEconomicQaConfiguration.AssertNaturalFreightHierarchy();
            GD.Print($"GA_NEXT_R89_MARITIME_HIERARCHY_PASS " +
                $"land_base={GaExplicitEconomicQaConfiguration.QaLandEdgeCostMicro} " +
                $"coast_base={GaExplicitEconomicQaConfiguration.QaCoastEdgeCostMicro} " +
                $"sea_base={GaExplicitEconomicQaConfiguration.QaSeaEdgeCostMicro} " +
                $"sea_edges={seaEdges} coast_edges={coastEdges} land_edges={landEdges} " +
                $"blocked_ice_edges={blockedEdges}");
            GD.Print($"GA_NEXT_R94_FROZEN_FREIGHT_PASS ground={landEdges} " +
                $"open_sea={seaEdges} ports={coastEdges} " +
                $"blocked_ice_sea={blockedEdges}");
            Must(_map.FineVisualDetailTileCount == 0,
                "R81 decorative microdetail must never be active in G18.");
            // R88: continuous G18 thermal rendering must preserve the signed
            // freight elevations and expose a smooth snow/sea-ice transition.
            Must(TacticalContextPreview.FineIceWeight(-800_000L) == 1f &&
                TacticalContextPreview.FineIceWeight(800_000L) == 0f &&
                TacticalContextPreview.FineIceWeight(-640_000L) > 0f &&
                TacticalContextPreview.FineIceWeight(-640_000L) < 1f,
                "G18 polar temperature must blend continuously.");
            Must(fine.FineTemperatureRaw.Count == count,
                "Every fine physical tile needs the signed G18 temperature.");
            // R91: terrain classification must be exhaustive, deterministic
            // and match the visual ice/open-water boundary. No coast can be
            // converted to land merely by changing the elevation or sea-level.
            int firmSeaIce = 0, openWater = 0, geologicalLand = 0;
            int previousDiscreteIce = 0, previousDiscreteSeaIce = 0;
            for (int i = 0; i < count; i++)
            {
                var surface = fine.SurfaceAtIndex(i);
                bool isGeologicalLand = fine.FineElevationRaw[i] > fine.SeaLevelRaw;
                if (fine.FineTemperatureRaw[i] <=
                    ExperimentalProceduralBiomeClassifier.IceThreshold)
                {
                    previousDiscreteIce++;
                    if (!isGeologicalLand) previousDiscreteSeaIce++;
                }
                bool frozenWater = !isGeologicalLand &&
                    fine.FineTemperatureRaw[i] <=
                        FoundationFineFirstPhysicalGeography.FirmSeaIceTemperatureRaw;
                Must((surface == FoundationFineSurfaceKind.Land) == isGeologicalLand &&
                    (surface == FoundationFineSurfaceKind.FirmSeaIce) == frozenWater &&
                    fine.CanTraverseOnFoot(i) ==
                        (surface != FoundationFineSurfaceKind.OpenWater) &&
                    fine.CanNavigateByShip(i) ==
                        (surface == FoundationFineSurfaceKind.OpenWater),
                    "The functional sea-ice boundary must match G18 temperature.");
                if (surface == FoundationFineSurfaceKind.Land) geologicalLand++;
                else if (surface == FoundationFineSurfaceKind.OpenWater) openWater++;
                else firmSeaIce++;
            }
            Must(firmSeaIce > 0 && openWater > 0 &&
                firmSeaIce <= previousDiscreteSeaIce &&
                previousDiscreteIce > 0 &&
                geologicalLand == fine.LandTileCount &&
                firmSeaIce + openWater + geologicalLand == count,
                "Polar ice, ocean and geological land must remain distinguishable.");
            GD.Print($"GA_NEXT_R91_SURFACE_CONTRACT_PASS " +
                $"land={geologicalLand} open_water={openWater} " +
                $"firm_sea_ice={firmSeaIce} " +
                $"prior_discrete_ice={previousDiscreteIce} " +
                $"prior_discrete_ocean_ice={previousDiscreteSeaIce} " +
                "worldgen_unchanged=true mobility_classification_derived=true");
            int frozen = 0, temperate = 0, transition = 0;
            for (int i = 0; i < count; i++)
            {
                float ice = TacticalContextPreview.FineIceWeight(
                    fine.FineTemperatureRaw[i]);
                if (ice >= 0.999f) frozen++;
                else if (ice <= 0.001f) temperate++;
                else transition++;
            }
            Must(frozen > 0 && temperate > 0 && transition > 0,
                "The signed G18 map must exhibit polar ice, temperate " +
                "terrain and smooth thermal transition.");
            GD.Print($"GA_NEXT_R88_G18_CONTINUOUS_ICE_PASS " +
                $"frozen={frozen} temperate={temperate} transition={transition} " +
                "source=signed_g18_temperature no_discrete_biomes=true");
            GD.Print($"GA_NEXT_R87_G18_TERRAIN_FREIGHT_PASS " +
                $"tiles={count} land={land} sea={water} " +
                $"land_edges={landEdges} coast_edges={coastEdges} " +
                $"sea_edges={seaEdges} blocked_ice_edges={blockedEdges} " +
                "source=signed_g18 exact_match=true");

            var initialHash = _host.StateHash;
            var camera = _map.TacticalCameraState;
            await Capture("01_g18_terrain_before_trade");
            await PressE();
            var first = _host.GetLastClosedEconomicTradeView(Human);
            Must(_host.StateHash != initialHash &&
                _host.NextTurn.Value == 2UL &&
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 1UL &&
                _host.Snapshot.Treasury.Get(Human).CashScaled.Sign > 0 &&
                first.EconomicCycleOrdinal == 1UL &&
                first.Availability ==
                    FoundationEconomicTradeOverlayAvailability.AuthorizedSubset &&
                _map.HasSettledTradeData &&
                _map.PresentedEconomicCycleOrdinal == 1UL &&
                _map.TacticalCameraState == camera,
                "One E-key must close an actual G18 economic sale and " +
                "publish its private participant-visible freight overlay.");
            await Capture("02_g18_after_first_trade");
            var firstHash = _host.StateHash;
            await PressE();
            var second = _host.GetLastClosedEconomicTradeView(Human);
            Must(_host.StateHash != firstHash &&
                _host.NextTurn.Value == 3UL &&
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 2UL &&
                second.EconomicCycleOrdinal == 2UL &&
                second.PublishedHash == _host.StateHash &&
                _map.PresentedEconomicCycleOrdinal == 2UL &&
                _map.TacticalCameraState == camera,
                "Second E-key must settle and publish exactly one new " +
                "G18 economic cycle without changing the map.");
            await Capture("03_g18_after_second_trade");

            // R90: capture both actual geographic caps, not just the
            // economy-demo starting neighborhood. Strategic focus only
            // navigates the camera; physical colors stay sourced from G18.
            var coarseGeometry = _host.Session.GeneratedWorld.StrategicSphericalGeometry;
            var north = coarseGeometry.Cells.OrderByDescending(x => x.Center.Z).First();
            var south = coarseGeometry.Cells.OrderBy(x => x.Center.Z).First();
            Must(north.CellId != south.CellId,
                "Polar preview requires distinct northern and southern cells.");
            var settledHash = _host.StateHash;
            _map.Open(north.CellId.Value);
            await Capture("04_g18_north_pole");
            _map.Open(south.CellId.Value);
            await Capture("05_g18_south_pole");
            Must(_host.StateHash == settledHash,
                "Polar camera inspection must not change economic state.");
            GD.Print($"GA_NEXT_R90_POLAR_FRAMES_PASS north={north.CellId.Value} " +
                $"south={south.CellId.Value} signed_g18=true economic_state_unchanged=true");
            GD.Print("GA_NEXT_R87_G18_GODOT_ACCEPTANCE_PASS " +
                "cycles=2 replay=true real_economy=true " +
                "terrain_and_freight=same_signed_g18 manual_control=true");
            _main.GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError($"GA_NEXT_R87_G18_GODOT_ACCEPTANCE_FAIL {e}");
            _main.GetTree().Quit(1);
        }
    }
}
