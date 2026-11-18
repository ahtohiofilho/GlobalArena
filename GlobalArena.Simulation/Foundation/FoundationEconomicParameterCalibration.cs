using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Per-civilization outcome of an UNCOMMITTED A/B parameter probe.
/// The projected net, tax and toll amounts do not create real cash.
/// Seller taxes credit that same civilization in the current v15 rules.
/// </summary>
public sealed record FoundationEconomicCalibrationCivilizationRow(
    CivilizationId Civilization,
    BigInteger SellerGrossScaled,
    BigInteger SellerNetScaled,
    BigInteger SellerTaxScaled,
    BigInteger ReceivedOwnerTollsScaled,
    BigInteger HypotheticalTreasuryCreditScaled);

/// <summary>
/// Immutable read-only evidence. Neither output, forecasts, nor potential
/// cash are actual journaled economic sales or legal investment funding.
/// An incomplete sale is explicitly flagged and must never be promoted.
/// </summary>
public sealed class FoundationEconomicCalibrationProbe
{
    public string Label { get; }
    public string PolicyFingerprintSha256 { get; }
    public WorldStateHash SourceWorldHash { get; }
    public WorldStateHash CandidateWorldHash { get; }
    public ulong SourceRevision { get; }
    public int RealWorkers { get; }
    public ulong DemandMicro { get; }
    public ulong ProducedGoodsMicro { get; }
    public ulong AllocatedGoodsMicro { get; }
    public bool WouldSettleAllProducedGoods =>
        ProducedGoodsMicro == AllocatedGoodsMicro;
    public bool IsCommitted => false;
    public ulong EdgeTraversalCargoMicro { get; }
    public int UsedRealEdges { get; }
    public string CargoUnitCode { get; }
    public BigInteger GrossSalesScaled { get; }
    public BigInteger PhysicalFreightScaled { get; }
    public BigInteger ExternalTollsScaled { get; }
    public BigInteger SellerTaxesScaled { get; }
    public BigInteger SellerNetScaled { get; }
    public BigInteger HypotheticalTreasuryCreditsScaled { get; }
    public IReadOnlyList<FoundationEconomicCalibrationCivilizationRow>
        Civilizations { get; }

    internal FoundationEconomicCalibrationProbe(
        string label, string fingerprint,
        WorldStateHash sourceHash, WorldStateHash candidateHash,
        ulong sourceRevision, int realWorkers,
        ulong demandMicro, ulong produced, ulong allocated,
        ulong edgeCargo, int edges, string cargoUnit,
        FoundationPhysicalEconomicAccountingProposal? accounting)
    {
        Label = label;
        PolicyFingerprintSha256 = fingerprint;
        SourceWorldHash = sourceHash;
        CandidateWorldHash = candidateHash;
        SourceRevision = sourceRevision;
        RealWorkers = realWorkers;
        DemandMicro = demandMicro;
        ProducedGoodsMicro = produced;
        AllocatedGoodsMicro = allocated;
        EdgeTraversalCargoMicro = edgeCargo;
        UsedRealEdges = edges;
        CargoUnitCode = cargoUnit;
        GrossSalesScaled = accounting?.GrossSalesScaled ?? BigInteger.Zero;
        PhysicalFreightScaled = accounting?.PhysicalFreightScaled ??
            BigInteger.Zero;
        ExternalTollsScaled = accounting?.ExternalTollsScaled ??
            BigInteger.Zero;
        SellerTaxesScaled = accounting?.RevenueTaxScaled ?? BigInteger.Zero;
        SellerNetScaled = accounting?.NetSellerReceiptsScaled ??
            BigInteger.Zero;
        var rows = accounting?.Accounts.Select(a =>
            new FoundationEconomicCalibrationCivilizationRow(
                a.Civilization, a.SalesGrossScaled, a.SellerNetScaled,
                a.RevenueTaxScaled, a.TollIncomeScaled,
                a.SellerNetScaled + a.RevenueTaxScaled +
                    a.TollIncomeScaled)).ToArray() ??
            Array.Empty<FoundationEconomicCalibrationCivilizationRow>();
        Civilizations = Array.AsReadOnly(rows);
        HypotheticalTreasuryCreditsScaled = rows.Aggregate(
            BigInteger.Zero,
            (sum, row) => sum + row.HypotheticalTreasuryCreditScaled);
    }
}

/// <summary>
/// Comparison of exactly two fully specified policies on the SAME real
/// physical world and workforce. Delta is right minus left, never a
/// comparison of two unrelated random world draws or settlement receipts.
/// </summary>
public sealed class FoundationEconomicCalibrationComparison
{
    public FoundationEconomicCalibrationProbe Baseline { get; }
    public FoundationEconomicCalibrationProbe Alternative { get; }
    public BigInteger DemandMicroDelta { get; }
    public BigInteger AllocatedGoodsMicroDelta { get; }
    public BigInteger EdgeTraversalCargoMicroDelta { get; }
    public BigInteger GrossSalesScaledDelta { get; }
    public BigInteger PhysicalFreightScaledDelta { get; }
    public BigInteger SellerTaxesScaledDelta { get; }
    public BigInteger HypotheticalTreasuryCreditsScaledDelta { get; }
    public bool IsCommitted => false;

