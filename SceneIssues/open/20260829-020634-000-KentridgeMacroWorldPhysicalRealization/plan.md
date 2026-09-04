# Plan

## Acceptance authority
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force graph while delivering physical settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded cost. Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`; current module-local validation-scene guidance is authoritative.

## Ownership and validation surfaces
- `Assets/Game/WorldBuilder/Generation` / `MountingForce.WorldGen.Voxel`: owns reusable physical planning and voxel realization. `Validation/MacroPhysicalWorld/MacroPhysicalWorldValidation.unity` now builds an independent semantic graph/physical catalogue, transfers it into `ShowcaseWorld`, and renders it through `RenderingComposition`; no validation-only geometry substitutes for production voxels.
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
- Survey composition now establishes one camera position before slice streaming and pins `CharacterMotor.EyePosition` to the rendered survey position, matching the shipped auto-survey contract.
- Experiment 040 proved and corrected shared GPU count-batch lane starvation with one round-robin seal authority. Exact run `33816510783` validates the independent fairness regression, but its 180-second player still stalls after Moordell.

## Current demonstrated blocker and root-cause gate
The continuing player failure is localized before extraction admission, not in count-batch sealing. `phases=0x2` is a bitmask representing active request phase 1 (`_stageAdmissionPending`). In exact post-master run `33824047466` on source `a01e8bd3e2b8659225ebb7373516e7e0ebe57681`, the workflow and 180-second process are green, yet artifact `9920411113` reaches only `MACROEVIDENCE content-ready target=moordell`; there is no `capture-ready`, no later macro target, and no dedicated macro evidence file.

The first exact attempt to run the new relocation discriminator, request `cb2b7919142402fccbcbe03f9fb8c5a3576fa086`, completed red before exercising GPU liveness. This was a validation-harness incompatibility, not product evidence: repository module validation intentionally sets `VOXEL_DISABLE_GPU_CUTOVER=1` for isolated PlayMode CPU-baseline tests, while the relocation discriminator requires production GPU cutover. Do not retry that exact request unchanged and do not weaken the repository-wide CPU-baseline policy.

The assignment-local correction keeps global CI behavior intact:
- `GpuSurfaceMirrorRelocationRequestedValidationTests.DistantRelocationExecutesProductionGpuLivenessRegression` clears the environment only around the requested relocation test and delegates to the existing discriminator, restoring it afterward.
- `GpuSurfaceMirrorRelocationValidationBootstrap` clears the same flag BeforeSceneLoad only for the dedicated GPU module-validation scene.
The next exact request therefore targets the adapter, not the old direct test.

Because the same acceptance symptom survived materially different coverage-radius, survey-demand, and count-batch corrections, no further renderer production change is allowed until the independent relocation repro confirms the next root cause. A static candidate remains intentionally unselected: `GpuSurfaceMirrorCoordinator.ApplyChange` may remove a non-active changed block from ready/mixed tracking while retaining its old mirror entry; cancelled recovery could then leave a slot untracked for eviction. The exact relocation repro must distinguish that bookkeeping leak from capacity pressure before any production edit.

Rejected alternatives remain: do not widen Kentridge load radius, weaken `HasCompletePublishedNearSurfaceCoverage`, increase extraction concurrency/device budgets, hardcode settlement-specific renderer policy, substitute top-level showcase scenes for module validation, replace production validation visuals with primitives, or rerun a known acceptance-red source as proof.

## Cost / blast radius
- Horizontal residency remains radius 3 = 29 X/Z columns; no horizontal-interest or device-budget increase.
- Existing physical baseline: 20 hard routes, 824 route tiles, 5 constrained routes, 1,090 solve steps, 16 generic buildings, max road rise 2 voxels, road step 30 dm, Rossdam water depth 24 voxels, water primitive bounding cells 9,281,584.
- Latest Moordell aligned-survey evidence reports `horizontalColumns=29`, `residentInRadius=58`, `baselineResident=58`, `featureVerticalExtra=0`.
- Partial runtime context only: median FPS 103.9, median mean-frame 9.61 ms, worker-p95 median 3.018 ms, admission-total median 1.1605 ms, ~26.99 MiB cumulative render-arena uploads over 114 calls. Final multi-target convergence, vertical resident/generated deltas, process/managed/native/GPU memory, and completed steady-state measurements remain required.

## Current branch state
- Latest repository guidance/master was reconciled through PR #258; merge `0d458e976d432b8a55ee02316bf6635afc3aecaa` includes master `56b28f3abdac8cbbd346a3cb29acef43da029806` before the final validation-surface work.
- Current feature work after that merge includes all four required module-local validation paths, the requested-test GPU adapter, and the WorldBuilder fidelity correction. The exact head must be re-read immediately before constructing the next targeted-CI transport request.
- `origin/master` has since advanced to `e18efe82ce1b4aa069031165d40bac14a9269412` via unrelated GameSystem16 persistence. Do not churn the current root-cause discriminator for that unrelated change; re-fetch/merge then-current master and exact-revalidate at the required final gate.
- `ci-test/fixes/agent-6` remains the only targeted-CI transport. Never replace a queued/running request.

## Next gates
1. Exact-SHA validate `VoxelEngine.Tests.PlayMode.GpuSurfaceMirrorRelocationRequestedValidationTests.DistantRelocationExecutesProductionGpuLivenessRegression` through `ci-test/fixes/agent-6`, with repository-derived module validation and the required SceneIssue replay. Do not replace the request while queued/running.
2. If the relocation repro fails, use ready/pending/demand/active/mixed-resident/capacity/completion diagnostics to prove the mirror-admission root cause before the smallest production correction. If it passes, reject the static candidate and isolate a more faithful boundary before changing renderer behavior.
3. Require all four module-local player scenarios to pass on exact source and inspect their capture/log evidence directly. Scene existence alone is not acceptance.
4. Require strict macro evidence to progress through Moordell, Rossdam/lake, Fairy, Orc, Southern Ridge/pass, network overview, and real CharacterMotor road traversal. Inspect full-resolution named evidence; generic harness screenshots do not satisfy acceptance.
5. Quantify final multi-target residency/convergence, feature work, CPU/FPS/render/far-field telemetry, and process/managed/native/GPU memory footprint against budgets.
6. Re-fetch then-current `origin/master` before closure, merge it into `fixes/agent-6` if advanced, and revalidate affected exact-SHA work.
7. Do not close until every `tasks.md` checkbox and acceptance criterion is complete. Then move directly `open -> closed`, set `status=fixed` and `resolvedUtc`, populate resolution metadata from the verified result, and promote only through a final feature-to-master PR with auto-merge. Never push the exact `fixes/agent-6` head directly to `origin/master`; never use `pending/`.
