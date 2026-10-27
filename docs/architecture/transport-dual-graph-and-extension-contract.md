# Transport topology and extensibility boundary — audit / proposed contract

**Status: Proposed architecture clarification, not an Accepted ADR or a persistent transport model.**
**Scope:** Correctly separate geometric borders, graph transitions, future
physical alignments, and authorized commercial operations. This document
records verified code behavior and proposed extension requirements after
reviewing the fine Goldberg transport reference. ADR-064 remains Proposed;
Issue #16 remains open.

## Finding: two different meanings of "edge"

The generated Goldberg **primal** topology includes:
- `StrategicCell`: a polygonal geographical tile.
- `StrategicEdge`: the **geometric common border of two distinct cells**;
  `IncidentVertexIds` are the two geometric ends of that border;
  `IncidentCellIds` are the two polygons on either side.
- `StrategicVertex`: a junction of geometric borders.

The implementation of `StrategicSurfaceGraph` builds a different,
**dual adjacency graph**: its **nodes are cells**, and its abstract
connection across the border joins the **two incident cells**. Its
`GetNeighborIndexes` refers to neighboring tiles, NOT traversal from
border-vertex to border-vertex.

The current experimental built-asset/reference transport system takes a
`PhysicalTacticalEdgeId` (the **qualified geometric border ID**) as the
key for an abstract **dual-graph crossing**. In
`EconomicFineTransportServiceItineraryDiscovery`, the two incident
**cells** become the source/target of the available built-asset link.
In `EconomicTransportServiceQuoteResolver`, an itinerary advances from
one incident **cell** to the other; it never advances between the
geometric edge's two vertices. It is thus **not routing along the
geometric border**.

**Important qualification:** `EconomicTransportBuiltAsset.GeographicEdgeId`
stores only a border/crossing reference, technology identity and
operational flag. It does **not** encode an explicit real-world rail/road
polyline, point of entry into either tile, within-tile curve, intersection,
end-of-line, station, cargo depot, bridge, port or transshipment capacity.
Its name and earlier phrases such as "built on a geographic edge" can be
misread as locating a line *along* the border. The correct contract is
"one eligible crossing/adjacency between its two incident tiles".
The **geometric crossing position** within the shared boundary is not
specified, and travel need not be exactly through tile centroids.

Therefore:
- A route **crosses** tile borders while traversing the dual graph.
- A proposed river or ridge **along** a tile border concerns the primal
  geometric boundary and is a **different spatial feature**.
- A shared geometric edge ID does **not** assert the transport line follows
  that border; equally it does **not** assert a bridge, ferry, station or
  cargo access is physically realizable there.
- Many in-tile approaches can eventually converge at a **single terminal
  or junction**. A terminal must not be duplicated once per crossing
  merely because the source and destination tiles have several edges.
- A vehicle passing through a tile or close to an island does **not**
  automatically create a commercial embarkation, discharge or handling
  opportunity.

## Hydrology distinction: existing strategic drainage is not detailed rivers

The **Accepted ADR-026** and
`StrategicHydrologyFieldGenerator` currently calculate a coarse strategic
**downstream CELL reference** to an adjacent lower-elevation cell with
deterministic flow accumulation. The derived fields **do not yet encode a
fine-grid river polyline or put the physical river course along shared
geometric borders**. The architectural intention to position detailed
rivers along geometric Goldberg edges will require an explicit derived
mapping/refinement from the strategic drainage field, not silently
equating the existing cell-to-cell drainage index with a river geometry.
Likewise, a transport crossing keyed to a border where a river is later
placed is **not** by itself authorization of a bridge, ford or port.

This is a remaining **design integration requirement**, not evidence
that today's economic service path is a riverbank route. Hydrology and
commercial transport currently use related topology but different
semantics and have no authoritative physical crossing compatibility
contract between them.

## Future universal transport contract — capabilities, not hardcoded modal enum

The general-purpose economic market consumes explicit
`EconomicTransportServiceOffer` and whole-service freight
`IEconomicTurnFreightCostFunction` with deterministic pre-cycle
forecasts; it does not branch on a canonical list of road, rail, ship or
air modalities. `TechnologyTypeId` plus
`TechnologyCatalogVersion` identifies technologies, but it is **not**
yet a populated persistent catalogue or a complete transport-plugin
contract.

A future versioned **transport technology definition** should reference
capabilities and providers, separating:

