using System;
using System.IO;
using System.Text;
using GlobalArena.Simulation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01F: opt-in, isolated Godot integration for the
/// automatic-offer timeline. This remains behind BOTH technical QA folds.
/// The playable canonical session and normal save are never replaced.
/// </summary>
public partial class Main
{
    private CanonicalAutomaticMarketOfferTimeline? _automaticOfferSandbox;
    private Label? _automaticOfferSandboxStatus;
    private Button? _automaticOfferSandboxAdvance;
    private Button? _automaticOfferSandboxSave;

    private static string AutomaticOfferSandboxSavePath =>
        ProjectSettings.GlobalizePath(
            "user://ga-econ-market-default-01f-experimental.json");

    private void InitializeAutomaticOfferSandboxControls(VBoxContainer laboratory)
    {
        var heading = new Label
        {
            Text = "OFERTA AUTOMÁTICA · LABORATÓRIO ISOLADO (NÃO É A PARTIDA)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        heading.AddThemeColorOverride("font_color", new Color("#e1c883"));
        laboratory.AddChild(heading);

        _automaticOfferSandboxStatus = new Label
        {
            Name = "GAQAOfferSandboxStatus",
            Text = "MODO INATIVO | inicie uma cópia independente no turno zero. " +
                   "Sem venda, receita ou nutrição certificada.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        laboratory.AddChild(_automaticOfferSandboxStatus);

        var start = new Button
        {
            Name = "GAQAOfferSandboxStart",
            Text = "INICIAR LAB DE OFERTA (CÓPIA DO TURNO ZERO)"
        };
        start.Pressed += StartAutomaticOfferSandbox;
        laboratory.AddChild(start);

        var actions = new HBoxContainer();
        laboratory.AddChild(actions);
        _automaticOfferSandboxAdvance = new Button
        {
            Name = "GAQAOfferSandboxTurn",
            Text = "AVANÇAR TURNO DO LAB",
            Disabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _automaticOfferSandboxAdvance.Pressed += AdvanceAutomaticOfferSandbox;
        actions.AddChild(_automaticOfferSandboxAdvance);

        _automaticOfferSandboxSave = new Button
        {
            Name = "GAQAOfferSandboxSave",
            Text = "SALVAR LAB",
            Disabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _automaticOfferSandboxSave.Pressed += SaveAutomaticOfferSandbox;
        actions.AddChild(_automaticOfferSandboxSave);

        var load = new Button
        {
            Name = "GAQAOfferSandboxLoad",
            Text = "REABRIR SAVE EXPERIMENTAL (SEPARADO)"
        };
        load.Pressed += LoadAutomaticOfferSandbox;
        laboratory.AddChild(load);
    }

    private void UpdateAutomaticOfferSandboxStatus(string message)
    {
        if (_automaticOfferSandboxStatus is null)
            return;

        var lab = _automaticOfferSandbox;
        _automaticOfferSandboxAdvance!.Disabled = lab is null;
        _automaticOfferSandboxSave!.Disabled = lab is null;
        _automaticOfferSandboxStatus.Text = lab is null
            ? "LAB INATIVO | " + message
            : $"LAB ISOLADO | TURNO {lab.MovementTurn} · " +
              $"CICLOS {lab.ClosedEconomicCycles} | " +
              $"HASH {lab.WorldHashHex[..12]}\n" +
              $"{message}\n" +
              "OFERTAS são compromissos físicos, NÃO vendas. " +
              "Partida principal, mapa, receita e nutrição permanecem intactos.";
    }

    private void StartAutomaticOfferSandbox()
    {
        try
        {
            if (_canonicalPhysicalLab is null)
                throw new InvalidOperationException("Partida canônica não inicializada.");
            // Opt-in is restricted to the unprogressed source. The wrapper
            // performs a byte-exact clone; never migrate an active save.
            var sandbox = CanonicalAutomaticMarketOfferTimeline.StartOptIn(
                _canonicalPhysicalLab);
            _automaticOfferSandbox = sandbox;
            UpdateAutomaticOfferSandboxStatus(
                "Modo explicitamente ativado em cópia separada, sem alterar a partida.");
            GD.Print("GA_ECON_MARKET_DEFAULT_01F_LAB_STARTED main_mutation=false");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException)
        {
            UpdateAutomaticOfferSandboxStatus(
                "INÍCIO RECUSADO (partida preservada): " + ex.Message);
        }
    }

    private void AdvanceAutomaticOfferSandbox()
    {
        if (_automaticOfferSandbox is null) return;
        try
        {
            var step = _automaticOfferSandbox.AdvanceMovementTurn();
            var details = step.ClosedEconomicCycle
                ? $"CICLO FECHADO | {step.NewlyCreatedOffers} novas ofertas; " +
                  $"{step.NewlyOfferedRaw} unidades físicas brutas vinculadas."
                : "Turno sem fechamento; nenhuma nova oferta criada.";
            UpdateAutomaticOfferSandboxStatus(details);
            GD.Print("GA_ECON_MARKET_DEFAULT_01F_LAB_TURN " +
                     $"turn={step.MovementTurn} cycle={step.ClosedEconomicCycle} " +
                     $"new_offers={step.NewlyCreatedOffers} main_mutation=false " +
                     "sale=false");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException or OverflowException)
        {
            UpdateAutomaticOfferSandboxStatus(
                "AVANÇO NÃO APLICADO: " + ex.Message);
        }
    }

    private void SaveAutomaticOfferSandbox()
    {
        if (_automaticOfferSandbox is null) return;
        try
        {
            var checkpoint = _automaticOfferSandbox.ExportCheckpointJson();
            var restored = CanonicalAutomaticMarketOfferTimeline
                .RestoreCheckpointJson(checkpoint);
            if (restored.ExportCheckpointJson() != checkpoint)
                throw new InvalidOperationException(
                    "O checkpoint experimental falhou na verificação de replay.");

            var path = AutomaticOfferSandboxSavePath;
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, checkpoint, new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
            UpdateAutomaticOfferSandboxStatus(
                "SAVE EXPERIMENTAL GRAVADO, validado e separado da partida.");
            GD.Print("GA_ECON_MARKET_DEFAULT_01F_LAB_SAVE main_mutation=false");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException or IOException or
                                   UnauthorizedAccessException)
        {
            UpdateAutomaticOfferSandboxStatus(
                "SAVE EXPERIMENTAL RECUSADO: " + ex.Message);
        }
    }

    private void LoadAutomaticOfferSandbox()
    {
        try
        {
            var path = AutomaticOfferSandboxSavePath;
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "Nenhum save experimental encontrado.");
            var checkpoint = File.ReadAllText(path, Encoding.UTF8);
            var loaded = CanonicalAutomaticMarketOfferTimeline
                .RestoreCheckpointJson(checkpoint);
            // Only publish after full save, hash and ruleset verification.
            _automaticOfferSandbox = loaded;
            UpdateAutomaticOfferSandboxStatus(
                "SAVE EXPERIMENTAL REABERTO, mantendo partida principal intocada.");
            GD.Print("GA_ECON_MARKET_DEFAULT_01F_LAB_LOAD main_mutation=false");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException or IOException or
                                   UnauthorizedAccessException)
        {
            UpdateAutomaticOfferSandboxStatus(
                "REABERTURA RECUSADA: " + ex.Message);
        }
    }
}
