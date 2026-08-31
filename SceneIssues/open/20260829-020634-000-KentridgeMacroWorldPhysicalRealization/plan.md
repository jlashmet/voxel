# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Keep the source-backed Mounting Force/Kentridge graph authoritative through shared WorldBuilder APIs; physically realize settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded road slopes, authoritative storage, substantial lake/ridge, explicit constrained routes, and two-stage presentation readiness.
- `RefreshPending` is isolated in a residency partial, and already-accepted X/Z columns additionally queue only feature-bearing Y-regions from semantic catalogue bounds. No horizontal interest radius, device budget, scheduler, or scene-specific preload policy changes.
- Reuse audit is complete: shared physical intent/planner/physical catalogue/water catalogue contain semantic inputs and no Kentridge/Rossdam policy. Existing generic `a`/`b` blocked-water fixture independently proves planner rejection and semantic `GoAround` reuse without Kentridge adapters.
- Exact run `33346099006` proves ordinary production `ShowcaseWorld.StepStreaming` makes a real authored upper feature Y-region resident without moving the viewer into that layer.
- Experiment 018 found presentation readiness could become true before those authored upper layers were feature-published. The shared readiness predicate now uses the same cached semantic feature-layer query as residency.
- Exact source `9d51fb9a947af76d0b8005c35288a7007dd6d9e6`, CI wrapper `96e082564b4a6a13f34b4ea4693a06be215365ba`, run `33354287850`: the lower-ready/upper-missing readiness race is 1/1 green and the supported 60 s built-player step is technically clean.
- The same run remains closure-red visually/timing-wise: Rossdam still has essentially one unmistakable complete building, the lake reads as a thin distant strip, and Fairy/Orc/ridge/network are not all captured by 60 s.
- The previous generic t=50 survey ownership hypothesis is rejected by exact telemetry; dedicated macro evidence retains Rossdam demand and converges. Do not make another ownership fix from that log alone.
- Generic physical planning still requires four real Rossdam blockouts; the settlement survey spans their footprint, so the surviving one-building visual symptom requires a production/storage-versus-render discriminator.

## Current discriminator — experiment 019
The first experiment-019 regression on exact source `aa4952b5706a9bf706ff9b955d36aa39f43a6819` failed in run `33357649096` before any storage probe: the name-prefix selector matched zero buildings. This is a test-construction failure, not evidence that production volumes are absent.

The corrected discriminator at source `74001f9d98041eb56dbce310f8caa1341221ef90` removes catalogue-name matching entirely. It builds the real `TopDownWorldPhysicalPlan`, selects Rossdam by semantic node id, requires its four `TopDownWorldBuildingBlockoutPlan`s, matches each planned X/Z centre to exactly one production `FeatureKind.Structure` placement, generates every intersecting 3D region through normal `FeatureGeneration.GenerateRegion`, and bounded-scans authoritative storage for production foundation/timber/roof occupancy. For each building it records voxel count, occupied min/max Y and span, and requires occupancy to begin at the production grounded placement and reach at least the semantic wall height.

No production world-generation, residency, scheduler, render, camera, or replay policy changed for this discriminator.

## Remaining gates
1. Run the corrected experiment-019 Rossdam authoritative-volume regression through `ci-test/fixes/agent-6` without replacing queued/running work.
2. Classify the result before any further visual fix: if all four volumes are healthy/grounded, stop modifying voxelization/readiness and investigate downstream render publication/framing; if a volume is absent/misgrounded, fix only that production owner.
3. Re-run the supported 60-second module-local `KentridgePlayableSlice` evidence. Rossdam/Fairy/Orc must show four readable authored buildings; the lake must read as substantial water plus constrained route; the final network target must be captured.
4. If correctness still exceeds the fixed 60-second cap, quantify phase timing and make only an acceptance-required validation-sequence correction; do not weaken readiness, pre-generate targets, widen residency, or extend replay duration.
5. Quantify added feature Y-region residency and final CPU/FPS/memory/streaming/render/far-field cost against budgets.
6. Re-fetch current master immediately before final exact-SHA validation. Only after every checkbox is green: move `open -> closed`, set fixed metadata, merge current master, and non-force push that exact feature head to master; if master advances, fetch/merge/retry.
