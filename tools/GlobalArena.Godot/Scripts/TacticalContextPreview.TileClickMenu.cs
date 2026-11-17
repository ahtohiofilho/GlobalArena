using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// EXPERIMENTAL, NOT CANONICAL: CLICK tile → CLICK activity → CLICK
/// real individual worker. Pointer motion never opens/expands/dismisses
/// this menu. Uses only the accepted v11 census; no new City entity,
/// no simulated production or change in the three coplanar badges.
/// </summary>
public partial class TacticalContextPreview
{
    private bool _tileClickHypothesis;
    private CivilizationId _tileClickHuman;
    private PanelContainer? _tileClickPanel;
    private Label? _tileClickHeading;
    private ColorRect? _tileClickCivilizationStripe;
    private Label? _tileClickSubheading;
    private ulong? _tileClickFineId;
    private ulong? _tileClickOwnerId;
    private PhysicalWorkerOccupation? _tileClickExpandedRole;
    private Vector2 _tileClickAnchor;
    private readonly Dictionary<PhysicalWorkerOccupation, Button>
        _tileClickRoleButtons = new();
    private readonly Dictionary<PhysicalWorkerOccupation, VBoxContainer>
        _tileClickMemberContainers = new();

    public bool IsTileClickHypothesisEnabled => _tileClickHypothesis;
    public bool IsTileClickMenuVisible => _tileClickPanel?.Visible == true;
    public ulong? TileClickMenuFineId => _tileClickFineId;
    public PhysicalWorkerOccupation? TileClickExpandedOccupation =>
        _tileClickExpandedRole;
    public Rect2 TileClickMenuRect => _tileClickPanel is { Visible: true } panel
        ? panel.GetGlobalRect()
        : new Rect2();
    public IReadOnlyList<ulong> TileClickListedWorkerIds
    {
        get
        {
            if (!_tileClickFineId.HasValue || !_tileClickOwnerId.HasValue ||
                !_tileClickExpandedRole.HasValue)
                return Array.Empty<ulong>();
            return _displayWorkers.Where(w =>
                w.TacticalTileId?.FineStrategicCellId.Value == _tileClickFineId.Value &&
                w.Owner.Value == _tileClickOwnerId.Value &&
                w.Occupation == _tileClickExpandedRole.Value)
                .Select(w => w.Id.Value).OrderBy(id => id).ToArray();
        }
    }

    public Rect2? TileClickActivityRowRect(PhysicalWorkerOccupation role) =>
        _tileClickRoleButtons.TryGetValue(role, out var button) &&
        button.IsVisibleInTree() ? button.GetGlobalRect() : null;

    public Rect2? TileClickWorkerRowRect(ulong workerId)
    {
        var node = _tileClickPanel?.FindChild(
            $"TileClickWorker_{workerId}", true, false) as Control;
        return node is not null && node.IsVisibleInTree()
            ? node.GetGlobalRect() : null;
    }

    public void EnableTileClickMenuHypothesis(CivilizationId human)
    {
        if (!human.IsValid)
            throw new ArgumentException("Valid local civilization required.");
        if (_tileClickHypothesis) return;
        _tileClickHuman = human;
        _tileClickHypothesis = true;
        InitializeTileClickMenu();
        GD.Print("GA_NEXT_TILE_CLICK_HYPOTHESIS_ENABLED " +
            "stage=click_only_experimental city_is_tile_alias=true " +
            "authority=read_only v11_required=true");
    }

    public bool DismissTileClickMenu()
    {
        if (!IsTileClickMenuVisible) return false;
        HideTileClickMenu();
        return true;
    }

