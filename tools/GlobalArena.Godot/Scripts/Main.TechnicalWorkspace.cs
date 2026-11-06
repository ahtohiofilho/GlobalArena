using System;
using System.Linq;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-UI-TECHNICAL-01: one opt-in technical workspace per map context.
/// Preserve existing node instances, signal handlers and engineering
/// diagnostics; the default right rail remains dedicated to player decisions.
/// Never transfer globe viewport or simulation objects into the sidebar.
/// </summary>
public partial class Main
{
    private VBoxContainer? _strategicTechnicalPanel;
    private VBoxContainer? _tacticalTechnicalPanel;
    private Button? _strategicTechnicalToggle;

    private static (Button Toggle, VBoxContainer Panel) CreateTechnicalFold(
        VBoxContainer host, string contextName)
    {
        ArgumentNullException.ThrowIfNull(host);
        var toggle = new Button
        {
            Name = $"GAQA{contextName}TechnicalToggle",
            Text = "FERRAMENTAS TÉCNICAS (QA)",
            TooltipText =
                "Diagnósticos e opções experimentais. Não são comandos essenciais do jogo.",
            ToggleMode = true,
            ButtonPressed = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        host.AddChild(toggle);

        var panel = new VBoxContainer
        {
            Name = $"GAQA{contextName}TechnicalPanel",
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        panel.AddThemeConstantOverride("separation", 4);
        host.AddChild(panel);

        toggle.Toggled += expanded =>
        {
            panel.Visible = expanded;
            toggle.Text = expanded
                ? "OCULTAR FERRAMENTAS TÉCNICAS (QA)"
                : "FERRAMENTAS TÉCNICAS (QA)";
            GD.Print($"GA_UI_TECHNICAL_WORKSPACE_01 context={contextName} " +
                     $"expanded={expanded.ToString().ToLowerInvariant()} " +
                     "canonical_mutation=false");
        };
        return (toggle, panel);
    }

    /// <summary>
    /// Two short labels describing the selected focus stay accessible.
    /// All old stats, shortcuts, legacy map selectors, experimental HD
    /// calibration and the explicit-clear action stay wired, but folded.
    /// </summary>
    private void ConfigureStrategicTechnicalWorkspace(VBoxContainer strategicHud)
    {
        ArgumentNullException.ThrowIfNull(strategicHud);
        if (_territorialPresenceSummary is null)
            throw new InvalidOperationException(
                "Strategic territory readout must exist before technical reparenting.");

        var transferred = strategicHud.GetChildren().OfType<Control>().ToArray();
        if (transferred.Length < 8 ||
            transferred[0] is not Label || transferred[1] is not Label)
            throw new InvalidOperationException(
                "Unexpected global globe HUD structure: refusing unsafe QA reparenting.");

        var (toggle, panel) = CreateTechnicalFold(strategicHud, "Strategic");
        _strategicTechnicalPanel = panel;
        _strategicTechnicalToggle = toggle;
        if (_strategicGlobe is not null)
            _strategicGlobe.TechnicalDetailsShortcutRequested += () =>
            {
                if (_strategicGlobe.Visible && _strategicTechnicalToggle is not null)
                    _strategicTechnicalToggle.ButtonPressed = true;
            };
        foreach (var node in transferred.Skip(2))
            node.Reparent(panel);

        // The lengthy political/presence explanation belongs to the same
        // optional diagnostic workspace, not the always-visible header.
        _territorialPresenceSummary.Reparent(panel);

        _strategicCalibrationGroups.Clear();
        foreach (var node in panel.GetChildren())
        {
            if (node is HBoxContainer row &&
                row.GetChildren().OfType<Button>()
                    .Any(b => b.Text.StartsWith("LAND ", StringComparison.Ordinal)))
                _strategicCalibrationGroups.Add(row);
            else if (node is Button next && next.Text == "NEXT SEED")
                _strategicCalibrationGroups.Add(next);
        }
        if (_strategicCalibrationGroups.Count != 2)
            throw new InvalidOperationException(
                "Both original HD calibration control groups must remain accessible.");

        GD.Print("GA_UI_TECHNICAL_01_GLOBAL_READY folded=true " +
                 "globe_shortcuts_preserved=true world_stats_preserved=true " +
                 "hd_calibration_preserved=true presence_readout_preserved=true");
    }

    /// <summary>
    /// The tactical top-level controls remain: economy, actual population,
    /// material flow, decisions, turn/cycle and save. Legacy diagnostics,
    /// frame/projection widgets and inspector move behind one disclosure.
    /// </summary>
    private void ConfigureTacticalTechnicalWorkspace(VBoxContainer tacticalHud)
    {
        ArgumentNullException.ThrowIfNull(tacticalHud);
        if (_tacticalTileEdges is null ||
            _tacticalInspectorToggle is null || _tacticalInspector is null ||
            _tacticalPreview is null || _workerLabAdvancedPanel is null)
            throw new InvalidOperationException(
                "Required existing tactical QA controls are missing.");

        var oldLaboratoryToggle = tacticalHud.FindChild(
            "GAQAAdvancedToggle", recursive: true, owned: false) as Button;
        if (oldLaboratoryToggle is null)
            throw new InvalidOperationException(
                "Original laboratory toggle must keep its existing handler.");

        var (_, panel) = CreateTechnicalFold(tacticalHud, "Tactical");
        _tacticalTechnicalPanel = panel;
        _tacticalTileEdges.Reparent(panel);
        _tacticalInspectorToggle.Reparent(panel);
        _tacticalInspector.Reparent(panel);
        _tacticalPreview.TransferHud(panel);

        // Reparent the original second-level laboratory and its button,
        // preserving every existing E2E signal and original callback.
        oldLaboratoryToggle.Reparent(panel);
        _workerLabAdvancedPanel.Reparent(panel);

        GD.Print("GA_UI_TECHNICAL_01_TACTICAL_READY folded=true " +
                 "physical_edges_optional=true inspector_preserved=true " +
                 "advanced_lab_preserved=true terrain_controls_preserved=true");
    }
}
