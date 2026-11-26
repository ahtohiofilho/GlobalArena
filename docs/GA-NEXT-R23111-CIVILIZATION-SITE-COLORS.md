# R231.11 — Civilization identity on tactical population markers

Base #682 657af60: 3 workflows / 4 checks passed; real M atlas toggle verified 599 black and 392 white reversible pixels. Windows review remains pending.

Reuse the existing client identity palette from #378: civilization 1 red (#ed5252), civilization 2 blue (#398bf2), civilization 3 green (#43ca63). Other IDs retain existing unassigned muted fallback. Marker fill shows the civilization of the displayed physical occupants, not sovereignty. Mixed displayed occupancy uses provisional neutral gray (#a3a3a3), irrespective of population or ordering. No change to marker diameters, polygon centroids, white outline, labels, click targets, terrain, or trade ink. No new visibility or intelligence data. This increment is tactical only; strategic population marker publication, mixed-site visual refinement, and a larger unique color palette remain future work.

Tests inspect actual visible red and blue founder Panels in the uniform-city profile, verify the pure mixed/RGB palette contract, and extend existing population marker checks to actual style colors and border. Existing real trade pixel gate, census, movement, hash/replay and all fixtures retained. No simulation changes. CI pending, draft no merge. Next: profiling and scale, visual refinement and economic AI.
