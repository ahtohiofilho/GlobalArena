using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// All numerical game-economy rules arrive EXPLICITLY from a trusted
/// world/session configuration owner. This type has NO hidden default
/// tax, no default demand coefficient, no implicit cargo equivalence,
/// no artificial initial money, and no compulsory builder workforce.
/// A TEST fixture may construct an instance, but it is never promoted
/// automatically to the ordinary client game configuration.
/// </summary>
public sealed class FoundationGameplayEconomicConfiguration
{
    public FoundationNaturalTransportSeedingPolicy OperatingTransport { get; }
    public FoundationMonetaryConstructionPolicy MonetaryConstruction { get; }
    public FoundationAuthoritativeEconomicSettlementPolicy Settlement { get; }
    public IReadOnlyList<(CivilizationId Civilization, ulong OpeningMoneyMicro)>
        OpeningBalances { get; }

    public FoundationGameplayEconomicConfiguration(
        FoundationNaturalTransportSeedingPolicy operatingTransport,
        FoundationMonetaryConstructionPolicy monetaryConstruction,
        FoundationAuthoritativeEconomicSettlementPolicy settlement,
        IEnumerable<(CivilizationId Civilization, ulong OpeningMoneyMicro)>
            openingBalances)
    {
        OperatingTransport = operatingTransport ??
            throw new ArgumentNullException(nameof(operatingTransport));
        MonetaryConstruction = monetaryConstruction ??
            throw new ArgumentNullException(nameof(monetaryConstruction));
        Settlement = settlement ??
            throw new ArgumentNullException(nameof(settlement));
        ArgumentNullException.ThrowIfNull(openingBalances);
        var balances = openingBalances.OrderBy(x => x.Civilization.Value)
            .ToArray();
        if (balances.Length == 0 ||
            balances.Any(b => !b.Civilization.IsValid) ||
            balances.Select(b => b.Civilization).Distinct().Count() !=
                balances.Length)
            throw new ArgumentException(
                "A real economy needs explicit opening balance for EVERY " +
                "actual civilization, including explicit zero balances.",
                nameof(openingBalances));
        OpeningBalances = Array.AsReadOnly(balances);
    }
}

/// <summary>
/// Replacement of a prepared v11 tactical *demo* host by the exact
/// same authoritative session under a fully declared economy profile.
/// It never mints invented sales, makes materials into a construction
/// input, or overrides a transport state already deployed by gameplay.
/// This is a controlled bootstrap boundary, not a new market solver.
/// </summary>
public static class FoundationGameplayEconomicBootstrap
{
    public static FoundationTacticalHumanAiTurnHost Enable(
        FoundationTacticalHumanAiTurnHost prepared,
        FoundationGameplayEconomicConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(config);
        var current = prepared.Snapshot;
        var participants = current.Civilizations.Civilizations;
        if (prepared.NextTurn.Value != 1UL ||
            prepared.PendingOrderCount != 0 ||
            prepared.ReadyCivilizations.Count != 0)
            throw new InvalidOperationException(
                "An explicit economy can be bootstrapped only before the " +
                "FIRST real collective turn, never after any order or " +
                "closed worker-only turn.");
        if (!current.Workers.HasAuthoritativeOccupations ||
            current.Transport.IsInitialized || current.Treasury.IsInitialized ||
            current.Treasury.HasClosedEconomicCycle ||
            !config.OpeningBalances.Select(x => x.Civilization)
                .SequenceEqual(participants) ||
            !config.Settlement.TaxRates.Select(x => x.Civilization)
                .SequenceEqual(participants) ||
            current.Workers.Workers.Any(w =>
                !config.Settlement.Demand.Commodities.Contains(w.Commodity) ||
                !config.Settlement.Cargo.Commodities.Contains(w.Commodity)))
            throw new InvalidOperationException(
                "Economic bootstrap requires a pristine, real v11 world; " +
                "complete matching roster, tax, demand, cargo and explicit " +
                "treasury profiles. No implicit numerical rules.");

        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            prepared.Session.GeneratedWorld, prepared.Incidence,
            config.OperatingTransport, config.MonetaryConstruction);
        var money = CivilizationTreasuryRuntimeState.CreateOpening(
            config.OpeningBalances);
        var deployed = current
            .DeployInitialPhysicalTransport(seeded.Transport,
                prepared.Session.GeneratedWorld, prepared.Incidence)
            .DeployInitialTreasury(money);

        // Session owns the canonical snapshot and is updated only after
        // complete policy/census/transport preflight. The new host retains
        // exactly the same participants and AI policy identities.
        prepared.Session.AcceptSnapshot(deployed);
        return new FoundationTacticalHumanAiTurnHost(
            prepared.Session, prepared.Incidence,
            prepared.HumanCivilizations,
            prepared.SnapshotAiPoliciesForBootstrap(),
            enableMonetaryInfrastructure: true,
            settlementPolicy: config.Settlement);
    }
}
