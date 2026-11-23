# GA-Next — R182 Civilization window (English-first)

## Product decision
English is the canonical language of the entire Global Arena UI. From R182 onward, **new UI controls, panels, tabs, statuses and notifications are written in English**. Existing strings are migrated gradually in normal functional work; no mass translation is implied and no separate localization framework is introduced.

## First civilization screen
- **C | CIVILIZATION** is a visible, direct action in the header, with `C` keyboard shortcut; `Y` remains City, `TAB` remains operational dock, `M` remains the planetary map.
- The civilization screen opens as an independent overlay over the map. When it opens, the city screen closes, and vice versa.
- `Overview` displays only authoritative civilization identity, current turn, world revision, current worker count and count of AI civilizations.
- `Economy` displays the actual civilization's treasury cash and cumulative infrastructure investment **only if initialized**. Otherwise, the UI explicitly reports unavailable data.
- Last-cycle sales, tax, toll and total cash credits come solely from `GetLastClosedTemporalEconomics(humanCivilization)`, the verified R171 viewer-scoped read model. No phantom income, GDP, population or arbitrary prices.
- Each displayed figure is refreshed from the current host when opening and after actual turns; the window has no engine- or world-state mutations.
- Only **Overview** and **Economy** are implemented in this increment. Policy, technology, diplomacy and other tabs may be added when their authoritative contracts are ready, not as placeholders.

## Boundaries
No changes to economic equations, global redistribution, transport, worldgen or city markers. The city's old Portuguese strings remain untouched by this incremental policy and can be migrated in a later functional change. Source code and developer-facing logs may preserve existing technical naming, while new player-facing strings are in English.

Godot real-scene and regression tests are mandatory. Windows/Linux are mandatory targets, macOS opt-in. Draft, stacked on R181, no merge.
