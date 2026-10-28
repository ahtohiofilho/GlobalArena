using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Caller-supplied, version-labelled Neolithic foundation requirements.
/// These are NOT authoritative catalog definitions until the resource and
/// construction catalog is ratified. Two different commodities are mandatory,
/// but their numeric IDs and quantities are not hard-coded in the kernel.
/// </summary>
public sealed class EconomicNeolithicFoundationRecipe
{
    public ulong CatalogVersion { get; }
    public CommodityId WoodCommodityId { get; }
    public CommodityId StoneCommodityId { get; }
    public ulong RawWoodRequired { get; }
    public ulong RawStoneRequired { get; }

    public EconomicNeolithicFoundationRecipe(
        ulong catalogVersion,
        CommodityId woodCommodityId, CommodityId stoneCommodityId,
        ulong rawWoodRequired, ulong rawStoneRequired)
    {
        if (catalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(catalogVersion));
        if (!woodCommodityId.IsValid || !stoneCommodityId.IsValid ||
            woodCommodityId == stoneCommodityId)
            throw new ArgumentException("Foundation requires distinct valid wood and stone commodity IDs.");
        if (rawWoodRequired == 0UL || rawStoneRequired == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawWoodRequired),
                "Founding needs a positive amount of both materials.");

        CatalogVersion = catalogVersion;
        WoodCommodityId = woodCommodityId;
        StoneCommodityId = stoneCommodityId;
        RawWoodRequired = rawWoodRequired;
        RawStoneRequired = rawStoneRequired;
    }
}

/// <summary>
/// A pure candidate bundle, not a simulation command or completed construction
/// attestation. Its material book is external to WorldState pending hash
/// reconciliation with the transport-facility state PR.
/// </summary>
public sealed class EconomicNeolithicSiteFoundingResult
{
    public WorldState ProposedWorld { get; }
    public EconomicMaterialReserveBook ProposedReserves { get; }
    public WorldStateHash PriorWorldHash { get; }
    public WorldStateHash ProposedWorldHash { get; }

    internal EconomicNeolithicSiteFoundingResult(
        WorldState world, EconomicMaterialReserveBook reserves,
        WorldStateHash priorHash, WorldStateHash proposedHash)
    {
        ProposedWorld = world;
        ProposedReserves = reserves;
        PriorWorldHash = priorHash;
        ProposedWorldHash = proposedHash;
    }
}

/// <summary>
/// G3-B3 reference: take people/workers from an existing point and claim an
/// adjacent, unowned LAND strategic cell using explicit wood and stone held
/// at the origin. Territory, economy and population are proposed together.
/// No fine-tile claim, roads, production facilities, revenue, or scheduler
/// authorization is inferred.
/// </summary>
public sealed class EconomicNeolithicSiteFoundingResolver
{
    private readonly EconomicAdjacentSiteRelocationResolver _relocation = new();
    private readonly EconomicConstructionMaterialDebitCandidateResolver _debit = new();
    private readonly CanonicalWorldStateHasher _hasher = new();

    public EconomicNeolithicSiteFoundingResult Propose(
        WorldGenerationResult generatedWorld, WorldState originalWorld,
        CivilizationId owner, WorldStateHash expectedWorldHash,
        EconomicMaterialReserveBook reserves,
        EconomicAdjacentSiteRelocationOrder relocation,
        EconomicNeolithicFoundationRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(originalWorld);
        ArgumentNullException.ThrowIfNull(reserves);
        ArgumentNullException.ThrowIfNull(relocation);
        ArgumentNullException.ThrowIfNull(recipe);

        var priorHash = _hasher.Compute(originalWorld);
        if (expectedWorldHash != priorHash)
            throw new InvalidOperationException("Founding references a stale or different WorldState.");
        if (originalWorld.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException("Founding requires the exact bound generated world.");
        if (!owner.IsValid || !originalWorld.Civilizations.Civilizations.Contains(owner))
            throw new ArgumentException("Founding civilization must exist.", nameof(owner));

        var newCell = relocation.DestinationStrategicCellId;
        if (!originalWorld.WorldBinding!.Contains(newCell))
            throw new InvalidOperationException("Founding target is outside the bound world.");
        if (originalWorld.Territory.TryGetController(newCell, out _))
            throw new InvalidOperationException("Founding cannot take over already controlled territory.");
        if (originalWorld.Economy.EconomicPoints.Any(p => p.StrategicCellId == newCell))
            throw new InvalidOperationException(
                "Founding target already contains an economic point; resolve its ownership first.");

        // Materials are local: reserves at any other point are not silently
        // transported to the founding expedition's source.
        var inputs = new[]
        {
            new EconomicConstructionMaterialRequirement(
                relocation.SourcePointId, recipe.WoodCommodityId,
                recipe.RawWoodRequired),
            new EconomicConstructionMaterialRequirement(
                relocation.SourcePointId, recipe.StoneCommodityId,
                recipe.RawStoneRequired)
        };
        // All debits are checked as one all-or-nothing candidate transition.
        var nextReserves = _debit.Debit(originalWorld, owner, reserves, inputs);

        // Stage territorial control only in a local immutable value; the
        // relocation resolver independently validates land, adjacency,
        // population/workforce balances and ownership of both cells.
        var newTerritory = new StrategicTerritoryRuntimeState(
            originalWorld.Territory.Controls.Append(
                new StrategicTerritoryControlEntry(newCell, owner)));
        var staged = originalWorld.WithTerritory(newTerritory);
        var nextWorld = _relocation.Apply(
            generatedWorld, staged, owner, relocation);

        return new EconomicNeolithicSiteFoundingResult(
            nextWorld, nextReserves, priorHash, _hasher.Compute(nextWorld));
    }
}
