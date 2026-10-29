# Global Arena — Milestone-Gated Marketing Roadmap

**Status:** Approved operational direction; no arbitrary week/month/hour commitments
**Date:** 2026-10-11

## Integration with functional MVP roadmap (revalidated 2026-10-29)

**The Neolithic phase is a starting scenario, not the definition of MVP readiness.**
GA-VS01/VS02/VS03 are engineering validation slices, not a public
demo or finished MVP. In VS03, people live/work on global physical
tactical tiles, production/consumption are previews, the strategic mesh
only aggregates and no "city founding" game action exists. Do not
market these prototypes as a completed economic simulation.

The **product sequence** and its **marketing gates** are:

| Product milestone | Evidence needed before marketing advances | Marketing gate | Assistant recommendation when triggered |
|---|---|---|---|
| Neolithic architecture established / current VS03 | Validated design of physical tactical workers, no independent strategic city; UI still being refined | **MKT-00 — ready for lightweight preparation; not completed** | "Now I would define positioning and the intended audience, examine comparable PC strategy games, reserve a consistent name/channel plan and create an evidence inventory. No paid promotion or claims that an MVP exists." |
| **First representative public-facing vertical slice** (before full MVP) | Readable visual integration with existing Godot UI, usable flat/local tactical presentation, clear tile selection, and repeatable **actual gameplay** captured from a build | **MKT-01** | "Now I would record an authentic short gameplay clip, select screenshots, write a simple WIP developer introduction and test reactions before committing to a publishing cadence." |
| Credible storefront presentation | Consistent real visuals, readable selection and gameplay explanation, truthful page copy/tags and current Steam/Valve asset requirements verified | **MKT-02** | "Now I would prepare Steamworks/Coming Soon material, request the owner's approval before account/payment/publication, and start measuring page interest if the page is made public." |
| **Real economic decisions** | Completed production/consumption/market/stock consequences shown in the game, not a preview counter | **MKT-03** | "Now I would demonstrate how a worker/transport/production decision changes the world, publish one focused explanation and contact a small set of relevant strategy/economy creators." |
| **Lean warfare playable** | Reproducible movement/conflict/territorial consequences with acceptable visuals | **MKT-04** | "Now I would show conflict in updated footage, update the storefront if live and test reactions with appropriate creators/communities." |
| **Complete offline MVP loop** | New game -> several real turns -> economy/logistics/AI and minimum combat -> save/reopen/continue; stable enough for external feedback | **MKT-05** | "Now I would invite selected external testers, capture experience/friction/bugs and prioritize fixes before wider disclosure." |
| **Stable public demo** | Representative, installable demo; on-boarding, reliability, and real player testing passed | **MKT-06** | "Now I would publish a demo campaign, arrange creator keys/access and evaluate the next eligible Steam festival using up-to-date requirements." |
| **Commercial release candidate** | Product QA, storefront assets, pricing/release planning, support capability and owner go/no-go | **MKT-07** | "Now I would coordinate the launch assets, creator/press outreach, release communications and post-launch measurement." |

### Mandatory reminder at engineering checkpoints

At the end of a meaningful engineering milestone, especially when the user
says **"vamos prosseguir"**, perform a brief **MKT gate check** against
accepted evidence from the **real game build**:

1. State the last evidenced product milestone and whether the next MKT gate
   is **blocked**, **ready**, **in execution**, or **closed**. Do not
   infer gate readiness merely from passing headless CI or screenshots
   of an isolated development harness.
2. **If ready**, explicitly tell the owner in the response:
   **"Agora eu faria estas ações de marketing: ..."** and recommend
   a small, prioritized batch with the required artifacts and metrics.
3. **If blocked**, identify the **specific missing gameplay/product
   evidence** and continue development; don't manufacture a marketing
   deadline or open unrelated infrastructure work.
4. **Before real external action** (public posts, contacting creators,
   paying Steam fees, publishing a store page or announcing a date),
   request explicit owner authorization for the concrete action.
5. Record the result in `docs/MARKETING_METRICS.md` only with actual
   evidence and observed numbers. A recommendation is not a completed
   campaign. Marketing must not consume GPP or inflate the 1,000 GPP
   product baseline without an approved change.

