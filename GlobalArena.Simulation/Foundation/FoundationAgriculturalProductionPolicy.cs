using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Immutable experimental crop output policy. One projection per signed
/// geography, explicit commodity mapping and whole-turn base yield.</summary>
public sealed class FoundationAgriculturalProductionPolicy
{
    private readonly AgriculturalGeographyProjection _projection;
    private readonly IReadOnlyDictionary<CropId, CommodityId> _items;
    private readonly GoldbergParameters _fine;
    private readonly ulong _baseYield;

    public FoundationAgriculturalProductionPolicy(FoundationFineFirstPhysicalGeography geography,
        PhysicalTacticalIncidenceMap incidence, IReadOnlyDictionary<CropId, CommodityId> items,
        ulong baseYieldMicro)
    {
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(items);
        if (baseYieldMicro == 0 || geography.FineParameters != incidence.Refinement.FineParameters ||
            items.Count != CropCatalog.Count || CropCatalog.All.Any(c => !items.TryGetValue(c.Id, out var id) || !id.IsValid) ||
            items.Values.Distinct().Count() != CropCatalog.Count)
            throw new ArgumentException("Explicit distinct six-crop market mapping and compatible fine geography required.");
        _items = new System.Collections.ObjectModel.ReadOnlyDictionary<CropId, CommodityId>(new Dictionary<CropId, CommodityId>(items));
        _baseYield = baseYieldMicro;
        _fine = geography.FineParameters;
        int count = geography.FineElevationRaw.Count;
        var adjacent = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        foreach (var edge in incidence.FineTopology.Edges)
        {
            int a = checked((int)edge.IncidentCellIds[0].Value - 1);
            int b = checked((int)edge.IncidentCellIds[1].Value - 1);
            adjacent[a].Add(b); adjacent[b].Add(a);
        }
        var nodes = Enumerable.Range(0, count).Select(i => new AgriculturalGeographicNode(
            i + 1L, checked((int)geography.FineElevationRaw[i]), checked((int)geography.FineTemperatureRaw[i]),
            checked((int)geography.FineMoistureRaw[i]),
            geography.SurfaceAtIndex(i) == FoundationFineSurfaceKind.Land &&
            geography.FineTemperatureRaw[i] > FoundationFineFirstPhysicalGeography.FirmSeaIceTemperatureRaw)).ToArray();
        var offsets = new int[count + 1];
        var flat = new List<int>();
        for (int i = 0; i < count; i++)
        {
            flat.AddRange(adjacent[i].OrderBy(n => n));
            offsets[i + 1] = flat.Count;
        }
        _projection = AgriculturalGeographyProjection.Build(new AgriculturalLogicalGeography(nodes, offsets, flat));
    }

    public IReadOnlyCollection<CommodityId> Commodities => _items.Values.ToArray();
    public (CommodityId Commodity, ulong WholeTurnOutput) Resolve(PhysicalTacticalTileId node, CropId crop)
    {
        if (!node.IsValid || node.FineGoldbergParameters != _fine) throw new ArgumentException("Foreign crop node.");
        int index = checked((int)node.FineStrategicCellId.Value - 1);
        int score = _projection.GetSuitability(index, crop);
        return (_items[crop], checked((ulong)((UInt128)_baseYield * (uint)score / AgriculturalSuitability.Scale)));
    }
}