    internal FoundationEconomicCalibrationComparison(
        FoundationEconomicCalibrationProbe baseline,
        FoundationEconomicCalibrationProbe alternative)
    {
        if (baseline.SourceWorldHash != alternative.SourceWorldHash ||
            baseline.SourceRevision != alternative.SourceRevision ||
            baseline.RealWorkers != alternative.RealWorkers ||
            baseline.CargoUnitCode != alternative.CargoUnitCode)
            throw new InvalidOperationException(
                "Calibration must compare exactly one real worker world " +
                "and a shared declared physical cargo unit.");
        Baseline = baseline;
        Alternative = alternative;
        DemandMicroDelta = (BigInteger)alternative.DemandMicro -
            baseline.DemandMicro;
        AllocatedGoodsMicroDelta =
            (BigInteger)alternative.AllocatedGoodsMicro -
            baseline.AllocatedGoodsMicro;
        EdgeTraversalCargoMicroDelta =
            (BigInteger)alternative.EdgeTraversalCargoMicro -
            baseline.EdgeTraversalCargoMicro;
        GrossSalesScaledDelta = alternative.GrossSalesScaled -
            baseline.GrossSalesScaled;
        PhysicalFreightScaledDelta = alternative.PhysicalFreightScaled -
            baseline.PhysicalFreightScaled;
        SellerTaxesScaledDelta = alternative.SellerTaxesScaled -
            baseline.SellerTaxesScaled;
        HypotheticalTreasuryCreditsScaledDelta =
            alternative.HypotheticalTreasuryCreditsScaled -
            baseline.HypotheticalTreasuryCreditsScaled;
    }
}

