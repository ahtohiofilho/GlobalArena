using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed record CanonicalTransportShare(
    ExperimentalTransportInstitution Beneficiary, string Numerator, string Denominator);

// Primitive journal rows are replayed through the same checked commands as live play.
public sealed record CanonicalTransportCommand(
    int PhysicalActionCount, string PhysicalHash, ulong PreviousRevision,
    string Kind, ulong AssetId = 0, ulong Technology = 0, ulong Catalog = 0,
    ulong Edge = 0, bool Enabled = false,
    ExperimentalTransportInstitution? Institution = null,
    ExperimentalBuiltFreightAssetGrant? Grant = null,
    ulong Source = 0, ulong Destination = 0, ulong Service = 0,
    CanonicalTransportShare[]? Shares = null);

/// <summary>
/// Session-owned host command boundary, like canonical worker orders. A quote
/// cannot install infrastructure or supply a permissive authority callback.
/// Commands express game institutional decisions, not external legal proof.
/// </summary>
public sealed class CanonicalTransportNetwork
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly Dictionary<ulong, EconomicTransportBuiltAsset> _assets = new();
    private readonly Dictionary<ulong, ExperimentalTransportInstitution> _owners = new();
    private readonly Dictionary<(ulong, string), ExperimentalBuiltFreightAssetGrant> _grants = new();
    private readonly Dictionary<(ulong, ulong, ulong), ExperimentalTransportServiceEntitlement> _rights = new();
    private readonly HashSet<StrategicEdgeId> _closed = new();
    private readonly List<CanonicalTransportCommand> _journal = new();

    internal CanonicalTransportNetwork(CanonicalPhysicalTacticalSession session) => _session = session;
    public ulong Revision => (ulong)_journal.Count;
    public string StateDigest => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_journal))));
    public IReadOnlyList<EconomicTransportBuiltAsset> InstalledAssets =>
        Array.AsReadOnly(_assets.Values.OrderBy(x => x.AssetId).ToArray());
    public StrategicEdgeAccessSnapshot Access => new(_closed);
    internal CanonicalTransportCommand[] ExportCommands() => _journal.Select(Clone).ToArray();
    private static CanonicalTransportCommand Clone(CanonicalTransportCommand c) =>
        c with { Shares = c.Shares?.ToArray() };
    private CanonicalTransportCommand Command(string kind) => new(
        _session.Events.Count, _session.CurrentHash.HexDigest, Revision, kind);

    public void Construct(EconomicTransportBuiltAsset asset, ExperimentalTransportInstitution owner)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.FineGeographicEdgeId is not { } fine ||
            fine.FineGoldbergParameters != _session.FineWorld.StrategicTopology.Parameters)
            throw new ArgumentException("Construction requires this session's fine topology.");
        Apply(Command("construct") with { AssetId = asset.AssetId,
            Technology = asset.TechnologyTypeId, Catalog = asset.TechnologyCatalogVersion,
            Edge = asset.GeographicEdgeId.Value, Enabled = asset.IsOperational, Institution = owner });
    }

    public void SetOperational(ulong assetId, bool operational) =>
        Apply(Command("operational") with { AssetId = assetId, Enabled = operational });
    public void SetAccess(StrategicEdgeId edge, bool open) =>
        Apply(Command("access") with { Edge = edge.Value, Enabled = open });
    public void TransferOwnership(ulong assetId, ExperimentalTransportInstitution owner) =>
        Apply(Command("owner") with { AssetId = assetId, Institution = owner });
    public void IssueGrant(ExperimentalBuiltFreightAssetGrant grant) =>
        Apply(Command("grant") with { Grant = grant });
    public void RevokeGrant(ulong assetId, ExperimentalTransportInstitution transportOperator) =>
        Apply(Command("revoke") with { AssetId = assetId, Institution = transportOperator });
    public void SetEntitlement(ExperimentalTransportServiceEntitlement right)
    {
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(right.RevenueShares);
        Apply(Command("entitlement") with { Source = right.Source.Value,
            Destination = right.Destination.Value, Service = right.ServiceId,
            Institution = right.Operator, Shares = right.RevenueShares.Select(x =>
                new CanonicalTransportShare(x.Beneficiary,
                    x.Fraction.Numerator.ToString(CultureInfo.InvariantCulture),
                    x.Fraction.Denominator.ToString(CultureInfo.InvariantCulture))).ToArray() });
    }

    private static void Institution(ExperimentalTransportInstitution? institution)
    {
        if (institution is null || string.IsNullOrWhiteSpace(institution.InstitutionId) ||
            institution.Kind == ExperimentalTransportInstitutionKind.Unspecified ||
            !Enum.IsDefined(institution.Kind))
            throw new ArgumentException("An explicit valid institution is required.");
    }

    internal void Apply(CanonicalTransportCommand input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var c = Clone(input);
        if (c.PhysicalActionCount != _session.Events.Count ||
            c.PhysicalHash != _session.CurrentHash.HexDigest || c.PreviousRevision != Revision)
            throw new InvalidOperationException("Stale or reordered transport command.");
        if (_journal.Count >= CanonicalPhysicalTacticalSession.MaximumActions)
            throw new InvalidOperationException("Transport journal is full.");
        switch (c.Kind)
        {
            case "construct":
                Institution(c.Institution);
                if (_assets.ContainsKey(c.AssetId))
                    throw new InvalidOperationException("An asset cannot be installed twice.");
                var asset = new EconomicTransportBuiltAsset(c.AssetId, c.Technology,
                    new PhysicalTacticalEdgeId(_session.FineWorld.StrategicTopology.Parameters,
                        new StrategicEdgeId(c.Edge)), c.Catalog, c.Enabled);
                ValidateLand(asset.GeographicEdgeId);
                _assets.Add(c.AssetId, asset);
                _owners.Add(c.AssetId, c.Institution!);
                break;
            case "operational":
                var old = _assets[c.AssetId];
                _assets[c.AssetId] = new EconomicTransportBuiltAsset(old.AssetId,
                    old.TechnologyTypeId, old.FineGeographicEdgeId!.Value,
                    old.TechnologyCatalogVersion, c.Enabled);
                break;
            case "access":
                var edge = new StrategicEdgeId(c.Edge);
                ValidateLand(edge);
                if (c.Enabled) _closed.Remove(edge); else _closed.Add(edge);
                break;
            case "owner":
                Institution(c.Institution);
                if (!_assets.ContainsKey(c.AssetId)) throw new ArgumentException("Unknown asset.");
                _owners[c.AssetId] = c.Institution!;
                foreach (var key in _grants.Keys.Where(x => x.Item1 == c.AssetId).ToArray())
                    _grants.Remove(key);
                break;
            case "grant":
                var g = c.Grant ?? throw new ArgumentException("Missing grant.");
                Institution(g.InfrastructureOwner);
                Institution(g.AuthorizedOperator);
                if (!_owners.TryGetValue(g.AssetId, out var owner) || owner != g.InfrastructureOwner ||
                    string.IsNullOrWhiteSpace(g.GrantId) || g.ValidFromNetworkRevision == 0 ||
                    g.ValidThroughNetworkRevision < g.ValidFromNetworkRevision ||
                    g.ValidThroughNetworkRevision < checked(Revision + 1) ||
                    _grants.Values.Any(x => x.GrantId == g.GrantId) ||
                    _grants.ContainsKey((g.AssetId, g.AuthorizedOperator.InstitutionId)))
                    throw new ArgumentException("Invalid, duplicate or unowned grant.");
                _grants.Add((g.AssetId, g.AuthorizedOperator.InstitutionId), g);
                break;
            case "revoke":
                Institution(c.Institution);
                if (!_grants.Remove((c.AssetId, c.Institution!.InstitutionId)))
                    throw new ArgumentException("Unknown grant.");
                break;
            case "entitlement":
                Institution(c.Institution);
                if (c.Source == 0 || c.Destination == 0 || c.Source == c.Destination ||
                    c.Service == 0 || c.Shares is null || c.Shares.Length == 0)
                    throw new ArgumentException("Invalid service entitlement.");
                var shares = c.Shares.Select(x =>
                {
                    Institution(x.Beneficiary);
                    return new ExperimentalTransportFreightRevenueShare(x.Beneficiary,
                        new FoodTransportCostFraction(
                            BigInteger.Parse(x.Numerator, CultureInfo.InvariantCulture),
                            BigInteger.Parse(x.Denominator, CultureInfo.InvariantCulture)));
                }).ToArray();
                var sum = new FoodTransportCostFraction(0, 1);
                foreach (var share in shares)
                    sum = new FoodTransportCostFraction(
                        sum.Numerator * share.Fraction.Denominator + share.Fraction.Numerator * sum.Denominator,
                        sum.Denominator * share.Fraction.Denominator);
                if (shares.Select(x => x.Beneficiary.InstitutionId).Distinct().Count() != shares.Length ||
                    sum.CompareTo(new FoodTransportCostFraction(1, 1)) != 0)
                    throw new ArgumentException("Revenue shares must uniquely conserve the whole freight.");
                _rights[(c.Source, c.Destination, c.Service)] = new(
                    new EconomicPointId(c.Source), new EconomicPointId(c.Destination), c.Service,
                    c.Institution!, Array.AsReadOnly(shares));
                break;
            default: throw new ArgumentException("Unsupported transport command.");
        }
        _journal.Add(c);
    }

    private void ValidateLand(StrategicEdgeId id)
    {
        var topology = _session.FineWorld.StrategicTopology;
        if (_session.CanonicalTerrain is null || !id.IsValid || id.Value > (ulong)topology.Edges.Count)
            throw new ArgumentException("Unknown canonical land edge.");
        var edge = topology.Edges[checked((int)id.Value - 1)];
        if (edge.IncidentCellIds.Any(x => !_session.IsPhysicalWorkableLand(x)))
            throw new InvalidOperationException("Construction/access requires physical workable land.");
    }

    public CanonicalTransportProof Capture(
        IEnumerable<ExperimentalBuiltFreightServiceEvidence> services,
        string purchasingPolicyId, ulong purchasingPolicyVersion,
        string calibrationId, ulong calibrationVersion, string nominalUnit)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (Revision == 0) throw new InvalidOperationException("No authoritative network exists.");
        var rows = services.ToArray();
        var rights = rows.Select(x => _rights[(x.BuiltOffer.RouteKey.SourceEconomicPointId.Value,
            x.BuiltOffer.RouteKey.DestinationEconomicPointId.Value, x.BuiltOffer.ServiceId)]).ToArray();
        var assets = rows.SelectMany(x => x.BuiltOffer.AssetPath).Distinct().Select(x => _assets[x]).ToArray();
        var grants = rows.SelectMany((x, i) => x.BuiltOffer.AssetPath.Select(id =>
            _grants[(id, rights[i].Operator.InstitutionId)])).Distinct().ToArray();
        var hash = _session.CurrentHash;
        var witness = new ExperimentalBuiltTransportNetworkWitness("SESSION-NETWORK", 1,
            hash, Revision, assets, rows, grants);
        var registry = new ExperimentalTransportInstitutionalRegistry("SESSION-RIGHTS", 1,
            hash, Revision, purchasingPolicyId, purchasingPolicyVersion, calibrationId,
            calibrationVersion, nominalUnit, rights);
        return new CanonicalTransportProof(_session, StateDigest, witness, registry, Access);
    }

    internal void Validate(CanonicalTransportProof proof)
    {
        if (!ReferenceEquals(proof.Session, _session) || proof.Network.NetworkRevision != Revision ||
            proof.Network.WorldStateHash != _session.CurrentHash || proof.NetworkDigest != StateDigest)
            throw new InvalidOperationException("Transport proof is foreign, expired or stale.");
        // Independent comparison against live committed records, not caller assertions.
        foreach (var asset in proof.Network.InstalledAssets)
            if (!_assets.TryGetValue(asset.AssetId, out var live) || !ReferenceEquals(live, asset) || !live.IsOperational)
                throw new InvalidOperationException("Asset is not the live operational asset.");
        foreach (var grant in proof.Network.AssetGrants)
            if (!_grants.TryGetValue((grant.AssetId, grant.AuthorizedOperator.InstitutionId), out var live) ||
                grant != live || _owners[grant.AssetId] != grant.InfrastructureOwner ||
                Revision < grant.ValidFromNetworkRevision || Revision > grant.ValidThroughNetworkRevision)
                throw new InvalidOperationException("Grant is not current session authority.");
        if (!Access.ClosedEdgeIds.SequenceEqual(proof.Access.ClosedEdgeIds))
            throw new InvalidOperationException("Access policy changed.");
        foreach (var right in proof.Registry.Entitlements)
        {
            var live = _rights[(right.Source.Value, right.Destination.Value, right.ServiceId)];
            if (right.Operator != live.Operator || !right.RevenueShares.SequenceEqual(live.RevenueShares))
                throw new InvalidOperationException("Institutional entitlement changed.");
        }
    }
}

/// <summary>Only the owning session can issue a proof; even an identical restored session must reissue it.</summary>
public sealed class CanonicalTransportProof
{
    internal CanonicalPhysicalTacticalSession Session { get; }
    public string NetworkDigest { get; }
    public ExperimentalBuiltTransportNetworkWitness Network { get; }
    public ExperimentalTransportInstitutionalRegistry Registry { get; }
    public StrategicEdgeAccessSnapshot Access { get; }
    internal CanonicalTransportProof(CanonicalPhysicalTacticalSession session, string digest,
        ExperimentalBuiltTransportNetworkWitness network,
        ExperimentalTransportInstitutionalRegistry registry, StrategicEdgeAccessSnapshot access)
    { Session = session; NetworkDigest = digest; Network = network; Registry = registry; Access = access; }
    public bool CanAuthorizeRealSettlement => false;
}
