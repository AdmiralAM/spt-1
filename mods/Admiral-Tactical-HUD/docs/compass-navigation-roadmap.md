# Compass navigation and mono-audio accessibility roadmap

Status: design draft only. This document does not add work to the active `1.13.3` release gate and does not authorize implementation before the current Admiral Tactical HUD milestones are complete.

## Product intent

Add an optional, lightweight compass strip to Admiral Tactical HUD. Navigation features are available only while a physical in-game compass is installed in a special slot. The strip may consume markers from Dynamic Maps and short-lived event direction from the raid.

The audio direction layer is an accessibility aid for players using mono speakers. It must integrate with the installed Accessibility Indicators mod instead of introducing a second sound-detection engine. It visualizes only information that the existing audio/accessibility path already exposes; it must not inspect hidden bot positions or become a radar.

## Experience contract

- No compass item in a special slot: no compass scale and no navigation/map markers.
- Compass item installed: show a thin heading strip with cardinal directions and optional degrees.
- Dynamic Maps is optional. When present, selected player markers can appear on the heading strip; when absent, the compass continues to work.
- Accessibility Indicators is optional and independently configurable. When present, its already-qualified sound events may be represented as approximate directional sectors.
- Sound sectors communicate direction, broad distance and event class, never exact world position or exact distance.
- Kill markers are short-lived and limited to player-caused, observed or already-audible events. They never reveal arbitrary deaths elsewhere on the map.
- Every integration fails closed and leaves the base HUD usable when its donor mod is absent or incompatible.

## Proposed settings

- `Compass > Enabled`
- `Compass > Require compass in special slot` (default `true`)
- `Compass > Show degrees`
- `Compass > Opacity`, scale and vertical offset
- `Dynamic Maps > Marker bridge`
- `Dynamic Maps > Marker categories`
- `Events > Kill direction marker`
- `Events > Kill marker lifetime` (default `30 s`)
- `Accessibility > Audio direction sectors`
- `Accessibility > Gunshots / explosions / running / walking`
- `Accessibility > Sector opacity, persistence and angular uncertainty`
- `Accessibility > Mono preset`

## Milestones

### C0 — integration contracts and prototype

Confirm the stable runtime boundaries for:

- detecting the physical compass in the intended special slots;
- reading heading without recurring reflection or scene scans;
- consuming Dynamic Maps markers through an optional adapter;
- consuming Accessibility Indicators output/events without copying its detection engine;
- identifying observable/audible kill events without global death intelligence.

Acceptance: a disposable prototype proves each available boundary, absent dependencies fail closed, and the implementation plan names no mandatory third-party dependency.

Estimated effort: **3–5 hours**.

### C1 — compass strip foundation

Implement a cached, screen-safe compass strip with cardinal directions, optional degrees, item gating, raid/menu lifecycle, positioning and independent enable/disable behavior.

Acceptance: the strip appears and disappears with the compass contract, rotates correctly through heading wrap-around, survives raid/menu transitions and performs no hidden work while disabled.

Estimated effort: **4–7 hours**.

### C2 — Dynamic Maps marker bridge

Project selected Dynamic Maps markers onto the compass strip. Clamp off-screen markers to the strip edges, avoid exact distance by default, and handle marker add/update/remove without polling the complete map state every frame.

Acceptance: marker direction follows player rotation, removal is deterministic, duplicate/stale markers do not survive lifecycle changes, and Dynamic Maps remains optional.

Estimated effort: **5–9 hours**.

### C3 — bounded event markers

Add short-lived directional markers for qualifying kill events, starting with a red skull and a default lifetime of 30 seconds. Restrict eligibility to information the player legitimately generated, observed or heard.

Acceptance: no remote or hidden death disclosure, expiration is deterministic, repeated events are bounded, and the feed/compass remain readable during busy fights.

Estimated effort: **3–6 hours**.

### C4 — Accessibility Indicators bridge

Translate existing Accessibility Indicators information into subtle compass sectors for gunshots, explosions, running and walking. Preserve the donor mod as the authority for whether an event is perceptible. Encode broad distance through intensity/persistence and uncertainty through sector width; do not expose coordinates.

Acceptance: mono users receive useful left/right/front/back guidance, walls/suppression/distance are not bypassed beyond donor information, no second audio classifier is active, and missing/incompatible versions disable only the bridge.

Estimated effort: **6–10 hours**.

### C5 — UX, performance and runtime acceptance

Polish the Mono preset and individual controls, cap simultaneous indicators, reuse UI objects, cache immutable presentation state, add EN/RU text, document optional dependencies and run one batched physical raid test.

Acceptance: the display stays quiet outside relevant events, remains legible during combat, introduces no measurable frametime/stutter regression and produces clean logs with either, both or neither optional integration installed.

Estimated effort: **4–7 hours**, plus physical playtesting.

## Expected total

The realistic implementation range is **25–44 hours**, not 60–80 hours, because Accessibility Indicators remains responsible for sound-event qualification. If its integration surface is already stable and directly consumable, the likely result is near the lower half of the range.

## Explicit non-goals

- no independent sound engine;
- no bot-position lookup or threat radar;
- no exact range or through-wall target location;
- no global notification for every death on the map;
- no mandatory Dynamic Maps or Accessibility Indicators dependency;
- no expansion of the active `1.13.3` RC while its recorded stabilization gate remains open.
