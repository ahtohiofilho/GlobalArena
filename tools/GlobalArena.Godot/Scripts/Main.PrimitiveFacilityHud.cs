using System;
using System.Linq;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-ECON-REBASE-12: the first user-visible end-to-end worksite cockpit.
/// Commands use ONLY the live canonical session. The tactical map stays
/// full-size; this compact panel lives in the existing scrolling sidebar.
/// </summary>
public partial class Main
{
    private Label? _worksitePulse;
    private Label? _worksiteDetails;
    private SpinBox? _worksiteWood;
    private SpinBox? _worksiteStone;
    private SpinBox? _worksiteCrew;
    private Button? _worksiteAssemble;
    private Button? _worksiteBuild;
    private Button? _worksiteOperate;
    private Button? _worksiteSuspend;

    private static string WorksiteRaw(ulong raw) =>
        (raw / 1_000_000.0).ToString("F2");

    private static ulong RawFromWorksiteInput(SpinBox input) =>
        checked((ulong)Math.Round(input.Value * 1_000_000.0,
            MidpointRounding.AwayFromZero));

    private void InitializePrimitiveFacilityHud(
        VBoxContainer overview, VBoxContainer lab)
    {
        _worksitePulse = new Label
        {
            Name = "GAWorksitePulse",
            Text = "OBRAS | trabalho primitivo sem infraestrutura obrigatória",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _worksitePulse.AddThemeColorOverride("font_color", new Color("#d9c27d"));
        overview.AddChild(_worksitePulse);

        var root = new VBoxContainer { Name = "GAWorksiteCockpit" };
        root.AddThemeConstantOverride("separation", 3);
        lab.AddChild(root);
        var title = new Label
        {
            Name = "GAWorksiteTitle",
            Text = "CONSTRUÇÃO E PRODUÇÃO · TILE SELECIONADO",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeColorOverride("font_color", new Color("#d4e9f3"));
        root.AddChild(title);

        _worksiteDetails = new Label
        {
            Name = "GAWorksiteDetails",
            Text = "Selecione um povoamento e uma atividade; acompanhe as fases aqui.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_worksiteDetails);

        var material = new HBoxContainer();
        material.AddThemeConstantOverride("separation", 3);
        root.AddChild(material);
        material.AddChild(new Label { Text = "Madeira:" });
        _worksiteWood = new SpinBox
        {
            Name = "GAWorksiteWoodUnits",
            MinValue = 0.01, MaxValue = 1000000, Step = 0.01, Value = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        material.AddChild(_worksiteWood);
        material.AddChild(new Label { Text = "Pedra:" });
        _worksiteStone = new SpinBox
        {
            Name = "GAWorksiteStoneUnits",
            MinValue = 0.01, MaxValue = 1000000, Step = 0.01, Value = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        material.AddChild(_worksiteStone);
        _worksiteWood.ValueChanged += _ => UpdatePrimitiveFacilityHud();
        _worksiteStone.ValueChanged += _ => UpdatePrimitiveFacilityHud();

        var crew = new HBoxContainer();
        crew.AddThemeConstantOverride("separation", 3);
        root.AddChild(crew);
        crew.AddChild(new Label { Text = "Pessoas na obra/operação:" });
        _worksiteCrew = new SpinBox
        {
            Name = "GAWorksiteCrew",
            MinValue = 1, MaxValue = 8, Step = 1, Value = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        crew.AddChild(_worksiteCrew);
        _worksiteCrew.ValueChanged += _ => UpdatePrimitiveFacilityHud();

        var construction = new HBoxContainer();
        construction.AddThemeConstantOverride("separation", 3);
        root.AddChild(construction);
        _worksiteAssemble = new Button
        {
            Name = "GAWorksiteAssemble",
            Text = "1 · MONTAR",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _worksiteAssemble.Pressed += AssembleWorksiteFromLocalMaterials;
        construction.AddChild(_worksiteAssemble);
        _worksiteBuild = new Button
        {
            Name = "GAWorksiteBuild",
            Text = "2 · CONSTRUIR",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _worksiteBuild.Pressed += StartWorksiteConstruction;
        construction.AddChild(_worksiteBuild);

        var operation = new HBoxContainer();
        operation.AddThemeConstantOverride("separation", 3);
        root.AddChild(operation);
        _worksiteOperate = new Button
        {
            Name = "GAWorksiteOperate",
            Text = "3 · OPERAR",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _worksiteOperate.Pressed += OperateCompletedWorksite;
        operation.AddChild(_worksiteOperate);
        _worksiteSuspend = new Button
        {
            Name = "GAWorksiteSuspend",
            Text = "PAUSAR OPERAÇÃO",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _worksiteSuspend.Pressed += SuspendWorksiteOperation;
        operation.AddChild(_worksiteSuspend);
    }

    private bool TryGetSelectedWorksite(
        out CanonicalPrimitiveWorksiteTileView? view)
    {
        view = null;
        if (_canonicalPhysicalLab is null || !_workerOrigin.HasValue ||
            _workerFromJob is null) return false;
        view = CanonicalPrimitiveWorksiteTacticalProjection.Read(
            _canonicalPhysicalLab, _workerOrigin.Value,
            (EconomicActivityKind)_workerFromJob.GetSelectedId());
        return true;
    }

    private void UpdatePrimitiveFacilityHud()
    {
        if (_worksiteDetails is null || _worksitePulse is null ||
            _worksiteAssemble is null || _worksiteBuild is null ||
            _worksiteOperate is null || _worksiteSuspend is null ||
            _worksiteWood is null || _worksiteStone is null ||
            _worksiteCrew is null) return;

        if (!TryGetSelectedWorksite(out var selected) || selected is null)
        {
            _worksitePulse.Text = "OBRAS | aguardando sessão e tile ocupado";
            _worksiteAssemble.Disabled = true;
            _worksiteBuild.Disabled = true;
            _worksiteOperate.Disabled = true;
            _worksiteSuspend.Disabled = true;
            return;
        }

        var v = selected;
        var active = _canonicalPhysicalLab!.PrimitiveFacilities
            .FirstOrDefault(x => x.Tile == v.Tile && x.Activity == v.Activity);
        var maxCrew = Math.Max(1.0, (double)v.ActivityWorkers);
        _worksiteCrew.MaxValue = maxCrew;
        if (_worksiteCrew.Value > maxCrew)
            _worksiteCrew.Value = maxCrew;
        var people = checked((ulong)_worksiteCrew.Value);

        string stage = v.Stage switch
        {
            PrimitiveWorksiteStage.NeedsPhysicalMaterials =>
                "AGUARDANDO madeira/pedra física neste tile",
            PrimitiveWorksiteStage.MaterialsAvailable =>
                "MATERIAIS DISPONÍVEIS: montagem opcional",
            PrimitiveWorksiteStage.AssemblyReady =>
                $"MONTAGEM #{v.AvailableAssemblyOrdinal} PRONTA: iniciar obra",
            PrimitiveWorksiteStage.UnderConstruction =>
                $"EM CONSTRUÇÃO: {v.ConstructionWorkers} pessoa(s) " +
                "sem produção normal até o próximo ciclo econômico",
            PrimitiveWorksiteStage.BuiltUnstaffed =>
                $"ABRIGO CONCLUÍDO no turno {v.CompletedTurn}: operação parada",
            PrimitiveWorksiteStage.Operating =>
                $"ABRIGO OPERANDO: {v.OperatingWorkers} trabalhador(es) real(is)",
            _ => "Status indisponível"
        };

        _worksitePulse.Text =
            $"INSTALAÇÕES {v.TotalWorldFacilities} | EM OBRA " +
            $"{v.TotalBuildingFacilities} | OPERANDO {v.TotalOperatingFacilities}\n" +
            $"TILE {v.Tile.FineStrategicCellId.Value} · {v.Activity}: {stage}";

        _worksiteDetails.Text =
            $"Pessoas {v.ResidentWorkers} · nesta atividade {v.ActivityWorkers}. " +
            $"ESTOQUE ATUAL: madeira {WorksiteRaw(v.LocalWoodRaw)} / " +
            $"pedra {WorksiteRaw(v.LocalStoneRaw)}.\n" +
            (v.IncorporatedWoodRaw > 0UL && v.IncorporatedStoneRaw > 0UL
                ? $"JÁ INCORPORADO: madeira {WorksiteRaw(v.IncorporatedWoodRaw)} / " +
                  $"pedra {WorksiteRaw(v.IncorporatedStoneRaw)} " +
                  "(descontado em MONTAR; CONSTRUIR não desconta novamente).\n"
                : "MONTAR desconta madeira/pedra; CONSTRUIR reserva pessoas, não materiais.\n") +
            "1 · Montar → 2 · Construir (1 ciclo) → 3 · Operar.\n" +
            (v.HasFreshAssistedOutput
                ? $"ÚLTIMO CICLO EXECUTADO, turno {v.LastCycleMovementTurn}: " +
                  $"produção atribuída ao abrigo {WorksiteRaw(v.LastAssistedProducedRaw)} " +
                  "(já incluída na produção total)."
                : $"Último ciclo global: turno {v.LastCycleMovementTurn}; " +
                  "produção assistida ainda não comprovada neste fechamento.") +
            "\nSEM bônus de produtividade ou dinheiro novo.";

        var woodRaw = RawFromWorksiteInput(_worksiteWood);
        var stoneRaw = RawFromWorksiteInput(_worksiteStone);
        _worksiteAssemble.Disabled = v.ResidentWorkers == 0UL ||
            v.HasWorldFacility || v.AvailableAssemblyOrdinal != 0 ||
            woodRaw == 0UL || stoneRaw == 0UL ||
            v.LocalWoodRaw < woodRaw || v.LocalStoneRaw < stoneRaw;
        _worksiteBuild.Disabled = !v.MayStartConstruction ||
            people == 0UL || people > v.ActivityWorkers;
        _worksiteOperate.Disabled = active is null ||
            !active.IsOperational || people > active.CommittedWorkers ||
            people > v.ActivityWorkers ||
            people == active.OperatingWorkers;
        _worksiteSuspend.Disabled = active is null ||
            active.OperatingWorkers == 0UL;
    }

    private void ExecuteWorksiteAction(
        Action<CanonicalPhysicalTacticalSession,
            CanonicalPrimitiveWorksiteTileView> operation,
        string success)
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _worksiteDetails is null ||
            !TryGetSelectedWorksite(out var view) || view is null) return;
        try
        {
            operation(_canonicalPhysicalLab, view);
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            _worksiteDetails.Text += "\n" + success;
            GD.Print($"GA_REBASE12_WORKSITE_ACTION {success} " +
                $"tile={view.Tile.FineStrategicCellId.Value} " +
                $"turn={_canonicalPhysicalLab.State.MovementTurn} " +
                $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException or OverflowException)
        {
            _worksiteDetails.Text = "AÇÃO RECUSADA; ESTADO PRESERVADO: " +
                ex.Message;
        }
    }

    private void AssembleWorksiteFromLocalMaterials()
    {
        if (_worksiteWood is null || _worksiteStone is null) return;
        ExecuteWorksiteAction((game, view) =>
        {
            game.AssemblePrimitiveMaterialFrame(view.Tile,
                CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,
                RawFromWorksiteInput(_worksiteWood),
                RawFromWorksiteInput(_worksiteStone));
        }, "MATERIAIS INCORPORADOS: sem geração de mercadorias.");
    }

    private void StartWorksiteConstruction()
    {
        if (_worksiteCrew is null) return;
        ExecuteWorksiteAction((game, view) =>
        {
            game.StartPrimitiveFacility(view.Tile,
                view.AvailableAssemblyOrdinal, view.Activity,
                checked((ulong)_worksiteCrew.Value));
        }, "OBRA INICIADA: avance um ciclo econômico para concluí-la.");
    }

    private void OperateCompletedWorksite()
    {
        if (_worksiteCrew is null) return;
        ExecuteWorksiteAction((game, view) =>
        {
            game.StaffPrimitiveFacility(view.Tile,
                view.FacilityAssemblyOrdinal, view.Activity,
                checked((ulong)_worksiteCrew.Value));
        }, "TRABALHADORES VINCULADOS: avance um ciclo para medir a produção.");
    }

    private void SuspendWorksiteOperation()
    {
        ExecuteWorksiteAction((game, view) =>
        {
            game.StaffPrimitiveFacility(view.Tile,
                view.FacilityAssemblyOrdinal, view.Activity, 0UL);
        }, "OPERAÇÃO PAUSADA: pessoas podem mudar de atividade.");
    }
}
