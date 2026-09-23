# Compass navigation and mono-audio accessibility roadmap

Status: implementation started by explicit user request on 2026-09-23. The accepted `1.13.3` Stable Beta runtime remains the reference build; compass work belongs to the same live HUD PR.

## Donor audit and first implementation slice

- [Vinarator/Compass-HUD](https://github.com/Vinarat0r/Compass-HUD), source version `1.1.2`, MIT: provides the 80-degree heading projection, cardinal/degree scale, EN/RU direction names, and optional extraction/transit/quest marker concepts. The initial Admiral slice adapts the heading projection and labels. It uses the existing HUD raid/player lifecycle and a cached camera, without the donor's recurring scene searches, directory-wide icon searches, or per-repaint style allocation.
- Immersive Compass (EN|RU) `1.0.0`, SPT `3.8.3`, MIT: the older donor demonstrates item-driven visibility and quest/extraction navigation. Its published source URL currently returns HTTP 410, so no code or assets from it have been incorporated.
- C1 in source: the compact heading strip runs without a physical item in the current test preview. Special-slot gating is not active and is deferred by user instruction. F12 settings control visibility, language, degrees, scale, opacity and top offset. The physical raid behavior still requires an in-game acceptance check.
- C2 source work in progress: eligible exfiltration points and transit points are read from the same raid controllers used by Dynamic Maps. The optional Dynamic Maps quest adapter reuses its active/incomplete quest qualification and map-position data; it performs the donor's one-time quest trigger/item capture after raid startup, then refreshes only the selected objectives. Missing or incompatible Dynamic Maps disables quest markers alone. This is not yet physical C2 acceptance.
- C3 source work in progress: death callbacks on already-registered players create a 15-second skull only when the local player caused the kill or the death happened within 25 m. Nearby corpses are read from `AllPlayersEverExisted` using the same `Corpse` field as Dynamic Maps and shown only within a configurable 25 m radius. Dynamic Maps floor bounds provide above/below status; clear 3 m vertical separation is the fallback. No scene-wide search or remote-death notification is added. Physical acceptance remains open.
- C4 audio integration is explicitly deferred: no sound information is inferred from hidden world entities in this test preview.

## Product intent

Add an optional, lightweight compass strip to Admiral Tactical HUD. The current basic test preview has no required physical item. Special-slot gating is a later feature. The strip may consume markers from Dynamic Maps and short-lived event direction from the raid.

The audio direction layer is an accessibility aid for players using mono speakers. It must integrate with the installed Accessibility Indicators mod instead of introducing a second sound-detection engine. It visualizes only information that the existing audio/accessibility path already exposes; it must not inspect hidden bot positions or become a radar.

## Experience contract

- Current test preview: show the thin heading strip with cardinal directions and optional degrees without a compass item.
- Later phase only: optional special-slot compass gating may hide the strip and markers without the item.
- Dynamic Maps is optional. When present, selected player markers can appear on the heading strip; when absent, the compass continues to work.
- Accessibility Indicators integration is deferred until after the basic compass test; later it will consume only already-qualified sound events.
- Sound sectors communicate direction, broad distance and event class, never exact world position or exact distance.
- Kill markers are short-lived and limited to player-caused or immediately nearby deaths (25 m by default). They never reveal arbitrary remote deaths elsewhere on the map.
- Every integration fails closed and leaves the base HUD usable when its donor mod is absent or incompatible.

## Proposed settings

- `Compass > Enabled`
- Special-slot compass requirement: deferred, not a setting in the basic test preview.
- `Compass > Show degrees`
- `Compass > Opacity`, scale and vertical offset
- `Compass > Show eligible extracts / Show transits / Show active quest objectives (Dynamic Maps)`
- `Compass Events > Player kill direction`
- `Compass Events > Kill marker lifetime` (default `15 s`)
- `Compass Events > Nearby bodies` and `Body detection radius` (default `25 m`)
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

Add short-lived directional markers for player-caused kills and deaths in immediate proximity, starting with a red skull and a default lifetime of 15 seconds. Show nearby bodies only inside the configured 25 m radius; do not reveal remote deaths.

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
