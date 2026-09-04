# Far-World Visibility Implementation / Validation Plan

## Implemented slices

1. Guarantee analytic far-terrain coverage through explicit snapped-extent math and keep startup fallback until authoritative coverage is contiguous.
2. Share deterministic world-space terrain presentation families/detail between near/far presentation without increasing voxel residency.
3. Derive renderer-neutral semantic feature presentation from canonical planning/catalogue facts before physical voxel realization.
4. Query/select semantic far features through stable IDs and shared policy, then draw cached/batched geometry through VoxelEngine Rendering.
5. Keep `FarFieldStructureStore` only for authored terrain/surface deviation plus anonymous/arbitrary voxel fallback; suppress semantic positive silhouettes when their canonical presentation source exists.
6. Aggregate dense structures/vegetation into bounded HLOD representations while retaining existing structure/tree state as authority.
7. Compose the shared contracts in both Showcase and `KentridgePlayableSlice`.

## Validation gates

- Behavioral EditMode tests cover coverage math, fallback retirement, semantic selection, deterministic aggregation/order, handoff stability, persistent state, vegetation/canopy invalidation, terrain material presentation, and semantic-vs-legacy fallback ownership.
- Module-owned standalone scene: `Assets/VoxelEngine/Rendering/Validation/FarWorld/FarWorldVisibilityDemo.unity` stages near/handoff plus 1/3/6/8/10/12 km views using production far-terrain shader and production far-feature renderer.
- Assembled-game standalone gate: canonical `Assets/Scenes/KentridgePlayableSlice.unity` validates reuse/integration and startup/runtime behavior.
- Visual evidence is inspected directly from built-player artifacts; test success alone is not visual acceptance.
- `FarWorldBudgetProbe` records final built-player frame timing, memory and semantic batching/cache counts after warmup. Values are compared with `specs/001-destructible-voxel-engine/device-matrix.md` and recorded in the SceneIssue ledger.

## Closure sequence

1. Merge current `origin/master` into `fixes/agent-7` (never rebase the feature work).
2. Run targeted CI from `ci-test/fixes/agent-7` with a request commit whose parent is the exact feature head. Never replace queued/running requests.
3. Inspect standalone logs/screenshots and budget output directly.
4. Reconcile every `tasks.md` checkbox with concrete code/test/player evidence; record any conditional task as not required only where evidence demonstrates the condition did not trigger.
5. Set SceneIssue status/resolution metadata and move the directory directly from `open/` to `closed/`.
6. Re-sync master if it advanced, then open the final `fixes/agent-7 -> master` PR, enable auto-merge, and wait for required PR affected tests including canonical Kentridge standalone validation.
7. Completion requires the PR merged and the closed SceneIssue present on master.
