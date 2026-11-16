# GA-Next Visual Foundation 01 — provisional production-grade UI foundation

**16 November 2026 | Draft stacked on #362 | not a final art direction**

## Product decisions

- TACTICAL is the primary viewport; it fills the actual **landscape rectangle** available to the root Control. The previous AspectRatioContainer 1:1 is gone. The planetary globe can remain circular inside a separate window without forcing tactical square clipping.
- M opens a planetary-navigation window; TAB opens a contextual operations desk, ESC dismisses them. Neither is an OS-native popup; the same Godot Viewport stays active underneath.
- The visual identity is **data-forward command center**: dark layered navy surfaces, restrained teal highlights and warm secondary accent, structured headers, fine borders, 5–8 px radii and clear type hierarchy. No external paid art packs or font dependencies.
- Operations metrics are **read-only snapshots from the same FoundationHumanAiLocalPreview**: next turn, local human worker count and AI civilization count; refreshing after authoritative commits does not simulate anything.
- All interactive control states should use **GaVisualFoundation.BuildTheme()**, with common tokens/styles in one C# file. Shared window chrome and metric tiles are reusable components; later reskins change their styles centrally, not every Main section.
- Window frames deliberately use styled opaque surfaces and a distinctive heading/left accent rather than relying on an undecorated translucent overlay. Current UX is an iterative first design, not a final identity.

## Technical scope

- Main removes the tactical map's old 1:1 AspectRatioContainer, leaves mapRegion as a flex-expanded PanelContainer and keeps the presentation-only G(18,0) tactical renderer intact.
- The same scene, input callbacks, window navigation and E turn closure remain wired. The C# authoritative game state, workers and journal v9 are not modified.
- GaVisualFoundation centralizes palette, Theme for Button/Label/OptionButton/PanelContainer and chrome/metric components, without new external textures, licenses or asset fonts.
- The tactical area is **not yet a continuous moving-region stream**; it still has the existing initial resident buffer. Workers are **not yet physically located on fine tiles**; the preview anchor is still a strategic region, and G/P/X remain disabled in the normal tactical-first mode.
- Tooling: a single PR-specific PowerShell launcher must pin the verified HEAD, isolate a Git worktree, compile and **open** the right Godot scene automatically. If the required engine isn't discoverable, the launcher automatically provisions the official version to a private user directory (subject to download/network access). Never ask the player to locate or import the project by hand.

## Automated acceptance

- Real .NET10 Godot project compiles on Windows and Linux.
- Historical PR #361 Godot E2E remains operational (human+AI multi-intent in one v9 journal).
- Independent Godot 4.7.2 Mono E2E requires `GANextWideTacticalCanvas.Width > 1.18 * Height`, the tactical child also landscape, a live Theme, themed planet and operations frames, and M / TAB / ESC / E unchanged.
- Linux Xvfb / Mesa screenshot evidence: tactical entry, planet modal, operational window, after AI turn.
- Human Windows checkpoint remains a separate go/no-go. Contrast, window density, readable typography and broad-screen proportions must be inspected before declaring final UX.

## Deferred

- Physical-tile worker identity, authoritativeness of fine-graph movement, marker display and dynamic tactical region resident streaming are NOT solved here.
- Resizable/draggable/dockable windows, profile persistence, accessibility contrast alternatives, animation and art identity exploration remain for future increments.
- No automatic merge. Keep #361 and #362 intact; this PR is Draft and stacked directly on #362.

## Governance

The architecture of visual components is production-oriented, but its look stays replaceable. Product review determines the final typography, accent color and iconography; backend rule changes remain independently gated.
