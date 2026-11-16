using System;
using System.Globalization;
using GlobalArena.Runtime;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Read-only, pointer-following worker inspection card anchored to the
/// existing rectangular tactical viewport. The coplanar VS16 badges and
/// 45-degree camera are unchanged. No new camera, economic settlement,
/// mouse-capturing modal layer, persistent window, or gameplay authority.
/// </summary>
public partial class TacticalContextPreview
{
    private const float WorkerInspectorWidth = 286f;
    private const float WorkerInspectorHeight = 171f;
    private const float WorkerInspectorInset = 8f;
    private PanelContainer? _workerInspectorCard;
    private Label? _workerInspectorHeading;
    private Label? _workerInspectorOccupation;
    private Label? _workerInspectorStatus;
    private Label? _workerInspectorLocation;
    private Label? _workerInspectorCapacity;
    private Label? _workerInspectorDemand;
    private ColorRect? _workerInspectorRoleAccent;
    private Vector2? _lastInspectorCursor;

    public bool IsWorkerInspectorVisible => _workerInspectorCard?.Visible == true;
    public Rect2 WorkerInspectorBounds => _workerInspectorCard is { Visible: true } card
        ? new Rect2(card.Position, card.Size)
        : new Rect2(Vector2.Zero, Vector2.Zero);
    public string? WorkerInspectorDescription => IsWorkerInspectorVisible
        ? string.Join(" | ", _workerInspectorHeading?.Text,
            _workerInspectorOccupation?.Text, _workerInspectorStatus?.Text,
            _workerInspectorLocation?.Text, _workerInspectorCapacity?.Text,
            _workerInspectorDemand?.Text)
        : null;

    private static Label InspectorLabel(string name, int fontSize, Color color)
    {
        var label = new Label
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipText = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private void InitializeWorkerInspector()
    {
        var card = new PanelContainer
        {
            Name = "GANextWorkerInspectionCard",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            Size = new Vector2(WorkerInspectorWidth, WorkerInspectorHeight),
            CustomMinimumSize = new Vector2(WorkerInspectorWidth, WorkerInspectorHeight),
            ZIndex = 15
        };
        // Reuse production design-system tokens, not a translucent gray mask.
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = GaVisualFoundation.Surface,
            BorderColor = GaVisualFoundation.AccentDim,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 11,
            ContentMarginRight = 11,
            ContentMarginTop = 9,
            ContentMarginBottom = 9,
            ShadowColor = new Color(0f, 0f, 0f, 0.43f),
            ShadowSize = 7
        });
        var stack = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        stack.AddThemeConstantOverride("separation", 3);
        card.AddChild(stack);

