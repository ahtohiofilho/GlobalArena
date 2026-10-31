using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// One real offered itinerary tied to the exact VS42 option handed to
/// VS52-VS55. Physical equivalence is rechecked from installed assets, not
/// inferred from a shared ServiceId or a claimed route name.
/// </summary>
public sealed record ExperimentalBuiltFreightServiceEvidence(
    EconomicTransportServiceOffer BuiltOffer,
    ExperimentalSharedFreightOption DeclaredService);

/// <summary>
/// Explicit institutional permission to exploit ONE constructed asset using
/// the stated operator. The infrastructure owner and operator may differ.
/// Valid revisions are inclusive; grant IDs are external institutional facts,
/// NOT generated contracts or proof of authority.
/// </summary>
public sealed record ExperimentalBuiltFreightAssetGrant(
    ulong AssetId,
    ExperimentalTransportInstitution InfrastructureOwner,
    ExperimentalTransportInstitution AuthorizedOperator,
    string GrantId,
    ulong ValidFromNetworkRevision,
    ulong ValidThroughNetworkRevision);

/// <summary>
/// A caller-supplied proposed network witness. The host must separately
/// authenticate its origin, construction, current access and network revision.
/// Retains read-only copies of the top-level collections; no private wallets.
/// </summary>
public sealed class ExperimentalBuiltTransportNetworkWitness
{
    public string WitnessId { get; }
    public ulong WitnessVersion { get; }
    public WorldStateHash WorldStateHash { get; }
    public ulong NetworkRevision { get; }
    public IReadOnlyList<EconomicTransportBuiltAsset> InstalledAssets { get; }
    public IReadOnlyList<ExperimentalBuiltFreightServiceEvidence> OfferedServices { get; }
    public IReadOnlyList<ExperimentalBuiltFreightAssetGrant> AssetGrants { get; }

    public ExperimentalBuiltTransportNetworkWitness(
        string witnessId, ulong witnessVersion,
        WorldStateHash worldStateHash, ulong networkRevision,
        IEnumerable<EconomicTransportBuiltAsset> installedAssets,
        IEnumerable<ExperimentalBuiltFreightServiceEvidence> offeredServices,
        IEnumerable<ExperimentalBuiltFreightAssetGrant> assetGrants)
    {
        if (string.IsNullOrWhiteSpace(witnessId) || witnessVersion == 0 ||
            worldStateHash.FormatVersion == 0 || networkRevision == 0)
            throw new ArgumentException(
                "A built-network witness requires a version, world hash and network revision.");
        ArgumentNullException.ThrowIfNull(installedAssets);
        ArgumentNullException.ThrowIfNull(offeredServices);
        ArgumentNullException.ThrowIfNull(assetGrants);
        WitnessId = witnessId;
        WitnessVersion = witnessVersion;
        WorldStateHash = worldStateHash;
        NetworkRevision = networkRevision;
        InstalledAssets = Array.AsReadOnly(installedAssets.ToArray());
        OfferedServices = Array.AsReadOnly(offeredServices.ToArray());
        AssetGrants = Array.AsReadOnly(assetGrants.ToArray());
    }
}

/// <summary>
/// A host-provided, fail-closed verifier for the same world, network version,
/// assets, and access policy. This interface is deliberately NOT implemented
/// by the kernel; a test stub is not a production authority.
/// </summary>
public interface IExperimentalBuiltTransportNetworkAuthority
{
    bool AcceptReadOnlyBuiltNetwork(
        ExperimentalBuiltTransportNetworkWitness witness,
        StrategicEdgeAccessSnapshot currentAccess);
}

public sealed record ExperimentalBuiltFreightServiceRight(
    EconomicPointId Source,
    EconomicPointId Destination,
    ulong ServiceId,
    ExperimentalTransportInstitution Operator,
    IReadOnlyList<ulong> BuiltAssetIds,
    IReadOnlyList<string> AssetGrantIds);

public sealed record ExperimentalBuiltFreightRightsProjection(
    string WitnessId,
    ulong WitnessVersion,
    WorldStateHash WorldStateHash,
    ulong NetworkRevision,
    ExperimentalTransportInstitutionalProjection InstitutionalProjection,
    IReadOnlyList<ExperimentalBuiltFreightServiceRight> ValidatedServiceRights,
    int DistinctOperationalAssets)
{
    // Authentication and hypothetical allocation are NOT a payment command.
    public bool CanAuthorizeRealSettlement => false;
}

