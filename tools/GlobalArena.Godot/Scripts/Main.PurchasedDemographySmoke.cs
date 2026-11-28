using Godot;
using GlobalArena.Runtime;

namespace GlobalArena.Godot;

public partial class Main
{
    private async Task ExercisePurchasedDemographyUiSmoke()
    {
        try
        {
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var host = _tacticalHost ?? throw new InvalidOperationException("Missing purchased demography host.");
            if (!host.PurchasedDemographyEnabled || host.StateHash.FormatVersion != 32U ||
                host.Snapshot.Workers.Workers.GroupBy(w => w.Owner).Any(g => g.Count() != 1) ||
                host.Snapshot.Treasury.Accounts.Any(a => !a.CashScaled.IsZero))
                throw new InvalidOperationException("Expected zero-cash singleton opening.");
            OpenCivilizationWindow();
            if (_demographicTabs is null || _demographicTabs.GetTabCount() != 3 ||
                _demographicTabs.GetTabTitle(2) != "Population")
                throw new InvalidOperationException("Missing Population tab.");
            _demographicTabs.CurrentTab = 2;
            _demographicQuoteButton!.EmitSignal(Button.SignalName.Pressed);
            if (!_demographicQueueButton!.Disabled || !_demographicCancelButton!.Disabled)
                throw new InvalidOperationException("Zero-cash purchase must be unavailable.");
            var founder = host.Snapshot.Workers.Workers.Single(w => w.Owner == _humanCivilization);
            int closed = 0;
            while (_demographicQueueButton.Disabled && closed < 10)
            {
                var turn = host.NextTurn.Value;
                EndFineCollectiveTurn();
                if (host.NextTurn.Value != turn + 1UL ||
                    host.Snapshot.Workers.Workers.Count != 2 || host.StateHash.FormatVersion != 32U)
                    throw new InvalidOperationException("Singleton earning cycle failed or free growth occurred.");
                closed++;
            }
            if (_demographicQueueButton.Disabled)
                throw new InvalidOperationException("Founder did not earn enough for QA purchase.");
            var quote = host.GetHumanDemographicQuote(_humanCivilization, 1UL);
            var before = host.StateHash;
            var beforeCash = host.Snapshot.Treasury.Get(_humanCivilization).CashScaled;
            _demographicQueueButton.EmitSignal(Button.SignalName.Pressed);
            if (host.StateHash != before || _demographicCancelButton.Disabled ||
                host.GetHumanDemographicQuote(_humanCivilization, 1UL).PendingQuantity != 1UL)
                throw new InvalidOperationException("Private purchase did not reach the host.");
            _demographicCancelButton.EmitSignal(Button.SignalName.Pressed);
            if (host.StateHash != before || !_demographicCancelButton.Disabled ||
                host.GetHumanDemographicQuote(_humanCivilization, 1UL).PendingQuantity != 0UL)
                throw new InvalidOperationException("Actual cancel did not clear private purchase.");
            _demographicQueueButton.EmitSignal(Button.SignalName.Pressed);
            var purchaseTurn = host.NextTurn.Value;
            EndFineCollectiveTurn();
            var children = host.GetHumanLastPurchasedNewborns(_humanCivilization);
            if (host.NextTurn.Value != purchaseTurn + 1UL || host.StateHash.FormatVersion != 33U ||
                children.Count != 1 || children[0].NatalNode != founder.TacticalTileId ||
                host.Snapshot.Workers.Workers.Count != 3 ||
                host.Snapshot.PurchasedDemography.Get(_humanCivilization).PurchasedWorkers != 1UL ||
                host.Snapshot.Treasury.Get(_humanCivilization).TotalDemographicPurchaseDebitsScaled !=
                    GlobalArenaMoneyScale.FromMoneyMicro(quote.MoneyMicro) ||
                !_demographicCancelButton.Disabled || !_demographicQueueButton.Disabled ||
                _demographicSummary?.Text.Contains("Last births: 1", StringComparison.Ordinal) != true ||
                host.GetLastClosedEconomicTradeView(_humanCivilization).EconomicCycleOrdinal != purchaseTurn)
                throw new InvalidOperationException("Paid birth or refreshed population readout failed.");
            EndFineCollectiveTurn();
            if (host.NextTurn.Value != purchaseTurn + 2UL || host.Snapshot.Workers.Workers.Count != 3 ||
                host.GetHumanLastPurchasedNewborns(_humanCivilization).Count != 0 ||
                host.Snapshot.Workers.Workers.Single(w => w.Id == children[0].Id).BirthWaitTurns != 1UL)
                throw new InvalidOperationException("Newborn next-cycle waiting or no-free-growth failed.");
            GD.Print($"GA_NEXT_R236E2_UI_PASS founders=1 opening_cash=zero earned_cycles={closed} private_queue=true cancel=true paid_birth=true next_cycle=true hash_v=33");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"GA_NEXT_R236E2_UI_FAIL {ex}");
            GetTree().Quit(1);
        }
    }
}