1. **Traversal domain:** whether the technology uses constructed
   tile-to-tile crossings, an installed continuous physical alignment,
   a navigable area/volume, a watercourse, or another explicitly
   implemented geometry/routing provider. No list of named modalities is
   necessary to establish these contracts; a new provider is needed
   only for a genuinely new traversal behavior.
2. **Physical asset and alignment:** stable installed asset ID,
   technology/version, actual physical construction geometry if
   required, operating state, and the currently traversable
   connections. Border crossing IDs can be used as **indices** for
   physical corridors without dictating the shape of the infrastructure.
3. **Commercial operations:** explicit embark/disembark,
   loading/unloading and transfer permissions at **facility/service
   nodes** with a physical location and compatible technology/service
   identities. A single terminal can connect multiple incoming
   approaches. Traversing a tile, river reach or sea area supplies
   **no implicit facility** or commercial stop.
4. **Service and itinerary:** who offers the service, its directional
   endpoints, permitted operating stops/handoffs and explicit composed
   physical path/segments. Transshipment between technologies requires
   a verified compatible operation; it cannot be inferred from
   geometric proximity or a shared tile.
5. **Economic pricing:** the caller supplies the applicable versioned
   whole-itinerary mathematical cost function using the pre-cycle
   forecast, and the market locks the resulting unit freight price
   through the economic close. No mandatory per-edge tariff, asset
   capacity cliff or auto-converted technology is introduced.
6. **Deterministic extension validation:** capability/provider versions,
   units, input topology and geographic level, declared facilities,
   quote provenance and snapshot identity must be validated before a
   service is eligible. The canonical state format is not changed by
   these architectural proposals.

A DTO/catalogue entry alone can register a new *technology using
already implemented capabilities*. If a new technology requires an
unimplemented physical traversal or service-operations behavior, a new
provider and explicit tests are required; the market and commodity
allocator should not need modality-specific branching.

**The new logical node/link interface is non-authoritative and opt-in.**
No canonical modal enum, persistent catalogue or WorldState schema is
ratified by this document; facility operations and traversal-provider
registration require later review and dedicated cross-OS validation.

## Implemented opt-in logical line connectivity (after the audit)

The non-authoritative experimental seam now includes
`EconomicTransportLogicalNodeId`, `EconomicTransportLogicalLink`,
`EconomicLogicalTransportServiceIntent`,
`IEconomicLogicalTransportTraversalProvider`,
`EconomicConstructedLogicalLineProvider`,
`IEconomicLogicalTransportItineraryPricing`, and
`EconomicLogicalTransportQuoteResolver`.

Here, a *line link* has **two endpoint node identities**. Two links
`A–B` and `B–C` can be traversed as a single `A→B→C` itinerary
precisely because they meet at **the same B node**, with no artificial
boundary endpoint or loading activity at B. Many approaches may share
one node; node location is a **logical connectivity identity**, not a
mandatory station, tile centroid or freight facility.

The constructed-line provider currently searches an explicitly supplied
set of operational links of matching technology type **and catalogue
version**, selecting a deterministic minimum-link itinerary with
lexicographic link-ID ties. A separate registered provider can implement
a different traversal domain and supply an equally explicit list of
logical nodes/links to the same quote adapter. No built Goldberg-border
key or fixed Road/Rail/Ship enum is required by the new API. The quote
adapter validates continuity, distinct nodes and links, the technology,
the declared destination, and a positive provider identity/version;
the traversal provider remains responsible for actual domain-specific
availability. **Untrusted providers are not a bypass for infrastructure
authorization:** a real integration must register and verify their
world-snapshot and physical-domain capabilities.

A returned node path is **transit only**. It cannot infer economic
supply/demand rights, intermediate passenger/cargo stops, unloading,
technology change, facility presence or transshipment. Explicit
commercial intents authorize only their declared economic source and
destination. A truly operational cargo loading/unloading provider and
versioned facility registry remain a separate future stage.

The pricing provider supplies one mathematical **whole-itinerary**
cost function for each reachable offered service, evaluated once at the
pre-economic-cycle expected load. The quote adapter produces an
`EconomicTurnFreightQuote` directly usable by the existing market;
the market's dynamic commodity marginal valuation is unchanged.
This minimum-link reference **does not** guarantee minimum total
commercial freight when the whole-route function is nonadditive.
There is no implicit tariff, multimodal switch or capacity model.