/// <summary>
/// VS56 read-only physical and institutional attestation gate. Rebuilds every
/// offered route through VS42 FromBuiltOffer against the current fine grid,
/// constructed operational assets, anchors and current edge access. Rechecks
/// the exact VS55 institutional operator, owner permission and grant validity
/// for EACH physical asset along that route. Finally runs the VS55 audit
/// against original inputs, never a cached or self-certified cost receipt.
/// </summary>
public sealed class ExperimentalBuiltFreightInstitutionalRightsProjector
{
    public ExperimentalBuiltFreightRightsProjection Project(
        CanonicalPhysicalTacticalSession session,
        ExperimentalAbstractPurchasingProjection purchasing,
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> nominalQuotes,
        ExperimentalAbstractPriceFreightCalibrationPolicy calibrationPolicy,
        string physicalFreightNominalUnitId,
        IEnumerable<ExperimentalSimultaneousFreightLane> offeredLanes,
        IEnumerable<ExperimentalAbstractPhysicalFreightDelivery> selectedDeliveries,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        ExperimentalTransportInstitutionalRegistry registry,
        IExperimentalTransportInstitutionalRegistryAuthority registryAuthority,
        ExperimentalBuiltTransportNetworkWitness network,
        IExperimentalBuiltTransportNetworkAuthority networkAuthority,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> anchors,
        StrategicEdgeAccessSnapshot currentAccess)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(purchasing);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(nominalQuotes);
        ArgumentNullException.ThrowIfNull(calibrationPolicy);
        ArgumentNullException.ThrowIfNull(offeredLanes);
        ArgumentNullException.ThrowIfNull(selectedDeliveries);
        ArgumentNullException.ThrowIfNull(congestionPolicy);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(registryAuthority);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(networkAuthority);
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(currentAccess);

        var initialHash = session.CurrentHash;
        if (network.WorldStateHash != initialHash ||
            network.WorldStateHash != registry.WorldStateHash ||
            network.NetworkRevision != registry.NetworkRevision)
            throw new ArgumentException(
                "Network witness and institutional entitlement must reference the same current world and network revision.",
                nameof(network));

        // The anchor for every economic point must be the SAME inhabited
        // physical tile used to derive VS52 purchasing and production.
        var expected = new Dictionary<EconomicPointId, PhysicalTacticalTileId>();
        foreach (var buyer in purchasing.Buyers)
            if (!expected.TryAdd(buyer.Buyer, buyer.Tile))
                throw new ArgumentException("Duplicate physical buyer identity.", nameof(purchasing));
        foreach (var producer in purchasing.Producers)
        {
            if (expected.TryGetValue(producer.Producer, out var existing))
            {
                if (existing != producer.Tile)
                    throw new ArgumentException(
                        "One producer/buyer identity cannot occupy two physical tiles.",
                        nameof(purchasing));
            }
            else expected.Add(producer.Producer, producer.Tile);
        }
        if (anchors.Count != expected.Count ||
            expected.Any(x => !anchors.TryGetValue(x.Key, out var tile) ||
                tile != x.Value))
            throw new ArgumentException(
                "Built commercial route anchors must equal VS52 physical site anchors.",
                nameof(anchors));

