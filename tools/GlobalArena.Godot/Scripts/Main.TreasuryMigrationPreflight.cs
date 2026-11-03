using System;
using System.IO;
using System.Linq;
using System.Text;
using GlobalArena.Simulation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// VS105: an explicitly requested, read-only V20 migration diagnostic.
/// It inspects the save that the existing REABRIR SAVE button would choose.
/// It cannot register a treasury, replace a save, change the live world,
/// issue currency, settle revenue, or touch the camera companion.
/// </summary>
public partial class Main
{
    private Button? _treasuryMigrationPreflightButton;
    private Label? _treasuryMigrationPreflightStatus;
    private string? _treasuryMigrationPreflightWorldHash;

    private void InitializeTreasuryMigrationPreflightControls(VBoxContainer advancedPanel)
    {
        var heading = new Label
        {
            Text = "COMPATIBILIDADE DE SAVE | PRÉ-VALIDAÇÃO V20",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        heading.AddThemeColorOverride("font_color", new Color("#d4e9f3"));
        advancedPanel.AddChild(heading);

        _treasuryMigrationPreflightButton = new Button
        {
            Name = "GAQATreasuryMigrationInspect",
            Text = "INSPECIONAR SAVE PARA V20 (SOMENTE LEITURA)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _treasuryMigrationPreflightButton.Pressed += InspectTreasurySaveMigration;
        advancedPanel.AddChild(_treasuryMigrationPreflightButton);

        _treasuryMigrationPreflightStatus = new Label
        {
            Name = "GAQATreasuryMigrationStatus",
            Text = "NENHUM SAVE INSPECIONADO. Consulte apenas quando desejar; nada será migrado.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        advancedPanel.AddChild(_treasuryMigrationPreflightStatus);
    }

    // Preserve exactly the same preference as REABRIR SAVE: a damaged V20
    // takes precedence and must never be silently replaced by a V14 fallback.
    private static string PreferredLabSavePath()
    {
        foreach (var path in new[]
        {
            TreasuryLabSavePath,
            RemoteJourneyLabSavePath,
            BornCivilizationLabSavePath,
            TimedLabSavePath,
            CashFoodLabSavePath,
            GeographyLabSavePath,
            AdaptiveLabSavePath,
            CanonicalLabSavePath
        })
        {
            if (File.Exists(path)) return path;
        }
        return LegacyLabSavePath;
    }

    private void UpdateTreasuryMigrationPreflightFreshness()
    {
        if (_treasuryMigrationPreflightStatus is null ||
            _treasuryMigrationPreflightWorldHash is null)
            return;
        if (_canonicalPhysicalLab is not null &&
            _canonicalPhysicalLab.CurrentHash.HexDigest ==
                _treasuryMigrationPreflightWorldHash)
            return;
        _treasuryMigrationPreflightWorldHash = null;
        _treasuryMigrationPreflightStatus.Text =
            "PRÉ-VALIDAÇÃO EXPIRADA: partida alterada; inspecione o arquivo novamente.";
    }

    private void InvalidateTreasuryMigrationPreflight()
    {
        _treasuryMigrationPreflightWorldHash = null;
        if (_treasuryMigrationPreflightStatus is not null)
            _treasuryMigrationPreflightStatus.Text =
                "PRÉ-VALIDAÇÃO EXPIRADA: save ou sessão reaberta; inspecione novamente.";
    }

    private void InspectTreasurySaveMigration()
    {
        if (_treasuryMigrationPreflightStatus is null) return;
        _treasuryMigrationPreflightWorldHash = null;

        try
        {
            if (_canonicalPhysicalLab is null || _canonicalSession is null)
                throw new InvalidOperationException(
                    "Inspecione primeiro o povoamento canônico.");
            var path = PreferredLabSavePath();
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "Nenhum save encontrado. Salve a partida antes de inspecionar.");

            if (path == LegacyLabSavePath)
                throw new InvalidOperationException(
                    "Save VS07 exige a migração histórica separada. " +
                    "Abra e salve no formato canônico antes de inspecionar V20.");

            var beforeHash = _canonicalPhysicalLab.CurrentHash;
            var beforeRevision = _canonicalPhysicalLab.Game.Snapshot.Revision;
            var beforeActions = _canonicalPhysicalLab.Events.ToArray();
            var beforeArchive = _canonicalPhysicalLab.ExportSaveJson();

            // This is disk inspection ONLY. The VS104 implementation restores
            // independent sandbox sessions without returning a candidate save.
            var source = File.ReadAllText(path, Encoding.UTF8);
            var preview = CanonicalDormantTreasuryMigrationPreflight.Inspect(source);
            if (preview.WorldSignatureSha256Hex !=
                _canonicalSession.Snapshot.WorldBinding!.WorldSignatureSha256Hex)
                throw new InvalidOperationException(
                    "Save pertence a outro mundo físico; reabertura neste mundo é incompatível.");
            if (_canonicalPhysicalLab.CurrentHash != beforeHash ||
                _canonicalPhysicalLab.Game.Snapshot.Revision != beforeRevision ||
                !_canonicalPhysicalLab.Events.SequenceEqual(beforeActions) ||
                _canonicalPhysicalLab.ExportSaveJson() != beforeArchive ||
                File.ReadAllText(path, Encoding.UTF8) != source)
                throw new InvalidOperationException(
                    "Pré-validação não confirmou a imutabilidade da sessão ou do arquivo.");

            var situation = preview.AlreadyReserved
                ? "V20 EXISTENTE: RESERVA DORMENTE JÁ REGISTRADA"
                : $"SAVE V{preview.SourceEnvelopeVersion} COMPATÍVEL COM V20";
            var decision = preview.RequiresExplicitPlayerRegistration
                ? "Registro da reserva exige ação explícita; esta inspeção NÃO registra."
                : "Sem nova ativação: reserva V1 permanece vazia e dormente.";
            _treasuryMigrationPreflightStatus.Text =
                $"{situation}\n" +
                $"Arquivo: {Path.GetFileName(path)} | " +
                $"ações históricas: {preview.HistoricalActionCount} | " +
                $"marco: {preview.ActivationAfterActionCount}\n" +
                $"Revisão: {preview.SourceWorldRevision} → " +
                $"{preview.CandidateWorldRevision} | " +
                $"SHA-256 origem: {preview.SourceArchiveSha256Hex[..12]}… | " +
                $"candidato: {preview.CandidateArchiveSha256Hex[..12]}…\n" +
                $"{decision}\n" +
                "APENAS DIAGNÓSTICO: nenhum arquivo gravado, nenhum evento, " +
                "nenhuma emissão, arrecadação, débito ou SETTLE.";
            _treasuryMigrationPreflightWorldHash = beforeHash.HexDigest;
            GD.Print(
                $"GA_VS105_SAVE_MIGRATION_READ_ONLY_OK source={preview.SourceEnvelopeVersion} " +
                $"candidate={preview.CandidateEnvelopeVersion} " +
                $"actions={preview.HistoricalActionCount} " +
                $"already_reserved={preview.AlreadyReserved} " +
                "write=false issuance=false settle=false");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or OverflowException)
        {
            _treasuryMigrationPreflightStatus.Text =
                "PRÉ-VALIDAÇÃO INDISPONÍVEL (PARTIDA E ARQUIVO PRESERVADOS): " + ex.Message;
            GD.Print("GA_VS105_SAVE_MIGRATION_REJECTED_READ_ONLY reason=" +
                ex.GetType().Name);
        }
    }
}