        var titleRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        titleRow.AddThemeConstantOverride("separation", 9);
        stack.AddChild(titleRow);
        _workerInspectorRoleAccent = new ColorRect
        {
            Name = "WorkerOccupationStripe",
            Color = GaVisualFoundation.Accent,
            CustomMinimumSize = new Vector2(7f, 30f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        titleRow.AddChild(_workerInspectorRoleAccent);
        var titleStack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        titleStack.AddThemeConstantOverride("separation", 1);
        titleRow.AddChild(titleStack);
        _workerInspectorHeading = InspectorLabel(
            "WorkerIdentityHeading", 12, GaVisualFoundation.Text);
        titleStack.AddChild(_workerInspectorHeading);
        _workerInspectorOccupation = InspectorLabel(
            "WorkerOccupationHeading", 10, GaVisualFoundation.Accent);
        titleStack.AddChild(_workerInspectorOccupation);

        var rule = new HSeparator { MouseFilter = MouseFilterEnum.Ignore };
        stack.AddChild(rule);
        _workerInspectorStatus = InspectorLabel(
            "WorkerStateLine", 11, GaVisualFoundation.Text);
        _workerInspectorLocation = InspectorLabel(
            "WorkerFineLocationLine", 11, GaVisualFoundation.Muted);
        _workerInspectorCapacity = InspectorLabel(
            "WorkerCapacityLine", 11, GaVisualFoundation.Text);
        _workerInspectorDemand = InspectorLabel(
            "WorkerDemandLine", 11, GaVisualFoundation.Muted);
        stack.AddChild(_workerInspectorStatus);
        stack.AddChild(_workerInspectorLocation);
        stack.AddChild(_workerInspectorCapacity);
        stack.AddChild(_workerInspectorDemand);
        var hint = InspectorLabel("WorkerHoverHint", 10, GaVisualFoundation.Accent);
        hint.Text = "CLIQUE NO MARCADOR PARA SELECIONAR";
        stack.AddChild(hint);

        AddChild(card);
        _workerInspectorCard = card;
        Resized += RepositionWorkerInspector;
    }

    /// <summary>
    /// Pure placement: favor right/below the cursor, flip left/up as needed,
    /// then clamp to viewport. The card never falls off the tactical map.
    /// No changes to the 3D camera or to world-space input coordinates.
    /// </summary>
    public static Vector2 PlaceWorkerInspector(
        Vector2 cursor, Vector2 cardSize, Vector2 viewportSize)
    {
        const float horizontalOffset = 18f;
        const float verticalOffset = 18f;
        float x = cursor.X + horizontalOffset;
        float y = cursor.Y + verticalOffset;
        if (x + cardSize.X + WorkerInspectorInset > viewportSize.X)
            x = cursor.X - cardSize.X - horizontalOffset;
        if (y + cardSize.Y + WorkerInspectorInset > viewportSize.Y)
            y = cursor.Y - cardSize.Y - verticalOffset;
        return new Vector2(
            Mathf.Clamp(x, WorkerInspectorInset,
                Mathf.Max(WorkerInspectorInset, viewportSize.X -
                    cardSize.X - WorkerInspectorInset)),
            Mathf.Clamp(y, WorkerInspectorInset,
                Mathf.Max(WorkerInspectorInset, viewportSize.Y -
                    cardSize.Y - WorkerInspectorInset)));
    }

    private void RepositionWorkerInspector()
    {
        if (_workerInspectorCard is null || !_lastInspectorCursor.HasValue ||
            !_workerInspectorCard.Visible)
            return;
        _workerInspectorCard.Position = PlaceWorkerInspector(
            _lastInspectorCursor.Value, _workerInspectorCard.Size, Size);
    }

    private static string WorkerOperationalStatus(PhysicalWorkerActivity activity) =>
        activity switch
        {
            PhysicalWorkerActivity.Producing => "Produzindo",
            PhysicalWorkerActivity.Traveling => "Em deslocamento",
            PhysicalWorkerActivity.Military => "Atividade militar",
            PhysicalWorkerActivity.Other => "Outra atividade",
            _ => throw new InvalidOperationException(
                "Unrecognized authoritative worker state")
        };

    private void ShowWorkerInspector(
        PhysicalWorkerRuntimeRecord worker, Vector2 localPointer)
    {
        if (_workerInspectorCard is null || _workerInspectorHeading is null ||
            _workerInspectorOccupation is null || _workerInspectorStatus is null ||
            _workerInspectorLocation is null || _workerInspectorCapacity is null ||
            _workerInspectorDemand is null || _workerInspectorRoleAccent is null)
            return;

        var occupation = worker.Occupation.HasValue
            ? WorkerOccupationLabel(worker.Occupation.Value)
            : "Profissão não registrada · v10";
        var tile = worker.TacticalTileId!.Value.FineStrategicCellId.Value;
        var unitCapacity = worker.OutputMicroPerTurn / 1_000_000m;
        var demand = worker.DemandMultiplierMicro / 1_000_000m;
        var occupants = _displayWorkers.Count(w =>
            w.TacticalTileId == worker.TacticalTileId);
        _workerInspectorHeading.Text = $"TRABALHADOR #{worker.Id.Value}";
        _workerInspectorOccupation.Text =
            $"{occupation.ToUpperInvariant()}  /  CIV. {worker.Owner.Value}";
        _workerInspectorRoleAccent.Color = WorkerStatusColor(worker);
        _workerInspectorStatus.Text =
            $"SITUAÇÃO    {WorkerOperationalStatus(worker.Activity)}";
        _workerInspectorLocation.Text =
            $"LOCAL        G(18,0)/{tile}  ·  Região T{worker.CellId.Value}";
        _workerInspectorCapacity.Text =
            $"ITEM {worker.Commodity.Value}   ·   Capacidade {unitCapacity.ToString("0.###", CultureInfo.InvariantCulture)}/turno";
        _workerInspectorDemand.Text =
            $"DEMANDA ×{demand.ToString("0.###", CultureInfo.InvariantCulture)}   ·   {occupants} pessoa(s) no tile";
        _lastInspectorCursor = localPointer;
        _workerInspectorCard.Visible = true;
        RepositionWorkerInspector();
    }

    private void HideWorkerInspector()
    {
        if (_workerInspectorCard is not null)
            _workerInspectorCard.Visible = false;
        _lastInspectorCursor = null;
    }

    private void RefreshWorkerInspectorFromCensus()
    {
        if (!_hoveredBadgeId.HasValue || !_lastInspectorCursor.HasValue) return;
        var worker = _displayWorkers.FirstOrDefault(
            w => w.Id.Value == _hoveredBadgeId.Value);
        if (worker is null ||
            !_workerGlyphPositions.ContainsKey(_hoveredBadgeId.Value))
        {
            HideWorkerInspector();
            return;
        }
        ShowWorkerInspector(worker, _lastInspectorCursor.Value);
    }
}
