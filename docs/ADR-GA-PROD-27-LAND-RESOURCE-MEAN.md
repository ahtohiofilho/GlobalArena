# ADR - GA-PROD-27 - Non-Water Resource Potential Mean

Date: 2026-10-13
Status: Accepted subject to operator visual inspection

## Decision

Present a read-only arithmetic mean of the canonical general resource potential over non-water cells, keeping the all-cell mean. Water filtering must reuse the existing biome classification; no additional simulation or resource system is introduced.

## Evidence and limitations

GA-PROD-27 QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release, runtime GA_PROD_LAND_RESOURCES_READY_27 land=41 water=51, and 26/26 evidence hashes. Display correctness and legibility are operator-attested via -VisualAccepted. GPP unchanged.
