# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Latest evidence and hypotheses
Exact run `33932977686` validated source `a37da26d7b35505507ac1271eb5866f568d4c414`. Persistent repository-derived tests passed. The 180-second Kentridge replay proved the macro ownership fix: runtime catalogue had 480 definitions including all acceptance towns, 20 roads/824 route tiles, ridge and water, and the prior legacy-input exception storm was gone. It remained closure-red because Moordell survey content did not fully converge and the requested GPU-liveness regression was blocked before its assertions by `VoxelShowcase.HandleKeys()` calling legacy `UnityEngine.Input` under Input-System-only settings.

Hypothesis A: macro definitions are still absent. Falsified by the exact replay catalogue/evidence logs.

Hypothesis B: stale FIFO terrain/feature queues starve Moordell. Falsified: both wanted terrain and pending features are rebuilt/selected nearest-first. The actual validation mismatch is a fixed elevated survey camera spanning four settlement columns; one diagonal building column remained behind nearer normal streaming demand.

Selected corrections:
1. `VoxelEngine.Showcase` now owns a scene-local Input-System bridge and package assembly reference; production `VoxelShowcase` reaches the requested GPU path without changing global input settings or suppressing errors.
2. Validation-only `KentridgeMacroWorldContentDemandDriver` supplies bounded deterministic CharacterMotor demand at the first unsettled authored building centre while an elevated acceptance settlement survey is active. It uses the same physical plan, normal `ShowcaseWorld` streaming budgets/queues, collision, rasterization and rendering; it never force-generates a region or widens residency.

## Remaining gates
1. Exact-SHA targeted CI: requested GPU liveness regression, repository-derived module validation/all four owned module players, and 180-second SceneIssue replay must all pass.
2. Inspect full-resolution built-player evidence. Require readable Moordell/Rossdam/Fairy/Orc settlements, substantial Rossdam water + constrained route, Southern Ridge/pass, macro-network overview, and real CharacterMotor traversal with no runtime exceptions.
3. Record final per-target convergence, vertical residency, FPS/CPU/GPU/streaming and process/managed/native/GPU memory against existing budgets.
4. Merge then-current `origin/master`, revalidate the exact merged feature SHA, complete every task/acceptance item, move only this issue `open -> closed`, then promote through PR + auto-merge and monitor required PR checks until merged.
