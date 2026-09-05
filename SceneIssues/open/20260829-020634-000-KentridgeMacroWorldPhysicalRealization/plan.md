# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Latest evidence and root cause
Exact run `33943012273` validated source `a90fd76933b2269fc3aea660d137c196a9882987` against master `51797c954490425964e602d6bb2252a0d7a7c5aa`. Persistent tests and the requested count-batch fairness regression passed. Both Kentridge players reached real macro content without forbidden exceptions; the 180-second replay reached `content-ready target=moordell columns=4`, but strict capture readiness never completed. Durable evidence still showed large checkerboard terrain holes, so coverage assertions must not be weakened.

The replay ended near `missingVisible=102`, `flight=8`, `phases=0x2`, with phase-2 requests occasionally 6.5–8.5 seconds old. `batchArenaWait=0` falsifies a fence that remains blocked for the entire stall, and production scheduler/sealer use the same `Time.frameCount`.

The demonstrated invariant violation is GPU extraction ownership lifetime. `TryBeginExtraction` increments the eight-slot extraction count and protects the sampled mirror footprint. `SealCountBatch` submits count/write/publication work and creates the authoritative graphics fence, but immediately marks each context `CompletePagedBatch`. The slot/readers are then released only later when that worker happens to poll phase 9 and `Release` calls `EndExtraction`. This makes lifetime depend on CPU shard polling rather than GPU completion: a frequently-polled worker can drop protection before the fence passes, while a rarely-polled worker can retain a slot long after the fence passes. The latter matches the artifact's saturated `flight=8`, phase-2 ages, and slow publication.

Selected correction: transfer each successfully submitted paged extraction's slot/mirror-reader ownership to the coordinator's in-flight graphics fence. Signal paged completion and call `EndExtraction` exactly when that fence passes; reset clears transferred records without double-release. Preserve all existing budgets, concurrency limits, coverage rules, streaming radius, and acceptance gates. The earlier active-worker ordering experiment is superseded and removed rather than shipped.

## Remaining gates
1. Exact-SHA targeted CI: behavioral fence-owned extraction-lifetime regression, existing GPU relocation/fairness coverage, repository-derived module validation, and 180-second SceneIssue replay.
2. Inspect full-resolution built-player evidence; require all settlements, Rossdam water/constrained route, Southern Ridge/pass, network overview, differentiated terrain, and real CharacterMotor traversal.
3. Record per-target convergence plus FPS/CPU/GPU/streaming and process/managed/native/GPU memory against existing budgets.
4. Merge then-current `origin/master`, revalidate the exact merged feature SHA as required, complete every task/acceptance item, move only this issue `open -> closed`, then promote through PR + auto-merge and monitor the required PR gate until merged.