This is an **opt-in contract and executable reference**, not a
migration of `EconomicFineTransportServiceItineraryDiscovery`,
a universal maritime/air routing implementation, a persistent catalogue
DTO, or a ratification of ADR-064. The earlier built **dual-grid
crossing** resolver remains independent and supported. As next steps,
implement adapters with verified installed assets, a capability
registry and commercial operations at separately authorized facilities
before making any of these contracts authoritative.

## Implemented opt-in versioned provider registry and authorized snapshots

The new experimental registry contract in
`EconomicLogicalTransportProviderRegistry.cs` adds an explicit, versioned
**technology-to-capability binding**:

- `EconomicLogicalTransportTechnologyDefinition` (catalogue-like DTO):
  `TechnologyTypeId`, `TechnologyCatalogVersion`, `ProviderId`,
  `ProviderVersion`. No fixed list or enum of modal names. Multiple
  technologies may reference one provider; a new traversal behavior requires
  its own implementation of `IEconomicLogicalTransportTraversalProvider`.
- `EconomicLogicalTransportProviderRegistry`: injects and validates positive,
  unique technology and provider identities. Missing capability/version or
  changed provider identity is **rejected**, not guessed from a technology ID.
- `EconomicLogicalTransportAuthorizationSnapshot`:
  explicit `SnapshotOrdinal`, economic-point-to-**logical-node**
  `EconomicLogicalTransportPointAnchor` records and provider-owned
  `EconomicLogicalTransportAuthorizedLink` attestations. Every returned
  physical link must match **provider ID/version, link ID, two endpoint nodes,
  technology ID/catalogue version and operational status**. A link absent or
  deactivated in the snapshot cannot be traversed merely because a registered
  provider happens to know it. An economic source or destination must also
  have a matching endpoint anchor. The snapshot copies registrations and
  references immutable link records so later caller collection mutations
  cannot silently rewrite established authorizations.
- `EconomicRegisteredLogicalTransportQuoteResolver`: strict opt-in
  commercial adapter. Resolves each **declared service** through its
  registered provider, checks the snapshot **before price-function creation**,
  and then uses the independently existing logical continuity validator.
  Each eligible service receives one fresh complete-itinerary
  forecast-derived quote. The cheapest declared service for the same
  economic-point pair is selected using exact rational cost, then ServiceId
  for ties. The result records immutable `AuthorizationSnapshotOrdinal`
  beside the existing logical selection and frozen
  `EconomicTurnFreightQuote`. The commodity allocator remains unchanged.

For example a declarative technology **71/v4** can bind to provider
**9/v2** without needing a `Road`, `Rail` or `Maritime` enum. Another
technology can bind to the same provider when its physical capabilities
are compatible; an entirely different geometry can be supplied through
another explicitly registered provider. Adding a DTO row does NOT
magically implement traversal rules, tariffs or facility operations.

**Security/trust and exact scope:** this is *snapshot authorization by
explicit caller-supplied attestation*, not authoritative proof of physical
construction. A future trusted post-movement world-state adapter must
generate the authorized links based on built infrastructure, geographic
access, technology conditions, or navigability. The provider is injected
by a trusted caller; this registry does not load arbitrary plugins or
execute untrusted code. Route validation checks that returned links are
part of the **authorized snapshot**, but does not independently prove
whether the snapshot represents physically lawful construction. No
longitudinal road geometry or mandatory Goldberg edge key is imposed.
No load/discharge or transfer facility is invented at a shared node,
and anchored endpoints do not themselves imply installed terminals.

The prior single-provider `EconomicLogicalTransportQuoteResolver` and
fine Goldberg route references remain available as independent
non-authoritative experiments. Use the *registered* variant whenever
explicit snapshot authorization is required; the legacy reference
API should not be mistaken for a world-authorized transport service.
Nothing is migrated into `WorldState`, and ADR-064 remains Proposed.

## Opt-in explicit cargo operations: loading, unloading, handoffs

The reference platform now separates commercial handling from **logical
line continuity** and from **physical link authorization**. A logical
node may connect A–B, B–C, B–D and many more links: their continuity
is unaffected by whether any cargo facility exists there. Transit
never generates a commercial stop.

The non-authoritative `EconomicLogicalCargoOperations.cs` contracts are:

- `EconomicLogicalCargoFacility`: uniquely identified logical-node
  location, associated economic point and operational state. **One
  facility may connect many incoming links**; neither every logical
  node nor every Goldberg boundary is an automatic facility.
