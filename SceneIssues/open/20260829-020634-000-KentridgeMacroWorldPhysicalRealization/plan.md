# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Keep the source-backed Mounting Force/Kentridge graph authoritative through shared WorldBuilder APIs; physically realize settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded road slopes, authoritative storage, substantial lake/ridge, explicit constrained routes, and two-stage presentation readiness.
- Exact run `33318399738` was focused-test/player green but closure-red: Moordell readable, Rossdam partial, Fairy/Orc roads present without readable authored buildings, and final network target missed the 60 s cutoff.
- Root cause discriminator identified vertical residency: static storage tests generate separate upper Y-regions for tall buildings, while ordinary residency previously demanded terrain/viewer Y only.
- Selected production fix is implemented: `RefreshPending` is isolated in a residency partial, and already-accepted X/Z columns additionally queue only feature-bearing Y-regions from semantic catalogue bounds. No horizontal interest radius, device budget, scheduler, or scene-specific preload policy changes.
- Reuse audit is complete: shared physical intent/planner/physical catalogue/water catalogue contain semantic inputs and no Kentridge/Rossdam policy. Existing generic `a`/`b` blocked-water fixture independently proves planner rejection and semantic `GoAround` reuse without Kentridge adapters.
- Exact retry run `33341240134` completed with the production code compiling; its focused vertical-residency regression failed before exercising residency because the test assumed the current Kentridge composition happened to contain an explicit placement already straddling a vertical region boundary. That is an incidental-content fixture assumption, not a production-residency failure.
- Regression repair at source `a9fbaef3a8cafd6646aae67b94ee6ae9d68e8a5c` keeps a real production definition/program/material set but deterministically repositions one nontrivially tall explicit instance to one voxel below its next Y-region boundary. This preserves real feature rasterization and makes the generic vertical-residency discriminator independent of current authored altitude.

## Current hypothesis / discriminator
H1: feature-aware vertical residency publishes a deterministically boundary-crossing production feature's upper shell through ordinary `ShowcaseWorld.StepStreaming`. H2: if the focused regression is green but built evidence still lacks structures, renderer publication/framing is the next discriminator. Do not change rendering or evidence framing before H1 is proven.

## Remaining gates
Run exact-SHA CI for the deterministic boundary fixture through `ci-test/fixes/agent-6`. If green, quantify added feature Y-regions and run/inspect the unchanged supported 60 s built-player replay. Inspect full-resolution Rossdam/Fairy/Orc settlement evidence, lake readability, final network capture, and cost telemetry. Only acceptance-required framing/dwell changes may follow. Re-fetch current master before final exact-SHA validation; after every checkbox is green, close `open -> closed`, set fixed metadata, merge current master, and non-force push the exact feature head to master.
