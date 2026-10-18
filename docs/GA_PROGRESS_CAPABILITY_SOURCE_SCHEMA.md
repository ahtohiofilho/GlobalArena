# GA Progress Capability Source Schema - QA candidate

Status: SCHEMA_QA_CANDIDATE_NOT_AUTHORITATIVE

## Purpose and source

This schema candidate extends the published GA Progress Live Model contract.
The canonical, published 377.80 / 1000 GPP (37.8%) accounting remains
unchanged. The existing live-model JSON is a reference, not an authoritative
capability database. This QA grants no additional GPP and approves no SCR.

## Provenance and gap inventory

All 11 domain budgets and existing earned totals are immutable input references.
Ten non-UI domains have no detailed capability records in the reference JSON.
The UI domain has six capability budget/factor records and a verified total of
31.50 earned GPP, but these records have no immutable accepted revision IDs or
individual evidence links in the candidate source. They are NOT promoted to
accepted capability facts by copying them into this schema.

Do not invent missing capability names, budgets, maturities, source evidence,
revision identity, or approved scope. A domain-only aggregate is a temporary
accounting reference, not a one-capability substitute or completion claim.

## Required authoritative capability record

Each future record requires durable capability ID, owning domain ID, positive
approved budget, maturity factor in {0,0.20,0.50,0.70,0.85,1.00}, immutable
acceptance evidence, accepted revision ID, and scope revision ID. Each domain
must reconcile the exact sum of its capability budgets and earned amounts.
A record without accepted evidence is pending, even if the aggregate GPP has
already been booked by a previous audited accounting close.

## Revision and approval

A future accepted revision requires immutable identity, predecessor,
independent audit, scoped FormalClose and atomic publication. Unapproved
candidates never override an official snapshot or claim new GPP. Changed V1
scope requires an independently approved Scope Change Record (SCR); adding
subtasks inside a domain redistributes its budget without automatically
raising the overall denominator. The official percentage is always computed
from earned GPP / the accepted scope GPP and may decrease after an approved
scope expansion or maturity downgrade. Rounding is one decimal, away from zero.

## Candidate safety and next gate

This JSON remains non-authoritative until the missing domain decomposition,
evidence linkage and revision lineage are independently audited and formally
accepted. Reject duplicate domain/capability IDs, unapproved evidence claims,
invalid maturity factors, budget/earned sum mismatches, unauthorized scope
changes and scenario percentages copied instead of recalculated.

Current gate: schema QA -> independent schema audit -> scoped publication;
next: reconstruct historical capability lines domain by domain, then prove the
engine against complete authoritative input. None of these steps credits GPP.
