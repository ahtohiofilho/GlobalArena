using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// End-to-end on actual Godot's oblique tactical map: a civilization starts
/// with six independently journaled people on one real fine tile, shown as
/// three counted occupation circles. Repeated group clicks cycle IDs,
/// moving one person splits only that activity group after atomic commit.
/// </summary>
public sealed class FoundationGodotConcentratedWorkforceSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;
    private readonly CivilizationId _human = new(1UL);

    public FoundationGodotConcentratedWorkforceSmoke(
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
        for (int i = 0; i < 4; i++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task KeyPress(Key key)
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventKey
            { Keycode = key, Pressed = true, Echo = false },
            inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventKey
            { Keycode = key, Pressed = false, Echo = false },
            inLocalCoords: true);
        await Frames();
    }

    private async Task MouseAt(Vector2 local, bool click)
    {
        var pos = _map.GlobalPosition + local;
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventMouseMotion
        {
            Position = pos, GlobalPosition = pos,
            Relative = Vector2.Zero, ButtonMask = (MouseButtonMask)0
        }, inLocalCoords: true);
        await Frames();
        if (!click) return;
        viewport.PushInput(new InputEventMouseButton
        {
            Position = pos, GlobalPosition = pos,
            ButtonIndex = MouseButton.Left, Pressed = true,
            ButtonMask = MouseButtonMask.Left
        }, inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventMouseButton
        {
            Position = pos, GlobalPosition = pos,
            ButtonIndex = MouseButton.Left, Pressed = false,
            ButtonMask = (MouseButtonMask)0
        }, inLocalCoords: true);
        await Frames();
    }

    private async Task SelectGroup(
        PhysicalTacticalTileId tile, PhysicalWorkerOccupation role)
    {
        Must(_map.TryGetActivityGroupPixel(
            tile.FineStrategicCellId.Value, _human.Value, role, out var pixel),
            "A real activity badge is missing or outside the tactical viewport.");
        await MouseAt(pixel, true);
    }

    private async Task SelectTile(PhysicalTacticalTileId tile)
    {
        Must(_map.TryGetRenderedFineTilePixel(tile, out var pixel),
            "Requested adjacent fine tile cannot be clicked in the renderer.");
        await MouseAt(pixel, true);
        Must(_map.SelectedCanonicalPhysicalTileId == tile.FineStrategicCellId.Value,
            "Real Godot mouse did not choose the canonical fine destination.");
    }

    private async Task Capture(string stage)
    {
        var dir = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_CONCENTRATED_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        await Frames();
        var image = _main.GetViewport().GetTexture().GetImage();
        Must(image is not null && !image.IsEmpty(),
            "Actual Godot v11 graphics framebuffer unavailable.");
        var file = Path.Combine(dir!, stage + ".png");
        Must(image!.SavePng(file) == Error.Ok &&
             new FileInfo(file).Length > 1500,
            "Failed to capture actual grouped workforce frame.");
        GD.Print($"GA_NEXT_CONCENTRATED_FRAME_PASS stage={stage} " +
            $"width={image.GetWidth()} height={image.GetHeight()}");
    }

    public async Task RunAsync()
    {
        try
        {
            await Frames();
            var census = _host.Snapshot.Workers.Workers;
            var hash = _host.StateHash;
            Must(hash.FormatVersion == 11U && census.Count == 12 &&
                _main.ActiveTacticalHost == _host &&
                census.Count(w => w.Owner == _human) == 6 &&
                census.Where(w => w.Owner == _human)
                    .Select(w => w.TacticalTileId).Distinct().Count() == 1,
                "Godot preview did not start 6 individual humans in one canonical tile.");
            var home = census.Single(w => w.Id.Value == 1UL).TacticalTileId!.Value;
            var aiHome = census.Single(w => w.Id.Value == 7UL).TacticalTileId!.Value;
            Must(census.Count(w => w.TacticalTileId == home) == 6 &&
                census.Count(w => w.TacticalTileId == aiHome) == 6 &&
                home != aiHome,
                "Preview did not separate the two civilization capital tiles.");
            Must(_map.VisibleWorkerMarkerCount == 6 &&
                _map.VisibleActivityGroupCount == 3 &&
                _map.TotalVisibleGroupMembers == 6 &&
                _map.VisibleWorkerMarkerIds.Distinct().Count() == 6,
                "Initial Godot chart must show six real people in three activity circles.");
            foreach (var role in new[] {
                         PhysicalWorkerOccupation.Agriculture,
                         PhysicalWorkerOccupation.Forestry,
                         PhysicalWorkerOccupation.Mining })
                Must(_map.ActivityGroupMemberCount(
                        home.FineStrategicCellId.Value, _human.Value, role) == 2,
                    $"Actual activity group {role} did not contain two individuals.");
            Must(_map.RenderedWorkerBadgeFill(1UL) == new Color("#61d98b") &&
                _map.RenderedWorkerBadgeFill(2UL) == new Color("#b88656") &&
                _map.RenderedWorkerBadgeFill(3UL) == new Color("#c4c7d4"),
                "Original green/brown/silver VS16 colors not preserved.");

            var originalCamera = _map.TacticalCameraState;
            Must(_map.TryGetActivityGroupPixel(home.FineStrategicCellId.Value,
                _human.Value, PhysicalWorkerOccupation.Agriculture, out var agPixel),
                "Agricultural activity group is not visible.");
            await MouseAt(agPixel, false);
            var hover = _map.WorkerInspectorDescription;
            Must(hover is not null && hover.Contains("GRUPO ×2") &&
                hover.Contains("Agricultura") && hover.Contains("Cap. 2") &&
                hover.Contains("Dem. ×2") &&
                _host.StateHash == hash &&
                _map.TacticalCameraState == originalCamera,
                "Group hover lacks the real aggregated facts or changed state/camera.");
            GD.Print("GA_NEXT_WORKFORCE_AGGREGATION_HOVER_PASS " +
                "physical_people=6 occupation_discs=3 sizes=2_2_2 " +
                "capacity_total=2 demand_total=2 independent_ids=true");
            await Capture("00_group_hover");

            await SelectGroup(home, PhysicalWorkerOccupation.Agriculture);
            Must(_main.SelectedTacticalWorkerId == 1UL,
                "First click must choose the first actual agricultural worker.");
            await SelectGroup(home, PhysicalWorkerOccupation.Agriculture);
            Must(_main.SelectedTacticalWorkerId == 4UL && _host.StateHash == hash,
                "Repeated group click must cycle to second real worker without mutation.");
            await SelectGroup(home, PhysicalWorkerOccupation.Agriculture);
            Must(_main.SelectedTacticalWorkerId == 1UL,
                "Third click must cycle to first individual ID.");
            await KeyPress(Key.Tab);
            var roster = _main.FindChild(
                "GANextOperationalRoster", true, false) as GaOperationalRoster;
            Must(roster is not null && roster.DisplayedWorkerCount == 6 &&
                roster.HasAuthoritativeFinePositions,
                "Tab must preserve all six individually identifiable people.");
            await Capture("01_three_groups");

            var adjacent = _host.Incidence.FineTopology.Cells[
                checked((int)home.FineStrategicCellId.Value - 1)].AdjacentCellIds
                .Select(id => new PhysicalTacticalTileId(
                    _host.Incidence.Refinement.FineParameters, id))
                .First(tile => _map.TryGetRenderedFineTilePixel(tile, out _));
            await SelectTile(adjacent);
            await KeyPress(Key.G);
            Must(_host.PendingOrderCount == 1 &&
                _host.StateHash == hash && _host.Snapshot.Revision == 0UL,
                "G must queue only worker #1 without splitting group before E.");
            await KeyPress(Key.X);
            Must(_host.PendingOrderCount == 0 && _host.StateHash == hash,
                "X must cancel pending individual order without mutation.");
            await KeyPress(Key.G);
            await KeyPress(Key.E);
            Must(_host.NextTurn.Value == 2UL &&
                _host.Snapshot.Workers.Workers.Count == 12 &&
                _host.Snapshot.Revision == 2UL &&
                _host.StateHash.FormatVersion == 11U,
                "E failed atomic human+AI commit over 12 individual workers.");
            Must(_host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .TacticalTileId == adjacent &&
                _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 7UL)
                    .Activity == PhysicalWorkerActivity.Producing,
                "One human move + AI action was not committed together.");
            Must(_map.VisibleWorkerMarkerCount == 6 &&
                _map.VisibleActivityGroupCount == 4 &&
                _map.ActivityGroupMemberCount(
                    home.FineStrategicCellId.Value, _human.Value,
                    PhysicalWorkerOccupation.Agriculture) == 1 &&
                _map.ActivityGroupMemberCount(
                    adjacent.FineStrategicCellId.Value, _human.Value,
                    PhysicalWorkerOccupation.Agriculture) == 1,
                "One individual move must split agriculture 2 into two true groups 1+1.");
            GD.Print("GA_NEXT_CONCENTRATED_WORKFORCE_COMMIT_PASS " +
                "human=6 ai=6 initial_groups=3 split_groups=4 " +
                "agriculture=2_to_1_plus_1 replay=v11");
            await Capture("02_after_individual_move");

            await SelectTile(adjacent);
            await KeyPress(Key.P);
            Must(_host.PendingOrderCount == 1 &&
                _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .Activity == PhysicalWorkerActivity.Traveling,
                "P cannot mutate status before collective E.");
            await KeyPress(Key.E);
            Must(_host.NextTurn.Value == 3UL &&
                _host.Snapshot.Revision == 3UL &&
                _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .Activity == PhysicalWorkerActivity.Producing &&
                _host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .TacticalTileId == adjacent,
                "Second turn production changed worker location or duplicated work.");
            await Capture("03_after_production");

            // Real in-place occupational reallocation after the prior
            // movement: the capital still has farmer #4 alone, while
            // worker #1 is producing on the adjacent real physical tile.
            // Select #4 from the existing capital agriculture badge, then
            // A/V/N orders affect only that individual's occupational fact.
            await SelectGroup(home, PhysicalWorkerOccupation.Agriculture);
            Must(_main.SelectedTacticalWorkerId == 4UL,
                "Capital agriculture badge must select the remaining farmer #4.");
            var beforeReassign = _host.StateHash;
            var farmerFour = _host.Snapshot.Workers.Workers.Single(
                w => w.Id.Value == 4UL);
            var priorOutput = farmerFour.OutputMicroPerTurn;
            var priorDemand = farmerFour.DemandMultiplierMicro;
            await KeyPress(Key.N);
            Must(_host.PendingOrderCount == 1 &&
                 _host.StateHash == beforeReassign &&
                 _host.Snapshot.Workers.Workers.Single(w =>
                    w.Id.Value == 4UL).Occupation == PhysicalWorkerOccupation.Agriculture &&
                 _map.ActivityGroupMemberCount(home.FineStrategicCellId.Value,
                    _human.Value, PhysicalWorkerOccupation.Agriculture) == 1,
                "N must only queue role change, without recoloring a group before E.");
            await KeyPress(Key.X);
            Must(_host.PendingOrderCount == 0 &&
                 _host.StateHash == beforeReassign,
                "X must cancel the individual occupational reassignment intent.");
            await KeyPress(Key.N);
            await KeyPress(Key.E);
            Must(_host.PendingOrderCount == 0 &&
                 _host.NextTurn.Value == 4UL &&
                 _host.Snapshot.Revision == 4UL &&
                 _host.StateHash.FormatVersion == 11U &&
                 _host.StateHash != beforeReassign &&
                 _host.Snapshot.Workers.Workers.Count == 12,
                "E must journal and replay one occupational reassignment only.");
            var reassigned = _host.Snapshot.Workers.Workers.Single(
                w => w.Id.Value == 4UL);
            Must(reassigned.Occupation == PhysicalWorkerOccupation.Mining &&
                 reassigned.TacticalTileId == home &&
                 reassigned.Activity == farmerFour.Activity &&
                 reassigned.OutputMicroPerTurn == priorOutput &&
                 reassigned.DemandMultiplierMicro == priorDemand &&
                 reassigned.Commodity == farmerFour.Commodity &&
                 reassigned.Owner == farmerFour.Owner &&
                 _map.VisibleActivityGroupCount == 3 &&
                 _map.ActivityGroupMemberCount(home.FineStrategicCellId.Value,
                    _human.Value, PhysicalWorkerOccupation.Agriculture) == 0 &&
                 _map.ActivityGroupMemberCount(home.FineStrategicCellId.Value,
                    _human.Value, PhysicalWorkerOccupation.Mining) == 3 &&
                 _map.ActivityGroupMemberCount(adjacent.FineStrategicCellId.Value,
                    _human.Value, PhysicalWorkerOccupation.Agriculture) == 1,
                "One reassignment should merge mining 2→3, remove empty farm " +
                "badge and preserve all independent worker economics.");
            await Capture("04_after_reassignment");
            GD.Print("GA_NEXT_OCCUPATION_TURN_E2E_PASS " +
                "human_worker=4 agriculture=1_to_0 mining=2_to_3 " +
                "capital_groups=3_to_2 individual=unchanged_location " +
                "pending_X_E=true hash_v11=true");
            GD.Print("GA_NEXT_CONCENTRATED_WORKFORCE_E2E_PASS " +
                "workers_per_civ=6 all_start_same_tile=true " +
                "groups=3 individual_selection_cycle=1_4_1 " +
                "G_X_E_P_E=true v11_replay=true");
            _main.GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("GA_NEXT_CONCENTRATED_WORKFORCE_FAIL " + e);
            _main.GetTree().Quit(88);
        }
    }
}
