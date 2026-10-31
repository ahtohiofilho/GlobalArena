using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// VS57 entry point: no external assets, grants, access or accept-all verifiers.
/// A session-issued proof is checked independently against live state before,
/// during and after the existing VS52-VS56 economic/physical verification.
/// </summary>
public sealed class CanonicalTransportFreightProjector
{
    private sealed class NetworkVerifier(CanonicalPhysicalTacticalSession session, CanonicalTransportProof proof)
        : IExperimentalBuiltTransportNetworkAuthority
    {
        public bool AcceptReadOnlyBuiltNetwork(ExperimentalBuiltTransportNetworkWitness witness,
            StrategicEdgeAccessSnapshot access)
        {
            session.TransportNetwork.Validate(proof);
            return ReferenceEquals(witness, proof.Network) && ReferenceEquals(access, proof.Access);
        }
    }

    private sealed class RegistryVerifier(CanonicalPhysicalTacticalSession session, CanonicalTransportProof proof)
        : IExperimentalTransportInstitutionalRegistryAuthority
    {
        public bool AcceptReadOnlyRegistry(ExperimentalTransportInstitutionalRegistry registry)
        {
            session.TransportNetwork.Validate(proof);
            return ReferenceEquals(registry, proof.Registry);
        }
    }

    public ExperimentalBuiltFreightRightsProjection Project(
        CanonicalPhysicalTacticalSession session,
        ExperimentalAbstractPurchasingProjection purchasing,
        EconomicProductionDemandResult flows, FoodCommodityCatalog catalog,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> nominalQuotes,
        ExperimentalAbstractPriceFreightCalibrationPolicy calibrationPolicy,
        string physicalFreightNominalUnitId,
        IEnumerable<ExperimentalSimultaneousFreightLane> offeredLanes,
        IEnumerable<ExperimentalAbstractPhysicalFreightDelivery> selectedDeliveries,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        CanonicalTransportProof proof,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> anchors)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(proof);
        session.TransportNetwork.Validate(proof);
        // Materialize once; hostile/lazy enumeration cannot substitute later inputs.
        var quotes = nominalQuotes.ToArray();
        var lanes = offeredLanes.ToArray();
        var deliveries = selectedDeliveries.ToArray();
        var fixedAnchors = anchors.ToDictionary(x => x.Key, x => x.Value);
        session.TransportNetwork.Validate(proof);
        var result = new ExperimentalBuiltFreightInstitutionalRightsProjector().Project(
            session, purchasing, flows, catalog, quotes, calibrationPolicy,
            physicalFreightNominalUnitId, lanes, deliveries, congestionPolicy,
            proof.Registry, new RegistryVerifier(session, proof), proof.Network,
            new NetworkVerifier(session, proof), fixedAnchors, proof.Access);
        session.TransportNetwork.Validate(proof);
        return result;
    }
}