- `EconomicLogicalCargoPermission`: explicit **Load** or **Unload**
  permission for one facility and technology type/catalogue version.
  The operation enum is *not* a list of transport modes. The two
  directions do not imply each other; permissions for one technology
  do not silently grant another version or provider's service rights.
- `EconomicLogicalCargoTransferPermission`: directed permission
  between **distinct technology identities/versions** at a single
  facility. `RequireTransfer` also demands explicit outgoing Load and
  incoming Unload permissions at that same operational facility.
  This validation is **not** an automatic multimodal itinerary planner
  and does not invent transfer prices.
- `EconomicLogicalCargoOperationsSnapshot`: caller-attested immutable
  facility/grant membership with a snapshot ordinal. It deterministically
  finds the lowest-ID matching operational facility, rejecting absent,
  incompatible or offline permissions. A node plus a point anchor
  alone grants **no** cargo right.
- `EconomicOperationalLogicalTransportQuoteResolver`: opt-in strict
  entrypoint combining the already registered physical provider/link
  snapshot with the handling snapshot. Snapshot ordinals must match.
  **Every active declared service's origin Load and destination Unload
  are checked before ANY whole-itinerary freight price function is
  created**, so an unauthorized competing offer cannot be hidden by
  an earlier priced offer. The underlying registered physical resolver
  still checks link identities and endpoints and forms each eligible
  fresh ex-ante whole-itinerary quote once. The returned immutable
  selection identifies `LoadFacilityId`, `UnloadFacilityId`,
  `OperationsSnapshotOrdinal` and the existing physical selection
  and `EconomicTurnFreightQuote`.

This permits a constructed line A–B to join B–C at node B as one
itinerary, even if B has no cargo handling rights. It likewise
prevents an island near the route of a hypothetical vessel from
receiving freight only because it is traversed or adjacent:
a declared commercial endpoint must have a compatible operational
facility and explicitly authorized service. **The present model
does not implement maritime vessels or ports**; the example is
a statement about general authorization semantics.

**Design limits:** Facilities and permissions are *provided by
the caller* as a post-movement experiment, **not** derived from
persistent installed infrastructure in authoritative `WorldState`.
No real station/port geometry, cargo capacities, transfer cost,
cross-modal itinerary composition, facility construction,
commodity-specific handling rules or automatic snapshot producer
has been added. `EconomicLogicalTransportQuoteResolver` and
`EconomicRegisteredLogicalTransportQuoteResolver` remain available
as independent reference layers that do not require facility grants.
The new strict operational entrypoint must be explicitly selected
when cargo authorizations are intended to be enforced.
ADR-064 remains Proposed.

## Contract audit and invariants

| Question | Existing guarantee | Not yet guaranteed |
| --- | --- | --- |
| Does a service route run ALONG a geometric Goldberg edge? | **No.** It advances between the edge's two incident cells (dual graph). | Actual in-tile route centerline, exact border crossing point |
| Do rivers share the commercial path representation? | No; strategic drainage is a separate downstream-cell field. | Fine river-on-border geometry and bridge/crossing compatibility |
| Does one crossing imply a loading terminal or modal transfer? | No; only declared service endpoints and transfer-index boundaries exist. | Physically validated terminals/ports, operational loading and transfer eligibility |
| Can a new technology identity enter economic quotes? | Yes: arbitrary nonzero ID/version with caller-provided price function. | Persistent definitions, construction/traversal providers and universal lifecycle |
| Can one tile/junction connect multiple approaches? | Abstract dual adjacency supports multiple neighboring cells. | Built, located junction/facility with many directed service approaches |
| Can any technology travel without built tile-edge crossings? | Not through the current fine **built-crossing reference**. | Alternative traversal providers (navigation, open space, others) |

### Governance gate

Before treating the current
`EconomicFineTransportServiceItineraryDiscovery` as a universal
transport pathfinder, require explicit architectural review of the
traversal provider boundary. Before putting terminals or river crossings
into authoritative persistent state, separately specify their physical
identity, location, compatible operations, and validated interaction
with the generated fine geometry. Until then retain the working
built-crossing search as **one optional specialized provider**, and
leave the accepted hydrology baseline and economic settlement unchanged.

Do **not** choose canonical modalities by inference, define them in a
closed enum, ratify ADR-064, impose a movement-round/economic-cycle
cadence, or claim this document alone implements ports or real physical
construction.
