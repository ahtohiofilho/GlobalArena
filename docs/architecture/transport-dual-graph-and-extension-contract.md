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

**No new C# interface, enum, persistent catalogue or canonical schema is
declared authoritative by this document.** First ratify the capability
boundary, then implement a narrow opt-in provider/DTO with real
constructors, validation and cross-OS tests before migration into
`WorldState`.

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
