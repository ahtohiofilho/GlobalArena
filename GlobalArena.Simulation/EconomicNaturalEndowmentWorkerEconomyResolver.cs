using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Opt-in G3-E primitive economy over version-2 natural endowments.
/// The version must be selected explicitly by the caller, so a replay cannot
/// silently switch from G3-D's original version-1 productivity formula.
/// </summary>
public sealed class EconomicNaturalEndowmentWorkerEconomyResolver
{
    private readonly EconomicPrimitiveWorkerEconomyResolver _flows = new();

    public uint DerivationVersion { get; }

    public EconomicNaturalEndowmentWorkerEconomyResolver(uint derivationVersion)
    {
        if (derivationVersion != StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion)
            throw new ArgumentOutOfRangeException(
                nameof(derivationVersion), "Unsupported natural endowment derivation version.");
        DerivationVersion = derivationVersion;
    }

    public EconomicProductionDemandResult Resolve(
        WorldGenerationResult generatedWorld, WorldState world,
        EconomicPrimitiveCommodityMapping commodities,
        IEnumerable<EconomicDemandProfile> workerDemandProfiles)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(commodities);
        ArgumentNullException.ThrowIfNull(workerDemandProfiles);

        if (world.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException(
                "Natural endowment production requires the bound generated world.");

        var endowments = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generatedWorld);
        var moisture = generatedWorld.StrategicClimateFields.WaterAvailability;
        var temperature = generatedWorld.StrategicClimateFields.Temperature;
        const long d = StrategicScalarField.Denominator;

        var potentials = new List<EconomicProductionPotential>();
        foreach (var point in world.Economy.EconomicPoints)
        {
            var cell = point.StrategicCellId;
            if (generatedWorld.StrategicPhysicalFields.LandWater.GetKind(cell)
                != StrategicLandWaterKind.Land)
                throw new InvalidOperationException(
                    "Primitive production is not supported at water-based economic points.");

            var availability = moisture.GetRawValue(cell);
            var temp = temperature.GetRawValue(cell);
            if (availability < 0L || availability > d || temp < -d || temp > d)
                throw new InvalidOperationException("Climate field is outside accepted range.");

            foreach (var allocation in point.ActivityAllocations)
            {
                CommodityId commodity;
                long rate;
                switch (allocation.Kind)
                {
                    case EconomicActivityKind.Agriculture:
                        commodity = commodities.Food;
                        // Physical suitability, water and thermal comfort
                        // jointly determine primitive cultivation productivity.
                        rate = checked((long)(
                            ((Int128)endowments.SoilSuitability.GetRawValue(cell)
                                * availability * (d - Math.Abs(temp)))
                            / ((Int128)d * d)));
                        break;
                    case EconomicActivityKind.Forestry:
                        commodity = commodities.Wood;
                        rate = endowments.WoodyVegetation.GetRawValue(cell);
                        break;
                    case EconomicActivityKind.Mining:
                        commodity = commodities.Stone;
                        rate = endowments.ExposedStone.GetRawValue(cell);
                        break;
                    default:
                        // Logistics labor contributes to demand but not
                        // primitive extraction production.
                        continue;
                }

                if (rate > 0L)
                    potentials.Add(new EconomicProductionPotential(
                        point.Id, commodity, allocation.Kind, rate));
            }
        }

        // No facilities, mandatory roads/terminals, or reserve inputs.
        // Every worker generates demand, including those with zero output.
        return _flows.Resolve(generatedWorld, world, potentials, workerDemandProfiles);
    }
}