**Current gate check (2026-10-29):** MKT-00 can be **proposed for
lightweight planning now**. MKT-01 remains **blocked** by an
authentic, compelling and repeatable player-facing vertical slice:
VS03 proves tactical tile worker transfers, but the selection is
visually difficult and the original flat tactical/primary Godot UI
integration remains pending. MKT-02+ are **not unlocked**. No public
account, marketing publication, Steam fees or campaign execution is
claimed by this document.

## Channel strategy

Steam is the priority PC commercial destination and wishlist conversion hub; opening Steamworks administration can start early, while public Coming Soon must wait for honest, appealing *production-build* gameplay media and store readiness. YouTube is the initial persistent video channel; Reddit is a community research/discovery channel, never a spam channel. Specialized strategy/economy content creators are an essential outreach track. Discord only when recurring testers/community justify maintenance. Do not simultaneously operate many social feeds. Other storefronts and consoles are future distribution options, not initial marketing workstreams.

## Work-cycle protocol

Engineering checkpoint → evidence of a real player-facing capability → marketing readiness gate → dedicated marketing tasks → audit of deliverables and available metrics → resume engineering. No fixed publishing cadence, hourly commitment or forecast dates. Marketing gate closes on verifiable work performed, not on guaranteed wishlists, views or reviews. If the feature isn't credible enough to publish, document the blocker and continue engineering until readiness; do not manufacture footage.

## Gates (sequence, not duration)

| Gate | Engineering trigger | Marketing deliverables | Close evidence |
|---|---|---|---|
| MKT-00 | Binding production architecture and MVP direction | Comparable-game/Steam research; positioning; names and brand identity; channel naming; media and analytics setup | Research brief, messaging and owned account checklist |
| MKT-01 | First actual Godot globe and consistent production UI | Capture reproducible short footage and screenshots; truthful development introduction | Build/commit identity, raw captures, published links if compelling |
| MKT-02 | Navigable strategic map, correct selection and sufficient representative UI | Steamworks onboarding; Coming Soon assets (capsule, copy, tags, at least five real screenshots, appropriate trailer); store review/publish | Approved public store URL, asset register, first metrics checkpoint |
| MKT-03 | Economic decisions/flows visibly affect the product UI | Explain an infrastructure/production/trade decision through real gameplay; target small creator list; test messaging | Public posts/creator contact log and observed conversion/feedback |
| MKT-04 | Lean warfare integrated | Updated gameplay trailer/store page showing war/territory consequences; relevant communities/creators | Media build provenance and updated campaign report |
| MKT-05 | Complete playable turn loop, IA, save/load | Closed external tests; recruit relevant players; prepare community support only if needed | Feedback, bug/retention analysis and iteration backlog |
| MKT-06 | Stable representative public demo | Demo page, creator access, launch of demo campaign; choose Steam Next Fest only if eligible and demo genuinely ready | Store/demo readiness checklist and campaign findings |
| MKT-07 | Commercial candidate meets technical and usability QA | Launch kit, pricing research/decision, release video, press contact, launch support and metrics plan | Technical + commercial release go/no-go |

Steam gates reflect store requirements as checked at execution time; do not hardcode platform terms/costs/event dates without current verification. Publishing and paying fees require owner approval.

## Media inventory

Archive raw recording, edited derivative, source build hash/version, feature shown, capture timestamp, language, channel, claim review and link/UTM tag. A feature is marketing-ready only when repeatable in the production game. Steam store gameplay media must comply with current Valve requirements.

## Solo developer operating priorities

Prioritize meaningful screenshots and short gameplay clips from each significant milestone, public product page conversion, selected communities, creator relationships and later demo/festival. Avoid daily-post obligations and paid advertising at launch of the marketing program. First optional spending priorities: compelling capsule artwork and trailer editing; advertising only with measurable experiments, not as a substitute for product positioning.

## Metrics and feedback

Record page visitors, source/UTM, gross/net wishlists, wishlist additions/removals when available, media reach, visits, attributable conversions, creator replies, demo starts/completions and qualitative player feedback. Wishlists are not guaranteed sales and raw totals are not a promised algorithmic ranking lever. Metrics inform positioning, assets and next milestone; external targets cannot block engineering indefinitely.

## Truthful communication

Never call a visual mockup gameplay. Don't advertise features not in verified builds. Identify work-in-progress. No invented release dates or sales forecasts. Maintain screenshots suitable for press/store only after production-readiness gate.
