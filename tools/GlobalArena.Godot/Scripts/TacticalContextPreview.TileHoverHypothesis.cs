using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// EXPERIMENTAL, NOT CANONICAL: map-first hover → activity → individual
/// worker drilldown on an inhabited physical tile. Reads only the already
/// published v11 census. The simulation and the existing 3 coplanar group
/// markers are deliberately untouched. Does not define a City domain entity.
/// </summary>
public partial class TacticalContextPreview
{
    private bool _tileHoverHypothesis;
    private CivilizationId _tileHoverHuman;
    private PanelContainer? _tileHoverPanel;
    private VBoxContainer? _tileHoverContents;
    private Label? _tileHoverHeading;
    private Label? _tileHoverSubheading;
    private Button? _tileHoverPinButton;
    private ulong? _tileHoverFineId;
    private ulong? _tileHoverOwnerId;
    private bool _tileHoverPinned;
    private PhysicalWorkerOccupation? _tileHoverExpandedRole;
    private Vector2 _tileHoverAnchor;
    private readonly Dictionary<PhysicalWorkerOccupation, Button>
        _tileHoverRoleButtons = new();
    private readonly Dictionary<PhysicalWorkerOccupation, VBoxContainer>
        _tileHoverMemberContainers = new();

    public bool IsTileHoverHypothesisEnabled => _tileHoverHypothesis;
    public bool IsTileHoverMenuVisible => _tileHoverPanel?.Visible == true;
    public bool IsTileHoverMenuPinned => _tileHoverPinned;
    public ulong? TileHoverMenuFineId => _tileHoverFineId;
    public PhysicalWorkerOccupation? TileHoverExpandedOccupation =>
        _tileHoverExpandedRole;
    public Rect2 TileHoverMenuRect => _tileHoverPanel is { Visible: true } panel
        ? panel.GetGlobalRect()
        : new Rect2();
    public IReadOnlyList<ulong> TileHoverListedWorkerIds
    {
        get
        {
            if (!_tileHoverFineId.HasValue || !_tileHoverOwnerId.HasValue ||
                !_tileHoverExpandedRole.HasValue)
                return Array.Empty<ulong>();
            return _displayWorkers.Where(w =>
                w.TacticalTileId?.FineStrategicCellId.Value == _tileHoverFineId.Value &&
                w.Owner.Value == _tileHoverOwnerId.Value &&
                w.Occupation == _tileHoverExpandedRole.Value)
                .Select(w => w.Id.Value).OrderBy(id => id).ToArray();
        }
    }

    public Rect2? TileHoverActivityRowRect(PhysicalWorkerOccupation role) =>
        _tileHoverRoleButtons.TryGetValue(role, out var button) &&
        button.IsVisibleInTree() ? button.GetGlobalRect() : null;

    public Rect2? TileHoverWorkerRowRect(ulong workerId)
    {
        var node = _tileHoverPanel?.FindChild(
            $"TileHoverWorker_{workerId}", true, false) as Control;
        return node is not null && node.IsVisibleInTree()
            ? node.GetGlobalRect() : null;
    }

    public void EnableTileHoverHypothesis(CivilizationId human)
    {
        if (!human.IsValid)
            throw new ArgumentException("Valid local civilization required.");
        if (_tileHoverHypothesis) return;
        _tileHoverHuman = human;
        _tileHoverHypothesis = true;
        InitializeTileHoverMenu();
        GD.Print("GA_NEXT_TILE_HOVER_HYPOTHESIS_ENABLED " +
            "stage=experimental city_is_tile_alias=true " +
            "authority=read_only v11_required=true");
    }

    public bool DismissTileHoverMenu()
    {
        if (!IsTileHoverMenuVisible) return false;
        HideTileHoverMenu();
        return true;
    }

