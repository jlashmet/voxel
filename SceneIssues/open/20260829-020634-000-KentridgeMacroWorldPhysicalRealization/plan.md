# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Keep the source-backed Mounting Force/Kentridge graph authoritative through shared WorldBuilder APIs; physically realize settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded road slopes, authoritative storage, substantial lake/ridge, explicit constrained routes, and two-stage presentation readiness.
- Exact run `33318399738` was focused-test/player green but closure-red: Moordell readable, Rossdam partial, Fairy/Orc roads present without readable authored buildings, and final network target missed the 60 s cutoff.
- Root cause discriminator identified vertical residency: static storage tests generate separate upper Y-regions for tall buildings, while ordinary residency previously demanded terrain/viewer Y only.
- Selected fix is implemented at source `1b548f44a5c9e8acf9880c9df9bcb1244932ecb8`: `RefreshPending` is isolated in a residency partial, and already-accepted X/Z columns additionally queue only feature-bearing Y-regions from semantic catalogue bounds. No horizontal interest radius, device budget, scheduler, or scene-specific preload policy changes.
- Focused ordinary-streaming regression is implemented; first CI attempt exposed only a test compile defect (`FeatureRule` type name), fixed by source `1b548f44...`. Exact-source retry run `33341240134` is queued and must not be replaced while queued/running.
- Reuse audit: shared physical intent/planner/physical catalogue/water catalogue contain semantic inputs and no Kentridge/Rossdam policy. Existing generic `a`/`b` blocked-water fixture independently proves planner rejection and semantic `GoAround` reuse without Kentridge adapters.

## Current hypothesis / discriminator
H1: feature-aware vertical residency publishes the missing upper settlement shells through ordinary `ShowcaseWorld.StepStreaming`. H2: if the focused regression is green but built evidence still lacks structures, renderer publication/framing is the next discriminator. Do not change rendering or evidence framing before H1 is proven.

## Remaining gates
Let exact run `33341240134` complete untouched; inspect its result. If green, quantify added feature Y-regions and run the unchanged 60 s exact built-player replay. Inspect full-resolution Rossdam/Fairy/Orc settlement evidence, lake readability, final network capture, and cost telemetry. Only acceptance-required framing/dwell changes may follow. Re-fetch/merge current master before final exact-SHA validation; after every checkbox is green, close `open -> closed`, set fixed metadata, merge current master again if needed, and non-force push the exact feature head to master.
