using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R85 explicit fine-first economic close, no host/session save migration.
/// This is a REAL authoritative WorldState treasury settlement; unlike an
/// outside candidate calculation it rederives the receipt on replay.
/// Caller must provide an already classified workforce, signed G18 natural
/// crossing inventory and explicit market/tax/cargo/monetary configuration.
/// </summary>
public static class FoundationFineFirstEconomicCycleGate
{
    public sealed record Receipt(
        FoundationEconomicSalesSettledEvent Event,
        WorldState Next,
        WorldStateHash Hash,
        bool ReplayMatched);

    public static WorldState OpenNewEconomy(
        WorldState prepared,
        WorldGenerationResult strategicWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fine,
        FoundationFineFirstWorldgenIdentity signature,
        FoundationGameplayEconomicConfiguration policy,
        FoundationExperimentalShallowFreightPolicy? researchShallow = null)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(policy);
        if (prepared.WorldBinding is null ||
            prepared.WorldBinding.IsFineFirstWorld ||
            prepared.Transport.IsInitialized || prepared.Treasury.IsInitialized ||
            !policy.OpeningBalances.Select(x => x.Civilization)
                .SequenceEqual(prepared.Civilizations.Civilizations) ||
            !policy.Settlement.TaxRates.Select(x => x.Civilization)
                .SequenceEqual(prepared.Civilizations.Civilizations) ||
            prepared.Workers.Workers.Any(worker =>
                !policy.Settlement.Demand.Commodities.Contains(worker.Commodity) ||
                !policy.Settlement.Cargo.Commodities.Contains(worker.Commodity)))
            throw new InvalidOperationException(
                "Fine-first opening requires a pristine tactical workforce " +
                "and one explicit policy/account per real civilization.");
        var signed = prepared.PromoteFineFirstBeforeTransport(
            strategicWorld, incidence, fine, signature);
        var natural = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            strategicWorld, incidence, fine, policy.OperatingTransport,
            policy.MonetaryConstruction, researchShallow);
        return signed.DeployInitialFineFirstPhysicalTransport(
                natural.Transport, strategicWorld, incidence, fine, signature)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(
                    policy.OpeningBalances));
    }

    public static Receipt CloseAndReplay(
        WorldState current,
        WorldGenerationResult strategicWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fine,
        FoundationFineFirstWorldgenIdentity signature,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(current);
        var signed = RuntimeWorldBinding.FromFineFirst(
            strategicWorld, incidence, fine, signature);
        if (current.WorldBinding != signed ||
            !current.Transport.HasMonetaryConstructionCosts ||
            !current.Treasury.IsInitialized)
            throw new InvalidOperationException(
                "Economic settlement requires actual signed fine-first " +
                "freight, complete monetary infrastructure and treasury.");
        var executor = new FoundationEconomicSalesSettlementEventExecutor(
            strategicWorld, incidence, policy, fine, signature);
        var (ev, next) = executor.CloseActualEconomicCycle(current, context);
        var replay = executor.Execute(current, ev, context);
        var hasher = new CanonicalWorldStateHasher();
        var hash = hasher.Compute(next);
        if (hash != hasher.Compute(replay))
            throw new InvalidOperationException(
                "G18 economic close must reproduce the identical canonical hash.");
        return new Receipt(ev, next, hash, true);
    }
}
