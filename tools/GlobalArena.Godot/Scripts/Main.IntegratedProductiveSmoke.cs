using Godot;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

public partial class Main
{
    private FoundationTacticalCollectiveReceipt? _integratedProductiveReceipt;

    // The same opening, economy and transport costs as the normal productive
    // QA preview. Only the explicit autonomous controller and smoke are new.
    private async Task ExerciseIntegratedProductiveSmoke()
    {
        try
        {
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var host = _tacticalHost ?? throw new InvalidOperationException("Missing integrated host.");
            var fine = _r123FineGeography ?? throw new InvalidOperationException("Missing signed terrain.");
            if (!host.ProductiveInfrastructureEnabled || !host.DefaultAutomaticBaseEnabled ||
                host.StateHash.FormatVersion != 31U)
                throw new InvalidOperationException("Integrated preview requires the productive automatic economy.");
            var initialWorkers = host.Snapshot.Workers.Workers.Count;
            var own = host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization)
                .OrderBy(w => w.Id.Value).ToArray();
            var builder = own[0];
            var site = builder.TacticalTileId!.Value;
            var roadBuilder = own.First(w => w.Id != builder.Id && w.TacticalTileId == site);
            var crossing = host.Incidence.FineTopology.Edges
                .Where(e => e.IncidentCellIds.Contains(site.FineStrategicCellId) &&
                    fine.CrossingAtIndices(checked((int)e.IncidentCellIds[0].Value - 1),
                        checked((int)e.IncidentCellIds[1].Value - 1)) == FoundationFineTransportCrossingKind.Ground)
                .OrderBy(e => e.Id.Value).First();
            var edge = new PhysicalTacticalEdgeId(host.Incidence.Refinement.FineParameters, crossing.Id);
            var target = new FoundationProductiveTarget(site, FoundationProductiveActivity.Agriculture);
            const ulong money = 10_000UL;
            int aiProductive = 0, aiLogistics = 0, aiEdits = 0;
            AssertCartographicPublication(host, 0UL);

            FoundationTacticalCollectiveReceipt Close(ulong cycle)
            {
                var before = host.StateHash;
                _integratedProductiveReceipt = null;
                EndFineCollectiveTurn();
                var receipt = _integratedProductiveReceipt ??
                    throw new InvalidOperationException("Integrated turn failed to close.");
                if (receipt.Turn.Value != cycle || !receipt.ReplayMatched ||
                    receipt.Hash != host.StateHash || before == host.StateHash ||
                    host.NextTurn.Value != cycle + 1UL ||
                    host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != cycle ||
                    host.Snapshot.Workers.Workers.Count != initialWorkers ||
                    receipt.PersistentTimeline is null || receipt.TemporalEconomicSettlement is null)
                    throw new InvalidOperationException("Integrated collective close, replay or fixed census mismatch.");
                AssertCartographicPublication(host, cycle);
                int p = receipt.ProductiveFunding.Count(e => e.Investor != _humanCivilization);
                int l = receipt.PersistentFunding.Count(e => e.Investor != _humanCivilization);
                int d = receipt.PersistentEdits.Count(e => e.Edit.Issuer != _humanCivilization);
                aiProductive += p; aiLogistics += l; aiEdits += d;
                GD.Print($"GA_NEXT_R235H_CYCLE cycle={cycle} hash={host.StateHash.HexDigest} ai_productive={p} ai_logistics={l} ai_edits={d} edges={host.GetLastClosedEconomicTradeView(_humanCivilization).UsedEdges.Count} replay=true");
                return receipt;
            }

            Close(1UL);
            FocusCityProductionSmokeSite(host, site);
            var tabs = FindChild("GANextCityTabs", true, false) as TabContainer
                ?? throw new InvalidOperationException("Missing City tabs.");
            tabs.CurrentTab = 2;
            if (_productivePanel?.IsVisibleInTree() != true)
                throw new InvalidOperationException("Integrated City Production controls unavailable.");
            _productiveAmount!.Text = GaDisplayNumbers.ExactMoneyInput(money);
            RefreshProductiveInvestmentControls();
            var check = _productiveWorkers!.GetChildren().OfType<CheckBox>()
                .Single(b => b.Text.StartsWith($"Worker #{builder.Id.Value} |", StringComparison.Ordinal));
            check.ButtonPressed = true;
            var opening = host.StateHash;
            if (_productiveQueue?.Disabled != false) throw new InvalidOperationException("Integrated productive funding unavailable.");
            _productiveQueue.EmitSignal(Button.SignalName.Pressed);
            var staged = host.GetHumanPendingProductiveFunding(_humanCivilization).Single();
            if (staged.Target != target || staged.MoneyMicro != money ||
                staged.Assignments.Single().Worker != builder.Id)
                throw new InvalidOperationException("Integrated productive command did not reach host.");
            // Ordinary logistics command; its existing actual-button smoke
            // remains independently mandatory in the same CI workflow.
            host.QueueHumanFundedConstruction(_humanCivilization, edge, money,
                new[] { new FoundationFundedWorkerInsertion(roadBuilder.Id, 0) });
            if (host.StateHash != opening || host.GetHumanPersistentFunding(_humanCivilization).Count != 1)
                throw new InvalidOperationException("Integrated private funding changed committed authority.");
            var second = Close(2UL);
            var ownProductive = second.ProductiveFunding.Single(e => e.Investor == _humanCivilization);
            var ownLogistics = second.PersistentFunding.Single(e => e.Investor == _humanCivilization);
            var productive = host.Snapshot.ProductiveInfrastructure.Projects.Single(p => p.Id == ownProductive.ProjectId);
            var logistics = host.Snapshot.Constructions.Projects.Single(p => p.Id == ownLogistics.ProjectId);
            if (!productive.IsActivated || !logistics.IsActivated ||
                productive.CompletedWorkMicro != productive.RequiredWorkMicro ||
                logistics.CompletedWorkMicro != logistics.RequiredWorkMicro ||
                host.GetHumanProductiveCapital(_humanCivilization, site).Single(c => c.Target == target).ActivatedCapitalMoneyMicro != money)
                throw new InvalidOperationException("Integrated productive/logistics construction did not activate.");
            if (_productiveProgress?.Visible != false)
                throw new InvalidOperationException("Integrated City construction progress failed to refresh.");
            for (ulong cycle = 3UL; cycle <= 6UL; cycle++) Close(cycle);
            if (host.GetHumanProductiveCapital(_humanCivilization, site).Single(c => c.Target == target).ActivatedCapitalMoneyMicro != money ||
                !host.Snapshot.Constructions.Projects.Single(p => p.Id == ownLogistics.ProjectId).IsActivated)
                throw new InvalidOperationException("Integrated committed human capital did not persist.");
            GD.Print($"GA_NEXT_R235H_INTEGRATED_PASS cycles=6 human_productive=true human_logistics=true actual_main_close=true replay=true hash_v=31 ai_productive={aiProductive} ai_logistics={aiLogistics} ai_edits={aiEdits}");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"GA_NEXT_R235H_INTEGRATED_FAIL {ex}");
            GetTree().Quit(1);
        }
    }
}