        var assets = new Dictionary<ulong, EconomicTransportBuiltAsset>();
        foreach (var asset in network.InstalledAssets)
        {
            if (asset is null || !assets.TryAdd(asset.AssetId, asset) ||
                asset.AssetId == 0 || !asset.IsOperational)
                throw new ArgumentException(
                    "Network evidence may contain only uniquely identified operational built assets.",
                    nameof(network));
        }
        var services = new Dictionary<
            (EconomicPointId Source, EconomicPointId Destination, ulong ServiceId),
            ExperimentalBuiltFreightServiceEvidence>();
        foreach (var evidence in network.OfferedServices)
        {
            if (evidence is null || evidence.BuiltOffer is null ||
                evidence.DeclaredService is null)
                throw new ArgumentException(
                    "Complete built-service evidence must be explicit.",
                    nameof(network));
            var route = evidence.BuiltOffer.RouteKey;
            var key = (route.SourceEconomicPointId,
                route.DestinationEconomicPointId, evidence.BuiltOffer.ServiceId);
            if (!services.TryAdd(key, evidence) ||
                evidence.DeclaredService.ServiceId != key.ServiceId ||
                evidence.DeclaredService.RouteKey != route)
                throw new ArgumentException(
                    "Each physical service must have a unique matching built-offer identity.",
                    nameof(network));
        }
        var originalCarriers = purchasing.UnresolvedCarriers
            .Select(x => (x.Source, x.Destination, x.ServiceId)).ToHashSet();
        if (originalCarriers.Count != purchasing.UnresolvedCarriers.Count ||
            !services.Keys.ToHashSet().SetEquals(originalCarriers))
            throw new ArgumentException(
                "Built service evidence must cover exactly the original VS52 service provenance.",
                nameof(network));

        var offeredIds = new HashSet<
            (EconomicPointId Source, EconomicPointId Destination, ulong ServiceId)>();
        foreach (var lane in offeredLanes)
        {
            if (lane is null)
                throw new ArgumentException("Null offered lane.", nameof(offeredLanes));
            foreach (var option in lane.Services)
            {
                var key = (lane.Supply.EconomicPointId,
                    lane.Demand.EconomicPointId, option.ServiceId);
                if (!offeredIds.Add(key) ||
                    !services.TryGetValue(key, out var evidence) ||
                    !ReferenceEquals(evidence.DeclaredService, option))
                    throw new ArgumentException(
                        "All offered services must be the exact physical options in the attested witness.",
                        nameof(offeredLanes));
            }
        }
        if (!offeredIds.SetEquals(originalCarriers))
            throw new ArgumentException(
                "Offered lanes and built witness must have exact service coverage.",
                nameof(offeredLanes));

        var rightsByService = new Dictionary<
            (EconomicPointId Source, EconomicPointId Destination, ulong ServiceId),
            ExperimentalTransportServiceEntitlement>();
        foreach (var right in registry.Entitlements)
        {
            if (right is null ||
                !rightsByService.TryAdd((right.Source, right.Destination,
                    right.ServiceId), right))
                throw new ArgumentException(
                    "An institutional service identity must be unique.",
                    nameof(registry));
        }
        if (!rightsByService.Keys.ToHashSet().SetEquals(originalCarriers))
            throw new ArgumentException(
                "Institutional operators must match exactly the built services.",
                nameof(registry));

        var grants = new Dictionary<(ulong AssetId, string Operator),
            ExperimentalBuiltFreightAssetGrant>();
        var owners = new Dictionary<ulong, ExperimentalTransportInstitution>();
        static void ValidateInstitution(ExperimentalTransportInstitution institution)
        {
            if (institution is null ||
                string.IsNullOrWhiteSpace(institution.InstitutionId) ||
                institution.Kind == ExperimentalTransportInstitutionKind.Unspecified ||
                !Enum.IsDefined(institution.Kind))
                throw new ArgumentException(
                    "Physical infrastructure rights need valid institutional identities.");
        }

        foreach (var grant in network.AssetGrants)
        {
            if (grant is null || grant.AssetId == 0 ||
                !assets.TryGetValue(grant.AssetId, out var asset) ||
                string.IsNullOrWhiteSpace(grant.GrantId) ||
                grant.ValidFromNetworkRevision == 0 ||
                grant.ValidThroughNetworkRevision < grant.ValidFromNetworkRevision ||
                grant.ValidFromNetworkRevision > network.NetworkRevision ||
                grant.ValidThroughNetworkRevision < network.NetworkRevision)
                throw new ArgumentException(
                    "Asset access grant must reference a constructed operational asset and include the current network revision.",
                    nameof(network));
            ValidateInstitution(grant.InfrastructureOwner);
            ValidateInstitution(grant.AuthorizedOperator);
            if (!grants.TryAdd((grant.AssetId,
                    grant.AuthorizedOperator.InstitutionId), grant))
                throw new ArgumentException(
                    "A physical asset and operator must have exactly one applicable grant.",
                    nameof(network));
            if (owners.TryGetValue(grant.AssetId, out var owner))
            {
                if (owner != grant.InfrastructureOwner)
                    throw new ArgumentException(
                        "A shared physical asset may not have contradictory institutional owners.",
                        nameof(network));
            }
            else owners.Add(grant.AssetId, grant.InfrastructureOwner);
        }