/// <summary>
/// Precommit calibration facility. NO static default profile and NO ability
/// to accept economic sales, grant money, mutate the canonical session,
/// post market prices, or replace fixed-freight rules. Everything derives
/// from explicit policies and one frozen authentic v11 worker census.
/// </summary>
public static class FoundationEconomicParameterCalibration
{
    /// <summary>
    /// Stable configuration checksum for governance and reproducible A/B.
    /// This does NOT turn any experimental profile into approved gameplay.
    /// Covers every declared coefficient and money/input setting, not a
    /// label or managed-object reference.
    /// </summary>
    public static string Fingerprint(
        FoundationGameplayEconomicConfiguration profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var canonical = new StringBuilder("GA-ECONOMIC-POLICY-PROBE-V1\n");
        static void Add(StringBuilder target, string key, ulong number) =>
            target.Append(key).Append('=').Append(
                number.ToString(CultureInfo.InvariantCulture)).Append('\n');
        var transport = profile.OperatingTransport;
        foreach (var (name, band) in new[]
        {
            ("land", transport.Land),
            ("coast", transport.Coast),
            ("sea", transport.Sea)
        })
        {
            Add(canonical, name + ".free",
                band.FreePhysicalCostMicroPerWholeCargoUnit);
            Add(canonical, name + ".capacity",
                band.CargoMicroAtReferenceLoad);
        }
        Add(canonical, "land.height.linear",
            transport.LandSlopeCostMicroPerNormalizedElevation);
        var money = profile.MonetaryConstruction;
        Add(canonical, "capital.land", money.LandBaseCostMoneyMicro);
        Add(canonical, "capital.coast", money.CoastBaseCostMoneyMicro);
        Add(canonical, "capital.sea", money.SeaBaseCostMoneyMicro);
        Add(canonical, "capital.height.squared",
            money.LandHeightSquaredCostMoneyMicro);

        var settlement = profile.Settlement;
        Add(canonical, "solver.maxProducerPasses",
            (ulong)settlement.MaxProducerPasses);
        var code = settlement.Cargo.CargoUnitCode;
        Add(canonical, "cargo.code.utf8length",
            (ulong)Encoding.UTF8.GetByteCount(code));
        canonical.Append("cargo.code=").Append(code).Append('\n');
        foreach (var commodity in settlement.Demand.Commodities)
        {
            Add(canonical, "commodity", commodity.Value);
            Add(canonical, "cargo.convert." + commodity.Value,
                settlement.Cargo.CargoMicro(commodity, 1_000_000UL));
            foreach (var profileId in
                Enum.GetValues<FoundationPhysicalDemandProfile>())
                Add(canonical,
                    "demand." + (uint)profileId + "." + commodity.Value,
                    settlement.Demand.IntensityMicroPerWorker(
                        profileId, commodity));
        }
        foreach (var tax in settlement.TaxRates)
        {
            Add(canonical, "tax.civilization", tax.Civilization.Value);
            Add(canonical, "tax.basisPoints",
                tax.GrossRevenueBasisPoints);
        }
        foreach (var balance in profile.OpeningBalances)
        {
            Add(canonical, "cash.civilization", balance.Civilization.Value);
            Add(canonical, "cash.moneyMicro", balance.OpeningMoneyMicro);
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    public static FoundationEconomicCalibrationProbe Preview(
        WorldState frozenWorld,
        WorldGenerationResult generated,
        PhysicalTacticalIncidenceMap incidence,
        FoundationGameplayEconomicConfiguration profile,
        string label)
    {
        ArgumentNullException.ThrowIfNull(frozenWorld);
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException(
                "Every calibration scenario must be explicitly labeled.",
                nameof(label));
        var civilizations = frozenWorld.Civilizations.Civilizations;
        if (!frozenWorld.Workers.HasAuthoritativeOccupations ||
            frozenWorld.WorldBinding !=
                RuntimeWorldBinding.FromGeneratedWorld(generated) ||
            frozenWorld.Treasury.IsInitialized ||
            frozenWorld.Transport.IsInitialized ||
            !profile.OpeningBalances.Select(b => b.Civilization)
                .SequenceEqual(civilizations) ||
            !profile.Settlement.TaxRates.Select(t => t.Civilization)
                .SequenceEqual(civilizations) ||
            frozenWorld.Workers.Workers.Any(w =>
                !profile.Settlement.Demand.Commodities.Contains(w.Commodity) ||
                !profile.Settlement.Cargo.Commodities.Contains(w.Commodity)))
            throw new InvalidOperationException(
                "Calibration requires one pristine bound v11 world and " +
                "complete explicit civic/commodity policy coverage.");

        var hasher = new CanonicalWorldStateHasher();
        var original = hasher.Compute(frozenWorld);
        // With-clauses materialize an isolated, new WORLD CANDIDATE.
        // Neither the passed world, canonical session nor treasury gets
        // modified; these computations are NEVER system settlement events.
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            generated, incidence, profile.OperatingTransport,
            profile.MonetaryConstruction);
        var candidate = frozenWorld.DeployInitialPhysicalTransport(
                seeded.Transport, generated, incidence)
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState
                .CreateOpening(profile.OpeningBalances));
        var plan = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(
                candidate, incidence, profile.Settlement.Demand,
                profile.Settlement.Cargo,
                maxProducerPasses: profile.Settlement.MaxProducerPasses);
        var audit = plan.Distribution.AllocationAudit;
        // Incomplete sales may be useful diagnostic feedback but are
        // NEVER presented as a closed cycle or as available financing.
        FoundationPhysicalEconomicAccountingProposal? accounting = null;
        if (audit.TotalAvailableGoodsMicro ==
            audit.TotalAllocatedGoodsMicro)
        {
            accounting = FoundationPhysicalEconomicAccountingReconciler
                .EvaluateCandidate(
                    candidate.Workers, incidence,
                    profile.Settlement.Demand, profile.Settlement.Cargo,
                    plan, profile.Settlement.TaxRates);
            if (accounting.GrossSalesScaled !=
                accounting.PhysicalFreightScaled +
                accounting.ExternalTollsScaled +
                accounting.RevenueTaxScaled +
                accounting.NetSellerReceiptsScaled)
                throw new InvalidOperationException(
                    "Calibration accounting must reconcile exact money.");
        }
        if (hasher.Compute(frozenWorld) != original ||
            candidate.Treasury.HasClosedEconomicCycle ||
            candidate.Treasury.Accounts.Any(a =>
                a.TotalSettledEconomicIncomeScaled != BigInteger.Zero))
            throw new InvalidOperationException(
                "A/B preview modified authentic world or minted money.");

        return new FoundationEconomicCalibrationProbe(
            label, Fingerprint(profile), original,
            hasher.Compute(candidate), frozenWorld.Revision,
            candidate.Workers.Workers.Count,
            audit.MarketInputs.TotalCommodityDemandMicro,
            audit.TotalAvailableGoodsMicro, audit.TotalAllocatedGoodsMicro,
            audit.Freight.TotalEdgeTraversalCargoMicro,
            audit.Freight.UsedEdges.Count,
            profile.Settlement.Cargo.CargoUnitCode, accounting);
    }

    public static FoundationEconomicCalibrationComparison Compare(
        WorldState frozenWorld,
        WorldGenerationResult generated,
        PhysicalTacticalIncidenceMap incidence,
        FoundationGameplayEconomicConfiguration baseline,
        FoundationGameplayEconomicConfiguration alternative)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(alternative);
        if (!baseline.Settlement.Demand.Commodities.SequenceEqual(
                alternative.Settlement.Demand.Commodities) ||
            baseline.Settlement.Cargo.CargoUnitCode !=
                alternative.Settlement.Cargo.CargoUnitCode ||
            baseline.Settlement.MaxProducerPasses !=
                alternative.Settlement.MaxProducerPasses ||
            !baseline.OpeningBalances.SequenceEqual(
                alternative.OpeningBalances))
            throw new InvalidOperationException(
                "A/B scenarios must share the SAME commodity catalog, " +
                "cargo unit, solver budget and opening cash. Differences " +
                "must be confined to declared economic coefficients.");
        return new FoundationEconomicCalibrationComparison(
            Preview(frozenWorld, generated, incidence,
                baseline, "baseline"),
            Preview(frozenWorld, generated, incidence,
                alternative, "alternative"));
    }
}
