# Transport service → function-derived freight quote: first reference seam

**Status:** Experimental, opt-in integration seam. Not an authoritative persistent transport world model. Refer to [ADR-064 (Proposed)](../adr/ADR-064-technology-persistent-transport-and-continuous-endogenous-pricing.md) and [Issue #16](https://github.com/ahtohiofilho/GlobalArena/issues/16).

## Boundaries already enforced

| Layer | Reference input | Guarantee |
| --- | --- | --- |
| Geography | Existing `StrategicSurfaceGraph` / `StrategicTopology` | Physical asset references a real strategic edge; merely adjacent geography never creates transport |
| Technology definition | Explicit `TechnologyTypeId` + `TechnologyCatalogVersion` | Identity and catalogue version remain stable in the snapshot; no inferred modal conversion or default mode enum |
| Constructed asset | `EconomicTransportBuiltAsset` with unique ID, geographic edge, technology, operational state | Parallel assets can occupy the same geographic edge; only supplied, operational assets are usable |
| Transport service | `EconomicTransportServiceOffer` with unique ID, ordered asset itinerary, declared transfer boundaries, expected load, price function and availability | Asset path must be geographically contiguous and terminate at real economic point anchors; technology changes require declared handoffs |
| Turn quote | `EconomicTransportServiceQuoteResolver` | Each eligible offered service is evaluated once by its own continuous pricing function using its *pre-turn* expected load; cheapest comparable service is chosen per directional economic-point pair, ties by stable ServiceId |
| Commercial allocation | Existing `EconomicTurnLockedFreightMarketResolver` | Consumes selected `EconomicTurnFreightQuote` unchanged; the commodity's marginal value still changes within the turn |

A service price function prices **the entire explicitly offered itinerary**, including any declared transfer/handling costs per its own ruleset. **Neither the adapter nor the topology assigns freight rates**. Price is not an arbitrary constant based on `TechnologyTypeId`. The same selected quote remains constant throughout the current turn and can change next turn when its forecast/rules change. Realized parcel quantities must never retroactively reprice earlier freight.

A service path is **explicitly offered**, not inferred from a free walk across adjacent cells. This initial reference deliberately does **not** find multi-service paths, combine their prices, determine a general transfer market, simulate vehicles, enforce generalized carrying-capacity cliffs, or guarantee that the lowest unit freight price remains feasible for unlimited traffic. Such commitments require separate ruleset and service contracts. The best selected service quote is currently represented as one economic-route quote; the additional `EconomicTransportServiceSelection` sidecar carries `ServiceId`, ordered physical `AssetIds` and technology types/catalogue version. Do not discard it when later introducing a trade provenance schema. Commercial selection is not proof of physical delivery.

Closed geography, unavailable services and inactive built assets never produce a quoted route. An unbuilt edge and a known geographic edge with no installed asset do not magically become available. The adapter validates foreign/duplicate assets and services, all listed physical itineraries, route anchors and explicit handoff boundaries. It also filters services without a matching producer/destination commodity; a future ruleset can define more nuanced transportability, access permissions and multimodal handling requirements.

## Governance and remaining work

This is a **caller-provided immutable input snapshot**, not authoritative `WorldState` data. Technology catalogue population, physical construction, persistent asset state, service ownership, repair, replacement, geographic access rights and topology-state revisions are **not implemented here**. No canonical hash version or migration occurs. `TechnologyCatalogVersion` is the supplied identifier of the catalogue from which the technology identity is interpreted, not proof that catalogue definitions have been validated against a persistent ruleset.

The existing commercial allocator API supports one selected freight quote per directional economic-point pair, so multiple parallel services are **compared**, not simultaneously allocated as distinct competing carriers. This does **not** implement per-service capacity or a full multi-path water-filling solution. A later evolution must distinguish service and path identity in each trade if per-service congestion, allocation, scheduling or finite availability is introduced.

Tests should use the generated strategic topology, preserve deterministic tie-breaking and verify every selected quote/asset path, both across turns and within one market clearing call. More geographically grounded performance scenarios and formal acceptance of ADR-064's supersession scope are separate checkpoints. Legacy M4.4 and PRs #12–#14 live-congestion behaviors are unchanged.

## Fine-mesh geography without an obligatory strategic routing layer

The reference service resolver now offers a **second, opt-in geography path** that accepts `PhysicalTacticalTileId` anchors for economic points and `PhysicalTacticalEdgeId` qualifiers for constructed corridor assets. Each fine tile/edge carries its **Goldberg resolution**. The supplied `StrategicSurfaceGraph` is instantiated from that exact *fine topology*, not the coarse strategic economic-routing graph. Despite its historical type name, the generator's fine `StrategicTopology` is used **as fine geography**; the resolver does not require travel across any coarse strategic edge.

- A fine asset constructor requires `PhysicalTacticalEdgeId(fineParameters, fineEdgeId)`; the original strategic asset constructor remains available for the historical adapter. The resolver rejects mixed or mismatched geography levels even if raw edge ordinal values happen to coincide.
- Economic-point anchors are supplied as a **frozen snapshot** `IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId>`. They must all belong to the supplied fine Goldberg topology. The old `EconomyRuntimeState` point anchors remain untouched and are not silently interpreted as fine coordinates.
- The geographic edge access snapshot is **topology-local** within the resolution being validated. For an eventually persisted, cross-topology closure identifier, an explicit qualified fine-edge form or mapping will be required. No such state migration is performed here.
- Both paths use the **same** service itinerary, asset technology, catalogue-version, handoff, availability, quote-function and market resolver semantics. No duplicated market algorithm or predefined economic corridor has been introduced.
- Fine topology may be large, but one commercial alternative is still a **fully offered service itinerary**, not every fine cell in the world. The computational budget therefore includes discovery of services and quotes as well as the market-clearing algorithm. The oceanic and other unbuilt dynamic-routing cases remain future distinct service-generation work.

### Economic cycle boundary (timing deliberately deferred)

The service resolver processes the **completed geography/access/asset/market snapshot supplied to one economic cycle**; it does not recompute routes on each movement round or mutate the strategic world. A future scheduler may run multiple movement rounds and then atomically assemble a new economic input snapshot, perform route/service generation, freeze function-derived transport quotes and settle allocations. That cycle length and any voluntary presentation delay are **not configured or decided** by this reference contract.

Performance acceptance should prioritize deterministic economic fidelity and useful world complexity, measuring **total economic-cycle wall time**, peak stalls and the separately measured route discovery/quote formation/allocation phases. An occasional visible processing pause is not automatically a reason to discard valuable economic depth. A dedicated turn scheduler, delivery/transit state and UI treatment will be evaluated in their respective cycles.

ADR-064 is still **Proposed**. Persistent built assets, fine-map economic-point binding, travel-time simulation, direct oceanic pathfinding and a rule for how/when economic cycles advance remain open architecture tasks.