    private void InitializeTileClickMenu()
    {
        var card = new PanelContainer
        {
            Name = "GANextExperimentalTileClick",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(302f, 246f),
            ZIndex = 19
        };
        card.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        var root = new VBoxContainer
        {
            Name = "CityClickActivityContents",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 4);
        card.AddChild(root);
        var top = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass
        };
        root.AddChild(top);
        _tileClickCivilizationStripe = new ColorRect
        {
            Name = "CityClickCivilizationIdentityStripe",
            Color = GaCivilizationVisualIdentity.ColorFor(1UL),
            CustomMinimumSize = new Vector2(6f, 30f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        top.AddChild(_tileClickCivilizationStripe);
        var titles = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titles.AddThemeConstantOverride("separation", 1);
        top.AddChild(titles);
        _tileClickHeading = new Label
        {
            Name = "CityClickHeading",
            Text = "NÚCLEO HABITADO",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _tileClickHeading.AddThemeFontSizeOverride("font_size", 12);
        titles.AddChild(_tileClickHeading);
        _tileClickSubheading = new Label
        {
            Name = "CityClickPopulation",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _tileClickSubheading.AddThemeFontSizeOverride("font_size", 10);
        _tileClickSubheading.AddThemeColorOverride(
            "font_color", GaVisualFoundation.Muted);
        titles.AddChild(_tileClickSubheading);
        var close = new Button
        {
            Name = "CityClickCloseButton", Text = "FECHAR",
            CustomMinimumSize = new Vector2(62f, 30f),
            FocusMode = FocusModeEnum.None
        };
        close.Pressed += HideTileClickMenu;
        top.AddChild(close);

        root.AddChild(new HSeparator
        {
            MouseFilter = MouseFilterEnum.Ignore
        });

        foreach (var role in new[] {
                     PhysicalWorkerOccupation.Agriculture,
                     PhysicalWorkerOccupation.Forestry,
                     PhysicalWorkerOccupation.Mining })
        {
            var section = new VBoxContainer
            {
                MouseFilter = MouseFilterEnum.Pass
            };
            section.AddThemeConstantOverride("separation", 2);
            root.AddChild(section);
            var activity = new Button
            {
                Name = $"CityClickRole_{role}",
                FocusMode = FocusModeEnum.None,
                MouseFilter = MouseFilterEnum.Stop,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            var roleCopy = role;
            activity.Pressed += () => ExpandTileClickOccupation(roleCopy);
            section.AddChild(activity);
            var members = new VBoxContainer
            {
                Name = $"CityClickMembers_{role}",
                Visible = false,
                MouseFilter = MouseFilterEnum.Pass
            };
            members.AddThemeConstantOverride("separation", 2);
            section.AddChild(members);
            _tileClickRoleButtons[role] = activity;
            _tileClickMemberContainers[role] = members;
        }

        var hint = new Label
        {
            Name = "CityClickHint",
            Text = "CLIQUE NA ATIVIDADE · DEPOIS NA PESSOA",
            MouseFilter = MouseFilterEnum.Ignore
        };
        hint.AddThemeColorOverride("font_color", GaVisualFoundation.Accent);
        hint.AddThemeFontSizeOverride("font_size", 10);
        root.AddChild(hint);
        card.Resized += PositionTileClickMenu;
        AddChild(card);
        _tileClickPanel = card;
        Resized += PositionTileClickMenu;
    }

    private static string OccupationHeading(PhysicalWorkerOccupation role) =>
        role switch
        {
            PhysicalWorkerOccupation.Agriculture => "AGRICULTURA",
            PhysicalWorkerOccupation.Forestry => "EXTRAÇÃO VEGETAL",
            PhysicalWorkerOccupation.Mining => "MINERAÇÃO",
            _ => throw new InvalidOperationException("Unknown productive role")
        };

    private void OpenTileClickMenu(ulong fineTile, Vector2 pointer)
    {
        if (!_tileClickHypothesis || _tileClickPanel is null ||
            !_displayWorkers.Any(w => w.Occupation.HasValue &&
                w.TacticalTileId?.FineStrategicCellId.Value == fineTile))
        {
            HideTileClickMenu();
            return;
        }
        var people = _displayWorkers.Where(w =>
            w.Occupation.HasValue &&
            w.TacticalTileId?.FineStrategicCellId.Value == fineTile).ToArray();
        var owner = people.Any(w => w.Owner == _tileClickHuman)
            ? _tileClickHuman.Value
            : people.Min(w => w.Owner.Value);
        if (_tileClickFineId == fineTile && _tileClickOwnerId == owner &&
            _tileClickPanel.Visible) return;

        _tileClickFineId = fineTile;
        _tileClickOwnerId = owner;
        _tileClickExpandedRole = null;
        _tileClickAnchor = pointer;
        RebuildTileClickMenu();
        _tileClickPanel.Visible = true;
        PositionTileClickMenu();
        GD.Print($"GA_NEXT_TILE_CLICK_OPEN tile={fineTile} civ={owner} " +
            $"count={people.Count(w => w.Owner.Value == owner)} " +
            "trigger=click selected_mutation=false");
    }

    private void RebuildTileClickMenu()
    {
        if (!_tileClickFineId.HasValue || !_tileClickOwnerId.HasValue ||
            _tileClickHeading is null || _tileClickSubheading is null)
            return;
        var tile = _tileClickFineId.Value;
        var owner = _tileClickOwnerId.Value;
        var workers = _displayWorkers.Where(w =>
            w.TacticalTileId?.FineStrategicCellId.Value == tile &&
            w.Owner.Value == owner).OrderBy(w => w.Id.Value).ToArray();
        if (workers.Length == 0)
        {
            HideTileClickMenu();
            return;
        }
        _tileClickHeading.Text = $"NÚCLEO HABITADO  ·  TILE {tile}";
        _tileClickSubheading.Text =
            $"CIV {owner}  ·  {GaCivilizationVisualIdentity.EnglishNameFor(owner)}  ·  " +
            $"{workers.Length} TRABALHADORES";
        if (_tileClickCivilizationStripe is not null)
            _tileClickCivilizationStripe.Color =
                GaCivilizationVisualIdentity.ColorFor(owner);
        foreach (var (role, button) in _tileClickRoleButtons)
        {
            var members = workers.Where(w => w.Occupation == role).ToArray();
            button.Text = $"{OccupationHeading(role)}     {members.Length}";
            var list = _tileClickMemberContainers[role];
            foreach (var child in list.GetChildren())
            {
                list.RemoveChild(child);
                child.QueueFree();
            }
            foreach (var worker in members)
            {
                var individual = worker;
                var selectable = individual.Owner == _tileClickHuman;
                var row = new Button
                {
                    Name = $"TileClickWorker_{worker.Id.Value}",
                    Text = $"#{worker.Id.Value}   {WorkerOperationalStatus(worker.Activity)}" +
                        (selectable ? "    ·  SELECIONAR" : "    ·  IA"),
                    FocusMode = FocusModeEnum.None,
                    MouseFilter = MouseFilterEnum.Stop,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    Disabled = !selectable
                };
                row.AddThemeFontSizeOverride("font_size", 10);
                row.Pressed += () =>
                {
                    CanonicalWorkerBadgeClicked?.Invoke(individual.Id.Value);
                    GD.Print($"GA_NEXT_TILE_CLICK_WORKER_PICK " +
                        $"tile={tile} worker={individual.Id.Value} " +
                        "source=real_v11 selection_only=true");
                };
                list.AddChild(row);
            }
            list.Visible = _tileClickExpandedRole == role;
        }
        PositionTileClickMenu();
    }

    private void ExpandTileClickOccupation(PhysicalWorkerOccupation role)
    {
        if (!IsTileClickMenuVisible) return;
        _tileClickExpandedRole =
            _tileClickExpandedRole == role ? null : role;
        foreach (var (kind, container) in _tileClickMemberContainers)
            container.Visible = kind == role;
        PositionTileClickMenu();
        GD.Print($"GA_NEXT_TILE_CLICK_ROLE_EXPAND " +
            $"tile={_tileClickFineId} role={role} " +
            $"workers={TileClickListedWorkerIds.Count}");
    }

    private void PositionTileClickMenu()
    {
        if (_tileClickPanel is null || !_tileClickPanel.Visible) return;
        // Position uses actual size and flips at the map bounds. For very
        // dense future populations, the dock will take over; this prototype
        // deliberately limits its interaction to the current small census.
        var menu = _tileClickPanel;
        var bounds = menu.Size;
        menu.Position = PlaceWorkerInspector(
            _tileClickAnchor, bounds, Size);
    }

    private void HideTileClickMenu()
    {
        if (_tileClickPanel is not null)
            _tileClickPanel.Visible = false;
        _tileClickFineId = null;
        _tileClickOwnerId = null;
        _tileClickExpandedRole = null;
    }

    private void RefreshTileClickMenuAfterSnapshot()
    {
        if (!_tileClickHypothesis || !_tileClickFineId.HasValue) return;
        RebuildTileClickMenu();
    }

}