        // NO unexamined grants, owner/operator assumptions, skipped
        // crossings or extra services: fail closed before authority callbacks.
        var consumedGrants = new HashSet<(ulong AssetId, string Operator)>();
        var valid = new List<ExperimentalBuiltFreightServiceRight>();
        foreach (var (key, evidence) in services.OrderBy(x => x.Key.Source.Value)
                     .ThenBy(x => x.Key.Destination.Value)
                     .ThenBy(x => x.Key.ServiceId))
        {
            // VS42 independently rechecks continuity, built assets, fine
            // Goldberg crossings, active access and technology version.
            var rebuilt = ExperimentalSharedFreightOption.FromBuiltOffer(
                evidence.BuiltOffer, session, anchors,
                network.InstalledAssets, currentAccess);
            var declared = evidence.DeclaredService;
            var x = rebuilt.Pricing;
            var y = declared.Pricing;
            if (rebuilt.ServiceId != declared.ServiceId ||
                rebuilt.RouteKey != declared.RouteKey ||
                !rebuilt.Corridors.SequenceEqual(declared.Corridors) ||
                x.RulesetVersion != y.RulesetVersion ||
                x.GeographicBaseNumerator != y.GeographicBaseNumerator ||
                x.OverheadNumerator != y.OverheadNumerator ||
                x.OverheadSmoothingMicrograms != y.OverheadSmoothingMicrograms ||
                x.SaturationNumerator != y.SaturationNumerator ||
                x.SaturationScaleMicrograms != y.SaturationScaleMicrograms)
                throw new InvalidOperationException(
                    "Advertised VS42 freight differs from current validated built route or monetary rules.");

            var operatorInstitution = rightsByService[key].Operator;
            ValidateInstitution(operatorInstitution);
            var grantIds = new List<string>();
            foreach (var assetId in evidence.BuiltOffer.AssetPath)
            {
                var grantKey = (assetId, operatorInstitution.InstitutionId);
                if (!grants.TryGetValue(grantKey, out var grant) ||
                    grant.AuthorizedOperator != operatorInstitution)
                    throw new InvalidOperationException(
                        "An institution cannot exploit an asset without an explicit current grant.");
                consumedGrants.Add(grantKey);
                grantIds.Add(grant.GrantId);
            }
            valid.Add(new ExperimentalBuiltFreightServiceRight(
                key.Source, key.Destination, key.ServiceId,
                operatorInstitution,
                Array.AsReadOnly(evidence.BuiltOffer.AssetPath.ToArray()),
                Array.AsReadOnly(grantIds.ToArray())));
        }
        if (!consumedGrants.SetEquals(grants.Keys))
            throw new ArgumentException(
                "Access-grant inventory contains unused or unsupported authorizations.",
                nameof(network));

        // The host authenticates built-asset source, current access snapshot
        // and network revision. There is deliberately no accept-all default.
        if (!networkAuthority.AcceptReadOnlyBuiltNetwork(network, currentAccess))
            throw new InvalidOperationException(
                "Independent host authority rejected the built network witness.");

        // VS55 independently audits the rights registry and all charges.
        var institutional = new ExperimentalTransportInstitutionalFreightProjector()
            .Project(session, purchasing, flows, catalog, nominalQuotes,
                calibrationPolicy, physicalFreightNominalUnitId,
                offeredLanes, selectedDeliveries, congestionPolicy,
                registry, registryAuthority);
        if (session.CurrentHash != initialHash)
            throw new InvalidOperationException(
                "Physical world changed during institutional validation.");
        return new ExperimentalBuiltFreightRightsProjection(
            network.WitnessId, network.WitnessVersion, network.WorldStateHash,
            network.NetworkRevision, institutional,
            Array.AsReadOnly(valid.ToArray()), consumedGrants.Select(x => x.AssetId).Distinct().Count());
    }
}