    private void InitializeTileHoverMenu()
    {
        var card = new PanelContainer
        {
            Name = "GANextExperimentalTileHover",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(302f, 246f),
            ZIndex = 19
        };
        card.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        var root = new VBoxContainer
        {
            Name = "CityHoverActivityContents",
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
        var titles = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titles.AddThemeConstantOverride("separation", 1);
        top.AddChild(titles);
        _tileHoverHeading = new Label
        {
            Name = "CityHoverHeading",
            Text = "NÚCLEO HABITADO",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _tileHoverHeading.AddThemeFontSizeOverride("font_size", 12);
        titles.AddChild(_tileHoverHeading);
        _tileHoverSubheading = new Label
        {
            Name = "CityHoverPopulation",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _tileHoverSubheading.AddThemeFontSizeOverride("font_size", 10);
        _tileHoverSubheading.AddThemeColorOverride(
            "font_color", GaVisualFoundation.Muted);
        titles.AddChild(_tileHoverSubheading);
        _tileHoverPinButton = new Button
        {
            Name = "CityHoverPinButton", Text = "FIXAR",
            CustomMinimumSize = new Vector2(58f, 30f),
            FocusMode = FocusModeEnum.None
        };
        _tileHoverPinButton.Pressed += () =>
        {
            _tileHoverPinned = !_tileHoverPinned;
            _tileHoverPinButton.Text = _tileHoverPinned ? "SOLTAR" : "FIXAR";
        };
        top.AddChild(_tileHoverPinButton);

        root.AddChild(new HSeparator
        {
            MouseFilter = MouseFilterEnum.Ignore
        });
        _tileHoverContents = root;

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
                Name = $"CityHoverRole_{role}",
                FocusMode = FocusModeEnum.None,
                MouseFilter = MouseFilterEnum.Stop,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            var roleCopy = role;
            activity.MouseEntered += () => ExpandTileHoverOccupation(roleCopy);
            activity.Pressed += () => ExpandTileHoverOccupation(roleCopy);
            section.AddChild(activity);
            var members = new VBoxContainer
            {
                Name = $"CityHoverMembers_{role}",
                Visible = false,
                MouseFilter = MouseFilterEnum.Pass
            };
            members.AddThemeConstantOverride("separation", 2);
            section.AddChild(members);
            _tileHoverRoleButtons[role] = activity;
            _tileHoverMemberContainers[role] = members;
        }

        var hint = new Label
        {
            Name = "CityHoverHint",
            Text = "PASSE NA ATIVIDADE · CLIQUE NA PESSOA",
            MouseFilter = MouseFilterEnum.Ignore
        };
        hint.AddThemeColorOverride("font_color", GaVisualFoundation.Accent);
        hint.AddThemeFontSizeOverride("font_size", 10);
        root.AddChild(hint);
        card.Resized += PositionTileHoverMenu;
        card.MouseExited += () =>
        {
            if (!_tileHoverPinned) HideTileHoverMenu();
        };
        AddChild(card);
        _tileHoverPanel = card;
        Resized += PositionTileHoverMenu;
    }

    private static string OccupationHeading(PhysicalWorkerOccupation role) =>
        role switch
        {
            PhysicalWorkerOccupation.Agriculture => "AGRICULTURA",
            PhysicalWorkerOccupation.Forestry => "EXTRAÇÃO VEGETAL",
            PhysicalWorkerOccupation.Mining => "MINERAÇÃO",
            _ => throw new InvalidOperationException("Unknown productive role")
        };

    private void OpenTileHoverMenu(ulong fineTile, Vector2 pointer)
    {
        if (!_tileHoverHypothesis || _tileHoverPanel is null ||
            !_displayWorkers.Any(w => w.Occupation.HasValue &&
                w.TacticalTileId?.FineStrategicCellId.Value == fineTile))
        {
            if (!_tileHoverPinned) HideTileHoverMenu();
            return;
        }
        if (_tileHoverPinned && _tileHoverFineId.HasValue) return;
        var people = _displayWorkers.Where(w =>
            w.Occupation.HasValue &&
            w.TacticalTileId?.FineStrategicCellId.Value == fineTile).ToArray();
        var owner = people.Any(w => w.Owner == _tileHoverHuman)
            ? _tileHoverHuman.Value
            : people.Min(w => w.Owner.Value);
        if (_tileHoverFineId == fineTile && _tileHoverOwnerId == owner &&
            _tileHoverPanel.Visible) return;

        _tileHoverFineId = fineTile;
        _tileHoverOwnerId = owner;
        _tileHoverExpandedRole = null;
        _tileHoverAnchor = pointer;
        RebuildTileHoverMenu();
        _tileHoverPanel.Visible = true;
        PositionTileHoverMenu();
        GD.Print($"GA_NEXT_TILE_HOVER_OPEN tile={fineTile} civ={owner} " +
            $"count={people.Count(w => w.Owner.Value == owner)} " +
            "stage=hypothesis selected_mutation=false");
    }

    private void RebuildTileHoverMenu()
    {
        if (!_tileHoverFineId.HasValue || !_tileHoverOwnerId.HasValue ||
            _tileHoverHeading is null || _tileHoverSubheading is null)
            return;
        var tile = _tileHoverFineId.Value;
        var owner = _tileHoverOwnerId.Value;
        var workers = _displayWorkers.Where(w =>
            w.TacticalTileId?.FineStrategicCellId.Value == tile &&
            w.Owner.Value == owner).OrderBy(w => w.Id.Value).ToArray();
        if (workers.Length == 0)
        {
            HideTileHoverMenu();
            return;
        }
        _tileHoverHeading.Text = $"NÚCLEO HABITADO  ·  TILE {tile}";
        _tileHoverSubheading.Text =
            $"CIV {owner}  ·  {workers.Length} TRABALHADORES";
        foreach (var (role, button) in _tileHoverRoleButtons)
        {
            var members = workers.Where(w => w.Occupation == role).ToArray();
            button.Text = $"{OccupationHeading(role)}     {members.Length}";
            var list = _tileHoverMemberContainers[role];
            foreach (var child in list.GetChildren())
            {
                list.RemoveChild(child);
                child.QueueFree();
            }
            foreach (var worker in members)
            {
                var individual = worker;
                var selectable = individual.Owner == _tileHoverHuman;
                var row = new Button
                {
                    Name = $"TileHoverWorker_{worker.Id.Value}",
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
                    _tileHoverPinned = true;
                    if (_tileHoverPinButton is not null)
                        _tileHoverPinButton.Text = "SOLTAR";
                    CanonicalWorkerBadgeClicked?.Invoke(individual.Id.Value);
                    GD.Print($"GA_NEXT_TILE_HOVER_WORKER_PICK " +
                        $"tile={tile} worker={individual.Id.Value} " +
                        "source=real_v11 selection_only=true");
                };
                list.AddChild(row);
            }
            list.Visible = _tileHoverExpandedRole == role;
        }
        PositionTileHoverMenu();
    }

    private void ExpandTileHoverOccupation(PhysicalWorkerOccupation role)
    {
        if (!IsTileHoverMenuVisible) return;
        if (_tileHoverExpandedRole == role) return;
        _tileHoverExpandedRole = role;
        foreach (var (kind, container) in _tileHoverMemberContainers)
            container.Visible = kind == role;
        PositionTileHoverMenu();
        GD.Print($"GA_NEXT_TILE_HOVER_ROLE_EXPAND " +
            $"tile={_tileHoverFineId} role={role} " +
            $"workers={TileHoverListedWorkerIds.Count}");
    }

    private void PositionTileHoverMenu()
    {
        if (_tileHoverPanel is null || !_tileHoverPanel.Visible) return;
        // Position uses actual size and flips at the map bounds. For very
        // dense future populations, the dock will take over; this prototype
        // deliberately limits its interaction to the current small census.
        var menu = _tileHoverPanel;
        var bounds = menu.Size;
        menu.Position = PlaceWorkerInspector(
            _tileHoverAnchor, bounds, Size);
    }

    private void HideTileHoverMenu()
    {
        if (_tileHoverPanel is not null)
            _tileHoverPanel.Visible = false;
        _tileHoverPinned = false;
        _tileHoverFineId = null;
        _tileHoverOwnerId = null;
        _tileHoverExpandedRole = null;
        if (_tileHoverPinButton is not null)
            _tileHoverPinButton.Text = "FIXAR";
    }

    private void RefreshTileHoverMenuAfterSnapshot()
    {
        if (!_tileHoverHypothesis || !_tileHoverFineId.HasValue) return;
        RebuildTileHoverMenu();
    }

    private void HandleTileHoverMotion(Vector2 position)
    {
        if (!_tileHoverHypothesis) return;
        if (TryBadgePlanePoint(position, out _, out var tile))
            OpenTileHoverMenu(tile, position);
        else if (!_tileHoverPinned)
            HideTileHoverMenu();
    }
}
