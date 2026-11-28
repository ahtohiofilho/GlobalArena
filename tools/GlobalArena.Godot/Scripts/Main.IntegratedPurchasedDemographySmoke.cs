using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    private FoundationTacticalCollectiveReceipt? _integratedPurchasedReceipt;

    // QA-only omniscient acceptance instrumentation, never a player read model.
    // Use the actual opening and ordinary autonomous coordinator, no grants,
    // fabricated prices, repositioning, or additional market solve.
    private async Task ExerciseIntegratedPurchasedDemographySmoke()
    {
        try
        {
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var host = _tacticalHost ?? throw new InvalidOperationException("Missing integrated demographic host.");
            var ai = host.AiCivilizations.Single();
            var human = _humanCivilization;
            var owners = new[] { human, ai };
            var start = host.Snapshot;
            if (!host.PurchasedDemographyEnabled || !host.ProductiveInfrastructureEnabled ||
                !host.DefaultAutomaticBaseEnabled || host.StateHash.FormatVersion != 32U ||
                start.Workers.Workers.Count != 2 ||
                owners.Any(c => start.Workers.Workers.Count(w => w.Owner == c) != 1) ||
                start.Treasury.Accounts.Any(a => !a.CashScaled.IsZero) ||
                start.Workers.Workers.Any(w => w.BirthWaitTurns != 0UL))
                throw new InvalidOperationException("Integrated demographic opening must be singleton and zero cash.");
            var price = new FoundationPurchasedDemographicPricePolicy(
                start.PurchasedDemography.InitialMoneyMicro, start.PurchasedDemography.ReferenceAcquisitions);
            OpenCivilizationWindow();
            if (_demographicTabs is null || _demographicTabs.GetTabTitle(2) != "Population")
                throw new InvalidOperationException("Missing Population controls.");
            _demographicTabs.CurrentTab = 2;
            _demographicQuoteButton!.EmitSignal(Button.SignalName.Pressed);
            if (!_demographicQueueButton!.Disabled)
                throw new InvalidOperationException("Opening purchase cannot be funded.");
            ulong humanPurchaseCycle = 0UL, firstAiPurchaseCycle = 0UL;
            int peakAiSites = 1, aiProductive = 0, aiLogistics = 0, aiMoves = 0;
            var elapsed = new List<long>();

            // 29 closes leave the interactive session at turn 30, matching
            // the prior R235 Windows checkpoint. Timing is observational.
            for (ulong cycle = 1UL; cycle <= 29UL; cycle++)
            {
                var before = host.Snapshot;
                var beforeHash = host.StateHash;
                bool humanQueued = false;
                if (humanPurchaseCycle == 0UL && !_demographicQueueButton.Disabled)
                {
                    _demographicQueueButton.EmitSignal(Button.SignalName.Pressed);
                    if (host.StateHash != beforeHash ||
                        host.GetHumanDemographicQuote(human, 1UL).PendingQuantity != 1UL)
                        throw new InvalidOperationException("Human purchase was not staged privately.");
                    humanQueued = true;
                }

                _integratedPurchasedReceipt = null;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                EndFineCollectiveTurn();
                timer.Stop();
                elapsed.Add(timer.ElapsedMilliseconds);
                var receipt = _integratedPurchasedReceipt ??
                    throw new InvalidOperationException($"Integrated demographic close rejected at cycle {cycle}.");
                var after = host.Snapshot;
                if (!receipt.ReplayMatched || receipt.Hash != host.StateHash ||
                    receipt.Hash == beforeHash || receipt.Turn.Value != cycle ||
                    host.NextTurn.Value != cycle + 1UL ||
                    after.Treasury.LastSettledEconomicCycleOrdinal != cycle ||
                    receipt.PersistentTimeline is null || receipt.TemporalEconomicSettlement is null ||
                    host.GetLastClosedEconomicTradeView(human).EconomicCycleOrdinal != cycle ||
                    host.PendingOrderCount != 0)
                    throw new InvalidOperationException($"Committed turn/replay mismatch at cycle {cycle}.");

                var oldIds = before.Workers.Workers.Select(w => w.Id).ToHashSet();
                var newborns = after.Workers.Workers.Where(w => !oldIds.Contains(w.Id)).ToArray();
                if (after.Workers.Workers.Count != before.Workers.Workers.Count + newborns.Length ||
                    newborns.Any(w => w.BirthWaitTurns != 0UL || !w.NatalNode.HasValue ||
                        w.NatalNode != w.TacticalTileId ||
                        after.WorkerOrders.Queues.Single(q => q.Worker == w.Id).Orders.Count != 0 ||
                        receipt.PersistentTimeline.Slices.Any(s => s.Worker == w.Id)))
                    throw new InvalidOperationException("Unexpected free/lost worker or same-cycle newborn activity.");

                foreach (var owner in owners)
                {
                    var oldLedger = before.PurchasedDemography.Get(owner);
                    var ledger = after.PurchasedDemography.Get(owner);
                    var oldCash = before.Treasury.Get(owner);
                    var cash = after.Treasury.Get(owner);
                    ulong births = (ulong)newborns.Count(w => w.Owner == owner);
                    var expectedDebit = GlobalArenaMoneyScale.FromMoneyMicro(
                        price.QuoteBatchMoneyMicro(oldLedger.NextHistoricalIndex, births));
                    if (ledger.InitialWorkers != 1UL ||
                        ledger.PurchasedWorkers != checked(oldLedger.PurchasedWorkers + births) ||
                        (ulong)after.Workers.Workers.Count(w => w.Owner == owner) != ledger.HistoricalAcquisitions ||
                        cash.TotalDemographicPurchaseDebitsScaled - oldCash.TotalDemographicPurchaseDebitsScaled != expectedDebit ||
                        cash.CashScaled.Sign < 0 ||
                        cash.CashScaled != cash.TotalSettledEconomicIncomeScaled -
                            cash.TotalInfrastructureInvestmentScaled - cash.TotalNavalFareDebitsScaled -
                            cash.TotalDemographicPurchaseDebitsScaled)
                        throw new InvalidOperationException($"Historical census or cash mismatch at cycle {cycle}.");
                    var oldPeople = before.Workers.Workers.Where(w => w.Owner == owner).ToDictionary(w => w.Id);
                    int resets = 0;
                    foreach (var person in after.Workers.Workers.Where(w => w.Owner == owner && oldIds.Contains(w.Id)))
                    {
                        var oldWait = oldPeople[person.Id].BirthWaitTurns!.Value;
                        if (person.BirthWaitTurns == 0UL && oldWait > 0UL) resets++;
                        else if (person.BirthWaitTurns != checked(oldWait + 1UL))
                            throw new InvalidOperationException("Existing waiting counter did not advance or reset.");
                    }
                    if ((ulong)resets != births)
                        throw new InvalidOperationException("Number of distinct parent resets does not match births.");
                }

                int humanBirths = newborns.Count(w => w.Owner == human);
                int aiBirths = newborns.Count(w => w.Owner == ai);
                if (humanBirths != (humanQueued ? 1 : 0) || aiBirths > 1 ||
                    host.GetHumanLastPurchasedNewborns(human).Count != humanBirths)
                    throw new InvalidOperationException("Human/AI birth authority or own read boundary failed.");
                if (humanBirths != 0)
                {
                    humanPurchaseCycle = cycle;
                    if (_demographicSummary?.Text.Contains("Last births: 1", StringComparison.Ordinal) != true)
                        throw new InvalidOperationException("Population readout did not reflect the committed birth.");
                }
                if (aiBirths != 0 && firstAiPurchaseCycle == 0UL) firstAiPurchaseCycle = cycle;
                int aiSites = after.Workers.Workers.Where(w => w.Owner == ai &&
                    w.TacticalTileId.HasValue && w.Activity != PhysicalWorkerActivity.Traveling &&
                    !after.EdgeTravels.Pending.Any(p => p.Worker == w.Id))
                    .Select(w => w.TacticalTileId!.Value).Distinct().Count();
                peakAiSites = Math.Max(peakAiSites, aiSites);
                aiProductive += receipt.ProductiveFunding.Count(e => e.Investor == ai);
                aiLogistics += receipt.PersistentFunding.Count(e => e.Investor == ai);
                aiMoves += receipt.PersistentEdits.Count(e => e.Edit.Issuer == ai &&
                    e.Edit.OrderKind == GlobalArena.Runtime.PersistentOrders.FoundationWorkerOrderKind.Move);
                GD.Print($"GA_NEXT_R236G_CYCLE cycle={cycle} hash={host.StateHash.HexDigest} human_workers={after.PurchasedDemography.Get(human).HistoricalAcquisitions} ai_workers={after.PurchasedDemography.Get(ai).HistoricalAcquisitions} ai_sites={aiSites} human_births={humanBirths} ai_births={aiBirths} replay=true");
                GD.Print($"GA_NEXT_R236G_TIMING cycle={cycle} elapsed_ms={timer.ElapsedMilliseconds}");
                // After exercising the real population controls, return to
                // the map and avoid repeated private UI calendar quotations.
                if (humanPurchaseCycle != 0UL && _civilizationWindowLayer is not null)
                    _civilizationWindowLayer.Visible = false;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var final = host.Snapshot;
            var aiPurchased = final.PurchasedDemography.Get(ai).PurchasedWorkers;
            if (humanPurchaseCycle == 0UL || firstAiPurchaseCycle == 0UL ||
                aiPurchased == 0UL || host.StateHash.FormatVersion != 33U)
                throw new InvalidOperationException("Both singleton civilizations must complete a paid birth.");
            var sorted = elapsed.OrderBy(v => v).ToArray();
            GD.Print($"GA_NEXT_R236G_OBSERVATION first_human={humanPurchaseCycle} first_ai={firstAiPurchaseCycle} ai_purchased={aiPurchased} ai_peak_sites={peakAiSites} ai_moves={aiMoves} ai_productive={aiProductive} ai_logistics={aiLogistics} median_ms={sorted[sorted.Length / 2]} max_ms={sorted[^1]}");
            GD.Print("GA_NEXT_R236G_INTEGRATED_PASS cycles=29 turn=30 human_paid=true ai_paid=true cash=true replay=true hash_v=33");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"GA_NEXT_R236G_INTEGRATED_FAIL {ex}");
            GetTree().Quit(1);
        }
    }
}
