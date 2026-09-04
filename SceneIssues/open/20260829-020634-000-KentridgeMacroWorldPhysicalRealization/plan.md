# Plan

## Acceptance authority
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force graph while delivering physical settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded cost. Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`; current module-local validation-scene guidance is authoritative.

## Ownership and validation surfaces
- `Assets/Game/WorldBuilder/Generation` / `MountingForce.WorldGen.Voxel`: owns reusable physical planning and voxel realization. `Validation/MacroPhysicalWorld/MacroPhysicalWorldValidation.unity` builds an independent semantic graph/physical catalogue, transfers it into `ShowcaseWorld`, and renders it through `RenderingComposition`; no validation-only geometry substitutes for production voxels.
- `Assets/Game/Composition/Showcase` / `Game.Composition.Showcase`: owns feature-aware vertical residency/readiness. `Validation/FeatureResidency/FeatureResidencyValidation.unity` uses a real authored multi-height catalogue placement and ordinary `ShowcaseWorld` streaming without widening horizontal residency.
- `Assets/Game/Composition/Kentridge/Playable/SceneRuntime` / `Game.Kentridge.PlayableSlice`: owns Kentridge runtime composition, macro catalogue handoff, streaming alignment, and evidence sequencing. `Validation/KentridgeMacroWorld/KentridgeMacroWorldValidation.unity` hosts the real `KentridgePlayableSlice`; a scene-scoped bootstrap checks source-backed Moordell/lake route relationships and attaches the production macro evidence driver.
- `Assets/VoxelEngine/Rendering`: owns shared mirror/extraction/publication liveness. `Validation/GpuSurfaceMirrorRelocation/GpuSurfaceMirrorRelocationValidation.unity` exercises the real GPU voxel path and controlled distant relocation. Its scene-scoped BeforeSceneLoad bootstrap enables the GPU cutover only for this dedicated GPU validation player.
- `Game.WorldBuilder.Api`, `Game.WorldBuilder.Runtime`, and `Game.WorldBuilder.Voxel` changes are semantic/config/catalogue adapters without an independent scene lifecycle. Keep module-local unit/behavioral coverage and validate player-visible composition through the WorldGen/Kentridge scenes.
- `VoxelEngine.Structures` changes (`FeatureGenerationTrace` / `FeatureRegionBuild` bookkeeping) are support instrumentation rather than independent scene behavior; module-local behavioral/unit coverage remains the owned validation surface.

## Proven implementation
- Production physical planning covers 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, constrained-route solutions, Rossdam Lake/ridge realization, bounded slopes, storage, and feature-aware vertical residency/readiness. Reuse is proven by independent non-Kentridge blocked-water, spatial-reservation, and synthetic generic-blockout fixtures.
- The playable catalogue ownership fix keeps authority scene-local: the intended Kentridge catalogue consumes the one-shot macro selection before temporary Showcase bootstrap. Runtime evidence reports 480 macro-aware definitions.
- The demonstrated generic-blockout cost defect was corrected with bounded perimeter plinths and hollow wall shells. The independent 100x80x60 fixture reduced foundation work 64.3%, timber-body work 82.8%, and combined foundation+timber work 79.7% while preserving grounding/footprint/material/support/collision/bounds.
- The Kentridge-only streaming coverage policy keeps radius-3 horizontal residency and strict coverage semantics, but narrows the near-surface completeness ring to the conservative guaranteed 102.4 m disk derived from the discrete 29-column lattice.
- Survey composition establishes one camera position before slice streaming and pins `CharacterMotor.EyePosition` to the rendered survey position, matching the shipped auto-survey contract.
- Experiment 040 proved and corrected shared GPU count-batch lane starvation with one round-robin seal authority. Exact run `33816510783` validates the independent fairness regression, but its 180-second player still stalls after Moordell.

## Current demonstrated blocker and root-cause gate
Exact run `33882801702` used feature source `8043896ea4b179a80de3c6ae83ef6328543f6f97` (transport `26319962ee914db4bab3d1ea28025f567f5be006`). The requested GPU-enabled distant-relocation regression passed, so reject the generic hypothesis that a 384 m relocation permanently strands every mirror worker. The run is still closure-red: automatic module validation fails in the Kentridge-owned player before its required CharacterMotor marker, and the 180-second SceneIssue replay reaches the local CharacterMotor walk then remains on the first Moordell survey instead of advancing through strict multi-target publication evidence.

The Kentridge replay supplies a denser and more faithful boundary than the generic relocation scene. Immediately after the Moordell survey relocation, eight GPU requests remain in admission (`phases=0x2`) with zero active extractions. Their live step-2 cache footprints are nearly disjoint (`demandUnique` samples about 45.9k-46.0k blocks), the pending set is entirely live (`pendingStale=0`), and ready tracking reaches its 65,536-block ceiling. `coreAbsent=1872` is cumulative, not a current absent-block gauge: it rises while Storage catches the relocation, then remains unchanged while live recovery continues, so it does not prove permanent Storage non-residency.

Because the same acceptance symptom survived materially different coverage-radius, survey-demand, and count-batch fixes, do not make another renderer production edit from the aggregate counters alone. The next discriminator is validation-only: extend the existing Kentridge mirror-demand diagnostic with slot capacity, resident/pinned mixed bricks, no-slot refusals, evictions, directory refusals, and live-vs-inactive mixed-ready counts. This distinguishes true dense-working-set saturation from recovery throughput or untracked-slot bookkeeping without changing renderer behavior. If the result demonstrates one of those failures, implement only the smallest production correction and add an independent regression for that exact boundary.

A previously noted static candidate remains unselected until the diagnostic proves it: changed/cancelled recovery can potentially leave mirror residency that is no longer represented by ready/mixed tracking. Conversely, raw footprint size alone is not sufficient evidence for capacity exhaustion: the 96 MiB minimum mirror is 47,662 mixed slots, while the current eight-footprint union is below that number and only mixed blocks consume slots.

Rejected alternatives remain: do not widen Kentridge load radius, weaken `HasCompletePublishedNearSurfaceCoverage`, increase extraction concurrency/device budgets, hardcode settlement-specific renderer policy, substitute top-level showcase scenes for module validation, replace production validation visuals with primitives, or rerun a known acceptance-red source as proof.

## Cost / blast radius
- Horizontal residency remains radius 3 = 29 X/Z columns; no horizontal-interest or device-budget increase.
- Existing physical baseline: 20 hard routes, 824 route tiles, 5 constrained routes, 1,090 solve steps, 16 generic buildings, max road rise 2 voxels, road step 30 dm, Rossdam water depth 24 voxels, water primitive bounding cells 9,281,584.
- Latest aligned-survey evidence reports `horizontalColumns=29`, `residentInRadius=58`, `baselineResident=58`, `featureVerticalExtra=0`.
- Partial runtime context only: median FPS 103.9, median mean-frame 9.61 ms, worker-p95 median 3.018 ms, admission-total median 1.1605 ms, ~26.99 MiB cumulative render-arena uploads over 114 calls. Final multi-target convergence, vertical resident/generated deltas, process/managed/native/GPU memory, and completed steady-state measurements remain required.

## Current branch state
- `fixes/agent-6` was last exact-tested at `8043896ea4b179a80de3c6ae83ef6328543f6f97`. Validation-only dense mirror diagnostics are now being added on top of that source before another production correction.
- Current `origin/master` is `283b512cf6dac4feba5f1cfd5b9d79ef0b3075e8`. The current root-cause work does not depend on that unrelated advancement; follow the feature guide and merge then-current master at the final gate, or earlier only if it becomes an actual prerequisite.
- `ci-test/fixes/agent-6` remains the only targeted-CI transport. Never replace a queued/running request.

## Next gates
1. Exact-SHA run the enhanced Kentridge dense-demand discriminator through `ci-test/fixes/agent-6` with repository-derived module validation and the required SceneIssue replay. Do not replace the request while queued/running.
2. Use slot/residency/refusal/eviction plus live-demand diagnostics to choose one demonstrated mirror-admission root cause. If no root cause is demonstrated, isolate a smaller faithful repro before another production change.
3. After the smallest demonstrated correction, require strict macro evidence to progress through Moordell, Rossdam/lake, Fairy, Orc, Southern Ridge/pass, network overview, and real CharacterMotor road traversal.
4. Require all four module-local player scenarios to pass on exact source and inspect their capture/log evidence directly. Scene existence alone is not acceptance.
5. Quantify final multi-target residency/convergence, feature work, CPU/FPS/render/far-field telemetry, and process/managed/native/GPU memory footprint against budgets.
6. Re-fetch then-current `origin/master` before closure, merge it into `fixes/agent-6` if advanced, and revalidate affected exact-SHA work.
7. Do not close until every `tasks.md` checkbox and acceptance criterion is complete. Then move directly `open -> closed`, set `status=fixed` and `resolvedUtc`, populate resolution metadata from the verified result, and promote only through a final feature-to-master PR with auto-merge. Never push the exact `fixes/agent-6` head directly to `origin/master`; never use `pending/`.
