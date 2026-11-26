# GA Visual 01 — Material and interaction foundation (independent design track)

Base: R230.6.4 #666 4036e57, already accepted provisionally by user. Parallel R231.1 #667 cartography remains untouched. Do not merge either PR automatically.

## Source direction and adaptation
User supplied external AI specification: attention hierarchy, five/six perceptible planes, directional light, contact/depth/ambient shadow, material continuity, composed gradients, motion hierarchy, progressive disclosure, microphysics, design tokens and independent effects. Preserve earlier requirements: subdued neutral graphite/gray UI, colored civilizations and world, operational map-led selection, no default selected object, four slots, fixed live Planet, detailed screens not dock, English UI, no new gameplay rules.

## First implementation
- Central reusable palette, radii, shadow/elevation and motion tokens in GaHudVisualTokens; native Godot styles stay compatible with prior assertions.
- Separate GaHudEffects: cached 128x128 RGBA composed lighting (upper-left key, weaker reflected lower-right light, vertical light, edge highlight, lower occlusion). Draw behind panel children, no input interception or full-screen rendering pass. Atmospheric canvas, structural group, functional object, recessed portrait, raised footer; interaction plane native button/focus/tooltip. Existing modal layer retained.
- Graphite material with distinct contact shadows and focused/hover/pressed/disabled controls. No new stats, no geometry or map selection change.
- GaHudMotion: separate hover/press/release/exit curves and disclosure fade; no delayed command, no layout scaling, no idle animation. Reduced motion via GA_NEXT_REDUCED_HUD_MOTION=1. Signal registration idempotent.
- CI retains all old gates and checks material attachments, cached directional texture, distinct native states and motion registration. Visual validation required; automated checks do not prove premium perception.

## Remaining work, not claimed done
Visual QA on Windows and measured frame-time/latency before further effects. Review lighting and material strength, rhythm, typography, portrait art, modal/tooltips, focus for inputs, multi-stage command feedback, real loading/success/error where applicable. Stagger for detail list reveal only if useful; parallax/volumetrics/blur/particles/noise not mandatory in every card: profile on Intel UHD compatibility and add only when perceived benefit warrants cost. Avoid distracting full-screen bloom, fake progress/status, color competition, artificial latency and layout jitter. The external design spec is an ambition and QA rubric, not a claim all effects have been implemented. Do not complete visual track until manual review of neutral/selected, expanded, hover, press, focus, grayscale and map readability.

CI pending. Draft, no merge. Resume cartography cycle separately.
