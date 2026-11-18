# GA-Next — Canonical physical transport authority (2026-11-18)

## Decision

After PR #420, buyer demand and delivered quantity are resolved per physical
`(fine tile, commodity)`. The next gate removes a second source of economic
inconsistency: externally supplied road, capacity and ownership vectors must
NOT be treated as authoritative world facts.

The proposed v12 contract introduces an immutable and canonically hashed
`PhysicalTransportInfrastructureRuntimeState` as a field of `WorldState`.
This is a **one-time schema deployment**, not transport construction and not
an economic turn. Existing unseeded v8–v11 snapshots retain exactly their
prior hash format and contents; they explicitly cannot call
`CoordinateWorldCandidate` until complete transport has been supplied.

## What is stored per canonical fine crossing

- A real `PhysicalTacticalEdgeId` from the bound Goldberg incidence map;
- Positive free-flow cost per whole common cargo unit;
- Positive cargo at the declared reference capacity;
- Ownership status: `NoInfrastructure` (natural connectivity),
  `UnownedInfrastructure`, or `Owned`;
- An actual civilization owner if and only if the status is `Owned`;
- Explicit third-party tariff (zero in the absence of a specific owner).

The deployment must provide **ALL fine crossings exactly once** and be
accompanied by the original generated world's canonical signature and fine
incidence topology. No missing road is converted into zero-cost travel.
The engine does not invent geometric passability, environmental accessibility
or era-dependent cost: those initial physical parameters must be supplied by
an explicit world-seeding policy in a later gate. Natural unbuilt graph
connectivity is a first-class row, not an absent row.

Only the seed operation may introduce transport into v11. It is prohibited
on an initialized v12 snapshot. Construction, upgrade, capture/ownership
transfer, tariff updates and degradation require **future journaled turn
events** with matching world revisions; these APIs have deliberately not been
implemented.

## Economic and hashing invariants

- V12 hashes sort every crossing by canonical fine edge identity and include
  free cost, reference capacity, owner, kind and tariff. They do **not**
  hash a planning preview, candidate route, market delivery or unaccepted
  congestion quotation.
- The authoritative entry `CoordinateWorldCandidate` reads exclusively
  from the committed snapshot's transport records and actual v11 workers.
  The older externally quoted `CoordinateCandidate` remains for research
  fixtures, not for production world-bound economic settlement.
- Every commodity retains the same normalization
  `P(Q)=sqrt(D/Q)`, with D/Q aggregated by physical market rather than
  civilization. Neither infrastructure nor owner identity multiplies price.
- The physical cost and owner tariff are frozen throughout the current turn;
  capacity reference is used to compute **next-cycle** lagged congestion
  after the current candidate finishes. No same-turn congestion feedback.
- This gate does not mark any candidate `IsCommitted`, create transaction
  journals, publish prices, credit treasuries, or generate a real
  `FoundationSettledEconomicTradeCycleArtifact`. The next settlement gate
  remains blocked until infrastructure transition events and economic
  publication are audited.

## Acceptance and residual risks

The test gate covers stable v12 canonical hashing, unchanged v11 hashes,
completeness/duplicate rejection, foreign civilization rejection, natural
toll exclusion, explicit crossing costs, and the new world-only planning
entry on real worker data. The C# and kernel suites must pass on Windows
and Ubuntu, and Godot/StateHash regressions must stay green.

Open issues include: canonical authored world-seeding policy for natural
land/sea crossing type and cost, military capture-to-title transfer, who
can propose and approve tariff and infrastructure changes, persistence and
replay of those transitions, and performance with many thousands of fine
crossings. The current world deployment does NOT yet implement any of them.
